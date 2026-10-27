# DeepTrace API Reference

This document describes all public types, interfaces, and methods exposed by the DeepTrace libraries.

## Core Library (DeepTrace.Core)

### Interfaces

#### `IIndexer`

Primary interface for indexing and querying decompiled .NET assemblies.

| Method | Return Type | Description |
|--------|-------------|-------------|
| `IndexAssembly(AssemblyInfo)` | `void` | Index a single assembly — extracts metadata, types, methods, fields, and relationships. |
| `IndexAssemblyAsync(AssemblyInfo, CancellationToken)` | `void` | Async version with cancellation support. |
| `IndexAssemblies(IReadOnlyList<AssemblyInfo>)` | `void` | Index multiple assemblies in a single batch using shared connection and transaction. |
| `RebuildLsaEmbeddings()` | `void` | Rebuild LSA embeddings from all indexed method bodies. Call once after all assemblies are indexed. |
| `GetTypes(string assemblyName)` | `IReadOnlyList<TypeInfo>` | List all types in an assembly. |
| `GetTypeInfo(string assemblyName, string typeFullName)` | `TypeInfo?` | Get detailed type info including methods, fields, properties, and relationships. |
| `GetMethod(string symbolId)` | `MethodInfo?` | Get full method details by symbol ID. |
| `GetMethods(string assemblyName)` | `IReadOnlyList<MethodInfo>` | List all methods in an assembly. |
| `Search(string query, int limit)` | `IReadOnlyList<SearchResult>` | Search indexed symbols by keyword. Results ranked by BM25 relevance. Default limit: 50. |
| `GetCallers(string methodSymbolId)` | `IReadOnlyList<string>` | Find direct callers of a method. |
| `GetCallees(string methodSymbolId)` | `IReadOnlyList<string>` | Find direct callees of a method. |
| `GetCallGraphEdges(string methodSymbolId)` | `IReadOnlyList<CallGraphEdge>` | Get enriched call graph edges for a method (direct, virtual, delegate, event). |
| `GetCallGraphEdgesByType(string methodSymbolId, string edgeType)` | `IReadOnlyList<CallGraphEdge>` | Get call graph edges filtered by type. |
| `GetInterfaceImplementations(string interfaceName, string methodName)` | `IReadOnlyList<string>` | Get concrete implementations of an interface method. |
| `GetEngineCallbacks(string typeSymbolId)` | `IReadOnlyList<string>` | Get detected Unity/Unreal engine lifecycle callbacks for a type. |
| `SearchReferences(string symbolId)` | `IReadOnlyList<SearchResult>` | Find all references to a symbol (type refs, callers, callees). |
| `GetDerivedTypes(string typeSymbolId)` | `IReadOnlyList<string>` | Find types that derive from the specified type. |
| `GetBaseTypes(string typeSymbolId)` | `IReadOnlyList<string>` | Find base types of the specified type. |
| `IsAssemblyIndexed(string assemblyName)` | `bool` | Check whether an assembly has been indexed. |
| `GetAssemblyLastWriteTime(string assemblyPath)` | `long` | Get last write time as .NET ticks. |
| `GetAssemblyHash(string assemblyPath)` | `string` | Compute SHA256 hash of an assembly file. |
| `StoreRuntimeObservation(RuntimeObservation)` | `void` | Store a single runtime observation correlated with an indexed symbol. |
| `ImportRuntimeObservations(string filePath, out int count)` | `void` | Bulk import runtime observations from a JSON file. Returns count of imported observations. |
| `GetRuntimeObservations(string symbolId)` | `IReadOnlyList<RuntimeObservation>` | Get runtime observations for a specific symbol. |
| `GetAllRuntimeObservations()` | `IReadOnlyList<RuntimeObservation>` | Get all stored runtime observations. |

**Symbol ID format:** `{AssemblyName}::{Namespace.Type}::{MethodName}::{Signature}`  
**Type symbol ID format:** `{AssemblyName}::{Namespace.Type}`

#### `IDecompiler`

