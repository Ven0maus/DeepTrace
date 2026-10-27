using DeepTrace.Core.Models;
using DeepTrace.Core.Retrieval;
using DeepTrace.Core.Runtime;
using DeepTrace.Decompiler;
using DeepTrace.Indexing;

// Parse --db flag (default: deeptrace.db)
string dbPath = "deeptrace.db";
var filteredArgs = new List<string>();
for (int i = 0; i < args.Length; i++)
{
    if (args[i] == "--db" && i + 1 < args.Length)
    {
        dbPath = args[i + 1];
        i++; // skip the path argument
    }
    else
    {
        filteredArgs.Add(args[i]);
    }
}
args = filteredArgs.ToArray();

var indexer = new SqliteIndexer(dbPath, new IlspyDecompiler());

if (args.Length == 0)
{
    PrintUsage(dbPath);
    return;
}

var command = args[0].ToLowerInvariant();

switch (command)
{
    case "discover":
        {
            if (args.Length < 2)
            {
                Console.Error.WriteLine("Usage: deeptrace [--db <path>] discover <directory>");
                return;
            }
            var assemblies = indexer.DiscoverAssemblies(args[1]);
            Console.WriteLine($"Discovered {assemblies.Count} assemblies:");
            foreach (var a in assemblies)
                Console.WriteLine($"  {a.Name} ({a.Location})");
            break;
        }

    case "index":
        {
            if (args.Length < 2)
            {
                Console.Error.WriteLine("Usage: deeptrace [--db <path>] index <directory>");
                return;
            }
            var assemblies = indexer.DiscoverAssemblies(args[1]);
            var indexed = 0;
            foreach (var assembly in assemblies)
            {
                if (indexer.IsAssemblyIndexed(assembly.Name))
                {
                    var hash = indexer.GetAssemblyHash(assembly.Location);
                    if (hash == assembly.Hash)
                    {
                        Console.WriteLine($"  Skip (unchanged): {assembly.Name}");
                        continue;
                    }
                }
                Console.WriteLine($"  Indexing: {assembly.Name}...");
                indexer.IndexAssembly(assembly);
                indexed++;
            }
            Console.WriteLine($"Indexed {indexed}/{assemblies.Count} assemblies.");
            break;
        }

    case "search":
        {
            if (args.Length < 2)
            {
                Console.Error.WriteLine("Usage: deeptrace [--db <path>] search <query> [limit]");
                return;
            }
            var query = args[1];
            var limit = args.Length > 2 ? int.Parse(args[2]) : 50;
            var results = indexer.Search(query, limit);
            Console.WriteLine($"Found {results.Count} results for '{query}':");
            foreach (var r in results)
                PrintResult(r);
            break;
        }

    case "methods":
        {
            if (args.Length < 2)
            {
                Console.Error.WriteLine("Usage: deeptrace [--db <path>] methods <assemblyName>");
                return;
            }
            var methods = indexer.GetMethods(args[1]);
            Console.WriteLine($"Assembly '{args[1]}' has {methods.Count} methods:");
            foreach (var m in methods)
                Console.WriteLine($"  {m.FullName} ({m.Signature[..Math.Min(80, m.Signature.Length)]})");
            break;
        }

    case "method":
        {
            if (args.Length < 2)
            {
                Console.Error.WriteLine("Usage: deeptrace [--db <path>] method <symbolId>");
                return;
            }
            var method = indexer.GetMethod(args[1]);
            if (method == null)
            {
                Console.WriteLine($"Method not found: {args[1]}");
                return;
            }
            PrintMethodInfo(method);
            break;
        }

    case "types":
        {
            if (args.Length < 2)
            {
                Console.Error.WriteLine("Usage: deeptrace [--db <path>] types <assemblyName>");
                return;
            }
            var types = indexer.GetTypes(args[1]);
            Console.WriteLine($"Assembly '{args[1]}' has {types.Count} types:");
            foreach (var t in types)
                Console.WriteLine($"  {t.FullName} (base: {t.BaseTypeFullName ?? "none"}, methods: {t.Methods.Count})");
            break;
        }

    case "type":
        {
            if (args.Length < 3)
            {
                Console.Error.WriteLine("Usage: deeptrace [--db <path>] type <assemblyName> <typeFullName>");
                return;
            }
            var type = indexer.GetTypeInfo(args[1], args[2]);
            if (type == null)
            {
                Console.WriteLine($"Type not found: {args[2]}");
                return;
            }
            PrintTypeInfo(type);
            break;
        }

    case "callers":
        {
            if (args.Length < 2)
            {
                Console.Error.WriteLine("Usage: deeptrace [--db <path>] callers <methodSymbolId>");
                return;
            }
            var callers = indexer.GetCallers(args[1]);
            Console.WriteLine($"Callers of '{args[1]}':");
            foreach (var c in callers)
                Console.WriteLine($"  {c}");
            break;
        }

    case "callees":
        {
            if (args.Length < 2)
            {
                Console.Error.WriteLine("Usage: deeptrace [--db <path>] callees <methodSymbolId>");
                return;
            }
            var callees = indexer.GetCallees(args[1]);
            Console.WriteLine($"Callees of '{args[1]}':");
            foreach (var c in callees)
                Console.WriteLine($"  {c}");
            break;
        }

    case "references":
        {
            if (args.Length < 2)
            {
                Console.Error.WriteLine("Usage: deeptrace [--db <path>] references <symbolId>");
                return;
            }
            var results = indexer.SearchReferences(args[1]);
            Console.WriteLine($"References for '{args[1]}': {results.Count}");
            foreach (var r in results)
                PrintResult(r);
            break;
        }

    case "investigate":
        {
            if (args.Length < 2)
            {
                Console.Error.WriteLine("Usage: deeptrace [--db <path>] investigate <question>");
                return;
            }
            var runtimeObserver = new DeepTrace.Core.Runtime.InMemoryRuntimeObserver();
            // Pre-populate from SQLite storage
            foreach (var obs in indexer.GetAllRuntimeObservations())
            {
                runtimeObserver.RecordCall(obs.SymbolId, obs.FullyQualifiedName, obs.Assembly,
                    obs.Parameters, obs.ReturnValue, obs.DurationMs, obs.Depth);
            }
            var tools = new DeepTrace.Core.Ai.InvestigationTools(indexer);
            var workflow = new DeepTrace.Core.Ai.InvestigationWorkflow(tools, runtimeObserver);
            var options = new DeepTrace.Core.Ai.InvestigationOptions
            {
                MaxSearches = 6,
                MaxDepth = 2,
                ResultLimit = 10,
                TokenBudget = 8000,
            };
            var report = workflow.Investigate(args[1], options);
            PrintReport(report);
            break;
        }

    case "observe":
        {
            if (args.Length < 3)
            {
                Console.Error.WriteLine("Usage: deeptrace [--db <path>] observe <symbolId> <durationMs> [parametersJson] [returnValueJson]");
                return;
            }
            var symbolId = args[1];
            var durationMs = args.Length > 2 ? double.Parse(args[2]) : 0;
            var parameters = args.Length > 3 ? args[3] : null;
            var returnValue = args.Length > 4 ? args[4] : null;
            var observer = new DeepTrace.Core.Runtime.InMemoryRuntimeObserver();
            var assembly = "Unknown";
            var fqName = symbolId.Split("::").LastOrDefault() ?? symbolId;
            observer.RecordCall(symbolId, fqName, assembly, parameters, returnValue, durationMs);
            var observation = new RuntimeObservation
            {
                Id = observer.GetAllObservations().First().Id,
                SymbolId = symbolId,
                FullyQualifiedName = fqName,
                Assembly = assembly,
                Timestamp = observer.GetAllObservations().First().Timestamp,
                Depth = 0,
                Parameters = parameters,
                ReturnValue = returnValue,
                DurationMs = durationMs,
            };
            indexer.StoreRuntimeObservation(observation);
            Console.WriteLine($"Observed: {symbolId} ({durationMs}ms)");
            break;
        }

    case "runtime":
        {
            if (args.Length < 2)
            {
                Console.Error.WriteLine("Usage: deeptrace [--db <path>] runtime <symbolId>");
                return;
            }
            var observations = indexer.GetRuntimeObservations(args[1]);
            Console.WriteLine($"Runtime observations for '{args[1]}': {observations.Count} recorded");
            foreach (var obs in observations)
            {
                Console.WriteLine($"  [{obs.Timestamp}] depth={obs.Depth} duration={obs.DurationMs:F1}ms");
                if (!string.IsNullOrEmpty(obs.Parameters))
                    Console.WriteLine($"    Params: {obs.Parameters}");
                if (!string.IsNullOrEmpty(obs.ReturnValue))
                    Console.WriteLine($"    Return: {obs.ReturnValue}");
            }
            break;
        }

    case "recipes":
        {
            if (args.Length < 2)
            {
                Console.WriteLine("Available recipes:");
                foreach (var r in DeepTrace.Core.Ai.InvestigationRecipes.Recipes)
                    Console.WriteLine($"  {r.Name}: {r.Description}");
                return;
            }
            var recipeName = args[1];
            var recipe = DeepTrace.Core.Ai.InvestigationRecipes.Recipes
                .FirstOrDefault(r => r.Name.Equals(recipeName, System.StringComparison.OrdinalIgnoreCase));
            if (recipe == null)
            {
                Console.Error.WriteLine($"Unknown recipe: {recipeName}");
                Console.WriteLine("Available:");
                foreach (var r in DeepTrace.Core.Ai.InvestigationRecipes.Recipes)
                    Console.WriteLine($"  {r.Name}");
                return;
            }
            Console.WriteLine(DeepTrace.Core.Ai.InvestigationRecipes.ExecuteAsText(recipe, indexer));
            break;
        }

    case "evaluate":
        {
            if (args.Length < 2)
            {
                Console.Error.WriteLine("Usage: deeptrace [--db <path>] evaluate <dllPath>");
                return;
            }
            var dllPath = args[1];
            if (!File.Exists(dllPath))
            {
                Console.Error.WriteLine($"File not found: {dllPath}");
                return;
            }
            // Build a temporary index for evaluation
            var evalDir = Path.Combine(Path.GetTempPath(), $"deeptrace_eval_{Guid.NewGuid()}");
            Directory.CreateDirectory(evalDir);
            var evalDb = Path.Combine(evalDir, "eval.db");
            File.Copy(dllPath, Path.Combine(evalDir, "eval.dll"), overwrite: true);
            var evalIndexer = new SqliteIndexer(evalDb, new IlspyDecompiler());
            var assemblies = evalIndexer.DiscoverAssemblies(evalDir);
            foreach (var asm in assemblies)
                evalIndexer.IndexAssembly(asm);

            try
            {
                // Build gold standard from actual indexed methods
                var greeterMethods = evalIndexer.GetMethods(assemblies[0].Name)
                    .Where(m => m.DeclaringTypeFullName.Contains("Greeter"))
                    .Select(m => m.SymbolId)
                    .ToList();
                var greetMethod = evalIndexer.GetMethods(assemblies[0].Name)
                    .FirstOrDefault(m => m.Name == "Greet");

                var evaluations = new List<(string, IReadOnlyList<string>)>
                {
                    ("Greeter", greeterMethods),
                    ("Greet", greetMethod != null ? new List<string> { greetMethod.SymbolId } : new List<string>()),
                };

                var report = RetrievalEvaluator.Evaluate(evaluations, evalIndexer);
                Console.WriteLine(RetrievalEvaluator.FormatReport(report));
            }
            finally
            {
                evalIndexer.Dispose();
            }
            Directory.Delete(evalDir, true);
            break;
        }

    case "stats":
        {
            Console.WriteLine($"Database: {dbPath}");
            Console.WriteLine($"Decompiler: IlspyDecompiler");
            break;
        }

    default:
        Console.Error.WriteLine($"Unknown command: {command}");
        PrintUsage(dbPath);
        break;
}

