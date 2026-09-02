#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Tools.AssemblyAnalysis.Responses;

namespace AiNetLinter.Mcp.Tools.AssemblyAnalysis;

internal static partial class AssemblyAnalysisResponseLimits
{
    internal static InspectAssemblyPayload ProjectResponseBudget(
        InspectAssemblyPayload payload,
        bool publicOnly,
        Func<InspectAssemblyPayload, bool>? fitsBudget = null)
    {
        fitsBudget ??= candidate => FitsResponseBudget(candidate, publicOnly);
        var projected = payload;
        if (fitsBudget(projected)) return projected;

        if (projected.Diagnostics.Count > 0)
        {
            projected = MarkResponseBudget(projected with
            {
                Diagnostics = projected.Diagnostics.Take(1).ToList(),
                DiagnosticsSummary = ProjectDiagnosticSamples(projected.DiagnosticsSummary, projected.Diagnostics.Take(1).ToList()),
            });
        }

        while (!fitsBudget(projected) && TryTrimInspect(ref projected)) { }

        return projected;
    }

    internal static FindAssemblyExtensionsPayload ProjectResponseBudget(
        FindAssemblyExtensionsPayload payload,
        Func<FindAssemblyExtensionsPayload, bool>? fitsBudget = null)
    {
        fitsBudget ??= FitsResponseBudget;
        var projected = payload;
        if (fitsBudget(projected)) return projected;

        if (projected.Diagnostics.Count > 0)
        {
            projected = MarkResponseBudget(projected with
            {
                Diagnostics = projected.Diagnostics.Take(1).ToList(),
                DiagnosticsSummary = ProjectDiagnosticSamples(projected.DiagnosticsSummary, projected.Diagnostics.Take(1).ToList()),
            });
        }

        while (!fitsBudget(projected) && TryTrimExtensions(ref projected)) { }

        return projected;
    }

    private static bool TryTrimInspect(ref InspectAssemblyPayload payload) =>
        TryTrimInspectOptional(ref payload) || TryTrimInspectSingleton(ref payload);

    private static bool TryTrimInspectOptional(ref InspectAssemblyPayload payload) =>
        TryRemoveLastReferenceSession(ref payload)
        || TryRemoveLastReference(ref payload)
        || TryRemoveLastDiagnostic(ref payload,
            candidate => candidate.Diagnostics,
            (candidate, diagnostics) => MarkResponseBudget(candidate with
            {
                Diagnostics = diagnostics,
                DiagnosticsSummary = ProjectDiagnosticSamples(candidate.DiagnosticsSummary, diagnostics),
            }))
        || TryRemoveLastMember(ref payload)
        || TryRemoveLastType(ref payload)
        || TryRemoveLastNamespace(ref payload);

    private static bool TryTrimInspectSingleton(ref InspectAssemblyPayload payload) =>
        TryRemoveLastReferenceSession(ref payload, allowLast: true)
        || TryRemoveLastReference(ref payload, allowLast: true)
        || TryRemoveLastDiagnostic(ref payload,
            candidate => candidate.Diagnostics,
            (candidate, diagnostics) => MarkResponseBudget(candidate with
            {
                Diagnostics = diagnostics,
                DiagnosticsSummary = ProjectDiagnosticSamples(candidate.DiagnosticsSummary, diagnostics),
            }),
            allowLast: true)
        || TryRemoveLastMember(ref payload, allowLast: true)
        || TryRemoveLastType(ref payload, allowLast: true)
        || TryRemoveLastNamespace(ref payload, allowLast: true);

    private static bool TryTrimExtensions(ref FindAssemblyExtensionsPayload payload) =>
        TryTrimExtensionsOptional(ref payload) || TryTrimExtensionsSingleton(ref payload);

    private static bool TryTrimExtensionsOptional(ref FindAssemblyExtensionsPayload payload) =>
        TryRemoveLastReferenceSession(ref payload)
        || TryRemoveLastReference(ref payload)
        || TryRemoveLastDiagnostic(ref payload,
            candidate => candidate.Diagnostics,
            (candidate, diagnostics) => MarkResponseBudget(candidate with
            {
                Diagnostics = diagnostics,
                DiagnosticsSummary = ProjectDiagnosticSamples(candidate.DiagnosticsSummary, diagnostics),
            }))
        || TryRemoveLastExtension(ref payload);

