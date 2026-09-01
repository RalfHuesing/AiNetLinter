#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using AiNetLinter.Mcp;

namespace AiNetLinter.Mcp.Tools.AssemblyAnalysis;

internal static class AssemblyAnalysisResponseLimits
{
    internal const int DefaultMaxDiagnostics = 20;
    internal const int MaxDiagnostics = 50;
    internal const int MaxDiagnosticCharacters = 256;
    internal const int MaxDiagnosticBytes = 4 * 1024;
    internal const int MaxReferences = 32;
    internal const int MaxReferenceSessions = 32;
    internal const int MaxSessionDiagnostics = 3;
    internal const int MaxResponseBytes = 8 * 1024;

    internal static int NormalizeDiagnosticLimit(int requested) =>
        requested <= 0 ? DefaultMaxDiagnostics : Math.Clamp(requested, 1, MaxDiagnostics);

    internal static AssemblyDiagnosticsSummary WithoutSamples(AssemblyDiagnosticsSummary summary) =>
        summary with
        {
            Root = summary.Root with { ShownCount = 0, Samples = Array.Empty<string>() },
            Transitive = summary.Transitive with { ShownCount = 0, Samples = Array.Empty<string>() },
            ShownCount = 0,
            Samples = Array.Empty<string>(),
        };

    internal static AssemblyDiagnosticsSummary ProjectDiagnostics(
        IEnumerable<string>? rootDiagnostics,
        IEnumerable<string>? transitiveDiagnostics,
        int requestedLimit = DefaultMaxDiagnostics)
    {
        var limit = NormalizeDiagnosticLimit(requestedLimit);
        var root = Normalize(rootDiagnostics);
        var transitive = Normalize(transitiveDiagnostics)
            .Where(diagnostic => !root.Contains(diagnostic, StringComparer.Ordinal))
            .ToList();
        var all = root.Concat(transitive).ToList();
        var selection = SelectSamples(SelectRepresentativeDiagnostics(root, transitive, limit), limit);
        var allSummary = CreateSummary(all, limit, selection.Samples, selection.ByteTruncated);
        return new(
            CreateSummary(root, limit, selection.RootSamples, selection.ByteTruncated),
            CreateSummary(transitive, limit, selection.TransitiveSamples, selection.ByteTruncated),
            all.Count,
            allSummary.ShownCount,
            allSummary.Truncated,
            allSummary.Samples,
            allSummary.TruncatedBy);
    }

    internal static IReadOnlyList<AssemblyReferenceDto> ProjectReferences(
        IEnumerable<AssemblyReferenceDto>? references)
    {
        if (references is null) return Array.Empty<AssemblyReferenceDto>();
        return references
            .Take(MaxReferences)
            .Select(reference => reference with { Diagnostic = NormalizeForDisplay(reference.Diagnostic) })
            .ToList();
    }

    internal static IReadOnlyList<AssemblyReferenceSessionDto> ProjectReferenceSessions(
        IEnumerable<AssemblyReferenceSession>? sessions)
    {
        if (sessions is null) return Array.Empty<AssemblyReferenceSessionDto>();
        return sessions
            .Take(MaxReferenceSessions)
            .Select(ProjectReferenceSession)
            .ToList();
    }

    internal static AssemblyReferenceSummary CreateReferenceSummary(
        IEnumerable<AssemblyReferenceDto>? references,
        IEnumerable<AssemblyReferenceSession>? sessions)
    {
        var referenceCount = references?.Count() ?? 0;
        var sessionCount = sessions?.Count() ?? 0;
        return new(
            referenceCount,
            Math.Min(referenceCount, MaxReferences),
            referenceCount > MaxReferences,
            sessionCount,
            Math.Min(sessionCount, MaxReferenceSessions),
            sessionCount > MaxReferenceSessions);
    }

    internal static InspectAssemblyPayload ProjectResponseBudget(
        InspectAssemblyPayload payload,
        bool publicOnly)
    {
        var projected = payload;
        if (FitsResponseBudget(projected, publicOnly)) return projected;

        if (projected.Diagnostics.Count > 0)
        {
            projected = MarkResponseBudget(projected with
            {
                Diagnostics = projected.Diagnostics.Take(1).ToList(),
                DiagnosticsSummary = ProjectDiagnosticSamples(projected.DiagnosticsSummary, projected.Diagnostics.Take(1).ToList()),
            });
        }

        while (!FitsResponseBudget(projected, publicOnly))
        {
            if (TryRemoveLastReferenceSession(ref projected)
                || TryRemoveLastReference(ref projected)
                || TryRemoveLastDiagnostic(ref projected)
                || TryRemoveLastMember(ref projected)
                || TryRemoveLastType(ref projected)
                || TryRemoveLastNamespace(ref projected))
            {
                continue;
            }

            break;
        }

        return projected;
    }

