using System.Collections.Generic;

namespace DeepTrace.Core.Models;

public sealed class TypeInfo
{
    public string SymbolId { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string Namespace { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? BaseTypeFullName { get; init; }
    public string AssemblyName { get; init; } = string.Empty;
    public IReadOnlyList<string> Interfaces { get; set; } = new List<string>();
    public IReadOnlyList<string> Attributes { get; set; } = new List<string>();
    public IReadOnlyList<MethodInfo> Methods { get; set; } = new List<MethodInfo>();
    public IReadOnlyList<FieldInfo> Fields { get; set; } = new List<FieldInfo>();
    public IReadOnlyList<PropertyInfo> Properties { get; set; } = new List<PropertyInfo>();
    public IReadOnlyList<string> DerivedTypes { get; set; } = new List<string>();
    public IReadOnlyList<string> BaseTypes { get; set; } = new List<string>();
    public IReadOnlyList<string> TypeReferences { get; set; } = new List<string>();
}
