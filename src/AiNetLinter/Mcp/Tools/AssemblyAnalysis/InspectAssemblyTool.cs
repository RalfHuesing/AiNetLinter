#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Assemblies;
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
                AssemblyAnalysisService.NormalizeLimit(arguments.MaxResults, 1, AssemblyAnalysisService.MaxResults),
                ct,
                (fullPath, context, maxResults) =>
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
                    var completeness = context.Diagnostics.Count == 0 ? StatusLabel(context.Status) : "partial";
                    var payload = new InspectAssemblyPayload(
                        fullPath,
                        context.Identity,
                        selection.Namespaces,
                        context.References,
                        selection.Items,
                        context.Diagnostics,
                        completeness,
                        selection.Truncated,
                        selection.Total,
                        context.Origin,
                        context.Generation,
                        context.Status.ToString().ToLowerInvariant());
                    return McpToolResults.Text(FormatText(payload, arguments.PublicOnly), payload);
                }));
    }

    private static string FormatText(InspectAssemblyPayload payload, bool publicOnly)
    {
        var builder = new StringBuilder();
        AppendHeader(builder, payload);
        AppendNamespaces(builder, payload.Namespaces, publicOnly);
        AppendReferences(builder, payload.References);
        AppendTypes(builder, payload, publicOnly);
        AppendDiagnostics(builder, payload.Diagnostics);

        return builder.ToString().TrimEnd();
    }

    private static void AppendHeader(StringBuilder builder, InspectAssemblyPayload payload)
    {
        builder.AppendLine($"Assembly: `{payload.Identity?.Name ?? "unbekannt"}`");
        builder.AppendLine($"Pfad: `{payload.AssemblyPath}`");
        builder.AppendLine($"Vollständigkeit: `{payload.Completeness}`");
        if (payload.Origin is { } origin)
        {
            builder.AppendLine($"Herkunft: `{origin.OriginKind}` — `{origin.GeneratedDocumentPath}`");
            builder.AppendLine("Hinweis: Der angeforderte Code wurde dekompiliert und kann von der Originalquelle abweichen.");
        }
        builder.AppendLine();
        if (payload.Identity is { } identity)
        {
            builder.AppendLine($"Identität: {identity.Name}, Version {identity.Version}, Kultur {identity.Culture}");
        }
    }

    private static void AppendNamespaces(StringBuilder builder, IReadOnlyList<string> namespaces, bool publicOnly)
    {
        builder.AppendLine($"{VisibilityLabel(publicOnly)}Namespaces: {namespaces.Count}");
        foreach (var namespaceName in namespaces) builder.AppendLine($"- `{namespaceName}`");
    }

    private static void AppendReferences(StringBuilder builder, IReadOnlyList<AssemblyReferenceDto> references)
    {
        builder.AppendLine($"Referenzen: {references.Count}");
        foreach (var reference in references)
        {
            builder.AppendLine($"- {reference.Name}, Version {reference.Version} ({(reference.Resolved ? "aufgelöst" : "nicht aufgelöst")})");
        }

        builder.AppendLine();
    }

    private static void AppendTypes(StringBuilder builder, InspectAssemblyPayload payload, bool publicOnly)
    {
        builder.AppendLine($"{VisibilityLabel(publicOnly)}API-Typen: {payload.Types.Count} von {payload.TotalTypes}{(payload.Truncated ? " (gekürzt)" : string.Empty)}");
        foreach (var type in payload.Types) AppendType(builder, type);
    }

    private static string VisibilityLabel(bool publicOnly) => publicOnly ? "Öffentliche " : string.Empty;

    private static void AppendType(StringBuilder builder, AssemblyTypeDto type)
    {
        var qualifiedName = string.IsNullOrEmpty(type.Namespace) ? type.Name : $"{type.Namespace}.{type.Name}";
        var memberCount = type.MembersTruncated
            ? $", Member {type.Members.Count} von {type.TotalMembers} gezeigt"
            : $", {type.TotalMembers} Member";
        builder.AppendLine($"- `{qualifiedName}` ({type.Kind}, {type.Accessibility}{memberCount})");
        foreach (var member in type.Members) builder.AppendLine($"  - {member.Kind}: `{member.Signature}`");
    }

    private static void AppendDiagnostics(StringBuilder builder, IReadOnlyList<string> diagnostics)
    {
        if (diagnostics.Count == 0) return;
        builder.AppendLine();
        builder.AppendLine("Diagnosen:");
        foreach (var diagnostic in diagnostics) builder.AppendLine($"- {diagnostic}");
    }

    private static string StatusLabel(AssemblySessionStatus status) =>
        status switch
        {
            AssemblySessionStatus.Complete => "complete",
            AssemblySessionStatus.Degraded => "degraded",
            AssemblySessionStatus.Partial => "partial",
            _ => "failed",
        };
}
