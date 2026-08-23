#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Projects;
using AiNetLinter.Observability;
using ModelContextProtocol.Protocol;
using RalfHuesing.Mcp.Observability;

namespace AiNetLinter.Mcp.Tools.ServerMaintenance;

/// <summary>
/// MCP-Tool <c>get_server_health</c>: Diagnose-Schnappschuss der residenten Projekt-Keys —
/// pro Key <see cref="ServerLoadState"/>, Solution-/Config-Quelle, LastUsedUtc, Uptime,
/// Refresh-Zaehler, Staleness-Aggregate und die Health-Felder des zweistufigen
/// Zustandsvertrags (<c>LastGoodStateUtc</c>/<c>LastLoadError</c>); dazu der prozessweite
/// Observability-Teil (Logging &amp; Feedback-Kanal) mit Call-Log-Aggregaten.
/// Einzige Pflicht-Ausnahme vom projectRoot-Kontrakt: Ohne Filter wird ueber ALLE Keys
/// aggregiert; mit Filter antwortet nur dieser Key (Guards wie bei allen Tools). Reine
/// Diagnose ohne Recoverable-Pfad.
/// </summary>
internal static class GetServerHealthTool
{
    internal static Task<CallToolResult> ExecuteAsync(
        ProjectRegistry registry,
        IMcpObservabilityService? observabilityService = null,
        string? projectRoot = null,
        string? observabilityLogPath = null)
    {
        IReadOnlyList<ProjectSnapshot> snapshots;
        if (projectRoot is not null)
        {
            var guard = ProjectToolCall.GuardRequiredAbsoluteRoot(projectRoot);
            if (guard is not null)
            {
                return Task.FromResult(McpToolResults.Error(guard.Code, guard.Message, hint: guard.Hint));
            }

            var snapshot = registry.FindSnapshot(projectRoot);
            if (snapshot is null) return Task.FromResult(ProjectNotInitialized(projectRoot));
            snapshots = [snapshot];
        }
        else
        {
            snapshots = registry.Snapshots();
        }

        return Task.FromResult(BuildResult(snapshots, observabilityService, observabilityLogPath));
    }

    private static CallToolResult BuildResult(
        IReadOnlyList<ProjectSnapshot> snapshots,
        IMcpObservabilityService? observabilityService,
        string? observabilityLogPath)
    {
        var isEnabled = observabilityService is null || observabilityService.IsEnabled;
        var effectiveLogPath = observabilityLogPath ?? observabilityService?.CurrentLogFilePath;
        var callLogResult = isEnabled ? McpLogAnalyzer.TryAnalyze(effectiveLogPath ?? string.Empty) : null;
        var callLogPayload = BuildCallLogPayload(effectiveLogPath, callLogResult);

        var sb = new StringBuilder();
        sb.AppendLine("# AiNetLinter MCP-Server — Health");
        sb.AppendLine();
        sb.AppendLine($"- Version: {McpServerOptionsFactory.GetServerVersion()}");
        sb.AppendLine();
        sb.AppendLine($"## Projekte ({snapshots.Count})");
        sb.AppendLine();
        foreach (var snapshot in snapshots)
        {
            AppendProjectSection(sb, snapshot);
        }

        sb.Append(DescribeObservability(isEnabled, effectiveLogPath, callLogPayload));
        var entries = snapshots.Select(ToEntry).ToList();
        var payload = new ServerHealthAggregatePayload(
            Version: McpServerOptionsFactory.GetServerVersion(),
            Projects: entries,
            CallLog: callLogPayload);
        return McpToolResults.Text(sb.ToString().TrimEnd(), payload);
    }

    private static void AppendProjectSection(StringBuilder sb, ProjectSnapshot snapshot)
    {
        var server = snapshot.Server;
        sb.AppendLine($"### {snapshot.RootPath}");
        sb.AppendLine($"- LoadState: {server.LoadState}");
        sb.AppendLine($"- Solution: {(server.LoadState == ServerLoadState.Loading ? "wird noch geladen" : server.GetCurrentSolution()?.FilePath ?? "unbekannt")}");
        var (_, usedDefaultConfig, resolvedConfigPath) = server.GetConfigSnapshot();
        sb.AppendLine($"- Config: {(usedDefaultConfig ? "Default-Regeln" : resolvedConfigPath ?? "unbekannt")}");
        sb.AppendLine($"- Zuletzt genutzt (UTC): {FormatTimestamp(snapshot.LastUsedUtc)}");
        sb.AppendLine($"- Uptime: {FormatUptime(server.Uptime)}");
        sb.AppendLine($"- Solution-Refreshes seit Start: {server.RefreshCount}");
        AppendStalenessSection(sb, server.LastStalenessStats);
        if (server.LastGoodStateUtc is { } lastGoodState)
        {
            sb.AppendLine($"- Letzter guter Zustand (UTC): {FormatTimestamp(lastGoodState)}");
        }
        if (server.LastLoadError is { } lastLoadError)
        {
            sb.AppendLine($"- Letzter Ladefehler: {lastLoadError}");
        }
        sb.AppendLine();
    }

    private static ProjectHealthEntry ToEntry(ProjectSnapshot snapshot)
    {
        var server = snapshot.Server;
        var (_, usedDefaultConfig, resolvedConfigPath) = server.GetConfigSnapshot();
        var staleness = server.LastStalenessStats;
        return new ProjectHealthEntry(
            ProjectRoot: snapshot.RootPath,
            LoadState: server.LoadState.ToString(),
            SolutionPath: server.LoadState == ServerLoadState.Loading ? null : server.GetCurrentSolution()?.FilePath,
            UsedDefaultConfig: usedDefaultConfig,
            ConfigPath: usedDefaultConfig ? null : resolvedConfigPath,
            LastUsedUtc: snapshot.LastUsedUtc,
            UptimeSeconds: server.Uptime.TotalSeconds,
            RefreshCount: server.RefreshCount,
            StalenessCheckCount: staleness.CheckCount,
            StalenessCheckDurationMs: staleness.TotalMilliseconds,
            StalenessWarningCount: staleness.WarningCount,
            LastStalenessWarning: staleness.LastWarning,
            LastGoodStateUtc: server.LastGoodStateUtc,
            LastLoadError: server.LastLoadError);
    }

    /// <summary>Strukturierter Fehler fuer einen adressierten, aber nicht residenten Key.</summary>
    private static CallToolResult ProjectNotInitialized(string projectRoot) =>
        McpToolResults.Error(
            ProjectErrorCodes.ProjectNotInitialized,
            $"Fuer '{projectRoot}' existiert kein residenter Projekt-Key.",
            context: projectRoot,
            hint: "Ersten Tool-Aufruf mit diesem projectRoot senden; der Server legt den Key lazy " +
                  "ueber eine Definitionsdatei ainetlinter.project.json im Projektroot an.");

    private static void AppendStalenessSection(StringBuilder sb, ServerStalenessStats staleness)
    {
        sb.AppendLine($"- Staleness-Checks seit Start: {staleness.CheckCount} (kumuliert {staleness.TotalMilliseconds:F0} ms)");
        if (staleness.LastWarning is { } warning)
        {
            sb.AppendLine($"- Staleness-Warnungen (letzter Lauf): {staleness.WarningCount}, zuletzt: {warning}");
        }
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

    private static string FormatTimestamp(DateTime utc) => utc.ToString("yyyy-MM-dd HH:mm:ss");

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
