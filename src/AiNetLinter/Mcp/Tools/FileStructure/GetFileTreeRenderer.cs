#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace AiNetLinter.Mcp.Tools.FileStructure;

internal static class GetFileTreeRenderer
{
    internal static string Render(FileTreeScanResult result)
    {
        var payload = result.Payload;
        var summary = payload.Summary;
        var builder = new StringBuilder();
        builder.AppendLine($"get_file_tree: root={payload.Root} view={payload.View}");
        builder.AppendLine(
            $"{summary.ScannedFileCount} physische Dateien gescannt, {summary.MatchedFileCount} physische Dateitreffer, " +
            $"{FormatBytes(summary.MatchedBytes)} gematcht");
        AppendExclusions(builder, payload.Exclusions);
        AppendExtensions(builder, summary.ByExtension);

        if (payload.View.Equals("tree", StringComparison.OrdinalIgnoreCase))
        {
            AppendTree(builder, payload, result.TreeDepth);
        }
        else if (payload.View.Equals("files", StringComparison.OrdinalIgnoreCase))
        {
            AppendFiles(builder, payload.Files);
        }
        else
        {
            AppendDirectories(builder, payload, result.TreeDepth, includeRoot: false);
        }

        AppendCompleteness(builder, payload);
        builder.AppendLine();
        builder.Append($"[NEXT: {payload.Next.Kind}] {payload.Next.Action}");
        return builder.ToString().TrimEnd();
    }

    private static void AppendExtensions(StringBuilder builder, IReadOnlyList<FileTreeExtensionEntry> extensions)
    {
        if (extensions.Count == 0) return;
        var values = extensions.Select(extension =>
            $"{extension.Extension ?? "[ohne Extension]"} {extension.Count}");
        builder.AppendLine($"Extensions: {string.Join(", ", values)}");
    }

    private static void AppendExclusions(StringBuilder builder, FileTreeExclusions exclusions)
    {
        if (exclusions.ExcludedPhysicalFileCount == 0) return;
        builder.AppendLine($"{exclusions.ExcludedPhysicalFileCount} physische Dateien durch angeforderte Ausschlussmuster ausgeschlossen");
    }

    private static void AppendTree(StringBuilder builder, FileTreePayload payload, int treeDepth)
    {
        builder.AppendLine();
        builder.AppendLine(payload.Root == "." ? "." : $"{payload.Root}/");
        AppendDirectories(builder, payload, treeDepth, includeRoot: false);

        var rootFiles = payload.Files
            .Where(file => !file.Path.Contains('/') && !file.Path.Contains('\\'))
            .OrderBy(file => file.Path, StringComparer.OrdinalIgnoreCase);

        foreach (var file in rootFiles)
        {
            builder.AppendLine($"├── {file.Path} {FormatFileDetails(file)}");
        }
    }

    private static void AppendDirectories(
        StringBuilder builder,
        FileTreePayload payload,
        int treeDepth,
        bool includeRoot)
    {
        var directories = payload.Directories
            .Where(directory => directory.Depth <= treeDepth)
            .Where(directory => includeRoot || !directory.Path.Equals(payload.Root, StringComparison.OrdinalIgnoreCase))
            .OrderBy(directory => directory.Path, StringComparer.OrdinalIgnoreCase);

        foreach (var directory in directories)
        {
            var indent = new string(' ', Math.Max(0, directory.Depth - 1) * 2);
            var name = directory.Path.TrimEnd('/').Split('/').Last();
            builder.AppendLine(
                $"{indent}├── {name}/ {directory.MatchedFileCount} Dateien | {FormatBytes(directory.MatchedBytes)}");
        }
    }

    private static void AppendFiles(StringBuilder builder, IReadOnlyList<FileTreeFileEntry> files)
    {
        builder.AppendLine();
        if (files.Count == 0)
        {
            builder.Append("Keine Dateitreffer.");
            return;
        }

        foreach (var file in files)
        {
            builder.AppendLine($"- {file.Path} {FormatFileDetails(file)}");
        }
    }

    private static void AppendCompleteness(StringBuilder builder, FileTreePayload payload)
    {
        var completeness = payload.Completeness;
        builder.AppendLine();
        if (completeness.Truncated)
        {
            var isSummary = payload.View.Equals("summary", StringComparison.OrdinalIgnoreCase);
            var isDepthOnly = completeness.TruncatedBy.Count == 1 && completeness.TruncatedBy[0] == "maxDepth";
            var warning = isDepthOnly
                ? $"[WARN]: Scantiefe begrenzt ({string.Join(", ", completeness.TruncatedBy)}), tiefere Ebenen nicht gescannt."
                : isSummary
                    ? $"[WARN]: {payload.Summary.MatchedFileCount} physische Dateien aggregiert, Verzeichnisliste begrenzt ({string.Join(", ", completeness.TruncatedBy)})."
                    : $"[WARN]: {payload.Summary.MatchedFileCount} physische Dateien gematcht, {completeness.ShownPhysicalFileCount} gezeigt ({string.Join(", ", completeness.TruncatedBy)}).";
            builder.AppendLine(warning);
            AppendSkippedDirectoryDetails(builder, completeness);
            builder.Append(isDepthOnly
                ? "[HINWEIS]: maxDepth bzw. treeDepth anpassen fuer tiefere Ebenen."
                : isSummary
                    ? "[HINWEIS]: Verzeichnisliste auf Top-Level-Aggregate begrenzt; maxResults oder treeDepth anpassen."
                    : "[HINWEIS]: root/fileFilter verfeinern oder maxResults anpassen.");
            return;
        }

        var status = payload.View.Equals("summary", StringComparison.OrdinalIgnoreCase)
            ? $"{payload.Summary.MatchedFileCount} physische Dateien aggregiert"
            : $"{completeness.ShownPhysicalFileCount} physische Dateien gezeigt";
        builder.Append($"[{(completeness.ScanCompleted ? "vollstaendig" : "partiell")}: {status}]");
        AppendSkippedDirectoryDetails(builder, completeness);
        if (completeness.Warnings.Count > 0)
        {
            builder.Append($" {completeness.Warnings.Count} Warnung(en)");
        }
    }

    private static void AppendSkippedDirectoryDetails(StringBuilder builder, FileTreeCompleteness completeness)
    {
        if (completeness.SkippedExcludedDirectoryCount > 0)
        {
            builder.Append($" {completeness.SkippedExcludedDirectoryCount} Standard-Ausschlussverzeichnisse uebersprungen.");
        }

        if (completeness.SkippedReparsePointDirectoryCount > 0)
        {
            builder.Append($" {completeness.SkippedReparsePointDirectoryCount} Reparse-Point-Verzeichnisse uebersprungen.");
        }

        if (completeness.InaccessibleDirectoryCount > 0)
        {
            builder.Append($" {completeness.InaccessibleDirectoryCount} unzugaengliche Verzeichnisse nicht gelesen.");
        }
    }

    private static string FormatFileDetails(FileTreeFileEntry file)
    {
        var details = new List<string>();
        if (file.SizeBytes is not null) details.Add(FormatBytes(file.SizeBytes.Value));
        if (file.LineCount is not null) details.Add($"{file.LineCount} Zeilen");
        return details.Count == 0 ? string.Empty : $"| {string.Join(", ", details)}";
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024d:0.0} KB";
        return $"{bytes / (1024d * 1024d):0.0} MB";
    }
}
