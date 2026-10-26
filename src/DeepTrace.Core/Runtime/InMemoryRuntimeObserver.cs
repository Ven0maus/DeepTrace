using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace DeepTrace.Core.Runtime;

/// <summary>
/// In-memory runtime observer for testing and local instrumentation.
/// </summary>
public sealed class InMemoryRuntimeObserver : IRuntimeObserver
{
    private readonly List<RuntimeObservation> _observations = new();
    private int _callCount;

    public int Count => _observations.Count;

    public void RecordCall(string symbolId, string fullyQualifiedName, string assembly,
        string? parameters, string? returnValue, double durationMs, int depth = 0)
    {
        var id = GenerateId(symbolId, fullyQualifiedName, ++_callCount);
        var observation = new RuntimeObservation
        {
            Id = id,
            SymbolId = symbolId,
            FullyQualifiedName = fullyQualifiedName,
            Assembly = assembly,
            Timestamp = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Depth = depth,
            Parameters = parameters,
            ReturnValue = returnValue,
            DurationMs = durationMs,
        };
        _observations.Add(observation);
    }

    public IReadOnlyList<RuntimeObservation> GetObservations(string symbolId)
    {
        return _observations
            .Where(o => o.SymbolId == symbolId)
            .ToList()
            .AsReadOnly();
    }

    public IReadOnlyList<RuntimeObservation> GetAllObservations()
    {
        return _observations.AsReadOnly();
    }

    public void Clear()
    {
        _observations.Clear();
        _callCount = 0;
    }

    private static string GenerateId(string symbolId, string fullyQualifiedName, int callNumber)
    {
        var input = $"{symbolId}|{fullyQualifiedName}|{callNumber}|{System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
        return $"obs_{System.Convert.ToHexString(hash).ToLowerInvariant().Substring(0, 16)}";
    }
}
