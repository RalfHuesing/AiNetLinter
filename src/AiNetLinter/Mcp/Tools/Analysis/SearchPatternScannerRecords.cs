#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.CodeAnalysis;

namespace AiNetLinter.Mcp.Tools.Analysis;

internal sealed record SearchPatternToolArguments(
    string? Pattern,
    bool IsRegex,
    int MaxResults,
    int MaxFiles,
    int ContextLines,
    int MaxResponseBytes,
    string? Scope,
    string[]? IncludePatterns,
    string[]? ExcludePatterns);

internal sealed record SearchPatternScannerParameters(
    Solution Solution,
    string Pattern,
    bool IsRegex,
    int MaxResults,
    int MaxFiles,
    int ContextLines,
    int MaxResponseBytes,
    string? Scope,
    IReadOnlyList<string>? IncludePatterns,
    IReadOnlyList<string>? ExcludePatterns,
    CancellationToken CancellationToken);

internal sealed record SearchPatternScanResult(
    SearchPatternPayload Payload,
    int TotalMatchedLineCount,
    int MaxResults,
    int MaxFiles,
    int MaxResponseBytes);

internal sealed record SearchPatternFileScanResult(
    SearchPatternFileScanStatus Status,
    SearchPatternFileMatches? Matches,
    bool RegexTimedOut,
    bool CancellationRequested);

internal sealed record SearchPatternLegacyFileHitScanResult(
    IReadOnlyList<string> Files,
    int FileReadErrorCount,
    bool RegexTimedOut)
{
    internal bool HasErrors => FileReadErrorCount > 0 || RegexTimedOut;
}

internal sealed record SearchPatternLegacyFileMatchResult(
    bool Matches,
    bool FileReadError,
    bool RegexTimedOut);

internal sealed record SearchPatternFileMatches(
    string RelativePath,
    string? ProjectName,
    string[] Content,
    IReadOnlyList<SearchPatternLineMatch> Lines);

internal sealed record SearchPatternLineMatch(
    int LineNumber,
    IReadOnlyList<SearchPatternMatchRange> Ranges);

internal sealed record SearchPatternLineScanResult(
    IReadOnlyList<SearchPatternLineMatch> Matches,
    bool RegexTimedOut,
    bool CancellationRequested);

internal enum SearchPatternFileScanStatus
{
    Skipped,
    Binary,
    Unreadable,
    Matched,
}

internal enum SearchFileReadStatus
{
    Read,
    Binary,
    Unreadable,
}

internal sealed record SearchFileReadResult(SearchFileReadStatus Status, string[] Lines);

internal sealed record SearchPatternCompletenessOptions(
    int TotalLines,
    int MatchedFiles,
    IReadOnlyList<SearchPatternMatch> VisibleMatches,
    int VisibleFiles,
    IReadOnlyList<string> Reasons,
    int SkippedBinary,
    int SkippedUnreadable,
    int EnumerationErrors,
    bool RegexTimedOut,
    bool CancellationRequested);

internal sealed record SearchPatternTruncationOptions(
    int TotalLines,
    int MaxResults,
    int MatchedFiles,
    int MaxFiles,
    int VisibleFileLines,
    int EnumerationErrors,
    bool RegexTimedOut,
    bool CancellationRequested);

internal sealed record SearchPatternResponseBudgetParameters(
    IReadOnlyList<SearchPatternMatch> Matches,
    SearchPatternScannerParameters ScannerParameters,
    SearchPatternCompleteness Completeness,
    IReadOnlyList<string> Reasons,
    string EffectiveScope,
    IReadOnlyList<string> IncludePatterns,
    IReadOnlyList<string> ExcludePatterns);

internal sealed record SearchPatternResponseBudgetResult(
    IReadOnlyList<SearchPatternMatch> Matches,
    SearchPatternCompleteness Completeness);

internal sealed record SearchPatternScanFlags
{
    internal bool RegexTimedOut { get; init; }
    internal bool CancellationRequested { get; init; }
    internal bool Stop => RegexTimedOut || CancellationRequested;
}

internal sealed record SearchPatternScanFileParameters(
    string FilePath,
    string SolutionRoot,
    SearchPatternScannerParameters ScannerParameters,
    string EffectiveScope,
    IReadOnlyList<string> IncludePatterns,
    IReadOnlyList<string> ExcludePatterns,
    System.Text.RegularExpressions.Regex? Regex);

internal sealed record SearchPatternAggregationParameters(
    SearchPatternScannerParameters ScannerParameters,
    string EffectiveScope,
    IReadOnlyList<string> IncludePatterns,
    IReadOnlyList<string> ExcludePatterns,
    IReadOnlyList<SearchPatternFileMatches> Files,
    int SkippedBinary,
    int SkippedUnreadable,
    int EnumerationErrors,
    SearchPatternScanFlags ScanFlags);

internal sealed record SearchPatternFileScanAggregation(
    List<SearchPatternFileMatches> Files,
    int SkippedBinary,
    int SkippedUnreadable,
    int EnumerationErrors,
    SearchPatternScanFlags ScanFlags);

internal sealed record SearchPatternScanFilesParameters(
    string SolutionRoot,
    SearchPatternScannerParameters ScannerParameters,
    string EffectiveScope,
    IReadOnlyList<string> IncludePatterns,
    IReadOnlyList<string> ExcludePatterns,
    System.Text.RegularExpressions.Regex? Regex);

internal sealed record SearchPatternPayload(
    IReadOnlyList<SearchPatternMatch> Matches,
    SearchPatternCompleteness Completeness,
    SearchPatternScopeMetadata Scope,
    SearchPatternSnapshotMetadata Snapshot);

internal sealed record SearchPatternMatch(
    string FilePath,
    int Line,
    IReadOnlyList<SearchPatternMatchRange> MatchRanges,
    string LineText,
    IReadOnlyList<string> ContextBefore,
    IReadOnlyList<string> ContextAfter,
    string? ProjectName);

internal sealed record SearchPatternMatchRange(int Column, int Length);

internal sealed record SearchPatternCompleteness(
    bool ScanCompleted,
    int MatchedFileCount,
    int ShownMatchedFileCount,
    int TotalMatchedLineCount,
    int ShownMatchedLineCount,
    bool Truncated,
    IReadOnlyList<string> TruncatedBy,
    int SkippedBinaryFileCount,
    int SkippedUnreadableFileCount,
    int EnumerationErrorCount,
    bool CancellationRequested,
    bool RegexTimedOut);

internal sealed record SearchPatternScopeMetadata(
    string SolutionRoot,
    string EffectiveScope,
    string? RequestedScope,
    IReadOnlyList<string> IncludePatterns,
    IReadOnlyList<string> ExcludePatterns,
    IReadOnlyList<string> DefaultExclusions);

internal sealed record SearchPatternSnapshotMetadata(
    string Source,
    string? SolutionFileName,
    int ProjectCount);
