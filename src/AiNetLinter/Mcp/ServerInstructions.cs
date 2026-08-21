#nullable enable

namespace AiNetLinter.Mcp;

/// <summary>
/// Zentrale globale Anleitung fuer die Discovery-Antworten des MCP-Servers. Das SDK stellt sie
/// ueber <see cref="McpServerOptionsFactory"/> sowohl im Legacy-<c>initialize</c>-Handshake als
/// auch in <c>server/discover</c> bereit. Tool-Schemas und die vollstaendige Kurzliste bleiben
/// bewusst in <c>tools/list</c> bzw. <c>ainetlinter://overview</c>, damit diese Angaben nicht in
/// jedem globalen Instructions-Text dupliziert werden.
/// </summary>
internal static class ServerInstructions
{
    internal const int MaxUtf8Bytes = 2_557;

    /// <summary>Globale Regeln fuer MCP-Discovery; tool-spezifische Details stehen in <c>tools/list</c>.</summary>
    internal const string Text =
        "AiNetLinter analysiert die resident geladene .NET-Solution mit Roslyn.\n\n" +
        "C#-Symbolgraph-Grenze: C#-Symbole ueber die semantischen Tools abfragen; fuer " +
        "Text/Namen ausserhalb von .cs (z. B. .js, .razor, .cshtml, .xaml, .html, .css) " +
        "search_pattern verwenden. enrichCSharp=true reichert sichtbare Treffer geladener " +
        "C#-Dokumente opt-in an; ambiguous/unavailable bleiben sichtbar.\n\n" +
        "Schemas und Toolzwecke: tools/list. Kompakter Status und Workflows: " +
        "ainetlinter://overview.\n\n" +
        "Sufficiency: Vollstaendige Ergebnisse nicht redundant per Read/Grep pruefen; bei " +
        "truncated Limits oder Scope verfeinern.\n\n" +
        "isError-Policy: isError=true ist fuer nicht geladene Solution, Sicherheitsverweigerung oder " +
        "Malfunction reserviert.\n\n" +
        "Start: Edits get_feature_context -> get_symbol_body; Impact find_symbol -> " +
        "find_references/get_impact; Gate safeguard -> get_violations.";
}
