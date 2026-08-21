#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using AiNetLinter.Baseline;
using AiNetLinter.Mcp;
using Microsoft.CodeAnalysis;

namespace AiNetLinter.Mcp.Tools.Analysis;

internal static class SearchPatternScanner
{
    private const RegexOptions CompiledIgnoreCase =
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant;
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(100);
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    internal static readonly string[] DefaultExclusions =
    [
        ".git", ".hg", ".svn", ".vs", ".idea", "obj", "bin", "node_modules", "worktrees",
        ".worktrees", "TestResults", "artifacts", "coverage", "temp", "packages", "*.min.*",
        "binary files",
    ];

    internal static string SearchAndFormat(
        Solution solution,
        string pattern,
        bool isRegex,
        int maxResults)
    {
        var parameters = new SearchPatternScannerParameters(
            solution, pattern, isRegex, maxResults, 0, 0, 0, null, null, null, default);
        return SearchPatternLegacyFormatter.Format(Scan(parameters));
    }

    internal static SearchPatternScanResult Scan(SearchPatternScannerParameters parameters)
    {
        var solutionRoot = GetSolutionRoot(parameters.Solution);
        var effectiveScope = NormalizeScope(parameters.Scope, solutionRoot);
        var includes = NormalizePatterns(parameters.IncludePatterns, "includePatterns");
        var excludes = NormalizePatterns(parameters.ExcludePatterns, "excludePatterns");
        var regex = parameters.IsRegex
            ? new Regex(parameters.Pattern, CompiledIgnoreCase, RegexTimeout)
            : null;
        var aggregation = ScanFiles(new(
            solutionRoot,
            parameters,
            effectiveScope,
            includes,
            excludes,
            regex));

        return BuildScanResult(new(
            parameters,
            effectiveScope,
            includes,
            excludes,
            aggregation.Files,
            aggregation.SkippedBinary,
            aggregation.SkippedUnreadable,
            aggregation.EnumerationErrors,
            aggregation.ScanFlags));
    }

    private static SearchPatternFileScanAggregation ScanFiles(SearchPatternScanFilesParameters options)
    {
        var aggregation = new SearchPatternFileScanAggregation(
            new List<SearchPatternFileMatches>(),
            0,
            0,
            0,
            new SearchPatternScanFlags());
        var seenFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var enumeration = FileSystemExclusionHelpers.SafeEnumerateFilesWithErrors(
            options.SolutionRoot,
            options.ScannerParameters.CancellationToken);
        var filePaths = new List<string>();
        foreach (var filePath in enumeration.Files)
        {
            filePaths.Add(filePath);
        }

        if (options.ScannerParameters.CancellationToken.IsCancellationRequested)
        {
            aggregation = aggregation with
            {
                ScanFlags = aggregation.ScanFlags with { CancellationRequested = true },
            };
        }

        aggregation = aggregation with { EnumerationErrors = enumeration.ErrorCount };
        foreach (var filePath in filePaths.OrderBy(path => path, StringComparer.Ordinal))
        {
            if (aggregation.ScanFlags.Stop) break;
            if (options.ScannerParameters.CancellationToken.IsCancellationRequested)
            {
                aggregation = aggregation with
                {
                    ScanFlags = aggregation.ScanFlags with { CancellationRequested = true },
                };
                break;
            }
            if (!seenFiles.Add(filePath)) continue;
            var file = ScanFile(new(
                filePath,
                options.SolutionRoot,
                options.ScannerParameters,
                options.EffectiveScope,
                options.IncludePatterns,
                options.ExcludePatterns,
                options.Regex));
            aggregation = AddFile(aggregation, file);
            if (aggregation.ScanFlags.Stop) break;
        }

        return aggregation;
    }

    private static SearchPatternFileScanAggregation AddFile(
        SearchPatternFileScanAggregation aggregation,
        SearchPatternFileScanResult file)
    {
        if (file.Status == SearchPatternFileScanStatus.Binary) aggregation = aggregation with
        {
            SkippedBinary = aggregation.SkippedBinary + 1,
        };
        if (file.Status == SearchPatternFileScanStatus.Unreadable) aggregation = aggregation with
        {
            SkippedUnreadable = aggregation.SkippedUnreadable + 1,
        };
        if (file.Matches is not null) aggregation.Files.Add(file.Matches);
        return aggregation with
        {
            ScanFlags = aggregation.ScanFlags with
            {
                RegexTimedOut = aggregation.ScanFlags.RegexTimedOut || file.RegexTimedOut,
                CancellationRequested = aggregation.ScanFlags.CancellationRequested || file.CancellationRequested,
            },
        };
    }

