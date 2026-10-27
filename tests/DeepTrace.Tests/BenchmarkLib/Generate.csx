// Generate 100 CS files, each with a class, 10 methods, fields, and properties
var outputDir = Path.Combine(Path.GetDirectoryName(Environment.GetCommandLineArgs()[0])!, "Generated");
Directory.CreateDirectory(outputDir);

var namespaces = new[] { "Data", "Services", "Models", "Handlers", "Controllers", "Managers", "Repositories", "Validators", "Formatters", "Parsers" };
var prefixes = new[] { "Process", "Compute", "Validate", "Transform", "Calculate", "Fetch", "Update", "Render", "Parse", "Handle" };
var suffixes = new[] { "Record", "Entry", "Item", "Data", "Result", "Config", "Settings", "Info", "State", "Value" };

var random = new Random(42);

for (int i = 0; i < 100; i++)
{
    var ns = namespaces[i % namespaces.Length];
    var className = $"{prefixes[i % prefixes.Length]}{suffixes[i % suffixes.Length]}{i}";
    var fileName = $"{className}.cs";

    var sb = new System.Text.StringBuilder();
    sb.AppendLine($"namespace BenchmarkLib.{ns};");
    sb.AppendLine();
    sb.AppendLine($"public class {className}");
    sb.AppendLine("{");

    // Fields
    var fieldTypes = new[] { "string", "int", "double", "bool", "DateTime", "Guid" };
    for (int f = 0; f < 5; f++)
    {
        var ft = fieldTypes[f % fieldTypes.Length];
        sb.AppendLine($"    private {ft} _field{f};");
    }
    sb.AppendLine();

    // Properties
    for (int p = 0; p < 5; p++)
    {
        var pt = fieldTypes[p % fieldTypes.Length];
        sb.AppendLine($"    public {pt} Property{p} {{ get; set; }}");
    }
    sb.AppendLine();

    // Constructor
    sb.AppendLine($"    public {className}()");
    sb.AppendLine("    {");
    for (int f = 0; f < 5; f++)
    {
        var ft = fieldTypes[f % fieldTypes.Length];
        if (ft == "string") sb.AppendLine($"        _field{f} = string.Empty;");
        else if (ft == "int") sb.AppendLine($"        _field{f} = {random.Next(0, 1000)};");
        else if (ft == "double") sb.AppendLine($"        _field{f} = {random.NextDouble() * 1000:F4};");
        else if (ft == "bool") sb.AppendLine($"        _field{f} = {random.Next(0, 2)} == 1;");
        else if (ft == "DateTime") sb.AppendLine($"        _field{f} = DateTime.UtcNow;");
        else if (ft == "Guid") sb.AppendLine($"        _field{f} = Guid.NewGuid();");
    }
    sb.AppendLine("    }");
    sb.AppendLine();

    // Methods (10 methods with varied signatures)
    var methodNames = new[] { "Execute", "Run", "Perform", "Operate", "ExecuteAsync", "ProcessInternal", "GetResult", "Initialize", "Dispose", "ToString" };
    var methodBodies = new[]
    {
        "return _field0;",
        "return _field1;",
        "return _field2;",
        "return _field3;",
        "return _field4;",
        "return _field0 != null;",
        "return _field1 > 0;",
        "return _field3;",
        "return _field4.ToString();",
        "return $\"{nameof({className})}\";",
    };

    for (int m = 0; m < 10; m++)
    {
        var returnType = fieldTypes[m % fieldTypes.Length];
        var paramName = $"param{random.Next(100, 999)}";
        var paramType = fieldTypes[random.Next(fieldTypes.Length)];

        sb.AppendLine($"    public {returnType} {methodNames[m]}({paramType} {paramName})");
        sb.AppendLine("    {");

        // Add some local variables and logic
        if (returnType == "string")
        {
            sb.AppendLine($"        var local = {paramName};");
            sb.AppendLine($"        return _field0 ?? local;");
        }
        else if (returnType == "int")
        {
            sb.AppendLine($"        var local = {paramName} + 1;");
            sb.AppendLine($"        return _field1 + local;");
        }
        else if (returnType == "double")
        {
            sb.AppendLine($"        var local = {paramName} * 2.0;");
            sb.AppendLine($"        return _field2 + local;");
        }
        else if (returnType == "bool")
        {
            sb.AppendLine($"        var local = {paramName} != default;");
            sb.AppendLine($"        return _field3 && local;");
        }
        else if (returnType == "DateTime")
        {
            sb.AppendLine($"        return _field4.AddSeconds({paramName} > 0 ? 1 : -1);");
        }
        else if (returnType == "Guid")
        {
            sb.AppendLine($"        return _field4;");
        }
        sb.AppendLine("    }");
        sb.AppendLine();
    }

    sb.AppendLine("}");

    File.WriteAllText(Path.Combine(outputDir, fileName), sb.ToString());
}

Console.WriteLine($"Generated {100} files in {outputDir}");
