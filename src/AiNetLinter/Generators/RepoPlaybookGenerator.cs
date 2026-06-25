#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using AiNetLinter.Configuration;
using AiNetLinter.Output;
using AiNetLinter.Models;
using AiNetLinter.Core;

namespace AiNetLinter.Generators;

/// <summary>
/// Generiert ein Repository-Playbook (.md) mit Suppression-Statistiken und Architekturmustern.
/// </summary>
public sealed class RepoPlaybookGenerator
{
    private const string DisableMarker = "ainetlinter-disable";
    private const string AllKeyword = "all";
    private const string MultiLineCommentEnd = "*/";

    /// <summary>
    /// Generiert das Playbook und schreibt es in die angegebene Datei.
    /// </summary>
    /// <param name="solution">Die zu analysierende Roslyn-Solution.</param>
    /// <param name="outputPath">Der Pfad zur Ausgabedatei (.md).</param>
    /// <param name="options">Optionen für Verbosity, Konfiguration und Vorab-Violations.</param>
    /// <returns>Ein Task-Objekt für asynchrone Ausführung.</returns>
    public static async Task GenerateAsync(
        Solution solution,
        string outputPath,
        PlaybookOptions? options = null)
    {
        var content = await BuildContentAsync(solution, options);
        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (File.Exists(outputPath) && await File.ReadAllTextAsync(outputPath, Encoding.UTF8) == content)
        {
            if (options?.Verbose == true)
            {
                Console.WriteLine($"[INFO]: Repo-Playbook ist bereits aktuell (kein Schreibzugriff): {outputPath}");
            }
            return;
        }

        await File.WriteAllTextAsync(outputPath, content, Encoding.UTF8);
        if (options?.Verbose == true)
        {
            Console.WriteLine($"[INFO]: Repo-Playbook erfolgreich generiert unter: {outputPath}");
        }
    }

    /// <summary>
    /// Generiert den Playbook-Inhalt als String (ohne Datei zu schreiben).
    /// Für den --check-Modus und Tests.
    /// </summary>
    /// <param name="solution">Die zu analysierende Roslyn-Solution.</param>
    /// <param name="options">Optionen für Verbosity, Konfiguration und Vorab-Violations.</param>
    /// <returns>Der generierte Markdown-Inhalt.</returns>
    public static async Task<string> BuildContentAsync(
        Solution solution,
        PlaybookOptions? options = null)
    {
        var opts = options ?? new PlaybookOptions();
        var stats = await ScanSolutionAsync(solution, opts);
        var solutionDir = Path.GetDirectoryName(solution.FilePath) ?? string.Empty;
        var version = typeof(RepoPlaybookGenerator).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";
        return BuildContent(new PlaybookBuildContext(stats, solutionDir, opts.Config, opts.ConfigPath, version));
    }

    private static async Task<PlaybookStats> ScanSolutionAsync(Solution solution, PlaybookOptions opts)
    {
        int totalResultMethods = 0;
        int totalThrows = 0;
        var suppressionCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var docInfos = new List<PlaybookDocInfo>();

        foreach (var project in solution.Projects)
        {
            foreach (var document in project.Documents)
            {
                var docScan = await ScanDocumentAsync(document, suppressionCounts, opts.Config);
                totalResultMethods += docScan.ResultMethods;
                totalThrows += docScan.Throws;
                docInfos.Add(new PlaybookDocInfo(
                    document.FilePath ?? string.Empty,
                    project.Name,
                    docScan.HasDisableAll,
                    docScan.LineCount,
                    docScan.Namespaces
                ));
            }
        }

        List<RuleViolation> violations = new();
        if (opts.Config != null)
        {
            if (opts.PrecomputedViolations != null)
            {
                violations.AddRange(opts.PrecomputedViolations);
            }
            else
            {
                string? rulesJsonContent = null;
                if (!string.IsNullOrEmpty(opts.ConfigPath) && File.Exists(opts.ConfigPath))
                    rulesJsonContent = File.ReadAllText(opts.ConfigPath, Encoding.UTF8);
                var engine = new LinterEngine(opts.Config, rulesJsonContent);
                violations.AddRange(await engine.RunAsync(solution));
            }
        }

        return new PlaybookStats(totalResultMethods, totalThrows, suppressionCounts, docInfos, violations);
    }