    private static SearchPatternScanResult BuildScanResult(SearchPatternAggregationParameters options)
    {
        var totalLines = options.Files.Sum(file => file.Lines.Count);
        var visibleFiles = SelectVisibleFiles(options.Files, options.ScannerParameters.MaxFiles);
        IReadOnlyList<SearchPatternMatch> visibleMatches =
            SelectVisibleMatches(visibleFiles, options.ScannerParameters.MaxResults, options.ScannerParameters.ContextLines);
        var visibleFileLines = visibleFiles.Sum(file => file.Lines.Count);
        var reasons = SearchPatternScannerCompleteness.DetermineTruncationReasons(new(
            totalLines,
            options.ScannerParameters.MaxResults,
            options.Files.Count,
            options.ScannerParameters.MaxFiles,
            visibleFileLines,
            options.EnumerationErrors,
            options.ScanFlags.RegexTimedOut,
            options.ScanFlags.CancellationRequested));
        var completeness = SearchPatternScannerCompleteness.Create(new(
            totalLines,
            options.Files.Count,
            visibleMatches,
            CountVisibleFiles(visibleMatches),
            reasons,
            options.SkippedBinary,
            options.SkippedUnreadable,
            options.EnumerationErrors,
            options.ScanFlags.RegexTimedOut,
            options.ScanFlags.CancellationRequested));
        if (options.ScannerParameters.MaxResponseBytes > 0)
        {
            var budgetResult = SearchPatternScannerCompleteness.ApplyResponseBudget(new(
                visibleMatches,
                options.ScannerParameters,
                completeness,
                reasons,
                options.EffectiveScope,
                options.IncludePatterns,
                options.ExcludePatterns));
            visibleMatches = budgetResult.Matches;
            completeness = budgetResult.Completeness;
        }

        var payload = BuildPayload(
            options.ScannerParameters,
            options.EffectiveScope,
            options.IncludePatterns,
            options.ExcludePatterns,
            visibleMatches,
            completeness);
        return new SearchPatternScanResult(
            payload,
            totalLines,
            options.ScannerParameters.MaxResults,
            options.ScannerParameters.MaxFiles,
            options.ScannerParameters.MaxResponseBytes);
    }

    private static SearchPatternFileScanResult ScanFile(SearchPatternScanFileParameters options)
    {
        var relativePath = ToRelativePath(options.SolutionRoot, options.FilePath);
        if (relativePath is null || !MatchesScope(relativePath, options.EffectiveScope)
            || FileSystemExclusionHelpers.IsSearchExcludedRelativePath(relativePath)
            || FileSystemExclusionHelpers.IsGeneratedPath(options.FilePath)
            || IsMinified(relativePath)
            || !MatchesFilters(relativePath, options.IncludePatterns, options.ExcludePatterns))
        {
            return new(SearchPatternFileScanStatus.Skipped, null, false, false);
        }

        var readResult = TryReadLines(options.FilePath);
        if (readResult.Status == SearchFileReadStatus.Binary)
        {
            return new(SearchPatternFileScanStatus.Binary, null, false, false);
        }

        if (readResult.Status == SearchFileReadStatus.Unreadable)
        {
            return new(SearchPatternFileScanStatus.Unreadable, null, false, false);
        }

        var lineScan = FindLineMatches(readResult.Lines, options.ScannerParameters, options.Regex);
        var matches = lineScan.Matches.Count == 0
            ? null
            : new SearchPatternFileMatches(
                relativePath,
                GetProjectName(options.ScannerParameters.Solution, options.FilePath),
                readResult.Lines,
                lineScan.Matches);
        return new(
            matches is null ? SearchPatternFileScanStatus.Skipped : SearchPatternFileScanStatus.Matched,
            matches,
            lineScan.RegexTimedOut,
            lineScan.CancellationRequested);
    }

    internal static IReadOnlyList<string> GetFilesWithHits(
        Solution solution,
        string pattern,
        bool isRegex)
        => SearchPatternLegacyFileHitScanner.GetFilesWithHits(solution, pattern, isRegex);

    private static SearchPatternPayload BuildPayload(
        SearchPatternScannerParameters parameters,
        string effectiveScope,
        IReadOnlyList<string> includes,
        IReadOnlyList<string> excludes,
        IReadOnlyList<SearchPatternMatch> matches,
        SearchPatternCompleteness completeness) =>
        new(
            matches,
            completeness,
            new SearchPatternScopeMetadata(
                ".",
                effectiveScope,
                parameters.Scope,
                includes,
                excludes,
                DefaultExclusions),
            new SearchPatternSnapshotMetadata(
                "resident-solution",
                Path.GetFileName(parameters.Solution.FilePath),
                parameters.Solution.ProjectIds.Count));

