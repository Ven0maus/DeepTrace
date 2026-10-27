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
    /// Uses line-by-line scanning to find the method declaration, then brace-counting
    /// to extract the method block.
    /// </summary>
    internal static string? ExtractMethod(string typeSource, string methodSignature)
    {
        var parenIdx = methodSignature.IndexOf('(');
        if (parenIdx <= 0)
            return null;
        var methodName = methodSignature[..parenIdx].Trim();

        var lines = typeSource.Split('\n');

        // Compute cumulative line positions
        var linePositions = new int[lines.Length];
        linePositions[0] = 0;
        for (var i = 1; i < lines.Length; i++)
            linePositions[i] = linePositions[i - 1] + lines[i - 1].Length + 1; // +1 for newline

        for (var lineIdx = 0; lineIdx < lines.Length; lineIdx++)
        {
            var line = lines[lineIdx];
            var trimmed = line.TrimStart();

            // Check if this line contains the method declaration
            var methodStartInTrimmed = FindMethodPosition(trimmed, methodName);
            if (methodStartInTrimmed < 0)
                continue;

            // The paren is right after the method name in the trimmed line
            var methodParenInTrimmed = methodStartInTrimmed + methodName.Length;

            // Compute absolute position: lineStart + leading whitespace offset + paren position
            var leadingWhitespace = line.Length - trimmed.Length;
            var absParenPos = linePositions[lineIdx] + leadingWhitespace + methodParenInTrimmed;

            // Find the opening brace starting from the paren
            var restFromLine = typeSource.Substring(linePositions[lineIdx]);
            var parenOffset = absParenPos - linePositions[lineIdx];
            var braceOffset = FindBraceAfterMethodDeclaration(restFromLine, parenOffset);
            if (braceOffset < 0)
                continue;

            var bracePos = linePositions[lineIdx] + braceOffset;

            // Find matching closing brace
            var endPos = FindMatchingBrace(typeSource, bracePos);
            if (endPos < 0)
                continue;

            return typeSource.Substring(bracePos, endPos - bracePos + 1).Trim();
        }

        return null;
    }

    /// <summary>
    /// Find the position of methodName( in a trimmed line. Returns -1 if not found.
    /// </summary>
    private static int FindMethodPosition(string trimmedLine, string methodName)
    {
        var searchPos = 0;
        while (searchPos < trimmedLine.Length)
        {
            var parenIdx = trimmedLine.IndexOf('(', searchPos);
            if (parenIdx < 0)
                return -1;

            var nameEnd = parenIdx;
            var nameStart = nameEnd;

            // Check if char before ( is '>' — generic method
            var isGeneric = nameEnd > 0 && trimmedLine[nameEnd - 1] == '>';

            while (nameStart > 0 && IsIdentifierChar(trimmedLine[nameStart - 1]))
                nameStart--;

            var potentialName = trimmedLine[nameStart..nameEnd];

            // Handle generic methods: Process<T> → extend left to include name before <...>
            if (isGeneric)
            {
                var gtPos = nameEnd - 1;
                var ltPos = trimmedLine.LastIndexOf('<', gtPos);
                if (ltPos >= 0)
                {
                    // Go back from ltPos to get the base name (e.g., "Process" from "Process<T>")
                    var baseNameStart = ltPos;
                    while (baseNameStart > 0 && IsIdentifierChar(trimmedLine[baseNameStart - 1]))
                        baseNameStart--;
                    var fullGenericName = trimmedLine[baseNameStart..nameEnd];
                    if (fullGenericName == methodName)
                    {
                        if (!IsCSharpKeywordOrType(trimmedLine[(baseNameStart + 1)..(nameEnd - 1)]))
                        {
                            if (baseNameStart > 0 && trimmedLine[baseNameStart - 1] == '.')
                            {
                                searchPos = parenIdx + 1;
                                continue;
                            }
                            return baseNameStart;
                        }
                    }
                }
            }

            if (potentialName == methodName && !IsCSharpKeywordOrType(potentialName))
            {
                // Reject if preceded by '.' (method call like base.MethodName())
                if (nameStart > 0 && trimmedLine[nameStart - 1] == '.')
                {
                    searchPos = parenIdx + 1;
                    continue;
                }
                return nameStart;
            }

            searchPos = parenIdx + 1;
        }
        return -1;
    }

    /// <summary>
    /// Find the opening brace after a method declaration.
    /// Start scanning from the given absolute position in source.
    /// </summary>
    private static int FindBraceAfterMethodDeclaration(string source, int absParenPos)
    {
        // Start from the opening paren of the method signature
        var i = absParenPos;
        var depth = 0;
        var inString = false;
        var inChar = false;
        var inGeneric = 0;
        var inComment = false;
        var inLineComment = false;

        // Limit search to 3 lines ahead to avoid finding other members
        var lineCount = 0;
        var maxLines = 3;

        while (i < source.Length && lineCount < maxLines)
        {
            var c = source[i];

            // Handle comments
            if (inLineComment) { if (c == '\n') { inLineComment = false; lineCount++; } i++; continue; }
            if (inComment) { if (c == '*' && i + 1 < source.Length && source[i + 1] == '/') { inComment = false; } i++; continue; }

            if (inString) { if (c == '"') inString = false; i++; continue; }
            if (inChar) { if (c == '\'') inChar = false; i++; continue; }

            if (c == '"') { inString = true; i++; continue; }
            if (c == '\'') { inChar = true; i++; continue; }

            if (c == '/' && i + 1 < source.Length)
            {
                if (source[i + 1] == '/') { inLineComment = true; i++; continue; }
                if (source[i + 1] == '*') { inComment = true; i++; continue; }
            }

            if (c == '<') { inGeneric++; i++; continue; }
            if (c == '>') { if (inGeneric > 0) inGeneric--; i++; continue; }

            if (inGeneric > 0) { i++; continue; }

            if (c == '(') { depth++; i++; continue; }
            if (c == ')') { if (depth > 0) depth--; i++; continue; }

            if (depth == 0 && c == '{')
                return i;

            if (c == '\n')
                lineCount++;

            i++;
        }

        return -1;
    }

    private static bool FindMethodDeclaration(string trimmedLine, string methodName)
    {
        // Look for methodName( in the line
        var searchPos = 0;
        while (searchPos < trimmedLine.Length)
        {
            var parenIdx = trimmedLine.IndexOf('(', searchPos);
            if (parenIdx < 0)
                return false;

            // Walk back from '(' to find the identifier
            var nameEnd = parenIdx;
            var nameStart = nameEnd;
            while (nameStart > 0 && IsIdentifierChar(trimmedLine[nameStart - 1]))
                nameStart--;

            var potentialName = trimmedLine[nameStart..nameEnd];
            if (potentialName == methodName && !IsCSharpKeywordOrType(potentialName))
                return true;

            searchPos = parenIdx + 1;
        }
        return false;
    }

    private static bool IsIdentifierChar(char c) =>
        (c >= 'a' && c <= 'z') ||
        (c >= 'A' && c <= 'Z') ||
        (c >= '0' && c <= '9') ||
        c == '_';

    private static bool IsCSharpKeywordOrType(string text)
    {
        return text is "public" or "private" or "protected" or "internal" or
               "static" or "virtual" or "override" or "sealed" or "abstract" or
               "new" or "unsafe" or "fixed" or "extern" or "async" or
               "readonly" or "partial" or "void" or "int" or "string" or
               "bool" or "float" or "double" or "long" or "short" or
               "char" or "object" or "type" or "Task" or "IEnumerator" or
               "IEnumerable" or "var" or "return" or "yield" or "throw" or
               "if" or "else" or "for" or "foreach" or "while" or "do" or
               "switch" or "case" or "default" or "try" or "catch" or
               "finally" or "class" or "struct" or "interface" or "enum" or
               "namespace" or "using" or "this" or "base" or "null" or
               "true" or "false" or "const" or "init" or "set" or "get";
    }

    private static int FindBraceInLineOrAfter(string source)
    {
        var i = 0;
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
