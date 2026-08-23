#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Projects;
using AiNetLinter.Mcp.Tools;
using AiNetLinter.Mcp.Tools.ServerMaintenance;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;
using RalfHuesing.Mcp.Observability;

namespace AiNetLinter.Mcp;

/// <summary>
/// Registriert die server-eigenen Wartungs-/Diagnose-Tools (<c>reload_config</c>,
/// <c>get_server_health</c>, <c>report_observability_feedback</c>) an der von <see cref="McpServerOptionsFactory"/> aufgebauten
/// Tool-Collection. Eigene Registrar-Klasse statt Anhaengen an eine bestehende Gruppe, weil diese
/// Tools semantisch den Server-Prozess selbst betreffen (Config-Reload, Health-Snapshot, Feedback) statt die
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
        IServiceProvider? serviceProvider = null)
    {
        AddReloadConfig(tools, registry);
        AddGetServerHealth(tools, registry, serviceProvider);
        AddReportObservabilityFeedback(tools, serviceProvider);
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
        IServiceProvider? serviceProvider)
    {
        var obsService = serviceProvider?.GetService<IMcpObservabilityService>();
        tools.Add(McpServerTool.Create(
            async (string? projectRoot = null, CancellationToken ct = default) =>
                await GetServerHealthTool.ExecuteAsync(registry, obsService, projectRoot),
            new McpServerToolCreateOptions
            {
                Name = "get_server_health",
                Description = GetServerHealthDescription,
            }));
    }

    private const string GetServerHealthDescription =
        "Wann nutzen: pruefen, ob der Server laeuft und welche Projekte resident sind. Ohne " +
        "projectRoot: ein Abschnitt pro geladenem Key (Root, Solution, rules.json, LastUsedUtc, " +
        "LoadState, RefreshCount, Staleness, Uptime, LastGoodStateUtc/LastLoadError) plus " +
        "prozessweiter Observability-Teil. Mit projectRoot: nur dieser Key (absoluter Pfad, " +
        "Pflichtformat wie bei allen Tools).";

    private static void AddReportObservabilityFeedback(
        McpServerPrimitiveCollection<McpServerTool> tools,
        IServiceProvider? serviceProvider)
    {
        tools.AddFeedbackTool(serviceProvider);
    }
}
