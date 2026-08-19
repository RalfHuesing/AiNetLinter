#nullable enable

using AiNetLinter.Metrics;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace AiNetLinter.FastTests.Metrics;

[Trait("Category", "Unit")]
public sealed class ComplexityCalculatorTests
{
    [Fact]
    public void GetCyclomaticComplexity_SyntaxNode_CalculatesForConstructor()
    {
        var tree = CSharpSyntaxTree.ParseText("""
            public class TestClass
            {
                public TestClass(int x)
                {
                    if (x > 0)
                    {
                        var y = x > 10 ? 1 : 2;
                    }
                }
            }
            """);

        var ctor = tree.GetRoot().DescendantNodes().OfType<ConstructorDeclarationSyntax>().Single();

        var cc = ComplexityCalculator.GetCyclomaticComplexity(ctor);

        // Base 1 + if (1) + ternary (1) = 3
        Assert.Equal(3, cc);
    }

    [Fact]
    public void GetCognitiveComplexity_SyntaxNode_CalculatesForAccessor()
    {
        var tree = CSharpSyntaxTree.ParseText("""
            public class TestClass
            {
                private int _val;
                public int Val
                {
                    get
                    {
                        if (_val > 0)
                        {
                            if (_val > 10) return _val;
                        }
                        return 0;
                    }
                }
            }
            """);

        var accessor = tree.GetRoot().DescendantNodes().OfType<AccessorDeclarationSyntax>().First();

        var cogC = ComplexityCalculator.GetCognitiveComplexity(accessor);

        // Outer if (1) + inner if (1 + nesting 1) = 3
        Assert.Equal(3, cogC);
    }
}
