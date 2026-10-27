using System;
using System.Collections.Generic;
using System.Linq;

using DeepTrace.Core.Models;

namespace DeepTrace.Indexing;

/// <summary>
/// Generates human-readable summaries of method purpose from metadata and body text.
/// Uses heuristic analysis of method name, parameters, body content, and lifecycle patterns.
/// </summary>
public static class MethodSummaryGenerator
{
    private const int MaxLength = 120;

    private static readonly string[] BoilerplatePatterns =
    {
        "if (", "throw new", "base.", "this.", "return;", "get;", "set;",
        "set {", "get {", "=> throw", "=> null", "=> base.", "=> this.",
        "lock (", "try {", "catch (", "finally {", "using (", "checked (",
        "unchecked (", "stackalloc", "goto ", "default(", "new ", "var ",
        "object ", "string ", "int ", "long ", "bool ", "void ",
    };

    private static readonly string[] LifecycleKeywords =
    {
        "On", "Handle", "Process", "Initialize", "Start", "Stop",
        "Create", "Destroy", "Load", "Unload", "Update", "FixedUpdate",
        "LateUpdate", "Awake", "Reset", "Dispose", "Close",
        "Open", "Connect", "Disconnect", "Subscribe", "Unsubscribe",
        "Register", "Unregister", "Add", "Remove", "Enable", "Disable",
        "Enter", "Exit", "Focus", "Blur", "FocusIn", "FocusOut",
    };

    public static string Generate(MethodInfo method, string? body)
    {
        if (string.IsNullOrEmpty(body))
            return GenerateFromSignature(method);

        var lines = body.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        var meaningfulLines = StripCommentsAndFilter(lines).ToArray();
        var actionLine = FindPrimaryAction(meaningfulLines);

        if (actionLine != null)
            return BuildSummary(method, actionLine, body);

        return GenerateFromSignature(method);
    }

    private static IEnumerable<string> StripCommentsAndFilter(string[] lines)
    {
        foreach (var line in lines)
        {
            var stripped = StripSingleLineComment(line.Trim());
            if (string.IsNullOrEmpty(stripped))
                continue;
            yield return stripped;
        }
    }

