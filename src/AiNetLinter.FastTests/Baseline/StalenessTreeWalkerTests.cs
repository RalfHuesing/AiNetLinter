#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
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
            FileSystemWalkOptions.ForFileTree(null, CancellationToken.None),
            visitDirectory: visited.Add,
            visitFile: null);

        Assert.DoesNotContain(visited, d => d.Contains(".git", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(Path.Combine(root, "src"), visited);
        Assert.Contains(sourceDir, visited);
        Assert.Equal(1, stats.SkippedExcludedDirectoryCount);
        Assert.Empty(stats.Warnings);
    }

    [Fact]
    public void Walk_MaxDepth_VisitsFilesAtLimitButNotDeeperDirectories()
    {
        using var tempDir = TestTempDirectory.Create("staleness-walk-depth-");
        var rootFile = tempDir.CreateFile("Root.cs");
        var levelOneFile = tempDir.CreateFile(Path.Combine("one", "LevelOne.cs"));
        var levelTwoFile = tempDir.CreateFile(Path.Combine("one", "two", "LevelTwo.cs"));
        var visitedFiles = new List<string>();

        var stats = FileSystemExclusionHelpers.WalkFilteredTree(
            [tempDir.DirectoryPath],
            FileSystemWalkOptions.ForFileTree(1, CancellationToken.None),
            visitDirectory: null,
            visitFile: visitedFiles.Add);

        Assert.Contains(rootFile, visitedFiles);
        Assert.Contains(levelOneFile, visitedFiles);
        Assert.DoesNotContain(levelTwoFile, visitedFiles);
        Assert.False(stats.CancellationRequested);
        Assert.Empty(stats.Warnings);
    }

    [Fact]
    public void Walk_Cancellation_ReturnsPartialStatsBeforeFurtherCallbacks()
    {
        using var tempDir = TestTempDirectory.Create("staleness-walk-cancel-");
        tempDir.CreateFile(Path.Combine("child", "Nested.cs"));
        using var cancellation = new CancellationTokenSource();
        var visitedDirectories = new List<string>();
        var visitedFiles = new List<string>();

        var stats = FileSystemExclusionHelpers.WalkFilteredTree(
            [tempDir.DirectoryPath],
            FileSystemWalkOptions.ForFileTree(null, cancellation.Token),
            visitDirectory: directory =>
            {
                visitedDirectories.Add(directory);
                cancellation.Cancel();
            },
            visitFile: visitedFiles.Add);

        Assert.Equal([tempDir.DirectoryPath], visitedDirectories);
        Assert.Empty(visitedFiles);
        Assert.True(stats.CancellationRequested);
        Assert.Empty(stats.Warnings);
    }

    [Fact]
    public void FileTreeOptions_NegativeMaxDepth_IsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            FileSystemWalkOptions.ForFileTree(-1, CancellationToken.None));
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
