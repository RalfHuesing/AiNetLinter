using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;
using AiNetLinter.Configuration;
using AiNetLinter.Core;

namespace AiNetLinter.Tests.Core;

public sealed class MethodParameterCountIgnoreTypePrefixesTests
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

    private static SemanticModel GetSemanticModel(string source)
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

        if (errors.Count > 0)
            throw new System.Exception("Compilation errors:\n" + string.Join("\n", errors));

        return compilation.GetSemanticModel(tree);
    }

    [Fact]
    public void IgnoreTypePrefixes_MatchingPrefix_NotCounted()
    {
        const string source = @"
public interface ILoggerFake {}
public sealed class MyService
{
    public void Process(string a, string b, string c, string d, ILoggerFake logger) { }
}";
        var config = CreateConfig(maxParams: 4) with
        {
            Metrics = CreateConfig(maxParams: 4).Metrics with
            {
                MethodParameterCountIgnoreTypePrefixes = ["ILogger"]
            }
        };
        var model = GetSemanticModel(source);
        var violations = LinterAnalyzer.Analyze("Test.cs", model, config);
        Assert.DoesNotContain(violations, v => v.RuleName == "MaxMethodParameterCount");
    }

    [Fact]
    public void IgnoreTypePrefixes_NonMatchingPrefix_IsCounted()
    {
        const string source = @"
public sealed class MyService
{
    public void Process(string a, string b, string c, string d, string e) { }
}";
        var config = CreateConfig(maxParams: 4) with
        {
            Metrics = CreateConfig(maxParams: 4).Metrics with
            {
                MethodParameterCountIgnoreTypePrefixes = ["ILogger"]
            }
        };
        var model = GetSemanticModel(source);
        var violations = LinterAnalyzer.Analyze("Test.cs", model, config);
        Assert.Contains(violations, v => v.RuleName == "MaxMethodParameterCount");
    }

    [Fact]
    public void IgnoreTypeNames_ExactName_NotCounted()
    {
        const string source = @"
public sealed class CancellationToken {}
public sealed class MyService
{
    public void Process(string a, string b, string c, string d, CancellationToken ct) { }
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
        Assert.DoesNotContain(violations, v => v.RuleName == "MaxMethodParameterCount");
    }

    [Fact]
    public void IgnoreTypeNames_PartialName_IsCounted()
    {
        const string source = @"
public sealed class CancellationTokenSource {}
public sealed class MyService
{
    public void Process(string a, string b, string c, string d, CancellationTokenSource cts) { }
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
        Assert.Contains(violations, v => v.RuleName == "MaxMethodParameterCount");
    }

    [Fact]
    public void BothNamesAndPrefixes_DetailsContainsBothHints()
    {
        const string source = @"
public interface ILoggerFake {}
public sealed class CancellationToken {}
public sealed class MyService
{
    public void Process(string a, string b, string c, string d, string e, ILoggerFake logger, CancellationToken ct) { }
}";
        var config = CreateConfig(maxParams: 4) with
        {
            Metrics = CreateConfig(maxParams: 4).Metrics with
            {
                MethodParameterCountIgnoreTypeNames = ["CancellationToken"],
                MethodParameterCountIgnoreTypePrefixes = ["ILogger"]
            }
        };
        var model = GetSemanticModel(source);
        var violations = LinterAnalyzer.Analyze("Test.cs", model, config);
        var v = Assert.Single(violations.Where(v => v.RuleName == "MaxMethodParameterCount"));
        Assert.Contains("CancellationToken", v.Details);
        Assert.Contains("ILogger*", v.Details);
    }
}
