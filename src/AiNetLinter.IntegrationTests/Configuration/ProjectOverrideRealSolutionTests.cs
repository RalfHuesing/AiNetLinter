#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AiNetLinter.Configuration;
using AiNetLinter.Core;
using AiNetLinter.IntegrationTests.Platform;
using Xunit;

namespace AiNetLinter.IntegrationTests.Configuration;

/// <summary>
/// Laedt die echte AiNetLinter.slnx einmal ueber SourceFileCatalog und belegt fuer die drei neuen
/// Testziel-Projekte sowohl den ProjectOverride- als auch den TestProjectDetector-Pfad mit den
/// tatsaechlichen Metadatenreferenzen der geladenen Solution -- Component-Tests mit synthetischem
/// AdhocWorkspace-Projekt beweisen nur den Namens-Fallback, nicht die Fidelity gegen die echte
/// MSBuild-Welt.
/// </summary>
[Trait("Category", "Integration")]
public sealed class ProjectOverrideRealSolutionTests
{
    [Theory]
    [InlineData("AiNetLinter.FastTests")]
    [InlineData("AiNetLinter.IntegrationTests")]
    [InlineData("AiNetLinter.TestKit")]
    public async Task RealSolutionProject_NewTestProjectNames_ResolvesOverrideAndIsDetectedAsTest(string projectName)
    {
        var rootDir = FindSolutionRoot();
        var rulesJsonPath = Path.Combine(rootDir, "rules.json");
        var globalConfig = ConfigLoader.TryLoadConfig(rulesJsonPath, isRequired: true);
        Assert.NotNull(globalConfig);

        using var catalog = await LoadedFixture.LoadCatalogAsync(rootDir);
        var project = catalog.Solution.Projects.SingleOrDefault(p => p.Name == projectName);
        Assert.NotNull(project);

        var resolved = ProjectConfigResolver.ResolveForProject(projectName, globalConfig!);
        Assert.False(resolved.Global.EnforceSealedClasses);
        Assert.Equal(100, resolved.Metrics.MaxMethodLineCount);

        var isTestProject = TestProjectDetector.IsTestProject(project!, globalConfig!.TestSentinel.TestProjectNameSuffixes);
        Assert.True(isTestProject);
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
