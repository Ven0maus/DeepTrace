namespace DeepTrace.Core.Interfaces;

public interface IDecompiler
{
    string DecompileMethod(string assemblyPath, string typeFullName, string methodSignature);
    string? TryDecompileMethod(string assemblyPath, string typeFullName, string methodSignature);
    IReadOnlyList<string> DecompileType(string assemblyPath, string typeFullName);
}
