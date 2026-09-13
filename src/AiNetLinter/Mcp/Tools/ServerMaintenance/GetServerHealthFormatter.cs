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
    private const string DaemonProfileLabel = "Daemon-Profil";

    internal static void AppendDaemonSection(StringBuilder builder, DaemonHealthPayload? daemon)
    {
        if (daemon is null) return;
        builder.AppendLine($"- Mode: {daemon.Mode}");
        builder.AppendLine($"- Connections: {daemon.Connections}");
        builder.AppendLine($"- Daemon-Uptime: {FormatUptime(TimeSpan.FromSeconds(daemon.UptimeSeconds))}");
        builder.AppendLine($"- Daemon-Keys: {(daemon.Keys.Count == 0 ? "keine" : string.Join(", ", daemon.Keys))}");
        builder.AppendLine($"- Daemon-Version: {daemon.DaemonVersion}");
        AppendOptionalAssemblyValue(builder, DaemonProfileLabel, daemon.DaemonProfile);
        builder.AppendLine();
    }

    internal static void AppendProjectSection(StringBuilder builder, ProjectSnapshot snapshot)
    {
        var server = snapshot.Server;
        builder.AppendLine($"### {snapshot.Definition.SolutionPath}");
        builder.AppendLine($"- LoadState: {server.LoadState}");
        builder.AppendLine($"- Solution: {(server.LoadState == ServerLoadState.Loading ? "wird noch geladen" : server.GetCurrentSolution()?.FilePath ?? "unbekannt")}");
        var (_, resolvedConfigPath) = server.GetConfigSnapshot();
        builder.AppendLine($"- targetPath: {snapshot.Definition.SolutionPath}");
        builder.AppendLine($"- Config: {resolvedConfigPath ?? "not_configured"}");
        builder.AppendLine($"- Zuletzt genutzt (UTC): {FormatTimestamp(snapshot.LastUsedUtc)}");
        builder.AppendLine($"- Uptime: {FormatUptime(server.Uptime)}");
        builder.AppendLine($"- Solution-Refreshes seit Start: {server.RefreshCount}");
        AppendProjectCapabilities(builder, server);
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

    internal static void AppendTargetProjectSection(StringBuilder builder, ProjectSnapshot snapshot)
    {
        var server = snapshot.Server;
        builder.AppendLine($"### {snapshot.Definition.SolutionPath}");
        builder.AppendLine($"- LoadState: {server.LoadState}");
        builder.AppendLine($"- Solution: {(server.LoadState == ServerLoadState.Loading ? "wird noch geladen" : server.GetCurrentSolution()?.FilePath ?? "unbekannt")}");
        var (_, resolvedConfigPath) = server.GetConfigSnapshot();
        builder.AppendLine($"- targetPath: {snapshot.Definition.SolutionPath}");
        builder.AppendLine($"- Config: {resolvedConfigPath ?? "not_configured"}");
        builder.AppendLine($"- Zuletzt genutzt (UTC): {FormatTimestamp(snapshot.LastUsedUtc)}");
        builder.AppendLine($"- Solution-Refreshes: {server.RefreshCount}");
        AppendProjectCapabilities(builder, server);
        AppendStalenessSection(builder, server.LastStalenessStats, "für dieses Target");
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

    internal static void AppendTargetAssemblySection(StringBuilder builder, AssemblyHealthEntry assembly)
    {
        AppendAssemblyHeader(builder, assembly, includeDaemonProfile: false);
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
        builder.AppendLine("- Sessiondetails global unterdrückt; nur serverweite Aggregate und Fehlerzähler");
        builder.AppendLine($"- Sessions gesamt: {totalSessions}");
        builder.AppendLine($"- Statusverteilung: {string.Join(", ", statusCounts.Select(pair => $"{pair.Key}={pair.Value}"))}");
        builder.AppendLine($"- Diagnosen gesamt: {diagnosticCount}");
    }

    internal static void AppendProjectAggregate(
        StringBuilder builder,
        int totalSessions,
        IReadOnlyDictionary<string, int> statusCounts)
    {
        builder.AppendLine("- Sessiondetails global unterdrückt; nur serverweite Aggregate und Statuszähler");
        builder.AppendLine($"- Sessions gesamt: {totalSessions}");
        builder.AppendLine($"- LoadState-Verteilung: {string.Join(", ", statusCounts.Select(pair => $"{pair.Key}={pair.Value}"))}");
    }

    private static void AppendAssemblyHeader(
        StringBuilder builder,
        AssemblyHealthEntry assembly,
        bool includeDaemonProfile = true)
    {
        builder.AppendLine($"### {assembly.TargetPath}");
        builder.AppendLine($"- LoadState: {assembly.LoadState}");
        if (!string.IsNullOrWhiteSpace(assembly.Completeness))
        {
            builder.AppendLine($"- analysisCompleteness: {assembly.Completeness}");
        }
        builder.AppendLine($"- Origin: {assembly.OriginKind ?? "unbekannt"}");
        builder.AppendLine($"- capabilities: syntax={GetAssemblySyntaxCapability(assembly)} lint=unsupported");
        if (includeDaemonProfile) AppendOptionalAssemblyValue(builder, DaemonProfileLabel, assembly.DaemonProfile);
        AppendOptionalAssemblyValue(builder, "Lock-Status", assembly.LockStatus);
        AppendOptionalAssemblyValue(builder, "Lease-Status", assembly.LeaseStatus);
        AppendOptionalAssemblyValue(builder, "Cleanup-Status", assembly.CleanupStatus);
        AppendOptionalAssemblyValue(builder, "Fehlercode", assembly.ErrorCode);
        AppendOptionalAssemblyValue(builder, "Fehlerphase", assembly.ErrorPhase);
        AppendOptionalAssemblyValue(builder, "Fehlerursache", assembly.ErrorCause);
        AppendOptionalAssemblyValue(builder, "Nächste Aktion", assembly.NextAction);
    }

    private static void AppendAssemblySourceDetails(StringBuilder builder, AssemblyHealthEntry assembly)
    {
        if (string.Equals(assembly.OriginKind, "decompiled", StringComparison.OrdinalIgnoreCase))
        {
            builder.AppendLine("- Quelle: Dekompilat");
        }
        else if (!string.IsNullOrWhiteSpace(assembly.OriginKind))
        {
            builder.AppendLine($"- Quelle: {assembly.OriginKind}");
        }

        AppendOptionalAssemblyValue(builder, "Hash", assembly.ContentHash);
        AppendOptionalAssemblyValue(builder, "Confidence", assembly.Confidence);
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
            builder.AppendLine("- Diagnosen: keine");
            return;
        }

        if (assembly.Diagnostics is null)
        {
            builder.AppendLine($"- Diagnosen: {summary.TotalCount} (omitted; includeDiagnostics=true)");
            return;
        }

        builder.AppendLine($"- Diagnosen: {summary.ShownCount} von {summary.TotalCount}{(summary.Truncated ? " (gekürzt)" : string.Empty)}");
        if (assembly.Diagnostics.Count == 0)
        {
            return;
        }

        foreach (var diagnostic in assembly.Diagnostics)
        {
            builder.AppendLine($"  - {diagnostic}");
        }
    }

    private static void AppendStalenessSection(
        StringBuilder builder,
        ServerStalenessStats staleness,
        string scope = "seit Start")
    {
        builder.AppendLine($"- Staleness-Checks {scope}: {staleness.CheckCount} (kumuliert {staleness.TotalMilliseconds:F0} ms)");
        if (staleness.LastWarning is { } warning)
        {
            builder.AppendLine($"- Staleness-Warnungen (letzter Lauf): {staleness.WarningCount}, zuletzt: {warning}");
        }
    }

    private static void AppendProjectCapabilities(StringBuilder builder, McpCodeGraphServer server)
    {
        var loadState = server.LoadState.ToString().ToLowerInvariant();
        var syntaxCapability = server.LoadState == ServerLoadState.Loaded ? "available" : "unavailable";
        var (_, configPath) = server.GetConfigSnapshot();
        var lintCapability = configPath is null ? "not_configured" : "configured";
        var solution = server.GetCurrentSolution();
        var projectCount = solution?.ProjectIds.Count ?? 0;
        var documentCount = solution?.Projects.Sum(project => project.DocumentIds.Count) ?? 0;
        builder.AppendLine($"- capabilities: syntax={syntaxCapability} lint={lintCapability}");
        builder.AppendLine($"- index: state={loadState} projects={projectCount} documents={documentCount}");
    }

    private static string GetAssemblySyntaxCapability(AssemblyHealthEntry assembly) =>
        string.Equals(assembly.OriginKind, "decompiled", StringComparison.OrdinalIgnoreCase)
            ? "decompiled"
            : "available";

    private static string FormatTimestamp(DateTime utc) => utc.ToString("yyyy-MM-dd HH:mm:ss");

    private static string FormatUptime(TimeSpan uptime)
    {
        if (uptime.TotalHours >= 1) return $"{(int)uptime.TotalHours}h {uptime.Minutes}min";
        return uptime.TotalMinutes >= 1 ? $"{(int)uptime.TotalMinutes}min {uptime.Seconds}s" : $"{uptime.Seconds}s";
    }
}
