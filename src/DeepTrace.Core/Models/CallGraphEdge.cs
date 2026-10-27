namespace DeepTrace.Core.Models;

/// <summary>
/// A directed edge in the call graph with its type classification.
/// </summary>
public record CallGraphEdge(string CallerId, string CalleeId, string EdgeType);
