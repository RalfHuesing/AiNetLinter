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
        var selection = CreateSelection(request);
        var diagnostics = AssemblyAnalysisResponseLimits.ProjectDiagnostics(
            context.Diagnostics,
            request.Lease?.ReferenceExpansionDiagnostics);
        var references = CreateReferences(request);
        var status = context.Status.ResolveEffectiveStatus(
            context.Diagnostics.Concat(request.Lease?.ReferenceExpansionDiagnostics ?? Array.Empty<string>()).ToArray());
        var payload = new InspectAssemblyPayload(
            request.FullPath,
            context.Identity,
            InspectAssemblyFormatter.CompactNamespaces(selection.Namespaces),
            references.References,
            selection.Items,
            diagnostics.Samples,
            status.ToCompletenessLabel(),
            selection.Truncated,
            selection.Total,
            selection.Items.Count,
            selection.TruncatedBy,
            context.Origin,
            context.Generation,
            status.ToWireValue(),
            references.Sessions,
            diagnostics,
            references.Summary,
            references.IncludeDetails,
            selection.Namespaces.Count,
            context.DecompiledProjectPaths?.DecompiledProjectDirectory,
            context.DecompiledProjectPaths?.DecompiledProjectPath,
            context.DecompiledProjectPaths?.DecompiledSourceRoot);
        return payload with
        {
            TotalCount = selection.Total,
            ReturnedCount = selection.Items.Count,
            IsTruncated = selection.Truncated,
            ContinuationToken = selection.Truncated
                ? AssemblyPaging.CreateToken(AssemblyPaging.ReadOffset(arguments.Cursor) + selection.Items.Count)
                : null,
            Scope = arguments.IncludeReferences == true ? "root+references" : "root",
        };
    }

    private static AssemblyTypeSelection CreateSelection(InspectAssemblyBuildRequest request)
    {
        var arguments = request.Arguments;
        return AssemblyAnalysisService.Inspect(
            request.Context,
            new AssemblyInspectionOptions(
                arguments.Namespace,
                arguments.TypeName,
                arguments.MemberName,
                arguments.PublicOnly,
                arguments.ExactTypeName,
                arguments.MemberNames,
                request.MaxResults,
                AssemblyAnalysisService.NormalizeLimit(arguments.MaxMembers, AssemblyAnalysisService.DefaultMaxMembers, AssemblyAnalysisService.MaxMembers),
                AssemblyPaging.ReadOffset(arguments.Cursor)));
    }

    private static InspectReferenceProjection CreateReferences(InspectAssemblyBuildRequest request)
    {
        var includeDetails = request.Arguments.IncludeReferenceDetails;
        return new(
            includeDetails ? AssemblyAnalysisResponseLimits.ProjectReferences(request.Context.References) : Array.Empty<AssemblyReferenceDto>(),
            includeDetails ? AssemblyAnalysisResponseLimits.ProjectReferenceSessions(request.Lease?.ReferenceSessions) : Array.Empty<AssemblyReferenceSessionDto>(),
            AssemblyAnalysisResponseLimits.CreateReferenceSummary(request.Context.References, request.Lease?.ReferenceSessions, includeDetails),
            includeDetails);
    }

    private static InspectAssemblyPayload ApplyResponseBudget(
        InspectAssemblyPayload payload,
        InspectAssemblyArguments arguments,
        AssemblyAnalysisLease? lease)
    {
        var budget = AssemblyAnalysisResponseLimits.ResolveResponseBudget(
            arguments.MaxResponseBytes,
            arguments.DetailLevel,
            configuredDefault: lease?.Context.ResponseBudgetBytes ?? AssemblyAnalysisResponseLimits.DefaultResponseBytes);
        return AssemblyAnalysisResponseLimits.ProjectResponseBudget(
            payload,
            arguments.PublicOnly,
            lease is null
                ? null
                : candidate => AssemblyAnalysisResponse.FitsResponseBudget(
                    McpToolResults.Text(InspectAssemblyFormatter.FormatText(candidate, arguments.PublicOnly), candidate),
                    lease,
                    budget),
            options: new(budget, AssemblyPaging.ReadOffset(arguments.Cursor)));
    }

}

internal sealed record InspectAssemblyBuildRequest(
    string FullPath,
    AssemblyContext Context,
    InspectAssemblyArguments Arguments,
    int MaxResults,
    AssemblyAnalysisLease? Lease);

internal sealed record InspectReferenceProjection(
    IReadOnlyList<AssemblyReferenceDto> References,
    IReadOnlyList<AssemblyReferenceSessionDto> Sessions,
    AssemblyReferenceSummary Summary,
    bool IncludeDetails);
