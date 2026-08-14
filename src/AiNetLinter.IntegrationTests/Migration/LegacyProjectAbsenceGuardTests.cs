#nullable enable

using System;
using System.IO;
using AiNetLinter.TestKit;
using Xunit;

namespace AiNetLinter.IntegrationTests.Migration;

/// <summary>
/// Dauerhafte Architektur-Invariante: das quarantinierte Legacy-Testprojekt
/// <c>AiNetLinter.Tests</c> darf weder in der Solution noch im Dateisystem
/// wieder auftauchen. Wenn diese Annahme bricht, ist die Migrations-Sicherheitshuelle
/// (StaticTestSentinel, IVT, rules.json-ProjectOverrides) kompromittiert.
/// </summary>
[Trait("Category", "Integration")]
public sealed class LegacyProjectAbsenceGuardTests
{
    private static readonly string LegacyProjectName = string.Concat("AiNetLinter", ".Tests");
    private static readonly string LegacyTestsRelativeDir = string.Concat("src/AiNetLinter", ".Tests");

    [Fact]
    public void LegacyProject_IsNotInSolutionAndNotOnDisk()
    {
        var root = SolutionRootLocator.Find();
        var slnxPath = Path.Combine(root, "AiNetLinter.slnx");
        var slnxContent = File.ReadAllText(slnxPath);

        Assert.DoesNotContain(LegacyProjectName, slnxContent, StringComparison.Ordinal);

        var legacyDir = Path.Combine(root, LegacyTestsRelativeDir.Replace('/', Path.DirectorySeparatorChar));
        Assert.False(Directory.Exists(legacyDir), $"Legacy-Verzeichnis existiert noch auf der Platte: {legacyDir}");
    }
}
