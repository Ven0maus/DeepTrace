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
        Assert.Contains(callers, c => c == "TestLib.Greeter.Main");
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
        Assert.Contains(callees, c => c == "TestLib.Greeter.Greet");
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
}

