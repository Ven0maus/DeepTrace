# CodeAtlas

**AI-powered code intelligence for decompiled .NET games and mod development.**

CodeAtlas helps AI coding assistants understand unfamiliar game codebases by indexing assemblies, analyzing code relationships, and tracing gameplay behavior back to its implementation.

Instead of searching for a method by name and guessing how it works, CodeAtlas enables an AI to investigate the existing code, follow relevant call chains, identify dependencies, and find suitable extension points before implementing a mod.

The initial focus is on .NET games and BepInEx mod development, with an architecture designed to support broader code analysis in the future.

## Goals

- **Understand unfamiliar codebases** — discover relevant types, methods, and gameplay systems.
- **Trace behavior** — follow callers, callees, constructors, and initialization paths.
- **Retrieve actual source** — provide precise method bodies, signatures, and source locations.
- **Support AI investigation** — expose structured tools that an LLM can use to explore code iteratively.
- **Bridge static and runtime analysis** — combine decompiled code with optional observations from a running game.
- **Remain local-first** — index and analyze local assemblies without requiring a remote service for basic functionality.

## Example

Consider the request:

> Create a new NPC using the same process the game uses to populate a city with NPCs.

A conventional text search might find a method with `NPC` or `Spawn` in its name, but that doesn't establish whether it is the correct entry point.

CodeAtlas aims to help an AI investigate the feature systematically:

1. Search for city initialization and population-related code.
2. Identify candidate NPC creation methods.
3. Trace callers and callees to reconstruct the creation path.
4. Inspect constructors, prefabs, initialization routines, and registration logic.
5. Identify existing APIs or potential HarmonyX patch points.
6. Present the relevant code and supporting evidence.
7. Propose an implementation and a validation strategy.

The objective is to reuse the game's existing behavior wherever possible, rather than reimplementing it based on assumptions.

## Architecture

CodeAtlas is designed around several cooperating components.

| Component | Responsibility |
|---|---|
| Assembly ingestion | Discover and decompile relevant game assemblies |
| Symbol indexing | Extract types, methods, signatures, and source locations |
| Structural analysis | Analyze calls, inheritance, references, and object construction |
| Search engine | Retrieve relevant code using lexical and, optionally, semantic search |
| Code graph | Navigate relationships between symbols |
| AI orchestration | Investigate feature requests using structured tools |
| Runtime integration | Collect optional observations from a running game |
| CLI | Configure indexing, inspect results, and manage investigations |

The initial implementation prioritizes a working local indexer and searchable symbol database before adding more advanced AI and runtime capabilities.

## Technology

The intended technology stack includes:

- **C# / .NET** — primary implementation language.
- **ILSpy / ICSharpCode.Decompiler** — decompilation of managed assemblies.
- **Mono.Cecil** — assembly metadata and IL analysis.
- **SQLite / FTS5** — local persistence and full-text search.
- **BepInEx / HarmonyX** — optional runtime instrumentation for compatible games.
- **LLM tool calling** — AI-assisted code exploration and investigation.

Exact framework versions and dependency choices will be determined by the target runtime and compatibility requirements.

Unity Mono and Unity IL2CPP require different integration approaches. Initial support should target managed .NET assemblies, with IL2CPP support treated as a separate compatibility concern.

## AI Tool Interface

CodeAtlas is intended to expose structured operations that an AI can call while investigating a codebase.

| Operation | Purpose |
|---|---|
| `SearchCode` | Search for relevant types, methods, and source snippets |
| `GetMethod` | Retrieve a method's signature, body, and metadata |
| `GetType` | Inspect a type's hierarchy, fields, properties, and methods |
| `GetCallers` | Find methods that call a selected method |
| `GetCallees` | Explore methods invoked by a selected method |
| `SearchReferences` | Find references to a symbol |
| `GetRelatedMethods` | Discover related lifecycle and initialization methods |
| `GetRuntimeEvidence` | Retrieve recorded runtime observations when available |

These operations are planned interfaces; their availability depends on implementation progress.

Results should include stable symbol identifiers, source provenance, and explicit distinctions between confirmed relationships and inferred ones.

## Search and Analysis

CodeAtlas is designed to combine several complementary forms of analysis.

### Full-text search

Search method names, signatures, namespaces, source bodies, and string literals.

### Structural analysis

Analyze method calls, type hierarchies, constructors, field access, and references using compiled assembly metadata where possible.

### Semantic search

Optionally use generated method summaries and embeddings to discover relevant code when the terminology in a request differs from the identifiers used by the game.

### Graph traversal

Follow relationships from candidate entry points to discover dependencies and reconstruct likely execution paths.

Static analysis cannot fully resolve every virtual call, reflection-based invocation, event subscription, dynamically loaded assembly, or engine callback. CodeAtlas should preserve these limitations in its results instead of presenting incomplete graphs as definitive execution traces.

## Runtime Integration

An optional development-only BepInEx plugin can extend static analysis with runtime evidence.

Potential capabilities include:

- Recording selected method calls and execution order.
- Inspecting selected arguments and return values safely.
- Capturing relevant exceptions and timing information.
- Correlating observed methods with indexed symbols.
- Exporting structured logs for AI-assisted investigation.

Instrumentation should be selective and configurable to limit overhead. Runtime evidence must remain distinguishable from statically inferred behavior.

## Project Status

**Planned / early development**

The initial milestone is a working local indexer with searchable methods and retrievable source code.

| Milestone | Scope |
|---|---|
| 1. Source indexing | Assembly ingestion, symbol extraction, SQLite storage, and source retrieval |
| 2. Structural analysis | Call relationships, type hierarchies, and reference searches |
| 3. AI investigation | Tool interface, iterative search, and evidence-based reports |
| 4. Runtime integration | Optional BepInEx instrumentation and runtime observations |
| 5. Retrieval improvements | Semantic search, ranking evaluation, and reusable investigation workflows |

Features should be marked complete only after implementation and verification.

## Design Principles

- **Evidence over assumptions:** never invent symbols, APIs, or execution paths.
- **Source provenance:** every retrieved fragment should be traceable to its original assembly and symbol.
- **Incremental indexing:** avoid reprocessing unchanged assemblies unnecessarily.
- **Local-first operation:** keep basic indexing and search independent of external AI services.
- **Separation of concerns:** keep indexing, analysis, search, AI orchestration, and runtime integration loosely coupled.
- **Bounded retrieval:** retrieve relevant code rather than dumping entire projects into an LLM context.
- **Testability:** validate indexing, graph traversal, and retrieval quality with automated tests.
- **Minimal implementation:** prefer a useful, working vertical slice over premature infrastructure.

## Getting Started

The setup instructions will be added once the initial CLI and indexing pipeline are implemented.

The intended workflow is:

1. Configure the target game's installation directory.
2. Discover and index its relevant assemblies.
3. Search for types and methods.
4. Retrieve source and inspect code relationships.
5. Connect an AI assistant to the structured tool interface.
6. Investigate gameplay features before implementing mods.

## License

To be determined.