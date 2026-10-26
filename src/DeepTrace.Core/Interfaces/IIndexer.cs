using DeepTrace.Core.Models;
using DeepTrace.Core.Runtime;

namespace DeepTrace.Core.Interfaces;

public interface IIndexer
{
    void IndexAssembly(AssemblyInfo assembly);
    void IndexAssemblyAsync(AssemblyInfo assembly, CancellationToken cancellationToken = default);
    IReadOnlyList<TypeInfo> GetTypes(string assemblyName);
    TypeInfo? GetTypeInfo(string assemblyName, string typeFullName);
    MethodInfo? GetMethod(string symbolId);
    IReadOnlyList<MethodInfo> GetMethods(string assemblyName);
    IReadOnlyList<SearchResult> Search(string query, int limit = 50);
    IReadOnlyList<string> GetCallers(string methodSymbolId);
    IReadOnlyList<string> GetCallees(string methodSymbolId);
    IReadOnlyList<SearchResult> SearchReferences(string symbolId);
    IReadOnlyList<string> GetDerivedTypes(string typeSymbolId);
    IReadOnlyList<string> GetBaseTypes(string typeSymbolId);
    bool IsAssemblyIndexed(string assemblyName);
    long GetAssemblyLastWriteTime(string assemblyPath);
    string GetAssemblyHash(string assemblyPath);
    void StoreRuntimeObservation(RuntimeObservation observation);
    IReadOnlyList<RuntimeObservation> GetRuntimeObservations(string symbolId);
    IReadOnlyList<RuntimeObservation> GetAllRuntimeObservations();
}
