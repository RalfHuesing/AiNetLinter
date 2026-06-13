using Xunit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using AiNetLinter.Configuration;
using AiNetLinter.Core;
using System.Linq;

namespace AiNetLinter.Tests.Core;

public sealed class ControlFlowResilienceTests
{
    private static LinterConfig CreateConfig(bool enabled)
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
                EnforceResultPatternOverExceptions = enabled,
                AllowedExceptions = System.Array.Empty<string>(),
                EnforceExplicitStateImmutability = false,
                EnforceStrictBoundaryForBusinessLogic = false,
                PreventContextDependentOverloads = false,
                RequireExplicitTruncationHandling = false,
                EnforceNamespaceDirectoryMapping = false,
                DetectAndBanPhantomDependencies = false
            },
            Metrics = new MetricsConfig
            {
                MaxLineCount = 100,
                MaxMethodParameterCount = 4,
                MaxCyclomaticComplexity = 5,
                MaxCognitiveComplexity = 5
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
    public void Throw_InConstructor_IsAllowed()
    {
        const string source = @"
public sealed class Test
{
    public Test()
    {
        throw new System.ArgumentException();
    }
}";
        var model = GetSemanticContext(source);
        var violations = LinterAnalyzer.Analyze("Test.cs", model, CreateConfig(true));
        Assert.Empty(violations);
    }

    [Fact]
    public void Throw_InStaticConstructor_IsAllowed()
    {
        const string source = @"
public sealed class Test
{
    static Test()
    {
        throw new System.InvalidOperationException();
    }
}";
        var model = GetSemanticContext(source);
        var violations = LinterAnalyzer.Analyze("Test.cs", model, CreateConfig(true));
        Assert.Empty(violations);
    }

    [Fact]
    public void Throw_InMethodEndingWithGuard_IsAllowed()
    {
        const string source = @"
public sealed class Test
{
    public void CheckGuard()
    {
        throw new System.InvalidOperationException();
    }
}";
        var model = GetSemanticContext(source);
        var violations = LinterAnalyzer.Analyze("Test.cs", model, CreateConfig(true));
        Assert.Empty(violations);
    }

    [Fact]
    public void Throw_InMethodEndingWithValidate_IsAllowed()
    {
        const string source = @"
public sealed class Test
{
    public void Validate()
    {
        throw new System.InvalidOperationException();
    }
}";
        var model = GetSemanticContext(source);
        var violations = LinterAnalyzer.Analyze("Test.cs", model, CreateConfig(true));
        Assert.Empty(violations);
    }

    [Fact]
    public void Throw_InLocalFunctionEndingWithGuard_IsAllowed()
    {
        const string source = @"
public sealed class Test
{
    public void SomeMethod()
    {
        void InternalGuard()
        {
            throw new System.InvalidOperationException();
        }
    }
}";
        var model = GetSemanticContext(source);
        var violations = LinterAnalyzer.Analyze("Test.cs", model, CreateConfig(true));
        Assert.Empty(violations);
    }

    [Fact]
    public void Throw_InLocalFunctionNestedInGuardMethod_IsAllowed()
    {
        const string source = @"
public sealed class Test
{
    public void RunGuard()
    {
        void InternalHelper()
        {
            throw new System.InvalidOperationException();
        }
    }
}";
        var model = GetSemanticContext(source);
        var violations = LinterAnalyzer.Analyze("Test.cs", model, CreateConfig(true));
        Assert.Empty(violations);
    }

    [Fact]
    public void ThrowStatement_InNormalMethod_IsDisallowed()
    {
        const string source = @"
public sealed class Test
{
    public void DoWork()
    {
        throw new System.InvalidOperationException();
    }
}";
        var model = GetSemanticContext(source);
        var violations = LinterAnalyzer.Analyze("Test.cs", model, CreateConfig(true));
        Assert.Single(violations);
        var violation = violations.First();
        Assert.Equal("EnforceResultPatternOverExceptions", violation.RuleName);
        Assert.Contains("Verwendung von 'throw' fuer Kontrollfluss erkannt", violation.Details);
    }

    [Fact]
    public void ThrowExpression_InNormalMethod_IsDisallowed()
    {
        const string source = @"
public sealed class Test
{
    public int DoWork(int? value)
    {
        return value ?? throw new System.ArgumentNullException();
    }
}";
        var model = GetSemanticContext(source);
        var violations = LinterAnalyzer.Analyze("Test.cs", model, CreateConfig(true));
        Assert.Single(violations);
        Assert.Equal("EnforceResultPatternOverExceptions", violations.First().RuleName);
    }

    [Fact]
    public void Throw_InPropertyAccessor_IsDisallowed()
    {
        const string source = @"
public sealed class Test
{
    public int Value
    {
        get => throw new System.NotImplementedException();
        set => throw new System.ArgumentException();
    }
}";
        var model = GetSemanticContext(source);
        var violations = LinterAnalyzer.Analyze("Test.cs", model, CreateConfig(true));
        Assert.Equal(2, violations.Count);
        Assert.All(violations, v => Assert.Equal("EnforceResultPatternOverExceptions", v.RuleName));
    }

    [Fact]
    public void Throw_InLambda_InsideNormalMethod_IsDisallowed()
    {
        const string source = @"
using System;
public sealed class Test
{
    public void Run()
    {
        Action a = () => throw new InvalidOperationException();
    }
}";
        var model = GetSemanticContext(source);
        var violations = LinterAnalyzer.Analyze("Test.cs", model, CreateConfig(true));
        Assert.Single(violations);
        Assert.Equal("EnforceResultPatternOverExceptions", violations.First().RuleName);
    }

    [Fact]
    public void Throw_InNormalMethod_WhenDisabled_IsAllowed()
    {
        const string source = @"
public sealed class Test
{
    public void DoWork()
    {
        throw new System.InvalidOperationException();
    }
}";
        var model = GetSemanticContext(source);
        var violations = LinterAnalyzer.Analyze("Test.cs", model, CreateConfig(false));
        Assert.Empty(violations);
    }

    [Fact]
    public void Throw_InNormalMethod_WithSuppressionComment_IsAllowed()
    {
        const string source = @"
public sealed class Test
{
    public void DoWork()
    {
        // ainetlinter-disable EnforceResultPatternOverExceptions
        throw new System.InvalidOperationException();
    }
}";
        var model = GetSemanticContext(source);
        var violations = LinterAnalyzer.Analyze("Test.cs", model, CreateConfig(true));
        Assert.Empty(violations);
    }
}