Interface for decompiling .NET assemblies back to C# source code.

| Method | Return Type | Description |
|--------|-------------|-------------|
| `DecompileMethod(string assemblyPath, string typeFullName, string methodSignature)` | `string` | Decompile a single method to C# source. |
| `TryDecompileMethod(string assemblyPath, string typeFullName, string methodSignature)` | `string?` | Try to decompile a method; returns null if not found. |
| `DecompileType(string assemblyPath, string typeFullName)` | `IReadOnlyList<string>` | Decompile an entire type to source lines. |

#### `IAssemblyDiscoverer`

Discovers managed .NET assemblies in a directory.

| Method | Return Type | Description |
|--------|-------------|-------------|
| `Discover(string directory)` | `IReadOnlyList<AssemblyInfo>` | Discover all managed .NET assemblies recursively. |

#### `IRuntimeObserver`

Observes method calls at runtime and correlates them with indexed symbols.

| Method | Return Type | Description |
|--------|-------------|-------------|
| `RecordCall(string symbolId, string fullyQualifiedName, string assembly, string? parameters, string? returnValue, double durationMs, int depth)` | `void` | Record an observed method call. |
| `GetObservations(string symbolId)` | `IReadOnlyList<RuntimeObservation>` | Get observations for a symbol. |
| `GetAllObservations()` | `IReadOnlyList<RuntimeObservation>` | Get all observations. |
| `Clear()` | `void` | Clear all stored observations. |
| `Count` | `int` | Count of stored observations. |

### Models

#### `AssemblyInfo` (record)

Metadata for a discovered managed .NET assembly.

| Property | Type | Description |
|----------|------|-------------|
| `Name` | `string` | Assembly name (e.g., "TestLib"). |
| `Location` | `string` | Full file path to the assembly DLL. |
| `LastWriteTimeUtcTicks` | `long` | Last write time as .NET ticks. |
| `Hash` | `string` | SHA256 hash of the assembly file. |
| `IndexedAt` | `DateTime` | Timestamp when this assembly was last indexed. |

#### `MethodInfo`

Metadata for a single method extracted from a decompiled assembly.

| Property | Type | Description |
|----------|------|-------------|
| `SymbolId` | `string` | Unique symbol ID (`Assembly::Namespace.Type::Method::Signature`). |
| `FullName` | `string` | Full method name including declaring type. |
| `DeclaringTypeFullName` | `string` | Fully qualified declaring type name. |
| `Name` | `string` | Simple method name (e.g., "Greet"). |
| `Signature` | `string` | Method signature including parameter and return types. |
| `ReturnType` | `string?` | Return type (e.g., "System.String"), or null for void. |
| `Parameters` | `IReadOnlyList<string>` | List of parameter type names. |
| `Attributes` | `IReadOnlyList<string>` | Custom attributes applied to the method. |
| `Body` | `string?` | Decompiled C# source body, or null if not available. |
| `SourceLocation` | `SourceLocation?` | Source location in the original assembly. |
| `AssemblyName` | `string` | Assembly name where this method is defined. |
| `IsStatic` | `bool` | Whether the method is static. |
| `IsVirtual` | `bool` | Whether the method is virtual. |
| `IsAbstract` | `bool` | Whether the method is abstract. |
| `IsOverride` | `bool` | Whether the method is an override. |
| `Summary` | `string?` | Heuristic-generated summary of the method's purpose. |

#### `TypeInfo`

Metadata for a single type extracted from a decompiled assembly.

