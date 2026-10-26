using DeepTrace.Core.Runtime;
using Xunit;

namespace DeepTrace.Tests;

public class InMemoryRuntimeObserverTests
{
    [Fact]
    public void RecordCall_StoresObservation()
    {
        var observer = new InMemoryRuntimeObserver();
        observer.RecordCall("symbol_1", "MyClass.MyMethod", "MyAssembly", "{\"x\":1}", "\"result\"", 42.5);
        Assert.Equal(1, observer.Count);
    }

    [Fact]
    public void RecordCall_GeneratesUniqueIds()
    {
        var observer = new InMemoryRuntimeObserver();
        observer.RecordCall("symbol_1", "MyClass.Method", "Assembly", null, null, 10);
        observer.RecordCall("symbol_1", "MyClass.Method", "Assembly", null, null, 20);
        observer.RecordCall("symbol_1", "MyClass.Method", "Assembly", null, null, 30);
        Assert.Equal(3, observer.Count);
    }

    [Fact]
    public void GetObservations_ReturnsFilteredBySymbolId()
    {
        var observer = new InMemoryRuntimeObserver();
        observer.RecordCall("symbol_a", "ClassA.Method", "A", null, null, 10);
        observer.RecordCall("symbol_b", "ClassB.Method", "B", null, null, 20);
        observer.RecordCall("symbol_a", "ClassA.Method", "A", null, null, 30);

        var aObs = observer.GetObservations("symbol_a");
        var bObs = observer.GetObservations("symbol_b");

        Assert.Equal(2, aObs.Count);
        Assert.Single(bObs);
    }

    [Fact]
    public void GetObservations_EmptySymbolId_ReturnsEmpty()
    {
        var observer = new InMemoryRuntimeObserver();
        observer.RecordCall("symbol_1", "Class.Method", "A", null, null, 10);
        var empty = observer.GetObservations("nonexistent");
        Assert.Empty(empty);
    }

    [Fact]
    public void GetAllObservations_ReturnsAll()
    {
        var observer = new InMemoryRuntimeObserver();
        observer.RecordCall("symbol_1", "A.Method", "A", null, null, 10);
        observer.RecordCall("symbol_2", "B.Method", "B", null, null, 20);
        var all = observer.GetAllObservations();
        Assert.Equal(2, all.Count);
    }

    [Fact]
    public void Clear_RemovesAllObservations()
    {
        var observer = new InMemoryRuntimeObserver();
        observer.RecordCall("symbol_1", "A.Method", "A", null, null, 10);
        observer.RecordCall("symbol_2", "B.Method", "B", null, null, 20);
        observer.Clear();
        Assert.Equal(0, observer.Count);
        Assert.Empty(observer.GetAllObservations());
    }

    [Fact]
    public void Observation_PreservesAllFields()
    {
        var observer = new InMemoryRuntimeObserver();
        var parameters = "{\"key\":\"value\"}";
        var returnValue = "{\"status\":\"ok\"}";
        observer.RecordCall("sym_1", "NS.Class.Method", "TestAssembly", parameters, returnValue, 99.5, depth: 3);

        var obs = observer.GetAllObservations().First();
        Assert.Equal("sym_1", obs.SymbolId);
        Assert.Equal("NS.Class.Method", obs.FullyQualifiedName);
        Assert.Equal("TestAssembly", obs.Assembly);
        Assert.Equal(parameters, obs.Parameters);
        Assert.Equal(returnValue, obs.ReturnValue);
        Assert.Equal(99.5, obs.DurationMs);
        Assert.Equal(3, obs.Depth);
    }
}
