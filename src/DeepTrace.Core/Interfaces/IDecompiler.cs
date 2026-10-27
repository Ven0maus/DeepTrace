namespace DeepTrace.Core.Interfaces;

/// <summary>
/// Interface for decompiling .NET assemblies back to source code.
/// </summary>
public interface IDecompiler
{
    /// <summary>
    /// Decompile a single method to C# source code.
    /// </summary>
    /// <param name="assemblyPath">Path to the assembly file.</param>
    /// <param name="typeFullName">Fully qualified type name containing the method.</param>
    /// <param name="methodSignature">Method signature (e.g., "MethodName(System.String) : System.String").</param>
    string DecompileMethod(string assemblyPath, string typeFullName, string methodSignature);

    /// <summary>
    /// Try to decompile a method; returns null if the method is not found.
    /// </summary>
    string? TryDecompileMethod(string assemblyPath, string typeFullName, string methodSignature);

    /// <summary>
    /// Decompile an entire type (all methods and fields) to a list of source lines.
    /// </summary>
    /// <param name="assemblyPath">Path to the assembly file.</param>
    /// <param name="typeFullName">Fully qualified type name.</param>
    IReadOnlyList<string> DecompileType(string assemblyPath, string typeFullName);
}
