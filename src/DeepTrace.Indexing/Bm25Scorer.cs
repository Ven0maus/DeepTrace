using System.Collections.Concurrent;
using System.Linq;
using DeepTrace.Core.Models;

namespace DeepTrace.Indexing;

/// <summary>
/// BM25-inspired relevance scoring for search results.
/// </summary>
public static class Bm25Scorer
{
    private const double K1 = 1.5;
    private const double B = 0.75;
    private static readonly ConcurrentDictionary<string, string[]> _tokenCache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Score a search result against a query.
    /// </summary>
    public static double Score(SearchResult result, string query)
    {
        if (string.IsNullOrWhiteSpace(query) || string.IsNullOrWhiteSpace(result.Name))
            return 0;

        var queryTerms = Tokenize(query);
        if (queryTerms.Length == 0)
            return 0;

        var nameTerms = Tokenize(result.Name);
        var fullText = Tokenize(result.FullyQualifiedName);
        var sigTerms = string.IsNullOrEmpty(result.Signature) ? Array.Empty<string>() : Tokenize(result.Signature);

        var nameScore = ComputeTermScore(nameTerms, queryTerms);
        var fullTextScore = ComputeTermScore(fullText, queryTerms);
        var sigScore = ComputeTermScore(sigTerms, queryTerms);

        // Exact name match gets heavy boost
        var exactMatch = result.Name.Equals(query, System.StringComparison.OrdinalIgnoreCase) ? 10.0 : 0.0;
        var prefixMatch = result.Name.StartsWith(query, System.StringComparison.OrdinalIgnoreCase) ? 3.0 : 0.0;

        // Name match is most important, then full text, then signature
        return (nameScore * 2.0) + (fullTextScore * 0.5) + (sigScore * 0.3) + exactMatch + prefixMatch;
    }

    /// <summary>
    /// Score a summary against a query (for summary-based ranking).
    /// </summary>
    public static double ScoreSummary(string? summary, string query)
    {
        if (string.IsNullOrWhiteSpace(summary) || string.IsNullOrWhiteSpace(query))
            return 0;

        var queryTerms = Tokenize(query);
        var summaryTerms = Tokenize(summary);
        return ComputeTermScore(summaryTerms, queryTerms) * 1.5;
    }

    private static string[] Tokenize(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return [];

        return _tokenCache.GetOrAdd(text, _ =>
        {
            var lower = text.ToLowerInvariant();
            return lower.Split(new[] { ' ', '.', '_', '-', '(', ')', ':', ',' },
                System.StringSplitOptions.RemoveEmptyEntries)
                .Where(t => t.Length >= 2)
                .Distinct()
                .ToArray();
        });
    }

    private static double ComputeTermScore(string[] documentTerms, string[] queryTerms)
    {
        if (documentTerms.Length == 0 || queryTerms.Length == 0)
            return 0;

        var score = 0.0;

        // Inline term frequency counting to avoid dictionary allocation
        var len = documentTerms.Length;
        foreach (var qTerm in queryTerms)
        {
            var freq = 0;
            for (var i = 0; i < len; i++)
            {
                if (documentTerms[i] == qTerm)
                    freq++;
            }

            if (freq > 0)
            {
                var idf = Math.Log(1.0 + (1.0 - freq / len) + 0.5);
                var tf = (freq * (K1 + 1.0)) / (freq + K1 * (1.0 - B + B * len));
                score += idf * tf;
            }
        }

        return score;
    }
}
