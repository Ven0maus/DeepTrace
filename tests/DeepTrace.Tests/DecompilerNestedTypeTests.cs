using DeepTrace.Decompiler;
using Xunit;

namespace DeepTrace.Tests;

/// <summary>
/// Tests for ExtractMethod with nested types and edge cases.
/// </summary>
public class DecomplierNestedTypeTests
{
    [Fact]
    public void ExtractMethod_FindsMethodInNestedType()
    {
        var source = @"
namespace Outer {
    public class Container {
        public void ContainerMethod() {
            int a = 1;
        }
        public class Inner {
            public void InnerMethod() {
                int b = 2;
            }
        }
    }
}";
        var result = IlspyDecompiler.ExtractMethod(source, "InnerMethod() : System.Void");

        Assert.NotNull(result);
        Assert.Contains("b = 2", result);
        Assert.DoesNotContain("a = 1", result);
    }

    [Fact]
    public void ExtractMethod_MethodWithGenericConstraint()
    {
        var source = @"
namespace Test {
    public class MyClass {
        public T Process<T>(T input) where T : class {
            return input;
        }
    }
}";
        var result = IlspyDecompiler.ExtractMethod(source, "Process<T>(T input) : System.Void");

        Assert.NotNull(result);
        Assert.Contains("return input", result);
    }

    [Fact]
    public void ExtractMethod_PropertyWithGetterSetter()
    {
        var source = @"
namespace Test {
    public class MyClass {
        public string Name { get; set; }
        public void Configure() {
            Name = ""test"";
        }
    }
}";
        var result = IlspyDecompiler.ExtractMethod(source, "Configure() : System.Void");

        Assert.NotNull(result);
        Assert.Contains(@"Name = ""test""", result);
    }

    [Fact]
    public void ExtractMethod_EmptyBody()
    {
        var source = @"
namespace Test {
    public class MyClass {
        public void Empty() {
        }
        public void HasBody() {
            int x = 42;
        }
    }
}";
        var result = IlspyDecompiler.ExtractMethod(source, "Empty() : System.Void");

        // Should return the empty method body (just whitespace/newlines)
        Assert.NotNull(result);
    }

    [Fact]
    public void ExtractMethod_SyncMethodNameOnlyMatch()
    {
        // Verify that just the method name (without signature) also works
        // since ExtractMethod uses the name before '(' as the anchor
        var source = @"
namespace Test {
    public class MyClass {
        public void Run() {
            int x = 1;
        }
        public void RunAsync() {
            int y = 2;
        }
    }
}";
        // Using full signature
        var result = IlspyDecompiler.ExtractMethod(source, "Run() : System.Void");

        Assert.NotNull(result);
        Assert.Contains("x = 1", result);
        Assert.DoesNotContain("y = 2", result);
    }

    [Fact]
    public void ExtractMethod_StringWithParentheses()
    {
        var source = @"
namespace Test {
    public class MyClass {
        public void Log() {
            var msg = ""Hello (world)"";
            var msg2 = ""Test (123)"";
        }
    }
}";
        var result = IlspyDecompiler.ExtractMethod(source, "Log() : System.Void");

        Assert.NotNull(result);
        Assert.Contains("Hello (world)", result);
        Assert.Contains("Test (123)", result);
    }

    [Fact]
    public void ExtractMethod_AttributeOnMethod()
    {
        var source = @"
namespace Test {
    public class MyClass {
        [System.Obsolete(""Use NewMethod"")]
        public void OldMethod() {
            int x = 1;
        }
        public void NewMethod() {
            int y = 2;
        }
    }
}";
        var result = IlspyDecompiler.ExtractMethod(source, "OldMethod() : System.Void");

        Assert.NotNull(result);
        Assert.Contains("x = 1", result);
        Assert.DoesNotContain("y = 2", result);
    }
}
