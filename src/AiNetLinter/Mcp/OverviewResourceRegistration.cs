#nullable enable

using System.Collections.Generic;
using System.Text;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using RalfHuesing.Mcp.Observability;

namespace AiNetLinter.Mcp;

/// <summary>
/// Registriert die MCP-Resource <c>ainetlinter://overview</c>: ein kurzer, bei jedem
/// <c>resources/read</c> frisch generierter Markdown-Ueberblick fuer Agenten, die den Server
/// noch nicht kennen — welche Tools es gibt (Kurzbeschreibung, nicht die volle Tool-Description)
/// und mit welcher Solution/Config-Quelle der Prozess tatsaechlich laeuft (z. B. ob eine
/// projekteigene <c>rules.json</c> gefunden wurde oder der Server mit Default-Regeln laeuft).
/// Ergaenzt <c>tools/list</c>, ersetzt es nicht — dort stehen die vollstaendigen
/// Parameter-Schemas.
/// </summary>
internal static class OverviewResourceRegistration
{
    private const string OverviewUri = "ainetlinter://overview";

    /// <summary>
    /// Kurzbeschreibungen aller registrierten Tools (ein Satz, keine Parameter-Details — die
    /// liefert <c>tools/list</c>). Bewusst hier gepflegt statt aus den vollen Tool-Descriptions
    /// abgeleitet (die sind fuer diesen Zweck zu lang) — <c>OverviewResourceRegistrationTests</c>
    /// prueft die Namens-Parität gegen die tatsaechlich registrierten Tools, damit ein neues
    /// oder umbenanntes Tool hier nicht stillschweigend fehlt.
    /// </summary>
    internal static readonly IReadOnlyList<(string Name, string Summary)> ToolSummaries =
    [
        ("find_symbol", "Sucht C#-Symbole (Klasse/Methode/Property/Interface) per Substring im Namen."),
        ("find_references", "Findet Aufrufstellen eines C#-Symbols."),
        ("get_call_tree", "Liefert den echten Aufrufer- oder Aufgerufene-Baum eines C#-Symbols (Eltern-Kind-Struktur, ASCII oder Mermaid)."),
        ("get_impact", "Findet Aufrufstellen geaenderter Signaturen — per Git-Diff (Default: uncommittete Aenderungen) oder fuer ein einzelnes Symbol."),
        ("get_type_hierarchy", "Liefert Basisklassen, Interfaces, abgeleitete Typen und heuristische DI-Registrierungen eines Typs."),
        ("dependency_graph", "Liefert die Datei-/Typ-Abhaengigkeiten einer Datei oder eines Typs (eingehend/ausgehend/beides, echte SemanticModel-Typreferenzen)."),
        ("get_file_skeleton", "Liefert das Struktur-Skelett (Typen, Signaturen ohne Bodies) einer C#-Datei."),
        ("get_class_structure", "Liefert eine tabellarische Member- und Zeilen-Uebersicht eines C#-Typs."),
        ("get_symbol_body", "Liefert den Source-Body eines C#-Symbols per stabiler ID oder Datei:Zeile:Spalte."),
        ("get_violations", "Liefert aktuelle Lint-Regelverstoesse der geladenen Solution."),
        ("safeguard", "Liefert einen deterministischen 0-10-Quality-Score inkl. Pass/Fail-Threshold, Top-Violations und Remediation-Hint."),
        ("pattern_detect", "Gruppiert Lint-Regelverstoesse nach Pattern-Kategorie (god-class, async-void, long-method, public-without-doc, empty-catch, feature-envy)."),
        ("find_magic_values", "On-Demand-Audit nach Magic Values (URLs, Pfade, Timeouts, Format-Strings, Schwellenwerte, HTTP-Statuscodes) in C#-Quellcode mit Ziel-Empfehlung (appsettings.json, Constants.cs, StatusCodes.StatusXXX...)."),
        ("find_dead_code", "Findet unreferenzierten/toten Code (Typen, Methoden, Properties, Felder) mit Confidence-Stufen (high/low) und False-Positive-Schutz."),
        ("get_index_scope", "Liefert eine Dateityp-Aufschluesselung der geladenen Solution."),
        ("get_hotspots", "Liefert .cs-Dateien, die ihrem Zeilen-Limit nahekommen oder es ueberschreiten."),
        ("metrics_tree", "Liefert einen ASCII-Baum mit aggregierten Werten pro Verzeichnisknoten (z. B. Code-Groesse, Kommentaranteil) zur Ebene-fuer-Ebene-Exploration."),
        ("metrics_lookup", "Liefert punktgenaue Metriken (LOC, Komplexitaet, Parameter, AI-Context-Footprint) und Schwellwert-Abgleich fuer ein C#-Symbol."),
        ("search_pattern", "Text- oder Regex-Suche ueber den gesamten Dateibestand, alle Dateitypen."),
        ("reload_config", "Liest die rules.json zur Laufzeit neu ein, ohne Server-Neustart."),
        ("get_server_health", "Liefert LoadState, Uptime, Solution-Refreshes und Observability-Status."),
        (McpObservabilityTools.FeedbackToolName, "Meldet Probleme, False-Positives oder Feature-Wünsche zu diesem Server."),
        ("get_namespace_tree", "Ermoeglicht hierarchische semantische Exploration einer C#-Codebase (Solution -> Projekte -> Namespaces -> Typen)."),
        ("find_duplicates", "Findet Code-Duplikate (Token-basiertes Clone-Detection, Jaccard-N-Gram, Method-Granularitaet) als Cluster."),
        ("get_feature_context", "Composite One-Shot-Exploration fuer ein C#-Symbol (Deklaration, Metriken, Aufrufer, Tests, Violations) vor Edits."),
    ];

