#nullable enable

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Core.DuplicateDetection;
using AiNetLinter.TestKit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

// @covers StructureProfileExtractor
// @covers ProfileWalker

namespace AiNetLinter.FastTests.Core.DuplicateDetection;

[Trait("Category", "Unit")]
public sealed class StructureProfileExtractorTests
{
    private static async Task<EligibleMethod> ExtractEligibleMethodAsync(string sourceCode, string methodName)
    {
        var testSolution = RoslynTestSolutionFactory.CreateSolution(
            @"C:\ainetlinter-virtual\StructureProfileExtractorTests.slnx",
            new ProjectSpec("ExtractorTestProject", [("Source.cs", sourceCode)]));

        var project = testSolution.Solution.Projects.Single();
        var document = project.Documents.Single();
        var syntaxTree = await document.GetSyntaxTreeAsync(CancellationToken.None);
        var semanticModel = await document.GetSemanticModelAsync(CancellationToken.None);

        Assert.NotNull(syntaxTree);
        Assert.NotNull(semanticModel);

        var root = await syntaxTree.GetRootAsync(CancellationToken.None);
        var methodSyntax = root.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .First(m => m.Identifier.Text == methodName);

        var symbol = semanticModel.GetDeclaredSymbol(methodSyntax, CancellationToken.None);
        Assert.NotNull(symbol);

        SyntaxNode bodyNode = methodSyntax.Body != null
            ? (SyntaxNode)methodSyntax.Body
            : methodSyntax.ExpressionBody?.Expression != null
                ? (SyntaxNode)methodSyntax.ExpressionBody.Expression
                : methodSyntax;

        return new EligibleMethod(
            FilePath: document.FilePath ?? "Source.cs",
            LineNumber: 1,
            SignatureName: symbol.Name,
            TokenCount: 50,
            Declaration: methodSyntax,
            Body: bodyNode,
            Symbol: (IMethodSymbol)symbol,
            SemanticModel: semanticModel);
    }

    [Fact]
    public async Task Extract_SwitchExpression_ExtractsFeaturesAndReturnForm()
    {
        const string code = """
            using Microsoft.CodeAnalysis;
            public static class Describer
            {
                public static string Describe(ITypeSymbol symbol) =>
                    symbol.TypeKind switch
                    {
                        TypeKind.Class => "Class",
                        TypeKind.Struct => "Struct",
                        _ => "Other"
                    };
            }
            """;

        var eligible = await ExtractEligibleMethodAsync(code, "Describe");
        var profile = StructureProfileExtractor.Extract(eligible);

        Assert.NotNull(profile);
        Assert.Contains("ret:string", profile.Features.Keys);
        Assert.Contains("param:ITypeSymbol", profile.Features.Keys);
        Assert.Contains("cf:switch-expr", profile.Features.Keys);

        Assert.Contains("ret=string", profile.Summary);
        Assert.Contains("form=switch", profile.Summary);
    }

    [Fact]
    public async Task Extract_IfElseControlFlow_RecordsSequenceAndForm()
    {
        const string code = """
            public static class Logic
            {
                public static int Evaluate(int a, int b)
                {
                    if (a > 0)
                    {
                        return a + b;
                    }
                    else
                    {
                        return b - a;
                    }
                }
            }
            """;

        var eligible = await ExtractEligibleMethodAsync(code, "Evaluate");
        var profile = StructureProfileExtractor.Extract(eligible);

        Assert.NotNull(profile);
        Assert.Contains("cf:if", profile.Features.Keys);
        Assert.Contains("ret:int", profile.Features.Keys);

        Assert.Contains("ret=int", profile.Summary);
        Assert.Contains("form=if", profile.Summary);
    }

    [Fact]
    public async Task Extract_IoCalls_MarksIoFlag()
    {
        const string code = """
            using System;
            using System.IO;
            public static class Logger
            {
                public static void WriteLog(string path, string message)
                {
                    File.AppendAllText(path, message);
                    Console.WriteLine(message);
                }
            }
            """;

        var eligible = await ExtractEligibleMethodAsync(code, "WriteLog");
        var profile = StructureProfileExtractor.Extract(eligible);

        Assert.NotNull(profile);
        Assert.Contains("io", profile.Summary);
    }

    [Fact]
    public async Task Extract_InstanceMutation_MarksMutatesState()
    {
        const string code = """
            public class Accumulator
            {
                private int _total;
                public void Add(int value)
                {
                    _total += value;
                }
            }
            """;

        var eligible = await ExtractEligibleMethodAsync(code, "Add");
        var profile = StructureProfileExtractor.Extract(eligible);

        Assert.NotNull(profile);
        Assert.Contains("mutates", profile.Summary);
    }

    [Fact]
    public async Task Extract_ThrowException_RecognizesThrowReturnForm()
    {
        const string code = """
            using System;
            public static class Guard
            {
                public static void Fail()
                {
                    throw new InvalidOperationException("Failed");
                }
            }
            """;

        var eligible = await ExtractEligibleMethodAsync(code, "Fail");
        var profile = StructureProfileExtractor.Extract(eligible);

        Assert.NotNull(profile);
        Assert.Contains("form=throw", profile.Summary);
    }
}
