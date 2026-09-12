#nullable enable

namespace AiNetLinter.Mcp;

/// <summary>
/// Zentrale globale Anleitung fuer die Discovery-Antworten des MCP-Servers. Das SDK stellt sie
/// ueber <see cref="McpServerOptionsFactory"/> sowohl im <c>initialize</c>-Handshake als
/// auch in <c>server/discover</c> bereit. Der statische Erstkontakt-Leitfaden steht unter
/// <c>ainetlinter://agent-guide</c>; der kompakte Status je Projekt-Key unter
/// <c>ainetlinter://overview?targetPath=...</c>. Tool-Schemas bleiben in <c>tools/list</c>.
/// </summary>
internal static class ServerInstructions
{
    internal const int MaxUtf8Bytes = 1_200;

    /// <summary>Globale Regeln fuer MCP-Discovery; tool-spezifische Details stehen in <c>tools/list</c>.</summary>
    internal const string Text =
        "Zielgebundene Aufrufe brauchen targetPath: absoluter .sln/.slnx-Pfad fuer Source oder .dll/.exe fuer Assembly; die Endung bestimmt die Route. get_server_health darf ohne Ziel laufen, report_observability_feedback nie mit Ziel.\n\n" +
        "C#-Symbole, Referenzen und Graphen mit den semantischen Tools abfragen; fuer Text und Nicht-C# search_pattern verwenden. Schemas, Defaults und Toolgrenzen stehen in tools/list.\n\n" +
        "Contract v2: structuredContent.navigation.status trennt operation und Completeness; IDs nur aus structuredContent.id uebernehmen. Bei Budgetfehler minimumResponseBytes mit gleichem Snapshot wiederholen; includeReferences erweitert Assembly-Suche nur explizit.\n\n" +
        "Den Integrationsleitfaden nur bei ausdruecklichem Auftrag unter ainetlinter://agent-guide lesen; die Agent-API-Resource ergaenzt Discovery-Details.";
}
