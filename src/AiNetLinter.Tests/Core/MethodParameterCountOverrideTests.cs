using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;
using AiNetLinter.Configuration;
using AiNetLinter.Core;

namespace AiNetLinter.Tests.Core;

public sealed class MethodParameterCountOverrideTests
{
    private static LinterConfig CreateConfig(int maxParams = 4) => new()
    {
        Global = new GlobalConfig
        {
            EnforceSealedClasses = false,
            AllowDynamic = false,
            AllowOutParameters = true,
            EnforceValueObjectContracts = false,
            EnforcePascalCase = false,
            EnforceXmlDocumentation = false,
            EnforceSemanticNaming = false,
            EnforceNullableEnable = false,
            EnforceNoSilentCatch = false,            EnforceExplicitStateImmutability = false,            PreventContextDependentOverloads = false,            EnforceNamespaceDirectoryMapping = false,
            DetectAndBanPhantomDependencies = false
        },
        Metrics = new MetricsConfig
        {
            MaxLineCount = 500,
            MaxMethodParameterCount = maxParams,
            MaxCyclomaticComplexity = 20,
            MaxCognitiveComplexity = 20
        }
    };

    private static SemanticModel GetSemanticModel(string source, params string[] additionalSources)
    {
        var mscorlib = MetadataReference.CreateFromFile(typeof(object).Assembly.Location);
        var trees = new[] { CSharpSyntaxTree.ParseText(source, path: "Test.cs") }
            .Concat(additionalSources.Select((s, i) => CSharpSyntaxTree.ParseText(s, path: $"Dep{i}.cs")))
            .ToArray();

        var compilation = CSharpCompilation.Create("TestAssembly")
            .AddSyntaxTrees(trees)
            .AddReferences(mscorlib)
            .WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var errors = compilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();

        if (errors.Count > 0)
            throw new System.Exception("Compilation errors:\n" + string.Join("\n", errors));

        return compilation.GetSemanticModel(trees[0]);
    }

    [Fact]
    public void OverrideMethod_WithTooManyParams_NoViolation()
    {
        const string baseSource = @"
public abstract class BaseService
{
    public abstract void Process(string a, string b, string c, string d, string e);
}";
        const string source = @"
public sealed class ConcreteService : BaseService
{
    public override void Process(string a, string b, string c, string d, string e) { }
}";
        var model = GetSemanticModel(source, baseSource);
        var violations = LinterAnalyzer.Analyze("Test.cs", model, CreateConfig(maxParams: 4));
        Assert.Empty(violations.Where(v => v.RuleName == "MaxMethodParameterCount"));
    }

    [Fact]
    public void ExplicitInterfaceImplementation_WithTooManyParams_NoViolation()
    {
        const string ifaceSource = @"
public interface ILogger
{
    void Log(int level, int eventId, string state, System.Exception ex, string formatter);
}";
        const string source = @"
public sealed class MyLogger : ILogger
{
    void ILogger.Log(int level, int eventId, string state, System.Exception ex, string formatter) { }
}";
        var model = GetSemanticModel(source, ifaceSource);
        var violations = LinterAnalyzer.Analyze("Test.cs", model, CreateConfig(maxParams: 4));
        Assert.Empty(violations.Where(v => v.RuleName == "MaxMethodParameterCount"));
    }

    [Fact]
    public void ImplicitInterfaceImplementation_WithTooManyParams_NoViolation()
    {
        const string ifaceSource = @"
public interface ILoader
{
    void Load(string a, string b, string c, string d, string e);
}";
        const string source = @"
public sealed class MyLoader : ILoader
{
    public void Load(string a, string b, string c, string d, string e) { }
}";
        var model = GetSemanticModel(source, ifaceSource);
        var violations = LinterAnalyzer.Analyze("Test.cs", model, CreateConfig(maxParams: 4));
        Assert.Empty(violations.Where(v => v.RuleName == "MaxMethodParameterCount"));
    }

    [Fact]
    public void NormalMethod_WithTooManyParams_Violation()
    {
        const string source = @"
public sealed class MyService
{
    public void DoWork(string a, string b, string c, string d, string e) { }
}";
        var model = GetSemanticModel(source);
        var violations = LinterAnalyzer.Analyze("Test.cs", model, CreateConfig(maxParams: 4));
        Assert.Single(violations.Where(v => v.RuleName == "MaxMethodParameterCount"));
    }

    [Fact]
    public void VirtualMethod_WithTooManyParams_Violation()
    {
        const string source = @"
public class MyService
{
    public virtual void DoWork(string a, string b, string c, string d, string e) { }
}";
        var model = GetSemanticModel(source);
        var violations = LinterAnalyzer.Analyze("Test.cs", model, CreateConfig(maxParams: 4));
        Assert.Single(violations.Where(v => v.RuleName == "MaxMethodParameterCount"));
    }

