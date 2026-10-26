using System.Collections.Generic;
using DeepTrace.Indexing;
using DeepTrace.Core.Models;
using Xunit;

namespace DeepTrace.Tests;

public class MethodSummaryGeneratorTests
{
    private MethodInfo MakeMethod(
        string name = "TestMethod",
        string declaringType = "TestNS.MyClass",
        bool isStatic = false,
        bool isOverride = false,
        IReadOnlyList<string>? parameters = null,
        string? returnType = null,
        IReadOnlyList<string>? attributes = null)
    {
        return new MethodInfo
        {
            FullName = $"{declaringType}.{name}",
            Name = name,
            DeclaringTypeFullName = declaringType,
            AssemblyName = "TestAssembly",
            IsStatic = isStatic,
            IsOverride = isOverride,
            Parameters = parameters ?? (IReadOnlyList<string>)new List<string>(),
            ReturnType = returnType ?? "System.Void",
            Attributes = attributes ?? (IReadOnlyList<string>)new List<string>(),
        };
    }

    [Fact]
    public void Generate_EmptyBody_ReturnsFromSignature()
    {
        var method = MakeMethod("Greet");
        var result = MethodSummaryGenerator.Generate(method, string.Empty);
        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    [Fact]
    public void Generate_NullBody_ReturnsFromSignature()
    {
        var method = MakeMethod("Greet");
        var result = MethodSummaryGenerator.Generate(method, null);
        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    [Fact]
    public void Generate_ConstructorWithBody_ReturnsInitializerSummary()
    {
        var method = MakeMethod(".ctor", "TestNS.Greeter", parameters: new List<string> { "System.String name" });
        var body = @"public Greeter(string name)
{
    _name = name;
}";
        var result = MethodSummaryGenerator.Generate(method, body);
        Assert.Contains("Assigns", result);
        Assert.Contains("_name", result);
    }

    [Fact]
    public void Generate_StaticInitializer_ReturnsStaticInitSummary()
    {
        var method = MakeMethod(".cctor", "TestNS.Factory", isStatic: true);
        var result = MethodSummaryGenerator.Generate(method, string.Empty);
        Assert.Contains("Static initializer", result);
    }

    [Fact]
    public void Generate_Getter_ReturnsGetsSummary()
    {
        var method = MakeMethod("get_Value", returnType: "System.Int32");
        var result = MethodSummaryGenerator.Generate(method, string.Empty);
        Assert.StartsWith("Gets Value", result);
    }

    [Fact]
    public void Generate_Setter_ReturnsSetsSummary()
    {
        var method = MakeMethod("set_Value", returnType: "System.Void");
        var result = MethodSummaryGenerator.Generate(method, string.Empty);
        Assert.StartsWith("Sets Value", result);
    }

    [Fact]
    public void Generate_ReturnStatement_ReturnsReturnsSummary()
    {
        var method = MakeMethod("GetCount", returnType: "System.Int32");
        var body = @"public int GetCount()
{
    return _count;
}";
        var result = MethodSummaryGenerator.Generate(method, body);
        Assert.Contains("Returns", result);
        Assert.Contains("_count", result);
    }

    [Fact]
    public void Generate_MethodCall_ReturnsCallsSummary()
    {
        var method = MakeMethod("Process");
        var body = @"public void Process()
{
    ValidateInput();
}";
        var result = MethodSummaryGenerator.Generate(method, body);
        Assert.Contains("Calls ValidateInput", result);
    }

    [Fact]
    public void Generate_LifecycleCallback_ReturnsHandlesSummary()
    {
        var method = MakeMethod("OnEnabled", attributes: new List<string> { "[EventHandler]" });
        var result = MethodSummaryGenerator.Generate(method, string.Empty);
        Assert.Contains("Handles", result);
    }

    [Fact]
    public void Generate_Override_ReturnsOverridesSummary()
    {
        var method = MakeMethod("ToString", isOverride: true, returnType: "System.String");
        var result = MethodSummaryGenerator.Generate(method, string.Empty);
        Assert.Contains("Overrides base ToString", result);
    }

    [Fact]
    public void Generate_BoilerplateLines_SkipsNullChecks()
    {
        var method = MakeMethod("Validate", returnType: "System.Boolean");
        var body = @"public bool Validate()
{
    if (_value == null) return false;
    if (string.IsNullOrEmpty(_value)) return false;
    return true;
}";
        var result = MethodSummaryGenerator.Generate(method, body);
        Assert.Contains("Returns", result);
    }

    [Fact]
    public void Generate_SummaryTruncatedAtMaxLength()
    {
        var method = MakeMethod("SetVeryLongPropertyName", returnType: "System.Void");
        var body = @"public void SetVeryLongPropertyName()
{
    _veryLongPropertyName = someVeryLongValueThatWillCauseTruncation;
}";
        var result = MethodSummaryGenerator.Generate(method, body);
        Assert.True(result.Length <= 120);
    }

    [Fact]
    public void Generate_PropertyAssignment_ReturnsSetsSummary()
    {
        var method = MakeMethod("Configure");
        var body = @"public void Configure()
{
    settings.Timeout = 30;
}";
        var result = MethodSummaryGenerator.Generate(method, body);
        Assert.Contains("Sets", result);
        Assert.Contains("Timeout", result);
    }

    [Fact]
    public void Generate_CommentedLine_SkipsComment()
    {
        var method = MakeMethod("TestMethod");
        var body = @"public void TestMethod()
{
    // This is a comment
    var x = 42;
}";
        var result = MethodSummaryGenerator.Generate(method, body);
        Assert.NotEmpty(result);
    }

    [Fact]
    public void Generate_FactoryMethod_ReturnsCreatesSummary()
    {
        var method = MakeMethod("CreateInstance", isStatic: true, returnType: "TestNS.Object");
        var result = MethodSummaryGenerator.Generate(method, string.Empty);
        Assert.Contains("Creates", result);
    }
}
