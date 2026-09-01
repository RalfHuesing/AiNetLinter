#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AiNetLinter.Mcp.Assemblies.Analysis;
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
        var payload = CreatePayload(fullPath, context, arguments, maxResults, lease);
        payload = ApplyResponseBudget(payload, arguments, lease);
        return McpToolResults.Text(InspectAssemblyFormatter.FormatText(payload, arguments.PublicOnly), payload);
    }

    private static InspectAssemblyPayload CreatePayload(
        string fullPath,
        AssemblyContext context,
        InspectAssemblyArguments arguments,
        int maxResults,
        AssemblyAnalysisLease? lease)
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
        var includeReferenceDetails = arguments.IncludeReferenceDetails;
        var referenceSessions = includeReferenceDetails
            ? AssemblyAnalysisResponseLimits.ProjectReferenceSessions(lease?.ReferenceSessions)
            : Array.Empty<AssemblyReferenceSessionDto>();
        var referenceSummary = AssemblyAnalysisResponseLimits.CreateReferenceSummary(
            context.References,
            lease?.ReferenceSessions,
            includeReferenceDetails);
        var effectiveStatus = context.Status.ResolveEffectiveStatus(
            context.Diagnostics
                .Concat(lease?.ReferenceExpansionDiagnostics ?? Array.Empty<string>())
                .ToArray());
        var payload = new InspectAssemblyPayload(
            fullPath,
            context.Identity,
            selection.Namespaces,
            includeReferenceDetails
                ? AssemblyAnalysisResponseLimits.ProjectReferences(context.References)
                : Array.Empty<AssemblyReferenceDto>(),
            selection.Items,
            diagnostics.Samples,
            effectiveStatus.ToCompletenessLabel(),
            selection.Truncated,
            selection.Total,
            selection.Items.Count,
            selection.TruncatedBy,
            context.Origin,
            context.Generation,
            effectiveStatus.ToWireValue(),
            referenceSessions,
            diagnostics,
            referenceSummary,
            includeReferenceDetails);
        return payload;
    }

    private static InspectAssemblyPayload ApplyResponseBudget(
        InspectAssemblyPayload payload,
        InspectAssemblyArguments arguments,
        AssemblyAnalysisLease? lease) =>
        AssemblyAnalysisResponseLimits.ProjectResponseBudget(
            payload,
            arguments.PublicOnly,
            lease is null
                ? null
                : candidate => AssemblyAnalysisResponse.FitsResponseBudget(
                    McpToolResults.Text(InspectAssemblyFormatter.FormatText(candidate, arguments.PublicOnly), candidate),
                    lease));
}
