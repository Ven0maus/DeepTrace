# AGENTS.md

## Project Overview

Local code intelligence system for AI-assisted game mod development. Targets decompiled .NET game assemblies and BepInEx-based mods.

Long-term goal: enable AI to understand game architecture, discover gameplay systems, trace execution paths, implement compatible mod features using evidence from actual game code.

Example use case:

"Create an NPC using the same process the game uses to populate a city."

Discover existing NPC creation and initialization path before proposing implementation.

## Repository Structure

```
src/
  DeepTrace.Cli/          — CLI entry point (Program.cs)
  DeepTrace.Core/         — Core models, interfaces, AI workflow, runtime observer
  DeepTrace.Decompiler/   — ILSpy-based decompiler (IlspyDecompiler.cs)
  DeepTrace.Indexing/     — SQLite indexer, BM25 scorer, assembly discoverer, summary generator
tests/
  DeepTrace.Tests/        — 13 test files + nested BenchmarkLib + TestLib sub-projects
test-assets/                 — kept but currently empty
docs/
  API.md                  — Full API reference
  CLI.md                  — CLI command reference
skills/
  deeptrace-cli.md        — CLI skill for agent use
```

7 projects. 1 solution (DeepTrace.slnx). All target .NET 8.0 or .NET 10.0.

## Engineering Principles

1. **Evidence over assumptions.** Never invent game classes, methods, signatures, APIs, or execution paths. Every claim must be traceable to indexed data or runtime observation.
2. **Inspect before implementing.** Explore repository, dependencies, conventions, architecture before changes. Read existing files first.
3. **Smallest useful implementation.** Avoid unnecessary abstractions, dependencies, frameworks, distributed infrastructure. Reuse existing patterns.
4. **Preserve provenance.** Every code fragment and structural relationship traceable to source assembly and symbol. Use stable symbol IDs.
5. **Distinguish facts from inference.** Label inferred relationships, generated summaries, runtime observations. The investigation workflow already does this via `EvidenceAndConfidence` with `ConfidenceLevel` and `EvidenceSource`.
6. **Validate changes.** Build and test affected components when environment permits. Run `dotnet test` before claiming completion.
7. **Incremental development.** Complete useful, testable milestones over large speculative architecture.
8. **Local-first.** Game assemblies, indexes, caches, configuration remain local unless explicit feature requires otherwise.

## Technology Stack

- **C# and .NET** — All projects. Core targets net8.0, CLI targets net10.0.
- **Mono.Cecil** — Compiled assembly metadata and IL analysis. Read-only access to types, methods, fields, calls.
- **ILSpy / ICSharpCode.Decompiler** — C# source extraction from assemblies. Type-level and method-level decompilation with brace-counting method extraction.
- **SQLite + FTS5** — Persistent storage. Full-text search on symbols (name, signature, summary) and methods (name, declaring type, body, summary). Triggers keep FTS5 in sync.
- **BM25** — Relevance scoring for search results. K1=1.5, B=0.75. Exact name match: +10.0, prefix match: +3.0.
- **InMemoryRuntimeObserver** — In-memory runtime observation store. Not persisted across process restarts. Stores per-call parameters, return values, duration, depth.

Do not add a dependency solely because it appears in this list. Check compatibility, maintenance, licensing, real problem solved.

## Architecture

### Projects and Responsibilities

| Project | Responsibility |
|---------|---------------|
| `DeepTrace.Core` | Interfaces (`IIndexer`, `IDecompiler`, `IAssemblyDiscoverer`, `IRuntimeObserver`), models, AI investigation workflow, runtime observer |
| `DeepTrace.Indexing` | `SqliteIndexer` (implements `IIndexer`), `AssemblyDiscoverer`, `Bm25Scorer`, `MethodSummaryGenerator`, database schema |
| `DeepTrace.Decompiler` | `IlspyDecompiler` (implements `IDecompiler`), type/method decompilation with caching |
| `DeepTrace.Cli` | CLI entry point, command routing, output formatting |

### Database Schema

Tables: `assemblies`, `symbols`, `methods`, `fields`, `properties`, `type_interfaces`, `type_attributes`, `method_attributes`, `method_parameters`, `method_callers`, `method_callees`, `type_hierarchy`, `type_references`, `call_graph_edges`, `engine_callbacks`, `interface_implementations`, `runtime_observations`.

FTS5 virtual tables: `fts_symbols` (full_name, name, signature, summary, kind), `fts_methods` (name, declaring_type, body, summary). Triggers keep FTS5 in sync on INSERT/UPDATE/DELETE.

