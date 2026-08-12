#nullable enable

using System;
using System.Threading.Tasks;
using AiNetLinter.Cli;
using AiNetLinter.Configuration;
using AiNetLinter.Maps.Skeleton;
using AiNetLinter.TestKit;
using Microsoft.CodeAnalysis;
using Xunit;

namespace AiNetLinter.FastTests.Maps.Skeleton;

[Trait("Category", "Component")]
public sealed class SkeletonMapFilterTests
{
    private readonly Solution filterMini;

    public SkeletonMapFilterTests(PreparedSolutionFixture fixture)
    {
        filterMini = fixture.GetOrCreate(
            "FilterMini",
            () => RoslynTestSolutionFactory.CreateSolution(FilterMiniSolutionSpec.CreateProjectSpecs()));
    }

    [Fact]
    public async Task SkeletonMap_ExcludeTests_OutputContainsNoTestTypes()
    {
        var (output, console, exitCode) = await RunSkeletonAsync(new LinterArgs
        {
            TargetPath = "FilterMini.slnx", Verbose = false, ExcludeTests = true
        });

        Assert.Equal(0, exitCode);
        Assert.Empty(console.ErrorLines);
        Assert.DoesNotContain("## FilterMini.Tests.Core", output, StringComparison.Ordinal);
        Assert.Contains("## FilterMini.Core", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SkeletonMap_TestsOnly_OutputContainsOnlyTestNamespaces()
    {
        var (output, console, exitCode) = await RunSkeletonAsync(new LinterArgs
        {
            TargetPath = "FilterMini.slnx", Verbose = false, TestsOnly = true
        });

        Assert.Equal(0, exitCode);
        Assert.Empty(console.ErrorLines);
        Assert.Contains("## FilterMini.Tests.Core", output, StringComparison.Ordinal);
        Assert.DoesNotContain("## FilterMini.Core", output, StringComparison.Ordinal);
        Assert.DoesNotContain("## FilterMini.Utils", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SkeletonMap_ProjectFilter_OutputContainsOnlyMatchingProject()
    {
        var (output, console, exitCode) = await RunSkeletonAsync(new LinterArgs
        {
            TargetPath = "FilterMini.slnx", Verbose = false, IncludeProjects = ["FilterMini"]
        });

        Assert.Equal(0, exitCode);
        Assert.Empty(console.ErrorLines);
        Assert.Contains("## FilterMini.Core", output, StringComparison.Ordinal);
        Assert.DoesNotContain("## FilterMini.Tests.Core", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SkeletonMap_ProjectGlobFilter_WildcardMatchesTests()
    {
        var (output, _, exitCode) = await RunSkeletonAsync(new LinterArgs
        {
            TargetPath = "FilterMini.slnx", Verbose = false, IncludeProjects = ["*.Tests"]
        });

        Assert.Equal(0, exitCode);
        Assert.Contains("## FilterMini.Tests.Core", output, StringComparison.Ordinal);
        Assert.DoesNotContain("## FilterMini.Core", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SkeletonMap_ExcludeProjectByGlob_OutputExcludesTests()
    {
        var (output, console, exitCode) = await RunSkeletonAsync(new LinterArgs
        {
            TargetPath = "FilterMini.slnx", Verbose = false, ExcludeProjects = ["*.Tests"]
        });

        Assert.Equal(0, exitCode);
        Assert.Empty(console.ErrorLines);
        Assert.DoesNotContain("## FilterMini.Tests.Core", output, StringComparison.Ordinal);
        Assert.Contains("## FilterMini.Core", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SkeletonMap_ExcludeProjectByExactName_OutputExcludesProject()
    {
        var (output, _, exitCode) = await RunSkeletonAsync(new LinterArgs
        {
            TargetPath = "FilterMini.slnx", Verbose = false, ExcludeProjects = ["FilterMini"]
        });

        Assert.Equal(0, exitCode);
        Assert.DoesNotContain("## FilterMini.Core", output, StringComparison.Ordinal);
        Assert.DoesNotContain("## FilterMini.Utils", output, StringComparison.Ordinal);
        Assert.Contains("## FilterMini.Tests.Core", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SkeletonMap_NamespaceFilter_OutputContainsOnlyCoreNamespace()
    {
        var (output, console, exitCode) = await RunSkeletonAsync(new LinterArgs
        {
            TargetPath = "FilterMini.slnx", Verbose = false, IncludeNamespaces = ["FilterMini.Core"]
        });

        Assert.Equal(0, exitCode);
        Assert.Empty(console.ErrorLines);
        Assert.Contains("Widget", output, StringComparison.Ordinal);
        Assert.DoesNotContain("Formatter", output, StringComparison.Ordinal);
        Assert.DoesNotContain("WidgetTests", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SkeletonMap_NamespaceGlobFilter_MatchesSubnamespaces()
    {
        var (output, console, exitCode) = await RunSkeletonAsync(new LinterArgs
        {
            TargetPath = "FilterMini.slnx", Verbose = false, IncludeNamespaces = ["FilterMini.Tests.*"]
        });

        Assert.Equal(0, exitCode);
        Assert.Empty(console.ErrorLines);
        Assert.Contains("## FilterMini.Tests.Core", output, StringComparison.Ordinal);
        Assert.DoesNotContain("## FilterMini.Core", output, StringComparison.Ordinal);
        Assert.DoesNotContain("## FilterMini.Utils", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SkeletonMap_ExcludeNamespace_OutputExcludesNamespace()
    {
        var (output, console, exitCode) = await RunSkeletonAsync(new LinterArgs
        {
            TargetPath = "FilterMini.slnx", Verbose = false, ExcludeNamespaces = ["FilterMini.Core"]
        });

        Assert.Equal(0, exitCode);
        Assert.Empty(console.ErrorLines);
        Assert.DoesNotContain("## FilterMini.Core", output, StringComparison.Ordinal);
        Assert.DoesNotContain("### Widget ", output, StringComparison.Ordinal);
        Assert.Contains("## FilterMini.Utils", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SkeletonMap_ExcludeNamespaceGlob_ExcludesAllTestNamespaces()
    {
        var (output, _, exitCode) = await RunSkeletonAsync(new LinterArgs
        {
            TargetPath = "FilterMini.slnx", Verbose = false, ExcludeNamespaces = ["FilterMini.Tests*"]
        });

        Assert.Equal(0, exitCode);
        Assert.DoesNotContain("FilterMini.Tests", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SkeletonMap_PublicOnly_OutputExcludesPrivateMethods()
    {
        var (output, console, exitCode) = await RunSkeletonAsync(new LinterArgs
        {
            TargetPath = "FilterMini.slnx", Verbose = false,
            IncludeNamespaces = ["FilterMini.Core"], PublicOnly = true
        });

        Assert.Equal(0, exitCode);
        Assert.Empty(console.ErrorLines);
        Assert.DoesNotContain("private ", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SkeletonMap_WithoutPublicOnly_OutputContainsPrivateMembers()
    {
        var (output, _, exitCode) = await RunSkeletonAsync(new LinterArgs
        {
            TargetPath = "FilterMini.slnx", Verbose = false, IncludeNamespaces = ["FilterMini.Core"]
        });

        Assert.Equal(0, exitCode);
        Assert.Contains("private ", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SkeletonMap_ExcludeTestsAndPublicOnly_ShowsOnlyPublicProductionTypes()
    {
        var (output, console, exitCode) = await RunSkeletonAsync(new LinterArgs
        {
            TargetPath = "FilterMini.slnx", Verbose = false, ExcludeTests = true, PublicOnly = true
        });

        Assert.Equal(0, exitCode);
        Assert.Empty(console.ErrorLines);
        Assert.DoesNotContain("FilterMini.Tests", output, StringComparison.Ordinal);
        Assert.DoesNotContain("private ", output, StringComparison.Ordinal);
        Assert.Contains("## FilterMini.Core", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SkeletonMap_ProjectAndNamespaceFilter_NarrowsOutputFurther()
    {
        var (output, console, exitCode) = await RunSkeletonAsync(new LinterArgs
        {
            TargetPath = "FilterMini.slnx", Verbose = false,
            IncludeProjects = ["FilterMini"], IncludeNamespaces = ["FilterMini.Core"]
        });

        Assert.Equal(0, exitCode);
        Assert.Empty(console.ErrorLines);
        Assert.Contains("Widget", output, StringComparison.Ordinal);
        Assert.DoesNotContain("Formatter", output, StringComparison.Ordinal);
        Assert.DoesNotContain("WidgetTests", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SkeletonMap_TestsOnlyAndNamespaceFilter_ShowsOnlyMatchingTestNamespace()
    {
        var (output, console, exitCode) = await RunSkeletonAsync(new LinterArgs
        {
            TargetPath = "FilterMini.slnx", Verbose = false,
            TestsOnly = true, IncludeNamespaces = ["FilterMini.Tests.Core"]
        });

        Assert.Equal(0, exitCode);
        Assert.Empty(console.ErrorLines);
        Assert.Contains("## FilterMini.Tests.Core", output, StringComparison.Ordinal);
        Assert.DoesNotContain("## FilterMini.Core", output, StringComparison.Ordinal);
        Assert.DoesNotContain("## FilterMini.Utils", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SkeletonMap_UnknownProject_ReturnsEmptyOutputWithoutError()
    {
        var (output, console, exitCode) = await RunSkeletonAsync(new LinterArgs
        {
            TargetPath = "FilterMini.slnx", Verbose = false, IncludeProjects = ["NonExistentProject"]
        });

        Assert.Equal(0, exitCode);
        Assert.Empty(console.ErrorLines);
        Assert.DoesNotContain("```csharp", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SkeletonMap_UnknownNamespace_ReturnsEmptyOutputWithoutError()
    {
        var (output, console, exitCode) = await RunSkeletonAsync(new LinterArgs
        {
            TargetPath = "FilterMini.slnx", Verbose = false, IncludeNamespaces = ["NonExistent.Namespace"]
        });

        Assert.Equal(0, exitCode);
        Assert.Empty(console.ErrorLines);
        Assert.DoesNotContain("```csharp", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SkeletonMap_ExcludeTestsAndTestsOnly_ExcludeTestsTakesPrecedence()
    {
        var (_, console, exitCode) = await RunSkeletonAsync(new LinterArgs
        {
            TargetPath = "FilterMini.slnx", Verbose = false, ExcludeTests = true, TestsOnly = true
        });

        Assert.Equal(0, exitCode);
        Assert.Empty(console.ErrorLines);
    }

    private async Task<(string Output, RecordingLintConsole Console, int ExitCode)> RunSkeletonAsync(LinterArgs args)
    {
        var console = new RecordingLintConsole();
        var request = new SkeletonMapBuildRequest("FilterMini.slnx", CreateConfig(), console, args);
        var exitCode = await SkeletonMapBuilder.BuildAsync(filterMini, request);
        return (console.OutputText, console, exitCode);
    }

    private static Config CreateConfig() => new()
    {
        Global = new GlobalConfig(),
        Metrics = new MetricsConfig(),
    };
}
