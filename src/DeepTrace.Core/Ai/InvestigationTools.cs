using System.Collections.Generic;
using System.Linq;
using DeepTrace.Core.Interfaces;
using DeepTrace.Core.Models;

namespace DeepTrace.Core.Ai;

/// <summary>
/// Exposes indexed code data as structured text for LLM consumption.
/// </summary>
public sealed class InvestigationTools : IInvestigationTools
{
    private readonly IIndexer _indexer;

    public InvestigationTools(IIndexer indexer)
    {
        _indexer = indexer;
    }

    public string SearchCode(string query, string? assemblyFilter = null, string? namespaceFilter = null, string? kindFilter = null, int limit = 20)
    {
        var results = _indexer.Search(query, limit: limit);

        if (!results.Any())
        {
            return BuildHeader("SearchCode") + "No results found.";
        }

        var filtered = results.Where(r =>
            (assemblyFilter == null || r.Assembly.Equals(assemblyFilter, System.StringComparison.OrdinalIgnoreCase))
            && (namespaceFilter == null || r.FullyQualifiedName.Contains(namespaceFilter, System.StringComparison.OrdinalIgnoreCase))
            && (kindFilter == null || r.Kind.ToString().Equals(kindFilter, System.StringComparison.OrdinalIgnoreCase))
        ).Take(limit).ToList();

        if (!filtered.Any())
        {
            return BuildHeader("SearchCode") + "No results match the given filters.";
        }

        var lines = new List<string> { BuildHeader("SearchCode"), $"Results: {filtered.Count} found" };

        foreach (var r in filtered)
        {
            lines.Add($"  [{r.Kind}] {r.Name}");
            lines.Add($"    SymbolId: {r.SymbolId}");
            lines.Add($"    Assembly: {r.Assembly}");
            lines.Add($"    Namespace: {r.FullyQualifiedName}");
            if (!string.IsNullOrEmpty(r.Signature))
                lines.Add($"    Signature: {r.Signature}");
            if (!string.IsNullOrEmpty(r.Excerpt))
                lines.Add($"    Excerpt: {r.Excerpt}");
            if (r.RelevanceScore > 0)
                lines.Add($"    Score: {r.RelevanceScore:F4}");
            lines.Add(string.Empty);
        }

        return string.Join("\n", lines);
    }

    public string GetMethod(string symbolId)
    {
        if (string.IsNullOrWhiteSpace(symbolId))
            return "GetMethod: symbolId is null or empty.";

        var method = _indexer.GetMethod(symbolId);

        if (method == null)
            return BuildHeader("GetMethod") + $"Method not found: {symbolId}";

        var lines = new List<string>
        {
            BuildHeader("GetMethod"),
            $"SymbolId: {method.SymbolId}",
            $"FullName: {method.FullName}",
            $"DeclaringType: {method.DeclaringTypeFullName}",
            $"Name: {method.Name}",
            $"Signature: {method.Signature}",
            $"ReturnType: {method.ReturnType ?? "(none)"}",
            $"IsStatic: {method.IsStatic}",
            $"IsVirtual: {method.IsVirtual}",
            $"IsAbstract: {method.IsAbstract}",
            $"IsOverride: {method.IsOverride}",
            $"Parameters: [{string.Join(", ", method.Parameters)}]"
        };

        if (method.Attributes.Any())
            lines.Add($"Attributes: [{string.Join(", ", method.Attributes)}]");

        if (method.SourceLocation != null)
        {
            var loc = method.SourceLocation;
            lines.Add($"SourceLocation: {loc.FilePath}:{loc.StartLine}:{loc.StartColumn}");
        }

        if (!string.IsNullOrEmpty(method.Summary))
            lines.Add($"Summary: {method.Summary}");

        if (!string.IsNullOrEmpty(method.Body))
        {
            var truncatedBody = method.Body.Length > 3000
                ? method.Body[..3000] + "..."
                : method.Body;
            lines.Add(string.Empty);
            lines.Add("--- Body ---");
            lines.Add(truncatedBody);
        }

        return string.Join("\n", lines);
    }

