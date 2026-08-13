#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AiNetLinter.Mcp.Tools.FileStructure;
using Microsoft.CodeAnalysis;

namespace AiNetLinter.Mcp.Tools.MetricsTree;

/// <summary>Gebuendelte, bereits validierte Parameter fuer <see cref="MetricsTreeScanner.BuildTree"/>.</summary>
internal sealed record MetricsTreeQuery(
    string? Root, MetricsTreeMode Mode, int Depth, int TopN, Regex? FileFilter);

/// <summary>
/// Pro-Datei-Metrik, gemeinsam fuer alle vier <c>metrics_tree</c>-Modi. Die zwei Datei-Modi
/// (<c>code_size</c>/<c>comment_density</c>) fuellen nur <see cref="CommentLines"/>/
/// <see cref="CodeLines"/>/<see cref="Bytes"/>, die zwei Roslyn-Modi (<see cref="MetricsTreeRoslynScanner"/>)
/// nur die Violation-/Complexity-Felder — die jeweils andere Gruppe bleibt beim Default 0. Top-Level
/// statt nested (obwohl nur von <see cref="MetricsTreeScanner"/>/<see cref="MetricsTreeRoslynScanner"/>
/// verwendet), weil <c>BanPublicNestedTypes</c> auch <c>internal nested</c> Typen verbietet
/// (<c>BanPublicNestedTypesAllowPrivate</c> erlaubt nur <c>private nested</c>).
/// </summary>
internal sealed record FileMetric(
    string RelativePath, int CommentLines, int CodeLines, long Bytes,
    int ViolationCount = 0, int ErrorCount = 0, int WarningCount = 0,
    int MethodCount = 0, int SumCyclomatic = 0, int MaxCyclomatic = 0, int MaxCognitive = 0);

/// <summary>
/// Aggregierter Baum-Knoten waehrend des Baus, gemeinsam fuer alle vier Modi — siehe
/// <see cref="FileMetric"/>. <see cref="MaxCyclomatic"/>/<see cref="MaxCognitive"/> sind die
/// einzigen Felder, die per <c>Math.Max</c> statt Summe aggregiert werden. Top-Level statt nested —
/// siehe Begruendung bei <see cref="FileMetric"/>.
/// </summary>
internal sealed record BuilderNode(
    string Name, string RelativePath, int FileCount, int CommentLines, int CodeLines, long Bytes,
    int ViolationCount, int ErrorCount, int WarningCount,
    int MethodCount, int SumCyclomatic, int MaxCyclomatic, int MaxCognitive,
    IReadOnlyList<BuilderNode> Children);

