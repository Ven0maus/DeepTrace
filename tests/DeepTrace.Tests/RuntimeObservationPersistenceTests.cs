using DeepTrace.Core.Runtime;
using DeepTrace.Indexing;
using Xunit;

namespace DeepTrace.Tests;

public class RuntimeObservationPersistenceTests
{
    private readonly string _testDir;
    private readonly string _dbPath;

    public RuntimeObservationPersistenceTests()
    {
        _testDir = Path.Combine(AppContext.BaseDirectory, $"deeptrace_rt_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDir);
        _dbPath = Path.Combine(_testDir, "test.db");
    }

    [Fact]
    public void StoreAndGetRuntimeObservation_PersistsAndRetrieves()
    {
        var indexer = new SqliteIndexer(_dbPath, new Decompiler.IlspyDecompiler());
        var observation = new RuntimeObservation
        {
            Id = "test_obs_1",
            SymbolId = "TestLib::TestLib.Greeter::Greet::Greet(System.String) : System.String",
            FullyQualifiedName = "TestLib.Greeter.Greet",
            Assembly = "TestLib",
            Timestamp = 1000000,
            Depth = 1,
            Parameters = "{\"name\":\"World\"}",
            ReturnValue = "\"Hello, World!\"",
            DurationMs = 42.5,
        };

        indexer.StoreRuntimeObservation(observation);

        var retrieved = indexer.GetRuntimeObservations(observation.SymbolId);
        Assert.Single(retrieved);
        Assert.Equal(observation.SymbolId, retrieved[0].SymbolId);
        Assert.Equal("TestLib.Greeter.Greet", retrieved[0].FullyQualifiedName);
        Assert.Equal("TestLib", retrieved[0].Assembly);
        Assert.Equal(1, retrieved[0].Depth);
        Assert.Equal("{\"name\":\"World\"}", retrieved[0].Parameters);
        Assert.Equal("\"Hello, World!\"", retrieved[0].ReturnValue);
        Assert.Equal(42.5, retrieved[0].DurationMs);
    }

    [Fact]
    public void StoreMultipleObservations_RetrievedCorrectly()
    {
        var indexer = new SqliteIndexer(_dbPath, new Decompiler.IlspyDecompiler());

        indexer.StoreRuntimeObservation(new RuntimeObservation
        {
            Id = "obs_1", SymbolId = "sym_1", FullyQualifiedName = "A.Method",
            Assembly = "TestAssembly", Timestamp = 1, Depth = 0, DurationMs = 10,
        });

        indexer.StoreRuntimeObservation(new RuntimeObservation
        {
            Id = "obs_2", SymbolId = "sym_1", FullyQualifiedName = "A.Method",
            Assembly = "TestAssembly", Timestamp = 2, Depth = 1, DurationMs = 20,
        });

        indexer.StoreRuntimeObservation(new RuntimeObservation
        {
            Id = "obs_3", SymbolId = "sym_2", FullyQualifiedName = "B.Method",
            Assembly = "TestAssembly", Timestamp = 3, Depth = 0, DurationMs = 30,
        });

        var sym1Obs = indexer.GetRuntimeObservations("sym_1");
        var sym2Obs = indexer.GetRuntimeObservations("sym_2");
        var allObs = indexer.GetAllRuntimeObservations();

        Assert.Equal(2, sym1Obs.Count);
        Assert.Single(sym2Obs);
        Assert.Equal(3, allObs.Count);
    }

    [Fact]
    public void GetAllRuntimeObservations_ReturnsAllSymbols()
    {
        var indexer = new SqliteIndexer(_dbPath, new Decompiler.IlspyDecompiler());

        indexer.StoreRuntimeObservation(new RuntimeObservation
        {
            Id = "obs_a", SymbolId = "sym_a", FullyQualifiedName = "A.Method",
            Assembly = "A", Timestamp = 1, DurationMs = 5,
        });

        indexer.StoreRuntimeObservation(new RuntimeObservation
        {
            Id = "obs_b", SymbolId = "sym_b", FullyQualifiedName = "B.Method",
            Assembly = "B", Timestamp = 2, DurationMs = 10,
        });

        var all = indexer.GetAllRuntimeObservations();
        Assert.Equal(2, all.Count);
    }

    [Fact]
    public void GetRuntimeObservations_EmptyDatabase_ReturnsEmpty()
    {
        var indexer = new SqliteIndexer(_dbPath, new Decompiler.IlspyDecompiler());
        var obs = indexer.GetRuntimeObservations("nonexistent");
        Assert.Empty(obs);
    }
}

