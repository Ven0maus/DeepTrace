namespace DeepTrace.Core.Models;

public sealed class SearchResult
{
    public string SymbolId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public SymbolKind Kind { get; init; }
    public string FullyQualifiedName { get; init; } = string.Empty;
    public string? Signature { get; init; }
    public string Assembly { get; init; } = string.Empty;
    public SourceLocation? SourceLocation { get; init; }
    public string? Excerpt { get; init; }
    public double RelevanceScore { get; set; }
    public string? RelevanceExplanation { get; set; }
}
