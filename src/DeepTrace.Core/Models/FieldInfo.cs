using System.Collections.Generic;

namespace DeepTrace.Core.Models;

/// <summary>
/// Metadata for a single field extracted from a decompiled assembly.
/// </summary>
public sealed class FieldInfo
{
    /// <summary>Unique symbol identifier.</summary>
    public string SymbolId { get; init; } = string.Empty;

    /// <summary>Full field name including declaring type.</summary>
    public string FullName { get; init; } = string.Empty;

    /// <summary>Fully qualified declaring type name.</summary>
    public string DeclaringTypeFullName { get; init; } = string.Empty;

    /// <summary>Simple field name (e.g., "_prefix").</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Field type name (e.g., "System.String").</summary>
    public string Type { get; init; } = string.Empty;

    /// <summary>Assembly name where this field is defined.</summary>
    public string AssemblyName { get; init; } = string.Empty;

    /// <summary>Whether the field is static.</summary>
    public bool IsStatic { get; init; }

    /// <summary>Whether the field has a compile-time literal value.</summary>
    public bool IsLiteral { get; init; }

    /// <summary>Literal value if IsLiteral is true.</summary>
    public object? LiteralValue { get; init; }
}
