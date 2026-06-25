using Xunit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using AiNetLinter.Configuration;
using AiNetLinter.Core;
using System.Linq;

namespace AiNetLinter.Tests.Core;

public sealed class CouplingSemanticTests
{
    private static Config CreateConfig(int maxConstructorDeps)
    {
        return new Config
        {
            Global = new GlobalConfig
            {
                EnforceSealedClasses = false,
                AllowDynamic = false,
                AllowOutParameters = false,
                EnforceValueObjectContracts = false,
                EnforcePascalCase = false,
                EnforceXmlDocumentation = false,
                EnforceSemanticNaming = false,
                EnforceNullableEnable = false,
                EnforceNoSilentCatch = false
            },
            Metrics = new MetricsConfig
            {
                MaxLineCount = 500,
                MaxMethodParameterCount = 4,
                MaxCyclomaticComplexity = 5,
                MaxCognitiveComplexity = 5,
                MaxConstructorDependencies = maxConstructorDeps
            }
        };
    }

    private static SemanticModel GetSemanticContext(string source)
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

        return compilation.GetSemanticModel(tree);
    }

    [Fact]
    public void ConstructorDependencies_Exceeded_IsDisallowed()
    {
        const string source = @"
public sealed class ComplexService
{
    public ComplexService(int a, int b, int c, int d) {}
}";
        var model = GetSemanticContext(source);
        var violations = LinterAnalyzer.Analyze("Test.cs", model, CreateConfig(maxConstructorDeps: 3));
        Assert.Single(violations);
        Assert.Equal("MaxConstructorDependencies", violations.First().RuleName);
    }

    [Fact]
    public void PrimaryConstructorDependencies_Exceeded_IsDisallowed()
    {
        const string source = @"
public sealed class ComplexRecord(int a, int b, int c, int d)
{
}";
        var model = GetSemanticContext(source);
        var violations = LinterAnalyzer.Analyze("Test.cs", model, CreateConfig(maxConstructorDeps: 3));
        Assert.Single(violations);
        Assert.Equal("MaxConstructorDependencies", violations.First().RuleName);
        Assert.Contains("Primaerkonstruktor", violations.First().Details);
    }

}
