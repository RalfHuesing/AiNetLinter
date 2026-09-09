#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AiNetLinter.Mcp.Composition;
using AiNetLinter.Mcp.Projects;
using AiNetLinter.Mcp.Tools.AssemblyAnalysis;
using AiNetLinter.Mcp.Tools.ServerMaintenance.Projection;
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
        var targeted = options.TargetPath is not null || options.AssemblyPath is not null;
        // The registration removes the public global detail flags. Keeping the
        // internal option semantics here preserves focused builder tests and
        // legacy in-process callers without widening the public schema.
        var includeDiagnostics = options.IncludeDiagnostics;
        var projectedAssemblies = assemblies
            .Select(assembly => AssemblyHealthProjection.Project(
                assembly,
                includeDiagnostics,
                options.MaxDiagnostics))
            .ToList();
        var totalAssemblySessions = projectedAssemblies.Count;
        var shownAssemblies = SelectShownAssemblies(projectedAssemblies, targeted, options);
        var sessionsTruncated = shownAssemblies is not null && totalAssemblySessions > shownAssemblies.Count;
        var sessionsTruncatedBy = sessionsTruncated ? new[] { "maxSessions" } : Array.Empty<string>();
        var statusCounts = AssemblyHealthProjection.CountStatuses(projectedAssemblies);
        var diagnosticCount = projectedAssemblies.Sum(assembly => assembly.DiagnosticsSummary?.TotalCount ?? 0);
        var daemonPayload = runtimeContext is null ? null : DaemonHealthProjection.FromContext(runtimeContext, targeted);
        var version = McpServerVersion.Get();
        var response = new HealthResponseData(
            version,
            McpServerVersion.RepositoryUrl,
            targeted ? snapshots : Array.Empty<ProjectSnapshot>(),
            daemonPayload,
            shownAssemblies,
            options,
            totalAssemblySessions,
            sessionsTruncated,
            sessionsTruncatedBy,
            statusCounts,
            diagnosticCount);
        var builder = BuildText(response);
        return McpToolResults.Text(
            builder,
            CreatePayload(response));
    }

    private static IReadOnlyList<AssemblyHealthEntry>? SelectShownAssemblies(
        IReadOnlyList<AssemblyHealthEntry> assemblies,
        bool targeted,
        GetServerHealthOptions options)
    {
        if (!targeted && !options.IncludeSessions && !options.IncludeDiagnostics) return null;
        var maxSessions = Math.Clamp(options.MaxSessions, 1, GetServerHealthTool.MaxSessions);
        return assemblies.Take(maxSessions).ToList();
    }

    private static string BuildText(HealthResponseData response)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# AiNetLinter MCP-Server — Health");
        builder.AppendLine();
        builder.AppendLine($"- Version: {response.Version}");
        builder.AppendLine($"- Repository: {response.RepositoryUrl}");
        GetServerHealthFormatter.AppendDaemonSection(builder, response.Daemon);
        builder.AppendLine();
        builder.AppendLine($"## Projekte ({response.Snapshots.Count})");
        builder.AppendLine();
        foreach (var snapshot in response.Snapshots) GetServerHealthFormatter.AppendProjectSection(builder, snapshot);
        builder.AppendLine($"## Assembly-Sessions ({response.TotalAssemblySessions})");
        builder.AppendLine();
        AppendAssemblyText(
            builder,
            response.ShownAssemblies,
            response.SessionsTruncated,
            response.TotalAssemblySessions,
            response.StatusCounts,
            response.DiagnosticCount);
        return builder.ToString().TrimEnd();
    }

    private static void AppendAssemblyText(
        StringBuilder builder,
        IReadOnlyList<AssemblyHealthEntry>? shownAssemblies,
        bool sessionsTruncated,
        int totalAssemblySessions,
        IReadOnlyDictionary<string, int> statusCounts,
        int diagnosticCount)
    {
        if (shownAssemblies is null)
        {
            GetServerHealthFormatter.AppendAssemblyAggregate(builder, totalAssemblySessions, statusCounts, diagnosticCount);
            return;
        }

        if (sessionsTruncated)
        {
            builder.AppendLine($"- Sessiondetails: {shownAssemblies.Count} von {totalAssemblySessions} (gekürzt: maxSessions)");
            builder.AppendLine();
        }

        foreach (var assembly in shownAssemblies) GetServerHealthFormatter.AppendAssemblySection(builder, assembly);
    }

    private static ServerHealthAggregatePayload CreatePayload(HealthResponseData response) =>
        new(
            Version: response.Version,
            Projects: response.Snapshots.Select(ProjectHealthProjection.FromSnapshot).ToList(),
            Repository: response.Targeted ? response.RepositoryUrl : null,
            Daemon: response.Daemon,
            Assemblies: response.ShownAssemblies?.Select(AssemblyHealthProjection.ToPublicEntry).ToList(),
            DiagnosticsIncluded: response.Options.IncludeDiagnostics,
            DiagnosticLimit: AssemblyAnalysisResponseLimits.NormalizeDiagnosticLimit(response.Options.MaxDiagnostics),
            SessionsIncluded: response.ShownAssemblies is not null,
            TotalAssemblySessions: response.TotalAssemblySessions,
            ShownSessionCount: response.ShownAssemblies?.Count ?? 0,
            SessionsTruncated: response.SessionsTruncated,
            SessionsTruncatedBy: response.SessionsTruncatedBy,
            AssemblyStatusCounts: response.StatusCounts,
            AssemblyDiagnosticCount: response.DiagnosticCount);

    private sealed record HealthResponseData(
        string Version,
        string RepositoryUrl,
        IReadOnlyList<ProjectSnapshot> Snapshots,
        DaemonHealthPayload? Daemon,
        IReadOnlyList<AssemblyHealthEntry>? ShownAssemblies,
        GetServerHealthOptions Options,
        int TotalAssemblySessions,
        bool SessionsTruncated,
        IReadOnlyList<string> SessionsTruncatedBy,
        IReadOnlyDictionary<string, int> StatusCounts,
        int DiagnosticCount)
    {
        internal bool Targeted => Options.TargetPath is not null || Options.AssemblyPath is not null;
    }

}
