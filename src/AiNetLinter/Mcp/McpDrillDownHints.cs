#nullable enable

namespace AiNetLinter.Mcp;

/// <summary>
/// Gegenlaeufiger Hinweistyp zu <see cref="McpSufficiencyHints"/>: <c>metrics_tree</c>-Output ist per
/// Definition nie vollstaendig (immer Top-N, nie alle Kinder) — <see cref="McpSufficiencyHints.Append"/>
/// waere hier irrefuehrend. Sibling-Datei, gleiches Kurz-Prinzip (ein einheitlicher Text, keine
/// tool-spezifischen Varianten).
/// </summary>
internal static class McpDrillDownHints
{
    /// <summary>Haengt einen Drill-down-Hinweis an <paramref name="text"/> an (Leerzeile davor).</summary>
    internal static string Append(string text, int depth)
    {
        return text + "\n\n" +
            $"[HINWEIS]: Dies zeigt Ebene 1-{depth} ab dem angefragten root — " +
            "Top-N-Ausschnitt, nicht vollstaendig. Fuer tiefere Details: root auf einen " +
            "der angezeigten Kind-Pfade setzen und/oder depth erhoehen.";
    }
}
