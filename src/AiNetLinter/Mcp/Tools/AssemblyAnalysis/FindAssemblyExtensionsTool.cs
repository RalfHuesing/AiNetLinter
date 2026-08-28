#nullable enable

using System;
using System.Text;
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
            new AssemblyToolExecutionParameters(
                state,
                arguments.AssemblyPath,
                arguments.ReceiverType,
                AssemblyAnalysisService.NormalizeLimit(arguments.MaxResults, 1, AssemblyAnalysisService.MaxResults),
                ct,
                (fullPath, context, maxResults) =>
                {
                    var selection = AssemblyAnalysisService.FindExtensions(
                        context,
                        new AssemblyExtensionSearchOptions(arguments.ExtensionName, arguments.Namespace, maxResults));
                    var completeness = context.Diagnostics.Count == 0
                        ? context.Status.ToCompletenessLabel()
                        : AssemblySessionStatus.Partial.ToCompletenessLabel();
                    var payload = new FindAssemblyExtensionsPayload(
                        fullPath,
                        selection.Items,
                        context.Diagnostics,
                        completeness,
                        selection.Truncated,
                        selection.Total,
                        context.ConsumerProject,
                        arguments.ReceiverType,
                        context.Origin,
                        context.Generation,
                        context.Status.ToString().ToLowerInvariant());
                    return McpToolResults.Text(FormatText(payload), payload);
                }));
    }

    private static string FormatText(FindAssemblyExtensionsPayload payload)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Assembly-Extensions: {payload.TotalExtensions}{(payload.Truncated ? " (gekürzt)" : string.Empty)}");
        builder.AppendLine($"Vollständigkeit: `{payload.Completeness}`");
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
            builder.AppendLine("Diagnosen:");
            foreach (var diagnostic in payload.Diagnostics) builder.AppendLine($"- {diagnostic}");
        }

        return builder.ToString().TrimEnd();
    }

}
