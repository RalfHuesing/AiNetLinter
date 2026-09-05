#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using AiNetLinter.Baseline;

namespace AiNetLinter.Mcp.Tools.AssemblyAnalysis;

internal sealed record AssemblySearchArguments(
    string? Pattern,
    bool? IsRegex = null,
    string? SearchKind = null,
    int MaxResults = 50,
    int MaxFiles = 0,
    int ContextLines = 0,
    int MaxResponseBytes = 0,
    string? FileFilter = null,
    string? Cursor = null,
    string? ContinuationToken = null,
    bool DeclarationOnly = false,
    string? Kind = null)
{
    internal string? EffectiveCursor => Cursor ?? ContinuationToken;
}

internal sealed class AssemblySearchAccumulator(FileSystemEnumerationResult enumeration)
{
    internal IReadOnlyList<string> Files { get; } = enumeration.Files.ToArray();
    internal int EnumerationErrorCount { get; } = enumeration.ErrorCount;
    internal List<AssemblySearchMatch> Matches { get; } = [];
    internal HashSet<string> MatchedFiles { get; } = new(StringComparer.OrdinalIgnoreCase);
    internal int SkippedBinary { get; set; }
    internal int SkippedUnreadable { get; set; }
    internal bool CancellationRequested { get; set; }
    internal bool RegexTimedOut { get; set; }
    internal bool Stop => CancellationRequested || RegexTimedOut;
}

internal sealed record AssemblySearchFileParameters(
    string Root,
    string FilePath,
    AssemblySearchArguments Arguments,
    string Pattern,
    Regex Regex,
    AssemblyFileFilter? FileFilter,
    AssemblySearchAccumulator Accumulator,
    CancellationToken CancellationToken)
{
    internal int ContextLines => Arguments.ContextLines;
}

internal sealed record AssemblySearchSelection(
    int TotalCount,
    AssemblySearchMatch[] VisibleMatches,
    int NextOffset,
    bool IsTruncated,
    bool MaxFilesTruncated,
    bool HasMoreVisibleMatches);

internal sealed record AssemblySearchPayload(
    string SearchKind,
    string Query,
    string Root,
    string Scope,
    IReadOnlyList<AssemblySearchMatch> Results,
    int TotalCount,
    int ReturnedCount,
    bool IsTruncated,
    string Completeness,
    IReadOnlyList<string> TruncatedBy,
    string? ContinuationToken,
    int MatchedFileCount,
    int ReturnedFileCount,
    int SkippedBinaryFileCount,
    int SkippedUnreadableFileCount,
    int EnumerationErrorCount,
    string? DetailHint);

internal sealed record AssemblySearchMatch(
    string Id,
    string FilePath,
    int Line,
    IReadOnlyList<AssemblySearchMatchRange> MatchRanges,
    string LineText,
    IReadOnlyList<string> ContextBefore,
    IReadOnlyList<string> ContextAfter);

internal sealed record AssemblySearchMatchRange(int Column, int Length);
