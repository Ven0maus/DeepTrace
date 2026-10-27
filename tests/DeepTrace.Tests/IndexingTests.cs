using DeepTrace.Core.Models;
using DeepTrace.Indexing;
using TestLib;
using Xunit;

namespace DeepTrace.Tests;

public class IndexingTests
{
    private readonly string _testDir;
    private readonly string _dbPath;

    public IndexingTests()
    {
        _testDir = Path.Combine(AppContext.BaseDirectory, $"deeptrace_idx_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDir);
        _dbPath = Path.Combine(_testDir, "test.db");
    }

    [Fact]
    public void IndexAssembly_IndexesTypes()
    {
        // Arrange
        var dllPath = Path.Combine(_testDir, "TestLib.dll");
        File.Copy(
            typeof(TestLib.Greeter).Assembly.Location,
            dllPath,
            overwrite: true);

        var indexer = new SqliteIndexer(_dbPath, new Decompiler.IlspyDecompiler());
        var assembly = indexer.DiscoverAssemblies(_testDir)[0];

        // Act
        indexer.IndexAssembly(assembly);

        // Assert
        var types = indexer.GetTypes(assembly.Name);
        Assert.True(types.Count >= 2); // Greeter + Class1
        var greeter = types.FirstOrDefault(t => t.Name == "Greeter");
        Assert.NotNull(greeter);
    }

    [Fact]
    public void IndexAssembly_IndexesMethods()
    {
        // Arrange
        var dllPath = Path.Combine(_testDir, "TestLib.dll");
        File.Copy(
            typeof(TestLib.Greeter).Assembly.Location,
            dllPath,
            overwrite: true);

        var indexer = new SqliteIndexer(_dbPath, new Decompiler.IlspyDecompiler());
        var assembly = indexer.DiscoverAssemblies(_testDir)[0];
        indexer.IndexAssembly(assembly);

        // Act
        var types = indexer.GetTypes(assembly.Name);
        var greeter = types.FirstOrDefault(t => t.Name == "Greeter");

        // Assert
        Assert.NotNull(greeter);
        Assert.True(greeter.Methods.Count >= 2); // .ctor + Greet
    }

    [Fact]
    public void IndexAssembly_SkipsUnchangedAssembly()
    {
        // Arrange
        var dllPath = Path.Combine(_testDir, "TestLib.dll");
        File.Copy(
            typeof(TestLib.Greeter).Assembly.Location,
            dllPath,
            overwrite: true);

        var indexer = new SqliteIndexer(_dbPath, new Decompiler.IlspyDecompiler());
        var assembly = indexer.DiscoverAssemblies(_testDir)[0];

        // Act — first index
        indexer.IndexAssembly(assembly);

        // Act — second index (same file, should skip)
        indexer.IndexAssembly(assembly);

        // Assert — verify data still intact after second call
        var types = indexer.GetTypes(assembly.Name);
        Assert.NotEmpty(types);
    }

    [Fact]
    public void Search_ReturnsMatchingMethods()
    {
        // Arrange
        var dllPath = Path.Combine(_testDir, "TestLib.dll");
        File.Copy(
            typeof(TestLib.Greeter).Assembly.Location,
            dllPath,
            overwrite: true);

        var indexer = new SqliteIndexer(_dbPath, new Decompiler.IlspyDecompiler());
        var assembly = indexer.DiscoverAssemblies(_testDir)[0];
        indexer.IndexAssembly(assembly);

        // Act
        var results = indexer.Search("Greet", 50);

        // Assert
        Assert.NotEmpty(results);
        Assert.Contains(results, r => r.Name == "Greet" && r.Kind == SymbolKind.Method);
    }

    [Fact]
    public void GetMethod_ReturnsMethodDetails()
    {
        // Arrange
        var dllPath = Path.Combine(_testDir, "TestLib.dll");
        File.Copy(
            typeof(TestLib.Greeter).Assembly.Location,
            dllPath,
            overwrite: true);

        var indexer = new SqliteIndexer(_dbPath, new Decompiler.IlspyDecompiler());
        var assembly = indexer.DiscoverAssemblies(_testDir)[0];
        indexer.IndexAssembly(assembly);

        // Act
        var methodId = $"TestLib::TestLib.Greeter::Greet::Greet(System.String) : System.String";
        var method = indexer.GetMethod(methodId);

        // Assert
        Assert.NotNull(method);
        Assert.Equal("TestLib.Greeter.Greet", method.FullName);
        Assert.Equal("TestLib", method.AssemblyName);
        Assert.False(method.IsStatic);
        Assert.Single(method.Parameters);
        Assert.Equal("System.String name", method.Parameters[0]);
    }

    [Fact]
    public void GetMethod_ReturnsNullForUnknownId()
    {
        // Arrange
        var indexer = new SqliteIndexer(_dbPath, new Decompiler.IlspyDecompiler());

        // Act
        var method = indexer.GetMethod("nonexistent::symbol");

        // Assert
        Assert.Null(method);
    }

    [Fact]
    public void Search_FtsSearchesByName()
    {
        // Arrange
        var dllPath = Path.Combine(_testDir, "TestLib.dll");
        File.Copy(
            typeof(TestLib.Greeter).Assembly.Location,
            dllPath,
            overwrite: true);

        var indexer = new SqliteIndexer(_dbPath, new Decompiler.IlspyDecompiler());
        var assembly = indexer.DiscoverAssemblies(_testDir)[0];
        indexer.IndexAssembly(assembly);

        // Act — search by partial name
        var results = indexer.Search("Greeter", 50);

        // Assert
        Assert.NotEmpty(results);
    }
}

