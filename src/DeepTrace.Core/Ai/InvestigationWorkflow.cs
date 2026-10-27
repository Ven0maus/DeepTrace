using System;
using System.Collections.Generic;
using System.Linq;
using DeepTrace.Core.Models;
using DeepTrace.Core.Runtime;

namespace DeepTrace.Core.Ai;

/// <summary>
/// Configuration for an AI investigation run.
/// Controls search depth, result limits, and token budget for the workflow.
/// </summary>
public sealed class InvestigationOptions
{
    /// <summary>Maximum number of SearchCode calls.</summary>
    public int MaxSearches { get; init; } = 8;

    /// <summary>Maximum traversal depth for callers/callees.</summary>
    public int MaxDepth { get; init; } = 2;

    /// <summary>Result limit per search call.</summary>
    public int ResultLimit { get; init; } = 20;

    /// <summary>Approximate character budget for the report body (~300 chars per token unit).</summary>
    public int TokenBudget { get; init; } = 12000;
}

/// <summary>
/// Contract for investigation tool methods used by the workflow.
/// </summary>
public interface IInvestigationTools
{
    string SearchCode(string query, string? assemblyFilter = null, string? namespaceFilter = null, string? kindFilter = null, int limit = 20);
    string GetMethod(string symbolId);
    string GetCallers(string methodSymbolId, int depth = 1, int limit = 50);
    string GetCallees(string methodSymbolId, int depth = 1, int limit = 50);
    string SearchReferences(string symbolId, int limit = 30);
    string GetRelatedMethods(string methodSymbolId, int limit = 10);
    string GetRuntimeObservations(string symbolId);
}

