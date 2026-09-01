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
        AssemblyAnalysisToolSupport.ExecuteLeaseAsync(
            lease,
            arguments,
            arguments.MaxResults,
            (fullPath, context, inspectedArguments, maxResults, activeLease) =>
                BuildResult(new InspectAssemblyBuildRequest(
                    fullPath,
                    context,
                    inspectedArguments,
                    maxResults,
                    activeLease)));

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
            (fullPath, context, maxResults) => BuildResult(new InspectAssemblyBuildRequest(
                fullPath,
                context,
                arguments,
                maxResults,
                null)));

    private static CallToolResult BuildResult(InspectAssemblyBuildRequest request)
    {
        var payload = CreatePayload(
            request.FullPath,
            request.Context,
            request.Arguments,
            request.MaxResults,
            request.Lease);
        payload = ApplyResponseBudget(payload, request.Arguments, request.Lease);
        return McpToolResults.Text(
            InspectAssemblyFormatter.FormatText(payload, request.Arguments.PublicOnly),
            payload);
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

    private sealed record InspectAssemblyBuildRequest(
        string FullPath,
        AssemblyContext Context,
        InspectAssemblyArguments Arguments,
        int MaxResults,
        AssemblyAnalysisLease? Lease);
}
