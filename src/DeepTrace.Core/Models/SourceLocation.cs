namespace DeepTrace.Core.Models;

/// <summary>
/// Represents a source location within a decompiled assembly file.
/// </summary>
/// <param name="FilePath">Path to the source file, or null if not available.</param>
/// <param name="StartLine">Starting line number (1-based).</param>
/// <param name="StartColumn">Starting column number (1-based).</param>
/// <param name="EndLine">Ending line number, or null for single-line locations.</param>
/// <param name="EndColumn">Ending column number, or null for single-line locations.</param>
public record SourceLocation(
    string? FilePath,
    int StartLine,
    int StartColumn,
    int? EndLine,
    int? EndColumn
);
