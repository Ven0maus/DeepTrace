using DeepTrace.Decompiler;
using Xunit;

namespace DeepTrace.Tests;

/// <summary>
/// Tests for IlspyDecompiler.ExtractMethod line-by-line scanning.
/// Validates the fix for the substring-matching bug that returned entire type bodies.
/// </summary>
public class DecomplierExtractMethodTests
{
    private static string? ExtractMethod(string typeSource, string methodSignature)
    {
        return IlspyDecompiler.ExtractMethod(typeSource, methodSignature);
    }

    [Fact]
    public void ExtractMethod_FindsMethodByParenAnchor()
    {
        var source = @"
namespace Test {
    public class MyClass {
        public void MyMethod(int x) {
            return x + 1;
        }
    }
}";
        // Use just the method name with params — ExtractMethod looks for "MyMethod("
        var result = ExtractMethod(source, "MyMethod(int x)");

        Assert.NotNull(result);
        Assert.Contains("x + 1", result);
        Assert.DoesNotContain("namespace Test", result); // Should NOT include surrounding type
    }

    [Fact]
    public void ExtractMethod_RejectsDotPrecedence()
    {
        var source = @"
namespace Test {
    public class MyClass {
        public void MyMethod() {
            base.MyMethod();
            int x = 42;
        }
    }
}";
        var result = ExtractMethod(source, "MyMethod() : System.Void");

        Assert.NotNull(result);
        Assert.Contains("base.MyMethod()", result); // Method body should contain the call
        Assert.Contains("int x = 42", result); // Both lines are in the method body
        Assert.DoesNotContain("namespace Test", result); // Should NOT include surrounding type
    }

    [Fact]
    public void ExtractMethod_MethodNameInStringNotMatched()
    {
        var source = @"
namespace Test {
    public class MyClass {
        public void Process() {
            var name = ""MyMethod"";
            DoSomething();
        }
    }
}";
        var result = ExtractMethod(source, "Process() : System.Void");

        Assert.NotNull(result);
        Assert.Contains("DoSomething()", result);
        Assert.DoesNotContain("namespace Test", result);
    }

    [Fact]
    public void ExtractMethod_EmptyMethodNameReturnsNull()
    {
        var source = @"
namespace Test {
    public class MyClass {
        public void Method() { }
    }
}";
        // Empty method name (like "() : System.Void" from old indexer)
        var result = ExtractMethod(source, "() : System.Void");

        // ExtractMethod returns null when paren is at position 0 (no method name before it)
        // DecompileMethod wraps null → string.Empty
        Assert.Null(result);
    }

    [Fact]
    public void ExtractMethod_ParenInDefaultParam()
    {
        var source = @"
namespace Test {
    public class MyClass {
        public void Configure(string name = ""default"", int count = 5) {
            int total = count;
        }
    }
}";
        var sig = @"Configure(string name = ""default"", int count = 5) : System.Void";
        var result = ExtractMethod(source, sig);

        Assert.NotNull(result);
        Assert.Contains("total = count", result);
    }

    [Fact]
    public void ExtractMethod_MultipleMethodsOnlyReturnsMatchedOne()
    {
        var source = @"
namespace Test {
    public class MyClass {
        public void First() {
            int a = 1;
        }
        public void Second() {
            int b = 2;
        }
    }
}";
        var result = ExtractMethod(source, "Second() : System.Void");

        Assert.NotNull(result);
        Assert.Contains("b = 2", result);
        Assert.DoesNotContain("a = 1", result);
    }

    [Fact]
    public void ExtractMethod_NestedBracesHandledCorrectly()
    {
        var source = @"
namespace Test {
    public class MyClass {
        public void Complex() {
            if (true) {
                foreach (var x in list) {
                    Console.WriteLine(x);
                }
            }
        }
    }
}";
        var result = ExtractMethod(source, "Complex() : System.Void");

        Assert.NotNull(result);
        Assert.Contains("Console.WriteLine(x)", result);
        Assert.DoesNotContain("namespace Test", result);
    }

    [Fact]
    public void ExtractMethod_GenericMethod()
    {
        var source = @"
namespace Test {
    public class MyClass {
        public T Process<T>(T input) {
            return input;
        }
    }
}";
        var result = ExtractMethod(source, "Process<T>(T input) : System.Void");

        // Should find the method even with generic params
        Assert.NotNull(result);
        Assert.Contains("return input", result);
    }
}
