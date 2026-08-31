#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using AiNetLinter.Baseline;
using AiNetLinter.Configuration;
using AiNetLinter.Output;

namespace AiNetLinter.Mcp.Tools.FileStructure;

internal static class GetFileTreeScanner
{
    internal static FileTreeScanResult Scan(
        string projectRoot,
        GetFileTreeInput input,
        CancellationToken cancellationToken)
    {
        var resolution = FileTreePathResolver.ResolveRoot(projectRoot, input.Root);
        if (!resolution.Succeeded)
        {
            throw new ArgumentException(resolution.ErrorMessage ?? "root ist ungueltig.", nameof(input));
        }

        var accumulator = new FileTreeAccumulator(projectRoot, resolution.EffectiveRoot!, input);
        if (FileSystemExclusionHelpers.IsExcludedDirectoryName(Path.GetFileName(resolution.EffectiveRoot)))
        {
            return accumulator.Build(new TreeWalkStats([]) { SkippedExcludedDirectoryCount = 1 });
        }

        var effectiveDepth = input.MaxDepth ?? input.TreeDepth;
        var options = FileSystemWalkOptions.ForFileTree(effectiveDepth, cancellationToken);
        var stats = FileSystemExclusionHelpers.WalkFilteredTree(
            [resolution.EffectiveRoot!],
            options,
            accumulator.VisitDirectory,
            accumulator.VisitFile);
        return accumulator.Build(stats);
    }
}

internal sealed class FileTreeAccumulator
{
    private readonly string _projectRoot;
    private readonly string _effectiveRoot;
    private readonly string _rootRelativePath;
    private readonly GetFileTreeInput _input;
    private readonly string[] _extensions;
    private readonly int _effectiveDepth;
    private readonly List<FileTreeCandidate> _matches = [];
    private readonly Dictionary<string, FileTreeDirectoryAccumulator> _directories = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _warnings = [];
    private int _scannedFileCount;
    private int _scannedDirectoryCount;

    internal FileTreeAccumulator(string projectRoot, string effectiveRoot, GetFileTreeInput input)
    {
        _projectRoot = Path.GetFullPath(projectRoot);
        _effectiveRoot = Path.GetFullPath(effectiveRoot);
        _rootRelativePath = NormalizeRelativePath(Path.GetRelativePath(_projectRoot, _effectiveRoot));
        _input = input;
        _effectiveDepth = input.MaxDepth ?? input.TreeDepth;
        _extensions = FileTreeFilter.NormalizeExtensions(input.IncludeExtensions);
    }

    internal void VisitDirectory(string directory)
    {
        _scannedDirectoryCount++;
        var relativePath = ToProjectRelativePath(directory);
        EnsureDirectory(relativePath);
    }

    internal void VisitFile(string filePath)
    {
        _scannedFileCount++;
        var relativePath = ToProjectRelativePath(filePath);
        var extension = NormalizeExtension(Path.GetExtension(filePath));
        if (!FileTreeFilter.Matches(new FileTreeMatchCriteria(
            relativePath,
            extension,
            _extensions,
            _input.FileFilter,
            _input.ExcludePatterns ?? [])))
        {
            return;
        }

        var sizeBytes = ReadSize(filePath, relativePath);
        var lineCount = _input.IncludeLineCount ? ReadLineCount(filePath, relativePath) : null;
        _matches.Add(new FileTreeCandidate(relativePath, extension, sizeBytes, lineCount, GetDepth(relativePath)));
        AddDirectoryMatch(relativePath, sizeBytes);
    }

