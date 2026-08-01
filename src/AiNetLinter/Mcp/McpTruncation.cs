#nullable enable

using System.Collections.Generic;
using System.Linq;

namespace AiNetLinter.Mcp;

/// <summary>
/// Wiederverwendbarer Trunkierungs-Helper fuer MCP-Tool-Listen-Antworten. Wenn die Treffermenge
/// das konfigurierte Limit (<paramref name="maxResults"/>) uebersteigt, werden nur die ersten
/// <paramref name="maxResults"/> Zeilen ausgegeben und eine einheitliche Meta-Zeile
/// "[N Treffer gesamt, M gezeigt — Pattern verfeinern oder maxResults erhöhen]" angehaengt.
/// Format entspricht <c>konzept.md</c> Z. 230-233 (P0/P1, Plain-Text, einheitlich fuer alle
/// Listen-Tools). Bewusst als sibling-Datei zu <see cref="McpToolResults"/> extrahiert, damit
/// Folge-Einheiten (003/004/005) den Helper ohne Suchen in der Antwort-Bibliothek finden.
/// </summary>
internal static class McpTruncation
{
    /// <summary>
    /// Liefert <paramref name="hitLines"/> als "\n"-verbundene Textzeilen. Wenn
    /// <paramref name="totalMatches"/> groesser als <paramref name="maxResults"/> ist, werden nur
    /// die ersten <paramref name="maxResults"/> Zeilen zurueckgegeben und eine Meta-Zeile
    /// "[N Treffer gesamt, M gezeigt — Pattern verfeinern oder maxResults erhöhen]" angehaengt.
    /// Quelle der Wahrheit fuer die Gesamtzahl in der Meta-Zeile ist <paramref name="totalMatches"/>
    /// (nicht <c>hitLines.Count</c>), damit die Aussage auch dann korrekt bleibt, wenn ein
    /// Aufrufer aus Speichergruenden vorab trunkiert hat (z. B. in einer kuenftigen EPIC-08-
    /// Optimierung).
    /// </summary>
    internal static string TruncateLines(
        IReadOnlyList<string> hitLines,
        int totalMatches,
        int maxResults)
    {
        if (totalMatches <= maxResults)
        {
            return string.Join("\n", hitLines);
        }

        var shown = hitLines.Count <= maxResults ? hitLines : hitLines.Take(maxResults).ToList();
        var meta = $"[{totalMatches} Treffer gesamt, {maxResults} gezeigt — Pattern verfeinern oder maxResults erhöhen]";
        return string.Join("\n", shown) + "\n" + meta;
    }
}
