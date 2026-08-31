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
        return await AssemblyAnalysisSourceToolSupport.ExecuteAsync(
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
        var payload = new InspectAssemblyPayload(
            fullPath,
            context.Identity,
            selection.Namespaces,
            AssemblyAnalysisResponseLimits.ProjectReferences(context.References),
            selection.Items,
            diagnostics.Samples,
            effectiveStatus.ToCompletenessLabel(),
            selection.Truncated,
            selection.Total,
            context.Origin,
            context.Generation,
            effectiveStatus.ToWireValue(),
            referenceSessions,
            diagnostics,
            referenceSummary);
        return McpToolResults.Text(FormatText(payload, arguments.PublicOnly), payload);
    }

    private static string FormatText(InspectAssemblyPayload payload, bool publicOnly)
    {
        var builder = new StringBuilder();
        AppendHeader(builder, payload);
        AppendNamespaces(builder, payload.Namespaces, publicOnly);
        AppendReferences(builder, payload.References, payload.ReferenceSummary);
        AppendReferenceSessions(
            builder,
            payload.ReferenceSessions ?? Array.Empty<AssemblyReferenceSessionDto>(),
            payload.ReferenceSummary);
        AppendTypes(builder, payload, publicOnly);
        AssemblyAnalysisResponseLimits.AppendDiagnostics(builder, payload.Diagnostics, payload.DiagnosticsSummary);

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

    private static void AppendReferences(
        StringBuilder builder,
        IReadOnlyList<AssemblyReferenceDto> references,
        AssemblyReferenceSummary? summary)
    {
        var referenceCount = summary is null
            ? references.Count.ToString()
            : $"{summary.ShownReferenceCount} von {summary.TotalReferenceCount}";
        builder.AppendLine($"Referenzen: {referenceCount}{(summary?.ReferencesTruncated == true ? " (gekürzt)" : string.Empty)}");
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
        IReadOnlyList<AssemblyReferenceSessionDto> sessions,
        AssemblyReferenceSummary? summary)
    {
        var sessionCount = summary is null
            ? sessions.Count.ToString()
            : $"{summary.ShownReferenceSessionCount} von {summary.TotalReferenceSessionCount}";
        builder.AppendLine($"Referenz-Sessions: {sessionCount}{(summary?.ReferenceSessionsTruncated == true ? " (gekürzt)" : string.Empty)}");
        foreach (var session in sessions)
        {
            var identity = session.Identity?.Name ?? session.Reference.Name;
            var diagnostic = session.Diagnostics.Count == 0
                ? string.Empty
                : $": {string.Join(" | ", session.Diagnostics)}";
            builder.AppendLine(
                $"- {identity} (Tiefe {session.Reference.Depth}, Zustand {session.Reference.ResolutionState}, Session {session.SessionStatus}, Vollständigkeit {session.Completeness}, Pfad `{session.AssemblyPath}`{diagnostic})");
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

}
