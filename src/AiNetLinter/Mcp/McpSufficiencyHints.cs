#nullable enable

namespace AiNetLinter.Mcp;

/// <summary>
/// Gemeinsamer Sufficiency-Hinweis fuer MCP-Tool-Outputs, die vollstaendige/finale Daten fuer
/// den angefragten Scope liefern (Q5 in <c>tasks/features/05-roadmap.md</c> §3, Sufficiency-
/// Doctrine in <see cref="ServerInstructions"/>). Verhindert, dass ein Agent nach einem bereits
/// vollstaendigen Tool-Ergebnis redundant per Read/Grep nachverifiziert — dieselbe Lehre wie
/// CodeGraphs "do NOT Read these files"-Hinweis (siehe <c>tasks/features/04-explore-vs-flow-tools.md</c>
/// §7.2). Sibling-Datei zu <see cref="McpTruncation"/>: dort steckt die Trunkierungs-Meta-Zeile
/// fuer den Gegenfall (Ergebnis unvollstaendig, weitere Calls noetig) — beide Hinweise schliessen
/// sich gegenseitig aus und werden nie auf denselben Output angewendet.
/// </summary>
internal static class McpSufficiencyHints
{
    /// <summary>
    /// Hinweistext fuer vollstaendige/nicht-trunkierte Ergebnisse. Bewusst kurz und einheitlich
    /// formuliert (keine tool-spezifischen Varianten), damit Agenten den Hinweis wiedererkennen,
    /// unabhaengig davon, welches der vier Tools (get_violations, get_symbol_body,
    /// find_references, get_type_hierarchy) ihn anhaengt.
    /// </summary>
    private const string CompleteDataHint =
        "[HINWEIS]: Diese Daten sind vollstaendig fuer den angefragten Scope — kein zusaetzliches Read/Grep noetig.";

    /// <summary>
    /// Haengt <see cref="CompleteDataHint"/> an <paramref name="text"/> an (Leerzeile davor).
    /// Aufrufer duerfen das nur fuer nicht-trunkierte Ergebnisse tun — trunkierte Ergebnisse
    /// tragen bereits ihre eigene Trunkierungs-Meta-Zeile (siehe <see cref="McpTruncation"/>),
    /// die implizit signalisiert, dass weitere Calls (nicht Read/Grep) noetig sind.
    /// </summary>
    internal static string Append(string text) => text + "\n\n" + CompleteDataHint;
}