static void PrintUsage(string currentDb)
{
    Console.WriteLine("Usage: deeptrace [--db <path>] <command> [args...]");
    Console.WriteLine();
    Console.WriteLine($"Current database: {currentDb}");
    Console.WriteLine();
    Console.WriteLine("Commands:");
    Console.WriteLine("  discover <directory>              Discover managed assemblies");
    Console.WriteLine("  index <directory>                 Index all assemblies");
    Console.WriteLine("  search <query> [limit]            Search indexed symbols");
    Console.WriteLine("  methods <assemblyName>            List methods in assembly");
    Console.WriteLine("  method <symbolId>                 Get method details");
    Console.WriteLine("  types <assemblyName>              List types in assembly");
    Console.WriteLine("  type <assemblyName> <typeFullName> Get single type info");
    Console.WriteLine("  callers <methodSymbolId>          Find callers of method");
    Console.WriteLine("  callees <methodSymbolId>          Find callees of method");
    Console.WriteLine("  references <symbolId>             Find all references");
    Console.WriteLine("  investigate <question>            Run AI investigation workflow");
    Console.WriteLine("  observe <symbolId> <durationMs>   Record a runtime observation");
    Console.WriteLine("  runtime <symbolId>                Show runtime observations");
    Console.WriteLine("  recipes [name]                    List or execute investigation recipes");
    Console.WriteLine("  evaluate <dllPath>                Measure retrieval quality");
    Console.WriteLine("  stats                             Show indexer stats");
}

