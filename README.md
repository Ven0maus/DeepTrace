# DeepTrace

Local code intelligence for AI-assisted game mod development.

DeepTrace indexes decompiled .NET assemblies, analyzes code relationships, and provides structured search and investigation capabilities for AI coding assistants working with unfamiliar game codebases.

Target: decompiled .NET assemblies and BepInEx-based mods.

## What it does

- Index assemblies using Mono.Cecil metadata extraction
- Search by name, signature, namespace, or source body text
- Rank results with BM25-inspired relevance scoring
- Analyze structural relationships: callers, callees, type hierarchies, references
- Run AI investigation workflows with iterative search, caller/callee traversal, and evidence-based reports
- Record and query runtime observations (via SQLite or in-memory store)
- Execute pre-defined investigation recipes for common modding tasks
- Evaluate retrieval quality with precision/recall/F1 metrics

## Example workflow

```
deeptrace discover ./GameDirectory
deeptrace index ./GameDirectory
deeptrace search "Greeter"
deeptrace callees "TestLib::TestLib.Greeter::Greet::Greet(System.String) : System.String"
deeptrace investigate "How does the game greet players?"
```

## CLI commands

| Command | Description |
|---|---|
| `discover <directory>` | Discover managed assemblies in a directory |
| `index <directory>` | Index all discovered assemblies |
| `search <query> [limit]` | Search indexed symbols (BM25-ranked) |
| `methods <assemblyName>` | List methods in an assembly |
| `method <symbolId>` | Get method details |
| `types <assemblyName>` | List types in an assembly |
| `type <assemblyName> <typeFullName>` | Get single type info |
| `callers <methodSymbolId>` | Find callers of a method |
| `callees <methodSymbolId>` | Find callees of a method |
| `references <symbolId>` | Find all references to a symbol |
| `investigate <question>` | Run AI investigation workflow |
| `observe <symbolId> <durationMs>` | Record a runtime observation |
| `runtime <symbolId>` | Show runtime observations |
| `recipes [name]` | List or execute investigation recipes |
| `evaluate <dllPath>` | Measure retrieval quality |
| `stats` | Show indexer statistics |

## Architecture

```
src/
  DeepTrace.Core/         Domain models and interfaces
    Models/               MethodInfo, TypeInfo, SearchResult, InvestigationReport, etc.
    Interfaces/           IIndexer, IAssemblyDiscoverer, IDecompiler
    Ai/                   InvestigationWorkflow, InvestigationTools, InvestigationRecipes
    Retrieval/            RetrievalEvaluator (precision/recall/F1)
    Runtime/              RuntimeObservation, IRuntimeObserver, InMemoryRuntimeObserver
  DeepTrace.Decompiler/   ILSpy-based decompiler wrapper
  DeepTrace.Indexing/     SqliteIndexer (SQLite + FTS5), Bm25Scorer, AssemblyDiscoverer
  DeepTrace.Cli/          CLI entry point with all commands
tests/
  DeepTrace.Tests/        xUnit test suite (101 tests)
```

## Technology

- C# / .NET 8 (core libraries), .NET 10 (CLI)
- Mono.Cecil 0.11.6 — assembly metadata extraction
- ILSpy / ICSharpCode.Decompiler — source body extraction
- SQLite / FTS5 — local persistence and full-text search
- SQLitePCLRaw 3.0.5 — native SQLite bindings (SQLite 3.53.4)
- xUnit — test framework

## Investigation recipes

Pre-defined patterns for common modding tasks:

- **Find Extension Points** — virtual methods, overrides, public instance methods (HarmonyX patch targets)
- **Trace Initialization** — constructors, static initializers, On* lifecycle methods
- **Find Service Registrations** — factory methods, Create/Build/From patterns, DI registrations
- **Find Type Hierarchies** — base types, interfaces, derived type relationships
- **Find Event Handlers** — event subscriptions, callbacks, handler methods

## Investigation workflow

The `investigate` command runs an iterative search workflow:

1. Extract concepts from the question
2. Search for relevant methods by each concept
3. Retrieve method bodies and generate summaries
4. Correlate with runtime observations (if recorded)
5. Explore caller/callee graphs (bounded depth)
6. Identify dependencies, extension points, and confidence levels
7. Produce a structured report

## Retrieval quality

The `evaluate` command measures search quality against known-gold-standard results:

- Precision: percentage of retrieved results that are relevant
- Recall: percentage of relevant results that were retrieved
- F1: harmonic mean of precision and recall

## Project status

**Milestones complete:**

| # | Milestone | Status |
|---|---|---|
| 1 | Source indexing | Complete — assembly discovery, Cecil metadata, SQLite + FTS5, CLI discover/index/search/method/types |
| 2 | Structural analysis | Complete — callers, callees, type hierarchies, references |
| 3 | AI investigation | Complete — iterative search, summaries, caller/callee traversal, structured reports |
| 4 | Runtime evidence | Complete — IRuntimeObserver, SQLite persistence, confidence boosting in reports |
| 5 | Retrieval improvements | Complete — BM25 scoring, investigation recipes, retrieval evaluation |

**Known limitations:**

- ILSpy decompiler body extraction is a stub — full body extraction deferred
- No semantic embeddings — BM25 scoring is lexical only
- No BepInEx/HarmonyX runtime instrumentation plugin — only in-memory and SQLite observation storage
- IL2CPP assemblies not supported (managed .NET only)
- Static call graph incomplete for virtual dispatch, reflection, events, engine callbacks

## Test results

```
101 tests passed, 0 failed
```

## License

To be determined.
