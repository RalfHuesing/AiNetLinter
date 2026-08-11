#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using AiNetLinter.Output;

namespace AiNetLinter.Maps;

/// <summary>
/// Ein Eintrag für eine gescannte .cs-Datei (Pfad, Zeilenzahl, Verzeichnis).
/// </summary>
internal sealed record StructureFileInfo(string RelativePath, int Lines, string Directory);

/// <summary>
/// Erzeugt eine Hotspot Map: Dateien die sich ihrem konfigurierten Limit nähern.
/// Proaktives Drift-Signal — sichtbar bevor ein Regelverstoß entsteht.
/// </summary>
internal static class HotspotMapBuilder
{
    private const double WarnThreshold     = 0.80;
    private const double CriticalThreshold = 0.95;

    internal static int Build(string targetPath, int maxLineCount, ILintConsole c)
    {
        var root = Directory.Exists(targetPath) ? targetPath : Path.GetDirectoryName(targetPath) ?? targetPath;
        if (!Directory.Exists(root))
        {
            c.WriteError($"[ERROR]: Pfad '{root}' existiert nicht.");
            return 1;
        }

        var files = CollectFileInfos(root);
        var critical = files.Where(f => (double)f.Lines / maxLineCount >= CriticalThreshold).ToList();
        var warning  = files.Where(f => (double)f.Lines / maxLineCount is >= WarnThreshold and < CriticalThreshold).ToList();

        var sb = new StringBuilder();
        sb.AppendLine("# AiNetLinter — Hotspot Map");
        sb.AppendLine();
        sb.AppendLine($"Gescannt: {files.Count} .cs-Dateien | MaxLineCount: {maxLineCount} | Pfad: {root.Replace('\\', '/')}");
        sb.AppendLine();

        HotspotSectionFormatter.AppendSection(sb, "🔴 Kritische Dateien (>95% des Limits)", critical.Select(f => (f.RelativePath, f.Lines)).ToList(), maxLineCount);
        HotspotSectionFormatter.AppendSection(sb, "⚠ Warnungs-Dateien (>80% des Limits)", warning.Select(f => (f.RelativePath, f.Lines)).ToList(), maxLineCount);

        if (critical.Count == 0 && warning.Count == 0)
        {
            sb.AppendLine("## ✓ Alle Dateien im grünen Bereich");
            sb.AppendLine();
            sb.AppendLine($"Keine Datei überschreitet 80% des Limits ({(int)(maxLineCount * WarnThreshold)} Zeilen).");
        }
        else
        {
            sb.AppendLine();
            sb.AppendLine($"## Alle anderen Dateien: {files.Count - critical.Count - warning.Count} Dateien im grünen Bereich");
        }

        c.WriteLine(sb.ToString().TrimEnd());
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

}
