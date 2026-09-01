#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AiNetLinter.Mcp.Composition;
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
            .Select(assembly => GetServerHealthProjection.ProjectAssemblyEntry(assembly, options))
            .ToList();
        var targeted = options.ProjectRoot is not null || options.AssemblyPath is not null;
        var totalAssemblySessions = projectedAssemblies.Count;
        var maxSessions = Math.Clamp(options.MaxSessions, 1, GetServerHealthTool.MaxSessions);
        var shownAssemblies = targeted || options.IncludeSessions
            ? projectedAssemblies.Take(maxSessions).ToList()
            : null;
        var sessionsTruncated = shownAssemblies is not null && totalAssemblySessions > shownAssemblies.Count;
        var sessionsTruncatedBy = sessionsTruncated ? new[] { "maxSessions" } : Array.Empty<string>();
        var statusCounts = GetServerHealthProjection.CountAssemblyStatuses(projectedAssemblies);
        var diagnosticCount = projectedAssemblies.Sum(assembly => assembly.DiagnosticsSummary?.TotalCount ?? 0);
        var builder = new StringBuilder();
        builder.AppendLine("# AiNetLinter MCP-Server — Health");
        builder.AppendLine();
        builder.AppendLine($"- Version: {McpServerVersion.Get()}");
        var daemonPayload = runtimeContext is null ? null : GetServerHealthProjection.CreateDaemonPayload(runtimeContext);
        GetServerHealthFormatter.AppendDaemonSection(builder, daemonPayload);
        builder.AppendLine();
        builder.AppendLine($"## Projekte ({snapshots.Count})");
        builder.AppendLine();
        foreach (var snapshot in snapshots)
        {
            GetServerHealthFormatter.AppendProjectSection(builder, snapshot);
        }

        builder.AppendLine($"## Assembly-Sessions ({totalAssemblySessions})");
        builder.AppendLine();
        if (shownAssemblies is null)
        {
            GetServerHealthFormatter.AppendAssemblyAggregate(
                builder, totalAssemblySessions, statusCounts, diagnosticCount);
        }
        else
        {
            if (sessionsTruncated)
            {
                builder.AppendLine($"- Sessiondetails: {shownAssemblies.Count} von {totalAssemblySessions} (gekürzt: maxSessions)");
                builder.AppendLine();
            }
            foreach (var assembly in shownAssemblies)
            {
                GetServerHealthFormatter.AppendAssemblySection(builder, assembly);
            }
        }

        var payload = new ServerHealthAggregatePayload(
            Version: McpServerVersion.Get(),
            Projects: snapshots.Select(GetServerHealthProjection.ToProjectEntry).ToList(),
            Daemon: daemonPayload,
            Assemblies: shownAssemblies,
            DiagnosticsIncluded: options.IncludeDiagnostics,
            DiagnosticLimit: AssemblyAnalysisResponseLimits.NormalizeDiagnosticLimit(options.MaxDiagnostics),
            SessionsIncluded: shownAssemblies is not null,
            TotalAssemblySessions: totalAssemblySessions,
            ShownSessionCount: shownAssemblies?.Count ?? 0,
            SessionsTruncated: sessionsTruncated,
            SessionsTruncatedBy: sessionsTruncatedBy,
            AssemblyStatusCounts: statusCounts,
            AssemblyDiagnosticCount: diagnosticCount);
        return McpToolResults.Text(builder.ToString().TrimEnd(), payload);
    }

}