    internal static FindAssemblyExtensionsPayload ProjectResponseBudget(
        FindAssemblyExtensionsPayload payload)
    {
        var projected = payload;
        if (FitsResponseBudget(projected)) return projected;

        if (projected.Diagnostics.Count > 0)
        {
            projected = MarkResponseBudget(projected with
            {
                Diagnostics = projected.Diagnostics.Take(1).ToList(),
                DiagnosticsSummary = ProjectDiagnosticSamples(projected.DiagnosticsSummary, projected.Diagnostics.Take(1).ToList()),
            });
        }

        while (!FitsResponseBudget(projected))
        {
            if (TryRemoveLastReferenceSession(ref projected)
                || TryRemoveLastReference(ref projected)
                || TryRemoveLastDiagnostic(ref projected)
                || TryRemoveLastExtension(ref projected))
            {
                continue;
            }

            break;
        }

        return projected;
    }

    internal static string? NormalizeForDisplay(string? diagnostic)
    {
        if (string.IsNullOrWhiteSpace(diagnostic)) return diagnostic;
        var normalized = NormalizeMessage(diagnostic);
        return normalized.Length <= MaxDiagnosticCharacters
            ? normalized
            : normalized[..(MaxDiagnosticCharacters - 1)] + "…";
    }

    internal static void AppendDiagnostics(
        StringBuilder builder,
        IReadOnlyList<string> diagnostics,
        AssemblyDiagnosticsSummary? summary)
    {
        if (diagnostics.Count == 0 && summary?.TotalCount is not > 0) return;
        builder.AppendLine();
        var count = summary is null
            ? diagnostics.Count.ToString()
            : $"{summary.ShownCount} von {summary.TotalCount}";
        builder.AppendLine($"Diagnosen: {count}{(summary?.Truncated == true ? " (gekürzt)" : string.Empty)}");
        foreach (var diagnostic in diagnostics) builder.AppendLine($"- {diagnostic}");
    }

    private static bool FitsResponseBudget(
        InspectAssemblyPayload payload,
        bool publicOnly) =>
        Encoding.UTF8.GetByteCount(InspectAssemblyFormatter.FormatText(payload, publicOnly)) <= MaxResponseBytes
        && JsonSerializer.SerializeToUtf8Bytes(payload, McpJsonOptions.Default).Length <= MaxResponseBytes;

    private static bool FitsResponseBudget(FindAssemblyExtensionsPayload payload) =>
        Encoding.UTF8.GetByteCount(FindAssemblyExtensionsTool.FormatText(payload)) <= MaxResponseBytes
        && JsonSerializer.SerializeToUtf8Bytes(payload, McpJsonOptions.Default).Length <= MaxResponseBytes;

    private static bool TryRemoveLastReferenceSession(ref InspectAssemblyPayload payload)
    {
        if (payload.ReferenceSessions is not { Count: > 1 } sessions) return false;
        var shownSessions = sessions.Count - 1;
        payload = MarkResponseBudget(payload with
        {
            ReferenceSessions = sessions.Take(shownSessions).ToList(),
        });
        payload = payload with { ReferenceSummary = UpdateReferenceSummary(payload.ReferenceSummary, payload.References.Count, shownSessions) };
        return true;
    }

    private static bool TryRemoveLastReference(ref InspectAssemblyPayload payload)
    {
        if (payload.References.Count <= 1) return false;
        payload = MarkResponseBudget(payload with
        {
            References = payload.References.Take(payload.References.Count - 1).ToList(),
        });
        payload = payload with { ReferenceSummary = UpdateReferenceSummary(payload.ReferenceSummary, payload.References.Count, payload.ReferenceSessions?.Count ?? 0) };
        return true;
    }