    private static string StripSingleLineComment(string line)
    {
        var inString = false;
        var inVerbatim = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (inVerbatim)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                        i++;
                    else
                        inVerbatim = false;
                }
                continue;
            }
            if (inString)
            {
                if (c == '\\')
                    i++;
                else if (c == '"')
                    inString = false;
                continue;
            }
            if (c == '"')
            {
                if (i + 1 < line.Length && line[i + 1] == '"')
                {
                    inVerbatim = true;
                    i++;
                }
                else
                {
                    inString = true;
                }
                continue;
            }
            if (c == '/' && i + 1 < line.Length && line[i + 1] == '/')
                return line.Substring(0, i).Trim();
        }
        return line;
    }

    private static string? FindPrimaryAction(string[] lines)
    {
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed))
                continue;
            if (IsBoilerplate(trimmed))
                continue;
            if (trimmed.StartsWith("[") || trimmed.StartsWith("namespace ") || trimmed.StartsWith("using "))
                continue;
            if (trimmed == "{" || trimmed == "}" || trimmed.StartsWith("{") || trimmed.StartsWith("}"))
                continue;
            if (IsMethodSignatureLine(trimmed))
                continue;
            return trimmed;
        }
        return null;
    }

    private static bool IsBoilerplate(string line)
    {
        var lower = line.ToLowerInvariant();
        if (lower.Contains("if (") && (lower.Contains("== null") || lower.Contains("!= null") || lower.Contains("is null") || lower.Contains("is not null")))
            return true;
        if (lower.Contains("base.") && lower.Contains("(") && lower.Contains(")"))
            return true;
        if (lower == "return;" || lower == "return null;" || lower == "return default;")
            return true;
        if (lower.Contains("get {") || lower.Contains("set {"))
            return true;
        if (lower.Contains("try {") || lower.Contains("catch (") || lower.Contains("finally {"))
            return true;
        if (lower.Contains("lock ("))
            return true;
        if (lower.Contains("using ("))
            return true;
        if (lower.Contains("default("))
            return true;
        return false;
    }

    private static bool IsMethodSignatureLine(string line)
    {
        var lowered = line.ToLowerInvariant();
        var modifiers = new[] { "public", "private", "protected", "internal", "static", "virtual", "override", "abstract", "sealed", "new", "async", "unsafe", "extern" };
        bool hasModifier = modifiers.Any(m => lowered.StartsWith(m + " ") || lowered.Contains(" " + m + " "));
        bool hasParen = line.Contains('(');
        bool hasBrace = line.Contains('{');

        if (hasModifier && hasParen && !hasBrace)
            return true;

        if (lowered.Contains('(') && !lowered.Contains("void") && !lowered.Contains("string") && !lowered.Contains("int") && !lowered.Contains("bool") && !lowered.Contains("return"))
        {
            var parenIdx = line.IndexOf('(');
            var beforeParen = line.Substring(0, parenIdx).Trim();
            var words = beforeParen.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length >= 2 && !words[0].Contains(" ") && !words[0].Contains("<"))
            {
                var lastWord = words[words.Length - 1];
                if (!IsCommonType(lastWord))
                    return true;
            }
        }

        return false;
    }

    private static bool IsCommonType(string word)
    {
        var lower = word.ToLowerInvariant();
        return lower is "void" or "string" or "int" or "long" or "bool" or "float" or "double" or "byte" or "char" or "short"
            or "object" or "type" or "parameter" or "argument" or "value" or "result" or "task";
    }

    private static string GenerateFromSignature(MethodInfo method)
    {
        if (method.Name == ".ctor" || method.Name == ".cctor")
        {
            var type = method.DeclaringTypeFullName;
            if (method.IsStatic)
                return Truncate("Static initializer for " + type, MaxLength);
            var ctorParams = BuildParamsDescription(method.Parameters);
            if (ctorParams.Length > 0)
                return Truncate("Initializes " + type + " with " + ctorParams, MaxLength);
            return Truncate("Initializes " + type, MaxLength);
        }

        if (method.Name.StartsWith("get_"))
            return Truncate("Gets " + method.Name.Substring(4), MaxLength);
        if (method.Name.StartsWith("set_"))
            return Truncate("Sets " + method.Name.Substring(4), MaxLength);
        if (method.Name.StartsWith("add_"))
            return Truncate("Adds handler for " + method.Name.Substring(4), MaxLength);
        if (method.Name.StartsWith("remove_"))
            return Truncate("Removes handler for " + method.Name.Substring(4), MaxLength);

        var attrsLower = string.Join(" ", method.Attributes.Select(a => a.ToLowerInvariant()));
        if (method.IsOverride || attrsLower.Contains("[override]"))
        {
            var action = DetermineActionFromName(method.Name);
            return Truncate("Overrides base " + method.Name + " to " + action, MaxLength);
        }

        if (method.IsStatic && (
            method.Name.Contains("Create", StringComparison.Ordinal) ||
            method.Name.Contains("From", StringComparison.Ordinal) ||
            method.Name.Contains("Parse", StringComparison.Ordinal) ||
            method.Name.Contains("TryParse", StringComparison.Ordinal) ||
            method.Name.Contains("Build", StringComparison.Ordinal) ||
            method.Name.Contains("Convert", StringComparison.Ordinal) ||
            method.Name.Contains("Get", StringComparison.Ordinal) ||
            method.Name.Contains("Find", StringComparison.Ordinal)))
        {
            var action = DetermineActionFromName(method.Name);
            var factoryRetType = string.IsNullOrEmpty(method.ReturnType) ? "value" : method.ReturnType;
            return Truncate("Creates " + factoryRetType + " " + action, MaxLength);
        }

        if (IsLifecycleCallback(method))
        {
            var phase = ExtractLifecyclePhase(method.Name);
            return Truncate("Handles " + phase, MaxLength);
        }

        var actionFromName = DetermineActionFromName(method.Name);
        var paramDesc = BuildParamsDescription(method.Parameters);
        var retType = string.IsNullOrEmpty(method.ReturnType) ? "void" : method.ReturnType;

        if (retType == "void")
            return Truncate(method.Name + " " + paramDesc, MaxLength);

        return Truncate("Returns " + retType + " " + actionFromName, MaxLength);
    }

    private static string BuildSummary(MethodInfo method, string actionLine, string body)
    {
        var trimmed = actionLine.TrimEnd(';').Trim();

        if (trimmed.StartsWith("return ", StringComparison.Ordinal))
        {
            var expr = StripWrapper(trimmed.Substring(7).Trim());
            return Truncate("Returns " + expr, MaxLength);
        }

        if (IsFieldAssignment(trimmed))
        {
            var parts = SplitAssignment(trimmed);
            if (parts != null)
                return Truncate("Assigns " + parts.Value.field + " to " + parts.Value.value, MaxLength);
        }

        var callee = ExtractFirstMethodCall(trimmed);
        if (callee != null)
        {
            var prep = DeterminePrepAction(method.Name, trimmed);
            var prepText = prep.Length > 0 ? " " + prep : "";
            return Truncate("Calls " + callee.Value.callee + prepText, MaxLength);
        }

        if (IsPropertyAssignment(trimmed))
        {
            var parts2 = SplitPropertyAssignment(trimmed);
            if (parts2 != null)
                return Truncate("Sets " + parts2.Value.prop + " to " + parts2.Value.value, MaxLength);
        }

        return GenerateFromSignature(method);
    }

    private static bool IsFieldAssignment(string trimmed)
    {
        if (!trimmed.Contains('='))
            return false;
        var eqIdx = trimmed.IndexOf('=');
        var beforeEq = trimmed.Substring(0, eqIdx).Trim();
        if (beforeEq.Contains('.') || beforeEq.Contains('('))
            return false;
        if (trimmed.Length > eqIdx + 1 && trimmed[eqIdx + 1] == '=')
            return false;
        if (!char.IsLetter(beforeEq[0]) && beforeEq[0] != '_')
            return false;
        return true;
    }

    private static (string field, string value)? SplitAssignment(string trimmed)
    {
        var eqIdx = trimmed.IndexOf('=');
        if (eqIdx <= 0) return null;
        var fieldName = trimmed.Substring(0, eqIdx).Trim();
        var value = trimmed.Substring(eqIdx + 1).Trim();
        if (string.IsNullOrEmpty(fieldName) || string.IsNullOrEmpty(value))
            return null;
        return (fieldName, StripWrapper(value));
    }

    private static bool IsPropertyAssignment(string trimmed)
    {
        if (!trimmed.Contains('.'))
            return false;
        var eqIdx = trimmed.IndexOf('=');
        if (eqIdx <= 0) return false;
        var beforeEq = trimmed.Substring(0, eqIdx).Trim();
        var dotIdx = beforeEq.LastIndexOf('.');
        if (dotIdx <= 0) return false;
        var obj = beforeEq.Substring(0, dotIdx).Trim();
        var prop = beforeEq.Substring(dotIdx + 1).Trim();
        if (string.IsNullOrEmpty(obj) || string.IsNullOrEmpty(prop))
            return false;
        if (beforeEq.Contains('('))
            return false;
        return true;
    }

    private static (string prop, string value)? SplitPropertyAssignment(string trimmed)
    {
        var eqIdx = trimmed.IndexOf('=');
        if (eqIdx <= 0) return null;
        var beforeEq = trimmed.Substring(0, eqIdx).Trim();
        var value = trimmed.Substring(eqIdx + 1).Trim();
        var dotIdx = beforeEq.LastIndexOf('.');
        if (dotIdx <= 0) return null;
        return (beforeEq.Substring(dotIdx + 1).Trim(), StripWrapper(value));
    }

    private static (string callee, string? args)? ExtractFirstMethodCall(string trimmed)
    {
        var parenIdx = trimmed.IndexOf('(');
        if (parenIdx <= 0) return null;
        var end = parenIdx;
        var start = end;
        while (start > 0 && (char.IsLetterOrDigit(trimmed[start - 1]) || trimmed[start - 1] == '_'))
            start--;
        if (start == end) return null;
        var callee = trimmed.Substring(start, end - start).Trim();
        if (string.IsNullOrEmpty(callee)) return null;
        var lower = callee.ToLowerInvariant();
        if (lower is "if" or "while" or "for" or "foreach" or "switch" or "catch" or "return"
            or "throw" or "new" or "typeof" or "nameof" or "default" or "checked" or "unchecked"
            or "lock" or "using" or "yield")
            return null;
        return (callee, null);
    }

    private static bool IsLifecycleCallback(MethodInfo method)
    {
        if (method.Name.StartsWith("On", StringComparison.Ordinal) && method.Name.Length > 2)
        {
            var secondChar = method.Name[2];
            if (char.IsUpper(secondChar))
                return true;
        }
        var attrsLower = string.Join(" ", method.Attributes.Select(a => a.ToLowerInvariant()));
        var lifecycleAttrs = new[] { "eventhandler", "handle", "callback", "lifecycle", "initialize", "setup", "teardown" };
        return lifecycleAttrs.Any(a => attrsLower.Contains(a));
    }

    private static string ExtractLifecyclePhase(string methodName)
    {
        if (methodName.StartsWith("On", StringComparison.Ordinal))
            return methodName.Substring(2);
        var prefixes = new[] { "Handle", "Process", "Initialize", "Start", "Stop", "Create", "Destroy" };
        foreach (var prefix in prefixes)
        {
            if (methodName.StartsWith(prefix, StringComparison.Ordinal))
                return methodName.Substring(prefix.Length);
        }
        return methodName;
    }

    private static string DetermineActionFromName(string methodName)
    {
        var lower = methodName.ToLowerInvariant();
        if (lower.Contains("create")) return "create";
        if (lower.Contains("build")) return "build";
        if (lower.Contains("parse")) return "parse";
        if (lower.Contains("convert")) return "convert";
        if (lower.Contains("find")) return "find";
        if (lower.Contains("get")) return "get";
        if (lower.Contains("set")) return "set";
        if (lower.Contains("add")) return "add";
        if (lower.Contains("remove")) return "remove";
        if (lower.Contains("update")) return "update";
        if (lower.Contains("delete")) return "delete";
        if (lower.Contains("save")) return "save";
        if (lower.Contains("load")) return "load";
        if (lower.Contains("init")) return "initialize";
        if (lower.Contains("start")) return "start";
        if (lower.Contains("stop")) return "stop";
        if (lower.Contains("process")) return "process";
        if (lower.Contains("handle")) return "handle";
        if (lower.Contains("check")) return "check";
        if (lower.Contains("validate")) return "validate";
        if (lower.Contains("calculate")) return "calculate";
        if (lower.Contains("compute")) return "compute";
        if (lower.Contains("send")) return "send";
        if (lower.Contains("receive")) return "receive";
        if (lower.Contains("connect")) return "connect";
        if (lower.Contains("disconnect")) return "disconnect";
        if (methodName.Length > 1 && char.IsUpper(methodName[0]))
            return methodName;
        return "execute";
    }

    private static string DeterminePrepAction(string methodName, string line)
    {
        var lower = line.ToLowerInvariant();
        if (lower.Contains("before ") || lower.Contains(" before "))
            return "before";
        if (lower.Contains("after ") || lower.Contains(" after "))
            return "after";
        if (lower.Contains("then "))
            return "then";
        if (lower.Contains("finally "))
            return "finally";
        if (lower.Contains("else"))
            return "otherwise";
        var nameLower = methodName.ToLowerInvariant();
        if (nameLower.Contains("prepare")) return "preparing";
        if (nameLower.Contains("setup")) return "setting up";
        if (nameLower.Contains("configure")) return "configuring";
        if (nameLower.Contains("initialize")) return "initializing";
        return "";
    }

    private static string BuildParamsDescription(IReadOnlyList<string> parameters)
    {
        if (parameters.Count == 0)
            return "";
        var parts = parameters.Take(3).ToList();
        var desc = string.Join(", ", parts);
        if (parameters.Count > 3)
            desc += ", ...";
        return desc;
    }

    private static string StripWrapper(string expr)
    {
        expr = expr.Trim();
        if (expr.StartsWith("new ", StringComparison.Ordinal))
            expr = expr.Substring(4).Trim();
        return expr;
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
            return value;
        if (value.Length <= maxLength)
            return value;
        return value.Substring(0, maxLength);
    }
}
