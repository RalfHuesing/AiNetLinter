#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AiNetLinter.Mcp.Assemblies.Analysis;
using AiNetLinter.Mcp.Assemblies.Analysis.References;
using AiNetLinter.Mcp.Composition;
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
        builder.AppendLine($"- Version: {McpServerVersion.Get()}");
        var daemonPayload = runtimeContext is null ? null : CreateDaemonPayload(runtimeContext);
        GetServerHealthFormatter.AppendDaemonSection(builder, daemonPayload);
        builder.AppendLine();
        builder.AppendLine($"## Projekte ({snapshots.Count})");
        builder.AppendLine();
        foreach (var snapshot in snapshots)
        {
            GetServerHealthFormatter.AppendProjectSection(builder, snapshot);
        }

        builder.AppendLine($"## Assembly-Sessions ({projectedAssemblies.Count})");
        builder.AppendLine();
        foreach (var assembly in projectedAssemblies)
        {
            GetServerHealthFormatter.AppendAssemblySection(builder, assembly);
        }

        var payload = new ServerHealthAggregatePayload(
            Version: McpServerVersion.Get(),
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
            ResolveEffectiveStatus(snapshot.LoadState, snapshot.Diagnostics ?? Array.Empty<string>()),
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
        var diagnostics = lease.Context.Diagnostics
            .Concat(lease.ReferenceExpansionDiagnostics)
            .ToArray();
        var effectiveStatus = lease.Context.Status.ResolveEffectiveStatus(diagnostics);
        return new(
            lease.CanonicalPath,
            effectiveStatus.ToWireValue(),
            origin.OriginKind,
            origin.SourceProjectPath,
            origin.SourceSnapshotIdentity,
            origin.ContentHash,
            origin.GeneratedDocumentPath,
            origin.Confidence,
            origin.Trust,
            lease.Context.Generation,
            lease.Context.Diagnostics,
            Completeness: effectiveStatus.ToCompletenessLabel(),
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
        var diagnostics = (assembly.Diagnostics ?? Array.Empty<string>())
            .Concat(assembly.TransitiveDiagnostics ?? Array.Empty<string>())
            .ToArray();
        var effectiveLoadState = ResolveEffectiveStatus(assembly.LoadState, diagnostics);
        var effectiveCompleteness = ResolveEffectiveStatus(
            assembly.Completeness ?? effectiveLoadState,
            diagnostics);
        if (!options.IncludeDiagnostics)
        {
            summary = AssemblyAnalysisResponseLimits.WithoutSamples(summary);
        }
        return assembly with
        {
            LoadState = effectiveLoadState,
            Diagnostics = options.IncludeDiagnostics ? summary.Samples : null,
            DiagnosticsSummary = summary,
            Completeness = effectiveCompleteness,
            TransitiveDiagnostics = null,
        };
    }

    private static string ResolveEffectiveStatus(
        string statusValue,
        IReadOnlyCollection<string> diagnostics)
    {
        if (!Enum.TryParse<AssemblySessionStatus>(statusValue, ignoreCase: true, out var status))
        {
            return statusValue;
        }

        return status.ResolveEffectiveStatus(diagnostics).ToWireValue();
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
}
