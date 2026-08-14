#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AiNetLinter.TestKit;
using Xunit;

namespace AiNetLinter.IntegrationTests.Migration;

/// <summary>
/// Finaler Migrationsabschluss-Guard: Sichert ab, dass 0 Ledger-Zeilen 'pending' sind,
/// alle Zielorte auf der Platte existieren und das Legacy-Projekt weder in der Solution
/// noch auf der Platte vorhanden ist.
/// </summary>
[Trait("Category", "Integration")]
public sealed class MigrationCompletionGuardTests
{
    private const string LedgerRelativePath = "tasks/speedup-tests/test-migration-ledger.md";
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

    [Fact]
    public void Ledger_HasZeroPendingEntries()
    {
        var root = SolutionRootLocator.Find();
        var pending = ParseLedger(root).Where(e => e.Status == "pending").ToList();

        Assert.Empty(pending);
    }

    [Fact]
    public void MigratedOrConsolidatedEntries_DoNotReferenceStillExistingLegacySourceFiles()
    {
        var root = SolutionRootLocator.Find();
        var offenders = new List<string>();

        foreach (var entry in ParseLedger(root).Where(e => e.Status is "migrated" or "consolidated"))
        {
            foreach (var sourceFile in entry.SourceFiles)
            {
                if (File.Exists(Path.Combine(root, sourceFile)))
                {
                    offenders.Add($"{entry.TestClassName} ({sourceFile})");
                }
            }
        }

        Assert.True(offenders.Count == 0,
            $"migrated/consolidated-Ledger-Eintraege mit noch existierender Legacy-Quelldatei: {string.Join(", ", offenders)}");
    }

    [Fact]
    public void MigratedOrConsolidatedEntries_HaveExistingNewCoverageLocation()
    {
        var root = SolutionRootLocator.Find();
        var offenders = new List<string>();

        foreach (var entry in ParseLedger(root).Where(e => e.Status is "migrated" or "consolidated"))
        {
            if (string.IsNullOrWhiteSpace(entry.NewCoverageLocation) ||
                !File.Exists(Path.Combine(root, ExtractPathFromCoverageLocation(entry.NewCoverageLocation))))
            {
                offenders.Add(entry.TestClassName);
            }
        }

        Assert.True(offenders.Count == 0,
            $"migrated/consolidated-Eintraege ohne existierenden neuen Abdeckungsort: {string.Join(", ", offenders)}");
    }

    [Fact]
    public void RemovedTrivialEntries_HaveNonEmptyJustification()
    {
        var root = SolutionRootLocator.Find();
        var offenders = ParseLedger(root)
            .Where(e => e.Status == "removed-trivial" && string.IsNullOrWhiteSpace(e.NewCoverageLocation))
            .Select(e => e.TestClassName)
            .ToList();

        Assert.True(offenders.Count == 0,
            $"removed-trivial-Eintraege ohne Begruendungstext: {string.Join(", ", offenders)}");
    }

    private static string ExtractPathFromCoverageLocation(string coverageLocation)
    {
        var trimmed = coverageLocation.Trim();
        if (trimmed.StartsWith('`'))
        {
            var closingBacktick = trimmed.IndexOf('`', 1);
            if (closingBacktick > 1) return trimmed[1..closingBacktick];
        }

        return trimmed.Split(" — ", StringSplitOptions.None)[0].Trim();
    }

    private static List<LedgerEntry> ParseLedger(string root)
    {
        var path = Path.Combine(root, LedgerRelativePath.Replace('/', Path.DirectorySeparatorChar));
        var entries = new List<LedgerEntry>();

        foreach (var line in File.ReadAllLines(path))
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith("| `", StringComparison.Ordinal))
            {
                continue;
            }

            var cells = trimmed.Trim('|').Split('|').Select(c => c.Trim()).ToArray();
            if (cells.Length != 6)
            {
                continue;
            }

            var sourceFiles = cells[0]
                .Split(',')
                .Select(s => s.Trim().Trim('`'))
                .Where(s => s.Length > 0)
                .ToArray();

            entries.Add(new LedgerEntry(
                sourceFiles,
                cells[1].Trim('`'),
                cells[2],
                cells[3],
                cells[4].Trim('`'),
                cells[5]));
        }

        return entries;
    }

    private sealed record LedgerEntry(
        string[] SourceFiles,
        string TestClassName,
        string ProductArea,
        string Status,
        string LegacyFilter,
        string NewCoverageLocation);
}
