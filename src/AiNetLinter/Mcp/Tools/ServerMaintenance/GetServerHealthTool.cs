#nullable enable

using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using AiNetLinter.Mcp.Assemblies.Analysis;
using AiNetLinter.Mcp.Assemblies.Analysis.References;
using AiNetLinter.Mcp.Daemon;
using AiNetLinter.Mcp.Projects;
using AiNetLinter.Output;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Tools.ServerMaintenance;

internal sealed record GetServerHealthOptions(
    string? ProjectRoot = null,
    DaemonRuntimeContext? RuntimeContext = null,
    string? AssemblyPath = null);

/// <summary>
/// MCP-Tool <c>get_server_health</c>: Diagnose-Schnappschuss der residenten Projekt- und
/// Assembly-Sessions — pro Projekt-Key <see cref="ServerLoadState"/>, Solution-/Config-Quelle,
/// LastUsedUtc, Uptime, Refresh-Zaehler, Staleness-Aggregate und die Health-Felder des
/// zweistufigen Zustandsvertrags (<c>LastGoodStateUtc</c>/<c>LastLoadError</c>), pro Assembly
/// Origin-/Snapshot-/Hash-/Generation-/Status-Felder und optional die Laufzeitdaten des Daemons.
/// Ohne Filter wird getrennt ueber alle residenten Projekt- und Assembly-Sessions aggregiert;
/// mit Filter antwortet nur dieser Target. Reine Diagnose ohne Recoverable-Pfad.
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
        => ExecuteAsync(registry, assemblyRegistry: null, options, CancellationToken.None);

    internal static async Task<CallToolResult> ExecuteAsync(
        ProjectRegistry registry,
        IAssemblyAnalysisRegistry? assemblyRegistry,
        GetServerHealthOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.AssemblyPath is not null)
        {
            if (assemblyRegistry is null)
            {
                return AssemblyAnalysisResponse.Unsupported(options.AssemblyPath);
            }

            var leaseResult = await assemblyRegistry.LeaseAsync(options.AssemblyPath, cancellationToken).ConfigureAwait(false);
            if (leaseResult.Error is not null)
            {
                return leaseResult.Error;
            }

            using var lease = leaseResult.Lease!;
            return BuildResult(
                Array.Empty<ProjectSnapshot>(),
                [ToAssemblyEntry(lease)],
                options);
        }

        if (options.ProjectRoot is not null)
        {
            var guard = ProjectToolCall.GuardRequiredAbsoluteRoot(options.ProjectRoot);
            if (guard is not null)
            {
                return McpToolResults.Error(guard.Code, guard.Message, hint: guard.Hint);
            }

            var snapshot = registry.FindSnapshot(options.ProjectRoot);
            if (snapshot is null) return ProjectNotInitialized(options.ProjectRoot);
            return BuildResult([snapshot], Array.Empty<AssemblyHealthEntry>(), options);
        }

        var assemblySnapshots = assemblyRegistry is null
            ? Array.Empty<AssemblyAnalysisHealthSnapshot>()
            : await assemblyRegistry.SnapshotsAsync().ConfigureAwait(false);
        return BuildResult(
            registry.Snapshots(),
            assemblySnapshots.Select(ToAssemblyEntry).ToList(),
            options);
    }

    private static CallToolResult BuildResult(
        IReadOnlyList<ProjectSnapshot> snapshots,
        IReadOnlyList<AssemblyHealthEntry> assemblies,
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

        sb.AppendLine($"## Assembly-Sessions ({assemblies.Count})");
        sb.AppendLine();
        foreach (var assembly in assemblies)
        {
            AppendAssemblySection(sb, assembly);
        }

        var entries = snapshots.Select(ToEntry).ToList();
        var payload = new ServerHealthAggregatePayload(
            Version: McpServerOptionsFactory.GetServerVersion(),
            Projects: entries,
            Daemon: daemonPayload,
            Assemblies: assemblies);
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

    private static AssemblyHealthEntry ToAssemblyEntry(AssemblyAnalysisHealthSnapshot snapshot) =>
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

    private static AssemblyHealthEntry ToAssemblyEntry(AssemblyAnalysisLease lease)
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
            lease.Context.Diagnostics.Concat(lease.ReferenceExpansionDiagnostics).Distinct(StringComparer.Ordinal).Take(100).ToList());
    }

    private static void AppendAssemblySection(StringBuilder sb, AssemblyHealthEntry assembly)
    {
        sb.AppendLine($"### {assembly.TargetPath}");
        sb.AppendLine($"- LoadState: {assembly.LoadState}");
        sb.AppendLine($"- Origin: {assembly.OriginKind ?? "unbekannt"}");
        sb.AppendLine($"- Generation: {assembly.Generation?.ToString() ?? "unbekannt"}");
        if (!string.IsNullOrWhiteSpace(assembly.SourceProjectPath))
        {
            sb.AppendLine($"- Source-Projekt: {assembly.SourceProjectPath}");
        }

        if (assembly.SourceSnapshot is { } snapshot)
        {
            sb.AppendLine($"- Source-Snapshot: {snapshot.RepositoryUrl} @ {snapshot.LoadedRevision} — Solution {snapshot.SolutionPath}");
        }

        if (!string.IsNullOrWhiteSpace(assembly.ContentHash)) sb.AppendLine($"- Hash: {assembly.ContentHash}");
        if (!string.IsNullOrWhiteSpace(assembly.GeneratedDocumentPath)) sb.AppendLine($"- GeneratedPath: {assembly.GeneratedDocumentPath}");
        if (!string.IsNullOrWhiteSpace(assembly.Confidence)) sb.AppendLine($"- Confidence: {assembly.Confidence}");
        if (!string.IsNullOrWhiteSpace(assembly.Trust)) sb.AppendLine($"- Trust: {assembly.Trust}");
        if (assembly.Diagnostics is { Count: > 0 })
        {
            sb.AppendLine($"- Diagnosen: {string.Join(" | ", assembly.Diagnostics)}");
        }

        sb.AppendLine();
    }

    /// <summary>Strukturierter Fehler fuer einen adressierten, aber nicht residenten Key.</summary>
    private static CallToolResult ProjectNotInitialized(string projectRoot) =>
        McpToolResults.Error(
            ProjectErrorCodes.ProjectNotInitialized,
            $"Fuer '{projectRoot}' existiert kein residenter Projekt-Key.",
            context: projectRoot,
            hint: "Ersten Tool-Aufruf mit diesem targetPath senden; der Server legt den Key lazy " +
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
