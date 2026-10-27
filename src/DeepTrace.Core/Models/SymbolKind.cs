namespace DeepTrace.Core.Models;

/// <summary>
/// Kind of symbol in the indexed assembly metadata.
/// </summary>
public enum SymbolKind
{
    /// <summary>Assembly-level symbol.</summary>
    Assembly,

    /// <summary>Namespace-level symbol.</summary>
    Namespace,

    /// <summary>Type (class, struct, enum, interface, delegate).</summary>
    Type,

    /// <summary>Method (including constructors and finalizers).</summary>
    Method,

    /// <summary>Field (including static and readonly fields).</summary>
    Field,

    /// <summary>Property (including get/set accessors).</summary>
    Property,

    /// <summary>Event.</summary>
    Event,

    /// <summary>Constructor (.ctor or .cctor).</summary>
    Constructor
}
