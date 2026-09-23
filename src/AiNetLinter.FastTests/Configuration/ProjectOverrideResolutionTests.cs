#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using AiNetLinter.Configuration;
using AiNetLinter.TestKit;
using Xunit;

namespace AiNetLinter.FastTests.Configuration;

/// <summary>
/// Belegt, dass die drei neuen Testziel-Projektnamen ueber den produktiven ProjectOverrides-Vertrag
/// in ainetlinter-rules.json den konfigurierten Test-Override erhalten.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ProjectOverrideResolutionTests
{
    [Theory]
    [InlineData("AiNetLinter.FastTests")]
    [InlineData("AiNetLinter.IntegrationTests")]
    [InlineData("AiNetLinter.TestKit")]
    public void ResolveForProject_NewTestProjectNames_AppliesTestOverride(string projectName)
    {
        var configContentPath = Path.Combine(SolutionRootLocator.Find(), "ainetlinter-rules.json");
        var globalConfig = ConfigLoader.TryLoadConfig(configContentPath, isRequired: true);
        Assert.NotNull(globalConfig);

        var resolved = ProjectConfigResolver.ResolveForProject(projectName, globalConfig!);

        Assert.False(resolved.Global.EnforceSealedClasses);
        Assert.Equal(100, resolved.Metrics.MaxMethodLineCount);
    }

    [Fact]
    public void ResolveForProject_DeadCodeApiSurface_UsesFirstMatchAndInheritsWhenMissing()
    {
        var config = TestHelper.CreateDefaultConfig() with
        {
            DeadCode = new DeadCodeConfig { DefaultApiSurface = "closed_solution" },
            ProjectOverrides = new Dictionary<string, ProjectOverrideEntry>
            {
                ["Sdk*"] = new() { DeadCode = new DeadCodeConfigOverride { ApiSurface = "external_library" } },
                ["SdkPublic"] = new() { DeadCode = new DeadCodeConfigOverride { ApiSurface = "closed_solution" } },
                ["Application"] = new(),
            },
            PathOverrides = new Dictionary<string, ProjectOverrideEntry>
            {
                ["**"] = new() { DeadCode = new DeadCodeConfigOverride { ApiSurface = "external_library" } },
            },
        };

        Assert.Equal("external_library", ProjectConfigResolver.ResolveForProject("SdkPublic", config).DeadCode.DefaultApiSurface);
        Assert.Equal("closed_solution", ProjectConfigResolver.ResolveForProject("Application", config).DeadCode.DefaultApiSurface);
    }
}
