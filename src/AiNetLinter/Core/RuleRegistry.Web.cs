#nullable enable

using AiNetLinter.Configuration;

namespace AiNetLinter.Core;

/// <summary>
/// Web-Asset-Regeln (Phase 1: CSS, Phase 2: JS, Phase 3: Razor). Opt-in via Web.IsEnabled.
/// Ausgelagert in eigene Datei, damit RuleRegistry.General.cs unter MaxLineCount bleibt.
/// </summary>
internal static partial class RuleRegistry
{
    /// <summary>
    /// Web-Asset-Regeln (Phase 1: CSS, Phase 2: JS, Phase 3: Razor). Opt-in via Web.IsEnabled.
    /// </summary>
    private static RuleMetadata[] BuildWebAssetRules() =>
    [
        BuildCssMaxCssLineCount(),
        BuildCssPreferScopedCss(),
        BuildCssMaxCssSelectorComplexity(),
        BuildCssParseError(),
        BuildJsMaxJsLineCount(),
        BuildJsEnforceJsModules(),
        BuildJsSyntaxError(),
    ];

    private static RuleMetadata BuildCssMaxCssLineCount() => new(
        RuleId: LinterRuleIds.CSS_MaxCssLineCount,
        DisplayName: "CSS Dateilaenge",
        GetShortDescription: c => $"CSS-Datei ueberschreitet das Zeilenlimit (max. {c.Web.Css.MaxCssLineCount}).",
        Warum: "Lange CSS-Dateien uebersteigen das lesbare Kontextfenster. Agenten verlieren bei Diffs die Uebersicht und erzeugen Style-Konflikte ('Lost in the Middle').",
        Alternativen:
        [
            "**Aufteilen nach Feature**: CSS-Datei in mehrere themenspezifische Dateien zerlegen (z. B. layout.css, typography.css).",
            "**Scoped CSS verwenden**: Komponenten-Styles in gleichnamige '.razor.css'-Datei extrahieren — Blazor scopped automatisch.",
            "**Custom Properties konsolidieren**: Design-Tokens als CSS-Variablen in einer kleinen 'tokens.css'."
        ],
        SicherheitsHinweis: null,
        Intent: "agent-context",
        Severity: "error",
        CursorHint: "CSS-Datei splitten oder in Scoped CSS ueberfuehren.",
        HasAutoFix: false,
        IsEnabled: c => c.Web.IsEnabled && c.Web.Css.MaxCssLineCount > 0,
        IsMetric: true,
        IncludeInCursorRules: true,
        GetMetricLimit: c => c.Web.Css.MaxCssLineCount,
        ConfigKeyHint: "rules.json → Web.Css.MaxCssLineCount (Web.IsEnabled muss true sein)"
    );

    private static RuleMetadata BuildCssPreferScopedCss() => new(
        RuleId: LinterRuleIds.CSS_PreferScopedCss,
        DisplayName: "Scoped CSS bevorzugen",
        GetShortDescription: c => $"Globale CSS-Datei mit vielen Regeln — Scoped CSS (.razor.css) bevorzugen (Schwellenwert: {c.Web.Css.PreferScopedCssMinRuleCount}).",
        Warum: "Globale CSS-Regeln sind fuer Agenten nicht lokalisierbar — eine Aenderung an '.card' wirkt sich auf alle Komponenten aus, ohne dass der Agent die Konsequenzen ueberblickt (Butterfly-Effekt).",
        Alternativen:
        [
            "**Scoped CSS**: Globale Regeln in gleichnamige '.razor.css'-Datei der Komponente extrahieren.",
            "**Globale Datei klein halten**: Nur Resets, Custom Properties und Font-Definitionen in der globalen Datei belassen.",
            "**Suppression** (bei wenigen, klar globalen Regeln): `/* ainetlinter-disable CSS_PreferScopedCss */`."
        ],
        SicherheitsHinweis: null,
        Intent: "agent-context",
        Severity: "warning",
        CursorHint: "Globale CSS-Dateien klein halten; Komponenten-Styles in .razor.css.",
        HasAutoFix: false,
        IsEnabled: c => c.Web.IsEnabled && c.Web.Css.PreferScopedCss,
        IsMetric: false,
        IncludeInCursorRules: true,
        ConfigKeyHint: "rules.json → Web.Css.PreferScopedCss | Web.Css.PreferScopedCssMinRuleCount"
    );

    private static RuleMetadata BuildCssMaxCssSelectorComplexity() => new(
        RuleId: LinterRuleIds.CSS_MaxCssSelectorComplexity,
        DisplayName: "CSS Selektor-Komplexitaet",
        GetShortDescription: c => $"CSS-Selektor zu komplex (max. {c.Web.Css.MaxCssSelectorComplexity} Segmente).",
        Warum: "Verschachtelte CSS-Selektoren sind fuer Modelle schwer zuzuordnen — der Agent matcht die Hierarchie falsch und erzeugt inkonsistente Styles.",
        Alternativen:
        [
            "**Scoped CSS verwenden**: Wurzel-Selektor '.my-component' reicht; Verschachtelung entfaellt.",
            "**Spezifitaet reduzieren**: IDs, !important und tief verschachtelte Klassen vermeiden.",
            "**Selektor aufteilen**: Statt '.card > .header .title' zwei separate Regeln fuer '.card-header' und '.card-title'."
        ],
        SicherheitsHinweis: null,
        Intent: "agent-context",
        Severity: "warning",
        CursorHint: "Maximal 3 Selektor-Segmente; Scoped CSS nutzen.",
        HasAutoFix: false,
        IsEnabled: c => c.Web.IsEnabled && c.Web.Css.MaxCssSelectorComplexity > 0,
        IsMetric: true,
        IncludeInCursorRules: true,
        GetMetricLimit: c => c.Web.Css.MaxCssSelectorComplexity,
        ConfigKeyHint: "rules.json → Web.Css.MaxCssSelectorComplexity (Web.IsEnabled muss true sein)"
    );

