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
            CreateParameters(state, arguments, ct));
    }

    // ainetlinter-disable DuplicateCode — der direkte Adapter bindet einen fachlich eigenen Payload-Builder.
    internal static Task<CallToolResult> ExecuteAsync(
        AssemblyAnalysisLease lease,
        InspectAssemblyArguments arguments) =>
        Task.FromResult(BuildResult(lease.CanonicalPath, lease.Context, arguments, AssemblyAnalysisService.NormalizeLimit(arguments.MaxResults, 1, AssemblyAnalysisService.MaxResults), lease));

    internal static async Task<CallToolResult> ExecuteAsync(
        McpCodeGraphServer? state,
        InspectAssemblyArguments arguments,
        CancellationToken ct,
        IAssemblySourceSelectionResolver orchestrator)
    {
        return await AssemblyAnalysisToolSupport.ExecuteAsync(
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
            (fullPath, context, maxResults) => BuildResult(fullPath, context, arguments, maxResults));

    private static CallToolResult BuildResult(
        string fullPath,
        AssemblyContext context,
        InspectAssemblyArguments arguments,
        int maxResults,
        AssemblyAnalysisLease? lease = null)
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
        var diagnostics = context.Diagnostics
            .Concat(lease?.ReferenceExpansionDiagnostics ?? Array.Empty<string>())
            .Distinct(StringComparer.Ordinal)
            .Take(100)
            .ToList();
        var effectiveStatus = context.Status.ResolveEffectiveStatus(diagnostics);
        var payload = new InspectAssemblyPayload(
            fullPath,
            context.Identity,
            selection.Namespaces,
            context.References,
            selection.Items,
            diagnostics,
            effectiveStatus.ToCompletenessLabel(),
            selection.Truncated,
            selection.Total,
            context.Origin,
            context.Generation,
            effectiveStatus.ToWireValue(),
            CreateReferenceSessions(lease));
        return McpToolResults.Text(FormatText(payload, arguments.PublicOnly), payload);
    }

    private static string FormatText(InspectAssemblyPayload payload, bool publicOnly)
    {
        var builder = new StringBuilder();
        AppendHeader(builder, payload);
        AppendNamespaces(builder, payload.Namespaces, publicOnly);
        AppendReferences(builder, payload.References);
        AppendReferenceSessions(builder, payload.ReferenceSessions ?? Array.Empty<AssemblyReferenceSessionDto>());
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
            AssemblyAnalysisOriginText.Append(builder, origin);
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
            var path = reference.ResolvedPath is null ? string.Empty : $", Pfad `{reference.ResolvedPath}`";
            var diagnostic = string.IsNullOrWhiteSpace(reference.Diagnostic) ? string.Empty : $": {reference.Diagnostic}";
            builder.AppendLine($"- {reference.Name}, Version {reference.Version} (Tiefe {reference.Depth}, Zustand {reference.ResolutionState}, {(reference.Resolved ? "aufgelöst" : "nicht aufgelöst")}{path}{diagnostic})");
        }

        builder.AppendLine();
    }

    private static void AppendReferenceSessions(
        StringBuilder builder,
        IReadOnlyList<AssemblyReferenceSessionDto> sessions)
    {
        builder.AppendLine($"Referenz-Sessions: {sessions.Count}");
        foreach (var session in sessions)
        {
            var identity = session.Identity?.Name ?? session.Reference.Name;
            var diagnostic = session.Diagnostics.Count == 0
                ? string.Empty
                : $": {string.Join(" ", session.Diagnostics)}";
            builder.AppendLine(
                $"- {identity} (Tiefe {session.Reference.Depth}, Zustand {session.Reference.ResolutionState}, Session {session.SessionStatus}, Vollständigkeit {session.Completeness}, Pfad `{session.AssemblyPath}`{diagnostic})");
        }

        builder.AppendLine();
    }

    private static IReadOnlyList<AssemblyReferenceSessionDto> CreateReferenceSessions(
        AssemblyAnalysisLease? lease)
    {
        if (lease is null || lease.ReferenceSessions.Count == 0)
        {
            return Array.Empty<AssemblyReferenceSessionDto>();
        }

        return lease.ReferenceSessions
            .Select(session => new AssemblyReferenceSessionDto(
                session.Reference,
                session.AssemblyPath,
                session.Identity,
                session.Diagnostics,
                session.Completeness,
                session.Origin,
                session.SessionStatus))
            .ToList();
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

}