    public string GetType(string assemblyName, string typeFullName)
    {
        if (string.IsNullOrWhiteSpace(assemblyName))
            return "GetType: assemblyName is null or empty.";
        if (string.IsNullOrWhiteSpace(typeFullName))
            return "GetType: typeFullName is null or empty.";

        var typeInfo = _indexer.GetTypeInfo(assemblyName, typeFullName);

        if (typeInfo == null)
            return BuildHeader("GetType") + $"Type not found: {typeFullName} in {assemblyName}";

        var lines = new List<string>
        {
            BuildHeader("GetType"),
            $"FullName: {typeInfo.FullName}",
            $"Namespace: {typeInfo.Namespace}",
            $"Name: {typeInfo.Name}",
            $"Assembly: {typeInfo.AssemblyName}",
            $"SymbolId: {typeInfo.SymbolId}"
        };

        if (!string.IsNullOrEmpty(typeInfo.BaseTypeFullName))
            lines.Add($"BaseType: {typeInfo.BaseTypeFullName}");

        if (typeInfo.Interfaces.Any())
            lines.Add($"Interfaces: [{string.Join(", ", typeInfo.Interfaces)}]");

        if (typeInfo.Attributes.Any())
            lines.Add($"Attributes: [{string.Join(", ", typeInfo.Attributes)}]");

        if (typeInfo.DerivedTypes.Any())
            lines.Add($"DerivedTypes ({typeInfo.DerivedTypes.Count}): [{string.Join(", ", typeInfo.DerivedTypes.Take(20))}]");

        if (typeInfo.BaseTypes.Any())
            lines.Add($"BaseTypes ({typeInfo.BaseTypes.Count}): [{string.Join(", ", typeInfo.BaseTypes.Take(20))}]");

        lines.Add(string.Empty);
        lines.Add($"Methods ({typeInfo.Methods.Count}):");
        foreach (var m in typeInfo.Methods.Take(50))
        {
            lines.Add($"  - {m.FullName} (static:{m.IsStatic}, virtual:{m.IsVirtual}, abstract:{m.IsAbstract})");
        }
        if (typeInfo.Methods.Count > 50)
            lines.Add($"  ... and {typeInfo.Methods.Count - 50} more");

        if (typeInfo.Fields.Any())
        {
            lines.Add(string.Empty);
            lines.Add($"Fields ({typeInfo.Fields.Count}):");
            foreach (var f in typeInfo.Fields.Take(30))
            {
                lines.Add($"  - {f.Name}: {f.Type}");
            }
            if (typeInfo.Fields.Count > 30)
                lines.Add($"  ... and {typeInfo.Fields.Count - 30} more");
        }

        if (typeInfo.Properties.Any())
        {
            lines.Add(string.Empty);
            lines.Add($"Properties ({typeInfo.Properties.Count}):");
            foreach (var p in typeInfo.Properties.Take(30))
            {
                lines.Add($"  - {p.Name}: {p.Type}");
            }
            if (typeInfo.Properties.Count > 30)
                lines.Add($"  ... and {typeInfo.Properties.Count - 30} more");
        }

        return string.Join("\n", lines);
    }

