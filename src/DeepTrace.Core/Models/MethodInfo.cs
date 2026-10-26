using System.Collections.Generic;

namespace DeepTrace.Core.Models;

public sealed class MethodInfo
{
    public string SymbolId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string DeclaringTypeFullName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Signature { get; set; } = string.Empty;
    public string? ReturnType { get; set; }
    public IReadOnlyList<string> Parameters { get; set; } = new List<string>();
    public IReadOnlyList<string> Attributes { get; set; } = new List<string>();
    public string? Body { get; set; }
    public SourceLocation? SourceLocation { get; set; }
    public string AssemblyName { get; set; } = string.Empty;
    public bool IsStatic { get; set; }
    public bool IsVirtual { get; set; }
    public bool IsAbstract { get; set; }
    public bool IsOverride { get; set; }
    public string? Summary { get; set; }
}