    [Fact]
    public void IgnoredType_CancellationToken_NotCountedTowardLimit()
    {
        const string source = @"
using System.Threading;
public sealed class AsyncService
{
    public async System.Threading.Tasks.Task DoAsync(string a, string b, string c, string d, CancellationToken ct) { }
}";
        var config = CreateConfig(maxParams: 4) with
        {
            Metrics = CreateConfig(maxParams: 4).Metrics with
            {
                MethodParameterCountIgnoreTypeNames = ["CancellationToken"]
            }
        };
        var model = GetSemanticModel(source);
        var violations = LinterAnalyzer.Analyze("Test.cs", model, config);
        Assert.Empty(violations.Where(v => v.RuleName == "MaxMethodParameterCount"));
    }

    [Fact]
    public void IgnoredType_ViolationDetailsContainsIgnoreHint()
    {
        const string source = @"
using System.Threading;
public sealed class AsyncService
{
    public async System.Threading.Tasks.Task DoAsync(string a, string b, string c, string d, string e, CancellationToken ct) { }
}";
        var config = CreateConfig(maxParams: 4) with
        {
            Metrics = CreateConfig(maxParams: 4).Metrics with
            {
                MethodParameterCountIgnoreTypeNames = ["CancellationToken"]
            }
        };
        var model = GetSemanticModel(source);
        var violations = LinterAnalyzer.Analyze("Test.cs", model, config);
        var v = Assert.Single(violations.Where(v => v.RuleName == "MaxMethodParameterCount"));
        Assert.Contains("nicht mitgezählt: CancellationToken", v.Details);
    }

    [Fact]
    public void IgnoredType_WithoutConfig_DetailsHasNoIgnoreHint()
    {
        const string source = @"
public sealed class MyService
{
    public void DoWork(string a, string b, string c, string d, string e) { }
}";
        var model = GetSemanticModel(source);
        var violations = LinterAnalyzer.Analyze("Test.cs", model, CreateConfig(maxParams: 4));
        var v = Assert.Single(violations.Where(v => v.RuleName == "MaxMethodParameterCount"));
        Assert.DoesNotContain("nicht mitgezählt", v.Details);
    }

    [Fact]
    public void IgnoredType_WithoutConfig_StillCounted()
    {
        const string source = @"
using System.Threading;
public sealed class AsyncService
{
    public async System.Threading.Tasks.Task DoAsync(string a, string b, string c, string d, CancellationToken ct) { }
}";
        var model = GetSemanticModel(source);
        var violations = LinterAnalyzer.Analyze("Test.cs", model, CreateConfig(maxParams: 4));
        Assert.Single(violations.Where(v => v.RuleName == "MaxMethodParameterCount"));
    }

    [Fact]
    public void TestFile_UsesTestLimit_WhenConfigured()
    {
        const string source = @"
public sealed class MyServiceTests
{
    public void ArrangeScenario(string a, string b, string c, string d, string e) { }
}";
        var config = CreateConfig(maxParams: 4) with
        {
            Metrics = CreateConfig(maxParams: 4).Metrics with
            {
                MaxMethodParameterCountInTestFiles = 6
            }
        };
        var model = GetSemanticModel(source);
        var violations = LinterAnalyzer.Analyze("MyServiceTests.cs", model, config, isTestFile: true);
        Assert.Empty(violations.Where(v => v.RuleName == "MaxMethodParameterCount"));
    }

    [Fact]
    public void TestFile_WithoutTestLimit_UsesDefaultLimit()
    {
        const string source = @"
public sealed class MyServiceTests
{
    public void ArrangeScenario(string a, string b, string c, string d, string e) { }
}";
        var model = GetSemanticModel(source);
        var violations = LinterAnalyzer.Analyze("MyServiceTests.cs", model, CreateConfig(maxParams: 4), isTestFile: true);
        Assert.Single(violations.Where(v => v.RuleName == "MaxMethodParameterCount"));
    }

    [Fact]
    public void ProductionFile_TestLimitIgnored_UsesDefaultLimit()
    {
        const string source = @"
public sealed class MyService
{
    public void DoWork(string a, string b, string c, string d, string e) { }
}";
        var config = CreateConfig(maxParams: 4) with
        {
            Metrics = CreateConfig(maxParams: 4).Metrics with
            {
                MaxMethodParameterCountInTestFiles = 10
            }
        };
        var model = GetSemanticModel(source);
        var violations = LinterAnalyzer.Analyze("Test.cs", model, config, isTestFile: false);
        Assert.Single(violations.Where(v => v.RuleName == "MaxMethodParameterCount"));
    }
}
