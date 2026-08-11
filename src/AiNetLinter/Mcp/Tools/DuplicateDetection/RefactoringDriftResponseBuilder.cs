#nullable enable

using System.Linq;
using System.Text;
using AiNetLinter.Core.DuplicateDetection;
using AiNetLinter.Output;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Tools.DuplicateDetection;

/// <summary>
/// Text-/JSON-Formatierung fuer den <c>mode="refactoring-drift"</c>-Zweig von
/// <c>find_duplicates</c> (Teil C) — aus <see cref="DuplicateDetectionTool"/> ausgelagert (eigene
/// Datei statt Anhaengen an dessen bereits vorhandene <c>clone</c>-Formatierung), damit die
/// "Kandidaten statt Verstoesse"-Formulierung (Roadmap "Teil C" Punkt 4) an einer Stelle lebt statt
/// mit der Cluster-Formatierung von Teil A vermischt zu werden.
/// </summary>
internal static class RefactoringDriftResponseBuilder
{
    internal static CallToolResult Build(Microsoft.CodeAnalysis.Solution solution, RefactoringDriftScanResultForTool result)
    {
        var solutionDir = System.IO.Path.GetDirectoryName(solution.FilePath) ?? "";
        var body = RenderText(solutionDir, result);
        // Trunkierungs-Meta-Zeile UND Sufficiency-Hinweis schliessen sich gegenseitig aus (siehe
        // McpSufficiencyHints-Doc-Kommentar) — nur bei vollstaendigem Ergebnis den Hinweis anhaengen.
        var finalText = result.Truncated ? body : McpSufficiencyHints.Append(body);

        var payload = new RefactoringDriftPayload(
            Candidates: result.ShownCandidates.Select(c => ToEntry(solutionDir, c)).ToList(),
            Summary: new RefactoringDriftSummary(
                HelperSymbol: result.HelperSymbolDisplayName,
                MethodsScanned: result.MethodsScanned,
                TotalCandidates: result.TotalCandidates,
                ShownCandidates: result.ShownCandidates.Count,
                Truncated: result.Truncated));

        // In ein Objekt gewrappt statt eines nackten Arrays (siehe
        // McpToolResults.Text<T>-Doc-Kommentar).
        return McpToolResults.Text(finalText, payload);
    }

    private static RefactoringDriftCandidateEntry ToEntry(string solutionDir, RefactoringDriftCandidate candidate) =>
        new(
            PathNormalizer.ToRelative(solutionDir, candidate.FilePath),
            candidate.LineNumber,
            candidate.SignatureName,
            candidate.TokenCount,
            candidate.Score);

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
                  "Kandidaten manuell, bevor du ihn auf den Helper umstellst:");

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
