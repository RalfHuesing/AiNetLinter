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
    private sealed record RuleDefinition(
        string Name,
        Func<GlobalConfig, bool> IsEnabled,
        string DeactiveDesc,
        string CursorHint
    );

    private sealed record MetricDescriptor(Func<MetricsConfig, int> GetVal, string Name, string Praxis);

    private static readonly string[] IntentOrder =
        ["agent-resilience", "agent-context", "architecture", "aspnet-binding", "test-coverage", "control-flow", "csharp-idiom", "general"];

    private static readonly RuleDefinition[] GlobalRules =
    [
        new("EnforceSealedClasses", g => g.EnforceSealedClasses,
            "aus — Migration, Ziel: sealed standard",
            "`sealed` für konkrete Klassen; Ausnahmen: Suffixe in `rules.json → SealedClassExemptSuffixes`."),
        new("AllowUnsealedPartialClasses", g => g.AllowUnsealedPartialClasses,
            "aus — alle partial Klassen müssen ebenfalls sealed sein",
            "Unversiegelte `partial` Klassen erlaubt (z. B. Blazor-Komponenten)."),
        new("AllowDynamic", g => g.AllowDynamic,
            "aus — Verwendung von `dynamic` ist verboten",
            "`dynamic` ist verboten."),
        new("AllowOutParameters", g => g.AllowOutParameters,
            "aus — Verwendung von `out` Parametern ist verboten",
            "`out` Parameter verboten; Ausnahme: `Try*`-Methoden."),
        new("AllowTryPatternOutParameters", g => g.AllowTryPatternOutParameters,
            "aus — `out` Parameter in `Try*` Methoden sind verboten",
            "`out` in `Try*`-Methoden erlaubt."),
        new("EnforceValueObjectContracts", g => g.EnforceValueObjectContracts,
            "aus — keine Prüfung für ValueObjects",
            "Klassen mit `*ValueObject`-Suffix: `record` oder `readonly struct`."),
        new("EnableTestSentinel", g => g.EnableTestSentinel,
            "aus — keine Test-Sentinel-Prüfung",
            "Für komplexe Typen: Testklasse, `typeof(T)` oder `// @covers T`."),
        new("EnforcePascalCase", g => g.EnforcePascalCase,
            "aus — keine Namenskonventionsprüfung",
            "Öffentliche Typen/Methoden/Properties: PascalCase."),
        new("EnforceXmlDocumentation", g => g.EnforceXmlDocumentation,
            "aus — keine XML-Dokumentationsprüfung",
            "XML-Dokumentation für öffentliche APIs."),
        new("EnforceSemanticNaming", g => g.EnforceSemanticNaming,
            "aus — keine semantische Namensprüfung",
            "Keine `data`/`temp`/`obj` in öffentlichen Signaturen."),
        new("EnforceNullableEnable", g => g.EnforceNullableEnable,
            "aus — keine Nullable-Prüfung",
            "`#nullable enable` am Dateianfang jeder `.cs`-Datei."),
        new("EnforceNoSilentCatch", g => g.EnforceNoSilentCatch,
            "aus — leere catch-Blöcke sind erlaubt",
            "`catch` immer mit Log + sichtbarem Fehler oder `throw;` — nie leer."),
        new("AllowCancellationShutdownCatch", g => g.AllowCancellationShutdownCatch,
            "aus — kein stummes Abfangen von OperationCanceledException",
            "`OperationCanceledException` beim Shutdown abfangen erlaubt."),
        new("EnforceMinimalApiAsParameters", g => g.EnforceMinimalApiAsParameters,
            "aus — keine AsParameters-Pflicht",
            "Minimal-API: >4 Parameter → `[AsParameters]` + `record`."),
        new("EnforceResultPatternOverExceptions", g => g.EnforceResultPatternOverExceptions,
            "aus — 170 `throw` im Repo; Ziel: Result für Domänenfehler",
            "`Result<T>` für Domänenfehler; `throw` nur für Infrastruktur-Fehler."),
        new("EnforceNoVariableShadowing", g => g.EnforceNoVariableShadowing,
            "aus — Variablenüberdeckung erlaubt",
            "Keine lokale Variable gleich benannt wie Feld/Property/Parameter."),
        new("EnforceReadonlyParameters", g => g.EnforceReadonlyParameters,
            "aus — Parameterzuweisungen erlaubt",
            "Parameter nicht reassignen — neue lokale Variable nutzen."),
        new("EnforceReadonlyFields", g => g.EnforceReadonlyFields,
            "aus — keine readonly-Pflicht für Felder",
            "Nur im Ctor gesetzte private Felder als `readonly` markieren."),
        new("EnforceNoMagicValues", g => g.EnforceNoMagicValues,
            "aus — Literale Werte in Ausdrücken sind erlaubt",
            "Keine Magic Numbers/Strings — benannte Konstanten verwenden."),
        new("EnforceExplicitStateImmutability", g => g.EnforceExplicitStateImmutability,
            "aus — Blazor/Handler-State; Zielbild: `platform-ai-strict`",
            "Felder und Properties `readonly`/`init`-only."),
        new("PreventContextDependentOverloads", g => g.PreventContextDependentOverloads,
            "aus — Überladungen erlaubt",
            "Keine Überladungen mit identischer Parameteranzahl für primitive Typen."),
        new("EnforceNamespaceDirectoryMapping", g => g.EnforceNamespaceDirectoryMapping,
            "aus — Namespaces können frei gewählt werden",
            "Namespace muss Verzeichnispfad entsprechen (Modus: `rules.json`)."),
        new("DetectAndBanPhantomDependencies", g => g.DetectAndBanPhantomDependencies,
            "aus — Phantom-Abhängigkeiten erlaubt",
            "Keine unauflösbaren `using`; kein `Type.GetType`/`Activator.CreateInstance` für App-Typen."),
        new("AllowedEmptyReads", g => g.AllowedEmptyReads,
            "aus — leere Leseoperationen verboten",
            "Leseoperationen immer mit unmittelbarem Guard versehen."),
    ];

    private static readonly MetricDescriptor[] MetricsList =
    [
        new(m => m.MaxLineCount, "MaxLineCount", "Datei splitten wenn sie wächst."),
        new(m => m.MaxMethodLineCount, "MaxMethodLineCount", "Eine Aufgabe pro Methode; Rest extrahieren."),
        new(m => m.MaxMethodParameterCount, "MaxMethodParameterCount", "Ab Überschreitung: `record` als Parameter-Object."),
        new(m => m.MaxCyclomaticComplexity, "MaxCyclomaticComplexity", "Weniger `if`/`switch`/`&&`/`||` pro Methode (McCabe)."),
        new(m => m.MaxCognitiveComplexity, "MaxCognitiveComplexity", "Weniger Verschachtelung; Early Return bevorzugen (kognitiv)."),
        new(m => m.MaxInheritanceDepth, "MaxInheritanceDepth", "Komposition vor Vererbung."),
        new(m => m.MaxMethodOverloads, "MaxMethodOverloads", "Methoden mit eindeutigen Namen bevorzugen."),
        new(m => m.MaxConstructorDependencies, "MaxConstructorDependencies", "Verantwortlichkeit aufteilen bei Überschreitung."),
        new(m => m.MaxDirectoryDepth, "MaxDirectoryDepth", "Ordner nicht unnötig tief schachteln."),
        new(m => m.MaxDirectoryChildren, "MaxDirectoryChildren", "0 = deaktiviert; zu viele Dateien/Unterordner → Unterverzeichnis anlegen."),
        new(m => m.MaxBoolParameterCount, "MaxBoolParameterCount", "0 = deaktiviert; bool-Parameter in Parameter-Object bündeln."),
        new(m => m.MaxPartialClassFiles, "MaxPartialClassFiles", "0 = deaktiviert; Logik in eigenständige Klassen auslagern (z. B. XyzChecker)."),
        new(m => m.MaxPublicMembersPerType, "MaxPublicMembersPerType", "0 = deaktiviert; Typ aufteilen oder Member kapseln."),
        new(m => m.MaxAIContextFootprint, "MaxAIContextFootprint", "Kopplung reduzieren; eigene Typen-Abhängigkeiten minimieren."),
    ];

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
        sb.AppendLine("Details: `rules.json`, `AiNetLinter.exe --readme`.");

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

    private static void AppendMetricsTable(StringBuilder sb, LinterConfig config)
    {
        sb.AppendLine("## Grenzwerte (Produktion)");
        sb.AppendLine("| Regel | Limit | Praxis |");
        sb.AppendLine("| :--- | :---: | :--- |");
        foreach (var metric in MetricsList)
        {
            var val = metric.GetVal(config.Metrics);
            sb.AppendLine($"| `{metric.Name}` | **{val}** | {metric.Praxis} |");
        }
        sb.AppendLine();
    }

    private static void AppendActiveRulesByIntent(StringBuilder sb, LinterConfig config)
    {
        var g = config.Global;
        var activeRules = GlobalRules
            .Where(r => r.IsEnabled(g))
            .Select(r => (Rule: r, Intent: RuleMetadataRegistry.Resolve(r.Name, config).Intent))
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
                sb.AppendLine($"- **{rule.Name}** — {rule.CursorHint}");
            sb.AppendLine();
        }

        sb.AppendLine("Ausnahmelisten (Immutability, Sealed, Namespace-Segmente): `rules.json`.");
        sb.AppendLine();
    }

    private static void AppendDisabledCompact(StringBuilder sb, LinterConfig config)
    {
        var g = config.Global;
        var disabledNames = GlobalRules
            .Where(r => !r.IsEnabled(g) && !r.Name.StartsWith("Allow", StringComparison.Ordinal))
            .Select(r => $"`{r.Name}`")
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
        foreach (var metric in MetricsList)
        {
            var prop = typeof(MetricsConfigOverride).GetProperty(metric.Name);
            if (prop?.GetValue(overrides.Metrics) is int val)
                parts.Add($"`{metric.Name}` **{val}**");
        }
    }

    private static void CollectGlobalOverrideParts(ProjectOverrideEntry overrides, List<string> parts)
    {
        if (overrides.Global == null) return;
        foreach (var rule in GlobalRules)
        {
            var prop = typeof(GlobalConfigOverride).GetProperty(rule.Name);
            if (prop?.GetValue(overrides.Global) is bool val)
                parts.Add($"`{rule.Name}` {(val ? "ein" : "aus")}");
        }
    }
}
