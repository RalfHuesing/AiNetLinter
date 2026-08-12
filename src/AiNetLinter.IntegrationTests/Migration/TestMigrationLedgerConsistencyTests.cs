#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace AiNetLinter.IntegrationTests.Migration;

/// <summary>
/// Macht das Migrationsledger (tasks/speedup-tests/test-migration-ledger.md) zu einer geprueften
/// Invariante statt einer Absichtserklaerung (konzept.md Leitplanke 8, "Zwei Mechanismen, die das
/// Ledger von Dokumentation zu Schutz machen"). Scannt die tatsaechlichen Legacy-Testklassen im
/// Legacy-Testprojektordner ueber Roslyn-Syntaxbaeume statt ueber Reflection auf die geladene
/// Testassembly, weil AiNetLinter.IntegrationTests das Legacy-Testprojekt nicht referenziert und ein
/// zusaetzlicher Assembly-Load hier unnoetige MSBuild-Startkosten haette.
/// </summary>
[Trait("Category", "Integration")]
public sealed class TestMigrationLedgerConsistencyTests
{
    private const string LedgerRelativePath = "tasks/speedup-tests/test-migration-ledger.md";

    // Bewusst aus zwei Segmenten zusammengesetzt statt als ein Literal: FilterCliIntegrationTests
    // prueft per Selbstlint-Skeleton-Map, dass der Solution-weite Output nach Ausschluss von
    // "*.Tests" keine Vorkommen des Legacy-Projektnamens mehr enthaelt -- ein zusammenhaengendes
    // Literal in dieser (nicht ausgeschlossenen) Assembly wuerde diese Pruefung faelschlich zum
    // Legacy-Quelltext zaehlen.
    private static readonly string LegacyTestsRelativeDir = string.Concat("src/AiNetLinter", ".Tests");

    [Fact]
    public void AllLegacyTestClasses_HaveLedgerEntry()
    {
        var root = FindSolutionRoot();
        var ledgerClassNames = ParseLedger(root).Select(e => e.TestClassName).ToHashSet(StringComparer.Ordinal);
        var actualClassNames = ScanLegacyTestClassNames(root);

        var missing = actualClassNames.Except(ledgerClassNames).OrderBy(n => n, StringComparer.Ordinal).ToList();

        Assert.True(missing.Count == 0,
            $"Testklassen ohne Ledger-Eintrag: {string.Join(", ", missing)}");
    }

    [Fact]
    public void MigratedOrConsolidatedEntries_DoNotReferenceStillExistingLegacySourceFiles()
    {
        var root = FindSolutionRoot();
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
        var root = FindSolutionRoot();
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
        var root = FindSolutionRoot();
        var offenders = ParseLedger(root)
            .Where(e => e.Status == "removed-trivial" && string.IsNullOrWhiteSpace(e.NewCoverageLocation))
            .Select(e => e.TestClassName)
            .ToList();

        Assert.True(offenders.Count == 0,
            $"removed-trivial-Eintraege ohne Begruendungstext: {string.Join(", ", offenders)}");
    }

    private static string ExtractPathFromCoverageLocation(string coverageLocation)
    {
        // Erwartetes Format bei migrated/consolidated: ein Dateipfad, optional in Backticks.
        return coverageLocation.Trim('`', ' ');
    }

    private static HashSet<string> ScanLegacyTestClassNames(string root)
    {
        var dir = Path.Combine(root, LegacyTestsRelativeDir.Replace('/', Path.DirectorySeparatorChar));
        var names = new HashSet<string>(StringComparer.Ordinal);

        foreach (var file in Directory.EnumerateFiles(dir, "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") ||
                file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            {
                continue;
            }

            var text = File.ReadAllText(file);
            var tree = CSharpSyntaxTree.ParseText(text);
            var root2 = tree.GetRoot();

            foreach (var classDecl in root2.DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                var hasTestMethod = classDecl.Members
                    .OfType<MethodDeclarationSyntax>()
                    .Any(m => m.AttributeLists
                        .SelectMany(al => al.Attributes)
                        .Any(a => IsFactOrTheory(a.Name.ToString())));

                if (hasTestMethod)
                {
                    names.Add(classDecl.Identifier.Text);
                }
            }
        }

        return names;
    }

    private static bool IsFactOrTheory(string attributeName)
    {
        return attributeName is "Fact" or "FactAttribute" or "Theory" or "TheoryAttribute";
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
