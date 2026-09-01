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
        var fullPath = request.FullPath;
        var context = request.Context;
        var arguments = request.Arguments;
        var maxResults = request.MaxResults;
        var lease = request.Lease;
        var selection = AssemblyAnalysisService.FindExtensions(
            context,
            new AssemblyExtensionSearchOptions(arguments.ExtensionName, arguments.Namespace, arguments.ReceiverType, maxResults));
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
        var payload = new FindAssemblyExtensionsPayload(
            fullPath,
            selection.Items,
            diagnostics.Samples,
            effectiveStatus.ToCompletenessLabel(),
            selection.Truncated,
            selection.Total,
            selection.Items.Count,
            selection.TruncatedBy,
            context.ConsumerProject,
            arguments.ReceiverType,
            context.Origin,
            context.Generation,
            effectiveStatus.ToWireValue(),
            AssemblyAnalysisResponseLimits.ProjectReferences(context.References),
            referenceSessions,
            diagnostics,
            referenceSummary);
        payload = AssemblyAnalysisResponseLimits.ProjectResponseBudget(
            payload,
            lease is null
                ? null
                : candidate => AssemblyAnalysisResponse.FitsResponseBudget(
                    McpToolResults.Text(FormatText(candidate), candidate),
                    lease));
        return McpToolResults.Text(FormatText(payload), payload);
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
        builder.AppendLine($"Assembly-Extensions: {payload.ShownCount} von {payload.TotalExtensions}{(payload.Truncated ? $" (gekürzt: {string.Join(", ", payload.TruncatedBy)})" : string.Empty)}");
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
