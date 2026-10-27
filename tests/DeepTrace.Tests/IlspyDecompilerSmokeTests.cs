using DeepTrace.Decompiler;
using TestLib;
using Xunit;

namespace DeepTrace.Tests;

public class IlspyDecompilerSmokeTests
{
    [Fact]
    public void DecompileType_ReturnsSource()
    {
        var decompiler = new IlspyDecompiler();
        var assemblyPath = typeof(Greeter).Assembly.Location;
        var result = decompiler.DecompileType(assemblyPath, "TestLib.Greeter");

        Assert.NotEmpty(result);
        Assert.Contains("class Greeter", result[0]);
    }

    [Fact]
    public void DecompileMethod_ReturnsSource()
    {
        var decompiler = new IlspyDecompiler();
        var assemblyPath = typeof(Greeter).Assembly.Location;
        var result = decompiler.DecompileMethod(assemblyPath, "TestLib.Greeter", "Greet(System.String)");

        Assert.NotEmpty(result);
        Assert.Contains("_prefix", result);
        Assert.Contains("name", result);
    }

    [Fact]
    public void TryDecompileMethod_ReturnsNull_ForUnknownType()
    {
        var decompiler = new IlspyDecompiler();
        var assemblyPath = typeof(Greeter).Assembly.Location;
        var result = decompiler.TryDecompileMethod(assemblyPath, "NonExistent.Type", "Method()");

        Assert.Null(result);
    }

    [Fact]
    public void TryDecompileType_ReturnsSource()
    {
        var decompiler = new IlspyDecompiler();
        var assemblyPath = typeof(Greeter).Assembly.Location;
        var result = decompiler.DecompileType(assemblyPath, "TestLib.Greeter");

        Assert.NotEmpty(result);
        Assert.Contains("Greet", result[0]);
        Assert.Contains("Hello", result[0]);
    }
}
