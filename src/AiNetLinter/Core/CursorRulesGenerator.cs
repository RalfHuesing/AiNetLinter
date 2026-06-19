#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using AiNetLinter.Configuration;

namespace AiNetLinter.Core;

/// <summary>
/// Generiert eine Cursor-Regeldatei (.mdc) basierend auf der aktuellen Linter-Konfiguration.
/// </summary>
public static class CursorRulesGenerator
{


    /// <summary>
    /// Generiert die MDC-Datei und schreibt sie nach .cursor/rules/AiNetLinter.mdc relativ zum angegebenen Pfad.
    /// </summary>
    public static void Sync(string targetPath, LinterConfig config, bool verbose, string configPath = "rules.json")
    {
        string baseDir = ResolveBaseDirectory(targetPath);
        var cursorRulesDir = Path.Combine(baseDir, ".cursor", "rules");
        if (!Directory.Exists(cursorRulesDir))
        {
            Directory.CreateDirectory(cursorRulesDir);
        }

        var mdcPath = Path.Combine(cursorRulesDir, "AiNetLinter.mdc");
        var content = GenerateContent(config, configPath);

        if (File.Exists(mdcPath) && File.ReadAllText(mdcPath, Encoding.UTF8) == content)
        {
            if (verbose)
            {
                Console.WriteLine($"[INFO]: Cursor-Regeldatei ist bereits aktuell (kein Schreibzugriff): {mdcPath}");
            }
            return;
        }

        File.WriteAllText(mdcPath, content, Encoding.UTF8);

        if (verbose)
        {
            Console.WriteLine($"[INFO]: Cursor-Regeldatei erfolgreich synchronisiert unter: {mdcPath}");
        }
    }

    private static string ResolveBaseDirectory(string targetPath)
    {
        if (Directory.Exists(targetPath))
        {
            return targetPath;
        }
        if (File.Exists(targetPath))
        {
            return Path.GetDirectoryName(targetPath) ?? targetPath;
        }
        return targetPath;
    }

    /// <summary>
    /// Generiert den Inhalt für die MDC-Regeldatei.
    /// </summary>
    public static string GenerateContent(LinterConfig config, string configPath)
    {
        var sb = new StringBuilder();
        var version = typeof(CursorRulesGenerator).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";

        AppendFrontmatter(sb, version, configPath);
        AppendKurzStil(sb, config);
        AppendMetricsTable(sb, config);
        AppendActiveRulesByIntent(sb, config);
        AppendDisabledCompact(sb, config);
        AppendProjectOverridesDelta(sb, config);
        sb.AppendLine("Details: `rules.json`, `AiNetLinter.exe --docs <name>`.");

        return sb.ToString();
    }

    private static void AppendFrontmatter(StringBuilder sb, string version, string configPath)
    {
        sb.AppendLine("---");
        sb.AppendLine("description: C#-Codequalität — Automatisch generierte AiNetLinter-Richtlinien (alwaysApply)");
        sb.AppendLine("globs: *.cs");
        sb.AppendLine("alwaysApply: true");
        sb.AppendLine("---");
        sb.AppendLine("# C#-Codequalität (AiNetLinter)");
        sb.AppendLine($"Auto-generiert durch AiNetLinter {version} aus `{configPath}`. Neuen Produktionscode direkt konform schreiben.");
        sb.AppendLine();
    }

    private static void AppendKurzStil(StringBuilder sb, LinterConfig config)
    {
        var g = config.Global;
        var m = config.Metrics;
        sb.AppendLine("## Kurz-Stil");
        if (g.EnforceSealedClasses)
        {
            var partialNote = g.AllowUnsealedPartialClasses ? " Blazor-`partial` ohne `sealed` OK." : "";
            sb.AppendLine($"- `sealed` für konkrete Klassen.{partialNote}");
        }
        sb.AppendLine($"- Kurze, flache Methoden (≤{m.MaxMethodLineCount} Zeilen); ab {m.MaxMethodParameterCount + 1} Parametern ein Input-`record`.");
        if (g.EnforceNullableEnable)
            sb.AppendLine("- `#nullable enable` am Dateianfang.");
        if (g.EnforceNoSilentCatch)
            sb.AppendLine("- Kein leeres `catch` — Log + sichtbarer Fehler oder `throw;`.");
        AppendDynamicOutRestrictions(sb, g);
        sb.AppendLine($"- Klassen-Kopplung (Footprint) klein halten: max. {m.MaxAIContextFootprint} transitive Zeilen eigener Typen.");
        sb.AppendLine();
    }

    private static void AppendDynamicOutRestrictions(StringBuilder sb, GlobalConfig g)
    {
        if (g.AllowDynamic && g.AllowOutParameters) return;
        var parts = new List<string>();
        if (!g.AllowDynamic) parts.Add("kein `dynamic`");
        if (!g.AllowOutParameters)
        {
            var outText = g.AllowTryPatternOutParameters ? "`out` nur in `Try*`-Methoden" : "kein `out`";
            if (g.AllowOutParametersInPrivateMethods)
                outText += " (private Methoden ausgenommen)";
            parts.Add(outText);
        }
        var joined = string.Join("; ", parts);
        var bullet = joined.Length > 0 ? char.ToUpperInvariant(joined[0]) + joined[1..] : joined;
        sb.AppendLine($"- {bullet}.");
    }

