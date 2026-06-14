using Xunit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using AiNetLinter.Configuration;
using AiNetLinter.Core;
using System.Linq;

namespace AiNetLinter.Tests.Core;

public sealed class ResultPatternNamespaceTests
{
    private static LinterConfig CreateConfig(
        string[]? allowedNamespaceSuffixes = null,
        bool allowCatchRethrow = false)
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
                EnforceResultPatternOverExceptions = true,
                AllowedExceptions = System.Array.Empty<string>(),
                EnforceExplicitStateImmutability = false,
                EnforceStrictBoundaryForBusinessLogic = false,
                PreventContextDependentOverloads = false,
                RequireExplicitTruncationHandling = false,
                EnforceNamespaceDirectoryMapping = false,
                DetectAndBanPhantomDependencies = false,
                ResultPatternAllowThrowInNamespaceSuffixes = allowedNamespaceSuffixes ?? System.Array.Empty<string>(),
                ResultPatternAllowCatchRethrow = allowCatchRethrow,
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
            throw new System.Exception("Compilation errors:\n" + string.Join("\n", errors));

        return compilation.GetSemanticModel(tree);
    }

    [Fact]
    public void Throw_InAllowedNamespaceSuffix_IsAllowed()
    {
        const string source = @"
namespace MyApp.Infrastructure
{
    public sealed class SqlHandler
    {
        public void Run()
        {
            throw new System.Exception(""no connection"");
        }
    }
}";
        var model = GetSemanticContext(source);
        var violations = LinterAnalyzer.Analyze("Test.cs", model,
            CreateConfig(allowedNamespaceSuffixes: new[] { "Infrastructure" }));

        Assert.DoesNotContain(violations, v => v.RuleName == "EnforceResultPatternOverExceptions");
    }

    [Fact]
    public void Throw_InNonAllowedNamespace_IsDisallowed()
    {
        const string source = @"
namespace MyApp.Domain
{
    public sealed class OrderService
    {
        public void Process()
        {
            throw new System.Exception(""business error"");
        }
    }
}";
        var model = GetSemanticContext(source);
        var violations = LinterAnalyzer.Analyze("Test.cs", model,
            CreateConfig(allowedNamespaceSuffixes: new[] { "Infrastructure" }));

        Assert.Contains(violations, v => v.RuleName == "EnforceResultPatternOverExceptions");
    }

    [Fact]
    public void Throw_InAllowedNamespaceSuffix_ExactMatch_IsAllowed()
    {
        const string source = @"
namespace Infrastructure
{
    public sealed class StartupHandler
    {
        public void Init()
        {
            throw new System.Exception(""startup error"");
        }
    }
}";
        var model = GetSemanticContext(source);
        var violations = LinterAnalyzer.Analyze("Test.cs", model,
            CreateConfig(allowedNamespaceSuffixes: new[] { "Infrastructure" }));

        Assert.DoesNotContain(violations, v => v.RuleName == "EnforceResultPatternOverExceptions");
    }

    [Fact]
    public void BareRethrow_InCatchBlock_IsAllowedWhenConfigured()
    {
        const string source = @"
public sealed class Test
{
    public void DoWork()
    {
        try { }
        catch (System.Exception)
        {
            throw;
        }
    }
}";
        var model = GetSemanticContext(source);
        var violations = LinterAnalyzer.Analyze("Test.cs", model,
            CreateConfig(allowCatchRethrow: true));

        Assert.DoesNotContain(violations, v => v.RuleName == "EnforceResultPatternOverExceptions");
    }

    [Fact]
    public void BareRethrow_InCatchBlock_IsDisallowedWhenNotConfigured()
    {
        const string source = @"
public sealed class Test
{
    public void DoWork()
    {
        try { }
        catch (System.Exception)
        {
            throw;
        }
    }
}";
        var model = GetSemanticContext(source);
        var violations = LinterAnalyzer.Analyze("Test.cs", model,
            CreateConfig(allowCatchRethrow: false));

        Assert.Contains(violations, v => v.RuleName == "EnforceResultPatternOverExceptions");
    }

    [Fact]
    public void AllowedNamespaceSuffixes_Empty_StillEnforcesResultPattern()
    {
        const string source = @"
namespace MyApp.Infrastructure
{
    public sealed class SqlHandler
    {
        public void Run()
        {
            throw new System.Exception(""error"");
        }
    }
}";
        var model = GetSemanticContext(source);
        var violations = LinterAnalyzer.Analyze("Test.cs", model,
            CreateConfig(allowedNamespaceSuffixes: System.Array.Empty<string>()));

        Assert.Contains(violations, v => v.RuleName == "EnforceResultPatternOverExceptions");
    }
}