    private static List<SearchPatternFileMatches> SelectVisibleFiles(
        IReadOnlyList<SearchPatternFileMatches> files,
        int maxFiles) =>
        maxFiles > 0 ? files.Take(maxFiles).ToList() : files.ToList();

    private static List<SearchPatternMatch> SelectVisibleMatches(
        IReadOnlyList<SearchPatternFileMatches> files,
        int maxResults,
        int contextLines)
    {
        var matches = files
            .SelectMany(file => file.Lines.Select(line => CreateMatch(file, line, contextLines)))
            .ToList();
        return maxResults > 0 ? matches.Take(maxResults).ToList() : matches;
    }

    private static int CountVisibleFiles(IReadOnlyList<SearchPatternMatch> matches) =>
        matches.Select(match => match.FilePath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

    private static SearchPatternMatch CreateMatch(
        SearchPatternFileMatches file,
        SearchPatternLineMatch line,
        int contextLines)
    {
        var beforeStart = Math.Max(0, line.LineNumber - contextLines - 1);
        var before = file.Content[beforeStart..(line.LineNumber - 1)].ToArray();
        var afterEnd = Math.Min(file.Content.Length, line.LineNumber + contextLines);
        var after = file.Content[line.LineNumber..afterEnd].ToArray();
        return new SearchPatternMatch(
            file.RelativePath,
            line.LineNumber,
            line.Ranges,
            file.Content[line.LineNumber - 1],
            before,
            after,
            file.ProjectName);
    }

    private static SearchPatternLineScanResult FindLineMatches(
        string[] lines,
        SearchPatternScannerParameters parameters,
        Regex? regex)
    {
        var matches = new List<SearchPatternLineMatch>();
        var regexTimedOut = false;
        var cancelled = false;
        for (var index = 0; index < lines.Length; index++)
        {
            if (parameters.CancellationToken.IsCancellationRequested)
            {
                cancelled = true;
                break;
            }

            try
            {
                var ranges = regex is null
                    ? FindPlainRanges(lines[index], parameters.Pattern)
                    : FindRegexRanges(lines[index], regex);
                if (ranges.Count > 0) matches.Add(new SearchPatternLineMatch(index + 1, ranges));
            }
            catch (RegexMatchTimeoutException)
            {
                regexTimedOut = true;
                break;
            }
        }

        return new SearchPatternLineScanResult(matches, regexTimedOut, cancelled);
    }

    private static List<SearchPatternMatchRange> FindPlainRanges(string line, string pattern)
    {
        var ranges = new List<SearchPatternMatchRange>();
        var offset = 0;
        while (offset <= line.Length - pattern.Length)
        {
            var index = line.IndexOf(pattern, offset, StringComparison.OrdinalIgnoreCase);
            if (index < 0) break;
            ranges.Add(new SearchPatternMatchRange(index + 1, pattern.Length));
            offset = index + Math.Max(pattern.Length, 1);
        }

        return ranges;
    }

    private static List<SearchPatternMatchRange> FindRegexRanges(string line, Regex regex) =>
        regex.Matches(line)
            .Select(match => new SearchPatternMatchRange(match.Index + 1, match.Length))
            .ToList();

    internal static SearchFileReadResult TryReadLines(string filePath)
    {
        try
        {
            if (LooksBinary(filePath)) return new(SearchFileReadStatus.Binary, Array.Empty<string>());
            return new(SearchFileReadStatus.Read, File.ReadAllLines(filePath, StrictUtf8));
        }
        catch (DecoderFallbackException)
        {
            return new(SearchFileReadStatus.Unreadable, Array.Empty<string>());
        }
        catch (IOException)
        {
            return new(SearchFileReadStatus.Unreadable, Array.Empty<string>());
        }
        catch (UnauthorizedAccessException)
        {
            return new(SearchFileReadStatus.Unreadable, Array.Empty<string>());
        }
    }

    internal static bool LooksBinary(string filePath)
    {
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var buffer = new byte[4096];
        var count = stream.Read(buffer, 0, buffer.Length);
        if (count >= 2 && ((buffer[0] == 0xFF && buffer[1] == 0xFE) || (buffer[0] == 0xFE && buffer[1] == 0xFF)))
        {
            return false;
        }

        return buffer.AsSpan(0, count).IndexOf((byte)0) >= 0;
    }

    private static string GetSolutionRoot(Solution solution)
    {
        if (string.IsNullOrWhiteSpace(solution.FilePath))
        {
            throw new ArgumentException("Die Solution benoetigt fuer search_pattern einen Dateipfad.", nameof(solution));
        }

        var root = Path.GetDirectoryName(Path.GetFullPath(solution.FilePath));
        if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
        {
            throw new ArgumentException("Der Solution-Scope ist nicht verfuegbar.", nameof(solution));
        }

        return root;
    }

    private static string NormalizeScope(string? scope, string solutionRoot)
    {
        if (string.IsNullOrWhiteSpace(scope)) return ".";
        ValidateRelativeValue(scope, "scope");
        var fullPath = Path.GetFullPath(Path.Combine(solutionRoot, scope));
        if (!IsWithinRoot(fullPath, solutionRoot))
        {
            throw new ArgumentException("scope muss innerhalb des Solution-Roots liegen.", nameof(scope));
        }

        var relative = Path.GetRelativePath(solutionRoot, fullPath).Replace('\\', '/');
        return string.IsNullOrEmpty(relative) ? "." : relative.Trim('/');
    }

    private static string[] NormalizePatterns(IReadOnlyList<string>? patterns, string parameterName)
    {
        if (patterns is null || patterns.Count == 0) return Array.Empty<string>();
        var normalized = new List<string>(patterns.Count);
        foreach (var pattern in patterns)
        {
            if (string.IsNullOrWhiteSpace(pattern)) continue;
            ValidateRelativeValue(pattern, parameterName);
            normalized.Add(pattern.Replace('\\', '/').Trim('/'));
        }

        return normalized.ToArray();
    }

    private static void ValidateRelativeValue(string value, string parameterName)
    {
        if (Path.IsPathRooted(value) || value.Contains(':', StringComparison.Ordinal)
            || value.Replace('\\', '/').Split('/').Any(segment => segment == ".."))
        {
            throw new ArgumentException($"{parameterName} darf nur solution-relative Werte enthalten.", parameterName);
        }
    }

    private static bool MatchesScope(string relativePath, string scope) =>
        scope == "."
            || relativePath.Equals(scope, StringComparison.OrdinalIgnoreCase)
            || relativePath.StartsWith(scope + "/", StringComparison.OrdinalIgnoreCase);

    private static bool MatchesFilters(
        string relativePath,
        IReadOnlyList<string> includes,
        IReadOnlyList<string> excludes)
    {
        if (includes.Count > 0 && !includes.Any(pattern => MatchesPattern(relativePath, pattern))) return false;
        return !excludes.Any(pattern => MatchesPattern(relativePath, pattern));
    }

    private static bool MatchesPattern(string relativePath, string pattern) =>
        Configuration.FileFilterEvaluator.MatchesGlobForWeb(relativePath, pattern)
            || Configuration.FileFilterEvaluator.MatchesGlobForWeb(Path.GetFileName(relativePath), pattern)
            || (pattern.StartsWith("**/", StringComparison.Ordinal)
                && (Configuration.FileFilterEvaluator.MatchesGlobForWeb(relativePath, pattern[3..])
                    || Configuration.FileFilterEvaluator.MatchesGlobForWeb(Path.GetFileName(relativePath), pattern[3..])));

    private static bool IsMinified(string relativePath) =>
        Path.GetFileName(relativePath).Contains(".min.", StringComparison.OrdinalIgnoreCase);

    private static string? ToRelativePath(string solutionRoot, string filePath)
    {
        var fullPath = Path.GetFullPath(filePath);
        if (!IsWithinRoot(fullPath, solutionRoot)) return null;
        return Path.GetRelativePath(solutionRoot, fullPath).Replace('\\', '/');
    }

    private static bool IsWithinRoot(string path, string root)
    {
        var normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return path.Equals(root, StringComparison.OrdinalIgnoreCase)
            || path.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
    }

    private static string? GetProjectName(Solution solution, string filePath)
    {
        return solution.Projects
            .Where(project => !string.IsNullOrEmpty(project.FilePath))
            .Select(project => new
            {
                project.Name,
                Directory = Path.GetDirectoryName(Path.GetFullPath(project.FilePath!)),
            })
            .Where(project => project.Directory is not null && IsWithinRoot(filePath, project.Directory))
            .OrderByDescending(project => project.Directory!.Length)
            .Select(project => project.Name)
            .FirstOrDefault();
    }
}
