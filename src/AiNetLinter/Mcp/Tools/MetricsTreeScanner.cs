#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;

namespace AiNetLinter.Mcp.Tools;

/// <summary>Gebuendelte, bereits validierte Parameter fuer <see cref="MetricsTreeScanner.BuildTree"/>.</summary>
internal sealed record MetricsTreeQuery(
    string? Root, MetricsTreeMode Mode, int Depth, int TopN, Regex? FileFilter);

/// <summary>
/// Walk + Aggregation fuer die zwei Datei-Modi von <c>metrics_tree</c> (<c>code_size</c>,
/// <c>comment_density</c>) — nutzt <see cref="SolutionFileWalker"/> als Datenquelle und
/// <see cref="MetricsTreeRenderer"/> zur Ausgabe. Keine Abhaengigkeit von
/// <see cref="McpCodeGraphServer"/> — direkt unit-testbar (identisches Muster zu
/// <see cref="GetHotspotsScanner"/>).
/// </summary>
internal static class MetricsTreeScanner
{
    internal static string BuildTree(Solution solution, MetricsTreeQuery query)
    {
        var solutionDir = Path.GetDirectoryName(solution.FilePath) ?? "";
        var rootRelative = NormalizeRoot(query.Root);

        var walked = SolutionFileWalker.CollectFiles(solution, solutionDir, scopeFilter: null, query.FileFilter);
        var scoped = walked.Where(f => f.RelativePath.StartsWith(rootRelative, StringComparison.OrdinalIgnoreCase)).ToList();

        if (scoped.Count == 0)
        {
            return $"Keine Dateien unter root='{rootRelative}'" +
                   (query.FileFilter != null ? " mit file_filter" : "") + " — Pfad/Filter pruefen.";
        }

        var metrics = scoped
            .Select(f => query.Mode == MetricsTreeMode.CodeSize ? ComputeCodeSizeMetric(f) : ComputeCommentDensityMetric(f))
            .Where(m => m is not null)
            .Select(m => m!)
            .ToList();

        if (metrics.Count == 0)
        {
            return $"Keine lesbaren Dateien unter root='{rootRelative}' — Dateien pruefen (evtl. gesperrt/geloescht).";
        }

        var rootName = rootRelative.Length == 0 ? (Path.GetFileName(solutionDir) is { Length: > 0 } n ? n : ".") : rootRelative.Split('/')[^1];
        var builderRoot = BuildNode(rootName, rootRelative, metrics, level: 0, query.Depth);
        var treeRoot = ToMetricsTreeNode(builderRoot, query.Mode);
        var sortDescending = query.Mode == MetricsTreeMode.CodeSize;
        return MetricsTreeRenderer.Render(treeRoot, query.TopN, sortDescending);
    }

    private static string NormalizeRoot(string? root)
    {
        return string.IsNullOrWhiteSpace(root) ? "" : root.Replace('\\', '/').Trim('/');
    }

    private sealed record FileMetric(string RelativePath, int CommentLines, int CodeLines, long Bytes);

    private static FileMetric? ComputeCodeSizeMetric(WalkedFile f)
    {
        var lines = SolutionFileWalker.TryReadAllLines(f.AbsolutePath);
        if (lines is null) return null;
        return new FileMetric(f.RelativePath, CommentLines: 0, CodeLines: lines.Length, Bytes: TryGetFileSize(f.AbsolutePath));
    }

    private static FileMetric? ComputeCommentDensityMetric(WalkedFile f)
    {
        var lines = SolutionFileWalker.TryReadAllLines(f.AbsolutePath);
        if (lines is null) return null;
        var (commentLines, codeLines) = CountCommentLines(lines);
        return new FileMetric(f.RelativePath, commentLines, codeLines, Bytes: 0);
    }

    private static long TryGetFileSize(string path)
    {
        try
        {
            return new FileInfo(path).Length;
        }
        catch (IOException)
        {
            return 0;
        }
    }

    /// <summary>
    /// Einfache Zeilen-Heuristik statt vollstaendigem C#-Tokenizer (bewusst schneller Datei-Walk,
    /// kein Roslyn-Parse noetig — der ist den zwei EPIC-02-Modi vorbehalten). Block-Kommentar-Status
    /// wird ueber die Datei hinweg mitgefuehrt. Leerzeilen zaehlen weder als Code- noch als
    /// Kommentarzeile.
    /// </summary>
    private static (int CommentLines, int CodeLines) CountCommentLines(string[] lines)
    {
        var commentLines = 0;
        var codeLines = 0;
        var inBlockComment = false;

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.Length == 0) continue;

            if (inBlockComment)
            {
                commentLines++;
                if (line.Contains("*/", StringComparison.Ordinal)) inBlockComment = false;
                continue;
            }

            if (line.StartsWith("//", StringComparison.Ordinal))
            {
                commentLines++;
                continue;
            }

            if (line.StartsWith("/*", StringComparison.Ordinal))
            {
                commentLines++;
                inBlockComment = !line.Contains("*/", StringComparison.Ordinal);
                continue;
            }

