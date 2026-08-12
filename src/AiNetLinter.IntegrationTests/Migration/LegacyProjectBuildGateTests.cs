#nullable enable

using System;
using System.IO;
using System.Linq;
using Xunit;

namespace AiNetLinter.IntegrationTests.Migration;

/// <summary>
/// Konzept.md Leitplanke 8 "Legacy-Build-Gate": ein nicht mehr baubares Legacy-Projekt ist ein
/// Sicherheitsnetz, das nur noch im Dokument existiert. Liest <c>AiNetLinter.slnx</c> per
/// XML-Parsing (dasselbe Grundprinzip wie
/// <see cref="AiNetLinter.IntegrationTests.Migration.TestMigrationLedgerConsistencyTests"/>: Text-
/// bzw. Struktur-Scan statt Reflection auf eine geladene Assembly) und prueft mechanisch, dass
/// <c>AiNetLinter.Tests</c> weiterhin Teil der Solution und seine <c>.csproj</c>-Datei auf der
/// Platte vorhanden ist. Der eigentliche "bleibt kompilierbar"-Nachweis kommt weiterhin vom
/// ohnehin bei jedem Step laufenden <c>dotnet build AiNetLinter.slnx</c> -- dieser Guard sichert
/// nur die dafuer noetige Solution-Mitgliedschaft mechanisch ab.
/// </summary>
[Trait("Category", "Integration")]
public sealed class LegacyProjectBuildGateTests
{
    // Bewusst aus zwei Segmenten zusammengesetzt statt als ein Literal: FilterCliIntegrationTests
    // prueft per Selbstlint-Skeleton-Map, dass der Solution-weite Output nach Ausschluss von
    // "*.Tests" keine Vorkommen des Legacy-Projektnamens mehr enthaelt -- ein zusammenhaengendes
    // Literal in dieser (nicht ausgeschlossenen) Assembly wuerde diese Pruefung faelschlich zum
    // Legacy-Quelltext zaehlen.
    private static readonly string LegacyProjectName = string.Concat("AiNetLinter", ".Tests");

    [Fact]
    public void AiNetLinterSlnx_StillReferencesLegacyProject_AsLongAsLedgerHasPendingEntries()
    {
        var root = FindSolutionRoot();
        var pendingCount = CountPendingLedgerEntries(root);

        // Migriert der naechste Step die letzte Kohorte (pending = 0), darf das Legacy-Projekt aus
        // der Solution verschwinden -- dieser Guard greift bewusst nur, solange noch etwas zu
        // sichern ist (siehe konzept.md Leitplanke 8).
        if (pendingCount == 0)
        {
            return;
        }

        var slnxPath = Path.Combine(root, "AiNetLinter.slnx");
        var slnxContent = File.ReadAllText(slnxPath);

        Assert.Contains($"src/{LegacyProjectName}/{LegacyProjectName}.csproj", slnxContent, StringComparison.Ordinal);

        var csprojPath = Path.Combine(root, "src", LegacyProjectName, $"{LegacyProjectName}.csproj");
        Assert.True(File.Exists(csprojPath), $"Legacy-Projektdatei fehlt auf der Platte: {csprojPath}");
    }

    private static int CountPendingLedgerEntries(string root)
    {
        var ledgerPath = Path.Combine(root, "tasks", "speedup-tests", "test-migration-ledger.md");
        var lines = File.ReadAllLines(ledgerPath);

        return lines.Count(line =>
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith("| `", StringComparison.Ordinal))
            {
                return false;
            }

            var cells = trimmed.Trim('|').Split('|').Select(c => c.Trim()).ToArray();
            return cells.Length == 6 && cells[3] == "pending";
        });
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
