using DeepTrace.Indexing;
using Xunit;

namespace DeepTrace.Tests;

public class LsaEmbedderTests
{
    [Fact]
    public void Tokenize_SplitsOnSeparators()
    {
        var tokens = LsaEmbedder.Tokenize("Hello_World.Hello-World (a) : Return");
        Assert.Contains("hello", tokens);
        Assert.Contains("world", tokens);
        Assert.Contains("return", tokens);
        Assert.DoesNotContain("a", tokens); // single-char stopword filtered
    }

    [Fact]
    public void Tokenize_EmptyReturnsEmpty()
    {
        Assert.Empty(LsaEmbedder.Tokenize(string.Empty));
        Assert.Empty(LsaEmbedder.Tokenize(null!));
        Assert.Empty(LsaEmbedder.Tokenize("   "));
    }

    [Fact]
    public void Tokenize_Deduplicates()
    {
        var tokens = LsaEmbedder.Tokenize("hello hello world");
        var helloCount = tokens.Count(t => t == "hello");
        Assert.Equal(1, helloCount);
    }

    [Fact]
    public void CosineSimilarity_IdenticalVectorsReturnsOne()
    {
        var a = new[] { 1.0, 2.0, 3.0 };
        var b = new[] { 1.0, 2.0, 3.0 };
        var sim = LsaEmbedder.CosineSimilarity(a, b);
        Assert.InRange(sim, 0.999, 1.001);
    }

    [Fact]
    public void CosineSimilarity_OppositeVectorsReturnsNegative()
    {
        var a = new[] { 1.0, 0.0, 0.0 };
        var b = new[] { -1.0, 0.0, 0.0 };
        var sim = LsaEmbedder.CosineSimilarity(a, b);
        Assert.InRange(sim, -1.01, -0.99);
    }

    [Fact]
    public void CosineSimilarity_OrthogonalVectorsReturnsZero()
    {
        var a = new[] { 1.0, 0.0 };
        var b = new[] { 0.0, 1.0 };
        var sim = LsaEmbedder.CosineSimilarity(a, b);
        Assert.InRange(sim, -0.01, 0.01);
    }

    [Fact]
    public void CosineSimilarity_DifferentLengthReturnsZero()
    {
        var a = new[] { 1.0, 2.0 };
        var b = new[] { 1.0, 2.0, 3.0 };
        var sim = LsaEmbedder.CosineSimilarity(a, b);
        Assert.Equal(0.0, sim);
    }

    [Fact]
    public void BuildAndStore_SkipsSmallCorpus()
    {
        // Fewer than 5 documents — should skip silently, no tables created
        var docs = new[]
        {
            ("id1", "short body one"),
            ("id2", "short body two"),
        };

        var dbPath = Path.Combine(Path.GetTempPath(), $"lsa_test_{Guid.NewGuid()}.db");
        try
        {
            LsaEmbedder.BuildAndStore(dbPath, docs);

            // Embeddings should be empty (skipped for small corpus)
            var stored = LsaEmbedder.LoadStored(dbPath);
            Assert.Empty(stored);
        }
        catch (Microsoft.Data.Sqlite.SqliteException)
        {
            // Tables don't exist when build is skipped — acceptable
        }
        finally
        {
            if (File.Exists(dbPath))
                File.Delete(dbPath);
        }
    }

    [Fact]
    public void BuildAndStore_StoresEmbeddingsForAdequateCorpus()
    {
        // Create a corpus with 20+ docs for full 50-dim embeddings
        var docs = new List<(string id, string body)>();
        var topics = new[]
        {
            "player load data retrieve fetch record",
            "enemy spawn create instantiate destroy",
            "weapon fire shoot damage hit",
            "health heal restore recover life",
            "score points level up rank",
            "inventory item collect store bag",
            "quest mission objective complete finish",
            "dialogue conversation speak talk",
            "animation frame render draw sprite",
            "audio sound music play volume",
            "map level terrain explore world",
            "skill ability power mana energy",
            "team squad group party alliance",
            "resource gold coin treasure loot",
            "trade market shop buy sell",
            "combat fight battle war attack",
            "defense shield block guard protect",
            "magic spell enchant curse wizard",
            "quest mission objective goal target",
            "story narrative plot character arc",
        };

        for (int i = 0; i < topics.Length; i++)
        {
            docs.Add(($"doc{i}", topics[i] + " " + topics[i] + " additional text for dimensionality"));
        }

        var dbPath = Path.Combine(Path.GetTempPath(), $"lsa_test_{Guid.NewGuid()}.db");
        try
        {
            LsaEmbedder.BuildAndStore(dbPath, docs);

            var stored = LsaEmbedder.LoadStored(dbPath);
            Assert.Equal(docs.Count, stored.Count);

            // Each embedding should have full dimensions (vocab > 50, so k=50)
            foreach (var (_, emb) in stored)
            {
                Assert.Equal(LsaEmbedder.Dimensions, emb.Length);
            }

            // Check vocabulary is stored
            var vocab = LsaEmbedder.LoadVocabulary(dbPath);
            Assert.NotEmpty(vocab);

            // Check components are stored
            var components = LsaEmbedder.LoadComponents(dbPath);
            Assert.Equal(LsaEmbedder.Dimensions, components.GetLength(0));
            Assert.True(components.GetLength(1) > 0);
        }
        finally
        {
            if (File.Exists(dbPath))
                File.Delete(dbPath);
        }
    }

