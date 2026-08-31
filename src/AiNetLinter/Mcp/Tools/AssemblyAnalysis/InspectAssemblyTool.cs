#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AiNetLinter.Mcp.Assemblies.Analysis.References;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Assemblies;
using AiNetLinter.Mcp;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Tools.AssemblyAnalysis;

internal static class InspectAssemblyTool
{
    internal static async Task<CallToolResult> ExecuteAsync(
        McpCodeGraphServer? state,
        InspectAssemblyArguments arguments,
        CancellationToken ct)
    {
        return await AssemblyAnalysisToolSupport.ExecuteAsync(
            CreateParameters(state, arguments, ct));
    }

    internal static Task<CallToolResult> ExecuteAsync(
        AssemblyAnalysisLease lease,
        InspectAssemblyArguments arguments) =>
        AssemblyAnalysisToolSupport.ExecuteLeaseAsync(lease, arguments, arguments.MaxResults, BuildResult);

    internal static async Task<CallToolResult> ExecuteAsync(
        McpCodeGraphServer? state,
        InspectAssemblyArguments arguments,
        CancellationToken ct,
        IAssemblySourceSelectionResolver orchestrator)
    {
        return await AssemblyAnalysisSourceToolSupport.ExecuteAsync(
            CreateParameters(state, arguments, ct),
            orchestrator);
    }

    private static AssemblyToolExecutionParameters CreateParameters(
        McpCodeGraphServer? state,
        InspectAssemblyArguments arguments,
        CancellationToken ct) =>
        new(
            state,
            arguments.AssemblyPath,
            null,
            AssemblyAnalysisService.NormalizeLimit(arguments.MaxResults, 1, AssemblyAnalysisService.MaxResults),
            ct,
            (fullPath, context, maxResults) => BuildResult(fullPath, context, arguments, maxResults));

    private static CallToolResult BuildResult(
        string fullPath,
        AssemblyContext context,
        InspectAssemblyArguments arguments,
        int maxResults,
        AssemblyAnalysisLease? lease = null)
    {
        var selection = AssemblyAnalysisService.Inspect(
            context,
            new AssemblyInspectionOptions(
                arguments.Namespace,
                arguments.TypeName,
                arguments.MemberName,
                arguments.PublicOnly,
                arguments.ExactTypeName,
                arguments.MemberNames,
                maxResults,
                AssemblyAnalysisService.NormalizeLimit(arguments.MaxMembers, AssemblyAnalysisService.DefaultMaxMembers, AssemblyAnalysisService.MaxMembers)));
        var diagnostics = AssemblyAnalysisResponseLimits.ProjectDiagnostics(
            context.Diagnostics,
            lease?.ReferenceExpansionDiagnostics);
        var referenceSessions = AssemblyAnalysisResponseLimits.ProjectReferenceSessions(lease?.ReferenceSessions);
        var referenceSummary = AssemblyAnalysisResponseLimits.CreateReferenceSummary(
            context.References,
            lease?.ReferenceSessions);
        var effectiveStatus = context.Status.ResolveEffectiveStatus(
            context.Diagnostics
                .Concat(lease?.ReferenceExpansionDiagnostics ?? Array.Empty<string>())
                .ToArray());
        var payload = new InspectAssemblyPayload(
            fullPath,
            context.Identity,
            selection.Namespaces,
            AssemblyAnalysisResponseLimits.ProjectReferences(context.References),
            selection.Items,
            diagnostics.Samples,
            effectiveStatus.ToCompletenessLabel(),
            selection.Truncated,
            selection.Total,
            context.Origin,
            context.Generation,
            effectiveStatus.ToWireValue(),
            referenceSessions,
            diagnostics,
            referenceSummary);
        return McpToolResults.Text(InspectAssemblyFormatter.FormatText(payload, arguments.PublicOnly), payload);
    }
}
