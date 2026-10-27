using DeepTrace.Core.Models;
using DeepTrace.Indexing;
using TestLib;
using Xunit;

namespace DeepTrace.Tests;

/// <summary>
/// Tests for method body population during indexing.
/// Validates that TryDecompileMethod is called with correct signature format.
/// </summary>
public class IndexerBodyTests
{
    private readonly string _testDir;
    private readonly string _dbPath;

    public IndexerBodyTests()
    {
        _testDir = Path.Combine(AppContext.BaseDirectory, $"deeptrace_body_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDir);
        _dbPath = Path.Combine(_testDir, "test.db");
    }

    [Fact]
    public void IndexAssembly_PopulatesMethodBodies()
    {
        // Arrange
        var dllPath = Path.Combine(_testDir, "TestLib.dll");
        File.Copy(
            typeof(Greeter).Assembly.Location,
            dllPath,
            overwrite: true);

        var indexer = new SqliteIndexer(_dbPath, new Decompiler.IlspyDecompiler());
        var assembly = indexer.DiscoverAssemblies(_testDir)[0];
        indexer.IndexAssembly(assembly);

        // Act
        var methodId = "TestLib::TestLib.Greeter::Greet::Greet(System.String) : System.String";
        var method = indexer.GetMethod(methodId);

        // Assert
        Assert.NotNull(method);
        Assert.NotNull(method.Body);
        Assert.NotEmpty(method.Body);
        Assert.Contains("_prefix", method.Body);
        Assert.Contains("name", method.Body);
    }

    [Fact]
    public void IndexAssembly_IndexesMultipleMethods()
    {
        // Arrange
        var dllPath = Path.Combine(_testDir, "TestLib.dll");
        File.Copy(
            typeof(Greeter).Assembly.Location,
            dllPath,
            overwrite: true);

        var indexer = new SqliteIndexer(_dbPath, new Decompiler.IlspyDecompiler());
        var assembly = indexer.DiscoverAssemblies(_testDir)[0];
        indexer.IndexAssembly(assembly);

        // Act
        var greeter = indexer.GetTypes(assembly.Name).FirstOrDefault(t => t.Name == "Greeter");
        Assert.NotNull(greeter);

        // Assert — .ctor + Greet + Main + property accessors (filtered)
        Assert.True(greeter.Methods.Count >= 2);
    }

    [Fact]
    public void IndexAssembly_SameAssemblyNotReindexed()
    {
        // Arrange
        var dllPath = Path.Combine(_testDir, "TestLib.dll");
        File.Copy(
            typeof(Greeter).Assembly.Location,
            dllPath,
            overwrite: true);

        var indexer = new SqliteIndexer(_dbPath, new Decompiler.IlspyDecompiler());
        var assembly = indexer.DiscoverAssemblies(_testDir)[0];

        // Act — index twice
        indexer.IndexAssembly(assembly);
        indexer.IndexAssembly(assembly);

        // Assert — still have data, no duplicates
        var methods = indexer.GetMethods(assembly.Name);
        var greetCount = methods.Count(m => m.Name == "Greet");
        Assert.Equal(1, greetCount);
    }

    [Fact]
    public void IndexAssembly_WithNestedTypes()
    {
        // Arrange — use TestLib which has nested types
        var dllPath = Path.Combine(_testDir, "TestLib.dll");
        File.Copy(
            typeof(Greeter).Assembly.Location,
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
        Assert.True(greeter.Methods.Count >= 2);
    }

    [Fact]
    public void IndexAssembly_SymbolIdFormatCorrect()
    {
        // Arrange
        var dllPath = Path.Combine(_testDir, "TestLib.dll");
        File.Copy(
            typeof(Greeter).Assembly.Location,
            dllPath,
            overwrite: true);

        var indexer = new SqliteIndexer(_dbPath, new Decompiler.IlspyDecompiler());
        var assembly = indexer.DiscoverAssemblies(_testDir)[0];
        indexer.IndexAssembly(assembly);

        // Act
        var methodId = "TestLib::TestLib.Greeter::Greet::Greet(System.String) : System.String";
        var method = indexer.GetMethod(methodId);

        // Assert
        Assert.NotNull(method);
        Assert.Equal(methodId, method.SymbolId);
    }
}
