using Xunit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using AiNetLinter.Configuration;
using AiNetLinter.Core;
using System.Linq;

namespace AiNetLinter.Tests.Core;

public sealed class CouplingSemanticTests
{
    private static LinterConfig CreateConfig(bool magicValues, int maxConstructorDeps)
    {
        return new LinterConfig
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
                EnforceNoSilentCatch = false,
                EnforceNoVariableShadowing = false,
                EnforceReadonlyParameters = false,
                EnforceReadonlyFields = false,
                EnforceNoMagicValues = magicValues
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
        var violations = LinterAnalyzer.Analyze("Test.cs", model, CreateConfig(false, maxConstructorDeps: 3));
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
        var violations = LinterAnalyzer.Analyze("Test.cs", model, CreateConfig(false, maxConstructorDeps: 3));
        Assert.Single(violations);
        Assert.Equal("MaxConstructorDependencies", violations.First().RuleName);
        Assert.Contains("Primaerkonstruktor", violations.First().Details);
    }

    [Fact]
    public void MagicValues_IntAndStringInMethodBody_IsDisallowed()
    {
        const string source = @"
public sealed class Test
{
    public void Run()
    {
        int x = 42;
        string role = ""Admin"";
    }
}";
        var model = GetSemanticContext(source);
        var violations = LinterAnalyzer.Analyze("Test.cs", model, CreateConfig(true, 5));
        Assert.Equal(2, violations.Count);
        Assert.All(violations, v => Assert.Equal("EnforceNoMagicValues", v.RuleName));
    }

    [Fact]
    public void MagicValues_Exceptions_AreAllowed()
    {
        const string source = @"
public sealed class Test
{
    public void Run()
    {
        int a = 0;
        int b = 1;
        int c = -1;
        string s = """";
    }
}";
        var model = GetSemanticContext(source);
        var violations = LinterAnalyzer.Analyze("Test.cs", model, CreateConfig(true, 5));
        Assert.Empty(violations);
    }

    [Fact]
    public void MagicValues_ConstDeclarations_AreAllowed()
    {
        const string source = @"
public sealed class Test
{
    private const int MaxLimit = 100;
    public void Run()
    {
        const string DefaultRole = ""User"";
    }
}";
        var model = GetSemanticContext(source);
        var violations = LinterAnalyzer.Analyze("Test.cs", model, CreateConfig(true, 5));
        Assert.Empty(violations);
    }

    [Fact]
    public void MagicValues_AttributeArguments_AreAllowed()
    {
        const string source = @"
using System;
[Obsolete(""Use new method instead"")]
public sealed class Test
{
}";
        var model = GetSemanticContext(source);
        var violations = LinterAnalyzer.Analyze("Test.cs", model, CreateConfig(true, 5));
        Assert.Empty(violations);
    }
}
