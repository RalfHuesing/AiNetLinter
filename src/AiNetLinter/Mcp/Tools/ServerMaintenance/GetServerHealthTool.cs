#nullable enable

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Daemon;
using AiNetLinter.Mcp.Projects;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Tools.ServerMaintenance;

internal sealed record GetServerHealthOptions(
    string? ProjectRoot = null,
    DaemonRuntimeContext? RuntimeContext = null);

/// <summary>
/// MCP-Tool <c>get_server_health</c>: Diagnose-Schnappschuss der residenten Projekt-Keys —
/// pro Key <see cref="ServerLoadState"/>, Solution-/Config-Quelle, LastUsedUtc, Uptime,
/// Refresh-Zaehler, Staleness-Aggregate und die Health-Felder des zweistufigen
/// Zustandsvertrags (<c>LastGoodStateUtc</c>/<c>LastLoadError</c>) und optional die
/// Laufzeitdaten des Daemons.
/// Einzige Pflicht-Ausnahme vom projectRoot-Kontrakt: Ohne Filter wird ueber ALLE Keys
/// aggregiert; mit Filter antwortet nur dieser Key (Guards wie bei allen Tools). Reine
/// Diagnose ohne Recoverable-Pfad.
/// </summary>
internal static class GetServerHealthTool
{
    internal static Task<CallToolResult> ExecuteAsync(
        ProjectRegistry registry,
        string? projectRoot = null,
        DaemonRuntimeContext? runtimeContext = null) =>
        ExecuteAsync(registry, new GetServerHealthOptions(projectRoot, runtimeContext));

    internal static Task<CallToolResult> ExecuteAsync(
        ProjectRegistry registry,
        GetServerHealthOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.ProjectRoot is not null)
        {
            var guard = ProjectToolCall.GuardRequiredAbsoluteRoot(options.ProjectRoot);
            if (guard is not null)
            {
                return Task.FromResult(McpToolResults.Error(guard.Code, guard.Message, hint: guard.Hint));
            }

            var snapshot = registry.FindSnapshot(options.ProjectRoot);
            if (snapshot is null) return Task.FromResult(ProjectNotInitialized(options.ProjectRoot));
            return Task.FromResult(BuildResult([snapshot], options));
        }

        return Task.FromResult(BuildResult(registry.Snapshots(), options));
    }

    private static CallToolResult BuildResult(
        IReadOnlyList<ProjectSnapshot> snapshots,
        GetServerHealthOptions options)
    {
        var runtimeContext = options.RuntimeContext;

        var sb = new StringBuilder();
        sb.AppendLine("# AiNetLinter MCP-Server — Health");
        sb.AppendLine();
        sb.AppendLine($"- Version: {McpServerOptionsFactory.GetServerVersion()}");
        var daemonPayload = runtimeContext is null ? null : CreateDaemonPayload(runtimeContext);
        AppendDaemonSection(sb, daemonPayload);
        sb.AppendLine();
        sb.AppendLine($"## Projekte ({snapshots.Count})");
        sb.AppendLine();
        foreach (var snapshot in snapshots)
        {
            AppendProjectSection(sb, snapshot);
        }

        var entries = snapshots.Select(ToEntry).ToList();
        var payload = new ServerHealthAggregatePayload(
            Version: McpServerOptionsFactory.GetServerVersion(),
            Projects: entries,
            Daemon: daemonPayload);
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

    private static string FormatTimestamp(DateTime utc) => utc.ToString("yyyy-MM-dd HH:mm:ss");

    private static string FormatUptime(TimeSpan uptime)
    {
        if (uptime.TotalHours >= 1) return $"{(int)uptime.TotalHours}h {uptime.Minutes}min";
        return uptime.TotalMinutes >= 1 ? $"{(int)uptime.TotalMinutes}min {uptime.Seconds}s" : $"{uptime.Seconds}s";
    }

    private static DaemonHealthPayload CreateDaemonPayload(DaemonRuntimeContext context)
    {
        var snapshot = context.Snapshot;
        return new DaemonHealthPayload(
            context.Mode,
            context.ConnectionId,
            snapshot.Connections,
            snapshot.ProcessId,
            snapshot.Uptime.TotalSeconds,
            snapshot.Keys,
            snapshot.DaemonVersion);
    }

    private static void AppendDaemonSection(StringBuilder builder, DaemonHealthPayload? daemon)
    {
        if (daemon is null) return;
        builder.AppendLine($"- Mode: {daemon.Mode}");
        builder.AppendLine($"- Connections: {daemon.Connections}");
        builder.AppendLine($"- PID: {daemon.ProcessId}");
        builder.AppendLine($"- Daemon-Uptime: {FormatUptime(TimeSpan.FromSeconds(daemon.UptimeSeconds))}");
        builder.AppendLine($"- Daemon-Keys: {(daemon.Keys.Count == 0 ? "keine" : string.Join(", ", daemon.Keys))}");
        builder.AppendLine($"- Daemon-Version: {daemon.DaemonVersion}");
        builder.AppendLine($"- connectionId: {daemon.ConnectionId}");
        builder.AppendLine();
    }
}