    public string GetCallers(string methodSymbolId, int depth = 1, int limit = 50)
    {
        if (string.IsNullOrWhiteSpace(methodSymbolId))
            return "GetCallers: methodSymbolId is null or empty.";

        var directCallers = _indexer.GetCallers(methodSymbolId);

        if (!directCallers.Any())
            return BuildHeader("GetCallers") + "No callers found.";

        var lines = new List<string> { BuildHeader("GetCallers"), $"Direct callers: {directCallers.Count}" };

        foreach (var callerId in directCallers.Take(limit))
        {
            var caller = _indexer.GetMethod(callerId);
            if (caller != null)
            {
                lines.Add($"  {caller.FullName}");
                if (caller.SourceLocation != null)
                    lines.Add($"    Location: {caller.SourceLocation.FilePath}:{caller.SourceLocation.StartLine}");
            }
            else
            {
                lines.Add($"  {callerId}");
            }
        }

        if (depth > 1)
        {
            var visited = new HashSet<string>(directCallers);
            var currentLevel = new List<string>(directCallers);
            var deeperResults = new List<string>();
            var levels = 2;

            while (levels <= depth && currentLevel.Any())
            {
                var nextLevel = new HashSet<string>();

                foreach (var callerId in currentLevel)
                {
                    var callers = _indexer.GetCallers(callerId);
                    foreach (var c in callers)
                    {
                        if (visited.Add(c) && nextLevel.Add(c))
                        {
                            var m = _indexer.GetMethod(c);
                            if (m != null)
                            {
                                deeperResults.Add($"    Level {levels}: {m.FullName}");
                            }
                            else
                            {
                                deeperResults.Add($"    Level {levels}: {c}");
                            }
                        }
                    }
                }

                if (deeperResults.Count >= limit)
                    break;

                currentLevel = nextLevel.ToList();
                levels++;
            }

            if (deeperResults.Any())
            {
                lines.Add(string.Empty);
                lines.Add($"Deeper callers (depth {2}-{depth}): {deeperResults.Count}");
                lines.AddRange(deeperResults.Take(limit));
            }
        }

        return string.Join("\n", lines);
    }

    public string GetCallees(string methodSymbolId, int depth = 1, int limit = 50)
    {
        if (string.IsNullOrWhiteSpace(methodSymbolId))
            return "GetCallees: methodSymbolId is null or empty.";

        var directCallees = _indexer.GetCallees(methodSymbolId);

        if (!directCallees.Any())
            return BuildHeader("GetCallees") + "No callees found.";

        var lines = new List<string> { BuildHeader("GetCallees"), $"Direct callees: {directCallees.Count}" };

        foreach (var calleeId in directCallees.Take(limit))
        {
            var callee = _indexer.GetMethod(calleeId);
            if (callee != null)
            {
                lines.Add($"  {callee.FullName}");
                if (callee.SourceLocation != null)
                    lines.Add($"    Location: {callee.SourceLocation.FilePath}:{callee.SourceLocation.StartLine}");
            }
            else
            {
                lines.Add($"  {calleeId}");
            }
        }

        if (depth > 1)
        {
            var visited = new HashSet<string>(directCallees);
            var currentLevel = new List<string>(directCallees);
            var deeperResults = new List<string>();
            var levels = 2;

            while (levels <= depth && currentLevel.Any())
            {
                var nextLevel = new HashSet<string>();

                foreach (var calleeId in currentLevel)
                {
                    var callees = _indexer.GetCallees(calleeId);
                    foreach (var c in callees)
                    {
                        if (visited.Add(c) && nextLevel.Add(c))
                        {
                            var m = _indexer.GetMethod(c);
                            if (m != null)
                            {
                                deeperResults.Add($"    Level {levels}: {m.FullName}");
                            }
                            else
                            {
                                deeperResults.Add($"    Level {levels}: {c}");
                            }
                        }
                    }
                }

                if (deeperResults.Count >= limit)
                    break;

                currentLevel = nextLevel.ToList();
                levels++;
            }

            if (deeperResults.Any())
            {
                lines.Add(string.Empty);
                lines.Add($"Deeper callees (depth {2}-{depth}): {deeperResults.Count}");
                lines.AddRange(deeperResults.Take(limit));
            }
        }

        return string.Join("\n", lines);
    }

