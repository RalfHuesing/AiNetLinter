using System.Collections.Generic;
using Xunit;
using AiNetLinter.Configuration;

namespace AiNetLinter.Tests.Configuration;

public sealed class PathOverridesTests
{
    private static LinterConfig CreateBaseConfig(int maxMethodLines = 42) => new()
    {
        Global = new GlobalConfig { EnforceSealedClasses = false },
        Metrics = new MetricsConfig { MaxMethodLineCount = maxMethodLines }
    };

    [Fact]
    public void MatchesGlobPath_DoubleStar_MatchesNestedFolders()
    {
        Assert.True(ProjectConfigResolver.MatchesGlobPath("src/MyApp/Handlers/OrderHandler.cs", "src/MyApp/Handlers/**"));
        Assert.True(ProjectConfigResolver.MatchesGlobPath("src/MyApp/Handlers/Sub/OrderHandler.cs", "src/MyApp/Handlers/**"));
    }

    [Fact]
    public void MatchesGlobPath_SingleStar_MatchesOnlyDirectFiles()
    {
        Assert.True(ProjectConfigResolver.MatchesGlobPath("src/MyApp/Handlers/OrderHandler.cs", "src/MyApp/Handlers/*"));
        Assert.False(ProjectConfigResolver.MatchesGlobPath("src/MyApp/Handlers/Sub/OrderHandler.cs", "src/MyApp/Handlers/*"));
    }

    [Fact]
    public void MatchesGlobPath_CaseInsensitive()
    {
        Assert.True(ProjectConfigResolver.MatchesGlobPath("SRC/MyApp/Handlers/OrderHandler.cs", "src/myapp/handlers/**"));
    }

    [Fact]
    public void ResolveRelativePath_WithBasePath_ComputesRelativePath()
    {
        var relative = ProjectConfigResolver.ResolveRelativePath(
            @"C:\Solution\src\MyApp\Handlers\OrderHandler.cs",
            @"C:\Solution");
        Assert.Equal("src/MyApp/Handlers/OrderHandler.cs", relative);
    }

    [Fact]
    public void ResolveRelativePath_WithoutBasePath_ReturnsFallback()
    {
        var relative = ProjectConfigResolver.ResolveRelativePath(
            @"C:\Solution\src\MyApp\Handlers\OrderHandler.cs",
            null);
        Assert.Equal("C:/Solution/src/MyApp/Handlers/OrderHandler.cs", relative);
    }

    [Fact]
    public void FileInMatchingPath_OverrideApplied()
    {
        var config = CreateBaseConfig(maxMethodLines: 42) with
        {
            SolutionBasePath = @"C:\Solution",
            PathOverrides = new Dictionary<string, ProjectOverrideEntry>
            {
                ["src/MyApp/Handlers/**"] = new ProjectOverrideEntry
                {
                    Metrics = new MetricsConfigOverride { MaxMethodLineCount = 80 }
                }
            }
        };

        var effectiveMetrics = config.Metrics.Apply(
            config.PathOverrides["src/MyApp/Handlers/**"].Metrics);

        Assert.Equal(80, effectiveMetrics.MaxMethodLineCount);
    }

    [Fact]
    public void PathOverrides_AppliedAfterProjectOverride_PathWins()
    {
        var global = CreateBaseConfig(maxMethodLines: 42) with
        {
            SolutionBasePath = @"C:\Solution",
            ProjectOverrides = new Dictionary<string, ProjectOverrideEntry>
            {
                ["MyApp"] = new ProjectOverrideEntry
                {
                    Metrics = new MetricsConfigOverride { MaxMethodLineCount = 60 }
                }
            },
            PathOverrides = new Dictionary<string, ProjectOverrideEntry>
            {
                ["src/MyApp/Handlers/**"] = new ProjectOverrideEntry
                {
                    Metrics = new MetricsConfigOverride { MaxMethodLineCount = 80 }
                }
            }
        };

        // After ProjectOverride: 60
        var afterProject = global.Metrics.Apply(global.ProjectOverrides["MyApp"].Metrics);
        Assert.Equal(60, afterProject.MaxMethodLineCount);

        // After PathOverride: 80 (wins)
        var afterPath = afterProject.Apply(global.PathOverrides["src/MyApp/Handlers/**"].Metrics);
        Assert.Equal(80, afterPath.MaxMethodLineCount);
    }
}
