#nullable enable

using System;
using System.IO;
using AiNetLinter.Configuration;
using AiNetLinter.TestKit;
using Xunit;

namespace AiNetLinter.FastTests.Configuration;

/// <summary>
/// Belegt, dass die drei neuen Testziel-Projektnamen ueber den produktiven ProjectOverrides-Vertrag
/// in rules.json den konfigurierten Test-Override erhalten.
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
        var rulesJsonPath = Path.Combine(SolutionRootLocator.Find(), "rules.json");
        var globalConfig = ConfigLoader.TryLoadConfig(rulesJsonPath, isRequired: true);
        Assert.NotNull(globalConfig);

        var resolved = ProjectConfigResolver.ResolveForProject(projectName, globalConfig!);

        Assert.False(resolved.Global.EnforceSealedClasses);
        Assert.Equal(100, resolved.Metrics.MaxMethodLineCount);
    }
}
