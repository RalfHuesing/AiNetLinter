#nullable enable

using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp;
using AiNetLinter.Output;
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
            new AssemblyToolExecutionParameters(
                state,
                arguments.AssemblyPath,
                null,
                AssemblyAnalysisService.NormalizeMaxResults(arguments.MaxResults),
                ct,
                (fullPath, context, maxResults) =>
                {
                    var selection = AssemblyAnalysisService.Inspect(
                        context,
                        new AssemblyInspectionOptions(arguments.Namespace, arguments.TypeName, arguments.MemberName, arguments.PublicOnly, maxResults));
                    var completeness = context.Diagnostics.Count == 0 ? "complete" : "partial";
                    var payload = new InspectAssemblyPayload(
                        fullPath,
                        context.Identity,
                        selection.Namespaces,
                        context.References,
                        selection.Items,
                        context.Diagnostics,
                        completeness,
                        selection.Truncated,
                        selection.Total);
                    return McpToolResults.Text(FormatText(payload), payload);
                }));
    }

    private static string FormatText(InspectAssemblyPayload payload)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Assembly: `{payload.Identity?.Name ?? "unbekannt"}`");
        builder.AppendLine($"Pfad: `{payload.AssemblyPath}`");
        builder.AppendLine($"Vollständigkeit: `{payload.Completeness}`");
        builder.AppendLine();
        if (payload.Identity is { } identity)
        {
            builder.AppendLine($"Identität: {identity.Name}, Version {identity.Version}, Kultur {identity.Culture}");
        }

        builder.AppendLine($"Öffentliche Namespaces: {payload.Namespaces.Count}");
        foreach (var namespaceName in payload.Namespaces)
        {
            builder.AppendLine($"- `{namespaceName}`");
        }

        builder.AppendLine($"Referenzen: {payload.References.Count}");
        foreach (var reference in payload.References)
        {
            builder.AppendLine($"- {reference.Name}, Version {reference.Version} ({(reference.Resolved ? "aufgelöst" : "nicht aufgelöst")})");
        }

        builder.AppendLine();
        builder.AppendLine($"Öffentliche API-Typen: {payload.Types.Count} von {payload.TotalTypes}{(payload.Truncated ? " (gekürzt)" : string.Empty)}");
        foreach (var type in payload.Types)
        {
            var qualifiedName = string.IsNullOrEmpty(type.Namespace) ? type.Name : $"{type.Namespace}.{type.Name}";
            builder.AppendLine($"- `{qualifiedName}` ({type.Kind}, {type.Accessibility})");
            foreach (var member in type.Members)
            {
                builder.AppendLine($"  - {member.Kind}: `{member.Signature}`");
            }
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