            codeLines++;
        }

        return (commentLines, codeLines);
    }

    private sealed record BuilderNode(
        string Name, string RelativePath, int FileCount, int CommentLines, int CodeLines, long Bytes,
        IReadOnlyList<BuilderNode> Children);

    private static BuilderNode BuildNode(string name, string nodeRelativePath, List<FileMetric> metrics, int level, int depth)
    {
        var isFileLeaf = metrics.Count == 1 && metrics[0].RelativePath.Equals(nodeRelativePath, StringComparison.OrdinalIgnoreCase);
        if (isFileLeaf || level >= depth)
        {
            return AggregateLeaf(name, nodeRelativePath, metrics);
        }

        var groups = GroupByNextSegment(metrics, nodeRelativePath);
        var children = groups
            .Select(g => BuildNode(g.Segment, CombinePath(nodeRelativePath, g.Segment), g.Metrics, level + 1, depth))
            .ToList();

        return AggregateWithChildren(name, nodeRelativePath, children);
    }

    private static BuilderNode AggregateLeaf(string name, string relativePath, List<FileMetric> metrics)
    {
        return new BuilderNode(
            name, relativePath, metrics.Count,
            metrics.Sum(m => m.CommentLines), metrics.Sum(m => m.CodeLines), metrics.Sum(m => m.Bytes),
            Array.Empty<BuilderNode>());
    }

    private static BuilderNode AggregateWithChildren(string name, string relativePath, List<BuilderNode> children)
    {
        return new BuilderNode(
            name, relativePath, children.Sum(c => c.FileCount),
            children.Sum(c => c.CommentLines), children.Sum(c => c.CodeLines), children.Sum(c => c.Bytes),
            children);
    }

    private static List<(string Segment, List<FileMetric> Metrics)> GroupByNextSegment(List<FileMetric> metrics, string nodeRelativePath)
    {
        var groups = new Dictionary<string, List<FileMetric>>(StringComparer.OrdinalIgnoreCase);
        foreach (var metric in metrics)
        {
            var remainder = GetRemainder(metric.RelativePath, nodeRelativePath);
            if (remainder.Length == 0) continue;

            var slashIndex = remainder.IndexOf('/');
            var segment = slashIndex < 0 ? remainder : remainder[..slashIndex];
            if (!groups.TryGetValue(segment, out var list))
            {
                list = new List<FileMetric>();
                groups[segment] = list;
            }
            list.Add(metric);
        }
        return groups.Select(kv => (kv.Key, kv.Value)).ToList();
    }

    private static string GetRemainder(string relativePath, string nodeRelativePath)
    {
        if (nodeRelativePath.Length == 0) return relativePath;
        var prefixLen = nodeRelativePath.Length + 1;
        return prefixLen <= relativePath.Length ? relativePath[prefixLen..] : "";
    }

    private static string CombinePath(string nodeRelativePath, string segment)
    {
        return nodeRelativePath.Length == 0 ? segment : $"{nodeRelativePath}/{segment}";
    }

    private static MetricsTreeNode ToMetricsTreeNode(BuilderNode node, MetricsTreeMode mode)
    {
        var sortValue = ComputeSortValue(mode, node.CommentLines, node.CodeLines);
        var displayLine = FormatDisplayLine(mode, node.FileCount, sortValue, node.CommentLines, node.CodeLines, node.Bytes);
        var children = node.Children.Select(c => ToMetricsTreeNode(c, mode)).ToList();
        return new MetricsTreeNode(node.Name, node.RelativePath, node.FileCount, sortValue, displayLine, children);
    }

    // comment_density sortiert AUFSTEIGEND nach Kommentar-Ratio (niedrigste Ratio zuerst): eine
    // niedrige Ratio ist das eigentliche Risiko-Signal (schlecht dokumentierter Code), ein Knoten
    // mit hoher Ratio ist unauffaellig — umgekehrt zu code_size, wo grosse Knoten das Signal sind.
    private static double ComputeSortValue(MetricsTreeMode mode, int commentLines, int codeLines)
    {
        if (mode == MetricsTreeMode.CodeSize) return codeLines;

        var total = commentLines + codeLines;
        return total == 0 ? 0 : (double)commentLines / total;
    }

    private static string FormatDisplayLine(
        MetricsTreeMode mode, int fileCount, double sortValue, int commentLines, int codeLines, long bytes)
    {
        var fileWord = fileCount == 1 ? "Datei" : "Dateien";
        if (mode == MetricsTreeMode.CodeSize)
        {
            return $"{fileCount} {fileWord} | {codeLines:N0} LoC | {FormatBytes(bytes)}";
        }

        var total = commentLines + codeLines;
        var percent = sortValue * 100;
        return $"{fileCount} {fileWord} | {percent:F0}% Kommentaranteil ({commentLines:N0}/{total:N0} Zeilen)";
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes >= 1024 * 1024) return $"{bytes / 1024.0 / 1024.0:F1} MB";
        if (bytes >= 1024) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes} B";
    }
}
