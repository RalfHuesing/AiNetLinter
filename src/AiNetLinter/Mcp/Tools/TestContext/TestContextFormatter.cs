#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace AiNetLinter.Mcp.Tools.TestContext;

/// <summary>
/// Formatiert das Ergebnis von <see cref="GetTestContextTool"/> als lesbaren Markdown-Report.
/// </summary>
internal static class TestContextFormatter
{
    public static string FormatReport(TestContextPayload payload)
    {
        var sb = new StringBuilder();
        AppendHeader(sb, payload);
        AppendTestAssociations(sb, payload);
        AppendTruncationAndNextStep(sb, payload);
        AppendCompletenessNotice(sb, payload.Completeness);
        return sb.ToString().TrimEnd();
    }

    private static void AppendHeader(StringBuilder sb, TestContextPayload payload)
    {
        sb.AppendLine($"# Test-Kontext (statische Testkandidaten): {payload.TargetSymbol} ({payload.TargetKind})");
        sb.AppendLine();
        sb.AppendLine($"- **Zieldatei:** `{payload.TargetFilePath}`");
        sb.AppendLine($"- **Status:** `{payload.Completeness}`");
        sb.AppendLine($"- **Evidenzgrenze:** `{payload.EvidenceBoundary}`");
        sb.AppendLine("- **statische Testzuordnung:** Kandidaten aus statischen Heuristiken, kein Ausführungsnachweis.");
        var countLabel = payload.TestFiles.Count > 0 && payload.TestFiles.All(file => file.TestMethods.Count > 0)
            ? "Testmethoden"
            : "Evidenztreffer";
        sb.AppendLine($"- **Counts:** {payload.ReturnedTestFiles} von {payload.TotalTestFiles} Testdateien und {payload.ReturnedTestMethods} von {payload.TotalMatchingTests} {countLabel} zurückgegeben.");
    }

    private static void AppendTestAssociations(StringBuilder sb, TestContextPayload payload)
    {
        if (payload.IsUntested)
        {
            sb.AppendLine();
            sb.AppendLine("> [!NOTE]");
            sb.AppendLine("> In der statischen Test-Zuordnung wurden für dieses Symbol keine direkten Tests gefunden; es gibt keine statischen Testkandidaten (weder per Namenskonvention, typeof/nameof, @covers-Kommentar noch Methoden-Aufruf).");
            sb.AppendLine($"> **Empfehlung:** Neue Unit-Tests unter `{payload.SuggestedTestFilePath ?? $"{payload.TargetSymbol}Tests.cs"}` anlegen.");
            return;
        }

        var candidateLabel = payload.TestFiles.Count > 0 && payload.TestFiles.All(file => file.TestMethods.Count > 0)
            ? "Testmethode(n)"
            : "Evidenztreffer";
        sb.AppendLine($"- **Statische Kandidaten:** {payload.TotalMatchingTests} {candidateLabel} in {payload.TotalTestFiles} Testdatei(en)");
        sb.AppendLine();
        sb.AppendLine("### Zugeordnete Testdateien");
        foreach (var file in payload.TestFiles) AppendTestFile(sb, file);
        AppendRecommendedCommands(sb, payload.RecommendedTestCommands);
    }

    private static void AppendTestFile(StringBuilder sb, StaticTestCandidateFile file)
    {
        var evidence = $"{file.EvidenceKind}, confidence={file.Confidence}";
        var candidateDescription = file.TestMethods.Count > 0
            ? $"{file.TestMethods.Count} konkrete Methoden"
            : $"{file.TotalTestCount} Tests auf Klassenebene; keine Methode behauptet";
        sb.AppendLine($"- `{file.FilePath}` ({file.Category}, {candidateDescription} — {evidence}; {file.MatchReason}; scope={file.ScopeType}; sourceKind={file.SourceKind})");
        foreach (var method in file.TestMethods) sb.AppendLine($"  - `{method}()`");
    }

    private static void AppendRecommendedCommands(StringBuilder sb, IReadOnlyList<string> commands)
    {
        if (commands.Count == 0) return;
        sb.AppendLine();
        sb.AppendLine("### Empfohlene Test-Befehle");
        sb.AppendLine("```powershell");
        foreach (var command in commands) sb.AppendLine(command);
        sb.AppendLine("```");
    }

    private static void AppendTruncationAndNextStep(StringBuilder sb, TestContextPayload payload)
    {
        if (payload.IsTruncated || payload.Completeness == "truncated")
            sb.AppendLine($"- **TruncatedBy:** `{string.Join(", ", payload.TruncatedBy ?? [])}`");
        if (!string.IsNullOrWhiteSpace(payload.NextStep))
            sb.AppendLine($"- **Nächster sicherer Schritt:** {payload.NextStep}");
    }

    private static void AppendCompletenessNotice(StringBuilder sb, string completeness)
    {
        sb.AppendLine();
        sb.AppendLine(completeness switch
        {
            "complete" => "[HINWEIS]: Der statische Zuordnungsscope ist vollständig geprüft; die Daten enthalten keine Aussage zur Laufzeit und keinen Ausführungsnachweis.",
            "empty" => "[HINWEIS]: Im geprüften statischen Zuordnungsscope wurden keine Testkandidaten gefunden; die Daten enthalten keine Aussage zur Laufzeit und keinen Ausführungsnachweis.",
            "truncated" => "[HINWEIS]: Das Ergebnis wurde begrenzt; die Daten enthalten keine Aussage zur Laufzeit und keinen Ausführungsnachweis.",
            _ => "[HINWEIS]: Das Ergebnis ist für den statischen Zuordnungsscope nicht vollständig; die Daten enthalten keine Aussage zur Laufzeit und keinen Ausführungsnachweis.",
        });
    }
}
