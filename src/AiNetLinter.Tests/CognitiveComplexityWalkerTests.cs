using Xunit;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using AiNetLinter.Metrics;

namespace AiNetLinter.Tests;

public sealed class CognitiveComplexityWalkerTests
{
    [Fact]
    public void GetCognitiveComplexity_WithFlatCode_ReturnsZero()
    {
        const string source = @"
class Test
{
    void Flat()
    {
        int a = 1;
        int b = 2;
    }
}";
        var tree = CSharpSyntaxTree.ParseText(source);
        var root = tree.GetRoot();
        var method = root.DescendantNodes().OfType<MethodDeclarationSyntax>().Single();

        var complexity = ComplexityCalculator.GetCognitiveComplexity(method);
        Assert.Equal(0, complexity);
    }
}
