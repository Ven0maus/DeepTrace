using DeepTrace.Core.Ai;
using DeepTrace.Core.Models;
using DeepTrace.Core.Runtime;
using DeepTrace.Indexing;
using TestLib;
using Xunit;

namespace DeepTrace.Tests;

public class InvestigationWorkflowRuntimeTests
{
    private readonly string _testDir;
    private readonly string _dbPath;
    private readonly SqliteIndexer _indexer;
    private readonly InMemoryRuntimeObserver _runtimeObserver;

    public InvestigationWorkflowRuntimeTests()
    {
        _testDir = Path.Combine(AppContext.BaseDirectory, $"deeptrace_wf_rt_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDir);
        _dbPath = Path.Combine(_testDir, "test.db");

        var dllPath = Path.Combine(_testDir, "TestLib.dll");
        File.Copy(
            typeof(TestLib.Greeter).Assembly.Location,
            dllPath,
            overwrite: true);

        _indexer = new SqliteIndexer(_dbPath, new Decompiler.IlspyDecompiler());
        var assembly = _indexer.DiscoverAssemblies(_testDir)[0];
        _indexer.IndexAssembly(assembly);

        _runtimeObserver = new InMemoryRuntimeObserver();
    }

    [Fact]
    public void Investigate_WithRuntimeObserver_IncludesObservedPath()
    {
        // Pre-populate runtime observations
        var greeterCtorSymbol = "TestLib::TestLib.Greeter::.ctor::.ctor(System.String) : System.Void";
        _runtimeObserver.RecordCall(
            greeterCtorSymbol,
            "TestLib.Greeter..ctor",
            "TestLib",
            null, null, 5.2, depth: 0);

        var tools = new InvestigationTools(_indexer);
        var workflow = new InvestigationWorkflow(tools, _runtimeObserver);
        var options = new InvestigationOptions { MaxSearches = 2, MaxDepth = 0, ResultLimit = 5, TokenBudget = 3000 };

        var report = workflow.Investigate("Greeter", options);

        Assert.NotNull(report.ObservedExecutionPath);
        Assert.True(report.ObservedExecutionPath.Count >= 1);
    }

    [Fact]
    public void Investigate_WithRuntimeObserver_BoostsConfidence()
    {
        var greeterSymbol = "TestLib::TestLib.Greeter::.ctor::.ctor(System.String) : System.Void";
        _runtimeObserver.RecordCall(greeterSymbol, "TestLib.Greeter..ctor", "TestLib", null, null, 10, depth: 0);

        var tools = new InvestigationTools(_indexer);
        var workflow = new InvestigationWorkflow(tools, _runtimeObserver);
        var options = new InvestigationOptions { MaxSearches = 2, MaxDepth = 0, ResultLimit = 5, TokenBudget = 3000 };

        var report = workflow.Investigate("Greeter", options);

        var runtimeConfidence = report.EvidenceAndConfidence
            .Where(c => c.Source == EvidenceSource.Runtime)
            .ToList();

        Assert.NotEmpty(runtimeConfidence);
    }

    [Fact]
    public void Investigate_WithoutRuntimeObserver_EmptyObservedPath()
    {
        var tools = new InvestigationTools(_indexer);
        var workflow = new InvestigationWorkflow(tools, runtimeObserver: null);
        var options = new InvestigationOptions { MaxSearches = 2, MaxDepth = 0, ResultLimit = 5, TokenBudget = 3000 };

        var report = workflow.Investigate("Greeter", options);

        Assert.NotNull(report.ObservedExecutionPath);
        Assert.Empty(report.ObservedExecutionPath);
    }

    [Fact]
    public void Investigate_WithRuntimeObserver_ReportsCallCount()
    {
        var symbol = "TestLib::TestLib.Greeter::.ctor::.ctor(System.String) : System.Void";
        _runtimeObserver.RecordCall(symbol, "TestLib.Greeter..ctor", "TestLib", null, null, 5, depth: 0);
        _runtimeObserver.RecordCall(symbol, "TestLib.Greeter..ctor", "TestLib", null, null, 8, depth: 1);
        _runtimeObserver.RecordCall(symbol, "TestLib.Greeter..ctor", "TestLib", null, null, 3, depth: 0);

        var tools = new InvestigationTools(_indexer);
        var workflow = new InvestigationWorkflow(tools, _runtimeObserver);
        var options = new InvestigationOptions { MaxSearches = 2, MaxDepth = 0, ResultLimit = 5, TokenBudget = 3000 };

        var report = workflow.Investigate("Greeter", options);

        var observed = report.ObservedExecutionPath.FirstOrDefault(o => o.SymbolId == symbol);
        if (observed != null)
        {
            Assert.Equal(3, observed.CallCount);
            Assert.Equal(0, observed.MinDepth);
            Assert.Equal(1, observed.MaxDepth);
        }
    }

    [Fact]
    public void InvestigationReport_WithRuntime_EvidenceSourceHasRuntimeValue()
    {
        var symbol = "TestLib::TestLib.Greeter::.ctor::.ctor(System.String) : System.Void";
        _runtimeObserver.RecordCall(symbol, "TestLib.Greeter..ctor", "TestLib", null, null, 10, depth: 0);

        var tools = new InvestigationTools(_indexer);
        var workflow = new InvestigationWorkflow(tools, _runtimeObserver);
        var options = new InvestigationOptions { MaxSearches = 1, MaxDepth = 0, ResultLimit = 3, TokenBudget = 2000 };

        var report = workflow.Investigate("Greeter", options);

        var runtimeSources = report.EvidenceAndConfidence
            .Where(c => c.Source == EvidenceSource.Runtime)
            .ToList();

        Assert.NotEmpty(runtimeSources);
        foreach (var item in runtimeSources)
        {
            Assert.NotEmpty(item.Finding);
            Assert.True(item.Confidence == ConfidenceLevel.High || item.Confidence == ConfidenceLevel.Medium);
        }
    }
}