    private static bool TryRemoveLastDiagnostic(ref InspectAssemblyPayload payload)
    {
        if (payload.Diagnostics.Count <= 1) return false;
        var diagnostics = payload.Diagnostics.Take(payload.Diagnostics.Count - 1).ToList();
        payload = MarkResponseBudget(payload with
        {
            Diagnostics = diagnostics,
            DiagnosticsSummary = ProjectDiagnosticSamples(payload.DiagnosticsSummary, diagnostics),
        });
        return true;
    }

    private static bool TryRemoveLastMember(ref InspectAssemblyPayload payload)
    {
        var types = payload.Types.ToList();
        for (var index = types.Count - 1; index >= 0; index--)
        {
            if (types.Count > 1 && index == 0) break;
            var type = types[index];
            if (type.Members.Count == 0) continue;
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

    private static bool TryRemoveLastType(ref InspectAssemblyPayload payload)
    {
        if (payload.Types.Count <= 1) return false;
        var types = payload.Types.Take(payload.Types.Count - 1).ToList();
        payload = MarkResponseBudget(payload with
        {
            Types = types,
            ShownCount = types.Count,
        });
        return true;
    }

    private static bool TryRemoveLastNamespace(ref InspectAssemblyPayload payload)
    {
        if (payload.Namespaces.Count == 0) return false;
        payload = MarkResponseBudget(payload with
        {
            Namespaces = payload.Namespaces.Take(payload.Namespaces.Count - 1).ToList(),
        });
        return true;
    }

    private static bool TryRemoveLastReferenceSession(ref FindAssemblyExtensionsPayload payload)
    {
        if (payload.ReferenceSessions is not { Count: > 1 } sessions) return false;
        var shownSessions = sessions.Count - 1;
        payload = MarkResponseBudget(payload with
        {
            ReferenceSessions = sessions.Take(shownSessions).ToList(),
        });
        payload = payload with { ReferenceSummary = UpdateReferenceSummary(payload.ReferenceSummary, payload.References?.Count ?? 0, shownSessions) };
        return true;
    }

    private static bool TryRemoveLastReference(ref FindAssemblyExtensionsPayload payload)
    {
        if (payload.References is not { Count: > 1 } references) return false;
        var shownReferences = references.Count - 1;
        payload = MarkResponseBudget(payload with
        {
            References = references.Take(shownReferences).ToList(),
        });
        payload = payload with { ReferenceSummary = UpdateReferenceSummary(payload.ReferenceSummary, shownReferences, payload.ReferenceSessions?.Count ?? 0) };
        return true;
    }

    private static bool TryRemoveLastDiagnostic(ref FindAssemblyExtensionsPayload payload)
    {
        if (payload.Diagnostics.Count <= 1) return false;
        var diagnostics = payload.Diagnostics.Take(payload.Diagnostics.Count - 1).ToList();
        payload = MarkResponseBudget(payload with
        {
            Diagnostics = diagnostics,
            DiagnosticsSummary = ProjectDiagnosticSamples(payload.DiagnosticsSummary, diagnostics),
        });
        return true;
    }

    private static bool TryRemoveLastExtension(ref FindAssemblyExtensionsPayload payload)
    {
        if (payload.Extensions.Count <= 1) return false;
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
        var visibleSamples = summary.Samples
            .Where(samples.Contains)
            .ToList();
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

    private static AssemblyReferenceSessionDto ProjectReferenceSession(AssemblyReferenceSession session)
    {
        var summary = ProjectDiagnostics(session.Diagnostics, null, MaxSessionDiagnostics);
        return new(
            session.Reference with { Diagnostic = NormalizeForDisplay(session.Reference.Diagnostic) },
            session.AssemblyPath,
            session.Identity,
            summary.Samples,
            session.Completeness,
            session.Origin,
            session.SessionStatus,
            new(
                summary.Root.TotalCount,
                summary.Root.ShownCount,
                summary.Root.Truncated,
                summary.Root.Samples,
                summary.Root.TruncatedBy));
    }

    private static IReadOnlyList<string> Normalize(IEnumerable<string>? diagnostics) =>
        diagnostics is null
            ? []
            : diagnostics
                .Where(diagnostic => !string.IsNullOrWhiteSpace(diagnostic))
                .Select(NormalizeMessage)
                .Distinct(StringComparer.Ordinal)
                .ToList();

    private static AssemblyDiagnosticSummary CreateSummary(
        IReadOnlyList<string> diagnostics,
        int limit,
        IReadOnlyList<string>? selectedSamples = null,
        bool byteTruncated = false)
    {
        bool messageTruncated;
        IReadOnlyList<string> samples;
        if (selectedSamples is null)
        {
            var selection = SelectSamples(
                diagnostics
                    .Select(diagnostic => new DiagnosticSampleCandidate(diagnostic, false))
                    .ToList(),
                limit);
            samples = selection.Samples;
            byteTruncated = selection.ByteTruncated;
            messageTruncated = diagnostics.Any(diagnostic => diagnostic.Length > MaxDiagnosticCharacters);
        }
        else
        {
            samples = selectedSamples;
            messageTruncated = diagnostics.Any(diagnostic => diagnostic.Length > MaxDiagnosticCharacters);
        }
        var truncatedBy = new List<string>();
        if (diagnostics.Count > limit) truncatedBy.Add("maxDiagnostics");
        if (messageTruncated) truncatedBy.Add("messageLength");
        if (byteTruncated) truncatedBy.Add("maxDiagnosticBytes");
        return new(
            diagnostics.Count,
            samples.Count,
            truncatedBy.Count > 0,
            samples,
            truncatedBy);
    }

    private static IReadOnlyList<DiagnosticSampleCandidate> SelectRepresentativeDiagnostics(
        IReadOnlyList<string> root,
        IReadOnlyList<string> transitive,
        int limit)
    {
        if (transitive.Count == 0)
        {
            return root.Take(limit)
                .Select(diagnostic => new DiagnosticSampleCandidate(diagnostic, true))
                .ToList();
        }

        if (root.Count == 0)
        {
            return transitive.Take(limit)
                .Select(diagnostic => new DiagnosticSampleCandidate(diagnostic, false))
                .ToList();
        }

        var rootCount = Math.Min(root.Count, Math.Max(1, limit - 1));
        return root.Take(rootCount)
            .Select(diagnostic => new DiagnosticSampleCandidate(diagnostic, true))
            .Concat(transitive.Take(limit - rootCount)
                .Select(diagnostic => new DiagnosticSampleCandidate(diagnostic, false)))
            .ToList();
    }

    private static DiagnosticSampleSelection SelectSamples(
        IReadOnlyList<DiagnosticSampleCandidate> candidates,
        int limit)
    {
        var samples = new List<string>(Math.Min(limit, candidates.Count));
        var rootSamples = new List<string>();
        var transitiveSamples = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var bytes = 0;
        var byteTruncated = false;
        foreach (var candidate in candidates.Take(limit))
        {
            var sample = NormalizeForDisplay(candidate.Diagnostic)!;
            if (!seen.Add(sample)) continue;
            var sampleBytes = Encoding.UTF8.GetByteCount(sample);
            if (bytes + sampleBytes > MaxDiagnosticBytes)
            {
                byteTruncated = true;
                break;
            }
            samples.Add(sample);
            if (candidate.IsRoot) rootSamples.Add(sample);
            else transitiveSamples.Add(sample);
            bytes += sampleBytes;
        }

        return new(samples, rootSamples, transitiveSamples, byteTruncated);
    }

    private static string NormalizeMessage(string diagnostic) =>
        string.Join(' ', diagnostic.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private readonly record struct DiagnosticSampleCandidate(string Diagnostic, bool IsRoot);

    private sealed record DiagnosticSampleSelection(
        IReadOnlyList<string> Samples,
        IReadOnlyList<string> RootSamples,
        IReadOnlyList<string> TransitiveSamples,
        bool ByteTruncated);
}

internal sealed record AssemblyDiagnosticSummary(
    int TotalCount,
    int ShownCount,
    bool Truncated,
    IReadOnlyList<string> Samples,
    IReadOnlyList<string> TruncatedBy);

internal sealed record AssemblyDiagnosticsSummary(
    AssemblyDiagnosticSummary Root,
    AssemblyDiagnosticSummary Transitive,
    int TotalCount,
    int ShownCount,
    bool Truncated,
    IReadOnlyList<string> Samples,
    IReadOnlyList<string> TruncatedBy);

internal sealed record AssemblyReferenceSummary(
    int TotalReferenceCount,
    int ShownReferenceCount,
    bool ReferencesTruncated,
    int TotalReferenceSessionCount,
    int ShownReferenceSessionCount,
    bool ReferenceSessionsTruncated);
