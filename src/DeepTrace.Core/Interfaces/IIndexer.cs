using DeepTrace.Core.Models;
using DeepTrace.Core.Runtime;

namespace DeepTrace.Core.Interfaces;

/// <summary>
/// Primary interface for indexing and querying decompiled .NET assemblies.
/// </summary>
public interface IIndexer
{
    /// <summary>
    /// Index a single assembly — extracts metadata, types, methods, fields, and relationships.
    /// </summary>
    /// <param name="assembly">Assembly metadata obtained from DiscoverAssemblies.</param>
    void IndexAssembly(AssemblyInfo assembly);

    /// <summary>
    /// Async version of IndexAssembly with cancellation support.
    /// </summary>
    void IndexAssemblyAsync(AssemblyInfo assembly, CancellationToken cancellationToken = default);

    /// <summary>
    /// List all types in an assembly.
    /// </summary>
    /// <param name="assemblyName">Assembly name (e.g., "TestLib").</param>
    IReadOnlyList<TypeInfo> GetTypes(string assemblyName);

    /// <summary>
    /// Get detailed type information including methods, fields, properties, and relationships.
    /// </summary>
    /// <param name="assemblyName">Assembly name.</param>
    /// <param name="typeFullName">Fully qualified type name (e.g., "TestLib.Greeter").</param>
    TypeInfo? GetTypeInfo(string assemblyName, string typeFullName);

    /// <summary>
    /// Get full method details by symbol ID.
    /// </summary>
    /// <param name="symbolId">Symbol ID in format "Assembly::Namespace.Type::Method::Signature".</param>
    MethodInfo? GetMethod(string symbolId);

    /// <summary>
    /// List all methods in an assembly.
    /// </summary>
    /// <param name="assemblyName">Assembly name.</param>
    IReadOnlyList<MethodInfo> GetMethods(string assemblyName);

    /// <summary>
    /// Search indexed symbols by keyword or phrase. Results are ranked by BM25 relevance score.
    /// </summary>
    /// <param name="query">Search query string.</param>
    /// <param name="limit">Maximum number of results (default 50).</param>
    IReadOnlyList<SearchResult> Search(string query, int limit = 50);

    /// <summary>
    /// Find methods that call the specified method (direct callers only).
    /// </summary>
    /// <param name="methodSymbolId">Symbol ID of the callee method.</param>
    IReadOnlyList<string> GetCallers(string methodSymbolId);

    /// <summary>
    /// Find methods called by the specified method (direct callees only).
    /// </summary>
    /// <param name="methodSymbolId">Symbol ID of the caller method.</param>
    IReadOnlyList<string> GetCallees(string methodSymbolId);

    /// <summary>
    /// Find all references to a symbol (type references, method callers, method callees).
    /// </summary>
    /// <param name="symbolId">Symbol ID to search for.</param>
    IReadOnlyList<SearchResult> SearchReferences(string symbolId);

    /// <summary>
    /// Find types that derive from the specified type.
    /// </summary>
    /// <param name="typeSymbolId">Symbol ID of the base type (e.g., "Assembly::Namespace.Type").</param>
    IReadOnlyList<string> GetDerivedTypes(string typeSymbolId);

    /// <summary>
    /// Find base types of the specified type.
    /// </summary>
    /// <param name="typeSymbolId">Symbol ID of the type (e.g., "Assembly::Namespace.Type").</param>
    IReadOnlyList<string> GetBaseTypes(string typeSymbolId);

    /// <summary>
    /// Check whether an assembly has been indexed.
    /// </summary>
    /// <param name="assemblyName">Assembly name.</param>
    bool IsAssemblyIndexed(string assemblyName);

    /// <summary>
    /// Get the last write time of an assembly file as Unix ticks.
    /// </summary>
    /// <param name="assemblyPath">Full file path to the assembly.</param>
    long GetAssemblyLastWriteTime(string assemblyPath);

    /// <summary>
    /// Compute SHA256 hash of an assembly file.
    /// </summary>
    /// <param name="assemblyPath">Full file path to the assembly.</param>
    string GetAssemblyHash(string assemblyPath);

    /// <summary>
    /// Store a runtime observation correlated with an indexed symbol.
    /// </summary>
    void StoreRuntimeObservation(RuntimeObservation observation);

    /// <summary>
    /// Get runtime observations for a specific symbol.
    /// </summary>
    /// <param name="symbolId">Indexed symbol ID.</param>
    IReadOnlyList<RuntimeObservation> GetRuntimeObservations(string symbolId);

    /// <summary>
    /// Get all stored runtime observations.
    /// </summary>
    IReadOnlyList<RuntimeObservation> GetAllRuntimeObservations();

    /// <summary>
    /// Get all call graph edges for a method (both as caller and callee).
    /// </summary>
    /// <param name="methodSymbolId">Symbol ID of the method.</param>
    IReadOnlyList<CallGraphEdge> GetCallGraphEdges(string methodSymbolId);

    /// <summary>
    /// Get call graph edges filtered by type.
    /// </summary>
    /// <param name="methodSymbolId">Symbol ID of the method.</param>
    /// <param name="edgeType">Edge type filter (direct, virtual, delegate, event_subscription, event_unsubscription).</param>
    IReadOnlyList<CallGraphEdge> GetCallGraphEdgesByType(string methodSymbolId, string edgeType);

    /// <summary>
    /// Get concrete implementations of an interface method.
    /// </summary>
    /// <param name="interfaceName">Fully qualified interface name.</param>
    /// <param name="methodName">Method name on the interface.</param>
    IReadOnlyList<string> GetInterfaceImplementations(string interfaceName, string methodName);

    /// <summary>
    /// Get engine callback methods detected by convention for a type.
    /// </summary>
    /// <param name="typeSymbolId">Symbol ID of the type (e.g., "Assembly::Namespace.Type").</param>
    IReadOnlyList<string> GetEngineCallbacks(string typeSymbolId);

    /// <summary>
    /// Set additional search directories for Unity managed assemblies.
    /// Used to resolve UnityEngine dependencies during assembly loading.
    /// </summary>
    /// <param name="managedDirectories">Paths to Unity managed assembly directories.</param>
    void SetUnityManagedDirectories(params string[] managedDirectories);
}
