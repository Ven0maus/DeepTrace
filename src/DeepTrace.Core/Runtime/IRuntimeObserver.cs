using System.Collections.Generic;

namespace DeepTrace.Core.Runtime;

/// <summary>
/// Observes method calls at runtime and correlates them with indexed symbols.
/// </summary>
public interface IRuntimeObserver
{
    /// <summary>
    /// Record an observed method call.
    /// </summary>
    /// <param name="symbolId">Indexed symbol ID (e.g., "Assembly::Namespace.Type::Method::Signature").</param>
    /// <param name="fullyQualifiedName">Fully qualified method name.</param>
    /// <param name="assembly">Assembly name.</param>
    /// <param name="parameters">JSON-serialized parameters.</param>
    /// <param name="returnValue">JSON-serialized return value or exception.</param>
    /// <param name="durationMs">Call duration in milliseconds.</param>
    /// <param name="depth">Call depth in the invocation chain.</param>
    void RecordCall(string symbolId, string fullyQualifiedName, string assembly,
        string? parameters, string? returnValue, double durationMs, int depth = 0);

    /// <summary>
    /// Get all observations for a given symbol ID.
    /// </summary>
    IReadOnlyList<RuntimeObservation> GetObservations(string symbolId);

    /// <summary>
    /// Get all observations across all symbols.
    /// </summary>
    IReadOnlyList<RuntimeObservation> GetAllObservations();

    /// <summary>
    /// Clear all stored observations.
    /// </summary>
    void Clear();

    /// <summary>
    /// Count of stored observations.
    /// </summary>
    int Count { get; }
}
