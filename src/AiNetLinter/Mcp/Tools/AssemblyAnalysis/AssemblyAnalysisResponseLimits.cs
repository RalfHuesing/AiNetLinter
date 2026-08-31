#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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

    internal static int NormalizeDiagnosticLimit(int requested) =>
        requested <= 0 ? DefaultMaxDiagnostics : Math.Clamp(requested, 1, MaxDiagnostics);

    internal static AssemblyDiagnosticsSummary WithoutSamples(AssemblyDiagnosticsSummary summary) =>
        summary with
        {
            Root = summary.Root with { Samples = Array.Empty<string>() },
            Transitive = summary.Transitive with { Samples = Array.Empty<string>() },
            Samples = Array.Empty<string>(),
        };

    internal static AssemblyDiagnosticsSummary ProjectDiagnostics(
        IEnumerable<string>? rootDiagnostics,
        IEnumerable<string>? transitiveDiagnostics,
        int requestedLimit = DefaultMaxDiagnostics)
    {
        var limit = NormalizeDiagnosticLimit(requestedLimit);
        var root = Normalize(rootDiagnostics);
        var transitive = Normalize(transitiveDiagnostics);
        var all = root.Concat(transitive).Distinct(StringComparer.Ordinal).ToList();
        var samples = SelectRepresentativeSamples(root, transitive, limit);
        var allSummary = CreateSummary(all, limit, samples);
        return new(
            CreateSummary(root, limit),
            CreateSummary(transitive, limit),
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

    internal static string? NormalizeForDisplay(string? diagnostic)
    {
        if (string.IsNullOrWhiteSpace(diagnostic)) return diagnostic;
        var normalized = NormalizeMessage(diagnostic);
        return normalized.Length <= MaxDiagnosticCharacters
            ? normalized
            : normalized[..(MaxDiagnosticCharacters - 1)] + "…";
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
        IReadOnlyList<string>? selectedSamples = null)
    {
        bool messageTruncated;
        IReadOnlyList<string> samples;
        if (selectedSamples is null)
        {
            samples = SelectSamples(diagnostics, limit, out messageTruncated);
        }
        else
        {
            samples = selectedSamples;
            messageTruncated = diagnostics.Any(diagnostic => diagnostic.Length > MaxDiagnosticCharacters);
        }
        var truncatedBy = new List<string>();
        if (diagnostics.Count > limit) truncatedBy.Add("maxDiagnostics");
        if (messageTruncated) truncatedBy.Add("messageLength");
        if (samples.Count < Math.Min(diagnostics.Count, limit)) truncatedBy.Add("maxDiagnosticBytes");
        return new(
            diagnostics.Count,
            samples.Count,
            truncatedBy.Count > 0,
            samples,
            truncatedBy);
    }

    private static IReadOnlyList<string> SelectRepresentativeSamples(
        IReadOnlyList<string> root,
        IReadOnlyList<string> transitive,
        int limit)
    {
        if (transitive.Count == 0) return SelectSamples(root, limit, out _);
        if (root.Count == 0) return SelectSamples(transitive, limit, out _);

        var rootCount = Math.Min(root.Count, Math.Max(1, limit - 1));
        var selected = root.Take(rootCount).Concat(transitive.Take(limit - rootCount)).ToList();
        return SelectSamples(selected, limit, out _);
    }

    private static IReadOnlyList<string> SelectSamples(
        IReadOnlyList<string> diagnostics,
        int limit,
        out bool messageTruncated)
    {
        messageTruncated = diagnostics.Any(diagnostic => diagnostic.Length > MaxDiagnosticCharacters);
        var samples = new List<string>(Math.Min(limit, diagnostics.Count));
        var bytes = 0;
        foreach (var diagnostic in diagnostics.Take(limit))
        {
            var sample = NormalizeForDisplay(diagnostic)!;
            var sampleBytes = Encoding.UTF8.GetByteCount(sample);
            if (bytes + sampleBytes > MaxDiagnosticBytes) break;
            samples.Add(sample);
            bytes += sampleBytes;
        }

        return samples;
    }

    private static string NormalizeMessage(string diagnostic) =>
        string.Join(' ', diagnostic.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
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
