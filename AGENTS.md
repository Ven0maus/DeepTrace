# AGENTS.md

## Project Overview

Local code intelligence system for AI-assisted game mod development. Targets decompiled .NET game assemblies and BepInEx-based mods.

Long-term goal: enable AI to understand game architecture, discover gameplay systems, trace execution paths, implement compatible mod features using evidence from actual game code.

Example use case:

"Create an NPC using the same process the game uses to populate a city."

Discover existing NPC creation and initialization path before proposing implementation.

## Engineering Principles

1. **Evidence over assumptions.** Never invent game classes, methods, signatures, APIs, or execution paths.
2. **Inspect before implementing.** Explore repository, dependencies, conventions, architecture before changes.
3. **Smallest useful implementation.** Avoid unnecessary abstractions, dependencies, frameworks, distributed infrastructure.
4. **Preserve provenance.** Every code fragment and structural relationship traceable to source assembly and symbol.
5. **Distinguish facts from inference.** Label inferred relationships, generated summaries, runtime observations.
6. **Validate changes.** Build and test affected components when environment permits.
7. **Incremental development.** Complete useful, testable milestones over large speculative architecture.
8. **Local-first.** Game assemblies, indexes, caches, configuration remain local unless explicit feature requires otherwise.

## Technology Direction

Preferred technologies, subject to repository compatibility:

- C# and .NET for application and tooling.
- ILSpy / ICSharpCode.Decompiler for decompilation.
- Mono.Cecil for compiled assembly metadata and IL analysis.
- SQLite and FTS5 for local indexing and full-text search.
- HarmonyX and BepInEx for optional runtime instrumentation.
- Automated tests for indexing, graph traversal, retrieval, regression evaluation.

Do not add a dependency solely because it appears in this list. Check compatibility, maintenance, licensing, real problem solved.

Keep indexing application independent of target game's runtime wherever possible.

Treat Unity Mono and IL2CPP as distinct runtime targets. Do not assume techniques applicable to one work unchanged for the other.

## Architecture Boundaries

Maintain clear separation between:

- Assembly discovery and decompilation.
- Symbol extraction and metadata analysis.
- Persistent indexing and storage.
- Full-text and semantic retrieval.
- Code relationship graphs.
- AI orchestration and tool interfaces.
- Optional in-game runtime instrumentation.
- CLI, diagnostics, developer workflows.

Do not introduce all components as separate projects unless repository and implementation justify separation.

Avoid coupling indexing pipeline directly to LLM provider. Search, metadata retrieval, graph traversal work independently of AI orchestration layer.

Keep game-specific references isolated from core indexing application.

## Code Intelligence Requirements

System should progressively support:

- Searching code by names, signatures, text, natural-language intent.
- Retrieving complete method bodies and source locations.
- Inspecting types, fields, properties, constructors, inheritance.
- Finding callers, callees, symbol references.
- Tracing object creation, initialization, registration, lifecycle behavior.
- Identifying candidate mod extension points and HarmonyX patches.
- Correlating static analysis with optional runtime observations.

Use stable symbol identifiers based on assembly identity and fully qualified symbol signatures.

Do not use source line numbers as symbol identities.

Prefer metadata-aware analysis over naive text matching for compiled method and type references.

Static call graphs incomplete with virtual dispatch, reflection, event systems, dynamic loading, engine callbacks. Preserve those limitations in results and reports.

## AI Investigation Workflow

When asked to implement gameplay feature, AI should:

1. Decompose request into relevant game concepts.
2. Search for candidate methods and types using multiple queries.
3. Retrieve actual source evidence.
4. Follow relevant callers, callees, constructors, type relationships.
5. Identify initialization order, dependencies, state invariants.
6. Compare possible extension points.
7. Separate verified facts from assumptions.
8. Propose smallest compatible implementation.
9. Define validation strategy.
10. Implement only after sufficient investigation.

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

1. Build affected projects.
2. Run relevant tests.
3. Fix regressions introduced by change.
4. Report what was implemented and verified.
5. Explicitly identify remaining limitations and untested behavior.

Do not claim build or test passed unless actually executed successfully.

Do not replace working architecture or rewrite large repository portions without clear technical reason.

## Implementation Priorities

Prioritize milestones in order:

1. Local assembly ingestion and method-level indexing.
2. Full-text search and exact source retrieval.
3. Type relationships, call graphs, reference analysis.
4. AI-driven code investigation using structured tools.
5. Runtime observations through optional BepInEx instrumentation.
6. Semantic retrieval and advanced ranking when justified by evaluation.

First milestone must be useful without LLM.

AI layer must be useful without runtime instrumentation.

Each milestone should have tests and documented completion criteria.

## Task Execution

For each new task:

- Read this file and any applicable nested `AGENTS.md` files.
- Inspect existing repository before deciding what to create.
- Reuse existing implementations when appropriate.
- Prefer working vertical slice over placeholder projects.
- Do not ask questions when documented assumption allows safe progress.
- Ask for clarification when unknown materially affects correctness or risks destructive changes.

When task specifies concrete deliverable, prioritize implementing it over explaining how it could be implemented.
