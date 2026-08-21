#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using AiNetLinter.Mcp;

namespace AiNetLinter.Mcp.Tools.Analysis;

internal static class SearchPatternScannerCompleteness
{
    internal static SearchPatternCompleteness Create(SearchPatternCompletenessOptions options) =>
        new(
            !options.RegexTimedOut && !options.CancellationRequested && options.EnumerationErrors == 0,
            options.MatchedFiles,
            options.VisibleFiles,
            options.TotalLines,
            options.VisibleMatches.Count,
            options.Reasons.Count > 0,
            options.Reasons,
            options.SkippedBinary,
            options.SkippedUnreadable,
            options.EnumerationErrors,
            options.CancellationRequested,
            options.RegexTimedOut);

    internal static SearchPatternCompleteness MarkCancellation(
        SearchPatternCompleteness completeness)
    {
        var reasons = completeness.TruncatedBy.Contains("cancellation", StringComparer.Ordinal)
            ? completeness.TruncatedBy
            : completeness.TruncatedBy.Concat(["cancellation"]).ToArray();
        return completeness with
        {
            ScanCompleted = false,
            Truncated = true,
            TruncatedBy = reasons,
            CancellationRequested = true,
        };
    }

    internal static IReadOnlyList<string> DetermineTruncationReasons(SearchPatternTruncationOptions options)
    {
        var reasons = new List<string>();
        if (options.MaxResults > 0 && options.VisibleFileLines > options.MaxResults) reasons.Add("maxResults");
        if (options.MaxFiles > 0 && options.MatchedFiles > options.MaxFiles) reasons.Add("maxFiles");
        if (options.EnumerationErrors > 0) reasons.Add("enumerationError");
        if (options.RegexTimedOut) reasons.Add("regexTimeout");
        if (options.CancellationRequested) reasons.Add("cancellation");
        return reasons;
    }

    internal static SearchPatternResponseBudgetResult ApplyResponseBudget(
        SearchPatternResponseBudgetParameters options)
    {
        var visible = options.Matches.ToList();
        var exceeded = ExceedsResponseBudget(options with { Matches = visible });
        while (visible.Count > 0 && exceeded)
        {
            visible.RemoveAt(visible.Count - 1);
            exceeded = ExceedsResponseBudget(options with { Matches = visible });
        }

        var reasons = exceeded || visible.Count != options.Matches.Count
            ? AddReason(options.Reasons, "maxResponseBytes")
            : options.Reasons;
        var completeness = Create(new(
            options.Completeness.TotalMatchedLineCount,
            options.Completeness.MatchedFileCount,
            visible,
            visible.Select(match => match.FilePath).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            reasons,
            options.Completeness.SkippedBinaryFileCount,
            options.Completeness.SkippedUnreadableFileCount,
            options.Completeness.EnumerationErrorCount,
            options.Completeness.RegexTimedOut,
            options.Completeness.CancellationRequested));
        return new SearchPatternResponseBudgetResult(visible, completeness);
    }

    private static bool ExceedsResponseBudget(SearchPatternResponseBudgetParameters options)
    {
        if (options.ScannerParameters.MaxResponseBytes <= 0) return false;
        var shown = Create(new(
            options.Completeness.TotalMatchedLineCount,
            options.Completeness.MatchedFileCount,
            options.Matches,
            options.Matches.Select(match => match.FilePath).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            AddReason(options.Reasons, "maxResponseBytes"),
            options.Completeness.SkippedBinaryFileCount,
            options.Completeness.SkippedUnreadableFileCount,
            options.Completeness.EnumerationErrorCount,
            options.Completeness.RegexTimedOut,
            options.Completeness.CancellationRequested));
        var payload = new SearchPatternPayload(
            options.Matches,
            shown,
            new SearchPatternScopeMetadata(
                ".",
                options.EffectiveScope,
                options.ScannerParameters.Scope,
                options.IncludePatterns,
                options.ExcludePatterns,
                SearchPatternScanner.DefaultExclusions),
            new SearchPatternSnapshotMetadata(
                "resident-solution",
                Path.GetFileName(options.ScannerParameters.Solution.FilePath),
                options.ScannerParameters.Solution.ProjectIds.Count));
        return JsonSerializer.SerializeToUtf8Bytes(payload, McpJsonOptions.Default).Length
            > options.ScannerParameters.MaxResponseBytes;
    }

    private static IReadOnlyList<string> AddReason(IReadOnlyList<string> reasons, string reason) =>
        reasons.Contains(reason, StringComparer.Ordinal)
            ? reasons
            : reasons.Concat([reason]).ToArray();
}
