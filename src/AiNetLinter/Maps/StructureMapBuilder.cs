#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using AiNetLinter.Output;

namespace AiNetLinter.Maps;

/// <summary>
/// Ein Eintrag für eine Datei in der Structure Map.
/// </summary>
internal sealed record StructureFileInfo(string RelativePath, int Lines, string Directory);

/// <summary>
/// Erzeugt eine Structure Map: Verzeichnisstruktur mit Dateigrößen.
/// Dient als direkter Input für Eval-Prompt E03 (Architecture-Intent-Audit).
/// </summary>
internal static class StructureMapBuilder
{
    internal static int Build(string targetPath, int maxLineCount, ILintConsole c)
    {
        var root = ResolveRoot(targetPath);
        if (!Directory.Exists(root))
        {
            c.WriteError($"[ERROR]: Pfad '{root}' existiert nicht.");
            return 1;
        }
        var files = CollectFileInfos(root);
        c.WriteLine(BuildMarkdown(files, root, maxLineCount));
        return 0;
    }

    internal static IReadOnlyList<StructureFileInfo> CollectFileInfos(string root)
    {
        if (!Directory.Exists(root))
            return Array.Empty<StructureFileInfo>();

        return Directory
            .EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                     && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .Select(f => {
                var dirPath = Path.GetDirectoryName(f) ?? root;
                var relativeDir = Path.GetRelativePath(root, dirPath).Replace('\\', '/');
                if (relativeDir == ".") relativeDir = "";
                return new StructureFileInfo(
                    RelativePath: Path.GetRelativePath(root, f).Replace('\\', '/'),
                    Lines: File.ReadAllLines(f).Length,
                    Directory: relativeDir);
            })
            .OrderByDescending(f => f.Lines)
            .ThenBy(f => f.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string BuildMarkdown(
        IReadOnlyList<StructureFileInfo> files, string root, int maxLineCount)
    {
        var sb = new StringBuilder();
        var totalLines = files.Sum(f => f.Lines);

        sb.AppendLine("# AiNetLinter — Structure Map");
        sb.AppendLine();
        sb.AppendLine($"Gescannt: {files.Count} .cs-Dateien"
            + $" | {totalLines:N0} Zeilen gesamt"
            + $" | MaxLineCount: {maxLineCount}"
            + $" | Pfad: {root.Replace('\\', '/')}");
        sb.AppendLine();

        // Verzeichnis-Übersicht
        var byDir = files
            .GroupBy(f => string.IsNullOrEmpty(f.Directory) ? "(Root)" : f.Directory)
            .OrderByDescending(g => g.Sum(f => f.Lines))
            .ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase);

        sb.AppendLine("## Verzeichnis-Übersicht");
        sb.AppendLine();
        sb.AppendLine("| Verzeichnis | Dateien | Zeilen |");
        sb.AppendLine("|:---|---:|---:|");
        foreach (var dir in byDir)
        {
            var dirName = dir.Key;
            if (dirName != "(Root)" && !dirName.EndsWith("/"))
            {
                dirName += "/";
            }
            sb.AppendLine($"| {dirName} | {dir.Count()} | {dir.Sum(f => f.Lines):N0} |");
        }

        // Alle Dateien mit Warnstufe
        sb.AppendLine();
        sb.AppendLine("## Alle Dateien (sortiert nach Größe)");
        sb.AppendLine();
        sb.AppendLine("| Datei | Zeilen | Status |");
        sb.AppendLine("|:---|---:|:---|");
        foreach (var f in files)
        {
            var pct = (double)f.Lines / maxLineCount;
            var status = pct >= 0.95 ? "🔴 Kritisch" : pct >= 0.80 ? "⚠ Warnung" : "✓";
            sb.AppendLine($"| {f.RelativePath} | {f.Lines} | {status} |");
        }

        return sb.ToString().TrimEnd();
    }

    private static string ResolveRoot(string targetPath) =>
        Directory.Exists(targetPath)
            ? targetPath
            : Path.GetDirectoryName(targetPath) ?? targetPath;
}
