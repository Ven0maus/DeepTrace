# Build a Game Code Intelligence System for BepInEx Mod Development

## Role

Act as a senior .NET engineer specializing in C#, reverse engineering, static code analysis, Unity, BepInEx, HarmonyX, and AI-assisted developer tooling.

Design and implement a local code intelligence system that helps an AI understand a decompiled game's architecture and locate the correct code paths when implementing mods.

The system must allow me to ask high-level questions such as:

> "Build a way to create a new NPC similar to how the game populates the city with NPCs."

The AI should investigate the existing implementation, discover relevant types and methods, trace their relationships, understand the initialization lifecycle, and identify safe extension points before writing mod code.

This is not merely a source-code search tool. It should become a reliable code investigation layer for AI-assisted game mod development.

## Primary objective

Build a system that transforms decompiled game assemblies into a searchable, structured representation of the game's codebase.

The system must provide the AI with tools to:

- Search for relevant code using natural language, keywords, type names, method names, and signatures.
- Retrieve complete method bodies and their source locations.
- Explore types, inheritance, interfaces, fields, properties, constructors, and attributes.
- Trace method callers and callees.
- Find references to methods, types, and fields.
- Discover object creation, initialization, registration, and destruction paths.
- Identify candidate HarmonyX patch points.
- Retrieve runtime observations when available.
- Produce evidence-based investigation reports before proposing implementations.

Prioritize correctness, traceability, maintainability, and efficient retrieval over unnecessary architectural complexity.

## 1. Architecture

Implement the system as a modular .NET application with clearly separated responsibilities.

Suggested components:

### A. Assembly ingestion and decompilation

Responsibilities:

- Accept a configurable game installation directory.
- Discover managed assemblies relevant to the game.
- Decompile assemblies using ILSpy's decompiler libraries or CLI.
- Preserve assembly identity, namespaces, type names, signatures, and source locations.
- Support incremental indexing so unchanged assemblies do not need to be processed unnecessarily.
- Detect when assemblies change and invalidate affected index entries.
- Handle decompilation failures gracefully.
- Keep original game assemblies separate from generated source and index data.

Do not assume all games use Unity Mono. Design the ingestion layer so Unity Mono can be supported first, while allowing a future IL2CPP-specific backend.

### B. Source parser and symbol indexer

Use appropriate C# parsing and metadata analysis tools to extract:

- Namespaces and types.
- Classes, structs, interfaces, enums, and delegates.
- Methods and constructors.
- Method signatures and overloads.
- Fields, properties, and events.
- Attributes and inheritance relationships.
- Method bodies and source ranges.
- Assembly references.
- Relevant constants and string literals.
- Object construction and lifecycle-related operations.

Each symbol must have a stable identifier derived from its assembly identity, declaring type, member name, and signature.

Do not rely exclusively on filenames or line numbers as symbol identities because decompiled output may change between runs.

Store source locations separately from symbol identity.

### C. Structural relationship index

Construct a code graph containing nodes for types, methods, fields, properties, and assemblies.

Record relationships such as:

- Method calls method.
- Method constructs type.
- Type inherits from type.
- Type implements interface.
- Method reads or writes field.
- Method accesses property.
- Type references type.
- Method overrides method.
- Method is associated with an attribute.
- Method may participate in initialization or lifecycle behavior.

Use Mono.Cecil or equivalent metadata/IL analysis for relationships that can be extracted from compiled assemblies.

Distinguish directly observed relationships from inferred ones.

Account for the limitations of static analysis, including virtual dispatch, reflection, event-driven behavior, dependency injection, dynamically loaded assemblies, and Unity lifecycle callbacks.

Do not claim that a static call graph represents every runtime execution path.

### D. Search and persistence

Start with SQLite as the local persistence layer.

Use SQLite FTS5 for full-text indexing of:

- Symbol names.
- Signatures.
- Namespace and type names.
- Decompiled method bodies.
- Extracted string literals.
- Generated summaries and keywords.

Store the structural graph in relational tables or another simple representation that works well with SQLite.

Design the schema to support future semantic vector search without making a vector database mandatory for the initial implementation.

Include appropriate indexes, schema versioning, transactions, and incremental updates.

## 2. Required AI-facing tools

Expose the indexing system through a tool interface that an LLM can call iteratively.

The interface can use function calling or an MCP-compatible server if that fits the surrounding application.

Implement the following operations.

### SearchCode(query, filters, limit)

Search for code relevant to a natural-language question or keyword query.

Return ranked results containing:

- Stable symbol ID.
- Symbol name and kind.
- Fully qualified name.
- Signature.
- Assembly.
- Source location.
- Short relevant excerpt.
- Summary, when available.
- Relevance score and explanation.

Support filtering by assembly, namespace, symbol kind, and other useful metadata.

### GetMethod(symbolId)

Return the complete available method representation:

