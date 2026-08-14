#nullable enable

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Xunit;
using AiNetLinter.Configuration;
using AiNetLinter.Core;
using AiNetLinter.Metrics;
using AiNetLinter.Models;

namespace AiNetLinter.FastTests.Metrics;

/// <summary>
/// Testet, dass PathOverrides in PostAnalysisChecks korrekt aufgeloest werden,
/// wenn SolutionBasePath gesetzt ist.
/// </summary>
[Trait("Category", "Unit")]
public sealed class PostAnalysisChecksPathOverrideTests
{
    private static readonly string SolutionBase = @"C:\Solution";
    private static readonly string TestFilePath = @"C:\Solution\App\Pages\Test\DataTablePage.cs";
    private static readonly string OtherFilePath = @"C:\Solution\App\Pages\Production\ProdPage.cs";

    private static AnalysisState CreateState(params ClassInfo[] classes)
    {
        using var workspace = new AdhocWorkspace();
        return new AnalysisState(
            workspace.CurrentSolution,
            new ConcurrentBag<RuleViolation>(),
            new TestCoverageIndex(),
            new ConcurrentBag<ClassInfo>(classes),
            new ConcurrentBag<PartialClassPart>(),
            new ConcurrentDictionary<string, string>());
    }

    private static ClassInfo MakeClass(string name, string filePath, int footprint, string? project = null) =>
        new()
        {
            Name = name,
            FilePath = filePath,
            LineNumber = 1,
            MaxCognitiveComplexity = 0,
            InheritanceDepth = 0,
            AIContextFootprint = footprint,
            HasTestMethods = false,
            IsPartial = false,
            ProjectName = project,
        };

    private static Config MakeConfig(int globalLimit, int pathOverrideLimit) => TestHelper.CreateDefaultConfig() with
    {
        Global = new GlobalConfig
        {
            EnableTestSentinel = false,
            EnforceSealedClasses = false,
        },
        Metrics = new MetricsConfig { MaxAIContextFootprint = globalLimit },
        SolutionBasePath = SolutionBase,
        PathOverrides = new Dictionary<string, ProjectOverrideEntry>
        {
            ["App/Pages/Test/**"] = new ProjectOverrideEntry
            {
                Metrics = new MetricsConfigOverride { MaxAIContextFootprint = pathOverrideLimit }
            }
        }
    };

    [Fact]
    public async Task AIContextFootprint_WithPathOverride_NoViolationWhenUnderOverrideLimit()
    {
        var state = CreateState(MakeClass("DataTablePage", TestFilePath, footprint: 7000));

        await PostAnalysisChecks.RunAsync(state, MakeConfig(globalLimit: 5000, pathOverrideLimit: 12000));

        Assert.Empty(state.Violations.Where(v => v.RuleName == "AIContextFootprint"));
    }

    [Fact]
    public async Task AIContextFootprint_WithPathOverride_ViolationWhenAboveOverrideLimit()
    {
        var state = CreateState(MakeClass("DataTablePage", TestFilePath, footprint: 13000));

        await PostAnalysisChecks.RunAsync(state, MakeConfig(globalLimit: 5000, pathOverrideLimit: 12000));

        Assert.Contains(state.Violations, v => v.RuleName == "AIContextFootprint");
    }

    [Fact]
    public async Task AIContextFootprint_FileOutsideOverridePath_UsesGlobalLimit()
    {
        var state = CreateState(MakeClass("ProdPage", OtherFilePath, footprint: 7000));

        await PostAnalysisChecks.RunAsync(state, MakeConfig(globalLimit: 5000, pathOverrideLimit: 12000));

        Assert.Contains(state.Violations, v => v.RuleName == "AIContextFootprint");
    }

    [Fact]
    public async Task AIContextFootprint_PartialClass_UsesRepresentativeFileForPathOverride()
    {
        var state = CreateState(new ClassInfo
        {
            Name = "DataTablePage",
            FilePath = TestFilePath,
            LineNumber = 1,
            MaxCognitiveComplexity = 0,
            InheritanceDepth = 0,
            AIContextFootprint = 7000,
            HasTestMethods = false,
            IsPartial = true,
            ProjectName = null,
        });

        await PostAnalysisChecks.RunAsync(state, MakeConfig(globalLimit: 5000, pathOverrideLimit: 12000));

        Assert.Empty(state.Violations.Where(v => v.RuleName == "AIContextFootprint"));
    }

    [Fact]
    public async Task AIContextFootprint_WildcardPattern_MatchesNestedPaths()
    {
        var state = CreateState(MakeClass("Nested", @"C:\Solution\App\Pages\Test\Sub\Deep\Page.cs", footprint: 7000));
        var config = TestHelper.CreateDefaultConfig() with
        {
            Global = new GlobalConfig { EnableTestSentinel = false, EnforceSealedClasses = false },
            Metrics = new MetricsConfig { MaxAIContextFootprint = 5000 },
            SolutionBasePath = SolutionBase,
            PathOverrides = new Dictionary<string, ProjectOverrideEntry>
            {
                ["App/Pages/Test/**"] = new ProjectOverrideEntry
                {
                    Metrics = new MetricsConfigOverride { MaxAIContextFootprint = 12000 }
                }
            }
        };

        await PostAnalysisChecks.RunAsync(state, config);

        Assert.Empty(state.Violations.Where(v => v.RuleName == "AIContextFootprint"));
    }
}
