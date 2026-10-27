# DeepTrace CLI Skill

Use this skill when working with the DeepTrace CLI to index, search, and investigate .NET assemblies for game mod development.

## When to Use

- User wants to discover assemblies in a directory
- User wants to index assemblies for search
- User wants to search for code by keyword
- User wants to find callers/callees of a method
- User wants to run an AI investigation
- User wants to record runtime observations
- User wants to evaluate retrieval quality
- User wants to list or execute investigation recipes

## CLI Usage

```bash
# Build the project
dotnet build DeepTrace.slnx

# Run commands via dotnet
dotnet run --project src/DeepTrace.Cli -- <command> [args...]
```

## Commands

### `discover <directory>`
Discover managed .NET assemblies in a directory. Excludes System.*, Microsoft.*, Windows.*, and framework assemblies.

### `index <directory>`
Index all discovered assemblies. Creates `deeptrace.db`. Skips unchanged assemblies by hash.

### `search <query> [limit]`
Search indexed symbols by keyword. Results ranked by BM25 relevance. Default limit: 50.

### `methods <assemblyName>`
List all methods in an assembly.

### `method <symbolId>`
Get full method details. Symbol ID format: `Assembly::Namespace.Type::Method::Signature`.

### `types <assemblyName>`
List all types in an assembly.

### `type <assemblyName> <typeFullName>`
Get detailed type info.

### `callers <methodSymbolId>`
Find direct callers of a method.

### `callees <methodSymbolId>`
Find direct callees of a method.

### `references <symbolId>`
Find all references to a symbol (type refs, callers, callees).

### `investigate <question>`
Run AI investigation workflow. Extracts concepts, searches, traverses caller/callee graphs, produces structured report.

### `observe <symbolId> <durationMs> [parametersJson] [returnValueJson]`
Record a runtime observation. Stored in SQLite.

### `runtime <symbolId>`
Show runtime observations for a symbol.

### `recipes [name]`
List all recipes or execute a specific one by name.

### `evaluate <dllPath>`
Measure retrieval quality (precision/recall/F1) against a test assembly.

### `stats`
Show indexer statistics.

## Symbol ID Format

Method: `Assembly::Namespace.Type::Method::Signature`
Example: `TestLib::TestLib.Greeter::Greet::Greet(System.String) : System.String`

Type: `Assembly::Namespace.Type`
Example: `TestLib::TestLib.Greeter`

## Investigation Recipes

Pre-defined patterns for common modding tasks:

- **Find Extension Points** — virtual methods, overrides, public instance methods (HarmonyX patch targets)
- **Trace Initialization** — constructors, static initializers, On* lifecycle methods
- **Find Service Registrations** — factory methods, Create/Build/From patterns, DI registrations
- **Find Type Hierarchies** — base types, interfaces, derived type relationships
- **Find Event Handlers** — event subscriptions, callbacks, handler methods

## Typical Workflow

1. `discover ./GameDirectory` — find assemblies
2. `index ./GameDirectory` — index for search
3. `search "Greeter"` — find relevant symbols
4. `method <symbolId>` — inspect method details
5. `callers <symbolId>` / `callees <symbolId>` — explore relationships
6. `investigate "How does the game greet players?"` — full AI investigation
7. `recipes "Find Extension Points"` — find patch targets
8. `evaluate <dllPath>` — measure search quality

## Full Documentation

- CLI reference: `docs/CLI.md`
- API reference: `docs/API.md`
