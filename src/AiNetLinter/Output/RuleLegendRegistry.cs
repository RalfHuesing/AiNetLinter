#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.Text;
using AiNetLinter.Core;

namespace AiNetLinter.Output;

public sealed record RuleLegendEntry(string Warum, string[] Alternativen, string? SicherheitsHinweis = null);

/// <summary>
/// Enthält Warum-Beschreibungen und Fix-Alternativen für alle bekannten Linter-Regeln.
/// Neue Regeln in der RuleRegistry ergänzen.
/// </summary>
public static class RuleLegendRegistry
{
    public static bool HasEntry(string ruleName) => RuleRegistry.TryResolve(ruleName) != null;

    public static IReadOnlyCollection<string> KnownRuleNames =>
        RuleRegistry.All.Where(r => !string.IsNullOrEmpty(r.Warum)).Select(r => r.RuleId).ToList().AsReadOnly();

    public static RuleLegendEntry? TryGet(string ruleName)
    {
        var meta = RuleRegistry.TryResolve(ruleName);
        if (meta == null) return null;
        return new RuleLegendEntry(meta.Warum, meta.Alternativen, meta.SicherheitsHinweis);
    }

    internal static string Render(string ruleName, int count, string intent)
    {
        var sb = new StringBuilder();
        var plural = count == 1 ? "Verstoß" : "Verstösse";
        sb.Append($"\n### {ruleName} — {count} {plural} [{intent}]\n");

        var entry = RuleRegistry.TryResolve(ruleName);
        if (entry == null)
        {
            sb.Append("Keine spezifische Anleitung hinterlegt — behebe gemäß Projektrichtlinien.\n");
            return sb.ToString();
        }

        sb.Append($"**Warum:** {entry.Warum}\n\n");
        sb.Append("**Fix-Alternativen:**\n");
        foreach (var alt in entry.Alternativen)
            sb.Append($"- {alt}\n");

        if (entry.SicherheitsHinweis != null)
            sb.Append($"\n> ⚠ {entry.SicherheitsHinweis}\n");

        return sb.ToString();
    }
}