SQLite PRAGMAs: WAL journaling, NORMAL synchronous, MEMORY temp storage, 100k cache, 256MB memory mapping.

### Symbol ID Format

Method: `Assembly::Namespace.Type::Method::Signature`
Example: `TestLib::TestLib.Greeter::Greet::Greet(System.String) : System.String`

Type: `Assembly::Namespace.Type`
Example: `TestLib::TestLib.Greeter`

Call graph edges reference the same format. External methods (not in indexed assemblies) use `FullName::MethodName`.

### AI Investigation Workflow

The `InvestigationWorkflow` class executes these phases without an LLM:

1. **Concept extraction** — Split request into tokens, filter stop words, deduplicate.
2. **Search** — Run BM25 search for each concept (bounded by `MaxSearches`). Deduplicate by symbol ID.
3. **Method body fetch** — Retrieve full method details for top candidates (up to 30).
4. **Runtime correlation** — Check `InMemoryRuntimeObserver` for observed calls matching discovered methods.
5. **Graph traversal** — Explore callers and callees up to `MaxDepth` levels. Deduplicate with visited set.
6. **Dependency extraction** — Parse method bodies for constructor field assignments, `new` patterns, base calls.
7. **Extension point identification** — Mark virtual methods, overrides, public instance methods with meaningful bodies.
8. **Confidence assessment** — Assign `High`/`Medium`/`Low` confidence with `StaticAnalysis`/`Runtime`/`Inference` source.
9. **Report synthesis** — Produce `InvestigationReport` with execution path, dependencies, extension points, implementation plan, validation plan.

### Investigation Recipes

Pre-defined patterns (`InvestigationRecipes.Recipes`):

- **Find Extension Points** — virtual methods, overrides, public instance methods (HarmonyX patch targets)
- **Trace Initialization** — constructors, static initializers, On* lifecycle methods
- **Find Service Registrations** — factory methods, Create/Build/From patterns, DI registrations
- **Find Type Hierarchies** — base types, interfaces, derived type relationships
- **Find Event Handlers** — event subscriptions, callbacks, handler methods

Each recipe runs multiple queries, deduplicates results, and sorts by BM25 score.

### Runtime Observations

`InMemoryRuntimeObserver` records method calls with: symbol ID, fully qualified name, assembly, timestamp, call depth, JSON-serialized parameters, JSON-serialized return value, duration.

`SqliteIndexer` persists observations to `runtime_observations` table. CLI `observe` command stores a single observation. `runtime` command retrieves by symbol. Investigations load observations into `InMemoryRuntimeObserver` before running.

## Code Intelligence Capabilities

### Implemented

- Searching code by names, signatures, body text via BM25 ranking.
- Retrieving complete method bodies and source locations.
- Inspecting types, fields, properties, constructors, inheritance hierarchies.
- Finding callers, callees, symbol references (type refs, callers, callees combined).
- Tracing object creation, initialization, registration, lifecycle behavior.
- Identifying candidate mod extension points (virtual, override, public instance methods).
- Enriched call graph with edge types: `direct`, `virtual`, `delegate`, `event_subscription`, `event_unsubscription`.
- Convention-based engine callback detection (Unity: Awake/Start/Update/etc., Unreal: BeginPlay/Tick/EndPlay).
- Interface implementation mapping (explicit and implicit).
- AI investigation workflow (concept extraction, search, traversal, report synthesis).
- Runtime observation recording and correlation.
- Retrieval quality evaluation (precision/recall/F1 via `RetrievalEvaluator`).
- Investigation recipes for common modding tasks.

### Known Limitations

- Static call graphs incomplete with virtual dispatch, reflection, event systems, dynamic loading, engine callbacks.
- Decompiler extracts method bodies by brace-counting from type source. Edge cases with nested types or complex generics may produce incomplete extraction.
- `IlspyDecompiler` caches full type source per assembly path. Large assemblies consume memory.
- BM25 scoring is heuristic. No learned parameters. Exact name match gives highest weight.
- Runtime observer is in-memory only. No persistent instrumentation (BepInEx/HarmonyX hooks not implemented).
- No semantic search. Only keyword-based BM25 and text matching.
- No LLM integration yet. Investigation workflow is fully deterministic.

## CLI Commands

```bash
dotnet run --project src/DeepTrace.Cli -- [--db <path>] <command> [args...]
```

