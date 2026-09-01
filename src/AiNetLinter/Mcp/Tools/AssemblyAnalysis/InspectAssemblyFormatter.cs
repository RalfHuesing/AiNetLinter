#nullable enable

using System;
using System.Collections.Generic;
using System.Text;

namespace AiNetLinter.Mcp.Tools.AssemblyAnalysis;

/// <summary>
/// Formatiert den Text-Payload von <see cref="InspectAssemblyTool"/>.
/// Ausgelagert, um den AIContextFootprint von InspectAssemblyTool unter dem Grenzwert zu halten.
/// </summary>
internal static class InspectAssemblyFormatter
{
    internal static string FormatText(InspectAssemblyPayload payload, bool publicOnly)
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
        if (summary?.ReferencesTruncated == true && references.Count == 0)
        {
            builder.AppendLine("- Referenzdetails nicht angefordert; includeReferences=true für die Liste");
        }
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
        if (summary?.ReferenceSessionsTruncated == true && sessions.Count == 0)
        {
            builder.AppendLine("- Referenz-Sessiondetails nicht angefordert; includeReferences=true für die Liste");
        }
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
        builder.AppendLine($"{VisibilityLabel(publicOnly)}API-Typen: {payload.ShownCount} von {payload.TotalTypes}{FormatTruncation(payload.Truncated, payload.TruncatedBy)}");
        foreach (var type in payload.Types) AppendType(builder, type);
    }

    private static string VisibilityLabel(bool publicOnly) => publicOnly ? "Öffentliche " : string.Empty;

    private static void AppendType(StringBuilder builder, AssemblyTypeDto type)
    {
        var qualifiedName = string.IsNullOrEmpty(type.Namespace) ? type.Name : $"{type.Namespace}.{type.Name}";
        var memberCount = type.MembersTruncated
            ? $", Member {type.Members.Count} von {type.TotalMembers} gezeigt{FormatTruncation(true, type.TruncatedBy)}"
            : $", {type.TotalMembers} Member";
        builder.AppendLine($"- `{qualifiedName}` ({type.Kind}, {type.Accessibility}{memberCount})");
        foreach (var member in type.Members) builder.AppendLine($"  - {member.Kind}: `{member.Signature}`");
    }

    private static string FormatTruncation(bool truncated, IReadOnlyList<string>? reasons) =>
        truncated
            ? $" (gekürzt{(reasons is { Count: > 0 } ? $": {string.Join(", ", reasons)}" : string.Empty)})"
            : string.Empty;
}