    private static bool TryTrimExtensionsSingleton(ref FindAssemblyExtensionsPayload payload) =>
        TryRemoveLastReferenceSession(ref payload, allowLast: true)
        || TryRemoveLastReference(ref payload, allowLast: true)
        || TryRemoveLastDiagnostic(ref payload,
            candidate => candidate.Diagnostics,
            (candidate, diagnostics) => MarkResponseBudget(candidate with
            {
                Diagnostics = diagnostics,
                DiagnosticsSummary = ProjectDiagnosticSamples(candidate.DiagnosticsSummary, diagnostics),
            }),
            allowLast: true)
        || TryRemoveLastExtension(ref payload, allowLast: true);

    private static bool FitsResponseBudget(
        InspectAssemblyPayload payload,
        bool publicOnly) =>
        Encoding.UTF8.GetByteCount(InspectAssemblyFormatter.FormatText(payload, publicOnly)) <= MaxResponseBytes
        && JsonSerializer.SerializeToUtf8Bytes(payload, McpJsonOptions.Default).Length <= MaxResponseBytes;

    private static bool FitsResponseBudget(FindAssemblyExtensionsPayload payload) =>
        Encoding.UTF8.GetByteCount(FindAssemblyExtensionsResponseBuilder.FormatText(payload)) <= MaxResponseBytes
        && JsonSerializer.SerializeToUtf8Bytes(payload, McpJsonOptions.Default).Length <= MaxResponseBytes;

    private static bool TryRemoveLastReferenceSession(
        ref InspectAssemblyPayload payload,
        bool allowLast = false)
    {
        if (payload.ReferenceSessions is not { Count: > 0 } sessions
            || (!allowLast && sessions.Count == 1)) return false;
        var shownSessions = sessions.Count - 1;
        payload = MarkResponseBudget(payload with { ReferenceSessions = sessions.Take(shownSessions).ToList() });
        payload = payload with { ReferenceSummary = UpdateReferenceSummary(payload.ReferenceSummary, payload.References.Count, shownSessions) };
        return true;
    }

    private static bool TryRemoveLastReference(
        ref InspectAssemblyPayload payload,
        bool allowLast = false)
    {
        if (payload.References.Count == 0
            || (!allowLast && payload.References.Count == 1)) return false;
        payload = MarkResponseBudget(payload with { References = payload.References.Take(payload.References.Count - 1).ToList() });
        payload = payload with { ReferenceSummary = UpdateReferenceSummary(payload.ReferenceSummary, payload.References.Count, payload.ReferenceSessions?.Count ?? 0) };
        return true;
    }

    private const int MinPreservedMembersPerType = 3;

    private static bool TryRemoveLastMember(
        ref InspectAssemblyPayload payload,
        bool allowLast = false)
    {
        var types = payload.Types.ToList();
        for (var index = types.Count - 1; index >= 0; index--)
        {
            var type = types[index];
            if (type.Members.Count == 0
                || (!allowLast && types.Count > 1 && type.Members.Count <= MinPreservedMembersPerType)
                || (!allowLast && types.Count == 1 && type.Members.Count == 1)
                || (!allowLast && types.Count > 1 && index == 0)) continue;
            types[index] = type with
            {
                Members = type.Members.Take(type.Members.Count - 1).ToList(),
                MembersTruncated = true,
                TruncatedBy = AddReason(type.TruncatedBy, "responseBudget"),
            };
            payload = MarkResponseBudget(payload with { Types = types });
            return true;
        }

        return false;
    }

    private static bool TryRemoveLastType(
        ref InspectAssemblyPayload payload,
        bool allowLast = false)
    {
        if (payload.Types.Count == 0
            || (!allowLast && payload.Types.Count == 1)) return false;
        var types = payload.Types.Take(payload.Types.Count - 1).ToList();
        payload = MarkResponseBudget(payload with
        {
            Types = types,
            ShownCount = types.Count,
        });
        return true;
    }

    private static bool TryRemoveLastNamespace(
        ref InspectAssemblyPayload payload,
        bool allowLast = false)
    {
        if (payload.Namespaces.Count == 0
            || (!allowLast && payload.Namespaces.Count == 1)) return false;
        payload = MarkResponseBudget(payload with
        {
            Namespaces = payload.Namespaces.Take(payload.Namespaces.Count - 1).ToList(),
        });
        return true;
    }

    private static bool TryRemoveLastReferenceSession(
        ref FindAssemblyExtensionsPayload payload,
        bool allowLast = false)
    {
        if (payload.ReferenceSessions is not { Count: > 0 } sessions
            || (!allowLast && sessions.Count == 1)) return false;
        var shownSessions = sessions.Count - 1;
        payload = MarkResponseBudget(payload with { ReferenceSessions = sessions.Take(shownSessions).ToList() });
        payload = payload with { ReferenceSummary = UpdateReferenceSummary(payload.ReferenceSummary, payload.References?.Count ?? 0, shownSessions) };
        return true;
    }

