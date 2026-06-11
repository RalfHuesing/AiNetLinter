using Xunit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using AiNetLinter.Configuration;
using AiNetLinter.Core;

namespace AiNetLinter.Tests;

public sealed class LinterAnalyzerTests
{
    private static LinterConfig CreateDefaultConfig()
    {
        return new LinterConfig
        {
            Global = new GlobalConfig
            {
                EnforceSealedClasses = true,
                AllowDynamic = false,
                AllowOutParameters = false,
                EnforceValueObjectContracts = true,
                EnforcePascalCase = false,
                EnforceXmlDocumentation = false,
                EnforceSemanticNaming = false,
                EnforceNullableEnable = false,
                EnforceNoSilentCatch = false
            },
            Metrics = new MetricsConfig
            {
                MaxLineCount = 10,
                MaxMethodParameterCount = 2,
                MaxCyclomaticComplexity = 5,
                MaxCognitiveComplexity = 5
            }
        };
    }

    private static (SyntaxTree, SemanticModel) GetSemanticContext(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        var mscorlib = MetadataReference.CreateFromFile(typeof(object).Assembly.Location);
        var compilation = CSharpCompilation.Create("TestAssembly")
            .AddSyntaxTrees(tree)
            .AddReferences(mscorlib)
            .WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            
        var errors = compilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();
            
        if (errors.Any())
        {
            throw new System.Exception("Compilation errors:\n" + string.Join("\n", errors));
        }

        var semanticModel = compilation.GetSemanticModel(tree);
        return (tree, semanticModel);
    }

    [Fact]
    public void Analyze_WithValidCode_HasNoViolations()
    {
        const string source = @"
namespace TestNamespace
{
    public sealed class TestClass
    {
        public void Work(int x, int y) {}
    }
}";
        var config = CreateDefaultConfig();
        var (tree, model) = GetSemanticContext(source);
        var violations = LinterAnalyzer.Analyze("Test.cs", model, config, isTestFile: false);
        Assert.Empty(violations);
    }

    [Fact]
    public void Analyze_WithForbiddenNamespaceInStatement_ReturnsViolation()
    {
        const string source = @"
namespace MyFeature.Domain
{
    public sealed class DomainService
    {
        public void Run()
        {
            var helper = new MyFeature.Infrastructure.DbHelper();
        }
    }
}
namespace MyFeature.Infrastructure
{
    public sealed class DbHelper {}
}";
        var config = CreateDefaultConfig() with
        {
            ForbiddenNamespaceDependencies = new[]
            {
                new NamespaceRule { SourceNamespace = "MyFeature.Domain", TargetNamespace = "MyFeature.Infrastructure" }
            }
        };
        var (tree, model) = GetSemanticContext(source);
        var violations = LinterAnalyzer.Analyze("Test.cs", model, config, isTestFile: false);
        Assert.Contains(violations, v => v.RuleName == "ForbiddenNamespaceDependency");
    }

    [Fact]
    public void Analyze_WithSuppressionComment_IgnoresViolation()
    {
        const string source = @"
// ainetlinter-disable MaxMethodParameterCount
namespace TestNamespace
{
    public sealed class TestClass
    {
        public void Work(int a, int b, int c, int d, int e) {}
    }
}";
        var config = CreateDefaultConfig();
        var (tree, model) = GetSemanticContext(source);
        var violations = LinterAnalyzer.Analyze("Test.cs", model, config, isTestFile: false);
        Assert.Empty(violations);
    }

    [Fact]
    public void Analyze_WithVariableNamedDynamic_DoesNotThrowViolation()
    {
        const string source = @"
namespace TestNamespace
{
    public sealed class TestClass
    {
        public void Work()
        {
            var dynamic = 5;
        }
    }
}";
        var config = CreateDefaultConfig();
        config = config with { Metrics = config.Metrics with { MaxLineCount = 50 } };
        var (tree, model) = GetSemanticContext(source);
        var violations = LinterAnalyzer.Analyze("Test.cs", model, config, isTestFile: false);
        Assert.Empty(violations);
    }
}
