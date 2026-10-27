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

## Quick start

```bash
# Build
dotnet build DeepTrace.slnx

# Discover managed assemblies
dotnet run --project src/DeepTrace.Cli -- discover ./GameDirectory

# Index for search
dotnet run --project src/DeepTrace.Cli -- index ./GameDirectory

# Search
dotnet run --project src/DeepTrace.Cli -- search "Greeter"

# Investigate
dotnet run --project src/DeepTrace.Cli -- investigate "How does the game greet players?"
```

### Multiple game directories

Use `--db <path>` to target a specific database. Each game gets its own SQLite database.

```bash
# Index separate games into separate databases
dotnet run --project src/DeepTrace.Cli -- --db games/unity.db index C:/Games/UnityGame
dotnet run --project src/DeepTrace.Cli -- --db games/steamgame.db index C:/Games/SteamGame

# Search a specific game
dotnet run --project src/DeepTrace.Cli -- --db games/unity.db search "NPC"
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
| `call-graph <methodId>` | Show enriched call graph edges |
| `engine-callbacks <typeId>` | Show detected Unity/Unreal callbacks |
| `interface-implementations <iface> <method>` | Show concrete implementations |
| `investigate <question>` | Run AI investigation workflow |
| `observe <symbolId> <durationMs>` | Record a runtime observation |
| `runtime <symbolId>` | Show runtime observations |
| `recipes [name]` | List or execute investigation recipes |
| `evaluate <dllPath>` | Measure retrieval quality |
| `stats` | Show indexer statistics |

All commands accept `--db <path>` anywhere in args to target a specific database. Default: `deeptrace.db`.

Full CLI reference: [docs/CLI.md](docs/CLI.md)

## Architecture

```
src/
  DeepTrace.Core/         Domain models and interfaces
    Models/               MethodInfo, TypeInfo, SearchResult, InvestigationReport, etc.
    Interfaces/           IIndexer, IAssemblyDiscoverer, IDecompiler, IRuntimeObserver
    Ai/                   InvestigationWorkflow, InvestigationTools, InvestigationRecipes
    Retrieval/            RetrievalEvaluator (precision/recall/F1)
    Runtime/              RuntimeObservation, InMemoryRuntimeObserver
  DeepTrace.Decompiler/   ILSpy-based decompiler wrapper
  DeepTrace.Indexing/     SqliteIndexer (SQLite + FTS5), Bm25Scorer, AssemblyDiscoverer
  DeepTrace.Cli/          CLI entry point with all commands
tests/
  DeepTrace.Tests/        xUnit test suite (196 tests)
  TestLib/                Self-contained test assembly
  BenchmarkLib/           Benchmark test fixtures
```

## Technology

- C# / .NET 8 (core libraries), .NET 10 (CLI)
- Mono.Cecil 0.11.6 — assembly metadata extraction
- ICSharpCode.Decompiler 9.0.0.7889 (ILSpy) — source body extraction
- Microsoft.Data.Sqlite 10.0.0 — SQLite ORM
- SQLitePCLRaw.bundle_e_sqlite3 3.0.5 — native SQLite bindings
- xUnit 2.9.3 — test framework

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

## AI integration

DeepTrace includes an AI skill for use in agent setups. To configure your project:

### Adding the DeepTrace CLI skill to AGENTS.md

Copy the skill file into your project and reference it in AGENTS.md:

```markdown
## Skills

<skill>
  <id>deeptrace-cli</id>
  <name>DeepTrace CLI</name>
  <description>Use when working with the DeepTrace CLI to index, search, and investigate .NET assemblies for game mod development.</description>
  <path>skills/deeptrace-cli.md</path>
</skill>
```

Or add the skill path to your OpenCode configuration to enable auto-discovery:

```json
{
  "skills": {
    "paths": ["skills/"]
  }
}
```

The skill provides a quick-reference guide for all DeepTrace CLI commands, symbol ID formats, and typical workflows. See [skills/deeptrace-cli.md](skills/deeptrace-cli.md) for the full skill content.

### Using DeepTrace as a library

```csharp
using DeepTrace.Indexing;
using DeepTrace.Decompiler;
using DeepTrace.Core.Ai;

var indexer = new SqliteIndexer("deeptrace.db", new IlspyDecompiler());
var assemblies = indexer.DiscoverAssemblies("./GameDirectory");
foreach (var asm in assemblies)
    indexer.IndexAssembly(asm);

// Search
var results = indexer.Search("Greeter", limit: 20);

// Investigation
var tools = new InvestigationTools(indexer);
var workflow = new InvestigationWorkflow(tools);
var report = workflow.Investigate("How does the game greet players?");
```

## Documentation

- [CLI Reference](docs/CLI.md) — all commands with parameters and examples
- [API Reference](docs/API.md) — public types, interfaces, and methods
- [AI Skill](skills/deeptrace-cli.md) — quick-reference for agent setups

## Known limitations

- No semantic embeddings — BM25 scoring is lexical only
- IL2CPP assemblies not supported (managed .NET only)
- Static call graph does not cover reflection — use runtime observations for reflection-based calls
- Decompiler extracts method bodies by brace-counting from type source
- Runtime observer is in-memory only (no persistent instrumentation)

## License

To be determined.