| Command | Description |
|---------|-------------|
| `discover <dir>` | Discover managed .NET assemblies recursively. Excludes System.*, Microsoft.*, Windows.*, framework assemblies. |
| `index <dir>` | Index all discovered assemblies. Skips unchanged assemblies by SHA256 hash. |
| `search <query> [limit]` | BM25-ranked symbol search. Default limit: 50. |
| `methods <assemblyName>` | List all methods in an assembly. |
| `method <symbolId>` | Get full method details including body, parameters, attributes. |
| `types <assemblyName>` | List all types in an assembly. |
| `type <assemblyName> <typeFullName>` | Get detailed type info with methods, fields, interfaces, hierarchy. |
| `callers <methodSymbolId>` | Find direct callers of a method. |
| `callees <methodSymbolId>` | Find direct callees of a method. |
| `references <symbolId>` | Find all references (type refs, callers, callees). |
| `call-graph <methodId> [edgeType]` | Show enriched call graph edges. Filter by type: direct, virtual, delegate, event_subscription. |
| `engine-callbacks <typeId>` | Show detected Unity/Unreal engine lifecycle callbacks. |
| `interface-implementations <iface> <method>` | Show concrete implementations of an interface method. |
| `investigate <question>` | Run full AI investigation workflow. Produces structured report. |
| `observe <symbolId> <durationMs> [params] [return]` | Record a runtime observation. |
| `runtime <symbolId>` | Show runtime observations for a symbol. |
| `recipes [name]` | List all recipes or execute a specific one. |
| `evaluate <dllPath>` | Measure retrieval quality (precision/recall/F1). |
| `stats` | Show indexer statistics. |

### Multi-game Workflow

```bash
dotnet run -- --db games/unity.db index C:/Games/UnityGame
dotnet run -- --db games/steamgame.db index C:/Games/SteamGame
dotnet run -- --db games/unity.db search "NPC"
```

## AI Investigation Workflow

When asked to implement gameplay feature, use the investigation workflow:

1. Decompose request into relevant game concepts.
2. Run `investigate <question>` or use `recipes` for known patterns.
3. Review the structured `InvestigationReport` output:
   - `DiscoveredExecutionPath` — methods found via static analysis
   - `ObservedExecutionPath` — methods confirmed by runtime observation
   - `RequiredDependenciesAndInvariants` — dependencies and state invariants
   - `CandidateExtensionPoints` — virtual/override/public methods for patching
   - `EvidenceAndConfidence` — confidence levels and evidence sources
   - `ImplementationPlan` — synthesized approach
   - `ValidationPlan` — test strategy
4. Inspect individual methods with `method <symbolId>` for full body text.
5. Follow callers/callees with `callers`/`callees` commands.
6. Check runtime observations with `runtime <symbolId>`.

Do not choose a method solely because its name resembles the requested feature.

Do not recommend HarmonyX patch before investigating existing API or safer extension point reuse.

If evidence insufficient, perform further targeted searches or explain unresolved questions.

## Code Quality

- Follow repository's existing naming, formatting, architectural conventions.
- Prefer clear, strongly typed APIs and explicit error handling.
- Avoid unnecessary global state and hidden side effects.
- Make long-running operations cancellable where practical.
- Use structured logging for operational diagnostics.
- Keep configuration explicit and game paths configurable.
- Use deterministic tests for pure logic.
- Test game-dependent behavior through integration tests when necessary.
- Avoid arbitrary execution of decompiled code.
- Do not silently swallow exceptions or report success on failure.

## Data Integrity and Safety

- Treat game assemblies as read-only inputs.
- Store generated source, indexes, summaries, caches separately from original game files.
- Never overwrite original assemblies during normal indexing.
- Avoid executing arbitrary game methods for inspection.
- Bound graph traversal, search results, log sizes, serialized object representations.
- Handle object cycles and exceptions during runtime inspection.
- Do not log arbitrary object graphs or sensitive values without explicit need.
- Make indexing operations recoverable after interruption.

## Development Workflow

Before changing code:

1. Inspect relevant files and existing implementation.
2. Identify smallest appropriate change.
3. Check affected dependencies and interfaces.
4. State important assumptions affecting design.

After changing code:

1. Build affected projects: `dotnet build DeepTrace.slnx`
2. Run relevant tests: `dotnet test tests/DeepTrace.Tests/DeepTrace.Tests.csproj`
3. Fix regressions introduced by change.
4. Report what was implemented and verified.
5. Explicitly identify remaining limitations and untested behavior.

Do not claim build or test passed unless actually executed successfully.

