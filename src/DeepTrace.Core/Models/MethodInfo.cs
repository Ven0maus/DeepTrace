using System.Collections.Generic;

namespace DeepTrace.Core.Models;

/// <summary>
/// Metadata for a single method extracted from a decompiled assembly.
/// </summary>
public sealed class MethodInfo
{
    /// <summary>Unique symbol identifier in format "Assembly::Namespace.Type::Method::Signature".</summary>
    public string SymbolId { get; set; } = string.Empty;

    /// <summary>Full method name including declaring type (e.g., "TestLib.Greeter.Greet").</summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>Fully qualified declaring type name.</summary>
    public string DeclaringTypeFullName { get; set; } = string.Empty;

    /// <summary>Simple method name (e.g., "Greet").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Method signature including parameter and return types.</summary>
    public string Signature { get; set; } = string.Empty;

    /// <summary>Return type (e.g., "System.String"), or null for void.</summary>
    public string? ReturnType { get; set; }

    /// <summary>List of parameter type names (e.g., "System.String").</summary>
    public IReadOnlyList<string> Parameters { get; set; } = new List<string>();

    /// <summary>Custom attributes applied to the method.</summary>
    public IReadOnlyList<string> Attributes { get; set; } = new List<string>();

    /// <summary>Decompiled C# source body, or null if not available.</summary>
    public string? Body { get; set; }

    /// <summary>Source location in the original assembly, if available.</summary>
    public SourceLocation? SourceLocation { get; set; }

    /// <summary>Assembly name where this method is defined.</summary>
    public string AssemblyName { get; set; } = string.Empty;

    /// <summary>Whether the method is static.</summary>
    public bool IsStatic { get; set; }

    /// <summary>Whether the method is virtual.</summary>
    public bool IsVirtual { get; set; }

    /// <summary>Whether the method is abstract.</summary>
    public bool IsAbstract { get; set; }

    /// <summary>Whether the method is an override.</summary>
    public bool IsOverride { get; set; }

    /// <summary>Heuristic-generated summary of the method's purpose.</summary>
    public string? Summary { get; set; }
}
