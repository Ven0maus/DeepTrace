namespace DeepTrace.Core.Runtime;

/// <summary>
/// A single observed method call during runtime execution.
/// </summary>
public sealed class RuntimeObservation
{
    /// <summary>Unique identifier for this observation.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Indexed symbol ID correlated with this observation.</summary>
    public string SymbolId { get; init; } = string.Empty;

    /// <summary>Fully qualified method name.</summary>
    public string FullyQualifiedName { get; init; } = string.Empty;

    /// <summary>Assembly name.</summary>
    public string Assembly { get; init; } = string.Empty;

    /// <summary>Unix millisecond timestamp when the call started.</summary>
    public long Timestamp { get; init; }

    /// <summary>Call depth in the invocation chain (0 = top-level).</summary>
    public int Depth { get; init; }

    /// <summary>JSON-serialized method parameters.</summary>
    public string? Parameters { get; init; }

    /// <summary>JSON-serialized return value or exception message.</summary>
    public string? ReturnValue { get; init; }

    /// <summary>Duration in milliseconds.</summary>
    public double DurationMs { get; init; }
}