static void PrintResult(SearchResult r)
{
    Console.WriteLine($"  [{r.Kind}] {r.FullyQualifiedName}");
    if (!string.IsNullOrEmpty(r.Signature))
        Console.WriteLine($"    Sig: {r.Signature}");
    if (!string.IsNullOrEmpty(r.Excerpt))
        Console.WriteLine($"    {string.Join("\n     ", r.Excerpt.Split('\n'))}");
    Console.WriteLine($"    Assembly: {r.Assembly}");
}

static void PrintMethodInfo(MethodInfo m)
{
    Console.WriteLine($"SymbolId: {m.SymbolId}");
    Console.WriteLine($"Name: {m.FullName}");
    Console.WriteLine($"Signature: {m.Signature}");
    Console.WriteLine($"DeclaringType: {m.DeclaringTypeFullName}");
    Console.WriteLine($"Assembly: {m.AssemblyName}");
    Console.WriteLine($"IsStatic: {m.IsStatic}");
    Console.WriteLine($"IsVirtual: {m.IsVirtual}");
    Console.WriteLine($"IsAbstract: {m.IsAbstract}");
    Console.WriteLine($"IsOverride: {m.IsOverride}");
    if (m.Attributes.Count > 0)
        Console.WriteLine($"Attributes: {string.Join(", ", m.Attributes)}");
    if (m.Parameters.Count > 0)
        Console.WriteLine($"Parameters: {string.Join(", ", m.Parameters)}");
    if (!string.IsNullOrEmpty(m.Body))
    {
        Console.WriteLine("Body:");
        Console.WriteLine(m.Body);
    }
    else
    {
        Console.WriteLine("Body: (not available — decompilation deferred)");
    }
    if (!string.IsNullOrEmpty(m.Summary))
        Console.WriteLine($"Summary: {m.Summary}");
}

