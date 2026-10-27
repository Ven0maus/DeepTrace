using DeepTrace.Core.Interfaces;
using DeepTrace.Core.Models;
using DeepTrace.Core.Retrieval;
using Xunit;

namespace DeepTrace.Tests;

public class RetrievalEvaluatorTests
{
    private sealed class FakeIndexer : IIndexer
    {
        private readonly Dictionary<string, List<SearchResult>> _results = new();

        public void AddResults(string query, List<SearchResult> results)
        {
            _results[query.ToLowerInvariant()] = results;
        }

        public IReadOnlyList<SearchResult> Search(string query, int limit = 50)
        {
            _results.TryGetValue(query.ToLowerInvariant(), out var results);
            return results?.GetRange(0, Math.Min(limit, results.Count)) ?? new List<SearchResult>();
        }

        public void IndexAssembly(Core.Models.AssemblyInfo assembly) => throw new NotImplementedException();
        public IReadOnlyList<Core.Models.TypeInfo> GetTypes(string assemblyName) => throw new NotImplementedException();
        public Core.Models.TypeInfo? GetTypeInfo(string assemblyName, string typeFullName) => throw new NotImplementedException();
        public Core.Models.MethodInfo? GetMethod(string symbolId) => throw new NotImplementedException();
        public IReadOnlyList<Core.Models.MethodInfo> GetMethods(string assemblyName) => throw new NotImplementedException();
        public IReadOnlyList<string> GetCallers(string methodSymbolId) => throw new NotImplementedException();
        public IReadOnlyList<string> GetCallees(string methodSymbolId) => throw new NotImplementedException();
        public IReadOnlyList<SearchResult> SearchReferences(string symbolId) => throw new NotImplementedException();
        public IReadOnlyList<string> GetDerivedTypes(string typeSymbolId) => throw new NotImplementedException();
        public IReadOnlyList<string> GetBaseTypes(string typeSymbolId) => throw new NotImplementedException();
        public bool IsAssemblyIndexed(string assemblyName) => throw new NotImplementedException();
        public long GetAssemblyLastWriteTime(string assemblyPath) => throw new NotImplementedException();
        public string GetAssemblyHash(string assemblyPath) => throw new NotImplementedException();
        public void StoreRuntimeObservation(Core.Runtime.RuntimeObservation observation) => throw new NotImplementedException();
        public IReadOnlyList<Core.Runtime.RuntimeObservation> GetRuntimeObservations(string symbolId) => throw new NotImplementedException();
        public IReadOnlyList<Core.Runtime.RuntimeObservation> GetAllRuntimeObservations() => throw new NotImplementedException();
        public IReadOnlyList<CallGraphEdge> GetCallGraphEdges(string methodSymbolId) => throw new NotImplementedException();
        public IReadOnlyList<CallGraphEdge> GetCallGraphEdgesByType(string methodSymbolId, string edgeType) => throw new NotImplementedException();
        public IReadOnlyList<string> GetInterfaceImplementations(string interfaceName, string methodName) => throw new NotImplementedException();
        public IReadOnlyList<string> GetEngineCallbacks(string typeSymbolId) => throw new NotImplementedException();
        public void SetUnityManagedDirectories(params string[] managedDirectories) { }
        public void IndexAssemblies(IReadOnlyList<Core.Models.AssemblyInfo> assemblies) => throw new NotImplementedException();
        public void RebuildLsaEmbeddings() { }
    }

    [Fact]
    public void Evaluate_PerfectPrecision_Returns100()
    {
        var indexer = new FakeIndexer();
        indexer.AddResults("greeter", new List<SearchResult>
        {
            new SearchResult { SymbolId = "sym_1", Name = "Greet", Kind = SymbolKind.Method, FullyQualifiedName = "TestLib.Greeter.Greet" },
        });

        var report = RetrievalEvaluator.Evaluate(
            new List<(string, IReadOnlyList<string>)> { ("greeter", new List<string> { "sym_1" }) },
            indexer);

        Assert.Equal(100, report.PerQueryResults.First().Precision);
    }

    [Fact]
    public void Evaluate_PerfectRecall_Returns100()
    {
        var indexer = new FakeIndexer();
        indexer.AddResults("greeter", new List<SearchResult>
        {
            new SearchResult { SymbolId = "sym_1", Name = "Greet", Kind = SymbolKind.Method, FullyQualifiedName = "TestLib.Greeter.Greet" },
            new SearchResult { SymbolId = "sym_2", Name = "Hello", Kind = SymbolKind.Method, FullyQualifiedName = "TestLib.Greeter.Hello" },
        });

        var report = RetrievalEvaluator.Evaluate(
            new List<(string, IReadOnlyList<string>)> { ("greeter", new List<string> { "sym_1", "sym_2" }) },
            indexer);

        Assert.Equal(100, report.PerQueryResults.First().Recall);
    }

