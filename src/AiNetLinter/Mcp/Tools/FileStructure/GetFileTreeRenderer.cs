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
            $"{summary.ScannedFileCount} Dateien gescannt, {summary.MatchedFileCount} Treffer, " +
            $"{FormatBytes(summary.MatchedBytes)} gematcht");
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
        return builder.ToString().TrimEnd();
    }

    private static void AppendExtensions(StringBuilder builder, IReadOnlyList<FileTreeExtensionEntry> extensions)
    {
        if (extensions.Count == 0) return;
        var values = extensions.Select(extension =>
            $"{extension.Extension ?? "[ohne Extension]"} {extension.Count}");
        builder.AppendLine($"Extensions: {string.Join(", ", values)}");
    }

    private static void AppendTree(StringBuilder builder, FileTreePayload payload, int treeDepth)
    {
        builder.AppendLine();
        builder.AppendLine(payload.Root == "." ? "." : $"{payload.Root}/");
        AppendDirectories(builder, payload, treeDepth, includeRoot: false);

        foreach (var file in payload.Files.OrderBy(file => file.Path, StringComparer.OrdinalIgnoreCase))
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
            var warning = isSummary
                ? $"[WARN]: {payload.Summary.MatchedFileCount} Dateien aggregiert, Verzeichnisliste begrenzt ({string.Join(", ", completeness.TruncatedBy)})."
                : $"[WARN]: {payload.Summary.MatchedFileCount} Dateien gematcht, {completeness.ShownFileCount} gezeigt ({string.Join(", ", completeness.TruncatedBy)}).";
            builder.AppendLine(warning);
            builder.Append(isSummary
                ? "[HINWEIS]: Verzeichnisliste auf Top-Level-Aggregate begrenzt; maxResults oder treeDepth anpassen."
                : "[HINWEIS]: root/fileFilter verfeinern oder maxResults anpassen.");
            return;
        }

        var status = payload.View.Equals("summary", StringComparison.OrdinalIgnoreCase)
            ? $"{payload.Summary.MatchedFileCount} Dateien aggregiert"
            : $"{completeness.ShownFileCount} Dateien gezeigt";
        builder.Append($"[{(completeness.ScanCompleted ? "vollstaendig" : "partiell")}: {status}]");
        if (completeness.Warnings.Count > 0)
        {
            builder.Append($" {completeness.Warnings.Count} Warnung(en)");
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