static void PrintTypeInfo(TypeInfo t)
{
    Console.WriteLine($"SymbolId: {t.SymbolId}");
    Console.WriteLine($"Name: {t.FullName}");
    Console.WriteLine($"Namespace: {t.Namespace}");
    Console.WriteLine($"Assembly: {t.AssemblyName}");
    Console.WriteLine($"BaseType: {t.BaseTypeFullName ?? "none"}");
    if (t.BaseTypes.Count > 0)
        Console.WriteLine($"BaseTypes: {string.Join(", ", t.BaseTypes)}");
    if (t.DerivedTypes.Count > 0)
        Console.WriteLine($"DerivedTypes: {string.Join(", ", t.DerivedTypes)}");
    if (t.Interfaces.Count > 0)
        Console.WriteLine($"Interfaces: {string.Join(", ", t.Interfaces)}");
    if (t.Attributes.Count > 0)
        Console.WriteLine($"Attributes: {string.Join(", ", t.Attributes)}");
    if (t.TypeReferences.Count > 0)
        Console.WriteLine($"TypeReferences: {string.Join(", ", t.TypeReferences)}");
    Console.WriteLine($"Methods: {t.Methods.Count}");
    foreach (var m in t.Methods)
        Console.WriteLine($"  {m.FullName} ({m.Signature[..Math.Min(60, m.Signature.Length)]})");
    if (t.Fields.Count > 0)
    {
        Console.WriteLine($"Fields: {t.Fields.Count}");
        foreach (var f in t.Fields)
            Console.WriteLine($"  {f.FullName} : {f.Type}");
    }
}