/// <summary>
/// Iterative investigation workflow that explores code via structured tool calls.
/// </summary>
public sealed class InvestigationWorkflow
{
    private static readonly HashSet<string> StopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "the", "a", "an", "is", "to", "for", "with", "how", "does", "where",
        "what", "create", "build", "implement", "add", "and", "or", "not",
        "in", "on", "at", "by", "of", "it", "that", "this", "from", "use",
        "using", "make", "when", "then", "can", "will", "should", "would",
        "which", "their", "there", "are", "been", "have", "had", "was", "get",
        "set", "return", "if", "else", "new", "into", "as", "be", "do", "so"
    };

    private readonly IInvestigationTools _tools;
    private readonly IRuntimeObserver? _runtimeObserver;
    private int _searchCount;

    public InvestigationWorkflow(IInvestigationTools tools, IRuntimeObserver? runtimeObserver = null)
    {
        _tools = tools ?? throw new ArgumentNullException(nameof(tools));
        _runtimeObserver = runtimeObserver;
        _searchCount = 0;
    }

    /// <summary>
    /// Run the full iterative investigation workflow.
    /// </summary>
    public InvestigationReport Investigate(string request, InvestigationOptions? options = null)
    {
        options ??= new InvestigationOptions();

        var concepts = ExtractConcepts(request);
        var report = BuildReport(request, concepts);

        var rawEvidenceItems = new List<EvidenceEntry>();
        var dependencies = new List<DependencyItem>();
        var extensionPoints = new List<ExtensionPointItem>();
        var confidenceItems = new List<ConfidenceItem>();

        // Phase 1: Search for each concept (bounded by MaxSearches)
        var candidateSymbolIds = new HashSet<string>();
        var searchesRemaining = options.MaxSearches;

        foreach (var concept in concepts)
        {
            if (searchesRemaining <= 0)
                break;

            var searchResults = _tools.SearchCode(concept, limit: options.ResultLimit);
            searchesRemaining--;
            _searchCount++;

            var parsed = ParseSearchResults(searchResults);
            foreach (var entry in parsed)
            {
                if (candidateSymbolIds.Add(entry.SymbolId))
                {
                    rawEvidenceItems.Add(new EvidenceEntry
                    {
                        SymbolId = entry.SymbolId,
                        FullyQualifiedName = entry.FullyQualifiedName,
                        Assembly = entry.Assembly,
                        SourceLocation = entry.SourceLocation?.ToString() ?? string.Empty,
                        Relevance = entry.Relevance,
                        Excerpt = entry.Excerpt,
                        IsVerified = false
                    });
                }
            }
        }

        // Phase 2: Get full method bodies for top candidates
        var exploredMethods = new Dictionary<string, MethodInfo>();
        var verifiedSymbolIds = new HashSet<string>();
        var maxBodyFetches = Math.Min(candidateSymbolIds.Count, 30);
        var symbolIds = candidateSymbolIds.ToList();

        for (int i = 0; i < Math.Min(maxBodyFetches, symbolIds.Count); i++)
        {
            var symbolId = symbolIds[i];
            var methodBody = _tools.GetMethod(symbolId);
            var methodInfo = ParseGetMethodResult(methodBody);
            if (methodInfo != null)
            {
                exploredMethods[symbolId] = methodInfo;
                verifiedSymbolIds.Add(symbolId);
            }
        }

        // Phase 2.5: Check runtime observations for discovered methods
        var observedItems = new List<ObservedExecutionItem>();
        var observedSymbolIds = new HashSet<string>();
        if (_runtimeObserver != null)
        {
            foreach (var symbolId in verifiedSymbolIds)
            {
                var observations = _runtimeObserver.GetObservations(symbolId);
                if (observations.Count > 0)
                {
                    observedSymbolIds.Add(symbolId);
                    var totalDuration = observations.Sum(o => o.DurationMs);
                    observedItems.Add(new ObservedExecutionItem
                    {
                        SymbolId = symbolId,
                        FullyQualifiedName = observations.First().FullyQualifiedName,
                        Assembly = observations.First().Assembly,
                        CallCount = observations.Count,
                        TotalDurationMs = totalDuration,
                        AvgDurationMs = totalDuration / observations.Count,
                        MinDepth = observations.Min(o => o.Depth),
                        MaxDepth = observations.Max(o => o.Depth),
                    });
                }
            }
        }

        // Phase 3: Explore callers and callees (bounded by MaxDepth)
        var visitedForTraversal = new HashSet<string>(exploredMethods.Keys);
        var currentDepth = 0;

        while (currentDepth < options.MaxDepth)
        {
            var nextIds = new HashSet<string>();

            foreach (var (symbolId, method) in exploredMethods)
            {
                // Get callees
                var calleesResult = _tools.GetCallees(symbolId, depth: 1, limit: 20);
                var calleeEntries = ParseSymbolIdsFromResult(calleesResult);
                foreach (var calleeId in calleeEntries)
                {
                    if (visitedForTraversal.Add(calleeId))
                        nextIds.Add(calleeId);
                }

                // Get callers
                var callersResult = _tools.GetCallers(symbolId, depth: 1, limit: 20);
                var callerEntries = ParseSymbolIdsFromResult(callersResult);
                foreach (var callerId in callerEntries)
                {
                    if (visitedForTraversal.Add(callerId))
                        nextIds.Add(callerId);
                }
            }

            // Fetch bodies for newly discovered methods
            foreach (var newId in nextIds)
            {
                var newMethodBody = _tools.GetMethod(newId);
                var newMethodInfo = ParseGetMethodResult(newMethodBody);
                if (newMethodInfo != null)
                {
                    exploredMethods[newId] = newMethodInfo;
                }
            }

            currentDepth++;
        }

        // Phase 4: Extract dependencies and extension points from explored methods
        foreach (var (symbolId, method) in exploredMethods)
        {
            ExtractDependencies(method, dependencies);
            ExtractExtensionPoints(method, symbolId, extensionPoints);
        }

        // Phase 5: Build confidence assessments
        confidenceItems.Add(new ConfidenceItem
        {
            Finding = "Request concepts mapped to search queries",
            Confidence = ConfidenceLevel.High,
            Source = EvidenceSource.StaticAnalysis,
            NeedsVerification = false
        });

        confidenceItems.Add(new ConfidenceItem
        {
            Finding = $"Execution path built from {exploredMethods.Count} methods",
            Confidence = exploredMethods.Count > 5 ? ConfidenceLevel.High : exploredMethods.Count > 2 ? ConfidenceLevel.Medium : ConfidenceLevel.Low,
            Source = EvidenceSource.StaticAnalysis,
            NeedsVerification = exploredMethods.Count <= 2
        });

        confidenceItems.Add(new ConfidenceItem
        {
            Finding = "Dependency extraction from method bodies",
            Confidence = ConfidenceLevel.Medium,
            Source = EvidenceSource.StaticAnalysis,
            NeedsVerification = true
        });

        confidenceItems.Add(new ConfidenceItem
        {
            Finding = "Extension point candidates (virtual/override/public)",
            Confidence = ConfidenceLevel.High,
            Source = EvidenceSource.StaticAnalysis,
            NeedsVerification = false
        });

        confidenceItems.Add(new ConfidenceItem
        {
            Finding = "Caller/callee traversal",
            Confidence = ConfidenceLevel.Medium,
            Source = EvidenceSource.StaticAnalysis,
            NeedsVerification = true
        });

        confidenceItems.Add(new ConfidenceItem
        {
            Finding = "Implementation plan synthesis",
            Confidence = ConfidenceLevel.Low,
            Source = EvidenceSource.Inference,
            NeedsVerification = true
        });

        // Runtime evidence confidence
        if (observedItems.Count > 0)
        {
            confidenceItems.Add(new ConfidenceItem
            {
                Finding = $"Execution confirmed by {observedItems.Count} runtime observation(s)",
                Confidence = observedItems.Count >= 3 ? ConfidenceLevel.High : ConfidenceLevel.Medium,
                Source = EvidenceSource.Runtime,
                NeedsVerification = false
            });

            // Boost confidence for verified methods that have runtime evidence
            foreach (var item in observedItems)
            {
                confidenceItems.Add(new ConfidenceItem
                {
                    Finding = $"{item.FullyQualifiedName} observed {item.CallCount} time(s)",
                    Confidence = ConfidenceLevel.High,
                    Source = EvidenceSource.Runtime,
                    NeedsVerification = false
                });
            }
        }

        // Phase 6: Build the report
        var finalEvidence = rawEvidenceItems.Select(e => new EvidenceItem
        {
            SymbolId = e.SymbolId,
            FullyQualifiedName = e.FullyQualifiedName,
            Assembly = e.Assembly,
            SourceLocation = e.SourceLocation,
            Relevance = e.Relevance,
            Excerpt = e.Excerpt,
            IsVerified = verifiedSymbolIds.Contains(e.SymbolId)
        }).ToList();

        return new InvestigationReport
        {
            RequestInterpretation = report.RequestInterpretation,
            DiscoveredExecutionPath = finalEvidence,
            ObservedExecutionPath = observedItems.AsReadOnly(),
            RequiredDependenciesAndInvariants = dependencies,
            CandidateExtensionPoints = extensionPoints,
            EvidenceAndConfidence = confidenceItems,
            ImplementationPlan = SynthesizeImplementationPlan(request, concepts, exploredMethods, observedItems),
            ValidationPlan = SynthesizeValidationPlan(exploredMethods, dependencies)
        };
    }

    /// <summary>
    /// Pre-build report with request interpretation before investigation.
    /// </summary>
    private static InvestigationReport BuildReport(string request, IReadOnlyList<string> concepts)
    {
        return new InvestigationReport
        {
            RequestInterpretation = BuildRequestInterpretation(request, concepts)
        };
    }

    /// <summary>
    /// Split request into meaningful concept tokens.
    /// </summary>
    private static IReadOnlyList<string> ExtractConcepts(string request)
    {
        if (string.IsNullOrWhiteSpace(request))
            return Array.Empty<string>();

        // Split on common delimiters: spaces, hyphens, underscores, dots
        var tokens = request.ToLowerInvariant()
            .Split(new[] { ' ', '-', '_', '.' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length >= 3)
            .Where(t => !StopWords.Contains(t))
            .Distinct()
            .ToList();

        return tokens.AsReadOnly();
    }

    /// <summary>
    /// Join concepts back into a human-readable interpretation.
    /// </summary>
    private static string BuildRequestInterpretation(string request, IReadOnlyList<string> concepts)
    {
        if (concepts.Count == 0)
            return $"No meaningful concepts extracted from: {request}";

        var conceptList = string.Join(", ", concepts);
        return $"Interpretation of \"{request}\": searching for concepts [{conceptList}]. " +
               $"Mapped to code symbols via indexed search and method inspection.";
    }

    /// <summary>
    /// Parse SearchCode output into structured entries.
    /// </summary>
    private static IReadOnlyList<SearchEntry> ParseSearchResults(string result)
    {
        var entries = new List<SearchEntry>();
        if (string.IsNullOrWhiteSpace(result))
            return entries;

        var lines = result.Split('\n');
        var current = new SearchEntry();

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            if (trimmed.StartsWith("[", StringComparison.Ordinal))
            {
                // New result entry: "[Kind] Name"
                if (current.SymbolId != string.Empty)
                    entries.Add(current);

                current = new SearchEntry();

                var bracketEnd = trimmed.IndexOf(']');
                if (bracketEnd > 0)
                {
                    current.Kind = trimmed.Substring(1, bracketEnd - 1);
                    current.Name = trimmed.Substring(bracketEnd + 1).Trim();
                }
            }
            else if (trimmed.StartsWith("SymbolId: ", StringComparison.Ordinal))
            {
                current.SymbolId = trimmed.Substring("SymbolId: ".Length);
            }
            else if (trimmed.StartsWith("Assembly: ", StringComparison.Ordinal))
            {
                current.Assembly = trimmed.Substring("Assembly: ".Length);
            }
            else if (trimmed.StartsWith("Namespace: ", StringComparison.Ordinal))
            {
                current.FullyQualifiedName = trimmed.Substring("Namespace: ".Length);
            }
            else if (trimmed.StartsWith("Signature: ", StringComparison.Ordinal))
            {
                current.Signature = trimmed.Substring("Signature: ".Length);
            }
            else if (trimmed.StartsWith("Excerpt: ", StringComparison.Ordinal))
            {
                current.Excerpt = trimmed.Substring("Excerpt: ".Length);
            }
            else if (trimmed.StartsWith("Score: ", StringComparison.Ordinal))
            {
                current.Relevance = trimmed.Substring("Score: ".Length);
            }
        }

        if (current.SymbolId != string.Empty)
            entries.Add(current);

        return entries;
    }

    /// <summary>
    /// Parse GetMethod output into a MethodInfo.
    /// </summary>
    private static MethodInfo? ParseGetMethodResult(string result)
    {
        if (string.IsNullOrWhiteSpace(result))
            return null;

        var lines = result.Split('\n');
        var method = new MethodInfo();
        var bodyBuilder = new List<string>();
        var inBody = false;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            if (trimmed.StartsWith("FullName: ", StringComparison.Ordinal))
                method.FullName = trimmed.Substring("FullName: ".Length);
            else if (trimmed.StartsWith("DeclaringType: ", StringComparison.Ordinal))
                method.DeclaringTypeFullName = trimmed.Substring("DeclaringType: ".Length);
            else if (trimmed.StartsWith("Name: ", StringComparison.Ordinal))
                method.Name = trimmed.Substring("Name: ".Length);
            else if (trimmed.StartsWith("Signature: ", StringComparison.Ordinal))
                method.Signature = trimmed.Substring("Signature: ".Length);
            else if (trimmed.StartsWith("ReturnType: ", StringComparison.Ordinal))
            {
                var val = trimmed.Substring("ReturnType: ".Length);
                method.ReturnType = val == "(none)" ? null : val;
            }
            else if (trimmed.StartsWith("IsStatic: ", StringComparison.Ordinal))
                method.IsStatic = trimmed.Substring("IsStatic: ".Length).Trim().Equals("True", StringComparison.Ordinal);
            else if (trimmed.StartsWith("IsVirtual: ", StringComparison.Ordinal))
                method.IsVirtual = trimmed.Substring("IsVirtual: ".Length).Trim().Equals("True", StringComparison.Ordinal);
            else if (trimmed.StartsWith("IsAbstract: ", StringComparison.Ordinal))
                method.IsAbstract = trimmed.Substring("IsAbstract: ".Length).Trim().Equals("True", StringComparison.Ordinal);
            else if (trimmed.StartsWith("IsOverride: ", StringComparison.Ordinal))
                method.IsOverride = trimmed.Substring("IsOverride: ".Length).Trim().Equals("True", StringComparison.Ordinal);
            else if (trimmed.StartsWith("SymbolId: ", StringComparison.Ordinal))
                method.SymbolId = trimmed.Substring("SymbolId: ".Length);
            else if (trimmed.StartsWith("SourceLocation: ", StringComparison.Ordinal))
            {
                var locStr = trimmed.Substring("SourceLocation: ".Length);
                var parts = locStr.Split(':');
                if (parts.Length >= 3)
                {
                    method.SourceLocation = new SourceLocation(
                        parts[0],
                        int.TryParse(parts[1], out var lineNum) ? lineNum : 0,
                        int.TryParse(parts[2], out var colNum) ? colNum : 0,
                        parts.Length > 3 ? int.TryParse(parts[3], out var endLine) ? endLine : (int?)null : null,
                        parts.Length > 4 ? int.TryParse(parts[4], out var endCol) ? endCol : (int?)null : null);
                }
            }
            else if (trimmed.StartsWith("Summary: ", StringComparison.Ordinal))
                method.Summary = trimmed.Substring("Summary: ".Length);
            else if (trimmed.StartsWith("--- Body ---", StringComparison.Ordinal))
            {
                inBody = true;
            }
            else if (inBody)
            {
                bodyBuilder.Add(trimmed);
            }
        }

        if (bodyBuilder.Count > 0)
        {
            var bodyText = string.Join("\n", bodyBuilder);
            if (bodyText.EndsWith("..."))
                bodyText = bodyText.Substring(0, bodyText.Length - 3);
            method.Body = bodyText;
        }

        if (string.IsNullOrEmpty(method.SymbolId))
            return null;

        return method;
    }

    /// <summary>
    /// Extract symbol IDs from caller/callee results.
    /// </summary>
    private static IReadOnlyList<string> ParseSymbolIdsFromResult(string result)
    {
        var ids = new HashSet<string>();
        if (string.IsNullOrWhiteSpace(result))
            return Array.Empty<string>();

        var lines = result.Split('\n');
        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            // Direct caller/callee: "  FullName" (2-space indent, not deeper)
            if (line.StartsWith("  ") && !line.StartsWith("    ") && !trimmed.StartsWith("Level "))
            {
                var name = trimmed;
                if (!string.IsNullOrEmpty(name) && name.Contains('.'))
                {
                    ids.Add(name);
                }
            }
            // Deeper: "    Level N: FullName"
            else if (trimmed.StartsWith("Level ", StringComparison.Ordinal))
            {
                var colonIdx = trimmed.IndexOf(':');
                if (colonIdx > 0)
                {
                    var name = trimmed.Substring(colonIdx + 1).Trim();
                    if (!string.IsNullOrEmpty(name) && name.Contains('.'))
                    {
                        ids.Add(name);
                    }
                }
            }
        }

        return ids.ToList();
    }

    /// <summary>
    /// Extract dependencies and invariants from method bodies.
    /// </summary>
    private static void ExtractDependencies(MethodInfo method, List<DependencyItem> dependencies)
    {
        if (string.IsNullOrWhiteSpace(method.Body))
            return;

        var body = method.Body;

        // Detect constructor patterns: field assignments, base calls, this calls
        if (method.Name == ".ctor" || method.Name == ".cctor")
        {
            // Look for field-like patterns: _fieldName = or this.fieldName =
            var fieldAssignments = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var bodyLines = body.Split('\n');

            foreach (var bodyLine in bodyLines)
            {
                var trimmed = bodyLine.Trim();

                // Field assignments: _field = or this.field =
                if (trimmed.Contains(" = ") || trimmed.Contains(" =new ") || trimmed.Contains(" = new "))
                {
                    var left = trimmed.Substring(0, trimmed.IndexOf(" = ", StringComparison.Ordinal));
                    var fieldName = left.Split(new[] { '.', '_', ' ' }, StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
                    if (!string.IsNullOrEmpty(fieldName) && fieldName.Length > 1)
                    {
                        fieldAssignments.Add(fieldName);
                    }
                }

                // Base class constructor call
                if (trimmed.StartsWith("base(", StringComparison.Ordinal) || trimmed.Contains(" : this("))
                {
                    dependencies.Add(new DependencyItem
                    {
                        Name = $"{method.DeclaringTypeFullName} requires base type initialization",
                        Category = "Constructor Chain",
                        Evidence = $"Constructor {method.Name} in {method.DeclaringTypeFullName}",
                        IsResolved = false
                    });
                }
            }

            foreach (var field in fieldAssignments)
            {
                dependencies.Add(new DependencyItem
                {
                    Name = field,
                    Category = "Instance State",
                    Evidence = $"Assigned in {method.DeclaringTypeFullName}.{method.Name}",
                    IsResolved = false
                });
            }
        }

        // Detect factory/creation patterns: new Type(), service resolution
        var newPatterns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var bodyLines2 = body.Split('\n');
        foreach (var bodyLine in bodyLines2)
        {
            var trimmed = bodyLine.Trim();
            if (trimmed.Contains("new "))
            {
                var parts = trimmed.Split(new[] { "new " }, StringSplitOptions.None);
                foreach (var part in parts.Skip(1))
                {
                    var typeName = part.Split(new[] { ' ', '(', '<', '[', ';' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                    if (!string.IsNullOrEmpty(typeName) && char.IsUpper(typeName[0]))
                    {
                        newPatterns.Add(typeName);
                    }
                }
            }
        }

        foreach (var type in newPatterns)
        {
            dependencies.Add(new DependencyItem
            {
                Name = type,
                Category = "Runtime Dependency",
                Evidence = $"Instantiated in {method.FullName}",
                IsResolved = false
            });
        }
    }

    /// <summary>
    /// Identify candidate extension points from method metadata.
    /// </summary>
    private static void ExtractExtensionPoints(MethodInfo method, string symbolId, List<ExtensionPointItem> extensionPoints)
    {
        // Virtual methods: override candidates
        if (method.IsVirtual && !method.IsStatic)
        {
            extensionPoints.Add(new ExtensionPointItem
            {
                SymbolId = symbolId,
                FullyQualifiedName = method.FullName,
                Reason = "Virtual method — can be overridden or patched with HarmonyX",
                RunsOnce = method.IsStatic,
                AvailableState = "Available",
                PotentialIssues = "Override must match base signature. Consider calling base implementation."
            });
        }

        // Override methods: extension candidates (already overridable)
        if (method.IsOverride)
        {
            extensionPoints.Add(new ExtensionPointItem
            {
                SymbolId = symbolId,
                FullyQualifiedName = method.FullName,
                Reason = "Override method — target for HarmonyX prefix/postfix patches",
                RunsOnce = method.IsStatic,
                AvailableState = "Available",
                PotentialIssues = "Patches must handle base call. Check for existing patches."
            });
        }

        // Public non-virtual, non-abstract methods: patch candidates
        if (!method.IsVirtual && !method.IsAbstract && !method.IsStatic &&
            !method.Name.StartsWith("<", StringComparison.Ordinal) &&
            !method.Name.StartsWith("get_", StringComparison.Ordinal) &&
            !method.Name.StartsWith("set_", StringComparison.Ordinal) &&
            !method.Name.StartsWith("add_", StringComparison.Ordinal) &&
            !method.Name.StartsWith("remove_", StringComparison.Ordinal))
        {
            // Heuristic: methods with non-trivial bodies are good patch targets
            if (!string.IsNullOrEmpty(method.Body) && method.Body.Length > 50)
            {
                extensionPoints.Add(new ExtensionPointItem
                {
                    SymbolId = symbolId,
                    FullyQualifiedName = method.FullName,
                    Reason = "Public instance method with meaningful body — good HarmonyX patch target",
                    RunsOnce = false,
                    AvailableState = "Available",
                    PotentialIssues = "Not virtual. Requires HarmonyX patch. May have side effects."
                });
            }
        }
    }

    /// <summary>
    /// Synthesize an implementation plan from discovered methods.
    /// </summary>
    private static string SynthesizeImplementationPlan(string request, IReadOnlyList<string> concepts, IDictionary<string, MethodInfo> exploredMethods, IReadOnlyList<ObservedExecutionItem> observedItems)
    {
        if (exploredMethods.Count == 0)
        {
            return $"No methods discovered for concepts [{string.Join(", ", concepts)}]. " +
                   $"Increase MaxSearches or refine concept extraction. " +
                   $"Recommend: provide assembly/namespace filters to narrow scope.";
        }

        var lines = new List<string>();
        lines.Add($"Implementation plan for: {request}");
        lines.Add(string.Empty);
        lines.Add($"Explored {exploredMethods.Count} methods across the following types:");

        var types = exploredMethods.Values.Select(m => m.DeclaringTypeFullName).Distinct().ToList();
        foreach (var type in types)
        {
            var typeMethods = exploredMethods.Values.Where(m => m.DeclaringTypeFullName == type).ToList();
            lines.Add($"  - {type} ({typeMethods.Count} methods)");
        }

        if (observedItems.Count > 0)
        {
            lines.Add(string.Empty);
            lines.Add($"Runtime evidence: {observedItems.Count} method(s) observed during execution:");
            foreach (var obs in observedItems.Take(10))
            {
                lines.Add($"  - {obs.FullyQualifiedName} (called {obs.CallCount}x, avg {obs.AvgDurationMs:F1}ms)");
            }
        }

        lines.Add(string.Empty);
        lines.Add("Recommended approach:");
        lines.Add("1. Identify the most relevant method from the discovered execution path.");
        lines.Add("2. Determine if it is virtual/override (prefer override) or patchable via HarmonyX.");
        lines.Add("3. Check RequiredDependenciesAndInvariants for missing registrations or initializations.");
        lines.Add("4. Implement the smallest compatible change — prefer extension over modification.");
        lines.Add("5. Validate against ValidationPlan test cases before shipping.");

        return string.Join("\n", lines);
    }

    /// <summary>
    /// Synthesize a validation plan from discovered dependencies.
    /// </summary>
    private static string SynthesizeValidationPlan(IDictionary<string, MethodInfo> exploredMethods, IReadOnlyList<DependencyItem> dependencies)
    {
        var lines = new List<string>();
        lines.Add("Validation plan:");
        lines.Add(string.Empty);

        // Suggest tests based on method types
        var virtualCount = exploredMethods.Values.Count(m => m.IsVirtual);
        var abstractCount = exploredMethods.Values.Count(m => m.IsAbstract);
        var constructorCount = exploredMethods.Values.Count(m => m.Name == ".ctor");

        if (virtualCount > 0)
        {
            lines.Add($"- Verify override compatibility for {virtualCount} virtual method(s)");
            lines.Add("  - Call base implementation and assert expected behavior");
            lines.Add("  - Test with null/edge-case inputs");
        }

        if (abstractCount > 0)
        {
            lines.Add($"- Confirm all {abstractCount} abstract method(s) have concrete implementations");
        }

        if (constructorCount > 0)
        {
            lines.Add($"- Test {constructorCount} constructor(s) with valid and invalid arguments");
            lines.Add("  - Verify field initialization");
            lines.Add("  - Check for side effects (static field mutations, registrations)");
        }

        if (dependencies.Count > 0)
        {
            var unresolved = dependencies.Count(d => !d.IsResolved);
            if (unresolved > 0)
            {
                lines.Add(string.Empty);
                lines.Add($"- Resolve {unresolved} unresolved dependency/ invariant before implementing:");
                foreach (var dep in dependencies.Where(d => !d.IsResolved).Take(10))
                {
                    lines.Add($"  - {dep.Name} ({dep.Category})");
                }
            }
        }

        lines.Add(string.Empty);
        lines.Add("- Run existing unit tests to confirm no regressions");
        lines.Add("- If using HarmonyX patches: verify patch order and priority");

        return string.Join("\n", lines);
    }

    /// <summary>
    /// Intermediate structure for parsed search results.
    /// </summary>
    private sealed class SearchEntry
    {
        public string SymbolId { get; set; } = string.Empty;
        public string Kind { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Assembly { get; set; } = string.Empty;
        public string FullyQualifiedName { get; set; } = string.Empty;
        public string Signature { get; set; } = string.Empty;
        public string Excerpt { get; set; } = string.Empty;
        public string Relevance { get; set; } = string.Empty;
        public SourceLocation? SourceLocation { get; set; }
    }

    /// <summary>
    /// Intermediate mutable evidence entry before final report assembly.
    /// </summary>
    private sealed class EvidenceEntry
    {
        public string SymbolId { get; set; } = string.Empty;
        public string FullyQualifiedName { get; set; } = string.Empty;
        public string Assembly { get; set; } = string.Empty;
        public string SourceLocation { get; set; } = string.Empty;
        public string Relevance { get; set; } = string.Empty;
        public string Excerpt { get; set; } = string.Empty;
        public bool IsVerified { get; set; }
    }
}
