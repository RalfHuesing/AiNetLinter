#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.Text;
using AiNetLinter.Configuration;
using AiNetLinter.Core;
using AiNetLinter.Models;

namespace AiNetLinter.Output;

/// <summary>
/// Formatiert Regelverstöße als token-effiziente, LLM-optimierte Markdown-Ausgabe.
/// Struktur: Handlungsanweisung → Regellegende → Strukturelle Verstöße → Violations nach Datei.
/// </summary>
public static class ViolationMarkdownFormatter
{
    private static readonly HashSet<string> StructuralRules = new(StringComparer.OrdinalIgnoreCase)
    {
        LinterRuleIds.MaxPartialClassFiles,
        LinterRuleIds.AIContextFootprint,
    };

    private static readonly HashSet<string> AutoFixableRules = new(
        RuleRegistry.All.Where(r => r.HasAutoFix).Select(r => r.RuleId),
        StringComparer.OrdinalIgnoreCase);

    public static string Format(
        IReadOnlyCollection<RuleViolation> violations,
        string outputRoot,
        LinterConfig? config = null)
    {
        if (violations.Count == 0)
            return string.Empty;

        var byRule = ViolationSummaryBuilder.BuildByRule(violations, config);
        var hasAutoFix = violations.Any(v => AutoFixableRules.Contains(v.RuleName ?? string.Empty));
        var output = new StringBuilder();

        output.Append($"# AiNetLinter - {violations.Count} violations\n");
        output.Append(BuildSummaryTable(violations, byRule, outputRoot));
        output.Append(BuildInstructionBlock(outputRoot, hasAutoFix));
        output.Append(BuildRegellegende(byRule));

        var structural = violations
            .Where(v => StructuralRules.Contains(v.RuleName ?? string.Empty))
            .ToList();
        if (structural.Count > 0)
            output.Append(BuildStrukturelleVerstoesse(structural, outputRoot));

        output.Append(BuildViolationsByFile(violations, outputRoot));
        return output.ToString();
    }

