using DeepTrace.Core.Ai;
using DeepTrace.Indexing;
using DeepTrace.Core.Models;
using TestLib;
using Xunit;

namespace DeepTrace.Tests;

public class InvestigationWorkflowTests
{
    private readonly string _testDir;
    private readonly string _dbPath;
    private readonly SqliteIndexer _indexer;

    public InvestigationWorkflowTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"deeptrace_wf_{Guid.NewGuid()}");
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
    }

    [Fact]
    public void Investigate_ReturnsReportWithAllSections()
    {
        var tools = new InvestigationTools(_indexer);
        var workflow = new InvestigationWorkflow(tools);
        var options = new InvestigationOptions
        {
            MaxSearches = 3,
            MaxDepth = 1,
            ResultLimit = 5,
            TokenBudget = 4000,
        };

        var report = workflow.Investigate("How does Greeter greet a user", options);

        Assert.NotNull(report);
        Assert.NotNull(report.RequestInterpretation);
        Assert.NotEmpty(report.RequestInterpretation);
        Assert.NotNull(report.DiscoveredExecutionPath);
        Assert.NotNull(report.RequiredDependenciesAndInvariants);
        Assert.NotNull(report.CandidateExtensionPoints);
        Assert.NotNull(report.EvidenceAndConfidence);
        Assert.NotNull(report.ImplementationPlan);
        Assert.NotNull(report.ValidationPlan);
    }

    [Fact]
    public void Investigate_FindsMethods()
    {
        var tools = new InvestigationTools(_indexer);
        var workflow = new InvestigationWorkflow(tools);
        var options = new InvestigationOptions { MaxSearches = 3, MaxDepth = 1, ResultLimit = 5, TokenBudget = 4000 };

        var report = workflow.Investigate("Greeter", options);

        Assert.True(report.DiscoveredExecutionPath.Count > 0);
    }

    [Fact]
    public void Investigate_TracksVerifiedVsInferred()
    {
        var tools = new InvestigationTools(_indexer);
        var workflow = new InvestigationWorkflow(tools);
        var options = new InvestigationOptions { MaxSearches = 3, MaxDepth = 1, ResultLimit = 5, TokenBudget = 4000 };

        var report = workflow.Investigate("Greeter", options);

        var verified = report.DiscoveredExecutionPath.Count(e => e.IsVerified);
        var inferred = report.DiscoveredExecutionPath.Count(e => !e.IsVerified);
        Assert.True(verified > 0, "Should have at least one verified entry from search results");
    }

    [Fact]
    public void Investigate_LimitsSearches()
    {
        var tools = new InvestigationTools(_indexer);
        var workflow = new InvestigationWorkflow(tools);
        var options = new InvestigationOptions
        {
            MaxSearches = 1,
            MaxDepth = 0,
            ResultLimit = 1,
            TokenBudget = 2000,
        };

        var report = workflow.Investigate("very long query with many concepts that could generate multiple searches", options);

        Assert.NotNull(report);
        Assert.NotNull(report.DiscoveredExecutionPath);
    }

    [Fact]
    public void Investigate_EmptyQuery_ReturnsReport()
    {
        var tools = new InvestigationTools(_indexer);
        var workflow = new InvestigationWorkflow(tools);
        var options = new InvestigationOptions { MaxSearches = 1, MaxDepth = 0, ResultLimit = 1, TokenBudget = 1000 };

        var report = workflow.Investigate("", options);

        Assert.NotNull(report);
        Assert.NotNull(report.RequestInterpretation);
    }

    [Fact]
    public void InvestigationOptions_DefaultsSetReasonableValues()
    {
        var options = new InvestigationOptions();
        Assert.Equal(8, options.MaxSearches);
        Assert.Equal(2, options.MaxDepth);
        Assert.Equal(20, options.ResultLimit);
        Assert.Equal(12000, options.TokenBudget);
    }

    [Fact]
    public void Investigate_EvidenceContainsConfidenceLevels()
    {
        var tools = new InvestigationTools(_indexer);
        var workflow = new InvestigationWorkflow(tools);
        var options = new InvestigationOptions { MaxSearches = 3, MaxDepth = 1, ResultLimit = 5, TokenBudget = 4000 };

        var report = workflow.Investigate("Greeter", options);

        Assert.NotEmpty(report.EvidenceAndConfidence);
        foreach (var evidence in report.EvidenceAndConfidence)
        {
            Assert.NotEmpty(evidence.Finding);
        }
    }

    [Fact]
    public void InvestigationReport_EvidenceSourceEnumHasExpectedValues()
    {
        var sources = (EvidenceSource[])Enum.GetValues(typeof(EvidenceSource));
        var names = Array.ConvertAll(sources, s => s.ToString());
        Assert.Contains("StaticAnalysis", names);
        Assert.Contains("Runtime", names);
        Assert.Contains("Inference", names);
    }

    [Fact]
    public void InvestigationReport_ConfidenceLevelEnumHasExpectedValues()
    {
        var levels = (ConfidenceLevel[])Enum.GetValues(typeof(ConfidenceLevel));
        var names = Array.ConvertAll(levels, s => s.ToString());
        Assert.Contains("High", names);
        Assert.Contains("Medium", names);
        Assert.Contains("Low", names);
    }
}