    internal static void Register(McpServerResourceCollection resources, McpCodeGraphServer mcpState)
    {
        resources.Add(McpServerResource.Create(
            () => BuildResult(mcpState),
            new McpServerResourceCreateOptions
            {
                UriTemplate = OverviewUri,
                Name = "overview",
                Description = "Kurzueberblick fuer Agenten: alle Tools in einem Satz je Zeile, " +
                    "plus aktueller Server-Status (geladene Solution, verwendete rules.json " +
                    "oder Default-Regeln). Bei jedem Read frisch generiert.",
                MimeType = "text/markdown",
            }));
    }

    private static ReadResourceResult BuildResult(McpCodeGraphServer mcpState)
    {
        return new ReadResourceResult
        {
            Contents =
            [
                new TextResourceContents
                {
                    Uri = OverviewUri,
                    MimeType = "text/markdown",
                    Text = BuildOverviewText(mcpState),
                },
            ],
        };
    }

    /// <summary>Reine Text-Bau-Funktion, direkt unit-testbar ohne MCP-Protokoll-Umweg.</summary>
    internal static string BuildOverviewText(McpCodeGraphServer mcpState)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# AiNetLinter MCP-Server — Kurzueberblick");
        sb.AppendLine();
        sb.AppendLine(
            "AiNetLinter ist ein Roslyn-basierter C#-Linter. In diesem Prozess laeuft er als " +
            "stdio-MCP-Server und macht die geladene .NET-Solution ueber die unten gelisteten " +
            "Tools abfragbar.");
        sb.AppendLine();
        sb.AppendLine("## Server-Status (dieser Prozess)");
        sb.AppendLine();
        sb.AppendLine($"- Solution: {DescribeSolution(mcpState)}");
        sb.AppendLine($"- Regeln: {DescribeConfig(mcpState)}");
        sb.AppendLine();
        sb.AppendLine($"## Tools ({ToolSummaries.Count})");
        sb.AppendLine();
        foreach (var (name, summary) in ToolSummaries)
        {
            sb.AppendLine($"- `{name}` — {summary}");
        }
        sb.AppendLine();
        sb.AppendLine("## Empfohlene Workflows (Tool-Choreographie)");
        sb.AppendLine();
        sb.AppendLine("1. Code erkunden (Token-sparend statt ganzer File-Dumps):");
        sb.AppendLine("   get_index_scope -> metrics_tree / get_hotspots -> get_file_skeleton / get_class_structure -> get_symbol_body");
        sb.AppendLine("2. Refactoring & Impact pruefen (Semantisch statt Text-Grep):");
        sb.AppendLine("   find_symbol -> find_references / get_call_tree -> get_impact / dependency_graph");
        sb.AppendLine("3. Quality-Gate vor Commit (Inkrementell im RAM):");
        sb.AppendLine("   safeguard (Score 0-10) -> get_violations -> find_magic_values / find_duplicates");
        sb.AppendLine();
        sb.AppendLine(
            "Vollstaendige Parameter-Schemas liefert `tools/list`; diese Resource ist nur die " +
            "Kurzuebersicht.");
        return sb.ToString().TrimEnd();
    }

    private static string DescribeSolution(McpCodeGraphServer mcpState)
    {
        return mcpState.LoadState switch
        {
            ServerLoadState.Loading => "wird noch geladen",
            ServerLoadState.LoadFailed => "Laden fehlgeschlagen — jeder Tool-Call liefert SOLUTION_NOT_LOADED",
            _ => mcpState.GetCurrentSolution()?.FilePath ?? "unbekannt",
        };
    }

    private static string DescribeConfig(McpCodeGraphServer mcpState)
    {
        // Atomarer Schnappschuss statt zweier getrennter Property-Zugriffe: sonst koennte ein
        // gleichzeitiger reload_config-Aufruf eine zerrissene Kombination liefern (siehe
        // McpCodeGraphServer.GetConfigSnapshot).
        var (_, usedDefaultConfig, resolvedConfigPath) = mcpState.GetConfigSnapshot();
        return usedDefaultConfig
            ? "keine rules.json gefunden — Server laeuft mit eingebauten Default-Regeln, nicht mit einer projekteigenen Konfiguration"
            : resolvedConfigPath ?? "unbekannt";
    }
}