    internal FileTreeScanResult Build(TreeWalkStats walkStats)
    {
        EnsureDirectory(_rootRelativePath);
        var sortedMatches = SortMatches(_matches, _input.SortBy);
        var exposesFiles = !string.Equals(_input.View, "summary", StringComparison.OrdinalIgnoreCase);
        var shownMatches = exposesFiles ? sortedMatches.Take(_input.MaxResults).ToList() : [];
        var directoryCandidates = BuildDirectoryCandidates();
        var directoriesTruncated = directoryCandidates.Count > _input.MaxResults;
        var directories = directoriesTruncated
            ? directoryCandidates.Take(_input.MaxResults).ToArray()
            : directoryCandidates;
        var truncationReasons = BuildTruncationReasons(
            walkStats,
            sortedMatches.Count,
            exposesFiles,
            directoriesTruncated);
        var warnings = walkStats.Warnings.Concat(_warnings).Distinct(StringComparer.Ordinal).Take(50).ToArray();
        var payload = new FileTreePayload(
            Root: NormalizeRoot(_input.Root),
            EffectiveRoot: NormalizePath(_effectiveRoot),
            View: _input.View.ToLowerInvariant(),
            Summary: new FileTreeSummary(
                _scannedFileCount,
                sortedMatches.Count,
                _scannedDirectoryCount,
                _directories.Values.Count(directory => directory.MatchedFileCount > 0),
                sortedMatches.Sum(match => match.SizeBytes),
                BuildExtensionSummary(sortedMatches)),
            Directories: directories,
            Files: shownMatches.Select(ToFileEntry).ToArray(),
            Completeness: new FileTreeCompleteness(
                ScanCompleted: !walkStats.CancellationRequested && walkStats.InaccessibleSubtreeCount == 0 && warnings.Length == 0,
                Truncated: truncationReasons.Count > 0,
                TruncatedBy: truncationReasons,
                ShownFileCount: shownMatches.Count,
                InaccessibleSubtreeCount: walkStats.InaccessibleSubtreeCount,
                SkippedExcludedDirectoryCount: walkStats.SkippedExcludedDirectoryCount,
                SkippedReparsePointCount: walkStats.SkippedReparsePointCount,
                Warnings: warnings));
        return new FileTreeScanResult(payload, _input.TreeDepth);
    }

    private List<string> BuildTruncationReasons(
        TreeWalkStats stats,
        int matchedCount,
        bool exposesFiles,
        bool directoriesTruncated)
    {
        var reasons = new List<string>();
        if ((exposesFiles && matchedCount > _input.MaxResults) || directoriesTruncated) reasons.Add("maxResults");
        if (stats.InaccessibleSubtreeCount > 0) reasons.Add("inaccessibleSubtree");
        if (stats.CancellationRequested) reasons.Add("cancellation");
        return reasons;
    }

    private IReadOnlyList<FileTreeDirectoryEntry> BuildDirectoryCandidates()
    {
        // Vollstaendige Aggregation: Child-Zaehler ueber alle gematchten Verzeichnisse.
        var childCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var directory in _directories.Values.Where(directory => directory.MatchedFileCount > 0))
        {
            var parent = GetParentPath(directory.Path);
            if (parent is not null) childCounts[parent] = childCounts.GetValueOrDefault(parent) + 1;
        }

