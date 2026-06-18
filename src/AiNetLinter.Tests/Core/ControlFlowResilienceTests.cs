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
                PreventContextDependentOverloads = false,
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
    public void Throw_InLocalFunctionNestedInGuardMethod_IsDisallowed()
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
        Assert.Single(violations);
        Assert.Equal("EnforceResultPatternOverExceptions", violations.First().RuleName);
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

    private static LinterConfig CreateSilentCatchConfig(bool enabled, bool allowCancellationCatch)
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
                EnforceNoSilentCatch = enabled,
                AllowCancellationShutdownCatch = allowCancellationCatch,
                EnforceResultPatternOverExceptions = false,
                AllowedExceptions = System.Array.Empty<string>(),
                EnforceExplicitStateImmutability = false,
                PreventContextDependentOverloads = false,
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

    [Fact]
    public void IsSwallowed_WithReturnOrAssignment_ReturnsNoViolation()
    {
        const string source = @"
using System;
public sealed class Test
{
    private string _lastError = """";
    private bool _isAvailable = true;

    public string? TryParseWithReturn(string input)
    {
        try
        {
            return input;
        }
        catch (FormatException)
        {
            return null; // Return-Statement -> Not swallowed
        }
    }

    public bool TryParseWithAssignmentAndReturn(string input)
    {
        try
        {
            return true;
        }
        catch (ArgumentException ex)
        {
            _lastError = ex.Message; // Assignment -> Not swallowed
            return false;
        }
    }

    public void CheckStatus()
    {
        try
        {
        }
        catch (System.IO.FileNotFoundException)
        {
            _isAvailable = false; // Assignment only -> Not swallowed
        }
    }
}";
        var model = GetSemanticContext(source);
        var violations = LinterAnalyzer.Analyze("Test.cs", model, CreateSilentCatchConfig(true, false));
        Assert.Empty(violations.Where(v => v.RuleName == nameof(GlobalConfig.EnforceNoSilentCatch)));
    }

    [Fact]
    public void IsSwallowed_EmptyOrOnlyDeclaration_ReturnsViolation()
    {
        const string source = @"
using System;
public sealed class Test
{
    public void DoWork()
    {
        try {}
        catch (Exception)
        {
            // Empty catch -> Swallowed!
        }
    }

    public void DoOtherWork()
    {
        try {}
        catch (Exception ex)
        {
            var _ = ex; // Only declaration -> Swallowed!
        }
    }
}";
        var model = GetSemanticContext(source);
        var violations = LinterAnalyzer.Analyze("Test.cs", model, CreateSilentCatchConfig(true, false));
        Assert.Equal(2, violations.Count(v => v.RuleName == nameof(GlobalConfig.EnforceNoSilentCatch)));
    }

    [Fact]
    public void IsAllowedCancellationCatch_WithoutWhenFilter_ReturnsNoViolation()
    {
        const string source = @"
using System;
using System.Threading.Tasks;
public sealed class Test
{
    public void DoWork()
    {
        try {}
        catch (OperationCanceledException)
        {
            // Allowed even when empty if AllowCancellationShutdownCatch is true
        }

        try {}
        catch (TaskCanceledException)
        {
            // Allowed
        }
    }
}";
        var model = GetSemanticContext(source);
        var violations = LinterAnalyzer.Analyze("Test.cs", model, CreateSilentCatchConfig(true, true));
        Assert.Empty(violations.Where(v => v.RuleName == nameof(GlobalConfig.EnforceNoSilentCatch)));
    }

    [Fact]
    public void IsAllowedCancellationCatch_OtherExceptions_ReturnsViolation()
    {
        const string source = @"
using System;
public sealed class Test
{
    public void DoWork()
    {
        try {}
        catch (System.IO.IOException)
        {
            // Other exceptions still violated
        }
    }
}";
        var model = GetSemanticContext(source);
        var violations = LinterAnalyzer.Analyze("Test.cs", model, CreateSilentCatchConfig(true, true));
        Assert.Single(violations.Where(v => v.RuleName == nameof(GlobalConfig.EnforceNoSilentCatch)));
    }
}
