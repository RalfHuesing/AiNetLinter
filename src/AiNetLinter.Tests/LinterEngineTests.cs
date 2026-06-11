using Xunit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using AiNetLinter.Configuration;
using AiNetLinter.Core;

namespace AiNetLinter.Tests;

public sealed class LinterEngineTests
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
                MaxCognitiveComplexity = 5,
                MaxInheritanceDepth = 2,
                MinCognitiveComplexityForTest = 3
            }
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
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        var solution = workspace.CurrentSolution.AddProject(projectInfo);
        foreach (var file in files)
        {
            var documentId = DocumentId.CreateNewId(projectId);
            solution = solution.AddDocument(documentId, file.fileName, file.content);
        }
        return solution;
    }

    [Fact]
    public void LinterEngine_CanBeInitialized()
    {
        var config = CreateDefaultConfig();
        var engine = new LinterEngine(config);
        Assert.NotNull(engine);
    }

    [Fact]
    public async Task Run_WithHighlyRelevantClassMissingTestClass_ReturnsSentinelViolation()
    {
        const string sourceClass = @"
namespace Domain;
public sealed class ComplexDomainService
{
    public void HighComplexityMethod(int x)
    {
        if (x > 1)
        {
            if (x > 2)
            {
                if (x > 3) {}
            }
        }
    }
}";
        var solution = CreateAdhocSolution(("ComplexDomainService.cs", sourceClass));
        var config = CreateDefaultConfig() with
        {
            Global = new GlobalConfig { EnableTestSentinel = true }
        };

        var engine = new LinterEngine(config);
        var violations = await engine.RunAsync(solution);

        Assert.Contains(violations, v => v.RuleName == "StaticTestSentinel");
    }

    [Fact]
    public async Task Run_WithInheritanceDepthExceeded_ReturnsViolation()
    {
        const string sourceCode = @"
namespace Test;
public class RootClass {}
public class ParentClass : RootClass {}
public sealed class ChildClass : ParentClass {}";

        var solution = CreateAdhocSolution(("Classes.cs", sourceCode));
        var config = CreateDefaultConfig() with
        {
            Metrics = new MetricsConfig { MaxInheritanceDepth = 1 }
        };

        var engine = new LinterEngine(config);
        var violations = await engine.RunAsync(solution);

        Assert.Contains(violations, v => v.RuleName == nameof(MetricsConfig.MaxInheritanceDepth));
    }

    [Fact]
    public async Task Run_WithDuplicateClassNamesInDifferentNamespaces_NoCrashAndResolvesCorrectly()
    {
        const string code = @"
namespace Test.N1
{
    public class MyClass {}
}
namespace Test.N2
{
    public class MyClass : Test.N1.MyClass {}
}";
        var solution = CreateAdhocSolution(("DuplicateClasses.cs", code));
        var config = CreateDefaultConfig() with
        {
            Metrics = new MetricsConfig { MaxInheritanceDepth = 1 }
        };

        var engine = new LinterEngine(config);
        var violations = await engine.RunAsync(solution);
        
        Assert.Empty(violations.Where(v => v.RuleName == nameof(MetricsConfig.MaxInheritanceDepth)));
    }

    [Fact]
    public async Task Run_WithConfigurableSentinelThreshold_RespectsConfigValue()
    {
        const string sourceClass = @"
namespace Domain;
public sealed class ComplexService
{
    public void MediumComplexityMethod(int x)
    {
        if (x > 1)
        {
            if (x > 2) {}
        }
    }
}";
        var solution = CreateAdhocSolution(("ComplexService.cs", sourceClass));
        var configLow = CreateDefaultConfig() with
        {
            Global = new GlobalConfig { EnableTestSentinel = true },
            Metrics = new MetricsConfig { MinCognitiveComplexityForTest = 1 }
        };

        var configHigh = CreateDefaultConfig() with
        {
            Global = new GlobalConfig { EnableTestSentinel = true },
            Metrics = new MetricsConfig { MinCognitiveComplexityForTest = 5 }
        };

        var engineLow = new LinterEngine(configLow);
        var violationsLow = await engineLow.RunAsync(solution);
        Assert.Contains(violationsLow, v => v.RuleName == "StaticTestSentinel");

        var engineHigh = new LinterEngine(configHigh);
        var violationsHigh = await engineHigh.RunAsync(solution);
        Assert.Empty(violationsHigh.Where(v => v.RuleName == "StaticTestSentinel"));
    }

    [Fact]
    public async Task Run_WithTestFileButNoTestMethods_SentinelFails()
    {
        const string sourceClass = @"
namespace Domain;
public sealed class HighlyRelevantService
{
    public void ComplexMethod(int x)
    {
        if (x > 1)
        {
            if (x > 2)
            {
                if (x > 3) {}
            }
        }
    }
}";
        const string testClass = @"
namespace Domain.Tests;
public class HighlyRelevantServiceTests
{
    // Keine Testmethoden
}";

        var solution = CreateAdhocSolution(
            ("HighlyRelevantService.cs", sourceClass),
            ("HighlyRelevantServiceTests.cs", testClass)
        );

        var config = CreateDefaultConfig() with
        {
            Global = new GlobalConfig { EnableTestSentinel = true },
            Metrics = new MetricsConfig { MinCognitiveComplexityForTest = 1 }
        };

        var engine = new LinterEngine(config);
        var violations = await engine.RunAsync(solution);

        Assert.Contains(violations, v => v.RuleName == "StaticTestSentinel");
    }

    [Fact]
    public void CreateWorkspaceProperties_ContainsDesignTimeBuildKeys()
    {
        var properties = LinterEngine.CreateWorkspaceProperties();

        Assert.Contains("DesignTimeBuild", properties.Keys);
        Assert.Contains("SkipCompilerExecution", properties.Keys);
        Assert.Contains("ProvideCommandLineArgs", properties.Keys);
        Assert.Contains("RunAnalyzers", properties.Keys);
        Assert.Contains("RunCodeAnalysis", properties.Keys);
        Assert.Equal("true", properties["DesignTimeBuild"]);
        Assert.Equal("false", properties["RunAnalyzers"]);
    }

    [Fact]
    public async Task Run_WithManyDocuments_ProducesExpectedViolations()
    {
        var files = new (string fileName, string content)[16];
        for (int i = 0; i < 16; i++)
        {
            files[i] = ($"Class{i}.cs", $@"
namespace Domain;
public class UnsealedClass{i}
{{
    public void TooManyParams(int a, int b, int c) {{}}
}}");
        }

        var solution = CreateAdhocSolution(files);
        var config = CreateDefaultConfig();
        var engine = new LinterEngine(config);

        var violations = await engine.RunAsync(solution);

        var violationKeys = violations
            .Select(v => (v.RuleName, v.FilePath, v.LineNumber))
            .ToHashSet();

        Assert.Equal(32, violationKeys.Count);
        Assert.Equal(16, violationKeys.Count(v => v.RuleName == nameof(GlobalConfig.EnforceSealedClasses)));
        Assert.Equal(16, violationKeys.Count(v => v.RuleName == nameof(MetricsConfig.MaxMethodParameterCount)));
    }
}
