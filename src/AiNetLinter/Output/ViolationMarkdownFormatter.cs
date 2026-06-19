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
        var exePath = Environment.ProcessPath ?? "ainetlinter";
        sb.Append($"  `{exePath} --docs configuration`\n");
        sb.Append("Bei vermutetem False-Positive: Nutzer explizit informieren, Optionen mit Empfehlung nennen, Einverständnis einholen — BEVOR du etwas änderst.\n");

        sb.Append("\n**Schritt 2 — Behebung echter Violations**\n");
        sb.Append("Reihenfolge: Code-Fix → Konfigurationsanpassung → Suppression-Kommentar (letztes Mittel, nur nach Nutzer-Freigabe).\n");

        if (hasAutoFix)
        {
            sb.Append("\n**Auto-Fix verfuegbar** fuer markierte Violations [auto-fix]:\n");
            sb.Append($"  `{exePath} --path <pfad> --fix`\n");
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
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase);

        foreach (var fileGroup in byFile)
        {
            sb.Append($"\n### {fileGroup.Key}\n");
            foreach (var v in fileGroup.OrderBy(x => x.LineNumber))
            {
                var fixTag = AutoFixableRules.Contains(v.RuleName ?? string.Empty) ? " [auto-fix]" : string.Empty;
                sb.Append($"- Z.{v.LineNumber} {v.RuleName}{fixTag} — {v.Details}\n");
            }
        }

        return sb.ToString();
    }
}
