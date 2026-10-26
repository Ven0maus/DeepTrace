namespace DeepTrace.Core.Models;

public record SourceLocation(
    string? FilePath,
    int StartLine,
    int StartColumn,
    int? EndLine,
    int? EndColumn
);