    private static readonly string[] IntentOrder =
        ["agent-resilience", "agent-context", "architecture", "aspnet-binding", "test-coverage", "control-flow", "csharp-idiom", "general"];

    private static void AppendMetricsTable(StringBuilder sb, LinterConfig config)
    {
        sb.AppendLine("## Grenzwerte (Produktion)");
        sb.AppendLine("| Regel | Limit | Praxis |");
        sb.AppendLine("| :--- | :---: | :--- |");
        foreach (var metric in RuleRegistry.All.Where(r => r.IsMetric))
        {
            var val = metric.GetMetricLimit != null ? metric.GetMetricLimit(config) : 0;
            sb.AppendLine($"| `{metric.RuleId}` | **{val}** | {metric.CursorHint} |");
        }
        sb.AppendLine();
    }

    private static void AppendActiveRulesByIntent(StringBuilder sb, LinterConfig config)
    {
        var activeRules = RuleRegistry.All
            .Where(r => r.IncludeInCursorRules && !r.IsMetric && r.IsEnabled(config))
            .Select(r => (Rule: r, Intent: RuleMetadataRegistry.Resolve(r.RuleId, config).Intent))
            .ToList();

        var groups = activeRules
            .GroupBy(x => x.Intent)
            .OrderBy(grp =>
            {
                var idx = Array.IndexOf(IntentOrder, grp.Key);
                return idx >= 0 ? idx : IntentOrder.Length;
            });

        foreach (var group in groups)
        {
            if (group.Key == "agent-context") continue;
            sb.AppendLine($"## {group.Key}");
            foreach (var (rule, _) in group)
            {
                var displayName = rule.RuleId == "StaticTestSentinel" ? "EnableTestSentinel" : rule.RuleId;
                sb.AppendLine($"- **{displayName}** — {rule.CursorHint}");
            }
            sb.AppendLine();
        }

        sb.AppendLine("Ausnahmelisten (Immutability, Sealed, Namespace-Segmente): `rules.json`.");
        sb.AppendLine();
    }

    private static void AppendDisabledCompact(StringBuilder sb, LinterConfig config)
    {
        var g = config.Global;
        var disabledNames = RuleRegistry.All
            .Where(r => r.IncludeInCursorRules && !r.IsMetric && !r.IsEnabled(config) && !r.RuleId.StartsWith("Allow", StringComparison.Ordinal))
            .Select(r => r.RuleId == "StaticTestSentinel" ? "`EnableTestSentinel`" : $"`{r.RuleId}`")
            .ToList();

        sb.AppendLine("## Deaktiviert");
        if (disabledNames.Count > 0)
            sb.AppendLine($"Linter erzwingt nicht (trotzdem anstreben): {string.Join(", ", disabledNames)}.");

        var forbidden = new List<string>();
        if (!g.AllowDynamic) forbidden.Add("`dynamic`");
        if (!g.AllowOutParameters)
            forbidden.Add(g.AllowTryPatternOutParameters ? "`out` (außer in `Try*`)" : "`out`");

        if (forbidden.Count > 0)
            sb.AppendLine($"Trotzdem immer verboten: {string.Join("; ", forbidden)}.");
        sb.AppendLine();
    }

    private static void AppendProjectOverridesDelta(StringBuilder sb, LinterConfig config)
    {
        if (config.ProjectOverrides == null || config.ProjectOverrides.Count == 0)
            return;

        sb.AppendLine("## Projekt-Overrides (nur Abweichungen)");
        sb.AppendLine();

        foreach (var pair in config.ProjectOverrides)
        {
            var parts = CollectOverrideParts(pair.Value);
            if (parts.Count > 0)
                sb.AppendLine($"**`{pair.Key}`:** {string.Join("; ", parts)}. Details: `rules.json`.");
        }

        sb.AppendLine();
    }

    private static List<string> CollectOverrideParts(ProjectOverrideEntry overrides)
    {
        var parts = new List<string>();
        CollectMetricOverrideParts(overrides, parts);
        CollectGlobalOverrideParts(overrides, parts);
        return parts;
    }

    private static void CollectMetricOverrideParts(ProjectOverrideEntry overrides, List<string> parts)
    {
        if (overrides.Metrics == null) return;
        foreach (var metric in RuleRegistry.All.Where(r => r.IsMetric))
        {
            var prop = typeof(MetricsConfigOverride).GetProperty(metric.RuleId);
            if (prop?.GetValue(overrides.Metrics) is int val)
                parts.Add($"`{metric.RuleId}` **{val}**");
        }
    }

    private static void CollectGlobalOverrideParts(ProjectOverrideEntry overrides, List<string> parts)
    {
        if (overrides.Global == null) return;
        foreach (var rule in RuleRegistry.All.Where(r => r.IncludeInCursorRules && !r.IsMetric))
        {
            var propName = rule.RuleId == "StaticTestSentinel" ? "EnableTestSentinel" : rule.RuleId;
            var prop = typeof(GlobalConfigOverride).GetProperty(propName);
            if (prop?.GetValue(overrides.Global) is bool val)
                parts.Add($"`{rule.RuleId}` {(val ? "ein" : "aus")}");
        }
    }
}
