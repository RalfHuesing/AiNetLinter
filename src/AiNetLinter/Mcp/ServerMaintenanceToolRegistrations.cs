#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Projects;
using AiNetLinter.Mcp.Tools;
using AiNetLinter.Mcp.Tools.ServerMaintenance;
using ModelContextProtocol.Server;

namespace AiNetLinter.Mcp;

/// <summary>
/// Registriert die server-eigenen Wartungs-/Diagnose-Tools (<c>reload_config</c>,
/// <c>get_server_health</c>) an der von <see cref="McpServerOptionsFactory"/> aufgebauten
/// Tool-Collection. Eigene Registrar-Klasse statt Anhaengen an eine bestehende Gruppe, weil diese
/// Tools semantisch den Server-Prozess selbst betreffen (Config-Reload, Health-Snapshot) statt die
/// Solution/den Symbolgraph zu befragen — passt zu keiner der bestehenden Gruppen (Symbolgraph,
/// Dateistruktur, Analyse, Symbol-Body).
/// </summary>
internal static class ServerMaintenanceToolRegistrations
{
    /// <summary>
    /// Fuegt <paramref name="tools"/> die Wartungs-Tools hinzu. Tools erreichen die residente
    /// Instanz ihres Keys per Lease-Closure - kein DI-Container (siehe
    /// <c>AiNetLinterRichtlinien.mdc</c> §2). Einzige Pflicht-Ausnahme: <c>get_server_health</c>
    /// nimmt <c>projectRoot</c> optional (Filter) und aggregiert ohne Filter ueber alle Keys.
    /// </summary>
    internal static void Register(
        McpServerPrimitiveCollection<McpServerTool> tools,
        ProjectRegistry registry,
        Daemon.DaemonRuntimeContext? runtimeContext = null)
    {
        AddReloadConfig(tools, registry);
        AddGetServerHealth(tools, registry, runtimeContext);
        AddReportObservabilityFeedback(tools);
    }

    private static void AddReloadConfig(
        McpServerPrimitiveCollection<McpServerTool> tools,
        ProjectRegistry registry)
    {
        tools.Add(McpServerTool.Create(
            async (string projectRoot, string? configPath = null, CancellationToken ct = default) =>
                await ProjectToolCall.ExecuteAsync(
                    registry,
                    projectRoot,
                    lease => ReloadConfigTool.ExecuteAsync(lease.Server, lease.Definition.RulesPath, configPath, ct)),
            new McpServerToolCreateOptions
            {
                Name = "reload_config",
                Description = ReloadConfigDescription,
            }));
    }

    private const string ReloadConfigDescription =
        "Wann nutzen: rules.json wurde waehrend des Server-Laufs geaendert und get_violations " +
        "soll die neuen Regeln sofort respektieren, ohne den Server neu zu starten. Ohne " +
        "configPath wird der rules-Pfad aus der Definitionsdatei (ainetlinter.project.json) des " +
        "adressierten Projekts neu eingelesen; mit configPath gilt der Pfad als Override fuer " +
        "genau diesen Key. Ungueltiger Pfad/JSON: bisherige Config bleibt aktiv.";

    private static void AddGetServerHealth(
        McpServerPrimitiveCollection<McpServerTool> tools,
        ProjectRegistry registry,
        Daemon.DaemonRuntimeContext? runtimeContext)
    {
        tools.Add(McpServerTool.Create(
            async (string? projectRoot = null, CancellationToken ct = default) =>
                await GetServerHealthTool.ExecuteAsync(
                    registry,
                    new GetServerHealthOptions(projectRoot, runtimeContext)),
            new McpServerToolCreateOptions
            {
                Name = "get_server_health",
                Description = GetServerHealthDescription,
            }));
    }

    private const string GetServerHealthDescription =
        "Wann nutzen: pruefen, ob der Server laeuft und welche Projekte resident sind. Ohne " +
        "projectRoot: ein Abschnitt pro geladenem Key (Root, Solution, rules.json, LastUsedUtc, " +
        "LoadState, RefreshCount, Staleness, Uptime, LastGoodStateUtc/LastLoadError). Mit " +
        "projectRoot: nur dieser Key (absoluter Pfad, " +
        "Pflichtformat wie bei allen Tools).";

    private static void AddReportObservabilityFeedback(McpServerPrimitiveCollection<McpServerTool> tools)
    {
        tools.Add(McpServerTool.Create(
            (string feedbackType,
             string title,
             string description,
             string? relatedTool = null,
             string? severity = "medium",
             string? expectedBehavior = null,
             string? actualBehavior = null,
             string? additionalContext = null,
             string? projectRoot = null,
             CancellationToken ct = default) =>
                ReportObservabilityFeedbackTool.ExecuteAsync(
                    new ReportObservabilityFeedbackParameters(
                        feedbackType,
                        title,
                        description,
                        relatedTool,
                        severity,
                        expectedBehavior,
                        actualBehavior,
                        additionalContext,
                        projectRoot)),
            new McpServerToolCreateOptions
            {
                Name = "report_observability_feedback",
                Description = ReportObservabilityFeedbackDescription,
            }));
    }

    private const string ReportObservabilityFeedbackDescription =
        "Wann nutzen: Ein MCP-Tool meldet einen unerwarteten internen Fehler, liefert verwirrende " +
        "Ausgaben, einen False Positive oder ein Feature fehlt. NICHT nutzen fuer normale " +
        "Leermengen (z. B. Symbol/Datei existiert im Code nicht). Protokolliert das Feedback direkt " +
        "in das System-Log zur Auswertung. Nach dem Absenden mit dem besten verfuegbaren Workaround fortfahren.";
}
