using System.Linq;
using DeepTrace.Core.Models;
using DeepTrace.Core.Runtime;
using DeepTrace.Decompiler;
using DeepTrace.Indexing;
using Xunit;
using Xunit.Abstractions;

namespace DeepTrace.Tests;

/// <summary>
/// Benchmarks for every CLI command path.
/// Profiles on BenchmarkLib (100 types, ~1100 methods) to identify bottlenecks.
/// Uses a shared pre-indexed database to isolate query time from indexing time.
/// </summary>
[Collection("CliBenchCollection")]
public class CliCommandBenchmarks
{
    private readonly ITestOutputHelper _output;
    private readonly SharedBenchDb _sharedDb;
    private readonly string _assemblyName = "BenchmarkLib";

    public CliCommandBenchmarks(ITestOutputHelper output, SharedBenchDb sharedDb)
    {
        _output = output;
        _sharedDb = sharedDb;
    }

    #region Index phase profiling

    [Fact]
    public void Benchmark_IndexPhaseBreakdown()
    {
        var testDir = Path.Combine(Path.GetTempPath(), $"deeptrace_phase_{Guid.NewGuid()}");
        Directory.CreateDirectory(testDir);
        var dbPath = Path.Combine(testDir, "phase.db");
        var dllSrc = typeof(BenchmarkLib.Data.ProcessRecord0).Assembly.Location;
        var dllPath = Path.Combine(testDir, "BenchmarkLib.dll");
        File.Copy(dllSrc, dllPath, overwrite: true);

        // Cecil load + type enumeration
        var sw = System.Diagnostics.Stopwatch.StartNew();
        using var assemblyDef = Mono.Cecil.AssemblyDefinition.ReadAssembly(dllPath, new Mono.Cecil.ReaderParameters());
        var typeCount = assemblyDef.MainModule.Types.Count(t => t.Namespace != "<>c" && t.Namespace != "<PrivateImplementationDetails>");
        var methodCount = assemblyDef.MainModule.Types.Sum(t => t.Methods.Count(m => !m.Name.StartsWith("get_") && !m.Name.StartsWith("set_") && !m.Name.StartsWith("add_") && !m.Name.StartsWith("remove_")));
        var tCecil = sw.ElapsedMilliseconds;

        // Decompile all methods (what IndexAssembly does internally)
        var decompiler = new IlspyDecompiler();
        sw.Restart();
        var totalBodies = 0;
        foreach (var type in assemblyDef.MainModule.Types)
        {
            if (type.Namespace == "<>c" || type.Namespace == "<PrivateImplementationDetails>")
                continue;
            foreach (var method in type.Methods)
            {
                if (method.Name.StartsWith("get_") || method.Name.StartsWith("set_") ||
                    method.Name.StartsWith("add_") || method.Name.StartsWith("remove_"))
                    continue;
                if (!method.IsAbstract && method.HasBody && method.Body.Instructions != null)
                {
                    var sig = $"{method.Name}({string.Join(", ", method.Parameters.Select(p => p.ParameterType.FullName))}) : {method.ReturnType.FullName}";
                    var body = decompiler.TryDecompileMethod(dllPath, type.FullName, sig);
                    if (body != null) totalBodies++;
                }
            }
        }
        var tDecompile = sw.ElapsedMilliseconds;

        // IL parsing for call graph (what ExtractCallGraph does)
        sw.Restart();
        var totalInstructions = 0;
        foreach (var type in assemblyDef.MainModule.Types)
        {
            if (type.Namespace == "<>c" || type.Namespace == "<PrivateImplementationDetails>")
                continue;
            foreach (var method in type.Methods)
            {
                if (method.Name.StartsWith("get_") || method.Name.StartsWith("set_") ||
                    method.Name.StartsWith("add_") || method.Name.StartsWith("remove_"))
                    continue;
                if (!method.IsAbstract && method.HasBody && method.Body.Instructions != null)
                {
                    foreach (var instr in method.Body.Instructions)
                    {
                        totalInstructions++;
                        if (instr.Operand is Mono.Cecil.MethodReference methodRef)
                        {
                            // This is the expensive Resolve() call
                            var resolved = methodRef.Resolve();
                        }
                    }
                }
            }
        }
        var tIlParsing = sw.ElapsedMilliseconds;

        var total = tCecil + tDecompile + tIlParsing;

        _output.WriteLine("=== GRANULAR INDEX BREAKDOWN ===");
        _output.WriteLine($"  Cecil load + scan:  {tCecil}ms ({typeCount} types, {methodCount} methods)");
        _output.WriteLine($"  Decompile bodies:   {tDecompile}ms ({totalBodies} bodies decompiled)");
        _output.WriteLine($"  IL parse + resolve: {tIlParsing}ms ({totalInstructions} instructions, {methodCount} methods)");
        _output.WriteLine($"  Subtotal:           {total}ms");

        // Full index for comparison
        sw.Restart();
        var indexer = new SqliteIndexer(dbPath, new IlspyDecompiler());
        var assemblies = new AssemblyDiscoverer().Discover(testDir);
        indexer.IndexAssembly(assemblies[0]);
        var tFullIndex = sw.ElapsedMilliseconds;

        var grandTotal = total + tFullIndex;
        _output.WriteLine($"  Full index (w/ DB): {tFullIndex}ms");
        _output.WriteLine($"  Grand total:        {grandTotal}ms");

        var types = indexer.GetTypes(_assemblyName);
        var methods = types.SelectMany(t => t.Methods).Count();
        _output.WriteLine($"  Result: {types.Count} types, {methods} methods");

        Assert.True(types.Count >= 100);
        Assert.True(methods >= 500);
    }