| Property | Type | Description |
|----------|------|-------------|
| `SymbolId` | `string` | Unique symbol ID (`Assembly::Namespace.Type`). |
| `FullName` | `string` | Fully qualified type name. |
| `Namespace` | `string` | Namespace (e.g., "TestLib"). |
| `Name` | `string` | Simple type name (e.g., "Greeter"). |
| `BaseTypeFullName` | `string?` | Direct base type name, or null for object types. |
| `AssemblyName` | `string` | Assembly name where this type is defined. |
| `Interfaces` | `IReadOnlyList<string>` | Interfaces implemented by this type. |
| `Attributes` | `IReadOnlyList<string>` | Custom attributes applied to the type. |
| `Methods` | `IReadOnlyList<MethodInfo>` | Methods defined in this type. |
| `Fields` | `IReadOnlyList<FieldInfo>` | Fields defined in this type. |
| `Properties` | `IReadOnlyList<PropertyInfo>` | Properties defined in this type. |
| `DerivedTypes` | `IReadOnlyList<string>` | Types that directly derive from this type. |
| `BaseTypes` | `IReadOnlyList<string>` | Base types in the inheritance chain (excluding object). |
| `TypeReferences` | `IReadOnlyList<string>` | Other types referenced by this type. |

#### `SearchResult`

A single search result from BM25-ranked symbol search.

| Property | Type | Description |
|----------|------|-------------|
| `SymbolId` | `string` | Unique symbol identifier. |
| `Name` | `string` | Simple name of the symbol. |
| `Kind` | `SymbolKind` | Kind of symbol (Method, Type, Field, Property, Assembly). |
| `FullyQualifiedName` | `string` | Fully qualified name. |
| `Signature` | `string?` | Signature including parameter and return types. |
| `Assembly` | `string` | Assembly name. |
| `SourceLocation` | `SourceLocation?` | Source location. |
| `Excerpt` | `string?` | Relevant excerpt from body or source. |
| `RelevanceScore` | `double` | BM25 relevance score (higher = more relevant). |
| `RelevanceExplanation` | `string?` | Why this result matched. |

#### `SymbolKind` (enum)

| Value | Description |
|-------|-------------|
| `Assembly` | Assembly-level symbol. |
| `Namespace` | Namespace-level symbol. |
| `Type` | Type (class, struct, enum, interface, delegate). |
| `Method` | Method (including constructors and finalizers). |
| `Field` | Field. |
| `Property` | Property. |
| `Event` | Event. |
| `Constructor` | Constructor (.ctor or .cctor). |

#### `FieldInfo`

Metadata for a single field extracted from a decompiled assembly.

| Property | Type | Description |
|----------|------|-------------|
| `SymbolId` | `string` | Unique symbol identifier. |
| `FullName` | `string` | Full field name including declaring type. |
| `DeclaringTypeFullName` | `string` | Fully qualified declaring type name. |
| `Name` | `string` | Simple field name. |
| `Type` | `string` | Field type name. |
| `AssemblyName` | `string` | Assembly name. |
| `IsStatic` | `bool` | Whether the field is static. |
| `IsLiteral` | `bool` | Whether the field has a compile-time literal value. |
| `LiteralValue` | `object?` | Literal value if `IsLiteral` is true. |

#### `PropertyInfo`

Metadata for a single property extracted from a decompiled assembly.

#### `CallGraphEdge` (record)

A single edge in the enriched call graph.

| Property | Type | Description |
|----------|------|-------------|
| `CallerId` | `string` | Symbol ID of the calling method. |
| `CalleeId` | `string` | Symbol ID of the called method. |
| `EdgeType` | `string` | Type of call: `direct`, `virtual`, `delegate`, `event_subscription`, `event_unsubscription`. |

#### `PropertyInfo`

| Property | Type | Description |
|----------|------|-------------|
| `SymbolId` | `string` | Unique symbol identifier. |
| `FullName` | `string` | Full property name including declaring type. |
| `DeclaringTypeFullName` | `string` | Fully qualified declaring type name. |
| `Name` | `string` | Simple property name. |
| `Type` | `string` | Property type name. |
| `AssemblyName` | `string` | Assembly name. |
| `IsStatic` | `bool` | Whether the property is static. |

#### `SourceLocation` (record)

Source location within a decompiled assembly file.

