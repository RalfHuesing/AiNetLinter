#nullable enable

namespace AiNetLinter.Mcp;

/// <summary>
/// Single-Source-of-Truth fuer den <c>initialize</c>-Handshake-Hinweis, den
/// <see cref="McpServerOptionsFactory"/> ueber <c>ModelContextProtocol.Server.McpServerOptions
/// .ServerInstructions</c> an jeden verbundenen Client durchreicht (SDK-Property, kein
/// eigenes Protokoll-Feature — siehe <see cref="McpServerOptionsBuilder.WithServerInstructions"/>).
/// Liefert die vollstaendige, an einer Stelle gepflegte Doctrine (analog zu CodeGraphs
/// <c>server-instructions.ts</c>): Tool-Uebersicht, C#-Only-Grenze mit Fallback,
/// Sufficiency-Doctrine (siehe <see cref="McpSufficiencyHints"/>) und der isError-Hinweis
/// (Policy-Details in <c>src/AiNetLinter/Mcp/IsErrorPolicy.md</c>). Bewusst als einzelne
/// <see langword="const"/>-Property statt mehrerer verstreuter Strings, damit ein zukuenftiges
/// neues Tool oder eine Policy-Aenderung an genau einer Stelle nachgezogen wird, statt in jeder
/// Tool-Description dupliziert zu werden.
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
         "- get_call_tree: Liefert den echten Aufrufer- oder Aufgerufene-Baum eines C#-Symbols " +
         "(Eltern-Kind-Struktur, ASCII oder Mermaid), direction incoming/outgoing/both, " +
         "transitiv bis depth 5.\n" +
        "- dependency_graph: Liefert Datei-/Typ-Abhaengigkeiten einer Datei oder eines Typs " +
        "(eingehend/ausgehend/beides, echte SemanticModel-Typreferenzen statt using-Direktiven), " +
        "optional transitiv (depth).\n" +
        "- get_namespace_tree: Ermoeglicht hierarchische semantische Exploration einer C#-Codebase (Solution -> Projekte -> Namespaces -> Typen).\n" +
        "- get_file_skeleton: Liefert das Struktur-Skelett (Typen, Signaturen ohne Bodies) " +
        "einer oder mehrerer C#-Dateien (Batch-Support).\n" +
        "- get_class_structure: Liefert eine tabellarische Member- und Zeilen-Uebersicht eines C#-Typs.\n" +
        "- get_index_scope: Liefert eine Dateityp-Aufschluesselung der geladenen Solution — " +
        "guter erster Call vor find_symbol/search_pattern.\n" +
        "- get_hotspots: Liefert .cs-Dateien, die ihrem Zeilen-Limit nahekommen oder es " +
        "ueberschreiten.\n" +
        "- metrics_tree: Liefert einen ASCII-Baum mit aggregierten Werten pro " +
        "Verzeichnisknoten (Modi code_size/comment_density/violation_density/complexity) " +
        "und sortierten Top-N-Kindern je Ebene.\n" +
        "- metrics_lookup: Liefert punktgenaue Metriken (LOC, Komplexitaet, Parameter, AI-Context-Footprint) und Schwellwert-Abgleich fuer ein oder mehrere C#-Symbole (Batch-Support).\n" +
        "- get_feature_context: Composite One-Shot-Exploration fuer ein C#-Symbol (Deklaration, Metriken & Budget, direkte Aufrufer, Test-Abdeckung, Linter-Violations) in einem einzigen Call vor Edits.\n" +
        "- get_test_context: Ermittelt Test-Dateien, Test-Klassen, Test-Methoden, Kategorien und Filterbefehle fuer ein C#-Symbol.\n" +
        "- get_violations: Liefert aktuelle Lint-Regelverstoesse der geladenen Solution.\n" +
        "- safeguard: Liefert einen deterministischen 0-10-Quality-Score + Pass/Fail-Threshold " +
        "+ Top-Violations + Remediation-Hints fuer die geladene Solution (Quality-Gate).\n" +
        "- pattern_detect: Gruppiert Lint-Regelverstoesse nach Pattern-Kategorie (god-class, " +
        "async-void, long-method, public-without-doc, empty-catch, feature-envy) statt " +
        "flacher Datei-Liste — Solution-weite Audit-Sicht.\n" +
        "- find_magic_values: Fuehrt einen On-Demand-Audit nach Magic Values (URLs, Pfade, " +
        "Timeouts, Format-Strings, Schwellenwerte, HTTP-Statuscodes) in C#-Quellcode durch.\n" +
        "- find_dead_code: Findet unreferenzierten/toten Code (Typen, Methoden, Properties, Felder) " +
        "mit Confidence-Stufen (high/low) und False-Positive-Schutz.\n" +
        "- get_symbol_body: Liefert den Source-Body eines oder mehrerer C#-Symbole (Batch-Support) per stabiler ID oder " +
        "Datei:Zeile:Spalte.\n" +
        "- search_pattern: Text- oder Regex-Suche ueber den gesamten Dateibestand, alle " +
        "Dateitypen.\n" +
        "- reload_config: Liest die rules.json zur Laufzeit neu ein, ohne Server-Neustart.\n" +
        "- get_server_health: Liefert LoadState, Uptime, Solution-Refreshes und Observability-Status.\n" +
        "- report_observability_feedback: Meldet Probleme, False-Positives, unerwartete Ergebnisse oder Feature-Wünsche zu diesem MCP-Server, um AiNetLinter zu verbessern.\n" +
        "- find_duplicates: Findet Code-Duplikate (Token-basiertes Clone-Detection, " +
        "Jaccard-N-Gram, Method-Granularitaet) als transitiv gruppierte Cluster statt isolierter " +
        "Paare, gestaffelt nach exact/near/fuzzy-Aehnlichkeit; mode='structural' findet " +
        "semantisch aehnliche Hilfsmethoden als pruefbare Kandidatencluster.\n\n" +
        "C#-only-Grenze: find_symbol, find_references, get_call_tree, get_impact, " +
        "get_type_hierarchy, dependency_graph, get_namespace_tree, get_file_skeleton, get_class_structure, metrics_lookup, get_feature_context, get_test_context, get_violations, safeguard, " +
        "pattern_detect, find_magic_values, find_dead_code, get_symbol_body und find_duplicates " +
        "arbeiten ausschliesslich auf .cs-Quellcode (Roslyn-Symbolgraph). Fuer Namen/Strings, die nur in .js, .razor, " +
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
        "Empfohlene Workflows:\n" +
        "- Vor Edits & Refactoring: get_feature_context (One-Shot: Deklaration + Metriken + Callers + Tests + Violations) -> get_symbol_body\n" +
        "- Test-Absicherung: get_test_context (Test-Dateien, Methoden, Kategorien, dotnet test Befehle)\n" +
        "- Code erkunden: get_index_scope -> get_namespace_tree / metrics_tree / get_hotspots -> get_file_skeleton / get_class_structure -> get_symbol_body\n" +
        "- Refactoring & Impact: find_symbol -> find_references / get_call_tree -> get_impact / dependency_graph\n" +
        "- Quality-Gate vor Commit: safeguard -> get_violations -> find_magic_values / find_duplicates\n\n" +
        "isError-Policy (Details: src/AiNetLinter/Mcp/IsErrorPolicy.md): isError=true kommt " +
        "ausschliesslich bei SOLUTION_NOT_LOADED, Sicherheitsverweigerungen und echten " +
        "Malfunctions (unerwartete Fehler, Hinweis auf einmaligen Retry im Text) vor. Alle " +
        "anderen erwartbaren Bedingungen (Symbol nicht gefunden, mehrdeutiger Identifikator, " +
        "ungueltiges Argument, Datei nicht gefunden, leere Treffermengen) liefern isError=false " +
        "mit konkreter Handlungsanleitung im Text — kein Grund, das Tool deswegen aufzugeben.\n\n" +
        "Kurzueberblick inkl. aktuellem Server-Status (geladene Solution, verwendete " +
        "rules.json oder Default-Regeln): Resource ainetlinter://overview per resources/read.";
}
