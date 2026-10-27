namespace DeepTrace.Core.Models;

/// <summary>
/// A single search result from BM25-ranked symbol search.
/// </summary>
public sealed class SearchResult
{
    /// <summary>Unique symbol identifier.</summary>
    public string SymbolId { get; init; } = string.Empty;

    /// <summary>Simple name of the symbol.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Kind of symbol (Method, Type, Field, Property, Assembly).</summary>
    public SymbolKind Kind { get; init; }

    /// <summary>Fully qualified name (e.g., "TestLib.Greeter.Greet").</summary>
    public string FullyQualifiedName { get; init; } = string.Empty;

    /// <summary>Signature including parameter and return types.</summary>
    public string? Signature { get; init; }

    /// <summary>Assembly name where this symbol is defined.</summary>
    public string Assembly { get; init; } = string.Empty;

    /// <summary>Source location in the original assembly, if available.</summary>
    public SourceLocation? SourceLocation { get; init; }

    /// <summary>Relevant excerpt from the method body or type source.</summary>
    public string? Excerpt { get; init; }

    /// <summary>BM25 relevance score (higher = more relevant).</summary>
    public double RelevanceScore { get; set; }

    /// <summary>Human-readable explanation of why this result matched.</summary>
    public string? RelevanceExplanation { get; set; }
}