    private static bool TryRemoveLastReference(
        ref FindAssemblyExtensionsPayload payload,
        bool allowLast = false)
    {
        if (payload.References is not { Count: > 0 } references
            || (!allowLast && references.Count == 1)) return false;
        var shownReferences = references.Count - 1;
        payload = MarkResponseBudget(payload with { References = references.Take(shownReferences).ToList() });
        payload = payload with { ReferenceSummary = UpdateReferenceSummary(payload.ReferenceSummary, shownReferences, payload.ReferenceSessions?.Count ?? 0) };
        return true;
    }

    private static bool TryRemoveLastDiagnostic<TPayload>(
        ref TPayload payload,
        Func<TPayload, IReadOnlyList<string>> getDiagnostics,
        Func<TPayload, IReadOnlyList<string>, TPayload> removeDiagnostics,
        bool allowLast = false)
    {
        var diagnostics = getDiagnostics(payload);
        if (diagnostics.Count == 0 || (!allowLast && diagnostics.Count == 1)) return false;
        payload = removeDiagnostics(payload, diagnostics.Take(diagnostics.Count - 1).ToList());
        return true;
    }

    private static bool TryRemoveLastExtension(
        ref FindAssemblyExtensionsPayload payload,
        bool allowLast = false)
    {
        if (payload.Extensions.Count == 0
            || (!allowLast && payload.Extensions.Count == 1)) return false;
        var extensions = payload.Extensions.Take(payload.Extensions.Count - 1).ToList();
        payload = MarkResponseBudget(payload with
        {
            Extensions = extensions,
            ShownCount = extensions.Count,
        });
        return true;
    }

    private static InspectAssemblyPayload MarkResponseBudget(InspectAssemblyPayload payload) =>
        payload with
        {
            Truncated = true,
            TruncatedBy = AddReason(payload.TruncatedBy, "responseBudget"),
        };

    private static FindAssemblyExtensionsPayload MarkResponseBudget(FindAssemblyExtensionsPayload payload) =>
        payload with
        {
            Truncated = true,
            TruncatedBy = AddReason(payload.TruncatedBy, "responseBudget"),
        };

    private static AssemblyDiagnosticsSummary? ProjectDiagnosticSamples(
        AssemblyDiagnosticsSummary? summary,
        IReadOnlyList<string> samples) =>
        summary is null
            ? null
            : summary with
            {
                Root = ProjectDiagnosticSummary(summary.Root, samples),
                Transitive = ProjectDiagnosticSummary(summary.Transitive, samples),
                ShownCount = samples.Count,
                Truncated = summary.TotalCount > samples.Count || summary.Truncated,
                Samples = samples,
                TruncatedBy = summary.TotalCount > samples.Count
                    ? AddReason(summary.TruncatedBy, "responseBudget")
                    : summary.TruncatedBy,
            };

    private static AssemblyDiagnosticSummary ProjectDiagnosticSummary(
        AssemblyDiagnosticSummary summary,
        IReadOnlyList<string> samples)
    {
        var visibleSamples = summary.Samples.Where(samples.Contains).ToList();
        return summary with
        {
            ShownCount = visibleSamples.Count,
            Truncated = summary.TotalCount > visibleSamples.Count || summary.Truncated,
            Samples = visibleSamples,
            TruncatedBy = summary.TotalCount > visibleSamples.Count
                ? AddReason(summary.TruncatedBy, "responseBudget")
                : summary.TruncatedBy,
        };
    }

    private static AssemblyReferenceSummary? UpdateReferenceSummary(
        AssemblyReferenceSummary? summary,
        int shownReferences,
        int shownSessions) =>
        summary is null
            ? null
            : summary with
            {
                ShownReferenceCount = shownReferences,
                ReferencesTruncated = shownReferences < summary.TotalReferenceCount,
                ShownReferenceSessionCount = shownSessions,
                ReferenceSessionsTruncated = shownSessions < summary.TotalReferenceSessionCount,
            };

    private static IReadOnlyList<string> AddReason(
        IReadOnlyList<string>? reasons,
        string reason)
    {
        if (reasons?.Contains(reason, StringComparer.Ordinal) == true) return reasons;
        return (reasons ?? Array.Empty<string>()).Concat([reason]).ToList();
    }
}
