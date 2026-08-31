#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AiNetLinter.Mcp.Assemblies.Analysis;
using AiNetLinter.Mcp.Assemblies.Analysis.References;
using AiNetLinter.Mcp.Daemon;
using AiNetLinter.Mcp.Projects;
using AiNetLinter.Mcp.Tools.AssemblyAnalysis;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Tools.ServerMaintenance;

internal static class GetServerHealthResponseBuilder
{
    internal static CallToolResult Build(
        IReadOnlyList<ProjectSnapshot> snapshots,
        IReadOnlyList<AssemblyHealthEntry> assemblies,
        GetServerHealthOptions options)
    {
        var runtimeContext = options.RuntimeContext;
        var projectedAssemblies = assemblies
            .Select(assembly => ProjectAssemblyEntry(assembly, options))
            .ToList();
        var builder = new StringBuilder();
        builder.AppendLine("# AiNetLinter MCP-Server — Health");
        builder.AppendLine();
        builder.AppendLine($"- Version: {McpServerOptionsFactory.GetServerVersion()}");
        var daemonPayload = runtimeContext is null ? null : CreateDaemonPayload(runtimeContext);
        AppendDaemonSection(builder, daemonPayload);
        builder.AppendLine();
        builder.AppendLine($"## Projekte ({snapshots.Count})");
        builder.AppendLine();
        foreach (var snapshot in snapshots)
        {
            AppendProjectSection(builder, snapshot);
        }

        builder.AppendLine($"## Assembly-Sessions ({projectedAssemblies.Count})");
        builder.AppendLine();
        foreach (var assembly in projectedAssemblies)
        {
            AppendAssemblySection(builder, assembly);
        }

        var payload = new ServerHealthAggregatePayload(
            Version: McpServerOptionsFactory.GetServerVersion(),
            Projects: snapshots.Select(ToEntry).ToList(),
            Daemon: daemonPayload,
            Assemblies: projectedAssemblies,
            DiagnosticsIncluded: options.IncludeDiagnostics,
            DiagnosticLimit: AssemblyAnalysisResponseLimits.NormalizeDiagnosticLimit(options.MaxDiagnostics));
        return McpToolResults.Text(builder.ToString().TrimEnd(), payload);
    }

    internal static AssemblyHealthEntry ToAssemblyEntry(AssemblyAnalysisHealthSnapshot snapshot) =>
        new(
            snapshot.TargetPath,
            snapshot.LoadState,
            snapshot.OriginKind,
            snapshot.SourceProjectPath,
            snapshot.SourceSnapshot,
            snapshot.ContentHash,
            snapshot.GeneratedDocumentPath,
            snapshot.Confidence,
            snapshot.Trust,
            snapshot.Generation,
            snapshot.Diagnostics);

    internal static AssemblyHealthEntry ToAssemblyEntry(AssemblyAnalysisLease lease)
    {
        var origin = lease.Context.Origin;
        return new(
            lease.CanonicalPath,
            lease.Context.Status.ToWireValue(),
            origin.OriginKind,
            origin.SourceProjectPath,
            origin.SourceSnapshotIdentity,
            origin.ContentHash,
            origin.GeneratedDocumentPath,
            origin.Confidence,
            origin.Trust,
            lease.Context.Generation,
            lease.Context.Diagnostics,
            TransitiveDiagnostics: lease.ReferenceExpansionDiagnostics);
    }

    private static AssemblyHealthEntry ProjectAssemblyEntry(
        AssemblyHealthEntry assembly,
        GetServerHealthOptions options)
    {
        var summary = AssemblyAnalysisResponseLimits.ProjectDiagnostics(
            assembly.Diagnostics,
            assembly.TransitiveDiagnostics,
            options.MaxDiagnostics);
        if (!options.IncludeDiagnostics)
        {
            summary = AssemblyAnalysisResponseLimits.WithoutSamples(summary);
        }
        return assembly with
        {
            Diagnostics = options.IncludeDiagnostics ? summary.Samples : null,
            DiagnosticsSummary = summary,
            Completeness = assembly.Completeness ?? assembly.LoadState,
            TransitiveDiagnostics = null,
        };
    }

    private static void AppendProjectSection(StringBuilder builder, ProjectSnapshot snapshot)
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

    private static void AppendAssemblySection(StringBuilder builder, AssemblyHealthEntry assembly)
    {
        builder.AppendLine($"### {assembly.TargetPath}");
        builder.AppendLine($"- LoadState: {assembly.LoadState}");
        if (!string.IsNullOrWhiteSpace(assembly.Completeness))
        {
            builder.AppendLine($"- Vollständigkeit: {assembly.Completeness}");
        }
        builder.AppendLine($"- Origin: {assembly.OriginKind ?? "unbekannt"}");
        builder.AppendLine($"- Generation: {assembly.Generation?.ToString() ?? "unbekannt"}");
        if (!string.IsNullOrWhiteSpace(assembly.SourceProjectPath))
        {
            builder.AppendLine($"- Source-Projekt: {assembly.SourceProjectPath}");
        }

        if (assembly.SourceSnapshot is { } snapshot)
        {
            builder.AppendLine($"- Source-Snapshot: {snapshot.RepositoryUrl} @ {snapshot.LoadedRevision} — Solution {snapshot.SolutionPath}");
        }

        if (!string.IsNullOrWhiteSpace(assembly.ContentHash)) builder.AppendLine($"- Hash: {assembly.ContentHash}");
        if (!string.IsNullOrWhiteSpace(assembly.GeneratedDocumentPath)) builder.AppendLine($"- GeneratedPath: {assembly.GeneratedDocumentPath}");
        if (!string.IsNullOrWhiteSpace(assembly.Confidence)) builder.AppendLine($"- Confidence: {assembly.Confidence}");
        if (!string.IsNullOrWhiteSpace(assembly.Trust)) builder.AppendLine($"- Trust: {assembly.Trust}");
        if (assembly.DiagnosticsSummary is { } summary && summary.TotalCount > 0)
        {
            builder.AppendLine($"- Diagnosen: {summary.ShownCount} von {summary.TotalCount}{(summary.Truncated ? " (gekürzt)" : string.Empty)}");
            if (assembly.Diagnostics is { Count: > 0 })
            {
                foreach (var diagnostic in assembly.Diagnostics)
                {
                    builder.AppendLine($"  - {diagnostic}");
                }
            }
        }

        builder.AppendLine();
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
