#nullable enable

using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Tools;
using AiNetLinter.Mcp.Tools.ServerMaintenance;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace AiNetLinter.Mcp;

/// <summary>
/// Registriert die server-eigenen Wartungs-/Diagnose-Tools (<c>reload_config</c>,
/// <c>get_server_health</c>) an der von <see cref="McpServerOptionsFactory"/> aufgebauten
/// Tool-Collection. Eigene Registrar-Klasse statt Anhaengen an eine bestehende Gruppe, weil beide
/// Tools semantisch den Server-Prozess selbst betreffen (Config-Reload, Health-Snapshot) statt die
/// Solution/den Symbolgraph zu befragen — passt zu keiner der bestehenden Gruppen (Symbolgraph,
/// Dateistruktur, Analyse, Symbol-Body).
/// </summary>
internal static class ServerMaintenanceToolRegistrations
{
    /// <summary>
    /// Fuegt <paramref name="tools"/> die beiden Wartungs-Tools hinzu. Tools erreichen den resident
    /// gehaltenen <paramref name="mcpState"/> per Delegate-Closure - kein DI-Container (siehe
    /// <c>AiNetLinterRichtlinien.mdc</c> §2).
    /// </summary>
    internal static void Register(
        McpServerPrimitiveCollection<McpServerTool> tools,
        McpCodeGraphServer mcpState,
        IServiceProvider? serviceProvider = null)
    {
        AddReloadConfig(tools, mcpState);
        AddGetServerHealth(tools, mcpState);
        AddReportObservabilityFeedback(tools, serviceProvider);
    }

    private static void AddReloadConfig(
        McpServerPrimitiveCollection<McpServerTool> tools,
        McpCodeGraphServer mcpState)
    {
        tools.Add(McpServerTool.Create(
            (string? configPath = null, CancellationToken ct = default) =>
                ReloadConfigTool.ExecuteAsync(mcpState, configPath, ct),
            new McpServerToolCreateOptions
            {
                Name = "reload_config",
                Description = ReloadConfigDescription,
            }));
    }

    private const string ReloadConfigDescription =
        "Wann nutzen: rules.json wurde waehrend des Server-Laufs geaendert und get_violations " +
        "soll die neuen Regeln sofort respektieren, ohne den Server neu zu starten. Ohne " +
        "configPath wird der zuletzt geladene Pfad erneut gelesen (bzw. bei Default-Regeln neu " +
        "neben der Solution gesucht). Ungueltiger Pfad/JSON: bisherige Config bleibt aktiv.";

    private static void AddGetServerHealth(
        McpServerPrimitiveCollection<McpServerTool> tools,
        McpCodeGraphServer mcpState)
    {
        tools.Add(McpServerTool.Create(
            (CancellationToken ct = default) =>
                GetServerHealthTool.ExecuteAsync(mcpState),
            new McpServerToolCreateOptions
            {
                Name = "get_server_health",
                Description = GetServerHealthDescription,
            }));
    }

    private const string GetServerHealthDescription =
        "Wann nutzen: pruefen, ob der Server ueberhaupt laeuft, welche Solution/Config aktiv " +
        "ist, wie lange der Prozess schon laeuft, wie oft die Solution seit Start refresht wurde, " +
        "und ob Observability (Logging/Feedback) aktiv ist.";

    private static void AddReportObservabilityFeedback(
        McpServerPrimitiveCollection<McpServerTool> tools,
        IServiceProvider? serviceProvider)
    {
        tools.Add(McpServerTool.Create(
            (string feedbackType, string title, string description, string? relatedTool = null,
                string severity = "medium", string? expectedBehavior = null, string? actualBehavior = null,
                string? additionalContext = null, CancellationToken ct = default) =>
                ReportObservabilityFeedbackTool.ExecuteAsync(serviceProvider, feedbackType, title, description, relatedTool, severity, expectedBehavior, actualBehavior, additionalContext, ct),
            new McpServerToolCreateOptions
            {
                Name = "report_observability_feedback",
                Description = ReportObservabilityFeedbackDescription,
            }));
    }

    private const string ReportObservabilityFeedbackDescription =
        "Wann nutzen: Ein Problem, unerwartete Ausgaben, Falsch-Positive bei Lint-Regeln oder einen Feature-Wunsch " +
        "zu diesem MCP-Server melden, um AiNetLinter kontinuierlich zu verbessern. feedbackType ('issue' | 'feature_request'), " +
        "title (Kurztitel), description (ausfuehrliche Beschreibung), relatedTool (optional, Name des betroffenen Tools), " +
        "severity (optional, 'low', 'medium', 'high', 'critical'), expectedBehavior (optional), actualBehavior (optional), " +
        "additionalContext (optional).";
}