| Parameter | Type | Description |
|-----------|------|-------------|
| `FilePath` | `string?` | Path to the source file, or null. |
| `StartLine` | `int` | Starting line number (1-based). |
| `StartColumn` | `int` | Starting column number (1-based). |
| `EndLine` | `int?` | Ending line number, or null for single-line. |
| `EndColumn` | `int?` | Ending column number, or null for single-line. |

#### `RuntimeObservation`

A single observed method call during runtime execution.

| Property | Type | Description |
|----------|------|-------------|
| `Id` | `string` | Unique identifier. |
| `SymbolId` | `string` | Indexed symbol ID. |
| `FullyQualifiedName` | `string` | Fully qualified method name. |
| `Assembly` | `string` | Assembly name. |
| `Timestamp` | `long` | Unix millisecond timestamp. |
| `Depth` | `int` | Call depth in the invocation chain (0 = top-level). |
| `Parameters` | `string?` | JSON-serialized method parameters. |
| `ReturnValue` | `string?` | JSON-serialized return value or exception. |
| `DurationMs` | `double` | Duration in milliseconds. |

#### `InvestigationReport`

Evidence-based investigation report for AI-assisted game mod development.

| Property | Type | Description |
|----------|------|-------------|
| `RequestInterpretation` | `string` | Human interpretation of the original request. |
| `DiscoveredExecutionPath` | `IReadOnlyList<EvidenceItem>` | Methods discovered through static analysis. |
| `ObservedExecutionPath` | `IReadOnlyList<ObservedExecutionItem>` | Methods confirmed by runtime observation. |
| `RequiredDependenciesAndInvariants` | `IReadOnlyList<DependencyItem>` | Dependencies and state invariants. |
| `CandidateExtensionPoints` | `IReadOnlyList<ExtensionPointItem>` | Candidate mod extension points. |
| `EvidenceAndConfidence` | `IReadOnlyList<ConfidenceItem>` | Confidence assessments for findings. |
| `ImplementationPlan` | `string` | Synthesized implementation plan. |
| `ValidationPlan` | `string` | Synthesized validation plan. |

#### `InvestigationOptions`

Configuration for an investigation run.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `MaxSearches` | `int` | 8 | Maximum number of SearchCode calls. |
| `MaxDepth` | `int` | 2 | Maximum traversal depth for callers/callees. |
| `ResultLimit` | `int` | 20 | Result limit per search call. |
| `TokenBudget` | `int` | 12000 | Approximate character budget (~300 chars per token unit). |

#### `InvestigationRecipe`

