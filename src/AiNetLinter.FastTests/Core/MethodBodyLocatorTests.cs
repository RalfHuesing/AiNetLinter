#nullable enable

using System.Linq;
using AiNetLinter.Core.DuplicateDetection;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace AiNetLinter.FastTests.Core;

[Trait("Category", "Unit")]
public sealed class MethodBodyLocatorTests
{
    [Fact]
    public void GetBody_ReturnsBodiesForSupportedMethodDeclarations()
    {
        const string source = """
            class Sample
            {
                int Value { get => 42; }
                Sample() { }
                int Calculate() => Value;
                void Run() { void Local() { } Local(); }
            }
            """;
        var root = CSharpSyntaxTree.ParseText(source).GetRoot();
        var declarations = root.DescendantNodes()
            .Where(node => node is BaseMethodDeclarationSyntax or AccessorDeclarationSyntax or LocalFunctionStatementSyntax)
            .ToList();

        Assert.Equal(5, declarations.Count);
        Assert.All(declarations, declaration => Assert.NotNull(MethodBodyLocator.GetBody(declaration)));
    }
}
