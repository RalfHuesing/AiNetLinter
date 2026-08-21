#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using AiNetLinter.Baseline;
using AiNetLinter.TestKit;
using Xunit;

namespace AiNetLinter.FastTests.Baseline;

/// <summary>
/// Component-Tests fuer den gemeinsamen gefilterten Verzeichnis-Walk
/// (<see cref="FileSystemExclusionHelpers.WalkFilteredTree"/>) des Staleness-Subsystems:
/// Namens-Ausschluesse, Root-Deduplizierung, Fehlerzaehler statt Abbruch (Konzept 02) und
/// die deterministisch testbare Reparse-Point-Entscheidung (Konzept 02, B).
/// </summary>
[Trait("Category", "Component")]
public sealed class StalenessTreeWalkerTests
{
    [Fact]
    public void Walk_ExcludedDirectoryNames_AreNotTraversed()
    {
        using var tempDir = TestTempDirectory.Create("staleness-walk-");
        var root = tempDir.DirectoryPath;

        var sourceDir = Path.Combine(root, "src", "Project");
        Directory.CreateDirectory(sourceDir);
        var excludedDir = Path.Combine(root, ".git", "objects", "ab");
        Directory.CreateDirectory(excludedDir);

        // Zeitstempel so setzen, dass ein Fehler beim Ausschluss sichtbar waere:
        // der frischeste Knoten liegt bewusst im ausgeschlossenen Teilbaum.
        var anchor = DateTime.UtcNow.AddHours(-1);
        Directory.SetLastWriteTimeUtc(sourceDir, anchor);
        Directory.SetLastWriteTimeUtc(excludedDir, DateTime.UtcNow.AddHours(1));

        var visited = new List<string>();
        var stats = FileSystemExclusionHelpers.WalkFilteredTree(
            [root],
            filePattern: null,
            visitDirectory: visited.Add,
            visitFile: null);

        Assert.DoesNotContain(visited, d => d.Contains(".git", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(Path.Combine(root, "src"), visited);
        Assert.Contains(sourceDir, visited);
        Assert.Empty(stats.Warnings);
    }

    [Fact]
    public void Walk_NestedRoots_FileIsVisitedExactlyOnce()
    {
        using var tempDir = TestTempDirectory.Create("staleness-walk-nested-");
        var root = tempDir.DirectoryPath;
        var nested = Path.Combine(root, "src");
        Directory.CreateDirectory(nested);
        var file = Path.Combine(nested, "Only.cs");
        File.WriteAllText(file, "namespace X;");

        var fileVisits = new List<string>();
        var stats = FileSystemExclusionHelpers.WalkFilteredTree(
            [root, nested],
            filePattern: "*.cs",
            visitDirectory: null,
            visitFile: fileVisits.Add);

        var single = Assert.Single(fileVisits);
        Assert.Equal(file, single);
        Assert.Empty(stats.Warnings);
    }

    [Fact]
    public void Walk_MissingRoot_YieldsWarningAndContinuesWithRemainingRoots()
    {
        using var tempDir = TestTempDirectory.Create("staleness-walk-err-");
        var validDir = Path.Combine(tempDir.DirectoryPath, "valid");
        Directory.CreateDirectory(validDir);
        var validFile = Path.Combine(validDir, "Present.cs");
        File.WriteAllText(validFile, "namespace Y;");

        var missingRoot = Path.Combine(tempDir.DirectoryPath, "does-not-exist");
        var fileVisits = new List<string>();
        var stats = FileSystemExclusionHelpers.WalkFilteredTree(
            [missingRoot, validDir],
            filePattern: "*.cs",
            visitDirectory: null,
            visitFile: fileVisits.Add);

        // Konzept 02 (C): Ein unzugänglicher Teilbaum erzeugt genau eine Warnung und
        // bricht den Gesamtwalk nicht ab — die uebrigen Wurzeln werden normal traversiert.
        Assert.Equal(1, stats.InaccessibleSubtreeCount);
        Assert.Contains(missingRoot, stats.Warnings[0], StringComparison.Ordinal);
        Assert.Contains(validFile, fileVisits);
    }

    [Fact]
    public void IsTraversableSubDirectory_ReparsePointsAreNeverTraversed()
    {
        // Konzept 02 (B): Junction-/Symlink-Zyklen duerfen den Walk nicht aufblaehen oder
        // endlos laufen lassen. Die Traversierungs-Entscheidung ist als Pure Function
        // extrahiert und damit ohne echte Junctions (Rechte-/Plattform-abhaengig)
        // deterministisch pruefbar.
        Assert.False(FileSystemExclusionHelpers.IsTraversableSubDirectory(
            FileAttributes.Directory | FileAttributes.ReparsePoint));
        Assert.True(FileSystemExclusionHelpers.IsTraversableSubDirectory(
            FileAttributes.Directory));
        Assert.True(FileSystemExclusionHelpers.IsTraversableSubDirectory(
            FileAttributes.Directory | FileAttributes.Hidden));
    }
}