    private static async Task<PlaybookDocScanResult> ScanDocumentAsync(Document document, Dictionary<string, int> suppressionCounts, Config? config)
    {
        var semanticModel = await document.GetSemanticModelAsync();
        var syntaxRoot = await document.GetSyntaxRootAsync();
        if (semanticModel == null || syntaxRoot == null)
        {
            return new PlaybookDocScanResult(0, 0, false, 0, []);
        }

        var effectiveConfig = config != null ? ProjectConfigResolver.ResolveForDocument(document, config) : null;
        var allowedExceptions = effectiveConfig?.Global?.AllowedExceptions;

        var walker = new PlaybookSyntaxWalker(semanticModel, allowedExceptions);
        walker.Visit(syntaxRoot);

        var text = (await document.GetTextAsync()).ToString();
        bool hasDisableAll = text.Contains("ainetlinter-disable all");

        CollectSuppressionsFromTrivia(syntaxRoot, suppressionCounts);

        var namespaces = syntaxRoot.DescendantNodes()
            .OfType<BaseNamespaceDeclarationSyntax>()
            .Select(ns => ns.Name.ToString())
            .Distinct()
            .ToList();

        int lineCount = syntaxRoot.GetText().Lines.Count;

        return new PlaybookDocScanResult(walker.ResultPatternCount, walker.ThrowCount, hasDisableAll, lineCount, namespaces);
    }

    private static void CollectSuppressionsFromTrivia(SyntaxNode root, Dictionary<string, int> suppressionCounts)
    {
        var triviaList = root.DescendantTrivia();
        foreach (var trivia in triviaList)
        {
            var kind = trivia.Kind();
            if (kind == SyntaxKind.SingleLineCommentTrivia || kind == SyntaxKind.MultiLineCommentTrivia)
            {
                ProcessComment(trivia.ToString(), suppressionCounts);
            }
        }
    }

    private static void ProcessComment(string commentText, Dictionary<string, int> suppressionCounts)
    {
        var idx = commentText.IndexOf(DisableMarker, StringComparison.Ordinal);
        if (idx < 0)
        {
            return;
        }

        var suffix = commentText.Substring(idx + DisableMarker.Length).Trim();
        var rule = GetRuleNameFromSuffix(suffix);

        suppressionCounts[rule] = suppressionCounts.TryGetValue(rule, out var currentCount) ? currentCount + 1 : 1;
    }

    private static string GetRuleNameFromSuffix(string suffix)
    {
        if (string.IsNullOrEmpty(suffix) || suffix.StartsWith(AllKeyword, StringComparison.OrdinalIgnoreCase))
        {
            return AllKeyword;
        }

        var parts = suffix.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return AllKeyword;
        }

        var rule = parts[0].Trim();
        if (rule.EndsWith(MultiLineCommentEnd, StringComparison.Ordinal))
        {
            rule = rule.Substring(0, rule.Length - MultiLineCommentEnd.Length).Trim();
        }