    [Fact]
    public void Evaluate_NoMatches_ReturnsZero()
    {
        var indexer = new FakeIndexer();
        indexer.AddResults("greeter", new List<SearchResult>
        {
            new SearchResult { SymbolId = "sym_1", Name = "Greet", Kind = SymbolKind.Method, FullyQualifiedName = "TestLib.Greeter.Greet" },
        });

        var report = RetrievalEvaluator.Evaluate(
            new List<(string, IReadOnlyList<string>)> { ("greeter", new List<string> { "sym_99" }) },
            indexer);

        Assert.Equal(0, report.PerQueryResults.First().Precision);
        Assert.Equal(0, report.PerQueryResults.First().Recall);
        Assert.Equal(0, report.PerQueryResults.First().F1);
    }

    [Fact]
    public void Evaluate_MultipleQueries_AveragesCorrectly()
    {
        var indexer = new FakeIndexer();
        indexer.AddResults("q1", new List<SearchResult>
        {
            new SearchResult { SymbolId = "sym_1", Name = "A", Kind = SymbolKind.Method, FullyQualifiedName = "A" },
        });
        indexer.AddResults("q2", new List<SearchResult>
        {
            new SearchResult { SymbolId = "sym_2", Name = "B", Kind = SymbolKind.Method, FullyQualifiedName = "B" },
        });

        var report = RetrievalEvaluator.Evaluate(
            new List<(string, IReadOnlyList<string>)>
            {
                ("q1", new List<string> { "sym_1" }),
                ("q2", new List<string> { "sym_2" }),
            },
            indexer);

        Assert.Equal(100, report.AveragePrecision);
        Assert.Equal(100, report.AverageRecall);
        Assert.Equal(100, report.AverageF1);
        Assert.Equal(2, report.TotalQueries);
    }

    [Fact]
    public void Evaluate_EmptyExpected_ReturnsZero()
    {
        var report = RetrievalEvaluator.Evaluate(
            new List<(string, IReadOnlyList<string>)> { ("q", Array.Empty<string>().ToList()) },
            new FakeIndexer());

        Assert.Equal(0, report.PerQueryResults.First().Precision);
        Assert.Equal(0, report.PerQueryResults.First().Recall);
        Assert.Equal(0, report.PerQueryResults.First().F1);
    }

    [Fact]
    public void Evaluate_EmptyRetrieved_ReturnsZero()
    {
        var indexer = new FakeIndexer();
        indexer.AddResults("q", new List<SearchResult>());

        var report = RetrievalEvaluator.Evaluate(
            new List<(string, IReadOnlyList<string>)> { ("q", new List<string> { "sym_1" }) },
            indexer);

        Assert.Equal(0, report.PerQueryResults.First().Precision);
        Assert.Equal(0, report.PerQueryResults.First().Recall);
        Assert.Equal(0, report.PerQueryResults.First().F1);
    }

    [Fact]
    public void FormatReport_ContainsSummary()
    {
        var indexer = new FakeIndexer();
        indexer.AddResults("q", new List<SearchResult>
        {
            new SearchResult { SymbolId = "sym_1", Name = "A", Kind = SymbolKind.Method, FullyQualifiedName = "A" },
        });

        var report = RetrievalEvaluator.Evaluate(
            new List<(string, IReadOnlyList<string>)> { ("q", new List<string> { "sym_1" }) },
            indexer);

        var text = RetrievalEvaluator.FormatReport(report);
        Assert.Contains("RETRIEVAL EVALUATION REPORT", text);
        Assert.Contains("Total queries: 1", text);
        Assert.Contains("Avg Precision", text);
        Assert.Contains("100", text);
    }

    [Fact]
    public void Evaluate_PartialMatch_CalculatesCorrectF1()
    {
        var indexer = new FakeIndexer();
        indexer.AddResults("q", new List<SearchResult>
        {
            new SearchResult { SymbolId = "sym_1", Name = "A", Kind = SymbolKind.Method, FullyQualifiedName = "A" },
            new SearchResult { SymbolId = "sym_3", Name = "C", Kind = SymbolKind.Method, FullyQualifiedName = "C" },
        });

        var report = RetrievalEvaluator.Evaluate(
            new List<(string, IReadOnlyList<string>)> { ("q", new List<string> { "sym_1", "sym_2" }) },
            indexer);

        var eval = report.PerQueryResults.First();
        // P = 1/2 = 50, R = 1/2 = 50, F1 = 2*50*50/(50+50) = 50
        Assert.Equal(50, eval.Precision);
        Assert.Equal(50, eval.Recall);
        Assert.Equal(50, eval.F1);
    }

    [Fact]
    public void Evaluate_MultipleQueries_AggregatesTotals()
    {
        var indexer = new FakeIndexer();
        indexer.AddResults("q1", new List<SearchResult>
        {
            new SearchResult { SymbolId = "sym_1", Name = "A", Kind = SymbolKind.Method, FullyQualifiedName = "A" },
        });
        indexer.AddResults("q2", new List<SearchResult>
        {
            new SearchResult { SymbolId = "sym_2", Name = "B", Kind = SymbolKind.Method, FullyQualifiedName = "B" },
        });

        var report = RetrievalEvaluator.Evaluate(
            new List<(string, IReadOnlyList<string>)>
            {
                ("q1", new List<string> { "sym_1", "sym_99" }),
                ("q2", new List<string> { "sym_2", "sym_98" }),
            },
            indexer);

        Assert.Equal(2, report.TotalRelevantRetrieved);
        Assert.Equal(4, report.TotalRelevantAvailable);
    }
}
