#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ModelContextProtocol.Protocol;
using RalfHuesing.Mcp.Observability;

namespace AiNetLinter.Mcp.Tools.ServerMaintenance;

/// <summary>
/// MCP-Tool <c>get_server_health</c>: Diagnose-Schnappschuss des laufenden MCP-Server-Prozesses —
/// <see cref="ServerLoadState"/>, geladene Solution/Config-Quelle, Uptime, Anzahl
/// Solution-Refreshes seit Start und Observability-Status (Logging & Feedback-Kanal).
/// Reine Diagnose ohne Recoverable-Pfad; einzige Ausnahme
/// <see cref="ServerLoadState.LoadFailed"/>, konsistent mit den anderen Tools' SOLUTION_NOT_LOADED-
/// Kurzform.
/// </summary>
internal static class GetServerHealthTool
{
    internal static Task<CallToolResult> ExecuteAsync(
        McpCodeGraphServer state,
        string? observabilityLogPath = null)
    {
        return ExecuteAsync(state, observabilityService: null, observabilityLogPath: observabilityLogPath);
    }

    internal static Task<CallToolResult> ExecuteAsync(
        McpCodeGraphServer state,
        IMcpObservabilityService? observabilityService,
        string? observabilityLogPath = null)
    {
        if (state.LoadState == ServerLoadState.LoadFailed) return Task.FromResult(McpToolResults.SolutionNotLoaded());

        var effectiveLogPath = observabilityLogPath ?? observabilityService?.CurrentLogFilePath;
        var isEnabled = observabilityService is null || observabilityService.IsEnabled;
        var version = McpServerOptionsFactory.GetServerVersion();

        var sb = new StringBuilder();
        sb.AppendLine("# AiNetLinter MCP-Server — Health");
        sb.AppendLine();
        sb.AppendLine($"- Version: {version}");
        sb.AppendLine($"- LoadState: {state.LoadState}");
        sb.AppendLine($"- Solution: {DescribeSolution(state)}");
        sb.AppendLine($"- Config: {DescribeConfig(state)}");
        sb.AppendLine($"- Uptime: {FormatUptime(state.Uptime)}");
        sb.AppendLine($"- Solution-Refreshes seit Start: {state.RefreshCount}");
        sb.AppendLine();
        sb.Append(DescribeObservability(isEnabled, effectiveLogPath));

        var text = sb.ToString().TrimEnd();
        return Task.FromResult(McpToolResults.Text(text, BuildPayload(state, version, effectiveLogPath)));
    }

    /// <summary>
    /// StructuredContent mit denselben Rohwerten wie die Text-Sektionen oben — additiv,
    /// keine eigene Formatierungslogik (Text bleibt die Quelle der Wahrheit fuer Sonderfaelle
    /// wie "wird noch geladen").
    /// </summary>
    private static ServerHealthPayload BuildPayload(McpCodeGraphServer state, string version, string? observabilityLogPath)
    {
        var (_, usedDefaultConfig, resolvedConfigPath) = state.GetConfigSnapshot();
        var callLogPayload = observabilityLogPath is null
            ? null
            : new CallLogPayload(observabilityLogPath, 0, 0, new Dictionary<string, int>());

        return new ServerHealthPayload(
            Version: version,
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

    private static string DescribeObservability(bool isEnabled, string? logPath)
    {
        if (!isEnabled)
        {
            return "Observability: deaktiviert.";
        }

        if (string.IsNullOrWhiteSpace(logPath))
        {
            return "Observability: aktiv (RalfHuesing.Mcp.Observability, Tool-Call Logging & Feedback-Kanal).";
        }
        return $"Observability: aktiv ({logPath})";
    }
}
