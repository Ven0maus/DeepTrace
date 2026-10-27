using System.Collections.Concurrent;
using DeepTrace.Core.Interfaces;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.TypeSystem;

namespace DeepTrace.Decompiler;

/// <summary>
/// ILSpy-based decompiler. Extracts C# source from .NET assemblies using ICSharpCode.Decompiler.
/// </summary>
public sealed class IlspyDecompiler : IDecompiler, IDisposable
{
    private readonly ConcurrentDictionary<string, DecompilerSettings> _settingsCache = new();
    private readonly ConcurrentDictionary<string, string> _typeSourceCache = new();
    private bool _disposed;

    /// <summary>
    /// Decompile a single method. Caches the full type source to avoid re-decompiling the type
    /// when multiple methods from the same type are requested.
    /// </summary>
    public string DecompileMethod(string assemblyPath, string typeFullName, string methodSignature)
    {
        var cacheKey = $"{assemblyPath}|||{typeFullName}";
        var typeSource = _typeSourceCache.GetOrAdd(cacheKey, _ =>
        {
            var settings = _settingsCache.GetOrAdd(assemblyPath, _ => CreateSettings());
            var decompiler = new CSharpDecompiler(assemblyPath, settings);
            var fullName = new FullTypeName(typeFullName);
            return decompiler.DecompileTypeAsString(fullName);
        });

        if (string.IsNullOrWhiteSpace(typeSource))
            return string.Empty;

        return ExtractMethod(typeSource, methodSignature) ?? string.Empty;
    }

    public string? TryDecompileMethod(string assemblyPath, string typeFullName, string methodSignature)
    {
        try
        {
            return DecompileMethod(assemblyPath, typeFullName, methodSignature);
        }
        catch
        {
            return null;
        }
    }

    public IReadOnlyList<string> DecompileType(string assemblyPath, string typeFullName)
    {
        var settings = _settingsCache.GetOrAdd(assemblyPath, _ => CreateSettings());
        var decompiler = new CSharpDecompiler(assemblyPath, settings);
        var fullName = new FullTypeName(typeFullName);

        var source = decompiler.DecompileTypeAsString(fullName);
        if (string.IsNullOrWhiteSpace(source))
            return Array.Empty<string>();

        return new[] { source };
    }

    private static DecompilerSettings CreateSettings()
    {
        var settings = new DecompilerSettings();
        settings.DecompileMemberBodies = true;
        settings.ShowXmlDocumentation = false;
        settings.UseDebugSymbols = false;
        return settings;
    }

    /// <summary>
    /// Extract a specific method body from type source by matching the method signature.
    /// Uses brace-counting to find the method block.
    /// </summary>
    private static string? ExtractMethod(string typeSource, string methodSignature)
    {
        var methodName = ExtractMethodName(methodSignature);
        if (methodName == null)
            return null;

        // Find the method declaration
        var index = typeSource.IndexOf(methodName);
        if (index < 0)
            return null;

        // Find the opening brace for this method
        // Walk backwards from the method name to find the signature start
        var braceStart = FindOpeningBrace(typeSource, index);
        if (braceStart < 0)
            return null;

        // Find the matching closing brace
        var endPos = FindMatchingBrace(typeSource, braceStart);
        if (endPos < 0)
            return null;

        return typeSource.Substring(braceStart, endPos - braceStart + 1).Trim();
    }

    private static string? ExtractMethodName(string signature)
    {
        var parenIndex = signature.IndexOf('(');
        if (parenIndex <= 0)
            return null;
        return signature[..parenIndex].Trim();
    }

    private static int FindOpeningBrace(string source, int fromIndex)
    {
        // Search forward from the method name for the opening brace
        // Skip over return type, modifiers, generics, parameters
        var i = fromIndex;
        var depth = 0;
        var inString = false;
        var inChar = false;
        var inGeneric = 0;

        while (i < source.Length)
        {
            var c = source[i];

            if (inString) { if (c == '"') inString = false; i++; continue; }
            if (inChar) { if (c == '\'') inChar = false; i++; continue; }

            if (c == '"') { inString = true; i++; continue; }
            if (c == '\'') { inChar = true; i++; continue; }

            if (c == '<') inGeneric++;
            if (c == '>' && inGeneric > 0) inGeneric--;

            if (inGeneric > 0) { i++; continue; }

            if (c == '(') { depth++; i++; continue; }
            if (c == ')' && depth > 0) { depth--; i++; continue; }

            if (depth == 0 && c == '{')
                return i;

            i++;
        }

        return -1;
    }

    private static int FindMatchingBrace(string source, int openBraceIndex)
    {
        var depth = 0;
        var inString = false;
        var inChar = false;
        var inComment = false;
        var inLineComment = false;

        for (var i = openBraceIndex; i < source.Length; i++)
        {
            var c = source[i];

            // Handle line comments
            if (inLineComment) { if (c == '\n') inLineComment = false; continue; }

            // Handle block comments
            if (inComment) { if (c == '*' && i + 1 < source.Length && source[i + 1] == '/') inComment = false; continue; }

            if (inString) { if (c == '"' && (i == 0 || source[i - 1] != '\\')) { inString = false; } continue; }
            if (inChar) { if (c == '\'' && (i == 0 || source[i - 1] != '\\')) { inChar = false; } continue; }

            if (c == '"') { inString = true; continue; }
            if (c == '\'') { inChar = true; continue; }

            if (c == '/' && i + 1 < source.Length)
            {
                if (source[i + 1] == '/') { inLineComment = true; continue; }
                if (source[i + 1] == '*') { inComment = true; continue; }
            }

            if (c == '{') depth++;
            if (c == '}')
            {
                depth--;
                if (depth == 0)
                    return i;
            }
        }

        return -1;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _settingsCache.Clear();
        _typeSourceCache.Clear();
    }
}