static void PrintReport(DeepTrace.Core.Models.InvestigationReport report)
{
    Console.WriteLine("=== INVESTIGATION REPORT ===");
    Console.WriteLine();
    Console.WriteLine("--- Request Interpretation ---");
    Console.WriteLine(report.RequestInterpretation);
    Console.WriteLine();

    Console.WriteLine("--- Discovered Execution Path ---");
    if (report.DiscoveredExecutionPath.Count == 0)
        Console.WriteLine("  (no methods discovered)");
    else
    {
        foreach (var e in report.DiscoveredExecutionPath)
        {
            var verified = e.IsVerified ? " [VERIFIED]" : " [INFERRED]";
            Console.WriteLine($"  {e.SymbolId}{verified}");
            Console.WriteLine($"    {e.FullyQualifiedName} ({e.Assembly})");
            if (!string.IsNullOrEmpty(e.Relevance))
                Console.WriteLine($"    Reason: {e.Relevance}");
        }
    }
    Console.WriteLine();

    Console.WriteLine("--- Observed Execution Path ---");
    if (report.ObservedExecutionPath.Count == 0)
        Console.WriteLine("  (no runtime observations recorded)");
    else
    {
        foreach (var o in report.ObservedExecutionPath)
        {
            Console.WriteLine($"  {o.FullyQualifiedName} (called {o.CallCount}x, avg {o.AvgDurationMs:F1}ms, depth {o.MinDepth}-{o.MaxDepth})");
        }
    }
    Console.WriteLine();

    Console.WriteLine("--- Required Dependencies & Invariants ---");
    if (report.RequiredDependenciesAndInvariants.Count == 0)
        Console.WriteLine("  (none identified)");
    else
    {
        foreach (var d in report.RequiredDependenciesAndInvariants)
        {
            var resolved = d.IsResolved ? "" : " [UNRESOLVED]";
            Console.WriteLine($"  [{d.Category}] {d.Name}{resolved}");
            if (!string.IsNullOrEmpty(d.Evidence))
                Console.WriteLine($"    Evidence: {d.Evidence}");
        }
    }
    Console.WriteLine();

    Console.WriteLine("--- Candidate Extension Points ---");
    if (report.CandidateExtensionPoints.Count == 0)
        Console.WriteLine("  (none identified)");
    else
    {
        foreach (var ep in report.CandidateExtensionPoints)
        {
            Console.WriteLine($"  {ep.FullyQualifiedName}");
            Console.WriteLine($"    Why: {ep.Reason}");
            Console.WriteLine($"    Runs once: {ep.RunsOnce}");
            if (!string.IsNullOrEmpty(ep.PotentialIssues))
                Console.WriteLine($"    Issues: {ep.PotentialIssues}");
        }
    }
    Console.WriteLine();

    Console.WriteLine("--- Evidence & Confidence ---");
    foreach (var c in report.EvidenceAndConfidence)
        Console.WriteLine($"  [{c.Confidence}] {c.Finding} (source: {c.Source}, needs verification: {c.NeedsVerification})");
    Console.WriteLine();

    Console.WriteLine("--- Implementation Plan ---");
    Console.WriteLine(report.ImplementationPlan);
    Console.WriteLine();

    Console.WriteLine("--- Validation Plan ---");
    Console.WriteLine(report.ValidationPlan);
}
