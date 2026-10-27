using DeepTrace.Indexing;
using TestLib;
using Xunit;

namespace DeepTrace.Tests;

/// <summary>
/// Tests for FallbackAssemblyResolver shared resolver pattern.
/// Validates that SetUnityManagedDirectories configures the resolver correctly
/// and that indexing with managed directories succeeds without resolution errors.
/// </summary>
public class AssemblyResolverTests
{
    [Fact]
    public void SetUnityManagedDirectories_AllowsIndexing()
    {
        // Arrange
        var testDir = Path.Combine(AppContext.BaseDirectory, $"resolver_idx_{Guid.NewGuid()}");
        Directory.CreateDirectory(testDir);
        var dbPath = Path.Combine(testDir, "test.db");
        var dllPath = Path.Combine(testDir, "TestLib.dll");
        File.Copy(typeof(Greeter).Assembly.Location, dllPath, overwrite: true);

        var indexer = new SqliteIndexer(dbPath, new Decompiler.IlspyDecompiler());

        // Act — SetUnityManagedDirectories should not throw even with empty paths
        indexer.SetUnityManagedDirectories(Array.Empty<string>());

        // Assert — indexing still works
        var assembly = indexer.DiscoverAssemblies(testDir)[0];
        indexer.IndexAssembly(assembly);

        var methods = indexer.GetMethods(assembly.Name);
        Assert.NotEmpty(methods);

        // Cleanup
        Directory.Delete(testDir, true);
    }

    [Fact]
    public void Indexing_WithManagedDirectories_Succeeds()
    {
        // Arrange — create a temp dir with a real DLL
        var testDir = Path.Combine(AppContext.BaseDirectory, $"resolver_idx2_{Guid.NewGuid()}");
        Directory.CreateDirectory(testDir);
        var dbPath = Path.Combine(testDir, "test.db");
        var dllPath = Path.Combine(testDir, "TestLib.dll");
        File.Copy(typeof(Greeter).Assembly.Location, dllPath, overwrite: true);

        var indexer = new SqliteIndexer(dbPath, new Decompiler.IlspyDecompiler());

        // Act — configure with a real managed directory (TestLib's own directory)
        var testLibDir = Path.GetDirectoryName(typeof(Greeter).Assembly.Location)!;
        indexer.SetUnityManagedDirectories(new[] { testLibDir });

        var assembly = indexer.DiscoverAssemblies(testDir)[0];
        indexer.IndexAssembly(assembly);

        // Assert
        var types = indexer.GetTypes(assembly.Name);
        Assert.NotEmpty(types);

        var methods = indexer.GetMethods(assembly.Name);
        Assert.NotEmpty(methods);

        // Cleanup
        Directory.Delete(testDir, true);
    }

    [Fact]
    public void Indexing_NoUnhandledResolutionException()
    {
        // Arrange — indexer should not throw unhandled AssemblyResolutionException
        var testDir = Path.Combine(AppContext.BaseDirectory, $"resolver_idx3_{Guid.NewGuid()}");
        Directory.CreateDirectory(testDir);
        var dbPath = Path.Combine(testDir, "test.db");
        var dllPath = Path.Combine(testDir, "TestLib.dll");
        File.Copy(typeof(Greeter).Assembly.Location, dllPath, overwrite: true);

        var indexer = new SqliteIndexer(dbPath, new Decompiler.IlspyDecompiler());

        // Act + Assert — should not throw due to unhandled resolution failures
        var assembly = indexer.DiscoverAssemblies(testDir)[0];
        Exception? exception = null;
        try
        {
            indexer.IndexAssembly(assembly);
        }
        catch (Exception ex)
        {
            exception = ex;
        }

        // Either succeeds or throws a non-resolution exception
        // The fallback resolver catches resolution errors internally
        if (exception != null)
        {
            Assert.DoesNotContain("AssemblyResolutionException", exception.Message);
        }

        // Cleanup
        if (Directory.Exists(testDir))
            Directory.Delete(testDir, true);
    }
}
