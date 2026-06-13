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
        string ActiveDesc,
        string DeactiveDesc
    );

    private static readonly RuleDefinition[] GlobalRules =
    [
        new("EnforceSealedClasses", g => g.EnforceSealedClasses,
            "Konkrete Klassen müssen als `sealed` (oder `sealed partial`) deklariert sein.",
            "aus — Migration, Ziel: sealed standard"),
        new("AllowUnsealedPartialClasses", g => g.AllowUnsealedPartialClasses,
            "Erlaubt unversiegelte `partial` Klassen (z. B. für Blazor Komponenten).",
            "aus — alle partial Klassen müssen ebenfalls sealed sein"),
        new("AllowDynamic", g => g.AllowDynamic,
            "Erlaubt die Verwendung von `dynamic`.",
            "aus — Verwendung von `dynamic` ist verboten"),
        new("AllowOutParameters", g => g.AllowOutParameters,
            "Erlaubt die Verwendung von `out` Parametern.",
            "aus — Verwendung von `out` Parametern ist verboten"),
        new("AllowTryPatternOutParameters", g => g.AllowTryPatternOutParameters,
            "Erlaubt `out` Parameter in `Try*` Methoden.",
            "aus — `out` Parameter in `Try*` Methoden sind verboten"),
        new("EnforceValueObjectContracts", g => g.EnforceValueObjectContracts,
            "Klassen mit Suffix `ValueObject` müssen `record` oder `readonly struct` sein.",
            "aus — keine Prüfung für ValueObjects"),
        new("EnableTestSentinel", g => g.EnableTestSentinel,
            "Aktiviert den Static Test Sentinel (Prüfung auf Testabdeckung).",
            "aus — keine Test-Sentinel-Prüfung"),
        new("EnforcePascalCase", g => g.EnforcePascalCase,
            "Erzwingt PascalCase für öffentliche Typen/Methoden/Properties.",
            "aus — keine Namenskonventionsprüfung"),
        new("EnforceXmlDocumentation", g => g.EnforceXmlDocumentation,
            "Erzwingt XML-Dokumentation für öffentliche APIs.",
            "aus — keine XML-Dokumentationsprüfung"),
        new("EnforceSemanticNaming", g => g.EnforceSemanticNaming,
            "Keine generischen Namen (data, temp, obj) in öffentlichen Signaturen.",
            "aus — keine semantische Namensprüfung"),
        new("EnforceNullableEnable", g => g.EnforceNullableEnable,
            "Nutze `#nullable enable` am Dateianfang.",
            "aus — keine Nullable-Prüfung"),
        new("EnforceNoSilentCatch", g => g.EnforceNoSilentCatch,
            "Leere catch-Blöcke sind verboten. Benenne die Exception-Variable `ignored` oder wirf sie mit `throw;` weiter.",
            "aus — leere catch-Blöcke sind erlaubt"),
        new("AllowCancellationShutdownCatch", g => g.AllowCancellationShutdownCatch,
            "Erlaubt das Abfangen von OperationCanceledException beim Shutdown.",
            "aus — kein stummes Abfangen von OperationCanceledException"),
        new("EnforceMinimalApiAsParameters", g => g.EnforceMinimalApiAsParameters,
            "Erzwingt `[AsParameters]` in Minimal-APIs bei mehr als 4 Parametern.",
            "aus — keine AsParameters-Pflicht"),
        new("EnforceResultPatternOverExceptions", g => g.EnforceResultPatternOverExceptions,
            "Nutze für fachlichen Kontrollfluss das Result-Pattern (`Result<T>`) statt Exceptions (`throw`).",
            "aus — 170 `throw` im Repo; Ziel: Result für Domänenfehler"),
        new("EnforceNoVariableShadowing", g => g.EnforceNoVariableShadowing,
            "Verbietet das Verdecken von Feldern/Eigenschaften durch lokale Variablen.",
            "aus — Variablenüberdeckung erlaubt"),
        new("EnforceReadonlyParameters", g => g.EnforceReadonlyParameters,
            "Parameter dürfen innerhalb der Methode nicht neu zugewiesen werden.",
            "aus — Parameterzuweisungen erlaubt"),
        new("EnforceReadonlyFields", g => g.EnforceReadonlyFields,
            "Private Felder, die nur im Konstruktor zugewiesen werden, müssen `readonly` sein.",
            "aus — keine readonly-Pflicht für Felder"),
        new("EnforceNoMagicValues", g => g.EnforceNoMagicValues,
            "Keine Magic Numbers/Strings (erlaubt: 0, 1, -1, \"\").",
            "aus — Literale Werte in Ausdrücken sind erlaubt"),
        new("EnforceExplicitStateImmutability", g => g.EnforceExplicitStateImmutability,
            "Erzwingt, dass Klassenfelder und -eigenschaften readonly/init-only sein müssen.",
            "aus — Blazor/Handler-State; Zielbild: `platform-ai-strict`"),
        new("EnforceStrictBoundaryForBusinessLogic", g => g.EnforceStrictBoundaryForBusinessLogic,
            "Business-Logic-Klassen (z. B. Calculator, Rule) dürfen keine I/O-Operationen durchführen.",
            "aus — I/O in Business-Logic erlaubt"),
        new("PreventContextDependentOverloads", g => g.PreventContextDependentOverloads,
            "Keine Methodenüberladungen mit identischer Parameteranzahl für primitive Typen.",
            "aus — Überladungen erlaubt"),
        new("RequireExplicitTruncationHandling", g => g.RequireExplicitTruncationHandling,
            "Zeichenketten-Abschneiden (Truncation) nach I/O- und Stream-Leseoperationen muss explizit behandelt werden.",
            "aus — keine explizite Truncation-Pflicht"),
        new("EnforceNamespaceDirectoryMapping", g => g.EnforceNamespaceDirectoryMapping,
            "Der Namespace der Datei muss dem Pfad im Dateisystem entsprechen.",
            "aus — Namespaces können frei gewählt werden"),
        new("DetectAndBanPhantomDependencies", g => g.DetectAndBanPhantomDependencies,
            "Keine unauflösbaren `using`-Namespaces und kein `Type.GetType` / `Activator.CreateInstance` — nur statisch compilierbare Referenzen.",
            "aus — Phantom-Abhängigkeiten erlaubt"),
        new("AllowedEmptyReads", g => g.AllowedEmptyReads,
            "Erlaubt I/O-Leseoperationen ohne unmittelbare Guards.",
            "aus — leere Leseoperationen verboten")
    ];

    private sealed record MetricDescriptor(Func<MetricsConfig, int> GetVal, string Name, string Desc);

    private static readonly MetricDescriptor[] MetricsList =
    [
        new(m => m.MaxLineCount, "MaxLineCount", "Maximale Zeilenanzahl pro Datei."),
        new(m => m.MaxMethodLineCount, "MaxMethodLineCount", "Codezeilen pro Methode (ohne Kommentare/Klammern)."),
        new(m => m.MaxMethodParameterCount, "MaxMethodParameterCount", "Parameter pro Methode. Bei Überschreitung einen `record` (Parameter Object) nutzen."),
        new(m => m.MaxCyclomaticComplexity, "MaxCyclomaticComplexity", "Maximale zyklomatische Komplexität (McCabe) pro Methode."),
        new(m => m.MaxCognitiveComplexity, "MaxCognitiveComplexity", "Maximale kognitive Komplexität pro Methode."),
        new(m => m.MaxInheritanceDepth, "MaxInheritanceDepth", "Maximale Vererbungstiefe."),
        new(m => m.MaxMethodOverloads, "MaxMethodOverloads", "Maximale Anzahl an Methodenüberladungen pro Name in einer Klasse."),
        new(m => m.MaxConstructorDependencies, "MaxConstructorDependencies", "Maximale Anzahl an Konstruktor-Abhängigkeiten."),
        new(m => m.MaxDirectoryDepth, "MaxDirectoryDepth", "Maximale Ordnertiefe ab csproj-Ebene."),
        new(m => m.MaxAIContextFootprint, "MaxAIContextFootprint", "Maximale Anzahl transitiver Codezeilen von Klassenabhängigkeiten.")
    ];

    /// <summary>
    /// Generiert die MDC-Datei und schreibt sie nach .cursor/rules/AiNetLinter.mdc relativ zum angegebenen Pfad.
    /// </summary>
    /// <param name="targetPath">Der Pfad zum Zielverzeichnis oder der Solution.</param>
    /// <param name="config">Die geladene Linter-Konfiguration.</param>
    /// <param name="verbose">Gibt an, ob detaillierte Ausgaben protokolliert werden sollen.</param>
    /// <param name="configPath">Der Pfad zur Konfigurationsdatei.</param>
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
        var version = typeof(CursorRulesGenerator).Assembly.GetName().Version?.ToString(3) ?? "1.0.21";

        sb.AppendLine($"<!-- Auto-generated by AiNetLinter {version} | Quelle: {configPath} -->");
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
        foreach (var metric in MetricsList)
        {
            sb.AppendLine($"| `{metric.Name}` | **{metric.GetVal(config.Metrics)}** | {metric.Desc} |");
        }
        sb.AppendLine();

        sb.AppendLine("## 2. Globale Qualitäts-Regeln (Aktiviert)");
        sb.AppendLine();

        var g = config.Global;
        foreach (var rule in GlobalRules)
        {
            if (rule.IsEnabled(g))
            {
                var intent = RuleMetadataRegistry.Resolve(rule.Name, config).Intent;
                sb.AppendLine($"- **`{rule.Name}`** `[intent: {intent}]`: {rule.ActiveDesc}");
            }
        }

        if (g.ImmutabilityExemptSuffixes != null && g.ImmutabilityExemptSuffixes.Count > 0)
        {
            if (g.ImmutabilityExemptSuffixes.Count > 5)
            {
                sb.AppendLine("- **`ImmutabilityExemptSuffixes`**: Ausgenommene Suffixe (siehe `rules.json`).");
            }
            else
            {
                var suffixesStr = string.Join(", ", g.ImmutabilityExemptSuffixes.Select(s => $"`{s}`"));
                sb.AppendLine($"- **`ImmutabilityExemptSuffixes`**: Folgende Typ-Suffixe sind von der Immutability-Prüfung ausgenommen: {suffixesStr}.");
            }
        }

        if (g.ImmutabilityExemptPatterns != null && g.ImmutabilityExemptPatterns.Count > 0)
        {
            if (g.ImmutabilityExemptPatterns.Count > 5)
            {
                sb.AppendLine("- **`ImmutabilityExemptPatterns`**: Ausgenommene Wildcard-Muster (siehe `rules.json`).");
            }
            else
            {
                var patternsStr = string.Join(", ", g.ImmutabilityExemptPatterns.Select(p => $"`{p}`"));
                sb.AppendLine($"- **`ImmutabilityExemptPatterns`**: Folgende Wildcard-Muster sind ausgenommen: {patternsStr}.");
            }
        }
        sb.AppendLine();

        sb.AppendLine("## 3. Deaktiviert (bewusst — nicht in neuem Code nachahmen)");
        sb.AppendLine();
        foreach (var rule in GlobalRules)
        {
            if (!rule.IsEnabled(g))
            {
                sb.AppendLine($"- **`{rule.Name}`**: {rule.DeactiveDesc}");
            }
        }
        sb.AppendLine();

        AppendProjectOverrides(sb, config);

        return sb.ToString();
    }

    private static void AppendProjectOverrides(StringBuilder sb, LinterConfig config)
    {
        if (config.ProjectOverrides == null || config.ProjectOverrides.Count == 0)
        {
            return;
        }

        sb.AppendLine("## 4. Projekt-spezifische Abweichungen (Overrides)");
        sb.AppendLine();
        sb.AppendLine("Der Linter erlaubt in bestimmten Projekten Abweichungen von den obigen Regeln:");
        sb.AppendLine();

        foreach (var pair in config.ProjectOverrides)
        {
            sb.AppendLine($"### Bereich: `{pair.Key}`");
            sb.AppendLine();
            sb.AppendLine("| Regel | Wert | Beschreibung / Anweisung |");
            sb.AppendLine("| :--- | :--- | :--- |");
            
            var overrides = pair.Value;
            if (overrides.Global != null)
            {
                var og = overrides.Global;
                foreach (var rule in GlobalRules)
                {
                    // Wir versuchen die Eigenschaft per Reflection zu holen
                    var prop = typeof(GlobalConfigOverride).GetProperty(rule.Name);
                    if (prop != null)
                    {
                        var val = prop.GetValue(og) as bool?;
                        if (val.HasValue)
                        {
                            var valStr = val.Value ? "ein" : "aus";
                            var desc = val.Value ? rule.ActiveDesc : rule.DeactiveDesc;
                            sb.AppendLine($"| `{rule.Name}` | {valStr} | {desc} |");
                        }
                    }
                }

                if (og.AllowedExceptions != null)
                {
                    var listStr = string.Join(", ", og.AllowedExceptions);
                    sb.AppendLine($"| `AllowedExceptions` | `[{listStr}]` | Ausgenommene Exception-Typen für dieses Projekt. |");
                }
                if (og.ImmutabilityExemptSuffixes != null)
                {
                    var listStr = string.Join(", ", og.ImmutabilityExemptSuffixes);
                    sb.AppendLine($"| `ImmutabilityExemptSuffixes` | `[{listStr}]` | Ausgenommene Suffixe für dieses Projekt. |");
                }
                if (og.ImmutabilityExemptPatterns != null)
                {
                    var listStr = string.Join(", ", og.ImmutabilityExemptPatterns);
                    sb.AppendLine($"| `ImmutabilityExemptPatterns` | `[{listStr}]` | Ausgenommene Wildcard-Muster für dieses Projekt. |");
                }
            }

            if (overrides.Metrics != null)
            {
                var om = overrides.Metrics;
                foreach (var metric in MetricsList)
                {
                    var prop = typeof(MetricsConfigOverride).GetProperty(metric.Name);
                    if (prop != null)
                    {
                        var val = prop.GetValue(om) as int?;
                        if (val.HasValue)
                        {
                            sb.AppendLine($"| `{metric.Name}` | {val.Value} | Geändert auf {val.Value}. |");
                        }
                    }
                }
            }
            sb.AppendLine();
        }
    }
}
