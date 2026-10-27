using System.Collections.Generic;

namespace DeepTrace.Core.Models;

/// <summary>
/// Metadata for a single property extracted from a decompiled assembly.
/// </summary>
public sealed class PropertyInfo
{
    /// <summary>Unique symbol identifier.</summary>
    public string SymbolId { get; init; } = string.Empty;

    /// <summary>Full property name including declaring type.</summary>
    public string FullName { get; init; } = string.Empty;

    /// <summary>Fully qualified declaring type name.</summary>
    public string DeclaringTypeFullName { get; init; } = string.Empty;

    /// <summary>Simple property name (e.g., "BaseProperty").</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Property type name (e.g., "System.String").</summary>
    public string Type { get; init; } = string.Empty;

    /// <summary>Assembly name where this property is defined.</summary>
    public string AssemblyName { get; init; } = string.Empty;

    /// <summary>Whether the property is static.</summary>
    public bool IsStatic { get; init; }
}
