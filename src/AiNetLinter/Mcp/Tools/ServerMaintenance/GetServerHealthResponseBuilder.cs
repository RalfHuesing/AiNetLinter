#nullable enable

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

        builder.AppendLine($"## Assembly-Sessions ({projectedAssemblies.Count})");
        builder.AppendLine();
        foreach (var assembly in projectedAssemblies)
        {
            GetServerHealthFormatter.AppendAssemblySection(builder, assembly);
        }

        var payload = new ServerHealthAggregatePayload(
            Version: McpServerVersion.Get(),
            Projects: snapshots.Select(GetServerHealthProjection.ToProjectEntry).ToList(),
            Daemon: daemonPayload,
            Assemblies: projectedAssemblies,
            DiagnosticsIncluded: options.IncludeDiagnostics,
            DiagnosticLimit: AssemblyAnalysisResponseLimits.NormalizeDiagnosticLimit(options.MaxDiagnostics));
        return McpToolResults.Text(builder.ToString().TrimEnd(), payload);
    }

}