- Signature.
- Declaring type.
- Assembly.
- Decompiled source.
- Source range.
- Attributes.
- Direct callers and callees, when indexed.
- Known uncertainties or decompilation issues.

Handle overloaded methods using their full signatures rather than ambiguous names.

### GetType(typeId)

Return:

- Type declaration and assembly.
- Base type and implemented interfaces.
- Fields and properties.
- Constructors and methods.
- Attributes.
- References to related types.
- Known construction and initialization paths.

### GetCallers(symbolId, depth, limit)

Find methods that call the selected method.

Support bounded graph traversal, with safeguards against excessively large results.

### GetCallees(symbolId, depth, limit)

Find methods called by the selected method.

Return direct calls separately from deeper traversal results.

### SearchReferences(symbolId, limit)

Find references to a type, method, field, property, or other supported symbol.

Use metadata-aware resolution where possible. Clearly identify unresolved or heuristic matches.

### GetRelatedMethods(symbolId, limit)

Retrieve methods related through shared types, construction, initialization, registration, lifecycle behavior, or nearby call graph relationships.

Explain why each method was selected.

### GetRuntimeEvidence(symbolId, filters)

Retrieve runtime observations when the runtime instrumentation component is available.

Return observed calls, relevant arguments, timestamps or sequence numbers, and associated symbols when these can be resolved.

If no runtime evidence exists, return an explicit empty result. Never fabricate observations.

All tools must return structured, bounded results suitable for consumption by an LLM.

## 3. Search ranking

Implement hybrid ranking rather than relying entirely on vector similarity.

Consider these signals:

- Lexical relevance of identifiers and source text.
- Semantic similarity of summaries and the user's request, when semantic search is enabled.
- Symbol kind and structural role.
- Call graph proximity to promising candidate methods.
- Relevance of callers, callees, and related types.
- Evidence from previous investigations, if persisted.

Make ranking configurable and inspectable.

Avoid prematurely introducing a complex ranking model. Start with lexical search and structural relevance, then evaluate whether semantic embeddings improve real retrieval tasks.

Do not treat a method name match as proof that the method performs the requested behavior.

## 4. AI investigation workflow

Implement an explicit investigation workflow that runs before feature implementation.

When given a request such as:

"Create an NPC using the same process the game uses to populate a city."

The AI should:

1. Parse the request into concepts such as NPC, city population, spawning, object construction, initialization, registration, and lifecycle.
2. Search for candidate entry points using multiple queries rather than relying on one guessed method name.
3. Retrieve the most promising method bodies and type definitions.
4. Explore callers and callees to reconstruct the relevant execution path.
5. Identify constructors, factories, prefab loading, asset lookups, initialization routines, and registration logic where applicable.
6. Inspect methods that establish required dependencies and invariants.
7. Search for existing public APIs or mod-accessible extension points.
8. Identify suitable HarmonyX patch locations only when direct API reuse is insufficient.
9. Collect evidence supporting the proposed creation sequence.
10. Produce an investigation report before implementing code.

The workflow must be iterative. If evidence is incomplete, the AI should request additional targeted searches instead of guessing.

Use bounded retrieval, configurable graph depth, result limits, and a token budget.

Do not dump the entire codebase into the LLM context.

## 5. Investigation report format

For every substantial feature request, produce a report with these sections.

### Request interpretation

Explain the requested behavior and the important concepts identified.

### Discovered execution path

List the relevant methods in execution order when the evidence supports such an ordering.

For each method, include:

- Symbol ID.
- Fully qualified name.
- Assembly.
- Source location.
- Why it is relevant.
- Supporting source excerpts.

Distinguish verified call relationships from inferred relationships.

### Required dependencies and invariants

Identify relevant assets, prefabs, constructors, components, initialization requirements, registries, managers, and lifecycle constraints.

Only include dependencies supported by evidence. Mark unresolved questions explicitly.

### Candidate extension points

Identify possible APIs to reuse and candidate HarmonyX patch points.

Explain:

- Why each point is suitable.
- Whether it runs once or repeatedly.
- What arguments or state are available.
- Potential initialization-order problems.
- Potential duplicate registration, cleanup, or save compatibility issues.

Do not automatically recommend patching the first method returned by search.

### Evidence and confidence

Cite exact symbol IDs and source locations.

Assign confidence to individual findings rather than presenting the entire report as equally certain.

Explain which facts come from source analysis and which require runtime verification.

### Implementation plan

Recommend the smallest viable implementation that reuses existing game behavior.

Separate verified facts, assumptions, and proposed changes.

### Validation plan

Propose tests for the feature, including relevant integration tests against the real game when required.

## 6. Runtime instrumentation integration

Design an optional component that can integrate with a development-only BepInEx plugin.

Use HarmonyX or another appropriate mechanism to instrument selected methods at runtime.

Support:

