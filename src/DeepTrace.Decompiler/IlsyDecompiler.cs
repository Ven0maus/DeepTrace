using DeepTrace.Core.Interfaces;

namespace DeepTrace.Decompiler;

/// <summary>
/// Stub decompiler. Method body extraction deferred to future milestone.
/// Returns empty bodies; metadata still indexed via Mono.Cecil.
/// </summary>
public sealed class IlsyDecompiler : IDecompiler
{
    public string DecompileMethod(string assemblyPath, string typeFullName, string methodSignature)
        => string.Empty;

    public string? TryDecompileMethod(string assemblyPath, string typeFullName, string methodSignature)
        => string.Empty;

    public IReadOnlyList<string> DecompileType(string assemblyPath, string typeFullName)
        => Array.Empty<string>();
}
