using DeepTrace.Core.Ai;
using DeepTrace.Indexing;
using TestLib;
using Xunit;

namespace DeepTrace.Tests;

public class InvestigationToolsTests
{
    private readonly string _testDir;
    private readonly string _dbPath;
    private readonly SqliteIndexer _indexer;

    public InvestigationToolsTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"deeptrace_tools_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDir);
        _dbPath = Path.Combine(_testDir, "test.db");

        var dllPath = Path.Combine(_testDir, "TestLib.dll");
        File.Copy(
            typeof(TestLib.Greeter).Assembly.Location,
            dllPath,
            overwrite: true);

        _indexer = new SqliteIndexer(_dbPath, new Decompiler.IlsyDecompiler());
        var assembly = _indexer.DiscoverAssemblies(_testDir)[0];
        _indexer.IndexAssembly(assembly);
    }

    [Fact]
    public void SearchCode_ReturnsFormattedText()
    {
        var tools = new InvestigationTools(_indexer);
        var result = tools.SearchCode("Greeter");
        Assert.NotNull(result);
        Assert.StartsWith("=== SearchCode ===", result);
        Assert.Contains("Greeter", result);
    }

    [Fact]
    public void SearchCode_EmptyResult_ReturnsNoResults()
    {
        var tools = new InvestigationTools(_indexer);
        var result = tools.SearchCode("zzzznonexistentzzzz");
        Assert.Contains("No results found", result);
    }

    [Fact]
    public void SearchCode_WithAssemblyFilter_ReturnsFiltered()
    {
        var tools = new InvestigationTools(_indexer);
        var result = tools.SearchCode("", assemblyFilter: "TestLib");
        Assert.NotNull(result);
        Assert.Contains("TestLib", result);
    }

    [Fact]
    public void GetMethod_ReturnsFormattedText()
    {
        var tools = new InvestigationTools(_indexer);
        var methodId = "TestLib::TestLib.Greeter::Greet::Greet(System.String) : System.String";
        var result = tools.GetMethod(methodId);
        Assert.NotNull(result);
        Assert.StartsWith("=== GetMethod ===", result);
        Assert.Contains("Greet", result);
    }

    [Fact]
    public void GetMethod_UnknownSymbol_ReturnsNotFound()
    {
        var tools = new InvestigationTools(_indexer);
        var result = tools.GetMethod("nonexistent::symbol");
        Assert.Contains("not found", result.ToLowerInvariant());
    }

    [Fact]
    public void GetType_ReturnsFormattedText()
    {
        var tools = new InvestigationTools(_indexer);
        var result = tools.GetType("TestLib", "TestLib.Greeter");
        Assert.NotNull(result);
        Assert.StartsWith("=== GetType ===", result);
        Assert.Contains("Greeter", result);
    }

    [Fact]
    public void GetCallers_ReturnsFormattedText()
    {
        var tools = new InvestigationTools(_indexer);
        var methodId = "TestLib::TestLib.Greeter::.ctor::.ctor(System.String) : System.Void";
        var result = tools.GetCallers(methodId);
        Assert.NotNull(result);
        Assert.StartsWith("=== GetCallers ===", result);
    }

    [Fact]
    public void GetCallees_ReturnsFormattedText()
    {
        var tools = new InvestigationTools(_indexer);
        var methodId = "TestLib::TestLib.Greeter::.ctor::.ctor(System.String) : System.Void";
        var result = tools.GetCallees(methodId);
        Assert.NotNull(result);
        Assert.StartsWith("=== GetCallees ===", result);
    }

    [Fact]
    public void SearchReferences_ReturnsFormattedText()
    {
        var tools = new InvestigationTools(_indexer);
        var result = tools.SearchReferences("TestLib.Greeter");
        Assert.NotNull(result);
        Assert.StartsWith("=== SearchReferences ===", result);
    }

    [Fact]
    public void GetRelatedMethods_ReturnsFormattedText()
    {
        var tools = new InvestigationTools(_indexer);
        var result = tools.GetRelatedMethods("TestLib.Greeter");
        Assert.NotNull(result);
        Assert.StartsWith("=== GetRelatedMethods ===", result);
    }

    [Fact]
    public void AllTools_StartWithHeader()
    {
        var tools = new InvestigationTools(_indexer);
        var headers = new[]
        {
            tools.SearchCode("Greeter"),
            tools.GetMethod("TestLib::TestLib.Greeter::.ctor::.ctor(System.String) : System.Void"),
            tools.GetType("TestLib", "TestLib.Greeter"),
            tools.GetCallers("TestLib::TestLib.Greeter::.ctor::.ctor(System.String) : System.Void"),
            tools.GetCallees("TestLib::TestLib.Greeter::.ctor::.ctor(System.String) : System.Void"),
            tools.SearchReferences("TestLib.Greeter"),
            tools.GetRelatedMethods("TestLib.Greeter"),
        };

        foreach (var h in headers)
            Assert.StartsWith("=== ", h);
    }
}
