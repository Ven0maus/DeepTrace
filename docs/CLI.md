# DeepTrace CLI Reference

Command-line interface for DeepTrace — local code intelligence for AI-assisted game mod development.

## Prerequisites

- .NET 10 SDK (CLI)
- .NET 8 runtime (core libraries)
- SQLite (bundled via SQLitePCLRaw)

## Building

```bash
dotnet build DeepTrace.slnx
```

The CLI binary is at `src/DeepTrace.Cli/bin/Debug/net10.0/deeptrace.dll` (run with `dotnet deeptrace.dll`).

## Usage

```
deeptrace <command> [args...]
```

### `--db <path>` flag

All commands accept `--db <path>` to target a specific SQLite database. Place it anywhere in the argument list.

```bash
# Index a game into a named database
deeptrace --db games/unity.db index C:/Games/UnityGame

# Search that game
deeptrace --db games/unity.db search "NPC"

# Default database (backward compatible)
deeptrace index ./GameDirectory   # uses deeptrace.db
```

Without `--db`, all commands use `deeptrace.db` in the current working directory.

## Commands

### `discover`

Discover managed .NET assemblies in a directory.

```
deeptrace discover <directory>
```

Scans the directory recursively for `.dll` and `.exe` files, filters out system/framework assemblies, and prints each discovered assembly with its name and full path.

**Excluded assemblies:** System.*, Microsoft.*, Windows.*, netstandard, System.Core, System.Runtime, System.Linq, System.Threading, System.Data, System.Xml, ICSharpCode, Mono.Cecil, and others.

### `index`

Index all discovered assemblies into the SQLite database (`deeptrace.db`).

```
deeptrace index <directory>
```

For each discovered assembly:
1. Checks if already indexed and unchanged (by hash)
2. Skips unchanged assemblies
3. Extracts metadata using Mono.Cecil (types, methods, fields, properties)
4. Decom piles bodies using ILSpy
5. Builds FTS5 full-text index
6. Records type relationships (inheritance, interfaces, callers, callees)

Output shows indexed and skipped assemblies.

### `search`

Search indexed symbols by keyword or phrase. Results ranked by BM25 relevance.

```
deeptrace search <query> [limit]
```

| Parameter | Description | Default |
|-----------|-------------|---------|
| `query` | Search keyword or phrase | required |
| `limit` | Maximum results | 50 |

Scans symbol names, fully qualified names, signatures, and method body excerpts.

Output format:
```
Found N results for 'query':
  [Kind] FullyQualifiedName
    Sig: Signature
     Excerpt from body
    Assembly: AssemblyName
```

### `methods`

List all methods in an assembly.

```
deeptrace methods <assemblyName>
```

| Parameter | Description |
|-----------|-------------|
| `assemblyName` | Assembly name (e.g., "TestLib") |

Output: method symbol ID and truncated signature for each method.

### `method`

Get full details for a single method.

```
deeptrace method <symbolId>
```

| Parameter | Description |
|-----------|-------------|
| `symbolId` | Method symbol ID in format `Assembly::Namespace.Type::Method::Signature` |

Output:
```
SymbolId: ...
Name: ...
Signature: ...
DeclaringType: ...
Assembly: ...
IsStatic: true/false
IsVirtual: true/false
IsAbstract: true/false
IsOverride: true/false
Attributes: ...
Parameters: ...
Body:
  ...decompiled source...
Summary: ...
```

### `types`

List all types in an assembly.

```
deeptrace types <assemblyName>
```

| Parameter | Description |
|-----------|-------------|
| `assemblyName` | Assembly name |

Output: type full name, base type, and method count.

### `type`

Get detailed information for a single type.

```
deeptrace type <assemblyName> <typeFullName>
```

| Parameter | Description |
|-----------|-------------|
| `assemblyName` | Assembly name |
| `typeFullName` | Fully qualified type name (e.g., "TestLib.Greeter") |

Output: symbol ID, namespace, base type, base types, derived types, interfaces, attributes, type references, methods, and fields.

### `callers`

Find methods that call the specified method (direct callers).

```
deeptrace callers <methodSymbolId>
```

| Parameter | Description |
|-----------|-------------|
| `methodSymbolId` | Symbol ID of the callee method |

Output: list of caller symbol IDs.

### `callees`

Find methods called by the specified method (direct callees).

```
deeptrace callees <methodSymbolId>
```

| Parameter | Description |
|-----------|-------------|
| `methodSymbolId` | Symbol ID of the caller method |

Output: list of callee symbol IDs.

### `call-graph <methodId> [edgeType]`

Show the enriched call graph for a method — direct calls, virtual dispatch, delegates, and events.

```
deeptrace call-graph <methodId> [edgeType]
```

| Parameter | Description | Default |
|-----------|-------------|---------|
| `methodId` | Method symbol ID | required |
| `edgeType` | Filter by edge type: `direct`, `virtual`, `delegate`, `event_subscription`, `event_unsubscription` | all |

Edge types:
- **direct** — `call` opcode to a non-add/remove method
- **virtual** — `callvirt` opcode to a virtual/non-delegate method
- **delegate** — `callvirt` on `System.Action/Func/Predicate/Comparison` or `*Delegate.Invoke`
- **event_subscription** — `call` to an `add_` method
- **event_unsubscription** — `call` to a `remove_` method

