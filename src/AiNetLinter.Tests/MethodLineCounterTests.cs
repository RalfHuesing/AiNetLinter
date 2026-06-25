using Xunit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using AiNetLinter.Configuration;
using AiNetLinter.Core;
using AiNetLinter.Metrics;

namespace AiNetLinter.Tests;

public sealed class MethodLineCounterTests
{
    private static MethodDeclarationSyntax ParseMethod(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        var method = tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().First();
        return method;
    }

    [Fact]
    public void GetCodeLineCount_WithShortMethod_ReturnsExpectedCount()
    {
        const string source = @"
namespace Test;
public sealed class Sample
{
    public void Work()
    {
        var a = 1;
        var b = 2;
    }
}";

        var method = ParseMethod(source);
        var count = MethodLineCounter.GetCodeLineCount(method);

        Assert.Equal(5, count);
    }

    [Fact]
    public void GetCodeLineCount_IgnoresCommentsAndBlankLines()
    {
        const string source = @"
namespace Test;
public sealed class Sample
{
    public void Work()
    {
        // comment line
        var a = 1;

        /* block comment */
        var b = 2;
    }
}";

        var method = ParseMethod(source);
        var count = MethodLineCounter.GetCodeLineCount(method);

        Assert.Equal(5, count);
    }

    [Fact]
    public void GetCodeLineCount_ForAbstractMethod_ReturnsZero()
    {
        const string source = @"
namespace Test;
public abstract class Sample
{
    public abstract void Work();
}";

        var method = ParseMethod(source);
        var count = MethodLineCounter.GetCodeLineCount(method);

        Assert.Equal(0, count);
    }

    [Fact]
    public void Analyze_WithLongMethod_ReturnsMaxMethodLineCountViolation()
    {
        var statements = string.Join("\n", Enumerable.Range(1, 45).Select(i => $"        var v{i} = {i};"));
        var source = $@"
namespace Test;
public sealed class Sample
{{
    public void LongMethod()
    {{
{statements}
    }}
}}";

        var tree = CSharpSyntaxTree.ParseText(source);
        var compilation = CSharpCompilation.Create("TestAssembly")
            .AddSyntaxTrees(tree)
            .AddReferences(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
            .WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var model = compilation.GetSemanticModel(tree);

        var config = new Config
        {
            Global = new GlobalConfig
            {
                EnforceSealedClasses = false,
                EnforcePascalCase = false,
                EnforceXmlDocumentation = false,
                EnforceSemanticNaming = false,
                EnforceNullableEnable = false,
                EnforceNoSilentCatch = false
            },
            Metrics = new MetricsConfig
            {
                MaxLineCount = 500,
                MaxMethodLineCount = 42,
                CompoundSuppressions = Array.Empty<CompoundSuppression>()
            }
        };

        var violations = LinterAnalyzer.Analyze("Test.cs", model, config);

        Assert.Contains(violations, v => v.RuleName == nameof(MetricsConfig.MaxMethodLineCount));
    }
}