    public string SearchReferences(string symbolId, int limit = 30)
    {
        if (string.IsNullOrWhiteSpace(symbolId))
            return "SearchReferences: symbolId is null or empty.";

        var results = _indexer.SearchReferences(symbolId).Take(limit).ToList();

        if (!results.Any())
            return BuildHeader("SearchReferences") + "No references found.";

        var lines = new List<string> { BuildHeader("SearchReferences"), $"Results: {results.Count} found" };

        foreach (var r in results)
        {
            lines.Add($"  [{r.Kind}] {r.Name}");
            lines.Add($"    SymbolId: {r.SymbolId}");
            lines.Add($"    Assembly: {r.Assembly}");
            if (!string.IsNullOrEmpty(r.Signature))
                lines.Add($"    Signature: {r.Signature}");
            if (!string.IsNullOrEmpty(r.Excerpt))
                lines.Add($"    Excerpt: {r.Excerpt}");
            if (r.RelevanceScore > 0)
                lines.Add($"    Score: {r.RelevanceScore:F4}");
            lines.Add(string.Empty);
        }

        return string.Join("\n", lines);
    }

    public string GetRelatedMethods(string methodSymbolId, int limit = 10)
    {
        if (string.IsNullOrWhiteSpace(methodSymbolId))
            return "GetRelatedMethods: methodSymbolId is null or empty.";

        var targetMethod = _indexer.GetMethod(methodSymbolId);
        if (targetMethod == null)
            return BuildHeader("GetRelatedMethods") + $"Method not found: {methodSymbolId}";

        var seen = new Dictionary<string, (string reason, MethodInfo method)>();

        // Strategy 1: methods in the same declaring type
        var sameTypeMethods = _indexer.GetMethods(targetMethod.AssemblyName)
            .Where(m => m.DeclaringTypeFullName == targetMethod.DeclaringTypeFullName && m.SymbolId != methodSymbolId);

        foreach (var m in sameTypeMethods.Take(limit))
        {
            if (!seen.ContainsKey(m.SymbolId))
                seen[m.SymbolId] = ($"Same type: {targetMethod.DeclaringTypeFullName}", m);
        }

        // Strategy 2: methods sharing callers with the target
        var targetCallers = _indexer.GetCallers(methodSymbolId).ToList();
        foreach (var callerId in targetCallers)
        {
            var callersCallees = _indexer.GetCallees(callerId);
            foreach (var calleeId in callersCallees.Take(20))
            {
                if (calleeId == methodSymbolId || seen.ContainsKey(calleeId))
                    continue;
                var callee = _indexer.GetMethod(calleeId);
                if (callee != null)
                    seen[calleeId] = ($"Shares caller with target", callee);
            }
        }

        // Strategy 3: methods sharing callees with the target
        var targetCallees = _indexer.GetCallees(methodSymbolId).ToList();
        foreach (var calleeId in targetCallees)
        {
            var calleesCallers = _indexer.GetCallers(calleeId);
            foreach (var callerId in calleesCallers.Take(20))
            {
                if (callerId == methodSymbolId || seen.ContainsKey(callerId))
                    continue;
                var caller = _indexer.GetMethod(callerId);
                if (caller != null)
                    seen[callerId] = ($"Shares callee with target", caller);
            }
        }

        var related = seen.Values.Take(limit).ToList();

        if (!related.Any())
            return BuildHeader("GetRelatedMethods") + "No related methods found.";

        var lines = new List<string> { BuildHeader("GetRelatedMethods"), $"Results: {related.Count} found" };

        foreach (var (reason, m) in related)
        {
            lines.Add($"  [Reason: {reason}]");
            lines.Add($"  {m.FullName}");
            lines.Add($"    Signature: {m.Signature}");
            lines.Add($"    IsStatic: {m.IsStatic}, IsVirtual: {m.IsVirtual}");
            if (m.SourceLocation != null)
                lines.Add($"    Location: {m.SourceLocation.FilePath}:{m.SourceLocation.StartLine}");
            lines.Add(string.Empty);
        }

        return string.Join("\n", lines);
    }

    private static string BuildHeader(string methodName) => $"=== {methodName} ===\n";
}
