#nullable enable

using System;
using System.IO;
using AiNetLinter.Configuration;
using AiNetLinter.Core;
using Microsoft.CodeAnalysis;
using Xunit;

namespace AiNetLinter.FastTests.Core;

/// <summary>
/// Belegt, dass TestProjectDetector die drei neuen Testziel-Projektnamen ueber den
/// Namens-Suffix-Fallback aus rules.json (TestSentinel.TestProjectNameSuffixes) als
/// Testprojekte erkennt. Nutzt ein synthetisches AdhocWorkspace-Projekt ohne
/// Testframework-Metadatenreferenz, damit ausschliesslich der Suffix-Pfad greift -- daher
/// Component-Ebene statt Unit (kein Solution-Load von Platte).
/// </summary>
[Trait("Category", "Component")]
public sealed class TestProjectDetectorSuffixTests
{
    [Theory]
    [InlineData("AiNetLinter.FastTests")]
    [InlineData("AiNetLinter.IntegrationTests")]
    [InlineData("AiNetLinter.TestKit")]
    public void IsTestProject_NewTestProjectNames_RecognizedViaNameSuffix(string projectName)
    {
        var rulesJsonPath = Path.Combine(FindSolutionRoot(), "rules.json");
        var globalConfig = ConfigLoader.TryLoadConfig(rulesJsonPath, isRequired: true);
        Assert.NotNull(globalConfig);

        using var workspace = new AdhocWorkspace();
        var solution = workspace.CurrentSolution;
        var projectId = ProjectId.CreateNewId();
        solution = solution.AddProject(projectId, projectName, projectName, LanguageNames.CSharp);
        var project = solution.GetProject(projectId)!;

        var isTestProject = TestProjectDetector.IsTestProject(project, globalConfig!.TestSentinel.TestProjectNameSuffixes);

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
