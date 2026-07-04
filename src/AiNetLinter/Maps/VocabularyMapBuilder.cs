#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using AiNetLinter.Output;

namespace AiNetLinter.Maps;

/// <summary>
/// Ein Eintrag für eine Typ-Deklaration in der Vocabulary Map.
/// </summary>
internal sealed record VocabularyTypeEntry(string Name, string RelativePath, bool IsTest);

/// <summary>
/// Erzeugt eine Vocabulary Map: Typ-Deklarationen gruppiert nach Suffix-Muster.
/// Dient als direkter Input für Eval-Prompt E02 (Naming-Drift-Audit).
/// </summary>
internal static class VocabularyMapBuilder
{
    private static readonly Regex TypePattern = new(
        @"^\s*(public|internal|private|protected)\s+(sealed\s+|static\s+|abstract\s+)?"
        + @"(class|interface|record|enum)\s+(?<name>\w+)",
        RegexOptions.Multiline | RegexOptions.Compiled);

    internal static int Build(string targetPath, ILintConsole c)
    {
        var root = Directory.Exists(targetPath) ? targetPath : Path.GetDirectoryName(targetPath);
        if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
        {
            c.WriteError($"[ERROR]: Pfad '{targetPath}' existiert nicht.");
            return 1;
        }

        var csFiles = CollectCsFiles(targetPath);
        var entries = ExtractTypeEntries(csFiles, targetPath);
        var grouped = GroupBySuffix(entries);

        c.WriteLine(BuildMarkdown(entries, grouped, targetPath));
        return 0;
    }

    internal static IReadOnlyList<VocabularyTypeEntry> ExtractTypeEntries(
        IEnumerable<string> files, string rootPath)
    {
        var entries = new List<VocabularyTypeEntry>();
        var root = Directory.Exists(rootPath) ? rootPath : Path.GetDirectoryName(rootPath) ?? rootPath;

        foreach (var file in files)
        {
            var content = File.ReadAllText(file);
            var relativePath = Path.GetRelativePath(root, file).Replace('\\', '/');
            var isTest = relativePath.Contains("/Tests/") || relativePath.Contains(".Tests/");

            foreach (Match match in TypePattern.Matches(content))
            {
                entries.Add(new VocabularyTypeEntry(
                    Name: match.Groups["name"].Value,
                    RelativePath: relativePath,
                    IsTest: isTest));
            }
        }
        return entries;
    }

    private static IReadOnlyDictionary<string, List<VocabularyTypeEntry>> GroupBySuffix(
        IReadOnlyList<VocabularyTypeEntry> entries)
    {
        var result = new Dictionary<string, List<VocabularyTypeEntry>>(StringComparer.Ordinal);

        foreach (var entry in entries.Where(e => !e.IsTest))
        {
            var suffix = ExtractSuffix(entry.Name);
            if (!result.TryGetValue(suffix, out var list))
            {
                list = [];
                result[suffix] = list;
            }
            list.Add(entry);
        }

        return result;
    }

    internal static string ExtractSuffix(string typeName)
    {
        // PascalCase-Splitting: "AsyncVoidChecker" → ["Async", "Void", "Checker"]
        // Letztes Segment ab 4 Zeichen = Suffix-Kandidat
        var segments = SplitPascalCase(typeName);
        if (segments.Length >= 2)
        {
            var last = segments[^1];
            if (last.Length >= 4)
                return last;
        }
        return "(kein Suffix)";
    }

    private static string[] SplitPascalCase(string name)
    {
        var parts = new List<string>();
        var current = new StringBuilder();

        foreach (var ch in name)
        {
            if (char.IsUpper(ch) && current.Length > 0)
            {
                parts.Add(current.ToString());
                current.Clear();
            }
            current.Append(ch);
        }
        if (current.Length > 0)
            parts.Add(current.ToString());

        return [.. parts];
    }

    private static string BuildMarkdown(
        IReadOnlyList<VocabularyTypeEntry> all,
        IReadOnlyDictionary<string, List<VocabularyTypeEntry>> grouped,
        string targetPath)
    {
        var prodTypes = all.Where(e => !e.IsTest).ToList();
        var sb = new StringBuilder();

        sb.AppendLine("# AiNetLinter — Vocabulary Map");
        sb.AppendLine();
        sb.AppendLine($"Gescannt: {all.Count(e => !e.IsTest)} Produktions-Typen"
            + $" | {all.Count(e => e.IsTest)} Test-Typen"
            + $" | Pfad: {targetPath}");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("## Typ-Gruppen nach Suffix (Produktionscode)");

        foreach (var (suffix, types) in grouped.OrderByDescending(kv => kv.Value.Count).ThenBy(kv => kv.Key, StringComparer.Ordinal))
        {
            sb.AppendLine();
            sb.AppendLine($"### *{suffix} ({types.Count})");
            sb.AppendLine();
            sb.AppendLine("| Typ | Datei |");
            sb.AppendLine("|:---|:---|");
            foreach (var t in types.OrderBy(t => t.Name))
                sb.AppendLine($"| {t.Name} | {t.RelativePath} |");
        }

        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("## Suffix-Statistik");
        sb.AppendLine();
        sb.AppendLine("| Suffix | Anzahl | Anteil |");
        sb.AppendLine("|:---|---:|---:|");
        foreach (var (suffix, types) in grouped.OrderByDescending(kv => kv.Value.Count).ThenBy(kv => kv.Key, StringComparer.Ordinal))
        {
            var pct = prodTypes.Count > 0 ? (double)types.Count / prodTypes.Count * 100 : 0.0;
            sb.AppendLine($"| {suffix} | {types.Count} | {pct:F0} % |");
        }

        AppendHints(grouped, sb);

        return sb.ToString().TrimEnd();
    }

    private static void AppendHints(
        IReadOnlyDictionary<string, List<VocabularyTypeEntry>> grouped, StringBuilder sb)
    {
        // Verwandte Suffixe mit potenziell überlappender Bedeutung
        var checkerLike = new[] { "Checker", "Detector", "Scanner", "Analyzer", "Validator" };
        var found = checkerLike
            .Where(s => grouped.ContainsKey(s))
            .Select(s => $"{s} ({grouped[s].Count})")
            .ToList();

        if (found.Count > 1)
        {
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine("## ⚠ Hinweise");
            sb.AppendLine();
            sb.AppendLine($"Gemischte Patterns für Prüf-Klassen: {string.Join(", ", found)}");
            sb.AppendLine("→ Prüfen ob diese Unterscheidung intentional ist (z.B. Checker = erzeugt Violations, Detector = identifiziert Zustände).");
        }
    }

    internal static IEnumerable<string> CollectCsFiles(string targetPath)
    {
        var root = Directory.Exists(targetPath) ? targetPath : Path.GetDirectoryName(targetPath);
        if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
            return Array.Empty<string>();

        return Directory.EnumerateFiles(
            root,
            "*.cs",
            SearchOption.AllDirectories)
        .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                 && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"));
    }
}
