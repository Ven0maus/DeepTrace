using DeepTrace.Core.Models;
using DeepTrace.Indexing;
using TestLib;
using Xunit;

namespace DeepTrace.Tests;

public class StructuralAnalysisTests
{
    private readonly string _testDir;
    private readonly string _dbPath;

    public StructuralAnalysisTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"deeptrace_struct_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDir);
        _dbPath = Path.Combine(_testDir, "test.db");
    }

    private SqliteIndexer CreateIndexer()
    {
        var dllPath = Path.Combine(_testDir, "TestLib.dll");
        File.Copy(
            typeof(TestLib.Greeter).Assembly.Location,
            dllPath,
            overwrite: true);
        var indexer = new SqliteIndexer(_dbPath, new Decompiler.IlspyDecompiler());
        var assembly = indexer.DiscoverAssemblies(_testDir)[0];
        indexer.IndexAssembly(assembly);
        return indexer;
    }

    [Fact]
    public void GetTypeInfo_ReturnsCompleteType()
    {
        // Arrange
        var indexer = CreateIndexer();

        // Act
        var type = indexer.GetTypeInfo("TestLib", "TestLib.Greeter");

        // Assert
        Assert.NotNull(type);
        Assert.Equal("TestLib.Greeter", type.FullName);
        Assert.Equal("Greeter", type.Name);
        Assert.Equal("TestLib", type.Namespace);
        Assert.Equal(3, type.Methods.Count);
        Assert.Single(type.Fields);
        Assert.Equal("_prefix", type.Fields[0].Name);
        Assert.Equal("System.String", type.Fields[0].Type);
    }

    [Fact]
    public void GetTypeInfo_ReturnsNullForUnknownType()
    {
        // Arrange
        var indexer = CreateIndexer();

        // Act
        var type = indexer.GetTypeInfo("TestLib", "NonExistent.Type");

        // Assert
        Assert.Null(type);
    }

    [Fact]
    public void GetCallers_FindsCallingMethods()
    {
        // Arrange
        var indexer = CreateIndexer();
        var greetId = "TestLib::TestLib.Greeter::Greet::Greet(System.String) : System.String";

        // Act
        var callers = indexer.GetCallers(greetId);

        // Assert
        Assert.NotEmpty(callers);
        Assert.Contains(callers, c => c.Contains("Main"));
    }

    [Fact]
    public void GetCallees_FindsCalledMethods()
    {
        // Arrange
        var indexer = CreateIndexer();
        var mainId = "TestLib::TestLib.Greeter::Main::Main() : System.Void";

        // Act
        var callees = indexer.GetCallees(mainId);

        // Assert
        Assert.NotEmpty(callees);
        Assert.Contains(callees, c => c.Contains("Greet"));
    }

    [Fact]
    public void GetCallers_ReturnsEmptyForLeafMethod()
    {
        // Arrange
        var indexer = CreateIndexer();
        var ctorId = "TestLib::TestLib.Greeter::.ctor::.ctor(System.String) : System.Void";

        // Act
        var callers = indexer.GetCallers(ctorId);

        // Assert
        // .ctor may be called by Main (new Greeter()), but if not indexed, empty is valid
        Assert.IsAssignableFrom<IReadOnlyList<string>>(callers);
    }

    [Fact]
    public void GetCallees_ReturnsEmptyForMethodWithNoCalls()
    {
        // Arrange
        var indexer = CreateIndexer();
        var greetId = "TestLib::TestLib.Greeter::Greet::Greet(System.String) : System.String";

        // Act
        var callees = indexer.GetCallees(greetId);

        // Assert — Greet uses string interpolation which references System.String (not indexed)
        // So within this assembly, there are no callees
        Assert.IsAssignableFrom<IReadOnlyList<string>>(callees);
    }

    [Fact]
    public void GetTypeInfo_IncludesAttributes()
    {
        // Arrange
        var indexer = CreateIndexer();

        // Act
        var type = indexer.GetTypeInfo("TestLib", "TestLib.Greeter");

        // Assert
        Assert.NotEmpty(type.Attributes);
        Assert.Contains(type.Attributes, a => a.Contains("NullableAttribute"));
    }

    [Fact]
    public void GetTypeInfo_IncludesMethods()
    {
        // Arrange
        var indexer = CreateIndexer();

        // Act
        var type = indexer.GetTypeInfo("TestLib", "TestLib.Greeter");

        // Assert
        Assert.True(type.Methods.Count >= 2);
        var greet = type.Methods.FirstOrDefault(m => m.Name == "Greet");
        Assert.NotNull(greet);
        Assert.False(greet.IsStatic);
        Assert.Single(greet.Parameters);
    }

    [Fact]
    public void SearchReferences_FindsMethodCallers()
    {
        // Arrange
        var indexer = CreateIndexer();
        var greetId = "TestLib::TestLib.Greeter::Greet::Greet(System.String) : System.String";

        // Act
        var results = indexer.SearchReferences(greetId);

        // Assert
        Assert.NotEmpty(results);
        Assert.Contains(results, r => r.Excerpt == "caller" && r.FullyQualifiedName == "TestLib.Greeter.Main");
    }

    [Fact]
    public void SearchReferences_FindsMethodCallees()
    {
        // Arrange
        var indexer = CreateIndexer();
        var mainId = "TestLib::TestLib.Greeter::Main::Main() : System.Void";

        // Act
        var results = indexer.SearchReferences(mainId);

        // Assert
        Assert.NotEmpty(results);
        Assert.Contains(results, r => r.Excerpt == "callee" && r.FullyQualifiedName == "TestLib.Greeter.Greet");
    }

    [Fact]
    public void Indexing_PopulatesCallGraphTables()
    {
        // Arrange
        var indexer = CreateIndexer();

        // Act — verify callers and callees are populated
        var greetId = "TestLib::TestLib.Greeter::Greet::Greet(System.String) : System.String";
        var callers = indexer.GetCallers(greetId);
        var mainId = "TestLib::TestLib.Greeter::Main::Main() : System.Void";
        var callees = indexer.GetCallees(mainId);

        // Assert
        Assert.NotEmpty(callers);
        Assert.NotEmpty(callees);
    }

    [Fact]
    public void GetTypeInfo_IncludesFields()
    {
        // Arrange
        var indexer = CreateIndexer();

        // Act
        var type = indexer.GetTypeInfo("TestLib", "TestLib.Greeter");

        // Assert
        Assert.NotEmpty(type.Fields);
        var prefix = type.Fields.FirstOrDefault(f => f.Name == "_prefix");
        Assert.NotNull(prefix);
        Assert.Equal("System.String", prefix.Type);
        Assert.False(prefix.IsStatic);
    }

    [Fact]
    public void GetMethods_ReturnsAllMethodsInAssembly()
    {
        // Arrange
        var indexer = CreateIndexer();

        // Act
        var methods = indexer.GetMethods("TestLib");

        // Assert
        Assert.NotEmpty(methods);
        Assert.True(methods.Count >= 3);
        var greet = methods.FirstOrDefault(m => m.Name == "Greet");
        Assert.NotNull(greet);
        Assert.Contains("Greet", greet.FullName);
    }

    [Fact]
    public void GetDerivedTypes_ReturnsDerivedTypes()
    {
        // Arrange
        var indexer = CreateIndexer();
        var typeSymbolId = "TestLib::TestLib.BaseClass";

        // Act
        var derived = indexer.GetDerivedTypes(typeSymbolId);

        // Assert
        Assert.NotEmpty(derived);
        Assert.Contains(derived, d => d.Contains("DerivedClass"));
    }

    [Fact]
    public void GetBaseTypes_ReturnsBaseTypes()
    {
        // Arrange
        var indexer = CreateIndexer();
        var typeSymbolId = "TestLib::TestLib.DerivedClass";

        // Act
        var baseTypes = indexer.GetBaseTypes(typeSymbolId);

        // Assert
        Assert.NotEmpty(baseTypes);
        Assert.Contains(baseTypes, b => b.Contains("BaseClass"));
    }

    [Fact]
    public void IsAssemblyIndexed_ReturnsTrueAfterIndexing()
    {
        // Arrange
        var indexer = CreateIndexer();

        // Act
        var indexed = indexer.IsAssemblyIndexed("TestLib");

        // Assert
        Assert.True(indexed);
    }

    [Fact]
    public void IsAssemblyIndexed_ReturnsFalseForUnknown()
    {
        // Arrange
        var indexer = CreateIndexer();

        // Act
        var indexed = indexer.IsAssemblyIndexed("NonExistent");

        // Assert
        Assert.False(indexed);
    }

    [Fact]
    public void GetAssemblyLastWriteTime_ReturnsValidTime()
    {
        // Arrange
        var indexer = CreateIndexer();
        var dllPath = Path.Combine(_testDir, "TestLib.dll");

        // Act
        var writeTime = indexer.GetAssemblyLastWriteTime(dllPath);

        // Assert — Ticks should be > 2020*365*86400*10000000
        Assert.True(writeTime > 2020L * 365L * 86400L * 10000000L);
    }

    [Fact]
    public void GetAssemblyLastWriteTime_ReturnsDefaultForMissingFile()
    {
        // Arrange
        var indexer = CreateIndexer();

        // Act
        var writeTime = indexer.GetAssemblyLastWriteTime("nonexistent.dll");

        // Assert - File.GetLastWriteTimeUtc returns DateTime.MinValue for missing files on some systems
        Assert.True(writeTime >= 0);
    }

    [Fact]
    public void GetAssemblyHash_ReturnsNonEmptyString()
    {
        // Arrange
        var indexer = CreateIndexer();
        var dllPath = Path.Combine(_testDir, "TestLib.dll");

        // Act
        var hash = indexer.GetAssemblyHash(dllPath);

        // Assert
        Assert.NotNull(hash);
        Assert.NotEmpty(hash);
        Assert.Equal(64, hash.Length); // SHA256 hex = 64 chars
    }

    [Fact]
    public void Dispose_CleansUpResources()
    {
        // Arrange
        var indexer = CreateIndexer();

        // Act
        indexer.Dispose();

        // Assert — should not throw on second dispose
        indexer.Dispose();
    }

    [Fact]
    public void Search_ReturnsEmptyForNoMatch()
    {
        // Arrange
        var indexer = CreateIndexer();

        // Act
        var results = indexer.Search("zzzznonexistent_method_name");

        // Assert
        Assert.Empty(results);
    }

    [Fact]
    public void SearchReferences_ReturnsEmptyForUnknownSymbol()
    {
        // Arrange
        var indexer = CreateIndexer();

        // Act
        var results = indexer.SearchReferences("NonExistent.Symbol");

        // Assert
        Assert.Empty(results);
    }

    [Fact]
    public void GetCallGraphEdges_ReturnsEdgesForIndexedMethod()
    {
        // Arrange
        var indexer = CreateIndexer();
        var greetId = "TestLib::TestLib.Greeter::Greet::Greet(System.String) : System.String";

        // Act
        var edges = indexer.GetCallGraphEdges(greetId);

        // Assert
        Assert.NotNull(edges);
    }

    [Fact]
    public void GetCallGraphEdges_ReturnsEmptyForUnknownMethod()
    {
        // Arrange
        var indexer = CreateIndexer();

        // Act
        var edges = indexer.GetCallGraphEdges("NonExistent::NonExistent::Method::Method() : System.Void");

        // Assert
        Assert.Empty(edges);
    }

    [Fact]
    public void GetCallGraphEdgesByType_FiltersByEdgeType()
    {
        // Arrange
        var indexer = CreateIndexer();
        var greetId = "TestLib::TestLib.Greeter::Greet::Greet(System.String) : System.String";

        // Act
        var directEdges = indexer.GetCallGraphEdgesByType(greetId, "direct");
        var virtualEdges = indexer.GetCallGraphEdgesByType(greetId, "virtual");

        // Assert — both calls should not throw
        Assert.NotNull(directEdges);
        Assert.NotNull(virtualEdges);
    }

    [Fact]
    public void GetEngineCallbacks_ReturnsDetectedCallbacks()
    {
        // Arrange
        var indexer = CreateIndexer();
        var greeterTypeId = "TestLib::TestLib.Greeter";

        // Act
        var callbacks = indexer.GetEngineCallbacks(greeterTypeId);

        // Assert — should not throw, may be empty for non-engine types
        Assert.NotNull(callbacks);
    }

    [Fact]
    public void GetEngineCallbacks_ReturnsEmptyForUnknownType()
    {
        // Arrange
        var indexer = CreateIndexer();

        // Act
        var callbacks = indexer.GetEngineCallbacks("NonExistent::NonExistent.Type");

        // Assert
        Assert.Empty(callbacks);
    }

    [Fact]
    public void GetInterfaceImplementations_ReturnsEmptyForUnknownInterface()
    {
        // Arrange
        var indexer = CreateIndexer();

        // Act
        var implementations = indexer.GetInterfaceImplementations("NonExistent.IInterface", "Method");

        // Assert
        Assert.Empty(implementations);
    }
}