    [Fact]
    public void Benchmark_Index10xCycles()
    {
        var testDir = Path.Combine(Path.GetTempPath(), $"deeptrace_10x_{Guid.NewGuid()}");
        Directory.CreateDirectory(testDir);
        var dllSrc = typeof(BenchmarkLib.Data.ProcessRecord0).Assembly.Location;
        var dllPath = Path.Combine(testDir, "BenchmarkLib.dll");
        File.Copy(dllSrc, dllPath, overwrite: true);

        var times = new List<long>();
        for (var i = 0; i < 10; i++)
        {
            var dbPath = Path.Combine(testDir, $"bench_{i}.db");
            var indexer = new SqliteIndexer(dbPath, new IlspyDecompiler());
            var assemblies = indexer.DiscoverAssemblies(testDir);
            var sw = System.Diagnostics.Stopwatch.StartNew();
            indexer.IndexAssembly(assemblies[0]);
            sw.Stop();
            times.Add(sw.ElapsedMilliseconds);
        }
        var avg = times.Average();
        var min = times.Min();
        var max = times.Max();
        _output.WriteLine($"10x index: avg={avg:F0}ms, min={min}ms, max={max}ms");
        _output.WriteLine($"Times: {string.Join(", ", times)}");
    }

    #endregion

    #region Discovery (CLI: discover)

