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
        _testDir = Path.Combine(AppContext.BaseDirectory, $"deeptrace_tools_{Guid.NewGuid()}");
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

    [Fact]
    public void GetCallers_WithDepth_ReturnsDeeperResults()
    {
        var tools = new InvestigationTools(_indexer);
        var greetId = "TestLib::TestLib.Greeter::Greet::Greet(System.String) : System.String";
        var result = tools.GetCallers(greetId, depth: 2, limit: 20);

        Assert.NotNull(result);
        Assert.StartsWith("=== GetCallers ===", result);
        Assert.Contains("Greeter", result);
    }

    [Fact]
    public void GetCallers_EmptySymbolId_ReturnsErrorMessage()
    {
        var tools = new InvestigationTools(_indexer);
        var result = tools.GetCallers("");
        Assert.Contains("methodSymbolId is null or empty", result);
    }

    [Fact]
    public void GetCallees_WithDepth_ReturnsDeeperResults()
    {
        var tools = new InvestigationTools(_indexer);
        var greetId = "TestLib::TestLib.Greeter::Greet::Greet(System.String) : System.String";
        var result = tools.GetCallees(greetId, depth: 2, limit: 20);

        Assert.NotNull(result);
        Assert.StartsWith("=== GetCallees ===", result);
    }

    [Fact]
    public void GetCallees_EmptySymbolId_ReturnsErrorMessage()
    {
        var tools = new InvestigationTools(_indexer);
        var result = tools.GetCallees("");
        Assert.Contains("methodSymbolId is null or empty", result);
    }

    [Fact]
    public void SearchReferences_EmptySymbolId_ReturnsErrorMessage()
    {
        var tools = new InvestigationTools(_indexer);
        var result = tools.SearchReferences("");
        Assert.Contains("symbolId is null or empty", result);
    }

    [Fact]
    public void GetRelatedMethods_ReturnsSameTypeMethods()
    {
        var tools = new InvestigationTools(_indexer);
        var greetId = "TestLib::TestLib.Greeter::Greet::Greet(System.String) : System.String";
        var result = tools.GetRelatedMethods(greetId, limit: 10);

        Assert.NotNull(result);
        Assert.StartsWith("=== GetRelatedMethods ===", result);
        Assert.Contains("Greeter", result);
    }

    [Fact]
    public void GetRelatedMethods_UnknownSymbol_ReturnsNotFound()
    {
        var tools = new InvestigationTools(_indexer);
        var result = tools.GetRelatedMethods("NonExistent.Symbol");
        Assert.Contains("not found", result.ToLowerInvariant());
    }

    [Fact]
    public void GetRelatedMethods_EmptySymbolId_ReturnsErrorMessage()
    {
        var tools = new InvestigationTools(_indexer);
        var result = tools.GetRelatedMethods("");
        Assert.Contains("methodSymbolId is null or empty", result);
    }

    [Fact]
    public void GetRuntimeObservations_NoObservations_ReturnsEmptyMessage()
    {
        var tools = new InvestigationTools(_indexer);
        var result = tools.GetRuntimeObservations("TestLib.Greeter.Greet");
        Assert.NotNull(result);
        Assert.StartsWith("=== GetRuntimeObservations ===", result);
        Assert.Contains("No runtime observations", result);
    }

    [Fact]
    public void GetType_NullAssembly_ReturnsErrorMessage()
    {
        var tools = new InvestigationTools(_indexer);
        var result = tools.GetType("", "SomeType");
        Assert.Contains("assemblyName is null or empty", result);
    }

    [Fact]
    public void GetType_NullType_ReturnsErrorMessage()
    {
        var tools = new InvestigationTools(_indexer);
        var result = tools.GetType("TestLib", "");
        Assert.Contains("typeFullName is null or empty", result);
    }

    [Fact]
    public void GetType_UnknownType_ReturnsNotFound()
    {
        var tools = new InvestigationTools(_indexer);
        var result = tools.GetType("TestLib", "NonExistent.Type");
        Assert.Contains("not found", result.ToLowerInvariant());
    }

    [Fact]
    public void SearchCode_WithNamespaceFilter_ReturnsFiltered()
    {
        var tools = new InvestigationTools(_indexer);
        var result = tools.SearchCode("", namespaceFilter: "TestLib");
        Assert.NotNull(result);
        Assert.Contains("TestLib", result);
    }

    [Fact]
    public void SearchCode_WithKindFilter_ReturnsFiltered()
    {
        var tools = new InvestigationTools(_indexer);
        var result = tools.SearchCode("", kindFilter: "Method");
        Assert.NotNull(result);
        Assert.Contains("Method", result);
    }

    [Fact]
    public void SearchCode_NoFilterMatch_ReturnsNoResults()
    {
        var tools = new InvestigationTools(_indexer);
        var result = tools.SearchCode("", assemblyFilter: "NonExistent");
        Assert.Contains("No results match", result);
    }

    [Fact]
    public void SearchReferences_WithTypeSymbol_ReturnsResults()
    {
        var tools = new InvestigationTools(_indexer);
        var result = tools.SearchReferences("TestLib.Greeter");
        Assert.NotNull(result);
        Assert.StartsWith("=== SearchReferences ===", result);
    }

    [Fact]
    public void SearchReferences_WithSymbolId_ReturnsResults()
    {
        var tools = new InvestigationTools(_indexer);
        var result = tools.SearchReferences("TestLib::TestLib.Greeter::Greeter");
        Assert.NotNull(result);
        Assert.StartsWith("=== SearchReferences ===", result);
    }

    [Fact]
    public void GetCallees_WithDepth2_ReturnsDeeperLevels()
    {
        var tools = new InvestigationTools(_indexer);
        var greetId = "TestLib::TestLib.Greeter::Greet::Greet(System.String) : System.String";
        var result = tools.GetCallees(greetId, depth: 2, limit: 50);
        Assert.NotNull(result);
        Assert.StartsWith("=== GetCallees ===", result);
    }

    [Fact]
    public void GetCallers_WithDepth2_ReturnsDeeperLevels()
    {
        var tools = new InvestigationTools(_indexer);
        var greetId = "TestLib::TestLib.Greeter::Greet::Greet(System.String) : System.String";
        var result = tools.GetCallers(greetId, depth: 2, limit: 50);
        Assert.NotNull(result);
        Assert.StartsWith("=== GetCallers ===", result);
    }

    [Fact]
    public void GetCallees_NoCallees_ReturnsEmptyMessage()
    {
        var tools = new InvestigationTools(_indexer);
        var result = tools.GetCallees("TestLib::TestLib.BaseClass::BaseProperty::BaseProperty() : System.String");
        Assert.NotNull(result);
        Assert.StartsWith("=== GetCallees ===", result);
    }

    [Fact]
    public void GetRelatedMethods_WithLimit_ReturnsLimited()
    {
        var tools = new InvestigationTools(_indexer);
        var greetId = "TestLib::TestLib.Greeter::Greet::Greet(System.String) : System.String";
        var result = tools.GetRelatedMethods(greetId, limit: 2);
        Assert.NotNull(result);
        Assert.StartsWith("=== GetRelatedMethods ===", result);
    }

    [Fact]
    public void GetRuntimeObservations_WithSymbolId_ReturnsMessage()
    {
        var tools = new InvestigationTools(_indexer);
        var result = tools.GetRuntimeObservations("TestLib::TestLib.Greeter::Greet");
        Assert.NotNull(result);
        Assert.StartsWith("=== GetRuntimeObservations ===", result);
    }
}

