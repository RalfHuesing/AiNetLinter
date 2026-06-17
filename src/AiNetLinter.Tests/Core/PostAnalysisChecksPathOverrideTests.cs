#nullable enable

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Xunit;
using AiNetLinter.Configuration;
using AiNetLinter.Core;
using AiNetLinter.Metrics;
using AiNetLinter.Models;

namespace AiNetLinter.Tests.Core;

/// <summary>
/// Testet, dass PathOverrides in PostAnalysisChecks korrekt aufgelöst werden,
/// wenn SolutionBasePath gesetzt ist.
/// </summary>
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
            new ConcurrentDictionary<string, string>(),
            new ConcurrentDictionary<INamedTypeSymbol, FieldReadonlyTracker>(SymbolEqualityComparer.Default));
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

    private static LinterConfig MakeConfig(int globalLimit, int pathOverrideLimit) => new()
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
    public void AIContextFootprint_WithPathOverride_NoViolationWhenUnderOverrideLimit()
    {
        // footprint 7000 > global 5000 but ≤ override 12000 → no violation
        var state = CreateState(MakeClass("DataTablePage", TestFilePath, footprint: 7000));

        PostAnalysisChecks.Run(state, MakeConfig(globalLimit: 5000, pathOverrideLimit: 12000));

        Assert.Empty(state.Violations.Where(v => v.RuleName == "AIContextFootprint"));
    }

    [Fact]
    public void AIContextFootprint_WithPathOverride_ViolationWhenAboveOverrideLimit()
    {
        // footprint 13000 > override 12000 → violation even with override
        var state = CreateState(MakeClass("DataTablePage", TestFilePath, footprint: 13000));

        PostAnalysisChecks.Run(state, MakeConfig(globalLimit: 5000, pathOverrideLimit: 12000));

        Assert.Contains(state.Violations, v => v.RuleName == "AIContextFootprint");
    }

    [Fact]
    public void AIContextFootprint_FileOutsideOverridePath_UsesGlobalLimit()
    {
        // Production file is NOT under the PathOverride glob → global limit (5000) applies
        var state = CreateState(MakeClass("ProdPage", OtherFilePath, footprint: 7000));

        PostAnalysisChecks.Run(state, MakeConfig(globalLimit: 5000, pathOverrideLimit: 12000));

        Assert.Contains(state.Violations, v => v.RuleName == "AIContextFootprint");
    }

    [Fact]
    public void AIContextFootprint_PartialClass_UsesRepresentativeFileForPathOverride()
    {
        // Partial class with one file under the override path → override applies, no violation
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

        PostAnalysisChecks.Run(state, MakeConfig(globalLimit: 5000, pathOverrideLimit: 12000));

        Assert.Empty(state.Violations.Where(v => v.RuleName == "AIContextFootprint"));
    }

    [Fact]
    public void AIContextFootprint_WildcardPattern_MatchesNestedPaths()
    {
        // Ensure ** glob correctly matches nested path segments
        var state = CreateState(MakeClass("Nested", @"C:\Solution\App\Pages\Test\Sub\Deep\Page.cs", footprint: 7000));
        var config = new LinterConfig
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

        PostAnalysisChecks.Run(state, config);

        Assert.Empty(state.Violations.Where(v => v.RuleName == "AIContextFootprint"));
    }
}