    [Fact]
    public void Benchmark_DiscoverAssemblies()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var assemblies = _sharedDb.Indexer.DiscoverAssemblies(_sharedDb.WorkDir);
        sw.Stop();
        _output.WriteLine($"discover: {assemblies.Count} assemblies in {sw.ElapsedMilliseconds}ms");
        Assert.True(assemblies.Count >= 1);
    }

    #endregion

    #region Search (CLI: search)

    [Fact]
    public void Benchmark_SearchByName()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var results = _sharedDb.Indexer.Search("Execute", 500);
        sw.Stop();
        _output.WriteLine($"search(name='Execute',limit=500): {results.Count} results in {sw.ElapsedMilliseconds}ms");
        Assert.True(results.Count >= 100);
    }

    [Fact]
    public void Benchmark_SearchByBody()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var results = _sharedDb.Indexer.Search("string.Empty", 200);
        sw.Stop();
        _output.WriteLine($"search(body='string.Empty',limit=200): {results.Count} results in {sw.ElapsedMilliseconds}ms");
    }

    [Fact]
    public void Benchmark_SearchExactName()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var results = _sharedDb.Indexer.Search("Execute", 10);
        sw.Stop();
        _output.WriteLine($"search(exact='Execute',limit=10): {results.Count} results in {sw.ElapsedMilliseconds}ms");
    }

    #endregion

    #region Methods (CLI: methods)

    [Fact]
    public void Benchmark_GetMethods()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var methods = _sharedDb.Indexer.GetMethods(_assemblyName);
        sw.Stop();
        _output.WriteLine($"methods: {methods.Count} methods in {sw.ElapsedMilliseconds}ms");
        Assert.True(methods.Count >= 500);
    }

    #endregion

    #region Method detail (CLI: method)

    [Fact]
    public void Benchmark_GetMethod()
    {
        var methods = _sharedDb.Indexer.GetMethods(_assemblyName);
        var sampleMethod = methods.First();

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = _sharedDb.Indexer.GetMethod(sampleMethod.SymbolId);
        sw.Stop();
        _output.WriteLine($"method({sampleMethod.Name}): {sw.ElapsedMilliseconds}ms");
        Assert.NotNull(result);
    }

    [Fact]
    public void Benchmark_GetMethod100Calls()
    {
        var methods = _sharedDb.Indexer.GetMethods(_assemblyName).Take(100).ToList();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        foreach (var m in methods)
        {
            var result = _sharedDb.Indexer.GetMethod(m.SymbolId);
            Assert.NotNull(result);
        }
        sw.Stop();
        _output.WriteLine($"method x100: {sw.ElapsedMilliseconds}ms ({sw.ElapsedMilliseconds / 100:F0}ms each)");
    }

    #endregion

    #region Types (CLI: types, type)

    [Fact]
    public void Benchmark_GetTypes()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var types = _sharedDb.Indexer.GetTypes(_assemblyName);
        sw.Stop();
        _output.WriteLine($"types: {types.Count} types in {sw.ElapsedMilliseconds}ms");
        Assert.True(types.Count >= 100);
    }

    [Fact]
    public void Benchmark_GetTypeInfo()
    {
        var types = _sharedDb.Indexer.GetTypes(_assemblyName);
        var sampleType = types.First(t => t.Name != "<Module>");

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = _sharedDb.Indexer.GetTypeInfo(_assemblyName, sampleType.FullName);
        sw.Stop();
        _output.WriteLine($"type({sampleType.Name}): {sw.ElapsedMilliseconds}ms");
        Assert.NotNull(result);
    }

    [Fact]
    public void Benchmark_GetTypeInfo50Calls()
    {
        var types = _sharedDb.Indexer.GetTypes(_assemblyName).Where(t => t.Name != "<Module>").Take(50).ToList();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        foreach (var t in types)
        {
            var result = _sharedDb.Indexer.GetTypeInfo(_assemblyName, t.FullName);
            Assert.NotNull(result);
        }
        sw.Stop();
        _output.WriteLine($"type x50: {sw.ElapsedMilliseconds}ms ({sw.ElapsedMilliseconds / 50:F0}ms each)");
    }

    #endregion

    #region Relationships (CLI: callers, callees, references)

    [Fact]
    public void Benchmark_GetCallers()
    {
        var methods = _sharedDb.Indexer.GetMethods(_assemblyName);
        // Find a method with callers
        var withCallers = methods.FirstOrDefault(m => _sharedDb.Indexer.GetCallers(m.SymbolId).Count > 0);
        if (withCallers == null)
        {
            _output.WriteLine("No methods with callers found, skipping");
            return;
        }

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var callers = _sharedDb.Indexer.GetCallers(withCallers.SymbolId);
        sw.Stop();
        _output.WriteLine($"callers({withCallers.Name}): {callers.Count} in {sw.ElapsedMilliseconds}ms");
    }

    [Fact]
    public void Benchmark_GetCallees()
    {
        var methods = _sharedDb.Indexer.GetMethods(_assemblyName);
        var sampleMethod = methods.First();

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var callees = _sharedDb.Indexer.GetCallees(sampleMethod.SymbolId);
        sw.Stop();
        _output.WriteLine($"callees({sampleMethod.Name}): {callees.Count} in {sw.ElapsedMilliseconds}ms");
    }

    [Fact]
    public void Benchmark_SearchReferences()
    {
        var methods = _sharedDb.Indexer.GetMethods(_assemblyName);
        var sampleMethod = methods.First();

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var refs = _sharedDb.Indexer.SearchReferences(sampleMethod.SymbolId);
        sw.Stop();
        _output.WriteLine($"references({sampleMethod.Name}): {refs.Count} in {sw.ElapsedMilliseconds}ms");
    }

    #endregion

    #region Call graph + engine callbacks (CLI: call-graph, engine-callbacks, interface-implementations)

    [Fact]
    public void Benchmark_GetCallGraphEdges()
    {
        var methods = _sharedDb.Indexer.GetMethods(_assemblyName);
        var sampleMethod = methods.First();

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var edges = _sharedDb.Indexer.GetCallGraphEdges(sampleMethod.SymbolId);
        sw.Stop();
        _output.WriteLine($"call-graph({sampleMethod.Name}): {edges.Count} edges in {sw.ElapsedMilliseconds}ms");
    }

    [Fact]
    public void Benchmark_GetEngineCallbacks()
    {
        var types = _sharedDb.Indexer.GetTypes(_assemblyName);
        var sampleType = types.First();
        var typeId = sampleType.SymbolId;

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var callbacks = _sharedDb.Indexer.GetEngineCallbacks(typeId);
        sw.Stop();
        _output.WriteLine($"engine-callbacks({sampleType.Name}): {callbacks.Count} in {sw.ElapsedMilliseconds}ms");
    }

    [Fact]
    public void Benchmark_GetInterfaceImplementations()
    {
        var types = _sharedDb.Indexer.GetTypes(_assemblyName);
        var iface = types.FirstOrDefault(t => t.Interfaces.Count == 0 && t.FullName.Contains("I"));
        if (iface == null)
        {
            _output.WriteLine("No suitable interface found, skipping");
            return;
        }

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var impls = _sharedDb.Indexer.GetInterfaceImplementations(_assemblyName, iface.Name);
        sw.Stop();
        _output.WriteLine($"interface-implementations({iface.Name}): {impls.Count} in {sw.ElapsedMilliseconds}ms");
    }

    #endregion

    #region Runtime observations (CLI: observe, runtime)

    [Fact]
    public void Benchmark_StoreAndGetRuntimeObservation()
    {
        var methods = _sharedDb.Indexer.GetMethods(_assemblyName);
        var sampleMethod = methods.First();

        var observation = new RuntimeObservation
        {
            Id = Guid.NewGuid().ToString(),
            SymbolId = sampleMethod.SymbolId,
            FullyQualifiedName = sampleMethod.FullName,
            Assembly = _assemblyName,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Depth = 0,
            Parameters = "{\"arg\":42}",
            ReturnValue = "42",
            DurationMs = 1.5,
        };
        _sharedDb.Indexer.StoreRuntimeObservation(observation);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var obs = _sharedDb.Indexer.GetRuntimeObservations(sampleMethod.SymbolId);
        sw.Stop();
        _output.WriteLine($"runtime({sampleMethod.Name}): {obs.Count} observations in {sw.ElapsedMilliseconds}ms");
        Assert.NotEmpty(obs);
    }

    #endregion

    #region Recipes (CLI: recipes)

    [Fact]
    public void Benchmark_RecipeList()
    {
        var recipes = DeepTrace.Core.Ai.InvestigationRecipes.Recipes;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var count = recipes.Count;
        sw.Stop();
        _output.WriteLine($"recipes(list): {count} recipes in {sw.ElapsedMilliseconds}ms");
    }

    [Fact]
    public void Benchmark_RecipeExecute()
    {
        var recipe = DeepTrace.Core.Ai.InvestigationRecipes.Recipes.First();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var text = DeepTrace.Core.Ai.InvestigationRecipes.ExecuteAsText(recipe, _sharedDb.Indexer);
        sw.Stop();
        _output.WriteLine($"recipes(execute='{recipe.Name}'): {text.Length} chars in {sw.ElapsedMilliseconds}ms");
        Assert.NotEmpty(text);
    }

    #endregion

    #region Evaluate (CLI: evaluate)

    [Fact]
    public void Benchmark_Evaluate()
    {
        var evalDir = Path.Combine(Path.GetTempPath(), $"deeptrace_eval_{Guid.NewGuid()}");
        Directory.CreateDirectory(evalDir);
        var evalDb = Path.Combine(evalDir, "eval.db");
        var dllSrc = typeof(BenchmarkLib.Data.ProcessRecord0).Assembly.Location;
        File.Copy(dllSrc, Path.Combine(evalDir, "BenchmarkLib.dll"), overwrite: true);

        try
        {
            var evalIndexer = new SqliteIndexer(evalDb, new IlspyDecompiler());
            var assemblies = evalIndexer.DiscoverAssemblies(evalDir);
            foreach (var asm in assemblies)
                evalIndexer.IndexAssembly(asm);

            var greeterMethods = evalIndexer.GetMethods(assemblies[0].Name)
                .Where(m => m.DeclaringTypeFullName.Contains("BenchmarkLib"))
                .Select(m => m.SymbolId)
                .ToList();

            var evaluations = new List<(string, IReadOnlyList<string>)>
            {
                ("BenchmarkLib", greeterMethods),
            };

            var sw = System.Diagnostics.Stopwatch.StartNew();
            var report = DeepTrace.Core.Retrieval.RetrievalEvaluator.Evaluate(evaluations, evalIndexer);
            sw.Stop();
            _output.WriteLine($"evaluate: {sw.ElapsedMilliseconds}ms");

            var formatted = DeepTrace.Core.Retrieval.RetrievalEvaluator.FormatReport(report);
            _output.WriteLine(formatted);

            evalIndexer.Dispose();
        }
        finally
        {
            Directory.Delete(evalDir, true);
        }
    }

    #endregion

    #region Stats (CLI: stats)

    [Fact]
    public void Benchmark_Stats()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        using var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={_sharedDb.DbPath}");
        conn.Open();
        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT 'assemblies' as tbl, COUNT(*) as cnt FROM assemblies
            UNION ALL SELECT 'symbols', COUNT(*) FROM symbols
            UNION ALL SELECT 'methods', COUNT(*) FROM methods
            UNION ALL SELECT 'fields', COUNT(*) FROM fields
            UNION ALL SELECT 'type_interfaces', COUNT(*) FROM type_interfaces
            UNION ALL SELECT 'method_parameters', COUNT(*) FROM method_parameters
            UNION ALL SELECT 'method_callers', COUNT(*) FROM method_callers
            UNION ALL SELECT 'method_callees', COUNT(*) FROM method_callees
            UNION ALL SELECT 'call_graph_edges', COUNT(*) FROM call_graph_edges
            UNION ALL SELECT 'type_hierarchy', COUNT(*) FROM type_hierarchy
            UNION ALL SELECT 'type_references', COUNT(*) FROM type_references;";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            _output.WriteLine($"  {reader.GetString(0)}: {reader.GetInt32(1)}");
        sw.Stop();
        _output.WriteLine($"stats(query): {sw.ElapsedMilliseconds}ms");
    }

    #endregion

    #region Full CLI workflow (CLI: index + search + method + callers + type)

    [Fact]
    public void Benchmark_FullCliWorkflow()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        // 1. discover
        var assemblies = _sharedDb.Indexer.DiscoverAssemblies(_sharedDb.WorkDir);

        // 2. index (already done by shared setup, but measure it fresh)
        var sw2 = System.Diagnostics.Stopwatch.StartNew();
        var freshDbPath = Path.Combine(Path.GetDirectoryName(typeof(CliCommandBenchmarks).Assembly.Location)!, "benchdata", $"workflow_{Guid.NewGuid()}.db");
        var freshIndexer = new SqliteIndexer(freshDbPath, new IlspyDecompiler());
        freshIndexer.IndexAssembly(assemblies[0]);
        var tIndex = sw2.ElapsedMilliseconds;
        freshIndexer.Dispose();
        // Clean up fresh DB after use
        try { File.Delete(freshDbPath); } catch { }

        // 3. search
        var searchResults = _sharedDb.Indexer.Search("Execute", 100);

        // 4. methods
        var methods = _sharedDb.Indexer.GetMethods(_assemblyName);

        // 5. method detail
        var methodDetail = _sharedDb.Indexer.GetMethod(methods.First().SymbolId);

        // 6. types
        var types = _sharedDb.Indexer.GetTypes(_assemblyName);

        // 7. type detail
        var typeDetail = _sharedDb.Indexer.GetTypeInfo(_assemblyName, types.First(t => t.Name != "<Module>").FullName);

        // 8. callers
        var callers = _sharedDb.Indexer.GetCallers(methods.First().SymbolId);

        // 9. callees
        var callees = _sharedDb.Indexer.GetCallees(methods.First().SymbolId);

        sw.Stop();

        _output.WriteLine("Full CLI workflow:");
        _output.WriteLine($"  discover: {assemblies.Count} assemblies");
        _output.WriteLine($"  index: {tIndex}ms");
        _output.WriteLine($"  search: {searchResults.Count} results");
        _output.WriteLine($"  methods: {methods.Count} methods");
        _output.WriteLine($"  method: {methodDetail?.Name}");
        _output.WriteLine($"  types: {types.Count} types");
        _output.WriteLine($"  type: {typeDetail?.Name}");
        _output.WriteLine($"  callers: {callers.Count}");
        _output.WriteLine($"  callees: {callees.Count}");
        _output.WriteLine($"  total: {sw.ElapsedMilliseconds}ms");

        Assert.True(sw.ElapsedMilliseconds > 0);
    }

    #endregion
}

