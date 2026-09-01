#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AiNetLinter.Mcp.Daemon;
using AiNetLinter.Mcp.Projects;
using AiNetLinter.Mcp.Tools.AssemblyAnalysis;

namespace AiNetLinter.Mcp.Tools.ServerMaintenance;

/// <summary>
/// Formatiert die Server-Health-Ausgabe in Markdown.
/// Ausgelagert zur Einhaltung des AIContextFootprint-Limits in <see cref="GetServerHealthResponseBuilder"/>.
/// </summary>
internal static class GetServerHealthFormatter
{
    internal static void AppendDaemonSection(StringBuilder builder, DaemonHealthPayload? daemon)
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

    internal static void AppendProjectSection(StringBuilder builder, ProjectSnapshot snapshot)
    {
        var server = snapshot.Server;
        builder.AppendLine($"### {snapshot.RootPath}");
        builder.AppendLine($"- LoadState: {server.LoadState}");
        builder.AppendLine($"- Solution: {(server.LoadState == ServerLoadState.Loading ? "wird noch geladen" : server.GetCurrentSolution()?.FilePath ?? "unbekannt")}");
        var (_, usedDefaultConfig, resolvedConfigPath) = server.GetConfigSnapshot();
        builder.AppendLine($"- Config: {(usedDefaultConfig ? "Default-Regeln" : resolvedConfigPath ?? "unbekannt")}");
        builder.AppendLine($"- Zuletzt genutzt (UTC): {FormatTimestamp(snapshot.LastUsedUtc)}");
        builder.AppendLine($"- Uptime: {FormatUptime(server.Uptime)}");
        builder.AppendLine($"- Solution-Refreshes seit Start: {server.RefreshCount}");
        AppendStalenessSection(builder, server.LastStalenessStats);
        if (server.LastGoodStateUtc is { } lastGoodState)
        {
            builder.AppendLine($"- Letzter guter Zustand (UTC): {FormatTimestamp(lastGoodState)}");
        }
        if (server.LastLoadError is { } lastLoadError)
        {
            builder.AppendLine($"- Letzter Ladefehler: {lastLoadError}");
        }
        builder.AppendLine();
    }

    internal static void AppendAssemblySection(StringBuilder builder, AssemblyHealthEntry assembly)
    {
        AppendAssemblyHeader(builder, assembly);
        AppendAssemblySourceDetails(builder, assembly);
        AppendAssemblyDiagnostics(builder, assembly);
        builder.AppendLine();
    }

    internal static void AppendAssemblyAggregate(
        StringBuilder builder,
        int totalSessions,
        IReadOnlyDictionary<string, int> statusCounts,
        int diagnosticCount)
    {
        builder.AppendLine("- Sessiondetails unterdrückt; includeSessions=true für die Liste");
        builder.AppendLine($"- Sessions gesamt: {totalSessions}");
        builder.AppendLine($"- Statusverteilung: {string.Join(", ", statusCounts.Select(pair => $"{pair.Key}={pair.Value}"))}");
        builder.AppendLine($"- Diagnosen gesamt: {diagnosticCount}");
    }

    private static void AppendAssemblyHeader(StringBuilder builder, AssemblyHealthEntry assembly)
    {
        builder.AppendLine($"### {assembly.TargetPath}");
        builder.AppendLine($"- LoadState: {assembly.LoadState}");
        if (!string.IsNullOrWhiteSpace(assembly.Completeness))
        {
            builder.AppendLine($"- Vollständigkeit: {assembly.Completeness}");
        }
        builder.AppendLine($"- Origin: {assembly.OriginKind ?? "unbekannt"}");
        builder.AppendLine($"- Generation: {assembly.Generation?.ToString() ?? "unbekannt"}");
    }

    private static void AppendAssemblySourceDetails(StringBuilder builder, AssemblyHealthEntry assembly)
    {
        if (!string.IsNullOrWhiteSpace(assembly.SourceProjectPath))
        {
            builder.AppendLine($"- Source-Projekt: {assembly.SourceProjectPath}");
        }

        if (assembly.SourceSnapshot is { } snapshot)
        {
            builder.AppendLine($"- Source-Snapshot: {snapshot.RepositoryUrl} @ {snapshot.LoadedRevision} — Solution {snapshot.SolutionPath}");
        }

        AppendOptionalAssemblyValue(builder, "Hash", assembly.ContentHash);
        AppendOptionalAssemblyValue(builder, "GeneratedPath", assembly.GeneratedDocumentPath);
        AppendOptionalAssemblyValue(builder, "Confidence", assembly.Confidence);
        AppendOptionalAssemblyValue(builder, "Trust", assembly.Trust);
    }

    private static void AppendOptionalAssemblyValue(
        StringBuilder builder,
        string label,
        string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            builder.AppendLine($"- {label}: {value}");
        }
    }

    private static void AppendAssemblyDiagnostics(StringBuilder builder, AssemblyHealthEntry assembly)
    {
        if (assembly.DiagnosticsSummary is not { } summary || summary.TotalCount <= 0)
        {
            return;
        }

        builder.AppendLine($"- Diagnosen: {summary.ShownCount} von {summary.TotalCount}{(summary.Truncated ? " (gekürzt)" : string.Empty)}");
        if (assembly.Diagnostics is not { Count: > 0 })
        {
            return;
        }

        foreach (var diagnostic in assembly.Diagnostics)
        {
            builder.AppendLine($"  - {diagnostic}");
        }
    }

    private static void AppendStalenessSection(StringBuilder builder, ServerStalenessStats staleness)
    {
        builder.AppendLine($"- Staleness-Checks seit Start: {staleness.CheckCount} (kumuliert {staleness.TotalMilliseconds:F0} ms)");
        if (staleness.LastWarning is { } warning)
        {
            builder.AppendLine($"- Staleness-Warnungen (letzter Lauf): {staleness.WarningCount}, zuletzt: {warning}");
        }
    }

    private static string FormatTimestamp(DateTime utc) => utc.ToString("yyyy-MM-dd HH:mm:ss");

    private static string FormatUptime(TimeSpan uptime)
    {
        if (uptime.TotalHours >= 1) return $"{(int)uptime.TotalHours}h {uptime.Minutes}min";
        return uptime.TotalMinutes >= 1 ? $"{(int)uptime.TotalMinutes}min {uptime.Seconds}s" : $"{uptime.Seconds}s";
    }
}