        return rule.TrimEnd(',', ';', '.', '\'', '"');
    }

    private static string BuildContent(PlaybookBuildContext ctx)
    {
        var sb = new StringBuilder();
        AppendHeader(sb, ctx);
        AppendSuppressionList(sb, ctx.Stats.SuppressionCounts, ctx.Config);
        sb.AppendLine();
        if (ctx.Config == null) return sb.ToString();
        var filesWithDisableAll = ctx.Stats.DocInfos
            .Where(d => d.HasDisableAll).Select(d => d.FilePath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var waveReadyViolations = ctx.Stats.Violations
            .Where(v => !filesWithDisableAll.Contains(v.FilePath)).ToList();
        AppendMigrationStatus(sb, ctx, waveReadyViolations);
        AppendArchitectureSlices(sb, ctx);
        AppendAgentPriority(sb, waveReadyViolations, ctx.Config);
        return sb.ToString();
    }

    private static void AppendHeader(StringBuilder sb, PlaybookBuildContext ctx)
    {
        sb.AppendLine("---");
        sb.AppendLine("description: Repo-Statistik, bei Architektur-Fragen lesen");
        sb.AppendLine("globs: ");
        sb.AppendLine("alwaysApply: false");
        sb.AppendLine("---");
        sb.AppendLine("# AI Repository Playbook (Auto-Generated)");
        sb.AppendLine($"Auto-generiert durch AiNetLinter {ctx.Version} aus `{ctx.ConfigPath}`.");
        sb.AppendLine("Dieses Dokument wurde automatisiert durch den **AiNetLinter** erzeugt.");
        sb.AppendLine("Es dient als Orientierungshilfe fuer KI-Assistenten (wie Cursor), um sich an die Codierungsrichtlinien, Architekturmuster und Ausnahmen dieser Codebase anzupassen.");
        sb.AppendLine();
        sb.AppendLine("## 1. Genutzte Architekturmuster");
        sb.AppendLine($"- **Result-Pattern-Nutzung:** {ctx.Stats.TotalResultMethods} Methoden liefern `Result` oder `Result<T>` zurueck.");
        sb.AppendLine($"- **Kontrollfluss-Exceptions:** {ctx.Stats.TotalThrows} `throw`-Anweisungen wurden im Code-Rumpf gefunden.");
        sb.AppendLine();
        sb.AppendLine("## 2. Abweichungen / Unterdrueckte Linter-Regeln");
    }

    private static void AppendMigrationStatus(StringBuilder sb, PlaybookBuildContext ctx, List<RuleViolation> waveReadyViolations)
    {
        sb.AppendLine("## 3. Migrations-Status");
        sb.AppendLine();
        int totalFiles = ctx.Stats.DocInfos.Count;
        int waveReadyFiles = ctx.Stats.DocInfos.Count(d => !d.HasDisableAll);
        double pct = totalFiles > 0 ? (double)waveReadyFiles / totalFiles * 100 : 0;
        sb.AppendLine($"- **Wave-ready Dateien:** {waveReadyFiles} / {totalFiles} ({pct:F0} %)");
        sb.AppendLine($"- **Verstösse nur wave-ready (default rules):** {waveReadyViolations.Count}");
        sb.AppendLine($"- **Top-Ordner wave-ready-Verstöße:**");
        var folderGroups = waveReadyViolations
            .Select(v => GetViolationFolder(v, ctx.SolutionDir))
            .GroupBy(d => d)
            .Select(g => new { Folder = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count).Take(3).ToList();
        if (folderGroups.Count == 0)
        {
            sb.AppendLine("  - Keine offenen Verstöße in wave-ready Dateien.");
        }
        else
        {
            foreach (var folder in folderGroups)
                sb.AppendLine($"  - `{folder.Folder}/`: {folder.Count}");
        }
        sb.AppendLine();
    }

    private static string GetViolationFolder(RuleViolation v, string solutionDir)
    {
        if (string.IsNullOrEmpty(v.FilePath) || string.IsNullOrEmpty(solutionDir)) return "Root";
        var rel = PathNormalizer.ToRelative(solutionDir, v.FilePath);
        var dir = Path.GetDirectoryName(rel)?.Replace('\\', '/');
        return string.IsNullOrEmpty(dir) ? "Root" : dir;
    }

    private static void AppendArchitectureSlices(StringBuilder sb, PlaybookBuildContext ctx)
    {
        sb.AppendLine("## 4. Architektur-Slices (nach Ordner)");
        sb.AppendLine();
        var sliceGroups = ctx.Stats.DocInfos
            .Select(d => new { Doc = d, Slice = GetFolderSlice(d.FilePath, ctx.SolutionDir) })
            .GroupBy(x => x.Slice)
            .Select(g =>
            {
                var docs = g.Select(x => x.Doc).ToList();
                var sortedLocs = docs.Select(d => d.LineCount).OrderBy(x => x).ToList();
                int medianLoc = sortedLocs.Count > 0 ? sortedLocs[sortedLocs.Count / 2] : 0;
                return new { Slice = g.Key, FileCount = docs.Count, MedianLoc = medianLoc, DisableAllCount = docs.Count(d => d.HasDisableAll) };
            })
            .OrderByDescending(x => x.FileCount).Take(5).ToList();
        foreach (var slice in sliceGroups)
        {
            var disableAllStr = slice.DisableAllCount > 0 ? $", {slice.DisableAllCount}× disable-all" : "";
            sb.AppendLine($"- **{slice.Slice}/**: {slice.FileCount} files, median Footprint {slice.MedianLoc} LOC{disableAllStr}");
        }
        sb.AppendLine();
    }

    private static void AppendAgentPriority(StringBuilder sb, List<RuleViolation> waveReadyViolations, Config config)
    {
        sb.AppendLine("## 5. Empfohlene Agenten-Priorität (aus RuleMetadata + Counts)");
        sb.AppendLine();
        sb.AppendLine("| Intent | Offene Verstöße (wave-ready) | Regeln |");
        sb.AppendLine("| :--- | ---: | :--- |");
        var intentGroups = waveReadyViolations
            .GroupBy(v => RuleMetadataRegistry.Resolve(v.RuleName ?? "", config).Intent)
            .Select(g => new { Intent = g.Key, Count = g.Count(), Rules = string.Join(", ", g.Select(v => v.RuleName).Distinct()) })
            .OrderByDescending(x => x.Count).ToList();
        if (intentGroups.Count == 0)
        {
            sb.AppendLine("| - | 0 | Keine offenen Verstöße |");
        }
        else
        {
            foreach (var group in intentGroups)
                sb.AppendLine($"| {group.Intent} | {group.Count} | {group.Rules} |");
        }
        sb.AppendLine();
    }

    private static string GetFolderSlice(string filePath, string solutionDir)
    {
        if (string.IsNullOrEmpty(filePath) || string.IsNullOrEmpty(solutionDir))
        {
            return "Root";
        }
        var rel = PathNormalizer.ToRelative(solutionDir, filePath).Replace('\\', '/');
        var parts = rel.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2) return string.Join("/", parts.Take(2));
        return parts.Length == 1 ? parts[0] : "Root";
    }

    private static void AppendSuppressionList(StringBuilder sb, Dictionary<string, int> suppressionCounts, Config? config)
    {
        if (suppressionCounts.Count == 0)
        {
            sb.AppendLine("In dieser Codebase sind aktuell keine Linter-Regeln unterdrueckt.");
            return;
        }

        sb.AppendLine("Folgende Regeln werden in diesem Projekt bewusst unterdrueckt:");
        sb.AppendLine();
        foreach (var item in suppressionCounts.OrderByDescending(x => x.Value))
        {
            var rule = item.Key;
            var count = item.Value;
            string description;
            if (rule.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                description = "Alle Linter-Regeln deaktiviert fuer diese Datei oder diesen Bereich.";
            }
            else
            {
                var meta = RuleRegistry.TryResolve(rule);
                description = meta != null && config != null
                    ? meta.GetShortDescription(config)
                    : meta != null
                        ? meta.GetShortDescription(new Config { Global = new(), Metrics = new() })
                        : $"Regel '{rule}'.";
            }
            sb.AppendLine($"- **{rule}:** {count} mal deaktiviert.");
            sb.AppendLine($"  *Bedeutung:* {description}");
        }
    }
}
