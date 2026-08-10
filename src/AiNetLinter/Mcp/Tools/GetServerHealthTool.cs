#nullable enable

using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Tools;

/// <summary>
/// MCP-Tool <c>get_server_health</c> (Q3, <c>tasks/features/05-roadmap.md</c> §3): Diagnose-
/// Schnappschuss des laufenden MCP-Server-Prozesses — <see cref="ServerLoadState"/>, geladene
/// Solution/Config-Quelle, Uptime, Anzahl Solution-Refreshes seit Start und (falls <c>--mcp-log</c>
/// aktiv) eine Call-Log-Aggregation. Reine Diagnose ohne Recoverable-Pfad; einzige Ausnahme
/// <see cref="ServerLoadState.LoadFailed"/>, konsistent mit den anderen Tools' SOLUTION_NOT_LOADED-
/// Kurzform. Schliesst Recon-B-Schwaeche #10 (kein Server-Health-Tool).
/// </summary>
internal static class GetServerHealthTool
{
    internal static Task<CallToolResult> ExecuteAsync(McpCodeGraphServer state, McpCallLog? callLog)
    {
        if (state.LoadState == ServerLoadState.LoadFailed) return Task.FromResult(McpToolResults.SolutionNotLoaded());

        var sb = new StringBuilder();
        sb.AppendLine("# AiNetLinter MCP-Server — Health");
        sb.AppendLine();
        sb.AppendLine($"- LoadState: {state.LoadState}");
        sb.AppendLine($"- Solution: {DescribeSolution(state)}");
        sb.AppendLine($"- Config: {DescribeConfig(state)}");
        sb.AppendLine($"- Uptime: {FormatUptime(state.Uptime)}");
        sb.AppendLine($"- Solution-Refreshes seit Start: {state.RefreshCount}");
        sb.AppendLine();
        sb.Append(DescribeCallLog(callLog));

        var text = sb.ToString().TrimEnd();
        return Task.FromResult(McpToolResults.Text(text, BuildPayload(state, callLog)));
    }

    /// <summary>
    /// StructuredContent (S1.3) mit denselben Rohwerten wie die Text-Sektionen oben — additiv,
    /// keine eigene Formatierungslogik (Text bleibt die bestehende Quelle der Wahrheit fuer
    /// Sonderfaelle wie "wird noch geladen").
    /// </summary>
    private static ServerHealthPayload BuildPayload(McpCodeGraphServer state, McpCallLog? callLog)
    {
        var (_, usedDefaultConfig, resolvedConfigPath) = state.GetConfigSnapshot();
        var callLogPayload = callLog is null
            ? null
            : new CallLogPayload(callLog.LogPath, callLog.EntryCount, callLog.ErrorCount, callLog.CallCountsByTool);

        return new ServerHealthPayload(
            LoadState: state.LoadState.ToString(),
            SolutionPath: state.LoadState == ServerLoadState.Loading ? null : state.GetCurrentSolution()?.FilePath,
            UsedDefaultConfig: usedDefaultConfig,
            ConfigPath: usedDefaultConfig ? null : resolvedConfigPath,
            UptimeSeconds: state.Uptime.TotalSeconds,
            RefreshCount: state.RefreshCount,
            CallLog: callLogPayload);
    }

    private static string DescribeSolution(McpCodeGraphServer state)
    {
        return state.LoadState == ServerLoadState.Loading
            ? "wird noch geladen"
            : state.GetCurrentSolution()?.FilePath ?? "unbekannt";
    }

    private static string DescribeConfig(McpCodeGraphServer state)
    {
        // Atomarer Schnappschuss statt zweier getrennter Property-Zugriffe: sonst koennte ein
        // gleichzeitiger reload_config-Aufruf eine zerrissene Kombination liefern (siehe
        // McpCodeGraphServer.GetConfigSnapshot).
        var (_, usedDefaultConfig, resolvedConfigPath) = state.GetConfigSnapshot();
        return usedDefaultConfig
            ? "keine rules.json gefunden — Default-Regeln"
            : resolvedConfigPath ?? "unbekannt";
    }

    private static string FormatUptime(TimeSpan uptime)
    {
        if (uptime.TotalHours >= 1) return $"{(int)uptime.TotalHours}h {uptime.Minutes}min";
        return uptime.TotalMinutes >= 1 ? $"{(int)uptime.TotalMinutes}min {uptime.Seconds}s" : $"{uptime.Seconds}s";
    }

    /// <summary>
    /// Call-Log ist deaktiviert (Default): klarer Hinweistext statt Fehler, damit ein Agent nicht
    /// annimmt, das Tool sei kaputt. Aktiv: Gesamt-Eintraege, Gesamt-Fehler, Pro-Tool-Aufrufzahlen
    /// absteigend sortiert.
    /// </summary>
    private static string DescribeCallLog(McpCallLog? callLog)
    {
        if (callLog is null)
        {
            return "Call-Log: nicht aktiv (--mcp-log wurde beim Start nicht gesetzt).";
        }

        var sb = new StringBuilder();
        sb.AppendLine($"Call-Log: aktiv ({callLog.LogPath})");
        sb.AppendLine($"- Eintraege gesamt: {callLog.EntryCount}, Fehler: {callLog.ErrorCount}");
        foreach (var (tool, count) in callLog.CallCountsByTool
                     .OrderByDescending(kv => kv.Value)
                     .ThenBy(kv => kv.Key, StringComparer.Ordinal))
        {
            sb.AppendLine($"  - {tool}: {count}");
        }
        return sb.ToString();
    }
}