    private static RuleMetadata BuildCssParseError() => new(
        RuleId: LinterRuleIds.CSS_ParseError,
        DisplayName: "CSS Syntax-Fehler",
        GetShortDescription: c => "CSS-Datei konnte nicht geparst werden (Syntax-Fehler).",
        Warum: "Ein CSS-Parse-Fehler verhindert die weitere Analyse — Agenten uebersehen fehlende Klammern und erzeugen kaputte Styles.",
        Alternativen:
        [
            "**Syntax korrigieren**: Fehlende geschweifte Klammern, ungueltige Selektoren oder Komma-Fehler beheben.",
            "**ExemptPaths pruefen**: Falls Bibliotheks-CSS betroffen ist, ggf. in `Web.Css.ExemptPaths` aufnehmen."
        ],
        SicherheitsHinweis: null,
        Intent: "general",
        Severity: "error",
        CursorHint: "Syntax-Fehler im CSS beheben.",
        HasAutoFix: false,
        IsEnabled: c => c.Web.IsEnabled,
        IsMetric: false,
        IncludeInCursorRules: false
    );

    private static RuleMetadata BuildJsMaxJsLineCount() => new(
        RuleId: LinterRuleIds.JS_MaxJsLineCount,
        DisplayName: "JS Dateilaenge",
        GetShortDescription: c => $"JavaScript-Datei ueberschreitet das Zeilenlimit (max. {c.Web.Js.MaxJsLineCount}).",
        Warum: "Lange JavaScript-Dateien uebersteigen das lesbare Kontextfenster. Blazor-Interop-Dateien sollen minimal bleiben — komplexe Logik gehoert in C#.",
        Alternativen:
        [
            "**Logik nach C# migrieren**: Komplexe Berechnungen in C#-Methoden verschieben (Handler im IJSObjectReference).",
            "**Datei aufteilen**: Mehrere kleine ES6-Module mit klarer Verantwortung pro Datei.",
            "**Custom Values uebergeben**: Daten via Parameter an die exportierte Funktion uebergeben statt im Closure zu kapseln."
        ],
        SicherheitsHinweis: null,
        Intent: "agent-context",
        Severity: "error",
        CursorHint: "JavaScript-Datei aufteilen oder Logik nach C# migrieren.",
        HasAutoFix: false,
        IsEnabled: c => c.Web.IsEnabled && c.Web.Js.MaxJsLineCount > 0,
        IsMetric: true,
        IncludeInCursorRules: true,
        GetMetricLimit: c => c.Web.Js.MaxJsLineCount,
        ConfigKeyHint: "rules.json → Web.Js.MaxJsLineCount (Web.IsEnabled muss true sein)"
    );

    private static RuleMetadata BuildJsEnforceJsModules() => new(
        RuleId: LinterRuleIds.JS_EnforceJsModules,
        DisplayName: "ES6-Modul erzwingen",
        GetShortDescription: c => "JavaScript-Datei ist kein ES6-Modul oder nutzt das globale 'window'-Objekt.",
        Warum: "Blazors Dynamic Import erwartet Module; globale Script-Dateien sind nicht isoliert importierbar. Zuweisungen an 'window' erzeugen unvorhersehbare Seiteneffekte bei KI-Edits.",
        Alternativen:
        [
            "**ES6-Export hinzufuegen**: 'export function myHelper() { ... }' oder 'export { myHelper };'.",
            "**Dynamic Import nutzen**: 'await JSRuntime.InvokeAsync<IJSObjectReference>(\"import\", \"./myModule.js\")'.",
            "**Suppression** (bei Legacy-Bridge): `// ainetlinter-disable JS_EnforceJsModules`."
        ],
        SicherheitsHinweis: null,
        Intent: "agent-context",
        Severity: "error",
        CursorHint: "ES6-Modul mit 'export' verwenden; window-Zuweisungen vermeiden.",
        HasAutoFix: false,
        IsEnabled: c => c.Web.IsEnabled && c.Web.Js.EnforceJsModules,
        IsMetric: false,
        IncludeInCursorRules: true,
        ConfigKeyHint: "rules.json → Web.Js.EnforceJsModules"
    );

    private static RuleMetadata BuildJsSyntaxError() => new(
        RuleId: LinterRuleIds.JS_SyntaxError,
        DisplayName: "JS Syntax-Fehler",
        GetShortDescription: c => "JavaScript-Datei konnte nicht geparst werden (Syntax-Fehler).",
        Warum: "Ein JavaScript-Parse-Fehler verhindert die weitere Analyse — Agenten uebersehen fehlende Klammern oder Tippfehler und erzeugen nicht-funktionierende Module.",
        Alternativen:
        [
            "**Syntax korrigieren**: Fehlende Klammern, ungueltige Statements oder Komma-Fehler beheben.",
            "**ExemptPaths pruefen**: Falls Bibliotheks-JS betroffen ist, ggf. in `Web.Js.ExemptPaths` aufnehmen."
        ],
        SicherheitsHinweis: null,
        Intent: "general",
        Severity: "error",
        CursorHint: "Syntax-Fehler im JavaScript beheben.",
        HasAutoFix: false,
        IsEnabled: c => c.Web.IsEnabled,
        IsMetric: false,
        IncludeInCursorRules: false
    );
}