Do not replace working architecture or rewrite large repository portions without clear technical reason.

## Implementation Priorities

### Completed

1. Local assembly ingestion and method-level indexing (Mono.Cecil + SQLite).
2. Full-text search and exact source retrieval (BM25 + FTS5).
3. Type relationships, call graphs, reference analysis (hierarchy, callers/callees, edges).
4. AI-driven code investigation using structured tools (concept extraction, traversal, report synthesis).
5. Runtime observations through `InMemoryRuntimeObserver` + SQLite persistence.
6. Retrieval quality evaluation (precision/recall/F1).
7. Investigation recipes for common modding tasks.
8. Engine callback detection (Unity/Unreal conventions).
9. Interface implementation mapping.

### Not Yet Implemented

- Semantic retrieval (embedding-based search).
- BepInEx/HarmonyX runtime instrumentation hooks.
- LLM integration layer.
- Incremental indexing (hash-based skip for partially changed directories).
- Call graph visualization.
- Cross-assembly reference resolution.

## Testing

Test project: `tests/DeepTrace.Tests/DeepTrace.Tests.csproj`

| Test Class | Coverage |
|------------|----------|
| `IndexingTests` | Assembly discovery, type/method indexing, hash-based skip, BM25 search, method retrieval, null handling |
| `IlspyDecompilerSmokeTests` | Decompile method, type, tryDecompile, dispose |
| `Bm25ScorerTests` | Exact match, prefix match, full-text match, summary scoring |
| `InvestigationWorkflowTests` | Concept extraction, search, method body fetch, graph traversal, report synthesis |
| `InvestigationWorkflowRuntimeTests` | Runtime observation correlation, confidence boost |
| `InvestigationToolsTests` | SearchCode, GetMethod, GetCallers, GetCallees, SearchReferences, GetRelatedMethods |
| `InvestigationRecipesTests` | Recipe execution, text output |
| `RuntimeObserverTests` | Record, retrieve, count, clear |
| `RuntimeObservationPersistenceTests` | Store and retrieve from SQLite |
| `RetrievalEvaluatorTests` | Precision/recall/F1 calculation |
| `MethodSummaryGeneratorTests` | Summary generation from metadata and body |
| `BenchmarkIndexingTests` | Indexing throughput benchmarks |
| `CliCommandBenchmarks` | CLI command execution benchmarks |
| `StructuralAnalysisTests` | Type hierarchy, interface implementations, engine callbacks |

Test assets: `tests/DeepTrace.Tests/TestLib` (Greeter, Class1, INamedInterface, DerivedClass — nested sub-project), `test-assets/BenchmarkLib` (100 generated classes with methods).

## Common Patterns and Gotchas

- **Symbol ID format is strict.** Always use `Assembly::Namespace.Type::Method::Signature`. Mismatched format causes null returns from `GetMethod`.
- **AssemblyDiscoverer excludes framework assemblies.** System.*, Microsoft.*, Windows.*, netstandard, System.Core, System.Runtime, System.Linq, System.Threading, System.Data, System.Xml, ICSharpCode, Mono.Cecil.
- **SQLite uses `Pooling=false`.** Each query opens a new connection. Lock-based concurrency via `lock (_lock)`.
- **FTS5 triggers keep search index in sync.** Do not modify symbols or methods tables without updating triggers.
- **Decompiler caches type source per assembly path.** Clear cache on `Dispose()`.
- **Method summary is heuristic-generated.** Not guaranteed accurate. Check `MethodInfo.Summary` field.
- **Investigation workflow output is text-based.** `InvestigationTools` formats results as structured text. `InvestigationWorkflow` parses this text. Do not change output format without updating parsers.
- **Call graph edge types are inferred from IL opcodes.** `call` = `direct` (except add_/remove_ = `event_subscription`), `callvirt` = `virtual` (except delegate Invoke = `delegate`).
- **Engine callbacks are convention-based.** Not all games follow Unity/Unreal conventions. Results are hints, not guarantees.
- **Interface implementations detected via explicit naming (e.g., `IFoo.Bar`) and overrides.** Does not cover implicit implementations on types that don't declare them in Cecil metadata.
- **`MethodInfo.Body` may be null.** Decompilation can fail for abstract methods, methods with no body, or if ILSpy throws. Check before using body text.
- **`InvestigationWorkflow` parses its own tool output.** Tight coupling between `InvestigationTools` output format and `InvestigationWorkflow` parsers. Changing one requires updating the other.