        // Ausgegebene Eintraege: im summary-Modus nur Top-Level-Aggregate (Tiefe <= 1),
        // sonst alles innerhalb der effektiven Scantiefe; maxResults begrenzt in Build.
        var isSummary = string.Equals(_input.View, "summary", StringComparison.OrdinalIgnoreCase);
        var maxVisibleDirectoryDepth = isSummary ? 1 : _effectiveDepth;
        return _directories.Values
            .Where(directory => directory.MatchedFileCount > 0 || directory.Path.Equals(_rootRelativePath, StringComparison.OrdinalIgnoreCase))
            .Where(directory => GetDepth(directory.Path) <= maxVisibleDirectoryDepth)
            .OrderBy(directory => directory.Path, StringComparer.OrdinalIgnoreCase)
            .Select(directory => new FileTreeDirectoryEntry(
                directory.Path,
                GetDepth(directory.Path),
                directory.MatchedFileCount,
                directory.MatchedBytes,
                childCounts.GetValueOrDefault(directory.Path)))
            .ToArray();
    }

    private IReadOnlyList<FileTreeExtensionEntry> BuildExtensionSummary(IReadOnlyList<FileTreeCandidate> matches) =>
        matches
            .GroupBy(match => match.Extension, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key is null ? "" : group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => new FileTreeExtensionEntry(
                group.Key,
                group.Count(),
                group.Sum(match => match.SizeBytes)))
            .ToArray();

    private FileTreeFileEntry ToFileEntry(FileTreeCandidate match) =>
        new(
            match.Path,
            match.Extension,
            _input.IncludeMetadata ? match.SizeBytes : null,
            match.LineCount,
            match.Depth);

    private void AddDirectoryMatch(string filePath, long sizeBytes)
    {
        var directory = GetParentPath(filePath) ?? _rootRelativePath;
        while (true)
        {
            var aggregate = EnsureDirectory(directory);
            aggregate.MatchedFileCount++;
            aggregate.MatchedBytes += sizeBytes;
            if (directory.Equals(_rootRelativePath, StringComparison.OrdinalIgnoreCase)) return;
            directory = GetParentPath(directory) ?? _rootRelativePath;
        }
    }

    private FileTreeDirectoryAccumulator EnsureDirectory(string path)
    {
        var normalizedPath = NormalizeRelativePath(path);
        if (_directories.TryGetValue(normalizedPath, out var existing)) return existing;
        var created = new FileTreeDirectoryAccumulator(normalizedPath);
        _directories[normalizedPath] = created;
        return created;
    }

    private long ReadSize(string filePath, string relativePath)
    {
        try
        {
            return new FileInfo(filePath).Length;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _warnings.Add($"{relativePath}: Dateimetadaten konnten nicht gelesen werden ({ex.Message})");
            return 0;
        }
    }

    private int? ReadLineCount(string filePath, string relativePath)
    {
        try
        {
            return File.ReadLines(filePath).Count();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DecoderFallbackException)
        {
            _warnings.Add($"{relativePath}: Zeilenanzahl konnte nicht gelesen werden ({ex.Message})");
            return null;
        }
    }

    private List<FileTreeCandidate> SortMatches(IEnumerable<FileTreeCandidate> matches, string sortBy) =>
        sortBy.ToLowerInvariant() switch
        {
            "size_desc" => matches.OrderByDescending(match => match.SizeBytes).ThenBy(match => match.Path, StringComparer.OrdinalIgnoreCase).ToList(),
            "extension" => matches.OrderBy(match => match.Extension is null ? "" : match.Extension, StringComparer.OrdinalIgnoreCase).ThenBy(match => match.Path, StringComparer.OrdinalIgnoreCase).ToList(),
            _ => matches.OrderBy(match => match.Path, StringComparer.OrdinalIgnoreCase).ToList(),
        };

    private string ToProjectRelativePath(string absolutePath) =>
        NormalizeRelativePath(Path.GetRelativePath(_projectRoot, absolutePath));

    private static string NormalizePath(string path) => path.Replace('\\', '/');

    private static string NormalizeRelativePath(string path)
    {
        var normalized = NormalizePath(path).Trim('/');
        return string.IsNullOrEmpty(normalized) || normalized == "." ? "." : normalized;
    }

    private static string NormalizeRoot(string root) =>
        NormalizeRelativePath(string.IsNullOrWhiteSpace(root) ? "." : root);

    private static string? NormalizeExtension(string? extension) =>
        string.IsNullOrEmpty(extension) ? null : extension.ToLowerInvariant();

    private int GetDepth(string path)
    {
        if (path.Equals(_rootRelativePath, StringComparison.OrdinalIgnoreCase)) return 0;
        var relativeToRoot = _rootRelativePath == "."
            ? path
            : path.StartsWith(_rootRelativePath + "/", StringComparison.OrdinalIgnoreCase)
                ? path[(_rootRelativePath.Length + 1)..]
                : path;
        return NormalizeRelativePath(relativeToRoot).Split('/', StringSplitOptions.RemoveEmptyEntries).Length;
    }

    private static string? GetParentPath(string path)
    {
        var separator = path.LastIndexOf('/');
        return separator < 0 ? null : NormalizeRelativePath(path[..separator]);
    }

    private sealed class FileTreeDirectoryAccumulator(string path)
    {
        internal string Path { get; } = path;
        internal int MatchedFileCount { get; set; }
        internal long MatchedBytes { get; set; }
    }

    private sealed record FileTreeCandidate(
        string Path,
        string? Extension,
        long SizeBytes,
        int? LineCount,
        int Depth);
}