A predefined investigation recipe — a structured sequence of searches and analyses.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Name` | `string` | — | Human-readable name of the recipe. |
| `Description` | `string` | — | Description of what the recipe finds. |
| `Queries` | `IReadOnlyList<string>` | — | Search queries to run in order. |
| `AssemblyFilter` | `string?` | — | Optional assembly filter. |
| `NamespaceFilter` | `string?` | — | Optional namespace filter. |
| `KindFilter` | `string?` | — | Optional kind filter (e.g., "Method", "Type"). |
| `ResultLimit` | `int` | 20 | Max results per query. |

#### `QueryEvaluation`

Evaluation result for a single query.

| Property | Type | Description |
|----------|------|-------------|
| `Query` | `string` | The search query. |
| `ExpectedResults` | `IReadOnlyList<string>` | Expected symbol IDs (gold standard). |
| `RetrievedResults` | `IReadOnlyList<SearchResult>` | Actually retrieved results. |
| `Precision` | `int` | Precision percentage (0-100). |
| `Recall` | `int` | Recall percentage (0-100). |
| `F1` | `int` | F1 score (0-100). |
| `RelevantRetrieved` | `int` | Number of relevant results retrieved. |
| `TotalRetrieved` | `int` | Total number of results retrieved. |
| `TotalRelevant` | `int` | Total number of relevant results available. |

#### `EvaluationReport`

Aggregated evaluation metrics.

| Property | Type | Description |
|----------|------|-------------|
| `PerQueryResults` | `IReadOnlyList<QueryEvaluation>` | Per-query evaluation results. |
| `AveragePrecision` | `double` | Average precision across all queries. |
| `AverageRecall` | `double` | Average recall across all queries. |
| `AverageF1` | `double` | Average F1 score across all queries. |
| `TotalQueries` | `int` | Total number of queries evaluated. |
| `TotalRelevantRetrieved` | `int` | Total relevant results retrieved. |
| `TotalRelevantAvailable` | `int` | Total relevant results available. |

### AI Namespace

#### `InvestigationWorkflow`

Iterative investigation workflow that explores code via structured tool calls.

| Method | Return Type | Description |
|--------|-------------|-------------|
| `Investigate(string request, InvestigationOptions?)` | `InvestigationReport` | Run the full iterative investigation workflow. |

**Workflow phases:**
1. Extract concepts from the request
2. Search for relevant methods by each concept (bounded by `MaxSearches`)
3. Retrieve full method bodies for top candidates
4. Check runtime observations for discovered methods
5. Explore caller/callee graphs (bounded by `MaxDepth`)
6. Extract dependencies and extension points
7. Build confidence assessments
8. Produce structured report

#### `InvestigationTools`

Exposes indexed code data as structured text for LLM consumption.

| Method | Return Type | Description |
|--------|-------------|-------------|
| `SearchCode(string query, string? assemblyFilter, string? namespaceFilter, string? kindFilter, int limit)` | `string` | Search indexed symbols with optional filters. |
| `GetMethod(string symbolId)` | `string` | Get full method details. |
| `GetCallers(string methodSymbolId, int depth, int limit)` | `string` | Find callers with optional recursion depth. |
| `GetCallees(string methodSymbolId, int depth, int limit)` | `string` | Find callees with optional recursion depth. |
| `SearchReferences(string symbolId, int limit)` | `string` | Find all references to a symbol. |
| `GetRelatedMethods(string methodSymbolId, int limit)` | `string` | Find methods in the same type. |
| `GetRuntimeObservations(string symbolId)` | `string` | Get runtime observations for a symbol. |

#### `InvestigationRecipes`

Predefined investigation recipes for common modding tasks.

| Method | Return Type | Description |
|--------|-------------|-------------|
| `Recipes` | `IReadOnlyList<InvestigationRecipe>` | All pre-defined recipes. |
| `Execute(InvestigationRecipe, IIndexer)` | `IReadOnlyList<SearchResult>` | Run a recipe and return combined results. |
| `ExecuteAsText(InvestigationRecipe, IIndexer)` | `string` | Run a recipe and return formatted text report. |

**Pre-defined recipes:**
- **Find Extension Points** — virtual methods, overrides, public instance methods (HarmonyX patch targets)
- **Trace Initialization** — constructors, static initializers, On* lifecycle methods
- **Find Service Registrations** — factory methods, Create/Build/From patterns, DI registrations
- **Find Type Hierarchies** — base types, interfaces, derived type relationships
- **Find Event Handlers** — event subscriptions, callbacks, handler methods

#### `RetrievalEvaluator`

Evaluates retrieval quality by comparing search results against known-gold-standard results.

| Method | Return Type | Description |
|--------|-------------|-------------|
| `Evaluate(IReadOnlyList<(string, IReadOnlyList<string>)>, IIndexer, int)` | `EvaluationReport` | Evaluate search quality for queries with known relevant results. |
| `FormatReport(EvaluationReport)` | `string` | Format evaluation report as text. |

### Runtime Namespace

#### `InMemoryRuntimeObserver`

In-memory runtime observer for testing and local instrumentation. Not persisted across process restarts.

| Property/Method | Return Type | Description |
|-----------------|-------------|-------------|
| `Count` | `int` | Count of stored observations. |
| `RecordCall(...)` | `void` | Record an observed method call. |
| `GetObservations(string)` | `IReadOnlyList<RuntimeObservation>` | Get observations for a symbol. |
| `GetAllObservations()` | `IReadOnlyList<RuntimeObservation>` | Get all observations. |
| `Clear()` | `void` | Clear all stored observations. |

## Indexing Library (DeepTrace.Indexing)

### `SqliteIndexer`

SQLite-backed indexer for decompiled .NET assemblies. Uses FTS5 for full-text search and stores metadata, relationships, and runtime observations.

Implements: `IIndexer`, `IDisposable`

All methods documented under `IIndexer` above.

### `AssemblyDiscoverer`

Discovers managed .NET assemblies in a directory, excluding system and framework assemblies.

Implements: `IAssemblyDiscoverer`

| Method | Return Type | Description |
|--------|-------------|-------------|
| `Discover(string directory)` | `IReadOnlyList<AssemblyInfo>` | Discover all managed .NET assemblies recursively. |

Excluded assemblies include: System.*, Microsoft.*, Windows.*, netstandard, System.Core, System.Runtime, System.Linq, System.Threading, System.Data, System.Xml, ICSharpCode, Mono.Cecil, and others.

### `Bm25Scorer`

BM25-inspired relevance scoring for search results. Tokenization filters English stopwords and preserves single-char tokens.

| Method | Return Type | Description |
|--------|-------------|-------------|
| `Score(SearchResult result, string query)` | `double` | Score a search result against a query. |
| `ScoreSummary(string? summary, string query)` | `double` | Score a summary against a query. |

Parameters: K1=1.5, B=0.75. Exact name match: +10.0, prefix match: +3.0.

### `LsaEmbedder`

Latent Semantic Analysis engine. Builds TF-IDF term-document matrix from method bodies, extracts top-K latent dimensions via power-iteration SVD, and projects queries/documents into the same latent space for cosine similarity scoring.

| Method | Return Type | Description |
|--------|-------------|-------------|
| `Tokenize(string text)` | `string[]` | Tokenize text. Filters stopwords, preserves single-char tokens. |
| `BuildAndStore(string dbPath, IReadOnlyList<(string, string)>)` | `void` | Build TF-IDF matrix, run power-iteration SVD, store embeddings/vocab/components/IDF in SQLite. |
| `LoadStored(string dbPath)` | `IReadOnlyDictionary<string, double[]>` | Load per-method embedding vectors. |
| `LoadComponents(string dbPath)` | `double[,]` | Load principal components matrix (k x n). |
| `LoadVocabulary(string dbPath)` | `IReadOnlyDictionary<string, int>` | Load term-to-index mapping. |
| `LoadIdf(string dbPath)` | `IReadOnlyDictionary<string, double>` | Load per-term IDF values. |
| `ComputeQueryEmbedding(string, IReadOnlyDictionary<string, int>, double[,], IReadOnlyDictionary<string, double>?)` | `double[]` | Project query into latent space with IDF weighting. |
| `CosineSimilarity(double[], double[])` | `double` | Cosine similarity between two vectors. |

Configuration: `Dimensions = 50` (latent space size). Token cache: LRU, max 4096 entries. Power iteration: 30 max iterations with early stop at convergence (change < 0.0001), Gram-Schmidt orthogonalization per component, 2 re-convergence iterations after orthogonalization.

### `MethodSummaryGenerator`

Generates human-readable summaries of method purpose from metadata and body text.

Uses heuristic analysis of method name, parameters, body content, and lifecycle patterns.

## Decompiler Library (DeepTrace.Decompiler)

### `IlspyDecompiler`

ILSpy-based decompiler. Extracts C# source from .NET assemblies using ICSharpCode.Decompiler.

Implements: `IDecompiler`, `IDisposable`

All methods documented under `IDecompiler` above.

## Confidence Levels

| Level | Meaning |
|-------|---------|
| `High` | Strong evidence, low uncertainty |
| `Medium` | Moderate evidence, some uncertainty |
| `Low` | Weak evidence, high uncertainty |

## Evidence Sources

| Source | Description |
|--------|-------------|
| `StaticAnalysis` | Derived from code metadata and body inspection |
| `Runtime` | Confirmed by runtime observation |
| `Inference` | Synthesized by the workflow |
