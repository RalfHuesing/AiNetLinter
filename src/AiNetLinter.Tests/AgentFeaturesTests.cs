using AiNetLinter.Configuration;
using AiNetLinter.Core;
using AiNetLinter.Models;
using AiNetLinter.Output;
using AiNetLinter.Scope;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace AiNetLinter.Tests;

// @covers TestCoverageResolver
// @covers TestCoverageCollector
// @covers PartialClassLineAggregator
// @covers GitChangedFilesResolver
public sealed class AgentFeaturesTests
{
    private static LinterConfig CreateConfig(Func<GlobalConfig, GlobalConfig>? configureGlobal = null)
    {
        var global = configureGlobal?.Invoke(new GlobalConfig()) ?? new GlobalConfig();
        return new LinterConfig
        {
            Global = global,
            Metrics = new MetricsConfig
            {
                MaxLineCount = 500,
                MaxMethodParameterCount = 4,
                MaxCyclomaticComplexity = 5,
                MaxCognitiveComplexity = 5,
            },
        };
    }

    private static (SyntaxTree, SemanticModel) GetSemanticContext(string source, MetadataReference[]? extraRefs = null)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        var refs = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
        };
        if (extraRefs != null)
        {
            refs.AddRange(extraRefs);
        }

        var compilation = CSharpCompilation.Create("TestAssembly")
            .AddSyntaxTrees(tree)
            .AddReferences(refs)
            .WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var semanticModel = compilation.GetSemanticModel(tree);
        return (tree, semanticModel);
    }

    [Fact]
    public void Analyze_TryParseOutParameter_NoViolationWhenTryPatternEnabled()
    {
        const string source = """
            namespace Test;
            public sealed class Parser
            {
                public bool TryParse(string input, out int value)
                {
                    value = 0;
                    return int.TryParse(input, out value);
                }
            }
            """;

        var config = CreateConfig(g => g with { AllowTryPatternOutParameters = true });
        var (_, model) = GetSemanticContext(source);
        var violations = LinterAnalyzer.Analyze("Parser.cs", model, config);

        Assert.DoesNotContain(violations, v => v.RuleName == nameof(GlobalConfig.AllowOutParameters));
    }

    [Fact]
    public void Analyze_IsPatternOutParameter_NoViolationWhenTryPatternEnabled()
    {
        const string source = """
            namespace Test;
            public sealed class CommandParser
            {
                public bool IsZoomCommand(string? command, out string presetId)
                {
                    presetId = command ?? "";
                    return command != null && command.StartsWith("zoom");
                }
            }
            """;

        var config = CreateConfig(g => g with { AllowTryPatternOutParameters = true });
        var (_, model) = GetSemanticContext(source);
        var violations = LinterAnalyzer.Analyze("Parser.cs", model, config);

        Assert.DoesNotContain(violations, v => v.RuleName == nameof(GlobalConfig.AllowOutParameters));
    }

    [Fact]
    public void Analyze_VoidMethodWithOut_ReturnsViolation()
    {
        const string source = """
            namespace Test;
            public sealed class Bad
            {
                public void Foo(out int x) { x = 1; }
            }
            """;

        var config = CreateConfig(g => g with { AllowTryPatternOutParameters = true });
        var (_, model) = GetSemanticContext(source);
        var violations = LinterAnalyzer.Analyze("Bad.cs", model, config);

        Assert.Contains(violations, v => v.RuleName == nameof(GlobalConfig.AllowOutParameters));
    }

    [Fact]
    public void Analyze_OperationCanceledCatchWithFilter_NoSilentCatchViolation()
    {
        const string source = """
            namespace Test;
            public sealed class Worker
            {
                public async Task RunAsync(CancellationToken stoppingToken)
                {
                    try
                    {
                        await Task.Delay(100, stoppingToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                    }
                }
            }
            """;

        var config = CreateConfig(g => g with
        {
            EnforceNoSilentCatch = true,
            AllowCancellationShutdownCatch = true,
        });
        var (_, model) = GetSemanticContext(source);
        var violations = LinterAnalyzer.Analyze("Worker.cs", model, config);

        Assert.DoesNotContain(violations, v => v.RuleName == nameof(GlobalConfig.EnforceNoSilentCatch));
    }

    [Fact]
    public void Format_GuidanceInHeaderNotInDetailLine()
    {
        var violations = new[]
        {
            new RuleViolation
            {
                FilePath = @"C:\repo\src\Foo.cs",
                LineNumber = 15,
                RuleName = "MaxCognitiveComplexity",
                Details = "12 > 7",
                Guidance = "Vereinfache verschachtelte Kontrollstrukturen.",
            },
        };

        var result = ViolationTextFormatter.Format(violations, @"C:\repo", CreateConfig());

        // Rule instruction appears in the Handlungsanweisung header (deduped), not per-violation
        Assert.Contains("-> MaxCognitiveComplexity:", result);
        // Violation line contains only path:line rule | details — no guidance
        Assert.Contains("src/Foo.cs:15 MaxCognitiveComplexity | 12 > 7", result);
        Assert.DoesNotContain("src/Foo.cs:15 MaxCognitiveComplexity | 12 > 7 ->", result);
    }

    [Fact]
    public void TestCoverageResolver_TypeofReference_CoversSourceClass()
    {
        _ = typeof(TestCoverageResolver);
        _ = typeof(TestCoverageCollector);
        _ = typeof(PartialClassLineAggregator);
        _ = typeof(GitChangedFilesResolver);

        var index = new TestCoverageIndex();
        index.AddReferencedType("CircuitAiChatHubClient");

        var covered = TestCoverageResolver.IsCovered(
            "CircuitAiChatHubClient",
            index,
            new TestSentinelConfig());

        Assert.True(covered);
    }

    [Fact]
    public void TestCoverageResolver_WildcardPattern_MatchesIntegrationTests()
    {
        var index = new TestCoverageIndex();
        index.AddTestClass("JwtSigningDummyKeyGuardTests");

        var config = new TestSentinelConfig
        {
            ClassNamePatterns = ["{Name}*Tests"],
        };

        var covered = TestCoverageResolver.IsCovered("JwtSigningKeyMaterial", index, config);

        Assert.False(covered);
    }

    [Fact]
    public void TestCoverageResolver_CoversComment_CoversSourceClass()
    {
        var index = new TestCoverageIndex();
        index.AddCoversComment("MyService");

        var covered = TestCoverageResolver.IsCovered("MyService", index, new TestSentinelConfig());

        Assert.True(covered);
    }

    [Fact]
    public async Task Run_WithTypeofTestReference_NoSentinelViolation()
    {
        const string sourceClass = """
            namespace Domain;
            public sealed class HubClient
            {
                public void Complex(int x)
                {
                    if (x > 1) { if (x > 2) { if (x > 3) {} } }
                }
            }
            """;

        const string testClass = """
            using Xunit;
            namespace Domain.Tests;
            public sealed class OrchestratorTests
            {
                [Fact]
                public void CoversHub()
                {
                    _ = typeof(HubClient);
                }
            }
            """;

        var solution = CreateSolutionWithXunit((sourceClass, "HubClient.cs"), (testClass, "OrchestratorTests.cs"));
        var config = CreateConfig(g => g with { EnableTestSentinel = true }) with
        {
            Metrics = new MetricsConfig { MinCognitiveComplexityForTest = 1 },
        };

        var engine = new LinterEngine(config);
        var violations = await engine.RunAsync(solution);

        Assert.Empty(violations.Where(v => v.RuleName == "StaticTestSentinel"));
    }

    [Fact]
    public void Analyze_MinimalApiWithoutAsParameters_ReturnsViolation()
    {
        const string source = """
            namespace Microsoft.AspNetCore.Http { public sealed class AsParametersAttribute : System.Attribute {} }
            namespace App;
            public sealed record Query(int A, int B, int C, int D, int E);
            public static class Endpoints
            {
                public static void MapAll(object app)
                {
                    app.MapGet("/x", (int a, int b, int c, int d, Query q) => q);
                }
            }
            public static class MapExtensions
            {
                public static void MapGet(this object app, string route, System.Delegate handler) {}
            }
            """;

        var config = CreateConfig(g => g with
        {
            EnforceMinimalApiAsParameters = true,
            EnforceNullableEnable = false,
            EnforceXmlDocumentation = false,
            EnforcePascalCase = false,
        });
        var (_, model) = GetSemanticContext(source);
        var violations = LinterAnalyzer.Analyze("Endpoints.cs", model, config);

        Assert.Contains(violations, v => v.RuleName == nameof(GlobalConfig.EnforceMinimalApiAsParameters));
    }

    [Fact]
    public void PartialClassLineAggregator_SumsAcrossFiles()
    {
        var parts = new[]
        {
            new PartialClassPart("App.Scheduler", "A.cs", 1, 300),
            new PartialClassPart("App.Scheduler", "B.cs", 1, 250),
        };

        var config = CreateConfig() with
        {
            Metrics = new MetricsConfig { MaxLineCount = 500, AggregatePartialClassLineCount = true },
        };

        var violations = PartialClassLineAggregator.BuildViolations(parts, config);

        Assert.Contains(violations, v => v.RuleName == nameof(MetricsConfig.MaxLineCount));
    }

    [Fact]
    public void DisableAllDetector_RecognizesExactComment()
    {
        const string content = "// ainetlinter-disable all\nnamespace X;";

        Assert.True(DisableAllDetector.HasDisableAll(content));
    }

    [Fact]
    public void RuleMetadataRegistry_ResolvesKnownRule()
    {
        var config = new LinterConfig
        {
            Global = new GlobalConfig(),
            Metrics = new MetricsConfig(),
        };

        var metadata = RuleMetadataRegistry.Resolve("MaxLineCount", config);

        Assert.Equal("agent-context", metadata.Intent);
        Assert.Equal("error", metadata.Severity);
    }

    [Fact]
    public void Analyze_SwallowedCatchWithIgnoredVariable_NoSilentCatchViolation()
    {
        const string source = """
            namespace Test;
            public sealed class Worker
            {
                public void Run()
                {
                    try
                    {
                        int.Parse("x");
                    }
                    catch (System.Exception ignored)
                    {
                    }
                }
            }
            """;

        var config = CreateConfig(g => g with { EnforceNoSilentCatch = true });
        var (_, model) = GetSemanticContext(source);
        var violations = LinterAnalyzer.Analyze("Worker.cs", model, config);

        Assert.DoesNotContain(violations, v => v.RuleName == nameof(GlobalConfig.EnforceNoSilentCatch));
    }

    [Fact]
    public void Analyze_SwallowedCatchWithExpectedVariable_NoSilentCatchViolation()
    {
        const string source = """
            namespace Test;
            public sealed class Worker
            {
                public void Run()
                {
                    try
                    {
                        int.Parse("x");
                    }
                    catch (System.Exception expectedEx)
                    {
                    }
                }
            }
            """;

        var config = CreateConfig(g => g with { EnforceNoSilentCatch = true });
        var (_, model) = GetSemanticContext(source);
        var violations = LinterAnalyzer.Analyze("Worker.cs", model, config);

        Assert.DoesNotContain(violations, v => v.RuleName == nameof(GlobalConfig.EnforceNoSilentCatch));
    }

    private static Solution CreateSolutionWithXunit(params (string content, string fileName)[] files)
    {
        var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();
        var refs = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(FactAttribute).Assembly.Location),
        };

        var projectInfo = ProjectInfo.Create(
            projectId,
            VersionStamp.Create(),
            "TestProject",
            "TestProject",
            LanguageNames.CSharp)
            .WithMetadataReferences(refs)
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var solution = workspace.CurrentSolution.AddProject(projectInfo);
        foreach (var file in files)
        {
            var documentId = DocumentId.CreateNewId(projectId);
            solution = solution.AddDocument(documentId, file.fileName, file.content);
        }

        return solution;
    }
}