### `engine-callbacks <typeId>`

Show detected Unity/Unreal engine lifecycle callbacks for a type.

```
deeptrace engine-callbacks <typeId>
```

| Parameter | Description |
|-----------|-------------|
| `typeId` | Type symbol ID |

Detects conventions for:
- **Unity** (MonoBehaviour, ScriptableObject): Awake, Start, Update, FixedUpdate, LateUpdate, OnEnable, OnDisable, OnDestroy, OnTriggerEnter, OnCollisionEnter, etc.
- **Unreal** (Actor, GameMode): BeginPlay, Tick, Destroyed, etc.

### `interface-implementations <iface> <method>`

Show concrete implementations of an interface method — both explicit and implicit.

```
deeptrace interface-implementations <interfaceName> <methodName>
```

| Parameter | Description |
|-----------|-------------|
| `interfaceName` | Interface full name (e.g., "System.IDisposable") |
| `methodName` | Method name (e.g., "Dispose") |

Detects:
- **Explicit**: `InterfaceName.MethodName` pattern in method name
- **Implicit**: via `Overrides` collection in metadata

### `references`

Find all references to a symbol (type references, method callers, method callees).

```
deeptrace references <symbolId>
```

| Parameter | Description |
|-----------|-------------|
| `symbolId` | Symbol ID to search for |

Input can be a full symbol ID or a type full name (e.g., "TestLib.Greeter"). The CLI normalizes the input automatically.

### `investigate`

Run an AI investigation workflow — iterative search, caller/callee traversal, and structured report.

```
deeptrace investigate <question>
```

| Parameter | Description |
|-----------|-------------|
| `question` | Natural language question about the codebase |

**Workflow:**
1. Extract concepts from the question
2. Search for relevant methods by each concept (max 6 searches)
3. Retrieve method bodies and generate summaries
4. Correlate with runtime observations (if recorded)
5. Explore caller/callee graphs (depth 2)
6. Identify dependencies, extension points, and confidence levels
7. Produce structured report

Output: investigation report with request interpretation, discovered execution path, observed execution path, dependencies, extension points, confidence levels, implementation plan, and validation plan.

### `observe`

Record a runtime observation for a method.

```
deeptrace observe <symbolId> <durationMs> [parametersJson] [returnValueJson]
```

| Parameter | Description |
|-----------|-------------|
| `symbolId` | Indexed symbol ID (e.g., "TestLib::TestLib.Greeter::Greet::Greet(System.String) : System.String") |
| `durationMs` | Call duration in milliseconds |
| `parametersJson` | Optional JSON-serialized parameters |
| `returnValueJson` | Optional JSON-serialized return value |

Observations are stored in the SQLite database and used to boost confidence in investigation reports.

### `runtime`

Show runtime observations for a symbol.

```
deeptrace runtime <symbolId>
```

| Parameter | Description |
|-----------|-------------|
| `symbolId` | Indexed symbol ID |

Output: timestamp, depth, duration, parameters, and return value for each observation.

### `recipes`

List or execute pre-defined investigation recipes.

```
deeptrace recipes [name]
```

Without a name, lists all available recipes:
- **Find Extension Points** — virtual methods, overrides, public instance methods (HarmonyX patch targets)
- **Trace Initialization** — constructors, static initializers, On* lifecycle methods
- **Find Service Registrations** — factory methods, Create/Build/From patterns, DI registrations
- **Find Type Hierarchies** — base types, interfaces, derived type relationships
- **Find Event Handlers** — event subscriptions, callbacks, handler methods

With a recipe name, executes it and prints results.

### `evaluate`

Measure retrieval quality against a test assembly.

```
deeptrace evaluate <dllPath>
```

Creates a temporary index from the assembly, builds a gold standard from known methods (Greeter methods and Greet method), and computes precision/recall/F1 metrics.

Output:
```
=== RETRIEVAL EVALUATION REPORT ===

Total queries: N
Avg Precision: XX.X%
Avg Recall: XX.X%
Avg F1: XX.X
Total relevant retrieved: N/N

Query: "Greeter"
  Expected: N results, Retrieved: N
  P=XX% R=XX% F1=XX.X
  Relevant: N/N
```

### `stats`

Show indexer statistics.

```
deeptrace stats
```

Output: database path and decompiler type.

## Symbol ID Format

All commands that accept a `symbolId` use the following format:

**Method symbol ID:**
```
{AssemblyName}::{Namespace.Type}::{MethodName}::{Signature}
```

Example:
```
TestLib::TestLib.Greeter::Greet::Greet(System.String) : System.String
```

**Type symbol ID:**
```
{AssemblyName}::{Namespace.Type}
```

Example:
```
TestLib::TestLib.Greeter
```

## Database

Each invocation targets a single SQLite database file.

- **Default:** `deeptrace.db` in the current working directory
- **Custom:** `--db <path>` — parent directories are created automatically
- **Multiple games:** Each game gets its own database file (e.g., `games/unity.db`, `games/steamgame.db`)

Databases are self-contained. No cross-database queries are supported.
