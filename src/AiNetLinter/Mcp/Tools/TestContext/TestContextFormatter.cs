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

        sb.AppendLine($"# Test-Coverage-Kontext: {payload.TargetSymbol} ({payload.TargetKind})");
        sb.AppendLine();
        sb.AppendLine($"- **Zieldatei:** `{payload.TargetFilePath}`");

        if (payload.IsUntested)
        {
            sb.AppendLine();
            sb.AppendLine("> [!NOTE]");
            sb.AppendLine("> Für dieses Symbol wurden keine direkten Tests gefunden (weder per Namenskonvention, typeof/nameof, @covers-Kommentar noch Methoden-Aufruf).");
            var suggestedPath = payload.SuggestedTestFilePath ?? $"{payload.TargetSymbol}Tests.cs";
            sb.AppendLine($"> **Empfehlung:** Neue Unit-Tests unter `{suggestedPath}` anlegen.");
        }
        else
        {
            sb.AppendLine($"- **Gefundene Tests:** {payload.TotalMatchingTests} Testmethode(n) in {payload.TotalTestFiles} Testdatei(en)");
            sb.AppendLine();

            sb.AppendLine("### Zugeordnete Testdateien");
            foreach (var file in payload.TestFiles)
            {
                sb.AppendLine($"- `{file.FilePath}` ({file.Category}, {file.TestMethods.Count} Tests — {file.MatchReason})");
                foreach (var method in file.TestMethods)
                {
                    sb.AppendLine($"  - `{method}()`");
                }
            }

            if (payload.IsTruncated)
            {
                sb.AppendLine($"- *(Zeige {payload.TestFiles.Count} von {payload.TotalTestFiles} Testdateien — maxResults erhoehen fuer alle)*");
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

        sb.AppendLine();
        sb.AppendLine("[HINWEIS]: Diese Daten sind vollstaendig fuer den angefragten Scope — kein zusaetzliches Read/Grep noetig.");

        return sb.ToString().TrimEnd();
    }
}
