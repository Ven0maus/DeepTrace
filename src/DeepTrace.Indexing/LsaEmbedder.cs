using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace DeepTrace.Indexing;

/// <summary>
/// Latent Semantic Analysis engine for code search.
/// Builds a term-document matrix from method bodies, extracts top-K latent dimensions
/// via power-iteration SVD, and projects queries/documents into the same latent space
/// for cosine similarity scoring.
/// </summary>
public static class LsaEmbedder
{
    public const int Dimensions = 50;

    /// <summary>
    /// Maximum number of entries in the token cache. Evicts oldest on overflow.
    /// </summary>
    private const int TokenCacheMaxSize = 4096;

    /// <summary>
    /// LRU token cache. Keys are method bodies, values are token arrays.
    /// Access is serialized via lock to maintain LRU ordering.
    /// </summary>
    private static readonly ConcurrentDictionary<string, LinkedListNode<string>> _tokenCacheNodes = new(StringComparer.OrdinalIgnoreCase);
    private static readonly LinkedList<string> _tokenCacheOrder = new();
    private static readonly object _tokenCacheLock = new();

    /// <summary>
    /// English stopword set — common terms that lack discriminative power across code corpora.
    /// </summary>
    private static readonly HashSet<string> Stopwords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "the", "and", "or", "but", "in", "on", "at", "to", "for",
        "of", "with", "by", "from", "is", "are", "was", "were", "be", "been",
        "have", "has", "had", "do", "does", "did", "will", "would", "could",
        "should", "may", "might", "shall", "can", "this", "that", "these",
        "those", "it", "its", "not", "no", "if", "then", "else", "as",
        "load", "data", "get", "set", "create", "update", "delete", "save",
        "call", "handle", "process", "perform", "execute",
        "provide", "enable", "disable", "initialize", "configure", "validate",
        "parse", "format", "convert", "transform", "build", "make", "find",
        "search", "query", "read", "write", "open", "close", "start", "stop",
        "add", "remove", "insert", "remove", "clear", "reset", "apply",
        "show", "hide", "display", "render", "draw", "click", "press",
        "select", "toggle", "send", "receive", "connect", "disconnect",
        "throw", "catch", "try", "finally", "using", "new", "null",
    };

    /// <summary>
    /// Tokenizes text. Preserves single-char tokens (important for game APIs like Vector3, I/A prefixes).
    /// Filters English stopwords that carry no discriminative signal.
    /// </summary>
    public static string[] Tokenize(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return [];

        // Check cache under lock
        lock (_tokenCacheLock)
        {
            if (_tokenCacheNodes.TryGetValue(text, out var node))
            {
                _tokenCacheOrder.Remove(node);
                _tokenCacheOrder.AddLast(node.Value);
                return node.Value.Split(new[] { ' ', '.', '_', '-', '(', ')', ':', ',' },
                    System.StringSplitOptions.RemoveEmptyEntries)
                    .Where(t => t.Length >= 1)
                    .Where(t => !Stopwords.Contains(t))
                    .Distinct()
                    .ToArray();
            }
        }

        var tokens = text.ToLowerInvariant().Split(new[] { ' ', '.', '_', '-', '(', ')', ':', ',' },
                System.StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length >= 1)
            .Where(t => !Stopwords.Contains(t))
            .Distinct()
            .ToArray();

        // Insert into LRU cache
        lock (_tokenCacheLock)
        {
            if (_tokenCacheOrder.Count >= TokenCacheMaxSize)
            {
                var oldest = _tokenCacheOrder.First;
                _tokenCacheOrder.RemoveFirst();
                _tokenCacheNodes.TryRemove(oldest.Value, out _);
            }
            var node = _tokenCacheOrder.AddLast(text);
            _tokenCacheNodes[text] = node;
        }

        return tokens;
    }

    /// <summary>
    /// Build LSA embeddings from method bodies and store them in the database.
    /// Stores: method embeddings, vocabulary, and principal components matrix.
    /// </summary>
    public static void BuildAndStore(string dbPath, IReadOnlyList<(string id, string body)> documents)
    {
        if (documents.Count == 0)
            return;

        // Step 1: Tokenize and build vocabulary
        var vocab = new Dictionary<string, int>();
        var docTermCounts = new List<Dictionary<string, int>>();
        int m = documents.Count;

        foreach (var (id, body) in documents)
        {
            var terms = Tokenize(body);
            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var t in terms)
                counts[t] = counts.TryGetValue(t, out var c) ? c + 1 : 1;
            docTermCounts.Add(counts);
            foreach (var term in counts.Keys)
                vocab.TryAdd(term, vocab.Count);
        }

        int n = vocab.Count;
        if (n < 20 || m < 5)
            return;

        // Step 2: Build TF-IDF sparse matrix
        // Compute document frequency for IDF
        int[] docFreq = new int[n]; // how many documents contain each term
        foreach (var counts in docTermCounts)
        {
            foreach (var term in counts.Keys)
            {
                if (vocab.TryGetValue(term, out int ti))
                    docFreq[ti]++;
            }
        }

        var docSparse = new List<Dictionary<int, double>>(m);
        for (int d = 0; d < m; d++)
        {
            var sparse = new Dictionary<int, double>();
            var total = docTermCounts[d].Values.Sum();
            if (total == 0)
            {
                docSparse.Add(sparse);
                continue;
            }

            foreach (var (term, count) in docTermCounts[d])
            {
                if (!vocab.TryGetValue(term, out int ti))
                    continue;
                // Log-normalized TF * IDF
                double tf = Math.Log(1.0 + count) / Math.Log(1.0 + total);
                // IDF: log(N / df) + 1 (smoothed, ensures positive weights)
                double idf = Math.Log(m / (double)docFreq[ti]) + 1.0;
                sparse[ti] = tf * idf;
            }

            docSparse.Add(sparse);
        }

        // Step 3: Power iteration to compute top-K principal components
        int k = Math.Min(Dimensions, n);
        var components = PowerIteration(docSparse, n, k);

        // Step 3b: Compute per-term IDF and store it
        double[] idfValues = new double[n];
        for (int ti = 0; ti < n; ti++)
            idfValues[ti] = Math.Log(m / (double)docFreq[ti]) + 1.0;

        // Step 4: Project each document into latent space
        var embeddings = new double[m][];
        for (int d = 0; d < m; d++)
        {
            var emb = new double[k];
            foreach (var (ti, tf) in docSparse[d])
            {
                for (int c = 0; c < k; c++)
                    emb[c] += tf * components[c, ti];
            }
            embeddings[d] = emb;
        }

        // Step 5: Store everything in SQLite
        using var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={dbPath};Pooling=false");
        conn.Open();

        // Create tables if they don't exist (for standalone usage)
        conn.Cmd(@"CREATE TABLE IF NOT EXISTS method_embeddings (
            method_id TEXT PRIMARY KEY,
            embedding BLOB NOT NULL
        )").ExecuteNonQuery();
        conn.Cmd(@"CREATE TABLE IF NOT EXISTS lsa_vocab (
            term TEXT PRIMARY KEY,
            ""index"" INTEGER NOT NULL
        )").ExecuteNonQuery();
        conn.Cmd(@"CREATE TABLE IF NOT EXISTS lsa_components (
            dim INTEGER PRIMARY KEY,
            embedding BLOB NOT NULL
        )").ExecuteNonQuery();
        conn.Cmd(@"CREATE TABLE IF NOT EXISTS lsa_idf (
            term TEXT PRIMARY KEY,
            idf_value REAL NOT NULL
        )").ExecuteNonQuery();

        using var tx = conn.BeginTransaction();

        // Clear old data
        conn.Cmd("DELETE FROM method_embeddings", tx).ExecuteNonQuery();
        conn.Cmd("DELETE FROM lsa_vocab", tx).ExecuteNonQuery();
        conn.Cmd("DELETE FROM lsa_components", tx).ExecuteNonQuery();
        conn.Cmd("DELETE FROM lsa_idf", tx).ExecuteNonQuery();

        // Store vocabulary
        var insertVocab = conn.Cmd("INSERT INTO lsa_vocab (term, \"index\") VALUES (@term, @index)", tx);
        foreach (var (term, idx) in vocab)
        {
            insertVocab.P(new (string name, object value)[] { ("@term", term), ("@index", idx) }).ExecuteNonQuery();
        }

        // Store embeddings
        var insertEmb = conn.Cmd("INSERT INTO method_embeddings (method_id, embedding) VALUES (@id, @emb)", tx);
        for (int d = 0; d < m; d++)
        {
            insertEmb.P(new (string name, object value)[]
            {
                ("@id", documents[d].id),
                ("@emb", PackEmbedding(embeddings[d])),
            }).ExecuteNonQuery();
        }

        // Store principal components (k x n matrix as packed float32)
        var insertComp = conn.Cmd("INSERT INTO lsa_components (dim, embedding) VALUES (@dim, @emb)", tx);
        var compRow = new double[components.GetLength(1)];
        for (int c = 0; c < k; c++)
        {
            for (int j = 0; j < compRow.Length; j++)
                compRow[j] = components[c, j];
            insertComp.P(new (string name, object value)[]
            {
                ("@dim", c),
                ("@emb", PackEmbedding(compRow)),
            }).ExecuteNonQuery();
        }

        // Store IDF values
        var insertIdf = conn.Cmd("INSERT INTO lsa_idf (term, idf_value) VALUES (@term, @idf)", tx);
        foreach (var (term, idx) in vocab)
        {
            insertIdf.P(new (string name, object value)[]
            {
                ("@term", term),
                ("@idf", idfValues[idx]),
            }).ExecuteNonQuery();
        }

        tx.Commit();
    }

    /// <summary>
    /// Load stored embeddings from the database.
    /// </summary>
    public static IReadOnlyDictionary<string, double[]> LoadStored(string dbPath)
    {
        var result = new Dictionary<string, double[]>();

        using var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={dbPath};Pooling=false");
        conn.Open();

        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT method_id, embedding FROM method_embeddings";
        using var reader = cmd.ExecuteReader();

        while (reader.Read())
        {
            var id = reader.GetString(0);
            var blob = (byte[])reader.GetValue(1);
            result[id] = UnpackEmbedding(blob);
        }

        return result;
    }

    /// <summary>
    /// Load the principal components matrix from the database.
    /// </summary>
    public static double[,] LoadComponents(string dbPath)
    {
        int n = 0;

        using var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={dbPath};Pooling=false");
        conn.Open();

        // Get vocabulary size from vocab count
        var sizeCmd = conn.Cmd("SELECT MAX(\"index\") + 1 FROM lsa_vocab");
        var maxIdx = sizeCmd.ExecuteScalar() as long?;
        if (maxIdx == null || maxIdx.Value == 0)
            return new double[Dimensions, 0];

        n = (int)maxIdx.Value;
        var components = new double[Dimensions, n];

        var loadCmd = conn.Cmd("SELECT dim, embedding FROM lsa_components ORDER BY dim");
        using var reader = loadCmd.ExecuteReader();

        while (reader.Read())
        {
            int dim = reader.GetInt32(0);
            var blob = (byte[])reader.GetValue(1);
            var emb = UnpackEmbedding(blob);
            for (int i = 0; i < emb.Length && i < n; i++)
                components[dim, i] = emb[i];
        }

        return components;
    }

    /// <summary>
    /// Load the vocabulary from the database.
    /// </summary>
    /// <summary>
    /// Load per-term IDF values from the database.
    /// </summary>
    public static IReadOnlyDictionary<string, double> LoadIdf(string dbPath)
    {
        var idf = new Dictionary<string, double>();

        using var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={dbPath};Pooling=false");
        conn.Open();

        var cmd = conn.Cmd("SELECT term, idf_value FROM lsa_idf");
        using var reader = cmd.ExecuteReader();

        while (reader.Read())
            idf[reader.GetString(0)] = reader.GetDouble(1);

        return idf;
    }

    public static IReadOnlyDictionary<string, int> LoadVocabulary(string dbPath)
    {
        var vocab = new Dictionary<string, int>();

        using var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={dbPath};Pooling=false");
        conn.Open();

        var cmd = conn.Cmd("SELECT term, \"index\" FROM lsa_vocab ORDER BY \"index\"");
        using var reader = cmd.ExecuteReader();

        while (reader.Read())
            vocab[reader.GetString(0)] = reader.GetInt32(1);

        return vocab;
    }

    /// <summary>
    /// Compute a query embedding by projecting the query into the latent space defined by the principal components.
    /// Uses the same TF-IDF weighting as document embeddings.
    /// </summary>
    public static double[] ComputeQueryEmbedding(string query, IReadOnlyDictionary<string, int> vocab, double[,] components)
        => ComputeQueryEmbedding(query, vocab, components, idf: null);

    /// <summary>
    /// Compute a query embedding with per-term IDF values for proper weighting.
    /// </summary>
    public static double[] ComputeQueryEmbedding(string query, IReadOnlyDictionary<string, int> vocab, double[,] components,
        IReadOnlyDictionary<string, double>? idf)
    {
        var terms = Tokenize(query);
        var termCounts = terms.GroupBy(t => t).ToDictionary(g => g.Key, g => g.Count());

        int k = components.GetLength(0);
        var embedding = new double[k];
        double totalTerms = termCounts.Values.Sum();
        foreach (var (term, count) in termCounts)
        {
            if (!vocab.TryGetValue(term, out int ti))
                continue;
            // Same log-normalized TF as document embedding
            double tf = Math.Log(1.0 + count) / Math.Log(1.0 + totalTerms);
            // Use stored IDF if available, otherwise neutral weight
            double termIdF = idf != null && idf.TryGetValue(term, out double v) ? v : 1.0;
            for (int c = 0; c < k; c++)
                embedding[c] += tf * termIdF * components[c, ti];
        }

        return embedding;
    }

    /// <summary>
    /// Compute cosine similarity between two vectors.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double CosineSimilarity(double[] a, double[] b)
    {
        if (a.Length != b.Length)
            return 0;

        double dot = 0, normA = 0, normB = 0;
        for (int i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        if (normA == 0 || normB == 0)
            return 0;

        return dot / (Math.Sqrt(normA) * Math.Sqrt(normB));
    }

    /// <summary>
    /// Power iteration to extract top-K right singular vectors of A (principal components of A^T A).
    /// Uses Gram-Schmidt orthogonalization for multiple eigenvectors.
    /// </summary>
    private static double[,] PowerIteration(IReadOnlyList<Dictionary<int, double>> docSparse, int n, int k)
    {
        int m = docSparse.Count;
        var components = new double[k, n];

        var rng = new Random(42);

        // Pre-filter documents with content and build sparse representation
        var activeDocIndices = new int[m];
        int activeCount = 0;
        for (int d = 0; d < m; d++)
        {
            if (docSparse[d].Count > 0)
                activeDocIndices[activeCount++] = d;
        }

        if (activeCount == 0)
            return components;

        for (int comp = 0; comp < k; comp++)
        {
            // Initialize with random vector
            var v = new double[n];
            for (int i = 0; i < n; i++)
                v[i] = rng.NextDouble() * 2 - 1;

            // Normalize
            double norm = Math.Sqrt(v.Sum(x => x * x));
            if (norm > 0)
                for (int i = 0; i < n; i++)
                    v[i] /= norm;

            // Power iteration: v = A^T (A v), early stopping
            var Au = new double[m];
            var AtAu = new double[n];
            var prevV = new double[n];

            for (int iter = 0; iter < 30; iter++)
            {
                // Step 1: Au = A v
                for (int di = 0; di < activeCount; di++)
                {
                    int d = activeDocIndices[di];
                    double sum = 0;
                    foreach (var (ti, tf) in docSparse[d])
                        sum += tf * v[ti];
                    Au[d] = sum;
                }

                // Step 2: AtAu = A^T Au
                Array.Fill(AtAu, 0);
                for (int di = 0; di < activeCount; di++)
                {
                    int d = activeDocIndices[di];
                    double aVal = Au[d];
                    if (aVal == 0)
                        continue;
                    foreach (var (ti, tf) in docSparse[d])
                        AtAu[ti] += tf * aVal;
                }

                // Normalize and check convergence
                norm = Math.Sqrt(AtAu.Sum(x => x * x));
                if (norm == 0)
                    break;

                double change = 0;
                for (int i = 0; i < n; i++)
                {
                    double newVal = AtAu[i] / norm;
                    change += Math.Abs(newVal - prevV[i]);
                    v[i] = newVal;
                }

                if (iter > 3 && change < 0.0001)
                    break;

                // Copy v to prevV for next iteration
                for (int i = 0; i < n; i++)
                    prevV[i] = v[i];
            }

            // Gram-Schmidt orthogonalization against previously found components (deflation)
            for (int prev = 0; prev < comp; prev++)
            {
                double dot = 0;
                for (int i = 0; i < n; i++)
                    dot += v[i] * components[prev, i];
                for (int i = 0; i < n; i++)
                    v[i] -= dot * components[prev, i];
            }

            // Re-normalize
            norm = Math.Sqrt(v.Sum(x => x * x));
            if (norm > 0)
                for (int i = 0; i < n; i++)
                    v[i] /= norm;

            // Run 2 more iterations on the deflated vector to re-converge
            for (int iter = 0; iter < 2; iter++)
            {
                // Au = A v
                for (int di = 0; di < activeCount; di++)
                {
                    int d = activeDocIndices[di];
                    double sum = 0;
                    foreach (var (ti, tf) in docSparse[d])
                        sum += tf * v[ti];
                    Au[d] = sum;
                }

                // AtAu = A^T Au
                Array.Fill(AtAu, 0);
                for (int di = 0; di < activeCount; di++)
                {
                    int d = activeDocIndices[di];
                    double aVal = Au[d];
                    if (aVal == 0)
                        continue;
                    foreach (var (ti, tf) in docSparse[d])
                        AtAu[ti] += tf * aVal;
                }

                norm = Math.Sqrt(AtAu.Sum(x => x * x));
                if (norm == 0)
                    break;

                for (int i = 0; i < n; i++)
                    v[i] = AtAu[i] / norm;

                // Re-orthogonalize after each sub-iteration
                for (int prev = 0; prev < comp; prev++)
                {
                    double dot = 0;
                    for (int i = 0; i < n; i++)
                        dot += v[i] * components[prev, i];
                    for (int i = 0; i < n; i++)
                        v[i] -= dot * components[prev, i];
                }

                norm = Math.Sqrt(v.Sum(x => x * x));
                if (norm > 0)
                    for (int i = 0; i < n; i++)
                        v[i] /= norm;
            }

            // Copy to row of components matrix
            for (int i = 0; i < n; i++)
                components[comp, i] = v[i];
        }

        return components;
    }

    /// <summary>
    /// Pack a double array into a byte array (float32).
    /// </summary>
    private static byte[] PackEmbedding(double[] embedding)
    {
        var bytes = new byte[embedding.Length * sizeof(float)];
        for (int i = 0; i < embedding.Length; i++)
        {
            var f = (float)embedding[i];
            var b = BitConverter.GetBytes(f);
            b.CopyTo(bytes, i * sizeof(float));
        }
        return bytes;
    }

    /// <summary>
    /// Unpack a byte array into a double array.
    /// </summary>
    private static double[] UnpackEmbedding(byte[] bytes)
    {
        var embedding = new double[bytes.Length / sizeof(float)];
        for (int i = 0; i < embedding.Length; i++)
        {
            var f = BitConverter.ToSingle(bytes, i * sizeof(float));
            embedding[i] = (double)f;
        }
        return embedding;
    }
}
