#nullable enable

using System;
using System.Linq;
using System.Text;
using AiNetLinter.Core;
using AiNetLinter.Output;

namespace AiNetLinter.Commands;

/// <summary>
/// Gibt Regelinformationen aus: Übersicht, Detail-Beschreibung oder Volltextsuche.
/// </summary>
internal static class ListRulesCommand
{
    internal static int ListAll(ILintConsole? console = null)
    {
        var c = console ?? LinterConsole.Instance;
        var sb = new StringBuilder();
        sb.AppendLine("# AiNetLinter — Regeluebersicht");
        sb.AppendLine();
        sb.AppendLine("| RuleId | Bezeichnung | Intent | Severity | Auto-Fix |");
        sb.AppendLine("|:---|:---|:---|:---|:---|");

        foreach (var rule in RuleRegistry.All)
        {
            var autoFix = rule.HasAutoFix ? "ja (--fix)" : "-";
            sb.AppendLine($"| {rule.RuleId} | {rule.DisplayName} | {rule.Intent} | {rule.Severity} | {autoFix} |");
        }

        c.WriteLine(sb.ToString().TrimEnd());
        return 0;
    }

    internal static int DescribeOne(string ruleId, ILintConsole? console = null)
    {
        var c = console ?? LinterConsole.Instance;
        var rule = RuleRegistry.TryResolve(ruleId);
        if (rule == null)
        {
            c.WriteError($"[ERROR]: Unbekannte Regel '{ruleId}'. Nutze --list-rules fuer eine Uebersicht.");
            return 1;
        }

        var sb = new StringBuilder();
        sb.AppendLine($"## {rule.RuleId} — {rule.DisplayName}");
        sb.AppendLine();
        sb.AppendLine($"**Intent:** {rule.Intent} | **Severity:** {rule.Severity} | **Auto-Fix:** {(rule.HasAutoFix ? "ja (--fix)" : "nein")}");

        if (!string.IsNullOrWhiteSpace(rule.Warum))
        {
            sb.AppendLine();
            sb.AppendLine("**Warum:**");
            sb.AppendLine(rule.Warum);
        }

        if (rule.Alternativen.Length > 0)
        {
            sb.AppendLine();
            sb.AppendLine("**Fix-Alternativen:**");
            foreach (var alt in rule.Alternativen)
                sb.AppendLine($"- {alt}");
        }

        if (!string.IsNullOrWhiteSpace(rule.SicherheitsHinweis))
        {
            sb.AppendLine();
            sb.AppendLine($"**Sicherheitshinweis:** {rule.SicherheitsHinweis}");
        }

        c.WriteLine(sb.ToString().TrimEnd());
        return 0;
    }

    internal static int Search(string term, ILintConsole? console = null)
    {
        var c = console ?? LinterConsole.Instance;

        var matches = RuleRegistry.All
            .Where(r => Contains(r.RuleId, term)
                     || Contains(r.DisplayName, term)
                     || Contains(r.Warum, term)
                     || Contains(r.Intent, term))
            .ToList();

        if (matches.Count == 0)
        {
            c.WriteLine($"Keine Regeln gefunden fuer Suchbegriff: '{term}'");
            return 0;
        }

        var sb = new StringBuilder();
        sb.AppendLine($"# Suchergebnisse fuer '{term}' ({matches.Count} Treffer)");

        foreach (var rule in matches)
        {
            sb.AppendLine();
            sb.AppendLine($"## {rule.RuleId} — {rule.DisplayName}");
            sb.AppendLine($"Intent: {rule.Intent} | Severity: {rule.Severity} | Auto-Fix: {(rule.HasAutoFix ? "ja" : "nein")}");
            if (!string.IsNullOrWhiteSpace(rule.Warum))
                sb.AppendLine(rule.Warum);
        }

        c.WriteLine(sb.ToString().TrimEnd());
        return 0;
    }

    private static bool Contains(string source, string term) =>
        source.Contains(term, StringComparison.OrdinalIgnoreCase);
}
