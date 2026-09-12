#nullable enable

using System.Linq;
using System.Text;
using AiNetLinter.Core.DuplicateDetection;
using AiNetLinter.Output;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Tools.DuplicateDetection;

/// <summary>
/// Text-/JSON-Formatierung fuer den <c>mode="refactoring-drift"</c>-Zweig von
/// <c>find_duplicates</c> — aus <see cref="DuplicateDetectionTool"/> ausgelagert, damit die
/// "Kandidaten statt Verstoesse"-Formulierung getrennt von der Cluster-Formatierung bleibt.
/// </summary>
internal static class RefactoringDriftResponseBuilder
{
    internal static CallToolResult Build(Microsoft.CodeAnalysis.Solution solution, RefactoringDriftScanResultForTool result)
    {
        var solutionDir = System.IO.Path.GetDirectoryName(solution.FilePath) ?? "";
        var body = RenderText(solutionDir, result);
        return McpToolResults.Text(body);
    }

    private static string RenderText(string solutionDir, RefactoringDriftScanResultForTool result)
    {
        if (result.ShownCandidates.Count == 0)
        {
            return $"Keine Refactoring-Drift-Kandidaten fuer Helper '{result.HelperSymbolDisplayName}' gefunden " +
                   $"({result.MethodsScanned} Methoden gescannt). Kein Fund heisst nicht zwingend, dass der " +
                   "Helper ueberall korrekt genutzt wird — nur, dass keine strukturell aehnliche, nicht-aufrufende " +
                   "Methode oberhalb des near-Schwellwerts gefunden wurde.";
        }

        var sb = new StringBuilder();
        sb.Append($"{result.ShownCandidates.Count} von {result.TotalCandidates} Refactoring-Drift-Kandidat(en) fuer " +
                  $"Helper '{result.HelperSymbolDisplayName}' ({result.MethodsScanned} Methoden gescannt). " +
                  "Kandidaten, keine Verstoesse — strukturelle Aehnlichkeit ist nicht zwingend Drift " +
                  "(z. B. mehrere legitime, aehnlich aufgebaute Dispose()-Implementierungen). Pruefe jeden " +
                  "Kandidaten manuell, bevor du ihn auf den Helper umstellst:\n" +
                  "Evidenzgrenze: statische strukturelle Aehnlichkeit; kein Laufzeitbeweis.\n" +
                  "Countercheck: Reflection, DI, Generatoren, dynamic und manuelle Semantikpruefung.");

        var index = 0;
        foreach (var candidate in result.ShownCandidates)
        {
            index++;
            var relativePath = PathNormalizer.ToRelative(solutionDir, candidate.FilePath);
            sb.Append($"\n{index}. {candidate.SignatureName} ({relativePath}:{candidate.LineNumber}, " +
                      $"{candidate.TokenCount} Tokens, Score {candidate.Score:F2}) ruft '{result.HelperSymbolDisplayName}' nicht auf.");
        }

        if (result.Truncated)
        {
            sb.Append('\n');
            sb.Append($"[{result.TotalCandidates} Kandidaten gesamt, {result.ShownCandidates.Count} gezeigt — " +
                      "maxResults erhoehen oder scopeDir eingrenzen]");
        }

        return sb.ToString();
    }
}
