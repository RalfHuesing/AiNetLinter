#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Assemblies;
using AiNetLinter.Mcp.Assemblies.Analysis;
using AiNetLinter.Mcp.Assemblies.Analysis.References;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Tools.AssemblyAnalysis.Responses;

internal static class FindAssemblyExtensionsResponseBuilder
{
    internal static CallToolResult Build(FindAssemblyExtensionsBuildRequest request)
    {
        var payload = CreatePayload(request);
        payload = ApplyResponseBudget(payload, request);
        return McpToolResults.Text(FormatText(payload), payload);
    }

    private static FindAssemblyExtensionsPayload CreatePayload(FindAssemblyExtensionsBuildRequest request)
    {
        var arguments = request.Arguments;
        var context = request.Context;
        var selection = AssemblyAnalysisService.FindExtensions(
            context,
            new AssemblyExtensionSearchOptions(arguments.ExtensionName, arguments.Namespace, arguments.ReceiverType,
                request.MaxResults, AssemblyPaging.ReadOffset(arguments.Cursor)));
        var diagnostics = AssemblyAnalysisResponseLimits.ProjectDiagnostics(context.Diagnostics, request.Lease?.ReferenceExpansionDiagnostics);
        var status = context.Status.ResolveEffectiveStatus(context.Diagnostics.Concat(request.Lease?.ReferenceExpansionDiagnostics ?? Array.Empty<string>()).ToArray());
        var includeReferences = arguments.IncludeReferences;
        return new FindAssemblyExtensionsPayload(
            request.FullPath,
            selection.Items,
            diagnostics.Samples,
            status.ToCompletenessLabel(),
            selection.Truncated,
            selection.Total,
            selection.Items.Count,
            selection.TruncatedBy,
            context.ConsumerProject,
            arguments.ReceiverType,
            context.Origin,
            context.Generation,
            status.ToWireValue(),
            AssemblyAnalysisResponseLimits.ProjectReferences(context.References),
            AssemblyAnalysisResponseLimits.ProjectReferenceSessions(request.Lease?.ReferenceSessions),
            diagnostics,
            AssemblyAnalysisResponseLimits.CreateReferenceSummary(context.References, request.Lease?.ReferenceSessions, includeReferences)) with
        {
            TotalCount = selection.Total,
            ReturnedCount = selection.Items.Count,
            IsTruncated = selection.Truncated,
            ContinuationToken = selection.Truncated ? AssemblyPaging.CreateToken(AssemblyPaging.ReadOffset(arguments.Cursor) + selection.Items.Count) : null,
            Scope = includeReferences ? "root+references" : "root",
        };
    }

    private static FindAssemblyExtensionsPayload ApplyResponseBudget(
        FindAssemblyExtensionsPayload payload,
        FindAssemblyExtensionsBuildRequest request)
    {
        var budget = AssemblyAnalysisResponseLimits.ResolveResponseBudget(
            request.Arguments.MaxResponseBytes,
            request.Arguments.DetailLevel,
            request.Lease?.Context.ResponseBudgetBytes ?? AssemblyAnalysisResponseLimits.DefaultResponseBytes);
        return AssemblyAnalysisResponseLimits.ProjectResponseBudget(
            payload,
            request.Lease is null ? null : candidate => AssemblyAnalysisResponse.FitsResponseBudget(
                McpToolResults.Text(FormatText(candidate), candidate), request.Lease, budget),
            budget);
    }

    internal static string FormatText(FindAssemblyExtensionsPayload payload)
    {
        var builder = new StringBuilder();
        AppendHeader(builder, payload);
        AppendExtensions(builder, payload.Extensions);
        AssemblyAnalysisResponseLimits.AppendDiagnostics(builder, payload.Diagnostics, payload.DiagnosticsSummary);
        return builder.ToString().TrimEnd();
    }

    private static void AppendHeader(StringBuilder builder, FindAssemblyExtensionsPayload payload)
    {
        var extensionsTruncated = payload.ShownCount < payload.TotalExtensions;
        builder.AppendLine($"Assembly-Extensions: {payload.ShownCount} von {payload.TotalExtensions}{(extensionsTruncated ? $" (gekürzt: {string.Join(", ", payload.TruncatedBy)})" : string.Empty)}");
        builder.AppendLine($"Vollständigkeit: `{payload.Completeness}`");
        AppendReferenceSummary(builder, payload.ReferenceSummary);
        if (payload.Origin is { } origin) AssemblyAnalysisOriginText.Append(builder, origin);
        if (payload.ConsumerProject is not null) builder.AppendLine($"Consumer: `{payload.ConsumerProject}`");
        if (payload.ReceiverType is not null) builder.AppendLine($"Receiver: `{payload.ReceiverType}`");
    }

    private static void AppendReferenceSummary(StringBuilder builder, AssemblyReferenceSummary? summary)
    {
        if (summary is null) return;
        builder.AppendLine($"Referenzen: {summary.ShownReferenceCount} von {summary.TotalReferenceCount}{(summary.ReferencesTruncated ? " (gekürzt)" : string.Empty)}");
        builder.AppendLine($"Referenz-Sessions: {summary.ShownReferenceSessionCount} von {summary.TotalReferenceSessionCount}{(summary.ReferenceSessionsTruncated ? " (gekürzt)" : string.Empty)}");
    }

    private static void AppendExtensions(StringBuilder builder, IReadOnlyList<AssemblyExtensionDto> extensions)
    {
        foreach (var extension in extensions)
        {
            var qualifiedName = string.IsNullOrEmpty(extension.Namespace)
                ? extension.Name
                : $"{extension.Namespace}.{extension.Name}";
            builder.AppendLine($"- `{qualifiedName}` für `{extension.ReceiverType}` — {extension.Applicability}");
            builder.AppendLine($"  Signatur: `{extension.Signature}`");
            if (extension.ApplicabilityReason is not null) builder.AppendLine($"  Grund: {extension.ApplicabilityReason}");
        }
    }
}

internal sealed record FindAssemblyExtensionsBuildRequest(
    string FullPath,
    AssemblyContext Context,
    FindAssemblyExtensionsArguments Arguments,
    int MaxResults,
    AssemblyAnalysisLease? Lease);