/// <summary>
/// Walk + Aggregation fuer die zwei Datei-Modi von <c>metrics_tree</c> (<c>code_size</c>,
/// <c>comment_density</c>) — nutzt <see cref="SolutionFileWalker"/> als Datenquelle und
/// <see cref="MetricsTreeRenderer"/> zur Ausgabe. Keine Abhaengigkeit von
/// <see cref="McpCodeGraphServer"/> — direkt unit-testbar (identisches Muster zu
/// <see cref="GetHotspotsScanner"/>). Der Aggregations-Kern (<see cref="BuildNode"/>/
/// <see cref="ToMetricsTreeNode"/>/<see cref="NormalizeRoot"/>/<see cref="ComputeRootName"/>) ist
/// <c>internal</c>, damit <see cref="MetricsTreeRoslynScanner"/> dieselbe Baum-Aggregation fuer
/// die zwei Roslyn-Modi wiederverwendet, statt eine zweite unabhaengige Implementierung zu bauen.
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

        var rootName = ComputeRootName(solutionDir, rootRelative);
        var builderRoot = BuildNode(rootName, rootRelative, metrics, level: 0, query.Depth);
        var treeRoot = ToMetricsTreeNode(builderRoot, query.Mode);
        var sortDescending = query.Mode == MetricsTreeMode.CodeSize;
        return MetricsTreeRenderer.Render(treeRoot, query.TopN, sortDescending);
    }

    /// <summary>Normalisiert den <c>root</c>-Parameter (Backslashes, fuehrende/folgende Slashes) —
    /// gemeinsam genutzt von den Datei- und den Roslyn-Modi (<see cref="MetricsTreeRoslynScanner"/>).</summary>
    internal static string NormalizeRoot(string? root)
    {
        return string.IsNullOrWhiteSpace(root) ? "" : root.Replace('\\', '/').Trim('/');
    }

    /// <summary>Leitet den Anzeigenamen des Wurzelknotens aus dem normalisierten <paramref name="rootRelative"/>
    /// ab (Solution-Verzeichnisname als Fallback bei leerem Root) — gemeinsam genutzt von den Datei- und
    /// den Roslyn-Modi.</summary>
    internal static string ComputeRootName(string solutionDir, string rootRelative)
    {
        return rootRelative.Length == 0
            ? (Path.GetFileName(solutionDir) is { Length: > 0 } n ? n : ".")
            : rootRelative.Split('/')[^1];
    }

    private static FileMetric? ComputeCodeSizeMetric(WalkedFile f)
    {
        var lines = SolutionFileWalker.TryReadAllLines(f);
        if (lines is null) return null;
        return new FileMetric(f.RelativePath, CommentLines: 0, CodeLines: lines.Length, Bytes: TryGetFileSize(f.AbsolutePath));
    }

    private static FileMetric? ComputeCommentDensityMetric(WalkedFile f)
    {
        var lines = SolutionFileWalker.TryReadAllLines(f);
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
    /// kein Roslyn-Parse noetig — der ist den zwei Roslyn-Modi vorbehalten). Block-Kommentar-Status
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

    // ainetlinter-disable MaxMethodParameterCount — BuildNode kapselt den rekursiven Baum-Bau aus
    // 5 unabhaengigen, semantisch verschiedenen Eingaben (Knotenname, relativer Pfad,
    // Metrik-Zeilen, aktuelle/max. Rekursionstiefe). Ein Parameter-Object braechte hier keinen
    // semantischen Mehrwert (die Werte sind keine zusammengehoerige Konfiguration, sondern pro
    // Rekursionsstufe unterschiedliche Werte) und die relaxierte Nicht-Public-Grenze
    // (MaxMethodParameterCountForNonPublic: 6) greift fuer diese Methode nicht, weil sie
    // `internal` (nicht `private`/`protected`) sein muss, damit MetricsTreeRoslynScanner sie
    // wiederverwenden kann (siehe Klassen-Doku).
    internal static BuilderNode BuildNode(string name, string nodeRelativePath, List<FileMetric> metrics, int level, int depth)
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
            metrics.Sum(m => m.ViolationCount), metrics.Sum(m => m.ErrorCount), metrics.Sum(m => m.WarningCount),
            metrics.Sum(m => m.MethodCount), metrics.Sum(m => m.SumCyclomatic),
            metrics.Count == 0 ? 0 : metrics.Max(m => m.MaxCyclomatic),
            metrics.Count == 0 ? 0 : metrics.Max(m => m.MaxCognitive),
            Array.Empty<BuilderNode>());
    }

    private static BuilderNode AggregateWithChildren(string name, string relativePath, List<BuilderNode> children)
    {
        return new BuilderNode(
            name, relativePath, children.Sum(c => c.FileCount),
            children.Sum(c => c.CommentLines), children.Sum(c => c.CodeLines), children.Sum(c => c.Bytes),
            children.Sum(c => c.ViolationCount), children.Sum(c => c.ErrorCount), children.Sum(c => c.WarningCount),
            children.Sum(c => c.MethodCount), children.Sum(c => c.SumCyclomatic),
            children.Count == 0 ? 0 : children.Max(c => c.MaxCyclomatic),
            children.Count == 0 ? 0 : children.Max(c => c.MaxCognitive),
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

    internal static MetricsTreeNode ToMetricsTreeNode(BuilderNode node, MetricsTreeMode mode)
    {
        var sortValue = ComputeSortValue(mode, node);
        var displayLine = FormatDisplayLine(mode, node, sortValue);
        var children = node.Children.Select(c => ToMetricsTreeNode(c, mode)).ToList();
        return new MetricsTreeNode(node.Name, node.RelativePath, node.FileCount, sortValue, displayLine, children);
    }

    // comment_density sortiert AUFSTEIGEND nach Kommentar-Ratio (niedrigste Ratio zuerst): eine
    // niedrige Ratio ist das eigentliche Risiko-Signal (schlecht dokumentierter Code), ein Knoten
    // mit hoher Ratio ist unauffaellig — umgekehrt zu code_size, wo grosse Knoten das Signal sind.
    // violation_density/complexity sortieren beide ABSTEIGEND (siehe MetricsTreeRoslynScanner,
    // das sortDescending: true fest an MetricsTreeRenderer.Render uebergibt).
    private static double ComputeSortValue(MetricsTreeMode mode, BuilderNode node)
    {
        switch (mode)
        {
            case MetricsTreeMode.CodeSize:
                return node.CodeLines;
            case MetricsTreeMode.ViolationDensity:
                return node.ViolationCount;
            case MetricsTreeMode.Complexity:
                return node.MethodCount == 0 ? 0 : (double)node.SumCyclomatic / node.MethodCount;
            default:
                var total = node.CommentLines + node.CodeLines;
                return total == 0 ? 0 : (double)node.CommentLines / total;
        }
    }

    private static string FormatDisplayLine(MetricsTreeMode mode, BuilderNode node, double sortValue)
    {
        var fileWord = node.FileCount == 1 ? "Datei" : "Dateien";
        switch (mode)
        {
            case MetricsTreeMode.CodeSize:
                return $"{node.FileCount} {fileWord} | {node.CodeLines:N0} LoC | {FormatBytes(node.Bytes)}";
            case MetricsTreeMode.ViolationDensity:
                return $"{node.FileCount} {fileWord} | {node.ViolationCount} Violations " +
                       $"({node.ErrorCount} Fehler, {node.WarningCount} Warnungen)";
            case MetricsTreeMode.Complexity:
                return $"{node.FileCount} {fileWord} | Ø CC {sortValue.ToString("F1", CultureInfo.InvariantCulture)} | " +
                       $"max CC {node.MaxCyclomatic} | max CogC {node.MaxCognitive}";
            default:
                var total = node.CommentLines + node.CodeLines;
                var percent = sortValue * 100;
                return $"{node.FileCount} {fileWord} | {percent:F0}% Kommentaranteil ({node.CommentLines:N0}/{total:N0} Zeilen)";
        }
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes >= 1024 * 1024) return $"{bytes / 1024.0 / 1024.0:F1} MB";
        if (bytes >= 1024) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes} B";
    }
}
