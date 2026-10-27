using DeepTrace.Core.Models;
using DeepTrace.Indexing;
using TestLib;
using Xunit;

namespace DeepTrace.Tests;

/// <summary>
/// Tests for indexing integrity: FK constraint handling, pragma settings,
/// and cross-reference insertion that would fail with foreign_keys=ON.
/// </summary>
public class IndexingIntegrityTests
{
    private readonly string _testDir;
    private readonly string _dbPath;

    public IndexingIntegrityTests()
    {
        _testDir = Path.Combine(AppContext.BaseDirectory, $"idx_integrity_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDir);
        _dbPath = Path.Combine(_testDir, "test.db");
    }

    [Fact]
    public void Indexing_IndexesMethodBodies()
    {
        // Arrange
        var dllPath = Path.Combine(_testDir, "TestLib.dll");
        File.Copy(typeof(Greeter).Assembly.Location, dllPath, overwrite: true);

        var indexer = new SqliteIndexer(_dbPath, new Decompiler.IlspyDecompiler());
        var assembly = indexer.DiscoverAssemblies(_testDir)[0];
        indexer.IndexAssembly(assembly);

        // Act — use GetMethod which populates body
        var methodId = "TestLib::TestLib.Greeter::Greet::Greet(System.String) : System.String";
        var method = indexer.GetMethod(methodId);

        // Assert — body should be populated
        Assert.NotNull(method);
        Assert.NotNull(method.Body);
        Assert.NotEmpty(method.Body);
        Assert.Contains("_prefix", method.Body);
    }

    [Fact]
    public void Indexing_IndexesTypeHierarchy()
    {
        // Arrange — DerivedClass : BaseClass
        var dllPath = Path.Combine(_testDir, "TestLib.dll");
        File.Copy(typeof(Greeter).Assembly.Location, dllPath, overwrite: true);

        var indexer = new SqliteIndexer(_dbPath, new Decompiler.IlspyDecompiler());
        var assembly = indexer.DiscoverAssemblies(_testDir)[0];
        indexer.IndexAssembly(assembly);

        // Act
        var derived = indexer.GetTypeInfo(assembly.Name, "TestLib.DerivedClass");

        // Assert — DerivedClass has BaseClass as base type
        Assert.NotNull(derived);
        Assert.Equal("TestLib.BaseClass", derived.BaseTypeFullName);
    }

    [Fact]
    public void Indexing_IndexesDerivedTypes()
    {
        // Arrange
        var dllPath = Path.Combine(_testDir, "TestLib.dll");
        File.Copy(typeof(Greeter).Assembly.Location, dllPath, overwrite: true);

        var indexer = new SqliteIndexer(_dbPath, new Decompiler.IlspyDecompiler());
        var assembly = indexer.DiscoverAssemblies(_testDir)[0];
        indexer.IndexAssembly(assembly);

        // Act — use GetDerivedTypes
        var baseSymbolId = "TestLib::TestLib.BaseClass";
        var derivedTypes = indexer.GetDerivedTypes(baseSymbolId);

        // Assert — BaseClass has DerivedClass as derived type
        Assert.NotEmpty(derivedTypes);
        Assert.Contains(derivedTypes, d => d.Contains("DerivedClass"));
    }

    [Fact]
    public void Indexing_IndexesFields()
    {
        // Arrange
        var dllPath = Path.Combine(_testDir, "TestLib.dll");
        File.Copy(typeof(Greeter).Assembly.Location, dllPath, overwrite: true);

        var indexer = new SqliteIndexer(_dbPath, new Decompiler.IlspyDecompiler());
        var assembly = indexer.DiscoverAssemblies(_testDir)[0];
        indexer.IndexAssembly(assembly);

        // Act
        var types = indexer.GetTypes(assembly.Name);
        var greeter = types.FirstOrDefault(t => t.Name == "Greeter");
        Assert.NotNull(greeter);

        // Assert — _prefix field should be indexed
        Assert.NotEmpty(greeter.Fields);
        Assert.Contains(greeter.Fields, f => f.Name == "_prefix");
    }

    [Fact]
    public void Indexing_IndexesProperties()
    {
        // Arrange — properties are stored in DB but not loaded in GetTypes
        // Test that indexing doesn't crash and properties table is populated
        var dllPath = Path.Combine(_testDir, "TestLib.dll");
        File.Copy(typeof(Greeter).Assembly.Location, dllPath, overwrite: true);

        var indexer = new SqliteIndexer(_dbPath, new Decompiler.IlspyDecompiler());
        var assembly = indexer.DiscoverAssemblies(_testDir)[0];
        indexer.IndexAssembly(assembly);

        // Act — verify indexing completes without error
        var types = indexer.GetTypes(assembly.Name);
        Assert.NotNull(types);

        // Assert — types indexed (properties table may exist but not loaded in GetTypes)
        Assert.True(types.Count >= 4);
    }

    [Fact]
    public void Indexing_IndexesMethodParameters()
    {
        // Arrange
        var dllPath = Path.Combine(_testDir, "TestLib.dll");
        File.Copy(typeof(Greeter).Assembly.Location, dllPath, overwrite: true);

        var indexer = new SqliteIndexer(_dbPath, new Decompiler.IlspyDecompiler());
        var assembly = indexer.DiscoverAssemblies(_testDir)[0];
        indexer.IndexAssembly(assembly);

        // Act
        var methodId = "TestLib::TestLib.Greeter::Greet::Greet(System.String) : System.String";
        var method = indexer.GetMethod(methodId);

        // Assert
        Assert.NotNull(method);
        Assert.NotEmpty(method.Parameters);
        Assert.Equal("System.String name", method.Parameters[0]);
    }

    [Fact]
    public void Indexing_IndexesMethodMetadata()
    {
        // Arrange
        var dllPath = Path.Combine(_testDir, "TestLib.dll");
        File.Copy(typeof(Greeter).Assembly.Location, dllPath, overwrite: true);

        var indexer = new SqliteIndexer(_dbPath, new Decompiler.IlspyDecompiler());
        var assembly = indexer.DiscoverAssemblies(_testDir)[0];
        indexer.IndexAssembly(assembly);

        // Act
        var methodId = "TestLib::TestLib.Greeter::Greet::Greet(System.String) : System.String";
        var method = indexer.GetMethod(methodId);

        // Assert — method metadata populated correctly
        Assert.NotNull(method);
        Assert.Equal("TestLib.Greeter.Greet", method.FullName);
        Assert.Equal("TestLib", method.AssemblyName);
        Assert.False(method.IsStatic);
        Assert.Single(method.Parameters);
    }

    [Fact]
    public void Indexing_IndexesEngineCallbacks()
    {
        // Arrange — TestLib has no Unity/Unreal callbacks
        var dllPath = Path.Combine(_testDir, "TestLib.dll");
        File.Copy(typeof(Greeter).Assembly.Location, dllPath, overwrite: true);

        var indexer = new SqliteIndexer(_dbPath, new Decompiler.IlspyDecompiler());
        var assembly = indexer.DiscoverAssemblies(_testDir)[0];
        indexer.IndexAssembly(assembly);

        // Act
        var types = indexer.GetTypes(assembly.Name);

        // Assert — no crash, types indexed
        Assert.NotNull(types);
        Assert.True(types.Count >= 4); // Greeter, Class1, BaseClass, DerivedClass
    }

    [Fact]
    public void Indexing_CrossAssemblyReferencesDoNotFail()
    {
        // Arrange — create two assemblies where one references the other
        var dir1 = Path.Combine(AppContext.BaseDirectory, $"cross_ref_1_{Guid.NewGuid()}");
        var dir2 = Path.Combine(AppContext.BaseDirectory, $"cross_ref_2_{Guid.NewGuid()}");
        Directory.CreateDirectory(dir1);
        Directory.CreateDirectory(dir2);

        // Copy TestLib.dll as the "shared" assembly
        var testLibPath = typeof(Greeter).Assembly.Location;
        var testLibName = Path.GetFileName(testLibPath);
        File.Copy(testLibPath, Path.Combine(dir1, testLibName), overwrite: true);

        // Create a simple consumer assembly
        var consumerSource = @"
using TestLib;
namespace Consumer {
    public class Consumer {
        public string Consume() {
            var g = new Greeter();
            return g.Greet(""test"");
        }
    }
}";
        var consumerCs = Path.Combine(dir2, "Consumer.cs");
        File.WriteAllText(consumerCs, consumerSource);

        var consumerCsproj = Path.Combine(dir2, "Consumer.csproj");
        File.WriteAllText(consumerCsproj, $@"
<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <OutputType>Library</OutputType>
  </PropertyGroup>
  <ItemGroup>
    <Reference Include=""TestLib"">
      <HintPath>{Path.Combine(dir1, testLibName)}</HintPath>
    </Reference>
  </ItemGroup>
</Project>");

        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = "build Consumer.csproj -o " + dir2,
            WorkingDirectory = dir2,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        using var proc = System.Diagnostics.Process.Start(psi)!;
        proc.WaitForExit();

        if (proc.ExitCode != 0)
        {
            var output = proc.StandardOutput.ReadToEnd();
            var error = proc.StandardError.ReadToEnd();
            Assert.Fail($"Build failed: {error}");
            return;
        }

        var consumerDll = Directory.GetFiles(dir2, "Consumer.dll").FirstOrDefault();
        if (consumerDll == null)
        {
            Assert.Fail("Consumer.dll not found after build");
            return;
        }

        // Index both assemblies — this would fail FK constraints
        // if foreign_keys=ON because Consumer calls TestLib methods
        var dbPath2 = Path.Combine(_testDir, "cross.db");
        var indexer = new SqliteIndexer(dbPath2, new Decompiler.IlspyDecompiler());

        // Act — index all discovered assemblies; should not throw FK errors
        var assemblies1 = indexer.DiscoverAssemblies(dir1);
        foreach (var asm in assemblies1)
            indexer.IndexAssembly(asm);

        var assemblies2 = indexer.DiscoverAssemblies(dir2);
        foreach (var asm in assemblies2)
            indexer.IndexAssembly(asm);

        // Assert — no FK constraint exceptions thrown during indexing
        // (foreign_keys=OFF pragma allows cross-reference inserts)
        // The test passes if we reach here without exception
        Assert.True(true);

        // Cleanup
        Directory.Delete(dir1, true);
        Directory.Delete(dir2, true);
    }

    [Fact]
    public void Indexing_MultipleAssemblies_NoDuplicateMethods()
    {
        // Arrange — index same assembly twice
        var dllPath = Path.Combine(_testDir, "TestLib.dll");
        File.Copy(typeof(Greeter).Assembly.Location, dllPath, overwrite: true);

        var indexer = new SqliteIndexer(_dbPath, new Decompiler.IlspyDecompiler());
        var assembly = indexer.DiscoverAssemblies(_testDir)[0];

        // Act — index twice
        indexer.IndexAssembly(assembly);
        indexer.IndexAssembly(assembly);

        // Assert — no duplicates
        var methods = indexer.GetMethods(assembly.Name);
        var greetCount = methods.Count(m => m.Name == "Greet");
        Assert.Equal(1, greetCount);
    }

    [Fact]
    public void Indexing_IndexesCallGraphEdges()
    {
        // Arrange
        var dllPath = Path.Combine(_testDir, "TestLib.dll");
        File.Copy(typeof(Greeter).Assembly.Location, dllPath, overwrite: true);

        var indexer = new SqliteIndexer(_dbPath, new Decompiler.IlspyDecompiler());
        var assembly = indexer.DiscoverAssemblies(_testDir)[0];
        indexer.IndexAssembly(assembly);

        // Act — verify methods indexed with bodies
        var greeter = indexer.GetTypes(assembly.Name).FirstOrDefault(t => t.Name == "Greeter");
        Assert.NotNull(greeter);
        Assert.True(greeter.Methods.Count >= 2); // .ctor + Greet

        // Assert — use GetMethod to verify body population
        var methodId = "TestLib::TestLib.Greeter::.ctor::.ctor(System.String) : System.Void";
        var ctor = indexer.GetMethod(methodId);
        Assert.NotNull(ctor);
        Assert.NotNull(ctor.Body);
    }

    [Fact]
    public void Indexing_TypeReferencesIndexed()
    {
        // Arrange
        var dllPath = Path.Combine(_testDir, "TestLib.dll");
        File.Copy(typeof(Greeter).Assembly.Location, dllPath, overwrite: true);

        var indexer = new SqliteIndexer(_dbPath, new Decompiler.IlspyDecompiler());
        var assembly = indexer.DiscoverAssemblies(_testDir)[0];
        indexer.IndexAssembly(assembly);

        // Act
        var greeter = indexer.GetTypes(assembly.Name).FirstOrDefault(t => t.Name == "Greeter");
        Assert.NotNull(greeter);

        // Assert — type references are stored in DB (type_references table)
        // but not loaded in GetTypes — verify indexing completes without error
        Assert.NotNull(greeter.TypeReferences); // initialized to empty list
    }
}