- Recording selected method entry and exit events.
- Recording execution order.
- Recording safe representations of selected arguments and return values.
- Correlating observed execution with statically indexed symbols.
- Capturing exceptions and timing information where appropriate.
- Exporting structured logs for later AI investigation.

Do not instrument every method indiscriminately. Instrumentation must be selective, configurable, and safe enough to avoid excessive overhead.

Do not invoke arbitrary game methods merely to inspect them.

Avoid logging sensitive or enormous object graphs. Use bounded, cycle-safe representations and configurable redaction.

Runtime observations must be distinguishable from static analysis results.

## 7. Testing and evaluation

Create automated tests for the indexing and retrieval components.

Include test cases for:

- Overloaded methods.
- Nested and generic types.
- Inheritance and interface implementations.
- Constructors and object creation.
- Method caller/callee relationships.
- Unresolved references.
- Missing or malformed decompilation output.
- Incremental indexing.
- Stable symbol IDs.
- Large methods and large assemblies.
- Search relevance and bounded result sizes.

Create a small retrieval evaluation suite using representative modding questions, including:

- How does the game create and register an NPC?
- How are inventory items instantiated?
- Where is a particular UI element initialized?
- Which methods are responsible for saving and loading player state?
- Where should a behavior be intercepted with HarmonyX?

Measure whether the system returns the correct methods and relevant dependencies. Use these results to improve ranking and traversal instead of relying on subjective impressions.

## 8. Implementation constraints

- Use modern C# and .NET, choosing a target framework compatible with the intended tooling and deployment environment.
- Keep game-specific references isolated from the indexing application.
- Do not require the game's executable to run for ordinary static indexing.
- Do not assume decompiled source compiles as a standalone project.
- Do not rebuild or modify the original game assemblies just to index them.
- Avoid unnecessary external services; support local operation.
- Keep all game paths configurable.
- Make long-running indexing operations cancellable and report progress.
- Provide structured logging and actionable error messages.
- Avoid arbitrary execution of decompiled code.
- Keep generated indexes, caches, and derived summaries separate from source assemblies.
- Preserve the provenance of all retrieved code and generated summaries.
- Do not invent classes, methods, signatures, or source locations.
- Never present inferred call relationships as confirmed facts.
- Avoid building an elaborate agent framework before the basic search and graph tools work.

## 9. Suggested project structure

Use this as a starting point, adapting it when the implementation benefits from a different arrangement:

GameCodeIntelligence/
- src/
  - GameCodeIntelligence.Core/
  - GameCodeIntelligence.Decompilation/
  - GameCodeIntelligence.Indexing/
  - GameCodeIntelligence.Search/
  - GameCodeIntelligence.Graph/
  - GameCodeIntelligence.AI/
  - GameCodeIntelligence.ToolServer/
  - GameCodeIntelligence.Runtime/
- tests/
  - GameCodeIntelligence.Tests/
  - GameCodeIntelligence.RetrievalTests/
- tools/
  - GameCodeIntelligence.Cli/
- docs/

Do not create empty projects merely to match this structure. Split components when there is a real architectural reason to do so.

## 10. Development milestones

Implement the system incrementally.

### Milestone 1: Source indexing

- Discover and decompile assemblies.
- Extract symbols and source ranges.
- Persist metadata in SQLite.
- Implement SearchCode and GetMethod.
- Add basic tests and a CLI for inspecting search results.

### Milestone 2: Structural analysis

- Extract call relationships and type hierarchy.
- Implement GetType, GetCallers, GetCallees, and SearchReferences.
- Validate relationships against known examples.

### Milestone 3: AI-assisted investigation

- Add method summaries.
- Expose the tools to an LLM.
- Implement the iterative investigation workflow.
- Generate evidence-based investigation reports.

### Milestone 4: Runtime evidence

- Add optional BepInEx/HarmonyX instrumentation.
- Correlate runtime observations with indexed symbols.
- Use observed execution to improve investigation results.

### Milestone 5: Retrieval improvements

- Evaluate semantic embeddings.
- Improve ranking and graph traversal.
- Add reusable investigation recipes.
- Measure retrieval quality against representative modding requests.

## Instructions for starting work

First inspect the existing repository and determine which components already exist, which .NET versions and dependencies are in use, and how the tool will be executed.

If a repository already exists, extend it instead of replacing its architecture without justification.

Then:

1. Propose a concise architecture based on the actual repository.
2. Identify the smallest useful end-to-end milestone.
3. Implement that milestone completely, including tests and documentation.
4. Run the available tests and builds.
5. Report exactly what was implemented, what was verified, and what remains incomplete.

Do not stop after producing a design document or pseudocode if the environment allows implementation.

If a critical decision is unknown, choose a sensible default, document the assumption, and continue where possible rather than blocking on a long questionnaire.

The first deliverable should be a working local indexer with searchable methods and retrievable source code. Everything else should build on that foundation.