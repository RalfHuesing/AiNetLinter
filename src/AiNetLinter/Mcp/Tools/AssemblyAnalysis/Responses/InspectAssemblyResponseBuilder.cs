#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Assemblies;
using AiNetLinter.Mcp.Assemblies.Analysis;
using AiNetLinter.Mcp.Assemblies.Analysis.References;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Tools.AssemblyAnalysis.Responses;

internal static class InspectAssemblyResponseBuilder
{
    internal static CallToolResult Build(InspectAssemblyBuildRequest request)
    {
        var payload = CreatePayload(request);
        payload = ApplyResponseBudget(payload, request.Arguments, request.Lease);
        return McpToolResults.Text(
            InspectAssemblyFormatter.FormatText(payload, request.Arguments.PublicOnly),
            payload);
    }

    private static InspectAssemblyPayload CreatePayload(InspectAssemblyBuildRequest request)
    {
        var arguments = request.Arguments;
        var context = request.Context;
        var selection = AssemblyAnalysisService.Inspect(
            context,
            new AssemblyInspectionOptions(
                arguments.Namespace,
                arguments.TypeName,
                arguments.MemberName,
                arguments.PublicOnly,
                arguments.ExactTypeName,
                arguments.MemberNames,
                request.MaxResults,
                AssemblyAnalysisService.NormalizeLimit(arguments.MaxMembers, AssemblyAnalysisService.DefaultMaxMembers, AssemblyAnalysisService.MaxMembers)));
        var diagnostics = AssemblyAnalysisResponseLimits.ProjectDiagnostics(
            context.Diagnostics,
            request.Lease?.ReferenceExpansionDiagnostics);
        var includeReferenceDetails = arguments.IncludeReferenceDetails;
        var totalNamespaces = selection.Namespaces.Count;
        var namespaces = InspectAssemblyFormatter.CompactNamespaces(selection.Namespaces);
        var referenceSessions = includeReferenceDetails
            ? AssemblyAnalysisResponseLimits.ProjectReferenceSessions(request.Lease?.ReferenceSessions)
            : Array.Empty<AssemblyReferenceSessionDto>();
        var referenceSummary = AssemblyAnalysisResponseLimits.CreateReferenceSummary(
            context.References,
            request.Lease?.ReferenceSessions,
            includeReferenceDetails);
        var effectiveStatus = context.Status.ResolveEffectiveStatus(
            context.Diagnostics
                .Concat(request.Lease?.ReferenceExpansionDiagnostics ?? Array.Empty<string>())
                .ToArray());
        return new InspectAssemblyPayload(
            request.FullPath,
            context.Identity,
            namespaces,
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
            includeReferenceDetails,
            totalNamespaces);
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

internal sealed record InspectAssemblyBuildRequest(
    string FullPath,
    AssemblyContext Context,
    InspectAssemblyArguments Arguments,
    int MaxResults,
    AssemblyAnalysisLease? Lease);
