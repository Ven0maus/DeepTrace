using DeepTrace.Decompiler;
using DeepTrace.Indexing;
using Xunit;
using Xunit.Abstractions;

namespace DeepTrace.Tests;

public class BenchmarkIndexingTests
{
    private readonly string _testDir;
    private readonly string _dbPath;
    private readonly ITestOutputHelper _output;

    public BenchmarkIndexingTests(ITestOutputHelper output)
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"deeptrace_bench_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDir);
        _dbPath = Path.Combine(_testDir, "bench.db");
        _output = output;
    }

    [Fact]
    public void Benchmark_IndexLargeAssembly()
    {
        // Arrange — copy BenchmarkLib.dll to temp dir
        var dllPath = Path.Combine(_testDir, "BenchmarkLib.dll");
        File.Copy(
            typeof(BenchmarkLib.Data.ProcessRecord0).Assembly.Location,
            dllPath,
            overwrite: true);

        var indexer = new SqliteIndexer(_dbPath, new IlspyDecompiler());
        var assembly = indexer.DiscoverAssemblies(_testDir)[0];

        // Act — index
        var watch = System.Diagnostics.Stopwatch.StartNew();
        indexer.IndexAssembly(assembly);
        watch.Stop();

        var types = indexer.GetTypes(assembly.Name);
        var totalMethods = types.SelectMany(t => t.Methods).Count();

        _output.WriteLine($"Indexed {types.Count} types with {totalMethods} methods in {watch.ElapsedMilliseconds}ms");
        _output.WriteLine($"Types/sec: {types.Count / (watch.ElapsedMilliseconds / 1000.0):F0}");

        // Assert — basic sanity
        Assert.True(types.Count >= 100, $"Expected at least 100 types, got {types.Count}");
        Assert.True(totalMethods >= 1000, $"Expected at least 1000 methods, got {totalMethods}");
    }

    [Fact]
    public void Benchmark_SearchLargeIndex()
    {
        // Arrange
        var dllPath = Path.Combine(_testDir, "BenchmarkLib.dll");
        File.Copy(
            typeof(BenchmarkLib.Data.ProcessRecord0).Assembly.Location,
            dllPath,
            overwrite: true);

        var indexer = new SqliteIndexer(_dbPath, new IlspyDecompiler());
        var assembly = indexer.DiscoverAssemblies(_testDir)[0];
        indexer.IndexAssembly(assembly);

        // Act — search
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var results = indexer.Search("Execute", 500);
        watch.Stop();

        _output.WriteLine($"Search 'Execute': {results.Count} results in {watch.ElapsedMilliseconds}ms");

        Assert.NotEmpty(results);
    }

    [Fact]
    public void Benchmark_MultipleIndexCycles()
    {
        // Arrange
        var dllPath = Path.Combine(_testDir, "BenchmarkLib.dll");
        File.Copy(
            typeof(BenchmarkLib.Data.ProcessRecord0).Assembly.Location,
            dllPath,
            overwrite: true);

        var iterations = 5;
        var times = new List<long>();

        // Act
        for (var i = 0; i < iterations; i++)
        {
            var dbPath = Path.Combine(_testDir, $"bench_{i}.db");
            var indexer = new SqliteIndexer(dbPath, new IlspyDecompiler());
            var assembly = indexer.DiscoverAssemblies(_testDir)[0];

            var watch = System.Diagnostics.Stopwatch.StartNew();
            indexer.IndexAssembly(assembly);
            watch.Stop();

            times.Add(watch.ElapsedMilliseconds);
        }

        var avg = times.Average();
        var min = times.Min();
        var max = times.Max();

        _output.WriteLine($"5x index cycles: avg={avg:F0}ms, min={min}ms, max={max}ms");
        _output.WriteLine($"Times: {string.Join(", ", times)}");
    }
}

// Silent helper to avoid ITestOutputHelper dependency in parameterless constructor
internal class SilentTestOutputHelper : ITestOutputHelper
{
    public string OutputString { get; set; } = string.Empty;
    public bool IsThreadSafe => false;
    public string TestCaseDisplayName => string.Empty;
    public void Write(string message) { }
    public void Write(string format, params object?[] args) { }
    public void WriteLine(string message) { }
    public void WriteLine(string format, params object?[] args) { }
}