/// <summary>
/// Shared pre-indexed database for all benchmarks in this collection.
/// Creates the DB in the test project's output directory from freshly built DLLs.
/// </summary>
public class SharedBenchDb : IDisposable
{
    public readonly string DbPath;
    public readonly string WorkDir;
    public readonly SqliteIndexer Indexer;
    private bool _disposed;

    public SharedBenchDb()
    {
        // Use test assembly directory so the DB lives alongside the built test binary
        var testDir = Path.GetDirectoryName(typeof(CliCommandBenchmarks).Assembly.Location)!;
        WorkDir = Path.Combine(testDir, "benchdata");
        Directory.CreateDirectory(WorkDir);

        DbPath = Path.Combine(WorkDir, "bench.db");

        // Use the freshly built BenchmarkLib DLL from project references
        var dllSrc = typeof(BenchmarkLib.Data.ProcessRecord0).Assembly.Location;
        var dllPath = Path.Combine(WorkDir, "BenchmarkLib.dll");
        File.Copy(dllSrc, dllPath, overwrite: true);

        Indexer = new SqliteIndexer(DbPath, new IlspyDecompiler());
        var assemblies = Indexer.DiscoverAssemblies(WorkDir);
        Indexer.IndexAssembly(assemblies[0]);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Indexer.Dispose();
        try { Directory.Delete(WorkDir, true); }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to clean up benchdata: {ex.Message}");
        }
    }
}

[CollectionDefinition("CliBenchCollection")]
public class CliBenchCollection : ICollectionFixture<SharedBenchDb> { }
