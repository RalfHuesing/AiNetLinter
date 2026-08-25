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
        "AiNetLinter analysiert .NET-Solutions mit Roslyn. JEDEM Tool-Aufruf ist projectRoot " +
        "beizufuegen: ein absoluter Projektroot-Pfad. Einzige Ausnahme: der optionale " +
        "get_server_health-Filter.\n\n" +
        "Initialisierung: Im Projektroot liegt ainetlinter.project.json mit den Pflichtfeldern " +
        "\"solution\" und \"rules\"; Pfade gelten relativ zur Definitionsdatei. Fehlt oder ist " +
        "die Datei defekt, antwortet der Server deterministisch (PROJECT_NOT_INITIALIZED bzw. " +
        "RULES_INVALID) inklusive kopierfaehigem Template statt stillschweigenden Defaults.\n\n" +
        "Neue Integration: zuerst ainetlinter://agent-guide lesen; den Projektstatus danach " +
        "ueber ainetlinter://overview?projectRoot=<url-encoded> pruefen.\n\n" +
        "C#-Symbolgraph-Grenze: C#-Symbole ueber die semantischen Tools abfragen; fuer " +
        "Text/Namen ausserhalb von .cs (z. B. .js, .razor, .cshtml, .xaml, .html, .css) " +
        "search_pattern verwenden. enrichCSharp=true reichert sichtbare Treffer geladener " +
        "C#-Dokumente opt-in an; ambiguous/unavailable bleiben sichtbar.\n\n" +
        "Schemas und Toolzwecke: tools/list.\n\n" +
        "Sufficiency: Vollstaendige Ergebnisse nicht redundant per Read/Grep pruefen; bei " +
        "truncated Limits oder Scope verfeinern.\n\n" +
        "isError-Policy: isError=true ist nicht initialisierten Projekten, Sicherheitsverweigerung " +
        "oder Malfunction vorbehalten.\n\n" +
        "Start: get_feature_context -> get_symbol_body; find_symbol -> find_references/get_impact; " +
        "safeguard -> get_violations.";
}
