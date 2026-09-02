#nullable enable

namespace AiNetLinter.Mcp;

/// <summary>
/// Zentrale globale Anleitung fuer die Discovery-Antworten des MCP-Servers. Das SDK stellt sie
/// ueber <see cref="McpServerOptionsFactory"/> sowohl im Legacy-<c>initialize</c>-Handshake als
/// auch in <c>server/discover</c> bereit. Der statische Erstkontakt-Leitfaden steht unter
/// <c>ainetlinter://agent-guide</c>; der kompakte Status je Projekt-Key unter
/// <c>ainetlinter://overview?projectRoot=...</c>. Tool-Schemas bleiben in <c>tools/list</c>.
/// </summary>
internal static class ServerInstructions
{
    internal const int MaxUtf8Bytes = 2_557;

    /// <summary>Globale Regeln fuer MCP-Discovery; tool-spezifische Details stehen in <c>tools/list</c>.</summary>
    internal const string Text =
        "AiNetLinter analysiert .NET-Solutions mit Roslyn. JEDEM zielgebundenen Tool-Aufruf " +
        "sind targetType und targetPath beizufuegen: targetType='project' fuer eine Source-" +
        "Solution oder targetType='assembly' fuer eine lokale .dll- oder .exe-Datei; targetPath ist absolut. " +
        "get_server_health darf ohne Ziel aggregieren, report_observability_feedback bleibt " +
        "nicht zielgebunden.\n\n" +
        "Neue Integration nur bei ausdruecklichem Auftrag: ainetlinter://agent-guide lesen; " +
        "den Projektstatus danach ueber ainetlinter://overview?projectRoot=<url-encoded> pruefen. " +
        "PROJECT_NOT_INITIALIZED und RULES_INVALID bleiben deterministische Fehler.\n\n" +
        "C#-Symbolgraph-Grenze: C#-Symbole ueber die semantischen Tools abfragen; fuer " +
        "Text/Namen ausserhalb von .cs (z. B. .js, .razor, .cshtml, .xaml, .html, .css) " +
        "search_pattern verwenden. enrichCSharp=true reichert sichtbare Treffer geladener " +
        "C#-Dokumente opt-in an; ambiguous/unavailable bleiben sichtbar.\n\n" +
        "Assembly-Capability-Matrix (13 Cross-Target-Tools): dependency_graph, find_references, " +
        "find_symbol, get_call_tree, get_class_structure, get_file_skeleton, get_impact, " +
        "get_namespace_tree, get_symbol_body, get_type_hierarchy, metrics_lookup, metrics_tree " +
        "und get_server_health. get_impact akzeptiert fuer Assemblys nur symbolIdentifier. " +
        "Assembly-only: inspect_assembly und find_assembly_extensions; beide akzeptieren .dll/.exe.\n\n" +
        "Schemas und Toolzwecke: tools/list.\n\n" +
        "Sufficiency: Vollstaendige Ergebnisse nicht redundant per Read/Grep pruefen; bei " +
        "truncated Limits oder Scope verfeinern.\n\n" +
        "isError-Policy: isError=true ist nicht initialisierten Projekten, Sicherheitsverweigerung " +
        "oder Malfunction vorbehalten.\n\n" +
        "Start: get_feature_context -> get_symbol_body; find_symbol -> find_references/get_impact; " +
        "safeguard -> get_violations.";
}
