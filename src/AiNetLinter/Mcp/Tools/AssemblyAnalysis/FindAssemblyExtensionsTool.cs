#nullable enable

using System;
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

    // ainetlinter-disable DuplicateCode — der direkte Adapter bindet einen fachlich eigenen Payload-Builder.
    internal static Task<CallToolResult> ExecuteAsync(
        AssemblyAnalysisLease lease,
        FindAssemblyExtensionsArguments arguments) =>
        Task.FromResult(BuildResult(
            lease.CanonicalPath,
            lease.Context,
            arguments,
            AssemblyAnalysisService.NormalizeLimit(arguments.MaxResults, 1, AssemblyAnalysisService.MaxResults),
            lease));

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
            new AssemblyExtensionSearchOptions(arguments.ExtensionName, arguments.Namespace, maxResults));
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
        builder.AppendLine($"Assembly-Extensions: {payload.TotalExtensions}{(payload.Truncated ? " (gekürzt)" : string.Empty)}");
        builder.AppendLine($"Vollständigkeit: `{payload.Completeness}`");
        if (payload.ReferenceSummary is { } referenceSummary)
        {
            builder.AppendLine($"Referenzen: {referenceSummary.ShownReferenceCount} von {referenceSummary.TotalReferenceCount}{(referenceSummary.ReferencesTruncated ? " (gekürzt)" : string.Empty)}");
            builder.AppendLine($"Referenz-Sessions: {referenceSummary.ShownReferenceSessionCount} von {referenceSummary.TotalReferenceSessionCount}{(referenceSummary.ReferenceSessionsTruncated ? " (gekürzt)" : string.Empty)}");
        }
        if (payload.Origin is { } origin)
        {
            AssemblyAnalysisOriginText.Append(builder, origin);
        }
        if (payload.ConsumerProject is not null)
        {
            builder.AppendLine($"Consumer: `{payload.ConsumerProject}`");
        }

        if (payload.ReceiverType is not null)
        {
            builder.AppendLine($"Receiver: `{payload.ReceiverType}`");
        }

        foreach (var extension in payload.Extensions)
        {
            var qualifiedName = string.IsNullOrEmpty(extension.Namespace) ? extension.Name : $"{extension.Namespace}.{extension.Name}";
            builder.AppendLine($"- `{qualifiedName}` für `{extension.ReceiverType}` — {extension.Applicability}");
            builder.AppendLine($"  Signatur: `{extension.Signature}`");
            if (extension.ApplicabilityReason is not null) builder.AppendLine($"  Grund: {extension.ApplicabilityReason}");
        }

        if (payload.Diagnostics.Count > 0)
        {
            builder.AppendLine();
            var count = payload.DiagnosticsSummary is { } summary
                ? $"{summary.ShownCount} von {summary.TotalCount}"
                : payload.Diagnostics.Count.ToString();
            builder.AppendLine($"Diagnosen: {count}{(payload.DiagnosticsSummary?.Truncated == true ? " (gekürzt)" : string.Empty)}");
            foreach (var diagnostic in payload.Diagnostics) builder.AppendLine($"- {diagnostic}");
        }

        return builder.ToString().TrimEnd();
    }

}
