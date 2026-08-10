#nullable enable

using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Tools;
using AiNetLinter.Mcp.Tools.ServerMaintenance;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace AiNetLinter.Mcp;

/// <summary>
/// Registriert die server-eigenen Wartungs-/Diagnose-Tools (<c>reload_config</c>, Q2;
/// <c>get_server_health</c>, Q3 — <c>tasks/features/05-roadmap.md</c> §3) an der von
/// <see cref="McpServerOptionsFactory"/> aufgebauten Tool-Collection. Eigene Registrar-Klasse statt
/// Anhaengen an eine bestehende Gruppe, weil beide Tools semantisch den Server-Prozess selbst
/// betreffen (Config-Reload, Health-Snapshot) statt die Solution/den Symbolgraph zu befragen —
/// passt zu keiner der bestehenden Gruppen (Symbolgraph, Dateistruktur, Analyse, Symbol-Body).
/// </summary>
internal static class ServerMaintenanceToolRegistrations
{
    /// <summary>
    /// Fuegt <paramref name="tools"/> die beiden Wartungs-Tools hinzu. Tools erreichen den resident
    /// gehaltenen <paramref name="mcpState"/> per Delegate-Closure - kein DI-Container (siehe
    /// <c>AiNetLinterRichtlinien.mdc</c> §2). Optionaler <paramref name="callLog"/> zeichnet jeden
    /// Tool-Aufruf auf, wenn aktiv, und ist gleichzeitig die Datenquelle fuer
    /// <c>get_server_health</c>s Call-Log-Aggregation.
    /// </summary>
    internal static void Register(
        McpServerPrimitiveCollection<McpServerTool> tools,
        McpCodeGraphServer mcpState,
        McpCallLog? callLog = null)
    {
        AddReloadConfig(tools, mcpState, callLog);
        AddGetServerHealth(tools, mcpState, callLog);
    }

    private static void AddReloadConfig(
        McpServerPrimitiveCollection<McpServerTool> tools,
        McpCodeGraphServer mcpState,
        McpCallLog? callLog)
    {
        tools.Add(McpServerTool.Create(
            async (string? configPath = null, CancellationToken ct = default) =>
            {
                if (callLog is null)
                {
                    return await ReloadConfigTool.ExecuteAsync(mcpState, configPath, ct);
                }
                return await callLog.ExecuteCallAsync("reload_config", configPath ?? "",
                    () => ReloadConfigTool.ExecuteAsync(mcpState, configPath, ct));
            },
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
        McpCodeGraphServer mcpState,
        McpCallLog? callLog)
    {
        tools.Add(McpServerTool.Create(
            async (CancellationToken ct = default) =>
            {
                if (callLog is null)
                {
                    return await GetServerHealthTool.ExecuteAsync(mcpState, callLog);
                }
                return await callLog.ExecuteCallAsync("get_server_health", "",
                    () => GetServerHealthTool.ExecuteAsync(mcpState, callLog));
            },
            new McpServerToolCreateOptions
            {
                Name = "get_server_health",
                Description = GetServerHealthDescription,
            }));
    }

    private const string GetServerHealthDescription =
        "Wann nutzen: pruefen, ob der Server ueberhaupt laeuft, welche Solution/Config aktiv " +
        "ist, wie lange der Prozess schon laeuft, wie oft die Solution seit Start refresht wurde, " +
        "und (falls --mcp-log aktiv) Anzahl Calls/Fehler pro Tool in dieser Session.";
}
