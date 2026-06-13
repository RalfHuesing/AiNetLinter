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
    private sealed record RuleDescriptor(Func<GlobalConfig, bool> IsEnabled, string Name, string Description);

    private static readonly RuleDescriptor[] GlobalRules =
    [
        new(g => g.EnforceSealedClasses, "EnforceSealedClasses", "Konkrete Klassen müssen als `sealed` (oder `sealed partial`) deklariert sein."),
        new(g => g.EnforceNoSilentCatch, "EnforceNoSilentCatch", "Leere catch-Blöcke sind verboten. Benenne die Exception-Variable `ignored` oder wirf sie mit `throw;` weiter."),
        new(g => g.EnforceResultPatternOverExceptions, "EnforceResultPatternOverExceptions", "Nutze für fachlichen Kontrollfluss das Result-Pattern (`Result<T>`) statt Exceptions (`throw`)."),
        new(g => g.EnforceNullableEnable, "EnforceNullableEnable", "Nutze `#nullable enable` am Dateianfang."),
        new(g => g.EnforceValueObjectContracts, "EnforceValueObjectContracts", "Klassen mit Suffix `ValueObject` müssen `record` oder `readonly struct` sein."),
        new(g => g.EnforcePascalCase, "EnforcePascalCase", "Erzwingt PascalCase für öffentliche Typen/Methoden/Properties."),
        new(g => g.EnforceXmlDocumentation, "EnforceXmlDocumentation", "Erzwingt XML-Dokumentation für öffentliche APIs."),
        new(g => g.EnforceSemanticNaming, "EnforceSemanticNaming", "Keine generischen Namen (data, temp, obj) in öffentlichen Signaturen."),
        new(g => g.EnforceNoVariableShadowing, "EnforceNoVariableShadowing", "Verbietet das Verdecken von Feldern/Eigenschaften durch lokale Variablen."),
        new(g => g.EnforceReadonlyParameters, "EnforceReadonlyParameters", "Parameter dürfen innerhalb der Methode nicht neu zugewiesen werden."),
        new(g => g.EnforceReadonlyFields, "EnforceReadonlyFields", "Private Felder, die nur im Konstruktor zugewiesen werden, müssen `readonly` sein."),
        new(g => g.EnforceNoMagicValues, "EnforceNoMagicValues", "Keine Magic Numbers/Strings (erlaubt: 0, 1, -1, \"\")."),
        new(g => g.EnforceStrictBoundaryForBusinessLogic, "EnforceStrictBoundaryForBusinessLogic", "Business-Logic-Klassen (z. B. Calculator, Rule) dürfen keine I/O-Operationen durchführen."),
        new(g => g.PreventContextDependentOverloads, "PreventContextDependentOverloads", "Keine Methodenüberladungen mit identischer Parameteranzahl für primitive Typen."),
        new(g => g.RequireExplicitTruncationHandling, "RequireExplicitTruncationHandling", "Zeichenketten-Abschneiden (Truncation) nach I/O- und Stream-Leseoperationen muss explizit behandelt werden."),
        new(g => g.EnforceNamespaceDirectoryMapping, "EnforceNamespaceDirectoryMapping", "Der Namespace der Datei muss dem Pfad im Dateisystem entsprechen."),
        new(g => g.DetectAndBanPhantomDependencies, "DetectAndBanPhantomDependencies", "Banned dependencies detected (Phantom-Abhängigkeiten).")
    ];

    private sealed record OverrideDescriptor(Func<GlobalConfigOverride, bool?> GetVal, string Rule, string Desc);

    private static readonly OverrideDescriptor[] OverrideRules =
    [
        new(g => g.EnforceNoMagicValues, "EnforceNoMagicValues", "Deaktiviert (Literale Werte in Ausdrücken sind erlaubt)."),
        new(g => g.EnforceSealedClasses, "EnforceSealedClasses", "Deaktiviert (Klassen müssen nicht 'sealed' sein)."),
        new(g => g.EnforceResultPatternOverExceptions, "EnforceResultPatternOverExceptions", "Deaktiviert (Werfen von fachlichen Exceptions ist erlaubt)."),
        new(g => g.EnableTestSentinel, "EnableTestSentinel", "Deaktiviert (Test-Sentinel-Prüfung aus).")
    ];

    private sealed record MetricOverrideDescriptor(Func<MetricsConfigOverride, int?> GetVal, string Metric);

    private static readonly MetricOverrideDescriptor[] MetricOverrides =
    [
        new(m => m.MaxLineCount, "MaxLineCount"),
        new(m => m.MaxMethodLineCount, "MaxMethodLineCount"),
        new(m => m.MaxMethodParameterCount, "MaxMethodParameterCount")
    ];

    /// <summary>
    /// Generiert die MDC-Datei und schreibt sie nach .cursor/rules/AiNetLinter.mdc relativ zum angegebenen Pfad.
    /// </summary>
    /// <param name="targetPath">Der Pfad zum Zielverzeichnis oder der Solution.</param>
    /// <param name="config">Die geladene Linter-Konfiguration.</param>
    /// <param name="verbose">Gibt an, ob detaillierte Ausgaben protokolliert werden sollen.</param>
    public static void Sync(string targetPath, LinterConfig config, bool verbose)
    {
        string baseDir = ResolveBaseDirectory(targetPath);
        var cursorRulesDir = Path.Combine(baseDir, ".cursor", "rules");
        if (!Directory.Exists(cursorRulesDir))
        {
            Directory.CreateDirectory(cursorRulesDir);
        }

        var mdcPath = Path.Combine(cursorRulesDir, "AiNetLinter.mdc");
        var content = GenerateContent(config);
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

    private static string GenerateContent(LinterConfig config)
    {
        var sb = new StringBuilder();
        sb.AppendLine("---");
        sb.AppendLine("description: C#-Codequalität — Automatisch generierte AiNetLinter-Richtlinien (alwaysApply)");
        sb.AppendLine("globs: *.cs");
        sb.AppendLine("alwaysApply: true");
        sb.AppendLine("---");
        sb.AppendLine("# C#-Codequalität (AiNetLinter)");
        sb.AppendLine();
        sb.AppendLine("Diese Regeln werden automatisch aus der `rules.json` generiert und durch den `AiNetLinter` geprüft. Schreibe neuen Code direkt konform zu diesen Regeln.");
        sb.AppendLine();
        
        sb.AppendLine("## 1. Aktive Metriken & Grenzwerte (Produktions-Code)");
        sb.AppendLine();
        sb.AppendLine("| Regel | Limit | Beschreibung / Anweisung |");
        sb.AppendLine("| :--- | :---: | :--- |");
        sb.AppendLine($"| `MaxLineCount` | **{config.Metrics.MaxLineCount}** | Maximale Zeilenanzahl pro Datei. |");
        sb.AppendLine($"| `MaxMethodLineCount` | **{config.Metrics.MaxMethodLineCount}** | Codezeilen pro Methode (ohne Kommentare/Klammern). |");
        sb.AppendLine($"| `MaxMethodParameterCount` | **{config.Metrics.MaxMethodParameterCount}** | Parameter pro Methode. Bei Überschreitung einen `record` (Parameter Object) nutzen. |");
        sb.AppendLine($"| `MaxCyclomaticComplexity` | **{config.Metrics.MaxCyclomaticComplexity}** | Maximale zyklomatische Komplexität (McCabe) pro Methode. |");
        sb.AppendLine($"| `MaxCognitiveComplexity` | **{config.Metrics.MaxCognitiveComplexity}** | Maximale kognitive Komplexität pro Methode. |");
        sb.AppendLine($"| `MaxInheritanceDepth` | **{config.Metrics.MaxInheritanceDepth}** | Maximale Vererbungstiefe. |");
        sb.AppendLine($"| `MaxMethodOverloads` | **{config.Metrics.MaxMethodOverloads}** | Maximale Anzahl an Methodenüberladungen pro Name in einer Klasse. |");
        sb.AppendLine($"| `MaxConstructorDependencies` | **{config.Metrics.MaxConstructorDependencies}** | Maximale Anzahl an Konstruktor-Abhängigkeiten. |");
        sb.AppendLine($"| `MaxDirectoryDepth` | **{config.Metrics.MaxDirectoryDepth}** | Maximale Ordnertiefe ab csproj-Ebene. |");
        sb.AppendLine($"| `MaxAIContextFootprint` | **{config.Metrics.MaxAIContextFootprint}** | Maximale Anzahl transitiver Codezeilen von Klassenabhängigkeiten. |");
        sb.AppendLine();

        sb.AppendLine("## 2. Globale Qualitäts-Regeln (Aktiviert)");
        sb.AppendLine();
        AppendActiveGlobalRules(sb, config);
        sb.AppendLine();

        AppendProjectOverrides(sb, config);

        return sb.ToString();
    }

    private static void AppendActiveGlobalRules(StringBuilder sb, LinterConfig config)
    {
        var g = config.Global;
        foreach (var rule in GlobalRules)
        {
            if (rule.IsEnabled(g))
            {
                sb.AppendLine($"- **`{rule.Name}`**: {rule.Description}");
            }
        }

        if (g.ImmutabilityExemptSuffixes != null && g.ImmutabilityExemptSuffixes.Count > 0)
        {
            var suffixesStr = string.Join(", ", g.ImmutabilityExemptSuffixes.Select(s => $"`{s}`"));
            sb.AppendLine($"- **`ImmutabilityExemptSuffixes`**: Folgende Typ-Suffixe sind von der Immutability-Prüfung ausgenommen: {suffixesStr}.");
        }
    }

    private static void AppendProjectOverrides(StringBuilder sb, LinterConfig config)
    {
        if (config.ProjectOverrides == null || config.ProjectOverrides.Count == 0)
        {
            return;
        }

        sb.AppendLine("## 3. Projekt-spezifische Abweichungen (Overrides)");
        sb.AppendLine();
        sb.AppendLine("Der Linter erlaubt in bestimmten Projekten Abweichungen von den obigen Regeln:");
        sb.AppendLine();

        foreach (var pair in config.ProjectOverrides)
        {
            sb.AppendLine($"### Bereich: `{pair.Key}`");
            AppendSingleOverride(sb, pair.Value);
            sb.AppendLine();
        }
    }

    private static void AppendSingleOverride(StringBuilder sb, ProjectOverrideEntry overrides)
    {
        AppendGlobalOverrides(sb, overrides.Global);
        AppendMetricOverrides(sb, overrides.Metrics);
    }

    private static void AppendGlobalOverrides(StringBuilder sb, GlobalConfigOverride? g)
    {
        if (g == null) return;
        foreach (var rule in OverrideRules)
        {
            if (rule.GetVal(g) == false)
            {
                sb.AppendLine($"- **`{rule.Rule}`**: {rule.Desc}");
            }
        }
    }

    private static void AppendMetricOverrides(StringBuilder sb, MetricsConfigOverride? m)
    {
        if (m == null) return;
        foreach (var metric in MetricOverrides)
        {
            var val = metric.GetVal(m);
            if (val.HasValue)
            {
                sb.AppendLine($"- **`{metric.Metric}`**: Geändert auf **{val.Value}**.");
            }
        }
    }
}
