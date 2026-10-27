using System.Collections.Generic;
using System.Linq;
using DeepTrace.Core.Interfaces;
using DeepTrace.Core.Models;

namespace DeepTrace.Core.Ai;

/// <summary>
/// A predefined investigation recipe — a structured sequence of searches and analyses.
/// </summary>
public sealed class InvestigationRecipe
{
    /// <summary>Human-readable name of the recipe.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Description of what the recipe finds.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>Search queries to run in order.</summary>
    public IReadOnlyList<string> Queries { get; init; } = [];

    /// <summary>Optional assembly filter.</summary>
    public string? AssemblyFilter { get; init; }

    /// <summary>Optional namespace filter.</summary>
    public string? NamespaceFilter { get; init; }

    /// <summary>Optional kind filter (e.g., "Method", "Type").</summary>
    public string? KindFilter { get; init; }

    /// <summary>Max results per query.</summary>
    public int ResultLimit { get; init; } = 20;
}

/// <summary>
/// Predefined investigation recipes for common modding tasks.
/// </summary>
public static class InvestigationRecipes
{
    /// <summary>
    /// Find all virtual/override methods suitable for HarmonyX patches.
    /// </summary>
    public static IReadOnlyList<InvestigationRecipe> Recipes { get; } = new[]
    {
        new InvestigationRecipe
        {
            Name = "Find Extension Points",
            Description = "Discovers virtual methods, overrides, and public instance methods with meaningful bodies — ideal HarmonyX patch targets.",
            Queries = new[] { "virtual", "override", "public", "void", "bool", "int", "string" },
            KindFilter = "Method",
            ResultLimit = 50,
        },
        new InvestigationRecipe
        {
            Name = "Trace Initialization",
            Description = "Finds constructors, static initializers, and On* lifecycle methods to understand object creation order.",
            Queries = new[] { ".ctor", ".cctor", "On", "Initialize", "Start", "Awake", "Load" },
            KindFilter = "Method",
            ResultLimit = 30,
        },
        new InvestigationRecipe
        {
            Name = "Find Service Registrations",
            Description = "Discovers factory methods, Create/Build/From patterns, and dependency injection registrations.",
            Queries = new[] { "Create", "Build", "From", "Register", "Resolve", "Inject", "Service", "Manager" },
            KindFilter = "Method",
            ResultLimit = 30,
        },
        new InvestigationRecipe
        {
            Name = "Find Type Hierarchies",
            Description = "Discovers base types, interfaces, and derived type relationships for understanding inheritance patterns.",
            Queries = new[] { "MonoBehaviour", "ScriptableObject", "Component", "GameObject", "Behaviour" },
            KindFilter = "Type",
            ResultLimit = 50,
        },
        new InvestigationRecipe
        {
            Name = "Find Event Handlers",
            Description = "Discovers event subscription patterns, callbacks, and handler methods.",
            Queries = new[] { "Add", "Remove", "Subscribe", "Unsubscribe", "Handle", "Callback", "Event", "On" },
            KindFilter = "Method",
            ResultLimit = 30,
        },
    };

    /// <summary>
    /// Run a recipe against the indexer and return combined results.
    /// </summary>
    public static IReadOnlyList<SearchResult> Execute(InvestigationRecipe recipe, IIndexer indexer)
    {
        var results = new List<SearchResult>();
        var seen = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

        foreach (var query in recipe.Queries)
        {
            var searchResults = indexer.Search(query, limit: recipe.ResultLimit);
            foreach (var r in searchResults)
            {
                if (seen.Add(r.SymbolId))
                    results.Add(r);
            }
        }

        // Sort by relevance score (descending)
        results.Sort((a, b) => b.RelevanceScore.CompareTo(a.RelevanceScore));
        return results.AsReadOnly();
    }

    /// <summary>
    /// Execute a recipe and return a formatted text report suitable for LLM context.
    /// </summary>
    public static string ExecuteAsText(InvestigationRecipe recipe, IIndexer indexer)
    {
        var results = Execute(recipe, indexer);
        var lines = new List<string>
        {
            $"=== Recipe: {recipe.Name} ===",
            $"Description: {recipe.Description}",
            $"Results: {results.Count} unique symbols found",
            string.Empty,
        };

        foreach (var r in results.Take(50))
        {
            lines.Add($"  [{r.Kind}] {r.FullyQualifiedName} (score: {r.RelevanceScore:F2})");
            if (!string.IsNullOrEmpty(r.Signature))
                lines.Add($"    Sig: {r.Signature}");
            if (!string.IsNullOrEmpty(r.Excerpt))
                lines.Add($"    Excerpt: {r.Excerpt}");
            lines.Add(string.Empty);
        }

        return string.Join("\n", lines);
    }
}
