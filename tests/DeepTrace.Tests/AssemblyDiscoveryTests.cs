using DeepTrace.Indexing;
using Xunit;

namespace DeepTrace.Tests;

public class AssemblyDiscoveryTests
{
    private readonly string _testDir;
    private readonly string _dbPath;

    public AssemblyDiscoveryTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"deeptrace_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDir);
        _dbPath = Path.Combine(_testDir, "test.db");
    }

    [Fact]
    public void DiscoverAssemblies_FindsDllFiles()
    {
        // Arrange
        var dllPath = Path.Combine(_testDir, "TestLib.dll");
        File.Copy(
            @"C:\Temp\TestLib\TestLib\bin\Release\net8.0\TestLib.dll",
            dllPath,
            overwrite: true);

        var discoverer = new AssemblyDiscoverer();

        // Act
        var assemblies = discoverer.Discover(_testDir);

        // Assert
        Assert.Single(assemblies);
        Assert.Equal("TestLib", assemblies[0].Name);
    }

    [Fact]
    public void DiscoverAssemblies_ExcludesNonManagedFiles()
    {
        // Arrange
        var txtPath = Path.Combine(_testDir, "readme.txt");
        File.WriteAllText(txtPath, "not a dll");

        var discoverer = new AssemblyDiscoverer();

        // Act
        var assemblies = discoverer.Discover(_testDir);

        // Assert
        Assert.Empty(assemblies);
    }

    [Fact]
    public void DiscoverAssemblies_SkipsSystemAssemblies()
    {
        // Arrange
        var systemDll = Path.Combine(_testDir, "System.Private.CoreLib.dll");
        File.WriteAllText(systemDll, "junk"); // doesn't matter, excluded by name

        var discoverer = new AssemblyDiscoverer();

        // Act
        var assemblies = discoverer.Discover(_testDir);

        // Assert
        Assert.Empty(assemblies);
    }
}
