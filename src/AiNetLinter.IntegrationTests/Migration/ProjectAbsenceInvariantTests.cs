#nullable enable

using System;
using System.IO;
using AiNetLinter.TestKit;
using Xunit;

namespace AiNetLinter.IntegrationTests.Migration;

/// <summary>
/// Dauerhafte Architektur-Invariante: das zusätzliche Testprojekt
/// <c>AiNetLinter.Tests</c> darf weder in der Solution noch im Dateisystem
/// vorhanden sein. Wenn diese Annahme bricht, ist die Projektstruktur-Invariante
/// (StaticTestSentinel, IVT, rules.json-ProjectOverrides) verletzt.
/// </summary>
[Trait("Category", "Integration")]
public sealed class ProjectAbsenceInvariantTests
{
    private static readonly string ProjectName = string.Concat("AiNetLinter", ".Tests");
    private static readonly string TestsRelativeDir = string.Concat("src/AiNetLinter", ".Tests");

    [Fact]
    public void Project_IsNotInSolutionAndNotOnDisk()
    {
        var root = SolutionRootLocator.Find();
        var slnxPath = Path.Combine(root, "AiNetLinter.slnx");
        var slnxContent = File.ReadAllText(slnxPath);

        Assert.DoesNotContain(ProjectName, slnxContent, StringComparison.Ordinal);

        var testsDir = Path.Combine(root, TestsRelativeDir.Replace('/', Path.DirectorySeparatorChar));
        Assert.False(Directory.Exists(testsDir), $"Das zusätzliche Testverzeichnis existiert auf der Platte: {testsDir}");
    }
}