    private static string BuildSummaryTable(
        IReadOnlyCollection<RuleViolation> violations,
        IReadOnlyList<RuleViolationCount> byRule,
        string outputRoot)
    {
        var hasStructural = byRule.Any(r => StructuralRules.Contains(r.RuleName));
        var sb = new StringBuilder();
        sb.Append('\n');
        if (hasStructural)
        {
            sb.Append("| Regel | Gesamt | Prod | Tests | Struktur |\n");
            sb.Append("|---|---:|---:|---:|:---:|\n");
        }
        else
        {
            sb.Append("| Regel | Gesamt | Prod | Tests |\n");
            sb.Append("|---|---:|---:|---:|\n");
        }

        foreach (var r in byRule)
        {
            var ruleViolations = violations
                .Where(v => string.Equals(v.RuleName, r.RuleName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var prodCount = 0;
            var testCount = 0;
            foreach (var v in ruleViolations)
            {
                var relPath = PathNormalizer.ToRelative(outputRoot, v.FilePath ?? string.Empty);
                if (PathNormalizer.IsTestFile(relPath))
                {
                    testCount++;
                }
                else
                {
                    prodCount++;
                }
            }

            var structMarker = StructuralRules.Contains(r.RuleName) ? "⚠" : string.Empty;

            if (hasStructural)
            {
                sb.Append($"| {r.RuleName} | {r.Count} | {prodCount} | {testCount} | {structMarker} |\n");
            }
            else
            {
                sb.Append($"| {r.RuleName} | {r.Count} | {prodCount} | {testCount} |\n");
            }
        }

        return sb.ToString();
    }

    private static string BuildInstructionBlock(string projectRoot, bool hasAutoFix)
    {
        var sb = new StringBuilder();
        sb.Append("\n## Handlungsanweisung\n\n");
        sb.Append("Analysiere die Violations im Kontext der Architektur und Coding-Richtlinien dieses Projekts.\n");

        var cursorRulesPath = Path.Combine(projectRoot, ".cursor", "rules");
        var claudeMdPath = Path.Combine(projectRoot, "CLAUDE.md");

        if (Directory.Exists(cursorRulesPath))
            sb.Append("Projektkonfiguration erkannt: `.cursor/rules` — Architektur-Constraints und Regeln dort beachten.\n");
        if (File.Exists(claudeMdPath))
            sb.Append("Projektkonfiguration erkannt: `CLAUDE.md` — Architektur-Constraints dort beachten.\n");

        sb.Append("\n**Schritt 1 — False-Positive-Prüfung (PFLICHT vor jeder Änderung)**\n");
        sb.Append("Prüfe für jede Violation: Ist das ein echter Verstoß oder ein False-Positive, der durch die Architektur des Projekts gerechtfertigt ist?\n");
        sb.Append("Konfigurationsoptionen erkunden:\n");
        var exeName = Path.GetFileNameWithoutExtension(Environment.ProcessPath) ?? "ainetlinter";
        sb.Append($"  `{exeName} --docs configuration`\n");
        sb.Append("Bei vermutetem False-Positive: Nutzer explizit informieren, Optionen mit Empfehlung nennen, Einverständnis einholen — BEVOR du etwas änderst.\n");

        sb.Append("\n**Schritt 2 — Behebung echter Violations**\n");
        sb.Append("Reihenfolge: Code-Fix → Konfigurationsanpassung → Suppression-Kommentar (letztes Mittel, nur nach Nutzer-Freigabe).\n");

        if (hasAutoFix)
        {
            sb.Append("\n**Auto-Fix verfuegbar** fuer markierte Violations [auto-fix]:\n");
            sb.Append($"  `{exeName} --path <pfad> --fix`\n");
            sb.Append("Pruefe den Fix im Dry-Run zuerst: `--fix --dry-run`\n");
        }

        sb.Append("\n> ⚠ **Strukturelle Regeln** (MaxPartialClassFiles, AIContextFootprint, MaxPublicMembersPerType) erfordern ");
        sb.Append("oft tiefgreifende Architektureingriffe. **Frage den Nutzer VOR der Umsetzung** — nicht eigenständig beginnen.\n\n");

        return sb.ToString();
    }

    private static string BuildRegellegende(IReadOnlyList<RuleViolationCount> byRule)
    {
        var sb = new StringBuilder();
        sb.Append("## Regellegende\n");
        foreach (var r in byRule)
            sb.Append(RuleLegendRegistry.Render(r.RuleName, r.Count, r.Intent));
        return sb.ToString();
    }

    private static string BuildStrukturelleVerstoesse(
        IReadOnlyList<RuleViolation> structural, string outputRoot)
    {
        var sb = new StringBuilder();
        sb.Append("\n## Strukturelle Verstöße\n");
        sb.Append("> ⚠ Diese Violations erfordern Architekturentscheidungen. **Nutzer VOR Beginn fragen.**\n");

        var byRule = structural
            .GroupBy(v => v.RuleName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase);

        foreach (var group in byRule)
            AppendStructuralGroup(sb, group, outputRoot);

        return sb.ToString();
    }

    private static void AppendStructuralGroup(
        StringBuilder sb,
        IGrouping<string, RuleViolation> group,
        string outputRoot)
    {
        sb.Append($"\n### {group.Key}\n");
        foreach (var v in group.OrderBy(x => PathNormalizer.ToRelative(outputRoot, x.FilePath), StringComparer.OrdinalIgnoreCase))
            AppendStructuralViolation(sb, v, outputRoot);
    }

    private static void AppendStructuralViolation(StringBuilder sb, RuleViolation v, string outputRoot)
    {
        var path = PathNormalizer.ToRelative(outputRoot, v.FilePath);
        sb.Append($"- {path}:{v.LineNumber}\n");
        if (string.IsNullOrWhiteSpace(v.Details)) return;

        foreach (var line in v.Details.Split('\n'))
        {
            var trimmed = line.TrimEnd();
            if (!string.IsNullOrEmpty(trimmed))
                sb.Append($"  {trimmed}\n");
        }
    }

    private static string BuildViolationsByFile(
        IReadOnlyCollection<RuleViolation> violations, string outputRoot)
    {
        var sb = new StringBuilder();
        sb.Append("\n## Violations nach Datei\n");

        var byFile = violations
            .GroupBy(v => PathNormalizer.ToRelative(outputRoot, v.FilePath), StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var prodGroups = byFile.Where(g => !PathNormalizer.IsTestFile(g.Key)).ToList();
        var testGroups = byFile.Where(g => PathNormalizer.IsTestFile(g.Key)).ToList();

        if (prodGroups.Count > 0)
        {
            var countWord = prodGroups.Count == 1 ? "Datei" : "Dateien";
            sb.Append($"\n### Produktion ({prodGroups.Count} {countWord})\n");
            foreach (var fileGroup in prodGroups)
            {
                AppendFileGroup(sb, fileGroup, outputRoot);
            }
        }

        if (testGroups.Count > 0)
        {
            var countWord = testGroups.Count == 1 ? "Datei" : "Dateien";
            sb.Append($"\n### Tests ({testGroups.Count} {countWord})\n");
            foreach (var fileGroup in testGroups)
            {
                AppendFileGroup(sb, fileGroup, outputRoot);
            }
        }

        return sb.ToString();
    }

    private static void AppendFileGroup(
        StringBuilder sb,
        IGrouping<string, RuleViolation> fileGroup,
        string outputRoot)
    {
        sb.Append($"\n#### {fileGroup.Key}\n");
        foreach (var v in fileGroup.OrderBy(x => x.LineNumber))
        {
            var fixTag = AutoFixableRules.Contains(v.RuleName ?? string.Empty) ? " [auto-fix]" : string.Empty;
            var structTag = StructuralRules.Contains(v.RuleName ?? string.Empty) ? " [→ strukturell]" : string.Empty;
            var detail = (v.Details ?? string.Empty).Split('\n')[0].TrimEnd();
            sb.Append($"- Z.{v.LineNumber} {v.RuleName}{fixTag}{structTag} — {detail}\n");
        }
    }
}
