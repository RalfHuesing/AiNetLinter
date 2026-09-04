#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Baseline;
using AiNetLinter.Mcp.Assemblies.Analysis.References;
using AiNetLinter.Mcp.Tools.Analysis;
using AiNetLinter.Mcp.Tools.FileStructure;
using AiNetLinter.Output;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Tools.AssemblyAnalysis;

internal static class AssemblySearchTool
{
    internal const string TextSearchKind = "text";
    internal const string DataAccessSearchKind = "data_access";
    internal const string ExternalCallsSearchKind = "external_calls";
    internal const int DefaultMaxResults = 50;
    internal const int MaxResultsCap = 1_000;
    internal const int MaxContextLines = 5;
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(100);
    private static readonly RegexOptions SearchRegexOptions =
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant;
    private static readonly string[] MinifiedMarkers = [".min.", ".bundle."];
    internal static readonly IReadOnlyDictionary<string, string> BuiltInPatterns =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [DataAccessSearchKind] =
                @"\b(DbContext|DbSet|IDbConnection|DbCommand|SqlConnection|NpgsqlConnection|MySqlConnection|SqliteConnection|Execute(?:Reader|NonQuery|Scalar|Sql|SqlRaw|Interpolated|Async)?|FromSql(?:Raw|Interpolated)?|SaveChanges(?:Async)?|BeginTransaction(?:Async)?|TransactionScope|Dapper|DataContext|SELECT\s+.*?\s+FROM|INSERT\s+INTO|UPDATE\s+.*?\s+SET|DELETE\s+FROM|EXEC(?:UTE)?\s+[a-zA-Z0-9_#]+|File\.(?:Read|Write|Open)\w*|Directory\.\w+)\b",
            [ExternalCallsSearchKind] =
                @"\b(HttpClient|HttpRequestMessage|WebClient|RestClient|GrpcChannel|ChannelBase|Socket|TcpClient|Process\.Start|Assembly\.Load)\b",
        };

    internal static async Task<CallToolResult> ExecuteAsync(
        AssemblyAnalysisLease lease,
        AssemblySearchArguments arguments,
        CancellationToken cancellationToken)
    {
        var validation = Validate(arguments);
        if (validation is not null) return validation;

        var root = AssemblyGetFileTreeTool.ResolveRoot(lease);
        if (root is null)
        {
            return AssemblyAnalysisResponse.Unsupported(lease.CanonicalPath);
        }

        try
        {
            var payload = await Task.Run(
                () => Scan(root, arguments, cancellationToken),
                cancellationToken).ConfigureAwait(false);
            var text = RenderText(payload);
            return McpToolResults.Text(text, new { assemblySearch = payload });
        }
        catch (ArgumentException exception)
        {
            return McpToolResults.Recoverable(
                LinterErrorCodes.InvalidArgument,
                exception.Message,
                context: lease.CanonicalPath,
                hint: "searchKind, pattern und fileFilter gemaess search_assembly-Vertrag korrigieren.");
        }
    }

    private static CallToolResult? Validate(AssemblySearchArguments arguments)
    {
        var kind = NormalizeKind(arguments.SearchKind);
        return ValidateKind(kind, arguments.Pattern)
            ?? ValidateLimits(arguments)
            ?? ValidateCursor(arguments.Cursor);
    }

    private static CallToolResult? ValidateKind(string? kind, string? pattern)
    {
        if (kind is null)
        {
            return McpToolResults.Recoverable(
                LinterErrorCodes.InvalidArgument,
                "searchKind muss text, data_access oder external_calls sein.",
                hint: "searchKind waehlen; fuer text ein pattern angeben.");
        }

        return kind == TextSearchKind && string.IsNullOrWhiteSpace(pattern)
            ? McpToolResults.Recoverable(
                LinterErrorCodes.InvalidArgument,
                "pattern darf bei searchKind=text nicht leer sein.",
                hint: "pattern angeben oder searchKind=data_access beziehungsweise external_calls waehlen.")
            : null;
    }

    private static CallToolResult? ValidateLimits(AssemblySearchArguments arguments)
    {
        if (arguments.MaxResults < 0 || arguments.MaxFiles < 0 || arguments.ContextLines < 0
            || arguments.MaxResponseBytes < 0)
        {
            return McpToolResults.InvalidArgument(
                "maxResults, maxFiles, contextLines und maxResponseBytes duerfen nicht negativ sein.");
        }

        if (arguments.ContextLines > MaxContextLines)
        {
            return McpToolResults.InvalidArgument(
                $"contextLines muss zwischen 0 und {MaxContextLines} liegen.");
        }

        return arguments.MaxResults > MaxResultsCap || arguments.MaxFiles > GetFileTreeTool.MaxResultsCap
            ? McpToolResults.InvalidArgument(
                $"maxResults muss zwischen 0 und {MaxResultsCap} und maxFiles zwischen 0 und {GetFileTreeTool.MaxResultsCap} liegen.")
            : null;
    }

    private static CallToolResult? ValidateCursor(string? cursor)
    {
        return string.IsNullOrWhiteSpace(cursor)
            || int.TryParse(cursor, out var offset) && offset >= 0
            ? null
            : McpToolResults.InvalidArgument("cursor muss ein nichtnegativer numerischer Offset sein.");
    }

    private static AssemblySearchPayload Scan(
        string root,
        AssemblySearchArguments arguments,
        CancellationToken cancellationToken)
    {
        var kind = NormalizeKind(arguments.SearchKind)!;
        var pattern = ResolvePattern(kind, arguments.Pattern);
        var regex = CreateRegex(pattern, arguments.IsRegex || arguments.Pattern is null);
        var fileFilter = AssemblyFileFilter.Create(arguments.FileFilter, "fileFilter");
        var accumulator = ScanFiles(root, arguments, pattern, regex, fileFilter, cancellationToken);
        return BuildPayload(kind, pattern, arguments, accumulator);
    }

    private static AssemblySearchPayload BuildPayload(
        string kind,
        string pattern,
        AssemblySearchArguments arguments,
        AssemblySearchAccumulator accumulator)
    {
        var orderedMatches = accumulator.Matches
            .OrderBy(match => match.FilePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(match => match.Line)
            .ThenBy(match => match.Id, StringComparer.Ordinal)
            .ToArray();
        var selection = SelectMatches(orderedMatches, accumulator.MatchedFiles.Count, arguments);
        var reasons = BuildTruncationReasons(selection, accumulator);
        var completeness = GetCompleteness(reasons, accumulator);
        // maxFiles is an explicit scope limit, not a page over the hidden
        // files. Once the selected file scope is exhausted there is no
        // forward cursor to offer; the hint tells the caller to increase the
        // scope instead of returning the same offset forever.
        var continuationToken = selection.HasMoreVisibleMatches
            ? AssemblyPaging.CreateToken(selection.NextOffset)
            : null;
        return new AssemblySearchPayload(
            kind,
            arguments.Pattern ?? pattern,
            ".",
            "assembly-source-root",
            selection.VisibleMatches,
            selection.TotalCount,
            selection.VisibleMatches.Length,
            selection.IsTruncated,
            completeness,
            reasons,
            continuationToken,
            accumulator.MatchedFiles.Count,
            selection.VisibleMatches.Select(match => match.FilePath).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            accumulator.SkippedBinary,
            accumulator.SkippedUnreadable,
            accumulator.EnumerationErrorCount,
            BuildHint(selection.MaxFilesTruncated, selection.IsTruncated));
    }

    internal static AssemblySearchSelection SelectMatches(
        IReadOnlyList<AssemblySearchMatch> orderedMatches,
        int matchedFileCount,
        AssemblySearchArguments arguments)
    {
        var visibleFileSet = arguments.MaxFiles > 0
            ? orderedMatches.Select(match => match.FilePath).Distinct(StringComparer.OrdinalIgnoreCase).Take(arguments.MaxFiles).ToHashSet(StringComparer.OrdinalIgnoreCase)
            : null;
        var fileLimitedMatches = visibleFileSet is null
            ? orderedMatches
            : orderedMatches.Where(match => visibleFileSet.Contains(match.FilePath)).ToArray();
        var offset = AssemblyPaging.ReadOffset(arguments.Cursor);
        var maxResults = arguments.MaxResults == 0 ? DefaultMaxResults : arguments.MaxResults;
        var visibleMatches = fileLimitedMatches.Skip(offset).Take(maxResults).ToArray();
        var maxFilesTruncated = visibleFileSet is not null && matchedFileCount > visibleFileSet.Count;
        return new(
            fileLimitedMatches.Count,
            visibleMatches,
            offset + visibleMatches.Length,
            offset + visibleMatches.Length < fileLimitedMatches.Count || maxFilesTruncated,
            maxFilesTruncated,
            offset + visibleMatches.Length < fileLimitedMatches.Count);
    }

    private static List<string> BuildTruncationReasons(
        AssemblySearchSelection selection,
        AssemblySearchAccumulator accumulator)
    {
        var reasons = new List<string>();
        if (selection.NextOffset < selection.TotalCount) reasons.Add("maxResults");
        if (selection.MaxFilesTruncated) reasons.Add("maxFiles");
        if (accumulator.EnumerationErrorCount > 0) reasons.Add("enumerationErrors");
        if (accumulator.SkippedUnreadable > 0) reasons.Add("unreadableFiles");
        if (accumulator.CancellationRequested) reasons.Add("cancellation");
        if (accumulator.RegexTimedOut) reasons.Add("regexTimeout");
        return reasons;
    }

    private static string GetCompleteness(
        IReadOnlyList<string> reasons,
        AssemblySearchAccumulator accumulator) =>
        reasons.Contains("maxResults", StringComparer.Ordinal)
            || reasons.Contains("maxFiles", StringComparer.Ordinal)
            ? "truncated"
            : accumulator.EnumerationErrorCount > 0 || accumulator.SkippedUnreadable > 0
                || accumulator.CancellationRequested || accumulator.RegexTimedOut
                ? "partial"
                : "complete";

    private static string? BuildHint(bool maxFilesTruncated, bool truncated) =>
        maxFilesTruncated
            ? "maxFiles erhoehen, um weitere Dateien in den sichtbaren Suchscope aufzunehmen."
            : truncated
                ? "continuationToken mit derselben Suchanfrage verwenden oder maxResults erhoehen."
                : null;

    private static AssemblySearchAccumulator ScanFiles(
        string root,
        AssemblySearchArguments arguments,
        string pattern,
        Regex regex,
        AssemblyFileFilter? fileFilter,
        CancellationToken cancellationToken)
    {
        var accumulator = new AssemblySearchAccumulator(
            FileSystemExclusionHelpers.SafeEnumerateFilesWithErrors(root, cancellationToken));
        foreach (var filePath in accumulator.Files
                     .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            if (cancellationToken.IsCancellationRequested)
            {
                accumulator.CancellationRequested = true;
                break;
            }

            ScanFile(new(root, filePath, arguments, pattern, regex, fileFilter, accumulator, cancellationToken));
            if (accumulator.Stop) break;
        }

        return accumulator;
    }

    private static void ScanFile(AssemblySearchFileParameters options)
    {
        var relativePath = NormalizeRelativePath(Path.GetRelativePath(options.Root, options.FilePath));
        if (ShouldSkip(relativePath, options.FileFilter)) return;
        var read = SearchPatternScanner.TryReadLines(options.FilePath);
        if (read.Status == SearchFileReadStatus.Binary)
        {
            options.Accumulator.SkippedBinary++;
            return;
        }

        if (read.Status == SearchFileReadStatus.Unreadable)
        {
            options.Accumulator.SkippedUnreadable++;
            return;
        }

        ScanLines(relativePath, read.Lines, options, options.Accumulator);
    }

    private static void ScanLines(
        string relativePath,
        IReadOnlyList<string> lines,
        AssemblySearchFileParameters options,
        AssemblySearchAccumulator accumulator)
    {
        for (var index = 0; index < lines.Count; index++)
        {
            if (options.CancellationToken.IsCancellationRequested)
            {
                accumulator.CancellationRequested = true;
                return;
            }

            try
            {
                var ranges = FindRanges(lines[index], options.Regex);
                if (ranges.Count == 0) continue;
                accumulator.MatchedFiles.Add(relativePath);
                accumulator.Matches.Add(CreateMatch(relativePath, lines, index, ranges, options.ContextLines, options.Pattern));
            }
            catch (RegexMatchTimeoutException)
            {
                accumulator.RegexTimedOut = true;
                return;
            }
        }
    }

    private static bool ShouldSkip(string relativePath, AssemblyFileFilter? fileFilter) =>
        FileSystemExclusionHelpers.IsSearchExcludedRelativePath(relativePath)
        || IsMinified(relativePath)
        || fileFilter is not null && !fileFilter.IsMatch(relativePath);

    private static Regex CreateRegex(string pattern, bool useRegex) =>
        useRegex
            ? new Regex(pattern, SearchRegexOptions, RegexTimeout)
            : new Regex(Regex.Escape(pattern), SearchRegexOptions, RegexTimeout);

    private static string ResolvePattern(string kind, string? pattern) =>
        string.IsNullOrWhiteSpace(pattern) ? BuiltInPatterns[kind] : pattern;

    private static string? NormalizeKind(string? kind)
    {
        var normalized = string.IsNullOrWhiteSpace(kind) ? TextSearchKind : kind.Trim().ToLowerInvariant();
        return normalized is TextSearchKind or DataAccessSearchKind or ExternalCallsSearchKind ? normalized : null;
    }

    private static IReadOnlyList<AssemblySearchMatchRange> FindRanges(string line, Regex regex)
    {
        return regex.Matches(line)
            .Select(match => new AssemblySearchMatchRange(match.Index + 1, match.Length))
            .ToArray();
    }

    private static AssemblySearchMatch CreateMatch(
        string relativePath,
        IReadOnlyList<string> lines,
        int lineIndex,
        IReadOnlyList<AssemblySearchMatchRange> ranges,
        int contextLines,
        string pattern)
    {
        var beforeStart = Math.Max(0, lineIndex - contextLines);
        var afterEnd = Math.Min(lines.Count, lineIndex + contextLines + 1);
        var idInput = $"{relativePath}\n{lineIndex + 1}\n{pattern}";
        var id = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(idInput))).ToLowerInvariant()[..16];
        return new AssemblySearchMatch(
            $"asm-search:{id}",
            relativePath,
            lineIndex + 1,
            ranges,
            lines[lineIndex],
            lines.Skip(beforeStart).Take(lineIndex - beforeStart).ToArray(),
            lines.Skip(lineIndex + 1).Take(afterEnd - lineIndex - 1).ToArray());
    }

    private static string RenderText(AssemblySearchPayload payload)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Assembly-Suche: {payload.SearchKind}; {payload.ReturnedCount} von {payload.TotalCount}");
        builder.AppendLine($"Scope: {payload.Scope}; Vollständigkeit: {payload.Completeness}");
        foreach (var match in payload.Results)
        {
            builder.AppendLine($"{match.FilePath}:{match.Line}: {match.LineText}");
        }

        if (payload.IsTruncated)
        {
            builder.AppendLine($"Ergebnis gekürzt ({string.Join(", ", payload.TruncatedBy)}); " +
                               (payload.ContinuationToken is null ? payload.DetailHint :
                               $"continuationToken={payload.ContinuationToken}; {payload.DetailHint}"));
        }

        return builder.ToString().TrimEnd();
    }

    private static bool IsMinified(string relativePath) =>
        MinifiedMarkers.Any(marker => relativePath.Contains(marker, StringComparison.OrdinalIgnoreCase));

    private static string NormalizeRelativePath(string path)
    {
        var normalized = path.Replace('\\', '/').Trim('/');
        return string.IsNullOrEmpty(normalized) ? "." : normalized;
    }
}

internal sealed record AssemblySearchArguments(
    string? Pattern,
    bool IsRegex,
    string? SearchKind,
    int MaxResults,
    int MaxFiles,
    int ContextLines,
    int MaxResponseBytes,
    string? FileFilter,
    string? Cursor);

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
