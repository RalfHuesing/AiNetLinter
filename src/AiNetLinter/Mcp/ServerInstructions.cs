#nullable enable

namespace AiNetLinter.Mcp;

/// <summary>
/// Single-Source-of-Truth fuer den <c>initialize</c>-Handshake-Hinweis, den
/// <see cref="McpServerOptionsFactory"/> ueber <c>ModelContextProtocol.Server.McpServerOptions
/// .ServerInstructions</c> an jeden verbundenen Client durchreicht (SDK-Property, kein
/// eigenes Protokoll-Feature — siehe <see cref="McpServerOptionsBuilder.WithServerInstructions"/>).
/// Ersetzt den vormals inline in <see cref="McpServerOptionsFactory"/> gepflegten kurzen
/// C#-only-Hinweis durch eine vollstaendige, an einer Stelle gepflegte Doctrine (Q4 in
/// <c>tasks/features/05-roadmap.md</c> §3, analog zu CodeGraphs <c>server-instructions.ts</c>,
/// siehe <c>tasks/features/04-explore-vs-flow-tools.md</c> §7.2): Tool-Uebersicht, C#-Only-Grenze
/// mit Fallback, Sufficiency-Doctrine (siehe <see cref="McpSufficiencyHints"/>) und der
/// isError-Hinweis (Policy-Details in <c>src/AiNetLinter/Mcp/IsErrorPolicy.md</c>). Bewusst als
/// einzelne <see langword="const"/>-Property statt mehrerer verstreuter Strings, damit ein
/// zukuenftiges neues Tool oder eine Policy-Aenderung an genau einer Stelle nachgezogen wird,
/// statt in jeder Tool-Description dupliziert zu werden.
/// </summary>
internal static class ServerInstructions
{
    /// <summary>Vollstaendiger Instructions-Text, siehe Klassen-Dokumentation fuer Herkunft/Zweck
    /// der einzelnen Abschnitte.</summary>
    internal const string Text =
        "AiNetLinter ist ein Roslyn-basierter C#-Linter; in diesem Prozess laeuft er als " +
        "stdio-MCP-Server gegen eine resident geladene .NET-Solution.\n\n" +
        "Tools (1 Satz je Tool — volle Parameter-Schemas liefert tools/list):\n" +
        "- find_symbol: Sucht C#-Symbole per Substring im Namen.\n" +
        "- find_references: Findet Aufrufstellen eines C#-Symbols, optional transitiv (depth).\n" +
        "- get_impact: Findet Aufrufstellen geaenderter Signaturen per Git-Diff (Default: " +
        "uncommittete Aenderungen) oder fuer ein einzelnes Symbol.\n" +
        "- get_type_hierarchy: Liefert Basisklassen, Interfaces, abgeleitete Typen und " +
        "heuristische DI-Registrierungen eines Typs.\n" +
        "- get_file_skeleton: Liefert das Struktur-Skelett (Typen, Signaturen ohne Bodies) " +
        "einer C#-Datei.\n" +
        "- get_index_scope: Liefert eine Dateityp-Aufschluesselung der geladenen Solution — " +
        "guter erster Call vor find_symbol/search_pattern.\n" +
        "- get_hotspots: Liefert .cs-Dateien, die ihrem Zeilen-Limit nahekommen oder es " +
        "ueberschreiten.\n" +
        "- get_violations: Liefert aktuelle Lint-Regelverstoesse der geladenen Solution.\n" +
        "- get_symbol_body: Liefert den Source-Body eines C#-Symbols per stabiler ID oder " +
        "Datei:Zeile:Spalte.\n" +
        "- search_pattern: Text- oder Regex-Suche ueber den gesamten Dateibestand, alle " +
        "Dateitypen.\n" +
        "- reload_config: Liest die rules.json zur Laufzeit neu ein, ohne Server-Neustart.\n" +
        "- get_server_health: Liefert LoadState, Uptime, Solution-Refreshes und Call-Log-" +
        "Aggregate.\n\n" +
        "C#-only-Grenze: find_symbol, find_references, get_impact, get_type_hierarchy, " +
        "get_file_skeleton, get_violations und get_symbol_body arbeiten ausschliesslich auf " +
        ".cs-Quellcode (Roslyn-Symbolgraph). Fuer Namen/Strings, die nur in .js, .razor, " +
        ".cshtml, .xaml, .html oder .css vorkommen, ist search_pattern der passende Fallback " +
        "(deckt alle Dateitypen ab). get_index_scope und get_hotspots arbeiten ohne diese " +
        "C#-Beschraenkung.\n\n" +
        "Sufficiency-Doctrine: liefert ein Tool vollstaendige/finale Daten fuer den " +
        "angefragten Scope (erkennbar am Hinweis \"Diese Daten sind vollstaendig ... kein " +
        "zusaetzliches Read/Grep noetig\" — siehe get_violations, get_symbol_body ohne " +
        "Truncation, find_references ohne Truncation, get_type_hierarchy), diese nicht per " +
        "Read/Grep redundant nachverifizieren. Ein trunkiertes Ergebnis traegt stattdessen " +
        "eine eigene Meta-Zeile (\"... gezeigt — maxResults erhoehen/depth reduzieren\"): dort " +
        "sind weitere Tool-Calls mit angepassten Parametern der richtige naechste Schritt, " +
        "nicht Read/Grep.\n\n" +
        "isError-Policy (Details: src/AiNetLinter/Mcp/IsErrorPolicy.md): isError=true kommt " +
        "ausschliesslich bei SOLUTION_NOT_LOADED, Sicherheitsverweigerungen und echten " +
        "Malfunctions (unerwartete Fehler, Hinweis auf einmaligen Retry im Text) vor. Alle " +
        "anderen erwartbaren Bedingungen (Symbol nicht gefunden, mehrdeutiger Identifikator, " +
        "ungueltiges Argument, Datei nicht gefunden, leere Treffermengen) liefern isError=false " +
        "mit konkreter Handlungsanleitung im Text — kein Grund, das Tool deswegen aufzugeben.\n\n" +
        "Kurzueberblick inkl. aktuellem Server-Status (geladene Solution, verwendete " +
        "rules.json oder Default-Regeln): Resource ainetlinter://overview per resources/read.";
}