    [Fact]
    public void ComputeQueryEmbedding_ProjectsIntoLatentSpace()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"lsa_query_test_{Guid.NewGuid()}.db");
        try
        {
            // Build a larger corpus for full dimensions
            var docs = new List<(string id, string body)>();
            var topics = new[]
            {
                "load player data fetch retrieve user",
                "spawn enemy create instantiate monster",
                "fire weapon shoot damage attack",
                "heal health restore recover life",
                "score points level rank up",
                "inventory item collect store bag",
                "quest mission objective complete",
                "dialogue conversation speak talk",
                "animation frame render draw sprite",
                "audio sound music play volume",
                "map level terrain explore world",
                "skill ability power mana energy",
                "team squad group party alliance",
                "resource gold coin treasure loot",
                "trade market shop buy sell",
                "combat fight battle war attack",
                "defense shield block guard protect",
                "magic spell enchant curse wizard",
                "quest mission objective goal target",
                "story narrative plot character arc",
            };
            for (int i = 0; i < topics.Length; i++)
                docs.Add(($"doc{i}", topics[i] + " additional content for dimensionality expansion"));

            LsaEmbedder.BuildAndStore(dbPath, docs);

            var vocab = LsaEmbedder.LoadVocabulary(dbPath);
            var components = LsaEmbedder.LoadComponents(dbPath);

            // Query about players should project into the same space
            var stored = LsaEmbedder.LoadStored(dbPath);
            var queryEmb = LsaEmbedder.ComputeQueryEmbedding("load player data", vocab, components);
            Assert.True(queryEmb.Length > 0);
            Assert.True(queryEmb.Sum() != 0, $"Query embedding should be non-zero, got sum={queryEmb.Sum():F6}");
            var sim0 = LsaEmbedder.CosineSimilarity(queryEmb, stored["doc0"]);
            var sim1 = LsaEmbedder.CosineSimilarity(queryEmb, stored["doc1"]);
            Assert.True(sim0 > sim1, $"doc0 sim ({sim0:F4}) should be > doc1 sim ({sim1:F4})");
        }
        finally
        {
            if (File.Exists(dbPath))
                File.Delete(dbPath);
        }
    }

    [Fact]
    public void BuildAndStore_SemanticSimilarityMatchesQuery()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"lsa_sem_test_{Guid.NewGuid()}.db");
        try
        {
            var docs = new List<(string id, string body)>();
            // Semantically related groups
            docs.Add(("doc0", "load player data fetch retrieve user record"));
            docs.Add(("doc1", "save player data store persist user record"));
            docs.Add(("doc2", "spawn enemy create instantiate monster"));
            docs.Add(("doc3", "destroy enemy kill remove monster"));
            docs.Add(("doc4", "fire weapon shoot damage attack"));
            docs.Add(("doc5", "cast spell magic cast ability power"));
            docs.Add(("doc6", "health heal restore recover life"));
            docs.Add(("doc7", "score points level rank up"));
            docs.Add(("doc8", "inventory item collect store bag"));
            docs.Add(("doc9", "quest mission objective complete"));
            docs.Add(("doc10", "dialogue conversation speak talk"));
            docs.Add(("doc11", "animation frame render draw sprite"));
            docs.Add(("doc12", "audio sound music play volume"));
            docs.Add(("doc13", "map level terrain explore world"));
            docs.Add(("doc14", "skill ability power mana energy"));
            docs.Add(("doc15", "team squad group party alliance"));
            docs.Add(("doc16", "resource gold coin treasure loot"));
            docs.Add(("doc17", "trade market shop buy sell"));
            docs.Add(("doc18", "combat fight battle war attack"));
            docs.Add(("doc19", "defense shield block guard protect"));

            LsaEmbedder.BuildAndStore(dbPath, docs);

            var vocab = LsaEmbedder.LoadVocabulary(dbPath);
            var components = LsaEmbedder.LoadComponents(dbPath);
            var stored = LsaEmbedder.LoadStored(dbPath);

            // Query "retrieve user" should match doc0 and doc1 more than doc2
            var queryEmb = LsaEmbedder.ComputeQueryEmbedding("retrieve user", vocab, components);
            Assert.True(queryEmb.Length > 0);
            Assert.True(queryEmb.Any(x => x != 0), "Query embedding should have non-zero components");

            var sim0 = LsaEmbedder.CosineSimilarity(queryEmb, stored["doc0"]);
            var sim1 = LsaEmbedder.CosineSimilarity(queryEmb, stored["doc1"]);
            var sim2 = LsaEmbedder.CosineSimilarity(queryEmb, stored["doc2"]);

            // doc0 and doc1 share terms with query; doc2 is about enemies
            Assert.True(sim0 > sim2 || sim1 > sim2, $"doc0({sim0:F4})/doc1({sim1:F4}) should beat doc2({sim2:F4})");
        }
        finally
        {
            if (File.Exists(dbPath))
                File.Delete(dbPath);
        }
    }

    [Fact]
    public void Tokenize_FiltersStopwords()
    {
        var tokens = LsaEmbedder.Tokenize("load data get set create update");
        Assert.DoesNotContain("load", tokens);
        Assert.DoesNotContain("data", tokens);
        Assert.DoesNotContain("get", tokens);
        Assert.DoesNotContain("set", tokens);
        Assert.DoesNotContain("create", tokens);
        Assert.DoesNotContain("update", tokens);
    }

    [Fact]
    public void Tokenize_PreservesDomainTerms()
    {
        var tokens = LsaEmbedder.Tokenize("Vector3 playerController load data");
        Assert.Contains("vector3", tokens);
        Assert.Contains("playercontroller", tokens);
        Assert.DoesNotContain("load", tokens);
        Assert.DoesNotContain("data", tokens);
    }
}
