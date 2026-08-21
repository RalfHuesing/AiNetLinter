#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AiNetLinter.Observability;
using ModelContextProtocol.Protocol;
using RalfHuesing.Mcp.Observability;

namespace AiNetLinter.Mcp.Tools.ServerMaintenance;

/// <summary>
/// MCP-Tool <c>get_server_health</c>: Diagnose-Schnappschuss des laufenden MCP-Server-Prozesses —
/// <see cref="ServerLoadState"/>, geladene Solution/Config-Quelle, Uptime, Anzahl
/// Solution-Refreshes seit Start, Observability-Status (Logging & Feedback-Kanal) und aktuelle
/// Call-Log-Aggregate.
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
        var callLogResult = isEnabled ? McpLogAnalyzer.TryAnalyze(effectiveLogPath ?? string.Empty) : null;
        var callLogPayload = BuildCallLogPayload(effectiveLogPath, callLogResult);

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
        sb.Append(DescribeObservability(isEnabled, effectiveLogPath, callLogPayload));

        var text = sb.ToString().TrimEnd();
        return Task.FromResult(McpToolResults.Text(text, BuildPayload(state, version, callLogPayload)));
    }

    /// <summary>
    /// StructuredContent mit denselben Rohwerten wie die Text-Sektionen oben — additiv,
    /// keine eigene Formatierungslogik (Text bleibt die Quelle der Wahrheit fuer Sonderfaelle
    /// wie "wird noch geladen").
    /// </summary>
    private static ServerHealthPayload BuildPayload(McpCodeGraphServer state, string version, CallLogPayload? callLogPayload)
    {
        var (_, usedDefaultConfig, resolvedConfigPath) = state.GetConfigSnapshot();

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

    private static CallLogPayload? BuildCallLogPayload(
        string? logPath,
        McpLogAnalysisResult? analysisResult)
    {
        if (logPath is null)
        {
            return null;
        }

        if (analysisResult?.Report is { } report)
        {
            return new CallLogPayload(
                LogPath: logPath,
                EntryCount: report.ToolCallCount,
                ErrorCount: report.ErrorResultCount,
                CallCountsByTool: report.CallsPerTool);
        }

        return new CallLogPayload(
            LogPath: logPath,
            EntryCount: 0,
            ErrorCount: 0,
            CallCountsByTool: new Dictionary<string, int>(),
            AnalysisError: analysisResult?.Error);
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

    private static string DescribeObservability(bool isEnabled, string? logPath, CallLogPayload? callLog)
    {
        if (!isEnabled)
        {
            return "Observability: deaktiviert.";
        }

        if (string.IsNullOrWhiteSpace(logPath))
        {
            return "Observability: aktiv (RalfHuesing.Mcp.Observability, Tool-Call Logging & Feedback-Kanal).";
        }

        var builder = new StringBuilder($"Observability: aktiv ({logPath})");
        if (callLog?.AnalysisError is { } error)
        {
            builder.Append($"\n- Call-Log-Auswertung: nicht verfuegbar ({error})");
            return builder.ToString();
        }

        builder.Append($"\n- Call-Log-Aggregate: {callLog?.EntryCount ?? 0} Eintraege, " +
            $"{callLog?.ErrorCount ?? 0} isError-Ergebnisse");
        builder.Append("\n- Calls pro Tool: ");
        builder.Append(callLog is null || callLog.CallCountsByTool.Count == 0
            ? "keine"
            : string.Join(", ", callLog.CallCountsByTool.Select(item => $"{item.Key}={item.Value}")));
        return builder.ToString();
    }
}
