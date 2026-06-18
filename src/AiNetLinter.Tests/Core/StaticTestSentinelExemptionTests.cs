#nullable enable

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using AiNetLinter.Configuration;
using AiNetLinter.Core;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace AiNetLinter.Tests.Core;

public sealed class StaticTestSentinelExemptionTests
{
    private static LinterConfig CreateSentinelConfig(TestSentinelConfig? sentinel = null)
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
                EnforceResultPatternOverExceptions = false,
                EnforceExplicitStateImmutability = false,                PreventContextDependentOverloads = false,                EnforceNamespaceDirectoryMapping = false,
                DetectAndBanPhantomDependencies = false,
                EnableTestSentinel = true,
            },
            Metrics = new MetricsConfig
            {
                MinCognitiveComplexityForTest = 0,
            },
            TestSentinel = sentinel ?? new TestSentinelConfig(),
        };
    }

    private static Solution CreateAdhocSolution(params (string fileName, string content)[] files)
    {
        var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();
        var mscorlib = MetadataReference.CreateFromFile(typeof(object).Assembly.Location);

        var projectInfo = ProjectInfo.Create(
                projectId,
                VersionStamp.Create(),
                "TestProject",
                "TestProject",
                LanguageNames.CSharp)
            .WithMetadataReferences(new[] { mscorlib })
            .WithCompilationOptions(new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));

        var solution = workspace.CurrentSolution.AddProject(projectInfo);
        foreach (var file in files)
        {
            var documentId = DocumentId.CreateNewId(projectId);
            solution = solution.AddDocument(documentId, file.fileName, file.content);
        }
        return solution;
    }

    // Quelle mit Methode (Komplexität 1) ohne Testklasse → Sentinel feuert
    private const string SourceWithComplexMethod = @"
