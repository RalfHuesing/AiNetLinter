#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AiNetLinter.Mcp.Tools.AssemblyAnalysis;

internal static partial class AssemblyAnalysisResponseLimits
{
    internal const int DefaultMaxDiagnostics = 20;
    internal const int MaxDiagnostics = 50;
    internal const int MaxDiagnosticCharacters = 256;
    internal const int MaxDiagnosticBytes = 4 * 1024;
    internal const int MaxReferences = 32;
    internal const int MaxReferenceSessions = 32;
    internal const int MaxSessionDiagnostics = 3;
    internal const int DefaultResponseBytes = 16 * 1024;

    internal const int MaxResponseBytes = 32 * 1024;

    internal static int NormalizeResponseBudget(int requested) =>
        requested <= 0 ? DefaultResponseBytes : Math.Clamp(requested, 1, MaxResponseBytes);

    internal static int ResolveResponseBudget(int requested, string? detailLevel, int configuredDefault = DefaultResponseBytes)
    {
        if (requested > 0) return NormalizeResponseBudget(requested);
        var standard = Math.Clamp(configuredDefault, 1, MaxResponseBytes);
        return detailLevel?.Trim().ToLowerInvariant() switch
        {
            "compact" => Math.Min(DefaultResponseBytes, standard),
            "full" => MaxResponseBytes,
            _ => standard,
        };
    }

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
        IEnumerable<AssemblyReferenceSession>? sessions,
        bool includeDetails = true)
    {
        var referenceCount = references?.Count() ?? 0;
        var sessionCount = sessions?.Count() ?? 0;
        return new(
            referenceCount,
            includeDetails ? Math.Min(referenceCount, MaxReferences) : 0,
            includeDetails ? referenceCount > MaxReferences : referenceCount > 0,
            sessionCount,
            includeDetails ? Math.Min(sessionCount, MaxReferenceSessions) : 0,
            includeDetails ? sessionCount > MaxReferenceSessions : sessionCount > 0);
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
