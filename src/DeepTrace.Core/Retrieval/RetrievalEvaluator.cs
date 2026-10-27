using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DeepTrace.Core.Interfaces;
using DeepTrace.Core.Models;

namespace DeepTrace.Core.Retrieval;

/// <summary>
/// Evaluation result for a single query.
/// </summary>
public sealed class QueryEvaluation
{
    public string Query { get; init; } = string.Empty;
    public IReadOnlyList<string> ExpectedResults { get; init; } = [];
    public IReadOnlyList<SearchResult> RetrievedResults { get; init; } = [];
    public int Precision { get; init; }
    public int Recall { get; init; }
    public int F1 { get; init; }
    public int RelevantRetrieved { get; init; }
    public int TotalRetrieved { get; init; }
    public int TotalRelevant { get; init; }
}

/// <summary>
/// Aggregated evaluation metrics.
/// </summary>
public sealed class EvaluationReport
{
    public IReadOnlyList<QueryEvaluation> PerQueryResults { get; init; } = [];
    public double AveragePrecision { get; init; }
    public double AverageRecall { get; init; }
    public double AverageF1 { get; init; }
    public int TotalQueries { get; init; }
    public int TotalRelevantRetrieved { get; init; }
    public int TotalRelevantAvailable { get; init; }
}

/// <summary>
/// Evaluates retrieval quality by comparing search results against known-gold-standard results.
/// </summary>
public static class RetrievalEvaluator
{
    /// <summary>
    /// Evaluate search quality for a set of queries with known relevant results.
    /// </summary>
    /// <param name="evaluations">List of (query, expected symbol IDs).</param>
    /// <param name="indexer">The indexer to evaluate.</param>
    /// <param name="limit">Max results per query.</param>
    public static EvaluationReport Evaluate(
        IReadOnlyList<(string Query, IReadOnlyList<string> ExpectedResults)> evaluations,
        IIndexer indexer,
        int limit = 20)
    {
        var perQuery = new List<QueryEvaluation>();
        var totalPrecision = 0;
        var totalRecall = 0;
        var totalF1 = 0;
        var totalRetrieved = 0;
        var totalAvailable = 0;

        foreach (var (query, expected) in evaluations)
        {
            var results = indexer.Search(query, limit);
            var expectedSet = new HashSet<string>(expected, System.StringComparer.OrdinalIgnoreCase);
            var retrievedSet = new HashSet<string>(results.Select(r => r.SymbolId), System.StringComparer.OrdinalIgnoreCase);

            var relevantRetrieved = retrievedSet.Intersect(expectedSet).Count();
            var precision = results.Count > 0 ? (relevantRetrieved * 100 / results.Count) : 0;
            var recall = expected.Count > 0 ? (relevantRetrieved * 100 / expected.Count) : 0;
            var f1 = (precision + recall) > 0 ? (2 * precision * recall / (precision + recall)) : 0;

            var eval = new QueryEvaluation
            {
                Query = query,
                ExpectedResults = expected,
                RetrievedResults = new List<SearchResult>(results),
                Precision = precision,
                Recall = recall,
                F1 = f1,
                RelevantRetrieved = relevantRetrieved,
                TotalRetrieved = results.Count,
                TotalRelevant = expected.Count,
            };

            perQuery.Add(eval);
            totalPrecision += precision;
            totalRecall += recall;
            totalF1 += f1;
            totalRetrieved += relevantRetrieved;
            totalAvailable += expected.Count;
        }

        var n = evaluations.Count;
        return new EvaluationReport
        {
            PerQueryResults = (IReadOnlyList<QueryEvaluation>)perQuery.AsReadOnly(),
            AveragePrecision = n > 0 ? totalPrecision / n : 0,
            AverageRecall = n > 0 ? totalRecall / n : 0,
            AverageF1 = n > 0 ? totalF1 / n : 0,
            TotalQueries = n,
            TotalRelevantRetrieved = totalRetrieved,
            TotalRelevantAvailable = totalAvailable,
        };
    }

    /// <summary>
    /// Format evaluation report as text.
    /// </summary>
    public static string FormatReport(EvaluationReport report)
    {
        var lines = new List<string>
        {
            "=== RETRIEVAL EVALUATION REPORT ===",
            string.Empty,
            $"Total queries: {report.TotalQueries}",
            $"Avg Precision: {report.AveragePrecision.ToString("F1", CultureInfo.InvariantCulture)}%",
            $"Avg Recall: {report.AverageRecall.ToString("F1", CultureInfo.InvariantCulture)}%",
            $"Avg F1: {report.AverageF1.ToString("F1", CultureInfo.InvariantCulture)}",
            $"Total relevant retrieved: {report.TotalRelevantRetrieved}/{report.TotalRelevantAvailable}",
            string.Empty,
        };

        foreach (var q in report.PerQueryResults)
        {
            lines.Add($"Query: \"{q.Query}\"");
            lines.Add($"  Expected: {q.TotalRelevant} results, Retrieved: {q.TotalRetrieved}");
            lines.Add($"  P={q.Precision}% R={q.Recall}% F1={q.F1.ToString("F1", CultureInfo.InvariantCulture)}");
            lines.Add($"  Relevant: {q.RelevantRetrieved}/{q.TotalRetrieved}");
            lines.Add(string.Empty);
        }

        return string.Join("\n", lines);
    }
}
