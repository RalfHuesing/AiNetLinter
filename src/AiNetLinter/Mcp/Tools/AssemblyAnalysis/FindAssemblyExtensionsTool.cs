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

internal static class FindAssemblyExtensionsTool
{
    internal static async Task<CallToolResult> ExecuteAsync(
        McpCodeGraphServer? state,
        FindAssemblyExtensionsArguments arguments,
        CancellationToken ct)
    {
        return await AssemblyAnalysisToolSupport.ExecuteAsync(
            CreateParameters(state, arguments, ct));
    }

    internal static Task<CallToolResult> ExecuteAsync(
        AssemblyAnalysisLease lease,
        FindAssemblyExtensionsArguments arguments) =>
        AssemblyAnalysisToolSupport.ExecuteLeaseAsync(lease, arguments, arguments.MaxResults, BuildResult);

    internal static async Task<CallToolResult> ExecuteAsync(
        McpCodeGraphServer? state,
        FindAssemblyExtensionsArguments arguments,
        CancellationToken ct,
        IAssemblySourceSelectionResolver orchestrator)
    {
        return await AssemblyAnalysisSourceToolSupport.ExecuteAsync(
            CreateParameters(state, arguments, ct),
            orchestrator);
    }

    private static AssemblyToolExecutionParameters CreateParameters(
        McpCodeGraphServer? state,
        FindAssemblyExtensionsArguments arguments,
        CancellationToken ct) =>
        new(
            state,
            arguments.AssemblyPath,
            arguments.ReceiverType,
            AssemblyAnalysisService.NormalizeLimit(arguments.MaxResults, 1, AssemblyAnalysisService.MaxResults),
            ct,
            (fullPath, context, maxResults) => BuildResult(fullPath, context, arguments, maxResults));

    private static CallToolResult BuildResult(
        string fullPath,
        AssemblyContext context,
        FindAssemblyExtensionsArguments arguments,
        int maxResults,
        AssemblyAnalysisLease? lease = null)
    {
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
            context.ConsumerProject,
            arguments.ReceiverType,
            context.Origin,
            context.Generation,
            effectiveStatus.ToWireValue(),
            AssemblyAnalysisResponseLimits.ProjectReferences(context.References),
            referenceSessions,
            diagnostics,
            referenceSummary);
        return McpToolResults.Text(FormatText(payload), payload);
    }

    private static string FormatText(FindAssemblyExtensionsPayload payload)
    {
        var builder = new StringBuilder();
        AppendHeader(builder, payload);
        AppendExtensions(builder, payload.Extensions);
        AssemblyAnalysisResponseLimits.AppendDiagnostics(builder, payload.Diagnostics, payload.DiagnosticsSummary);
        return builder.ToString().TrimEnd();
    }

    private static void AppendHeader(StringBuilder builder, FindAssemblyExtensionsPayload payload)
    {
        builder.AppendLine($"Assembly-Extensions: {payload.TotalExtensions}{(payload.Truncated ? " (gekürzt)" : string.Empty)}");
        builder.AppendLine($"Vollständigkeit: `{payload.Completeness}`");
        AppendReferenceSummary(builder, payload.ReferenceSummary);
        if (payload.Origin is { } origin)
        {
            AssemblyAnalysisOriginText.Append(builder, origin);
        }
        AppendOptionalContext(builder, "Consumer", payload.ConsumerProject);
        AppendOptionalContext(builder, "Receiver", payload.ReceiverType);
    }

    private static void AppendReferenceSummary(StringBuilder builder, AssemblyReferenceSummary? summary)
    {
        if (summary is null)
        {
            return;
        }

        builder.AppendLine($"Referenzen: {summary.ShownReferenceCount} von {summary.TotalReferenceCount}{(summary.ReferencesTruncated ? " (gekürzt)" : string.Empty)}");
        builder.AppendLine($"Referenz-Sessions: {summary.ShownReferenceSessionCount} von {summary.TotalReferenceSessionCount}{(summary.ReferenceSessionsTruncated ? " (gekürzt)" : string.Empty)}");
    }

    private static void AppendOptionalContext(
        StringBuilder builder,
        string label,
        string? value)
    {
        if (value is not null)
        {
            builder.AppendLine($"{label}: `{value}`");
        }
    }

    private static void AppendExtensions(
        StringBuilder builder,
        IReadOnlyList<AssemblyExtensionDto> extensions)
    {
        foreach (var extension in extensions)
        {
            var qualifiedName = string.IsNullOrEmpty(extension.Namespace)
                ? extension.Name
                : $"{extension.Namespace}.{extension.Name}";
            builder.AppendLine($"- `{qualifiedName}` für `{extension.ReceiverType}` — {extension.Applicability}");
            builder.AppendLine($"  Signatur: `{extension.Signature}`");
            if (extension.ApplicabilityReason is not null)
            {
                builder.AppendLine($"  Grund: {extension.ApplicabilityReason}");
            }
        }
    }

}
