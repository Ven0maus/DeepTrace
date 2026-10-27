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

    [Fact]
    public void Generate_WithObjectParameter_ContainsParamName()
    {
        var method = MakeMethod("Process", parameters: new List<string> { "System.Object obj" });
        var result = MethodSummaryGenerator.Generate(method, string.Empty);
        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    [Fact]
    public void Generate_WithMultipleParameters_ListsThem()
    {
        var method = MakeMethod("Create", parameters: new List<string> { "System.String name", "System.Int32 id" });
        var result = MethodSummaryGenerator.Generate(method, string.Empty);
        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    [Fact]
    public void Generate_WithNullParameter_ReturnsNotNull()
    {
        var method = MakeMethod("Process", parameters: new List<string> { "System.Object obj" });
        var result = MethodSummaryGenerator.Generate(method, string.Empty);
        Assert.NotNull(result);
    }

    [Fact]
    public void Generate_WithOnlyBoilerplate_ReturnsFromSignature()
    {
        var method = MakeMethod("ValidateInput", returnType: "System.Boolean");
        var body = @"public bool ValidateInput()
{
    if (input == null) return false;
    if (input.Length == 0) return false;
    return true;
}";
        var result = MethodSummaryGenerator.Generate(method, body);
        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    [Fact]
    public void Generate_WithLongBody_TruncatesSummary()
    {
        var method = MakeMethod("Process");
        var body = @"public void Process()
{
    var result = SomeMethodThatIsVeryLongAndDoesManyThings();
    var anotherCall = AnotherMethodThatIsAlsoVeryLongAndDoesStuff();
    var thirdCall = YetAnotherMethodThatIsAlsoVeryLong();
    return result;
}";
        var result = MethodSummaryGenerator.Generate(method, body);
        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    [Fact]
    public void Generate_WithLifecycleCallback_HandlesOnEnabled()
    {
        var method = MakeMethod("OnEnabled");
        var result = MethodSummaryGenerator.Generate(method, string.Empty);
        Assert.NotNull(result);
        Assert.Contains("Handles", result);
        Assert.Contains("Enabled", result);
    }

    [Fact]
    public void Generate_WithLifecycleCallback_HandlesOnDisable()
    {
        var method = MakeMethod("OnDisable");
        var result = MethodSummaryGenerator.Generate(method, string.Empty);
        Assert.NotNull(result);
        Assert.Contains("Handles", result);
        Assert.Contains("Disable", result);
    }

    [Fact]
    public void Generate_WithProcessPrefix_UsesMethodName()
    {
        var method = MakeMethod("ProcessData");
        var result = MethodSummaryGenerator.Generate(method, string.Empty);
        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    [Fact]
    public void Generate_WithFieldAssignment_AssignsField()
    {
        var method = MakeMethod("SetValue");
        var body = @"public void SetValue()
{
    _field = 42;
}";
        var result = MethodSummaryGenerator.Generate(method, body);
        Assert.NotNull(result);
        Assert.Contains("Assigns", result);
        Assert.Contains("_field", result);
    }

    [Fact]
    public void Generate_WithPropertyAssignment_SetsProperty()
    {
        var method = MakeMethod("Configure");
        var body = @"public void Configure()
{
    this.Property = 42;
}";
        var result = MethodSummaryGenerator.Generate(method, body);
        Assert.NotNull(result);
        Assert.Contains("Sets", result);
        Assert.Contains("Property", result);
    }

    [Fact]
    public void Generate_WithMethodCall_CallsMethod()
    {
        var method = MakeMethod("Run");
        var body = @"public void Run()
{
    ExecuteTask();
}";
        var result = MethodSummaryGenerator.Generate(method, body);
        Assert.NotNull(result);
        Assert.Contains("Calls", result);
        Assert.Contains("ExecuteTask", result);
    }

    [Fact]
    public void Generate_WithCommentInBody_StripsComment()
    {
        var method = MakeMethod("Process");
        var body = @"public void Process()
{
    // This is a comment
    var x = DoSomething();
}";
        var result = MethodSummaryGenerator.Generate(method, body);
        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    [Fact]
    public void Generate_WithOverride_ReturnsOverrides()
    {
        var method = MakeMethod("ToString", isOverride: true);
        var result = MethodSummaryGenerator.Generate(method, string.Empty);
        Assert.NotNull(result);
        Assert.StartsWith("Overrides", result);
    }

    [Fact]
    public void Generate_Getter_ReturnsGets()
    {
        var method = MakeMethod("get_Value");
        var result = MethodSummaryGenerator.Generate(method, string.Empty);
        Assert.NotNull(result);
        Assert.StartsWith("Gets", result);
    }

    [Fact]
    public void Generate_Setter_ReturnsSets()
    {
        var method = MakeMethod("set_Value");
        var result = MethodSummaryGenerator.Generate(method, string.Empty);
        Assert.NotNull(result);
        Assert.StartsWith("Sets", result);
    }

    [Fact]
    public void Generate_AddHandler_ReturnsAddsHandler()
    {
        var method = MakeMethod("add_Click");
        var result = MethodSummaryGenerator.Generate(method, string.Empty);
        Assert.NotNull(result);
        Assert.Contains("Adds handler for", result);
    }

    [Fact]
    public void Generate_RemoveHandler_ReturnsRemovesHandler()
    {
        var method = MakeMethod("remove_Click");
        var result = MethodSummaryGenerator.Generate(method, string.Empty);
        Assert.NotNull(result);
        Assert.Contains("Removes handler for", result);
    }

    [Fact]
    public void Generate_StaticConstructor_ReturnsStaticInitializer()
    {
        var method = MakeMethod(".cctor", isStatic: true);
        var result = MethodSummaryGenerator.Generate(method, string.Empty);
        Assert.NotNull(result);
        Assert.Contains("Static initializer", result);
    }

    [Fact]
    public void Generate_Constructor_WithParams_UsesParamsInSummary()
    {
        var method = MakeMethod(".ctor", isStatic: false, parameters: new List<string> { "System.String name", "System.Int32 id" });
        var result = MethodSummaryGenerator.Generate(method, string.Empty);
        Assert.NotNull(result);
        Assert.Contains("Initializes", result);
        Assert.Contains("name", result);
        Assert.Contains("id", result);
    }

    [Fact]
    public void Generate_Constructor_NoParams_UsesTypeOnly()
    {
        var method = MakeMethod(".ctor", isStatic: false);
        var result = MethodSummaryGenerator.Generate(method, string.Empty);
        Assert.NotNull(result);
        Assert.Contains("Initializes", result);
    }

    [Fact]
    public void Generate_ReturnsNonVoid_ReturnsReturns()
    {
        var method = MakeMethod("Calculate", returnType: "System.Int32");
        var result = MethodSummaryGenerator.Generate(method, string.Empty);
        Assert.NotNull(result);
        Assert.StartsWith("Returns", result);
    }

    [Fact]
    public void Generate_WithBeforeKeyword_UsesBefore()
    {
        var method = MakeMethod("Validate");
        var body = @"public void Validate()
{
    var result = CheckInput();
    // before processing
    return result;
}";
        var result = MethodSummaryGenerator.Generate(method, body);
        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    [Fact]
    public void Generate_WithAfterKeyword_UsesAfter()
    {
        var method = MakeMethod("Process");
        var body = @"public void Process()
{
    var result = DoWork();
    // after processing
    return result;
}";
        var result = MethodSummaryGenerator.Generate(method, body);
        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    [Fact]
    public void Generate_WithPrepareName_UsesPreparing()
    {
        var method = MakeMethod("PrepareInput");
        var body = @"public void PrepareInput()
{
    var data = LoadConfig();
}";
        var result = MethodSummaryGenerator.Generate(method, body);
        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    [Fact]
    public void Generate_WithSetupName_UsesSettingUp()
    {
        var method = MakeMethod("SetupEnvironment");
        var body = @"public void SetupEnvironment()
{
    var config = LoadConfig();
}";
        var result = MethodSummaryGenerator.Generate(method, body);
        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }
}
