using System.Collections.Generic;

namespace DeepTrace.Core.Models;

public sealed class FieldInfo
{
    public string SymbolId { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string DeclaringTypeFullName { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public string AssemblyName { get; init; } = string.Empty;
    public bool IsStatic { get; init; }
    public bool IsLiteral { get; init; }
    public object? LiteralValue { get; init; }
}