namespace MyApp;
public sealed class OrderService
{
    public string Process(string input)
    {
        if (input == null) return string.Empty;
        return input;
    }
}";

    [Fact]
    public async Task Sentinel_WithoutExemption_ReportsMissingTest()
    {
        var config = CreateSentinelConfig();
        var solution = CreateAdhocSolution(("OrderService.cs", SourceWithComplexMethod));
        var engine = new LinterEngine(config);
        var violations = await engine.RunAsync(solution);

        Assert.Contains(violations, v => v.RuleName == "StaticTestSentinel");
    }

    [Fact]
    public async Task Sentinel_SuffixExemption_DoesNotReportExtensionsClass()
    {
        const string source = @"
namespace MyApp;
public static class StringExtensions
{
    public static string Transform(this string s)
    {
        if (s == null) return string.Empty;
        return s;
    }
}";
        var config = CreateSentinelConfig(new TestSentinelConfig
        {
            ExemptClassNameSuffixes = new[] { "Extensions" },
            ExemptStaticClasses = false,
        });
        var solution = CreateAdhocSolution(("StringExtensions.cs", source));
        var engine = new LinterEngine(config);
        var violations = await engine.RunAsync(solution);

        Assert.DoesNotContain(violations, v =>
            v.RuleName == "StaticTestSentinel" && v.Details.Contains("StringExtensions"));
    }

    [Fact]
    public async Task Sentinel_SuffixExemption_StillReportsNonExemptClass()
    {
        var config = CreateSentinelConfig(new TestSentinelConfig
        {
            ExemptClassNameSuffixes = new[] { "Extensions" },
        });
        var solution = CreateAdhocSolution(("OrderService.cs", SourceWithComplexMethod));
        var engine = new LinterEngine(config);
        var violations = await engine.RunAsync(solution);

        Assert.Contains(violations, v =>
            v.RuleName == "StaticTestSentinel" && v.Details.Contains("OrderService"));
    }

    [Fact]
    public async Task Sentinel_StaticClassExemption_DoesNotReportStaticClass()
    {
        const string source = @"
namespace MyApp;
public static class AppHelpers
{
    public static string Format(string s)
    {
        if (s == null) return string.Empty;
        return s.Trim();
    }
}";
        var config = CreateSentinelConfig(new TestSentinelConfig
        {
            ExemptStaticClasses = true,
        });
        var solution = CreateAdhocSolution(("AppHelpers.cs", source));
        var engine = new LinterEngine(config);
        var violations = await engine.RunAsync(solution);

        Assert.DoesNotContain(violations, v =>
            v.RuleName == "StaticTestSentinel" && v.Details.Contains("AppHelpers"));
    }

    [Fact]
    public async Task Sentinel_StaticClassExemptionDisabled_ReportsStaticClass()
    {
        const string source = @"
namespace MyApp;
public static class AppHelpers
{
    public static string Format(string s)
    {
        if (s == null) return string.Empty;
        return s.Trim();
    }
}";
        var config = CreateSentinelConfig(new TestSentinelConfig
        {
            ExemptStaticClasses = false,
        });
        var solution = CreateAdhocSolution(("AppHelpers.cs", source));
        var engine = new LinterEngine(config);
        var violations = await engine.RunAsync(solution);

        Assert.Contains(violations, v =>
            v.RuleName == "StaticTestSentinel" && v.Details.Contains("AppHelpers"));
    }

    [Fact]
    public async Task Sentinel_InheritsFromExemption_DoesNotReportDerivedClass()
    {
        const string source = @"
namespace MyApp;
public abstract class ComponentBase
{
    public virtual void Init() { }
}

public sealed class MyComponent : ComponentBase
{
    public string Render(string input)
    {
        if (input == null) return string.Empty;
        return input;
    }
}";
        var config = CreateSentinelConfig(new TestSentinelConfig
        {
            ExemptWhenInheritsFrom = new[] { "ComponentBase" },
        });
        var solution = CreateAdhocSolution(("MyComponent.cs", source));
        var engine = new LinterEngine(config);
        var violations = await engine.RunAsync(solution);

        Assert.DoesNotContain(violations, v =>
            v.RuleName == "StaticTestSentinel" && v.Details.Contains("MyComponent"));
    }

    [Fact]
    public async Task Sentinel_InheritsFromExemption_DoesNotReportInterfaceImplementation()
    {
        const string source = @"
namespace MyApp;
public interface IValueConverter
{
    object? Convert(object? value, System.Type targetType, object? parameter, System.Globalization.CultureInfo culture);
}

public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object? Convert(object? value, System.Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        if (value is bool b && b) return ""Visible"";
        return ""Collapsed"";
    }
}";
        var config = CreateSentinelConfig(new TestSentinelConfig
        {
            ExemptWhenInheritsFrom = new[] { "IValueConverter" },
        });
        var solution = CreateAdhocSolution(("BoolToVisibilityConverter.cs", source));
        var engine = new LinterEngine(config);
        var violations = await engine.RunAsync(solution);

        Assert.DoesNotContain(violations, v =>
            v.RuleName == "StaticTestSentinel" && v.Details.Contains("BoolToVisibilityConverter"));
    }

    [Fact]
    public async Task Sentinel_EmptyExemptions_ReportsNormalClass()
    {
        var config = CreateSentinelConfig(new TestSentinelConfig
        {
            ExemptClassNameSuffixes = System.Array.Empty<string>(),
            ExemptWhenInheritsFrom = System.Array.Empty<string>(),
            ExemptStaticClasses = false,
        });
        var solution = CreateAdhocSolution(("OrderService.cs", SourceWithComplexMethod));
        var engine = new LinterEngine(config);
        var violations = await engine.RunAsync(solution);

        Assert.Contains(violations, v => v.RuleName == "StaticTestSentinel");
    }

    [Fact]
    public async Task Sentinel_ProjectOverride_ExemptsSuffixForMatchingProject()
    {
        const string source = @"
namespace MyApp;
public static class StringExtensions
{
    public static string Transform(this string s)
    {
        if (s == null) return string.Empty;
        return s;
    }
}";
        // Global config: no exemptions
        // ProjectOverride for "TestProject": ExemptClassNameSuffixes = ["Extensions"]
        var config = new LinterConfig
        {
            Global = new GlobalConfig
            {
                EnableTestSentinel = true,
                EnforceSealedClasses = false,
                AllowDynamic = false,
                AllowOutParameters = false,
                EnforceValueObjectContracts = false,
                EnforcePascalCase = false,
                EnforceXmlDocumentation = false,
                EnforceSemanticNaming = false,
                EnforceNullableEnable = false,
                EnforceNoSilentCatch = false,
                EnforceResultPatternOverExceptions = false,
                EnforceExplicitStateImmutability = false,                PreventContextDependentOverloads = false,                EnforceNamespaceDirectoryMapping = false,
                DetectAndBanPhantomDependencies = false,
            },
            Metrics = new MetricsConfig { MinCognitiveComplexityForTest = 0 },
            TestSentinel = new TestSentinelConfig(), // no global exemptions
            ProjectOverrides = new System.Collections.Generic.Dictionary<string, ProjectOverrideEntry>
            {
                ["TestProject"] = new ProjectOverrideEntry
                {
                    TestSentinel = new TestSentinelConfigOverride
                    {
                        ExemptClassNameSuffixes = new[] { "Extensions" }
                    }
                }
            }
        };

        var solution = CreateAdhocSolution(("StringExtensions.cs", source));
        var engine = new LinterEngine(config);
        var violations = await engine.RunAsync(solution);

        Assert.DoesNotContain(violations, v =>
            v.RuleName == "StaticTestSentinel" && v.Details.Contains("StringExtensions"));
    }
}
