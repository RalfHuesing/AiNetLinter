#nullable enable

using System;
using System.IO;
using AiNetLinter.Configuration;
using Xunit;

namespace AiNetLinter.FastTests.Configuration;

/// <summary>
/// Belegt, dass die drei neuen Testziel-Projektnamen ueber den produktiven ProjectOverrides-Vertrag
/// in rules.json denselben Test-Override wie das Legacy-Projekt AiNetLinter.Tests erhalten.
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
        var rulesJsonPath = Path.Combine(FindSolutionRoot(), "rules.json");
        var globalConfig = ConfigLoader.TryLoadConfig(rulesJsonPath, isRequired: true);
        Assert.NotNull(globalConfig);

        var resolved = ProjectConfigResolver.ResolveForProject(projectName, globalConfig!);

        Assert.False(resolved.Global.EnforceSealedClasses);
        Assert.Equal(100, resolved.Metrics.MaxMethodLineCount);
    }

    private static string FindSolutionRoot()
    {
        var currentDir = new DirectoryInfo(AppContext.BaseDirectory);
        while (currentDir != null)
        {
            if (File.Exists(Path.Combine(currentDir.FullName, "AiNetLinter.slnx")))
            {
                return currentDir.FullName;
            }

            currentDir = currentDir.Parent;
        }

        throw new DirectoryNotFoundException("Das Root-Verzeichnis mit der Projektmappe 'AiNetLinter.slnx' wurde nicht gefunden.");
    }
}
