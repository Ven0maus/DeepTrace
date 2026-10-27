using System.Collections.Generic;

namespace DeepTrace.Core.Models;

/// <summary>
/// Metadata for a single type extracted from a decompiled assembly.
/// </summary>
public sealed class TypeInfo
{
    /// <summary>Unique symbol identifier in format "Assembly::Namespace.Type".</summary>
    public string SymbolId { get; init; } = string.Empty;

    /// <summary>Fully qualified type name (e.g., "TestLib.Greeter").</summary>
    public string FullName { get; init; } = string.Empty;

    /// <summary>Namespace (e.g., "TestLib").</summary>
    public string Namespace { get; init; } = string.Empty;

    /// <summary>Simple type name (e.g., "Greeter").</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Direct base type name, or null for object types.</summary>
    public string? BaseTypeFullName { get; init; }

    /// <summary>Assembly name where this type is defined.</summary>
    public string AssemblyName { get; init; } = string.Empty;

    /// <summary>Interfaces implemented by this type.</summary>
    public IReadOnlyList<string> Interfaces { get; set; } = new List<string>();

    /// <summary>Custom attributes applied to the type.</summary>
    public IReadOnlyList<string> Attributes { get; set; } = new List<string>();

    /// <summary>Methods defined in this type.</summary>
    public IReadOnlyList<MethodInfo> Methods { get; set; } = new List<MethodInfo>();

    /// <summary>Fields defined in this type.</summary>
    public IReadOnlyList<FieldInfo> Fields { get; set; } = new List<FieldInfo>();

    /// <summary>Properties defined in this type.</summary>
    public IReadOnlyList<PropertyInfo> Properties { get; set; } = new List<PropertyInfo>();

    /// <summary>Types that directly derive from this type.</summary>
    public IReadOnlyList<string> DerivedTypes { get; set; } = new List<string>();

    /// <summary>Base types in the inheritance chain (excluding object).</summary>
    public IReadOnlyList<string> BaseTypes { get; set; } = new List<string>();

    /// <summary>Other types referenced by this type (fields, methods, parameters).</summary>
    public IReadOnlyList<string> TypeReferences { get; set; } = new List<string>();
}
