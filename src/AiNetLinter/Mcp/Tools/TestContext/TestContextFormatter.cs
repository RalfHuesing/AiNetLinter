#nullable enable

using System;
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

        sb.AppendLine($"# Test-Kontext (statische Testkandidaten): {payload.TargetSymbol} ({payload.TargetKind})");
        sb.AppendLine();
        sb.AppendLine($"- **Zieldatei:** `{payload.TargetFilePath}`");
        sb.AppendLine($"- **Status:** `{payload.Completeness}`");
        sb.AppendLine($"- **Evidenzgrenze:** `{payload.EvidenceBoundary}`");
        sb.AppendLine("- **statische Testzuordnung:** Kandidaten aus statischen Heuristiken, kein Ausführungsnachweis.");
        sb.AppendLine($"- **Counts:** {payload.ReturnedTestFiles} von {payload.TotalTestFiles} Testdateien und {payload.ReturnedTestMethods} von {payload.TotalMatchingTests} Testmethoden zurückgegeben.");

        if (payload.IsUntested)
        {
            sb.AppendLine();
            sb.AppendLine("> [!NOTE]");
            sb.AppendLine("> In der statischen Test-Zuordnung wurden für dieses Symbol keine direkten Tests gefunden; es gibt keine statischen Testkandidaten (weder per Namenskonvention, typeof/nameof, @covers-Kommentar noch Methoden-Aufruf).");
            var suggestedPath = payload.SuggestedTestFilePath ?? $"{payload.TargetSymbol}Tests.cs";
            sb.AppendLine($"> **Empfehlung:** Neue Unit-Tests unter `{suggestedPath}` anlegen.");
        }
        else
        {
            sb.AppendLine($"- **Statische Kandidaten:** {payload.TotalMatchingTests} Testmethode(n) in {payload.TotalTestFiles} Testdatei(en)");
            sb.AppendLine();

            sb.AppendLine("### Zugeordnete Testdateien");
            foreach (var file in payload.TestFiles)
            {
                sb.AppendLine($"- `{file.FilePath}` ({file.Category}, {file.TestMethods.Count} statische Kandidaten — {file.MatchReason})");
                foreach (var method in file.TestMethods)
                {
                    sb.AppendLine($"  - `{method}()`");
                }
            }

            if (payload.RecommendedTestCommands.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("### Empfohlene Test-Befehle");
                sb.AppendLine("```powershell");
                foreach (var cmd in payload.RecommendedTestCommands)
                {
                    sb.AppendLine(cmd);
                }
                sb.AppendLine("```");
            }
        }

        if (payload.IsTruncated || payload.Completeness == "truncated")
        {
            sb.AppendLine($"- **TruncatedBy:** `{string.Join(", ", payload.TruncatedBy ?? [])}`");
        }
        if (!string.IsNullOrWhiteSpace(payload.NextStep))
        {
            sb.AppendLine($"- **Nächster sicherer Schritt:** {payload.NextStep}");
        }

        sb.AppendLine();
        sb.AppendLine(payload.Completeness switch
        {
            "complete" => "[HINWEIS]: Der statische Zuordnungsscope ist vollständig geprüft; die Daten enthalten keine Aussage zur Laufzeit und keinen Ausführungsnachweis.",
            "empty" => "[HINWEIS]: Im geprüften statischen Zuordnungsscope wurden keine Testkandidaten gefunden; die Daten enthalten keine Aussage zur Laufzeit und keinen Ausführungsnachweis.",
            "truncated" => "[HINWEIS]: Das Ergebnis wurde begrenzt; die Daten enthalten keine Aussage zur Laufzeit und keinen Ausführungsnachweis.",
            _ => "[HINWEIS]: Das Ergebnis ist für den statischen Zuordnungsscope nicht vollständig; die Daten enthalten keine Aussage zur Laufzeit und keinen Ausführungsnachweis.",
        });

        return sb.ToString().TrimEnd();
    }
}
