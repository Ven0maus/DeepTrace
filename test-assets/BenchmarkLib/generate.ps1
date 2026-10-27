# Generate 100 CS files for benchmark testing
$OutputDir = Join-Path $PSScriptRoot "Generated"
New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null

$Namespaces = @("Data", "Services", "Models", "Handlers", "Controllers", "Managers", "Repositories", "Validators", "Formatters", "Parsers")
$Prefixes = @("Process", "Compute", "Validate", "Transform", "Calculate", "Fetch", "Update", "Render", "Parse", "Handle")
$Suffixes = @("Record", "Entry", "Item", "Data", "Result", "Config", "Settings", "Info", "State", "Value")

# Each method: name, return type, param type, param name, body
# Param name is fixed so body references match
$Methods = @(
    @{ Name="Execute"; Ret="string"; Param="string"; PName="arg"; Body='return _field0 ?? arg;' },
    @{ Name="Run"; Ret="int"; Param="int"; PName="arg"; Body='return _field1 + arg;' },
    @{ Name="Perform"; Ret="double"; Param="double"; PName="arg"; Body='return _field2 + arg;' },
    @{ Name="Operate"; Ret="bool"; Param="bool"; PName="arg"; Body='return _field3 && arg;' },
    @{ Name="ExecuteAsync"; Ret="DateTime"; Param="int"; PName="arg"; Body='return _field4.AddSeconds(arg);' },
    @{ Name="ProcessInternal"; Ret="string"; Param="int"; PName="arg"; Body='return _field0 + arg.ToString();' },
    @{ Name="GetResult"; Ret="int"; Param="double"; PName="arg"; Body='return _field1 + (int)arg;' },
    @{ Name="Initialize"; Ret="double"; Param="bool"; PName="arg"; Body='return _field2 + (arg ? 1.0 : 0.0);' },
    @{ Name="Dispose"; Ret="bool"; Param="DateTime"; PName="arg"; Body='return arg > DateTime.MinValue;' },
    @{ Name="ToString"; Ret="string"; Param="Guid"; PName="arg"; Body='return _field0 ?? arg.ToString();' }
)

$FieldTypes = @("string", "int", "double", "bool", "DateTime")

for ($i = 0; $i -lt 100; $i++) {
    $ns = $Namespaces[$i % $Namespaces.Length]
    $className = "$($Prefixes[$i % $Prefixes.Length])$($Suffixes[$i % $Suffixes.Length])$i"
    $fileName = "$className.cs"

    $sb = New-Object System.Text.StringBuilder
    $sb.AppendLine("namespace BenchmarkLib.$ns;") | Out-Null
    $sb.AppendLine() | Out-Null
    $sb.AppendLine("public class $className") | Out-Null
    $sb.AppendLine("{") | Out-Null

    # Fields
    for ($f = 0; $f -lt 5; $f++) {
        $sb.AppendLine("    private " + $FieldTypes[$f] + " _field" + $f + ";") | Out-Null
    }
    $sb.AppendLine() | Out-Null

    # Properties
    for ($p = 0; $p -lt 5; $p++) {
        $sb.AppendLine("    public " + $FieldTypes[$p] + " Property" + $p + " { get; set; }") | Out-Null
    }
    $sb.AppendLine() | Out-Null

    # Constructor
    $sb.AppendLine("    public " + $className + "()") | Out-Null
    $sb.AppendLine("    {") | Out-Null
    $sb.AppendLine("        _field0 = string.Empty;") | Out-Null
    $sb.AppendLine("        _field1 = 0;") | Out-Null
    $sb.AppendLine("        _field2 = 0.0;") | Out-Null
    $sb.AppendLine("        _field3 = false;") | Out-Null
    $sb.AppendLine("        _field4 = DateTime.UtcNow;") | Out-Null
    $sb.AppendLine("    }") | Out-Null
    $sb.AppendLine() | Out-Null

    # Methods
    for ($m = 0; $m -lt 10; $m++) {
        $method = $Methods[$m]
        $sb.AppendLine("    public " + $method.Ret + " " + $method.Name + "(" + $method.Param + " " + $method.PName + ")") | Out-Null
        $sb.AppendLine("    {") | Out-Null
        $sb.AppendLine("        " + $method.Body) | Out-Null
        $sb.AppendLine("    }") | Out-Null
        $sb.AppendLine() | Out-Null
    }

    $sb.AppendLine("}") | Out-Null

    $content = $sb.ToString()
    $filePath = Join-Path $OutputDir $fileName
    [System.IO.File]::WriteAllText($filePath, $content, [System.Text.Encoding]::UTF8)
}

Write-Host "Generated $($Namespaces.Length * 10) files in $OutputDir"
