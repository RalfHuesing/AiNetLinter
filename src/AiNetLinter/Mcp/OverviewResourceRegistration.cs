#nullable enable

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Projects;
using AiNetLinter.Output;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using RalfHuesing.Mcp.Observability;

namespace AiNetLinter.Mcp;

/// <summary>
/// Registriert die MCP-Resource <c>ainetlinter://overview</c> als Resource-Template mit dem
/// Pflicht-Query-Parameter <c>projectRoot</c>: ein kurzer, bei jedem <c>resources/read</c>
/// frisch generierter Markdown-Ueberblick fuer Agenten, die den Server noch nicht kennen —
/// welche Tools es gibt (Kurzbeschreibung, nicht die volle Tool-Description) und mit welcher
/// Solution/Config-Quelle der adressierte Key tatsaechlich laeuft. MCP-Resources nehmen keine
/// Tool-Argumente, daher adressiert der URL-kodierte Projektroot den Registry-Key; Guards und
/// Fehlervertraege entsprechen denen der Tools (PROJECT_ROOT_REQUIRED/_INVALID,
/// PROJECT_NOT_INITIALIZED). Ergaenzt <c>tools/list</c>, ersetzt es nicht — dort stehen die
/// vollstaendigen Parameter-Schemas.
/// </summary>
internal static class OverviewResourceRegistration
{
    // RFC-6570-Form-Expansion: ainetlinter://overview{?projectRoot} expandiert zu
    // ainetlinter://overview?projectRoot=<url-encoded>.
    private const string OverviewUriTemplate = "ainetlinter://overview{?projectRoot}";

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
        ("get_file_skeleton", "Liefert das Struktur-Skelett (Typen, Signaturen ohne Bodies) einer oder mehrerer C#-Dateien (Batch-Support)."),
        ("get_class_structure", "Liefert eine tabellarische Member- und Zeilen-Uebersicht eines C#-Typs."),
        ("get_symbol_body", "Liefert den Source-Body eines oder mehrerer C#-Symbole (Batch-Support) per stabiler ID oder Datei:Zeile:Spalte."),
        ("get_violations", "Liefert aktuelle Lint-Regelverstoesse der geladenen Solution."),
        ("safeguard", "Liefert einen deterministischen 0-10-Quality-Score inkl. Pass/Fail-Threshold, Top-Violations und Remediation-Hint."),
        ("pattern_detect", "Gruppiert Lint-Regelverstoesse nach Pattern-Kategorie (god-class, async-void, long-method, public-without-doc, empty-catch, feature-envy)."),
        ("find_magic_values", "On-Demand-Audit nach Magic Values (URLs, Pfade, Timeouts, Format-Strings, Schwellenwerte, HTTP-Statuscodes) in C#-Quellcode mit Ziel-Empfehlung (appsettings.json, Constants.cs, StatusCodes.StatusXXX...)."),
        ("find_dead_code", "Findet unreferenzierten/toten Code (Typen, Methoden, Properties, Felder) mit Confidence-Stufen (high/low) und False-Positive-Schutz."),
        ("get_index_scope", "Liefert eine Dateityp-Aufschluesselung der geladenen Solution."),
        ("get_hotspots", "Liefert .cs-Dateien, die ihrem Zeilen-Limit nahekommen oder es ueberschreiten."),
        ("metrics_tree", "Liefert einen ASCII-Baum mit aggregierten Werten pro Verzeichnisknoten (z. B. Code-Groesse, Kommentaranteil) zur Ebene-fuer-Ebene-Exploration."),
        ("metrics_lookup", "Liefert punktgenaue Metriken (LOC, Komplexitaet, Parameter, AI-Context-Footprint) und Schwellwert-Abgleich fuer ein oder mehrere C#-Symbole (Batch-Support)."),
        ("search_pattern", "Text- oder Regex-Suche ueber alle Dateitypen; enrichCSharp=true reichert sichtbare C#-Treffer opt-in innerhalb des geladenen Solution-/Projekt-Snapshots an, markiert ambiguous/unavailable und nennt bei Trunkierung die Folge: Pattern, Scope oder Limits verfeinern."),
        ("reload_config", "Liest die rules.json zur Laufzeit neu ein, ohne Server-Neustart."),
        ("get_server_health", "Liefert pro Projekt-Key LoadState, Uptime, Solution-Refreshes und Observability-Status."),
        (McpObservabilityTools.FeedbackToolName, "Meldet Probleme, False-Positives oder Feature-Wünsche zu diesem Server."),
        ("get_namespace_tree", "Ermoeglicht hierarchische semantische Exploration einer C#-Codebase (Solution -> Projekte -> Namespaces -> Typen)."),
        ("find_duplicates", "Findet Code-Duplikate (Token-basiertes Clone-Detection, Jaccard-N-Gram, Method-Granularitaet) als Cluster."),
        ("get_feature_context", "Composite One-Shot-Exploration fuer ein C#-Symbol (Deklaration, Metriken, Aufrufer, statische Test-Zuordnung, Violations) vor Edits."),
        ("get_test_context", "Ermittelt statische Test-Zuordnungen, Test-Klassen, Test-Methoden, Kategorien und Filterbefehle fuer ein C#-Symbol."),
    ];

    internal static void Register(McpServerResourceCollection resources, ProjectRegistry registry)
    {
        resources.Add(McpServerResource.Create(
            (string projectRoot) => BuildTemplatedResult(registry, projectRoot),
            new McpServerResourceCreateOptions
            {
                UriTemplate = OverviewUriTemplate,
                Name = "overview",
                Description = "Kurzueberblick fuer Agenten: alle Tools in einem Satz je Zeile, " +
                    "plus aktueller Server-Status des adressierten Projekts (geladene Solution, " +
                    "verwendete rules.json oder Default-Regeln). Pflicht: Query-Parameter " +
                    "projectRoot mit absolutem Projektroot (URL-kodiert). Bei jedem Read frisch generiert.",
                MimeType = "text/markdown",
            }));
    }

    internal static ReadResourceResult BuildTemplatedResult(ProjectRegistry registry, string? projectRoot) =>
        BuildTemplatedResult(registry, projectRoot, BuildResult);

    internal static ReadResourceResult BuildTemplatedResult(
        ProjectRegistry registry,
        string? projectRoot,
        Func<ProjectSnapshot, ReadResourceResult> render)
    {
        var guard = ProjectToolCall.GuardRequiredAbsoluteRoot(projectRoot);
        if (guard is not null)
        {
            throw new McpException(ProjectToolCall.FormatGuard(guard));
        }

        var leaseResult = registry.Lease(projectRoot!);
        if (!leaseResult.Succeeded || leaseResult.Lease is null)
        {
            throw new McpException(LinterErrorFormatter.Format(
                leaseResult.ErrorCode!,
                leaseResult.ErrorMessage!,
                hint: ProjectToolCall.RecoverHint(leaseResult.ErrorCode!)));
        }

        using var lease = leaseResult.Lease;
        if (lease.Server.LoadState == ServerLoadState.LoadFailed)
        {
            var failure = ProjectToolCall.BuildLoadFailure(lease.Server, lease);
            throw new McpException(LinterErrorFormatter.Format(
                ProjectErrorCodes.ProjectLoadFailed,
                failure.Message,
                context: failure.Context,
                hint: failure.Hint));
        }

        return render(registry.SnapshotFor(lease));
    }

    private static string BuildCanonicalUri(string projectRoot) =>
        $"ainetlinter://overview?projectRoot={Uri.EscapeDataString(projectRoot)}";

    private static ReadResourceResult BuildResult(ProjectSnapshot snapshot)
    {
        return new ReadResourceResult
        {
            Contents =
            [
                new TextResourceContents
                {
                    Uri = BuildCanonicalUri(snapshot.RootPath),
                    MimeType = "text/markdown",
                    Text = BuildOverviewText(snapshot),
                },
            ],
        };
    }

    /// <summary>Reine Text-Bau-Funktion, direkt unit-testbar ohne MCP-Protokoll-Umweg.</summary>
    internal static string BuildOverviewText(ProjectSnapshot snapshot)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# AiNetLinter MCP-Server — Kurzueberblick");
        sb.AppendLine();
        sb.AppendLine(
            "AiNetLinter ist ein Roslyn-basierter C#-Linter. In diesem Prozess laeuft er als " +
            "stdio-MCP-Server und macht die geladene .NET-Solution ueber die unten gelisteten " +
            "Tools abfragbar.");
        sb.AppendLine();
        sb.AppendLine($"## Server-Status (Projekt {snapshot.RootPath})");
        sb.AppendLine();
        sb.AppendLine($"- Solution: {DescribeSolution(snapshot.Server)}");
        sb.AppendLine($"- Regeln: {DescribeConfig(snapshot.Server)}");
        sb.AppendLine($"- Zuletzt genutzt (UTC): {snapshot.LastUsedUtc:yyyy-MM-dd HH:mm:ss}");
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
            ServerLoadState.LoadFailed => "Laden fehlgeschlagen — jeder Tool-Call liefert PROJECT_LOAD_FAILED bis zur Neuanlage des Keys",
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
