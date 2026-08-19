#nullable enable

using System.Linq;
using System.Threading.Tasks;
using AiNetLinter.Core;
using AiNetLinter.TestKit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace AiNetLinter.FastTests.Core;

[Trait("Category", "Unit")]
public sealed class RoslynSymbolExtensionsTests
{
    private static RoslynTestSolution CreateSolution(string code) =>
        RoslynTestSolutionFactory.CreateSolution(
            @"C:\ainetlinter-virtual\RoslynSymbolExtensionsTests.slnx",
            new ProjectSpec("TestProject", [("TestFile.cs", code)]));

    [Fact]
    public void NormalizeToOwningMember_NullSymbol_ReturnsNull()
    {
        ISymbol? nullSymbol = null;
        Assert.Null(nullSymbol.NormalizeToOwningMember());
    }

    [Fact]
    public void TryGetDocCommentId_NullSymbol_ReturnsNull()
    {
        ISymbol? nullSymbol = null;
        Assert.Null(nullSymbol.TryGetDocCommentId());
    }

    [Fact]
    public async Task NormalizeToOwningMember_PropertyGetter_ResolvesToProperty()
    {
        const string code = """
            public sealed class Sample
            {
                public int Value { get; set; }
            }
            """;
        using var testSolution = CreateSolution(code);
        var document = testSolution.Solution.Projects.Single().Documents.Single();
        var syntaxTree = (await document.GetSyntaxTreeAsync())!;
        var semanticModel = (await document.GetSemanticModelAsync())!;
        var root = await syntaxTree.GetRootAsync();

        var propertyDecl = root.DescendantNodes().OfType<PropertyDeclarationSyntax>().Single();
        var getterDecl = propertyDecl.AccessorList!.Accessors.Single(a => a.Keyword.Text == "get");
        var getterSymbol = semanticModel.GetDeclaredSymbol(getterDecl);

        Assert.NotNull(getterSymbol);
        var normalized = getterSymbol.NormalizeToOwningMember();

        Assert.NotNull(normalized);
        Assert.IsAssignableFrom<IPropertySymbol>(normalized);
        Assert.Equal("Value", normalized.Name);
    }

    [Fact]
    public async Task NormalizeToOwningMember_RegularMethod_ReturnsSameSymbol()
    {
        const string code = """
            public sealed class Sample
            {
                public void DoWork() {}
            }
            """;
        using var testSolution = CreateSolution(code);
        var document = testSolution.Solution.Projects.Single().Documents.Single();
        var syntaxTree = (await document.GetSyntaxTreeAsync())!;
        var semanticModel = (await document.GetSemanticModelAsync())!;
        var root = await syntaxTree.GetRootAsync();

        var methodDecl = root.DescendantNodes().OfType<MethodDeclarationSyntax>().Single();
        var methodSymbol = semanticModel.GetDeclaredSymbol(methodDecl);

        Assert.NotNull(methodSymbol);
        var normalized = methodSymbol.NormalizeToOwningMember();

        Assert.Same(methodSymbol, normalized);
    }

    [Fact]
    public async Task TryGetDocCommentId_ValidSymbol_ReturnsCorrectId()
    {
        const string code = """
            namespace MyNamespace
            {
                public sealed class Calculator
                {
                    public int Add(int a, int b) => a + b;
                }
            }
            """;
        using var testSolution = CreateSolution(code);
        var document = testSolution.Solution.Projects.Single().Documents.Single();
        var syntaxTree = (await document.GetSyntaxTreeAsync())!;
        var semanticModel = (await document.GetSemanticModelAsync())!;
        var root = await syntaxTree.GetRootAsync();

        var methodDecl = root.DescendantNodes().OfType<MethodDeclarationSyntax>().Single();
        var methodSymbol = semanticModel.GetDeclaredSymbol(methodDecl);

        Assert.NotNull(methodSymbol);
        var docCommentId = methodSymbol.TryGetDocCommentId();

        Assert.NotNull(docCommentId);
        Assert.StartsWith("M:MyNamespace.Calculator.Add(System.Int32,System.Int32)", docCommentId, System.StringComparison.Ordinal);
    }
}
