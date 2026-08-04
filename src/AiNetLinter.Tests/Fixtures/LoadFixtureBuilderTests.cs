#nullable enable

using System.IO;
using System.Linq;
using AiNetLinter.Tests.Fixtures;
using Xunit;

namespace AiNetLinter.Tests.Fixtures;

/// <summary>
/// Stellt sicher, dass <see cref="LoadFixtureBuilder"/> die erwarteten Verzeichnis- und
/// Datei-Strukturen erzeugt: Solution-Datei, ein <c>.csproj</c> pro Projekt und die
/// spezifizierte Anzahl an <c>.cs</c>-Dateien mit konfigurierter Zeilenanzahl pro Datei.
/// </summary>
[Trait("Category", "Unit")]
public sealed class LoadFixtureBuilderTests
{
    [Fact]
    public void Build_MiniSolution_CreatesExpectedStructure()
    {
        using var handle = LoadFixtureBuilder.Build("mini", projectCount: 2, filesPerProject: 3, linesPerFile: 10);

        Assert.True(File.Exists(handle.SolutionPath), $"Solution-Datei fehlt: {handle.SolutionPath}");

        var expectedFileCount = 2 * 3;
        var actualFiles = Directory.EnumerateFiles(handle.RootPath, "*.cs", SearchOption.AllDirectories).ToList();
        Assert.Equal(expectedFileCount, actualFiles.Count);

        // Inhaltspruefung: jede Datei muss mindestens die spezifizierte Zeilenanzahl erreichen
        // (Header + Klassenoeffnung/-schluss + Methoden-Body).
        foreach (var file in actualFiles)
        {
            var lines = File.ReadAllLines(file);
            Assert.True(
                lines.Length >= 10,
                $"Datei {file} hat nur {lines.Length} Zeilen, erwartet >= 10.");
        }
    }
}
