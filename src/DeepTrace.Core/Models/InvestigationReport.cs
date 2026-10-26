namespace DeepTrace.Core.Models;

/// <summary>
/// Evidence-based investigation report for AI-assisted game mod development.
/// </summary>
public sealed class InvestigationReport
{
    public string RequestInterpretation { get; init; } = string.Empty;

    public IReadOnlyList<EvidenceItem> DiscoveredExecutionPath { get; init; } = [];

    public IReadOnlyList<ObservedExecutionItem> ObservedExecutionPath { get; init; } = [];

    public IReadOnlyList<DependencyItem> RequiredDependenciesAndInvariants { get; init; } = [];

    public IReadOnlyList<ExtensionPointItem> CandidateExtensionPoints { get; init; } = [];

    public IReadOnlyList<ConfidenceItem> EvidenceAndConfidence { get; init; } = [];

    public string ImplementationPlan { get; init; } = string.Empty;

    public string ValidationPlan { get; init; } = string.Empty;
}

/// <summary>
/// A single piece of evidence from code inspection.
/// </summary>
public sealed class EvidenceItem
{
    public string SymbolId { get; init; } = string.Empty;
    public string FullyQualifiedName { get; init; } = string.Empty;
    public string Assembly { get; init; } = string.Empty;
    public string? SourceLocation { get; init; }
    public string Relevance { get; init; } = string.Empty;
    public string Excerpt { get; init; } = string.Empty;
    public bool IsVerified { get; init; }
}

/// <summary>
/// A required dependency or state invariant for the feature.
/// </summary>
public sealed class DependencyItem
{
    public string Name { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string Evidence { get; init; } = string.Empty;
    public bool IsResolved { get; init; }
}

/// <summary>
/// A candidate extension point for mod code.
/// </summary>
public sealed class ExtensionPointItem
{
    public string SymbolId { get; init; } = string.Empty;
    public string FullyQualifiedName { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
    public bool RunsOnce { get; init; }
    public string AvailableState { get; init; } = string.Empty;
    public string PotentialIssues { get; init; } = string.Empty;
}

/// <summary>
/// Confidence level for a finding.
/// </summary>
public enum ConfidenceLevel
{
    High,
    Medium,
    Low
}

/// <summary>
/// Source of a finding.
/// </summary>
public enum EvidenceSource
{
    StaticAnalysis,
    Runtime,
    Inference
}

/// <summary>
/// A confidence assessment for a finding.
/// </summary>
public sealed class ConfidenceItem
{
    public string Finding { get; init; } = string.Empty;
    public ConfidenceLevel Confidence { get; init; }
    public EvidenceSource Source { get; init; }
    public bool NeedsVerification { get; init; }
}

/// <summary>
/// An execution path item confirmed by runtime observation.
/// </summary>
public sealed class ObservedExecutionItem
{
    /// <summary>Indexed symbol ID.</summary>
    public string SymbolId { get; init; } = string.Empty;

    /// <summary>Fully qualified method name.</summary>
    public string FullyQualifiedName { get; init; } = string.Empty;

    /// <summary>Assembly name.</summary>
    public string Assembly { get; init; } = string.Empty;

    /// <summary>Number of times this method was observed during the session.</summary>
    public int CallCount { get; init; }

    /// <summary>Total duration across all calls in milliseconds.</summary>
    public double TotalDurationMs { get; init; }

    /// <summary>Average duration per call in milliseconds.</summary>
    public double AvgDurationMs { get; init; }

    /// <summary>Minimum call depth observed.</summary>
    public int MinDepth { get; init; }

    /// <summary>Maximum call depth observed.</summary>
    public int MaxDepth { get; init; }
}
