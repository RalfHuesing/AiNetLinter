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
        BuildRazorMaxRazorLineCount(),
        BuildRazorMaxRazorCodeBlockLines(),
        BuildRazorMaxMarkupNestingDepth(),
        BuildRazorBanInlineEventLambdas(),
        BuildRazorMaxControlFlowBlocks(),
        BuildRazorMaxForeachNestingDepth(),
        BuildRazorMaxComponentParameterCount(),
        BuildRazorBanInlineTernaryInAttributes(),
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

    private static RuleMetadata BuildRazorMaxRazorLineCount() => new(
        RuleId: LinterRuleIds.RAZOR_MaxRazorLineCount,
        DisplayName: "Razor Dateilaenge",
        GetShortDescription: c => $"Razor-Datei ueberschreitet das Zeilenlimit (max. {c.Web.Razor.MaxRazorLineCount}).",
        Warum: "Lange Razor-Dateien uebersteigen das lesbare Kontextfenster. Agenten verlieren beim Erzeugen von Diffs die Uebersicht ueber die Markup-Struktur ('Lost in the Middle').",
        Alternativen:
        [
            "**Komponente aufteilen**: Eigenstaendige UI-Bereiche in separate Blazor-Komponenten extrahieren.",
            "**Partial Views / ChildContent**: Wiederkehrende Markup-Bloecke in wiederverwendbare Teilkomponenten verschieben.",
            "**Suppression** (bei Legacy-Komponenten): `@* ainetlinter-disable RAZOR_MaxRazorLineCount *@`."
        ],
        SicherheitsHinweis: null,
        Intent: "agent-context",
        Severity: "error",
        CursorHint: "Razor-Komponente aufteilen oder Teilbereiche extrahieren.",
        HasAutoFix: false,
        IsEnabled: c => c.Web.IsEnabled && c.Web.Razor.MaxRazorLineCount > 0,
        IsMetric: true,
        IncludeInCursorRules: true,
        GetMetricLimit: c => c.Web.Razor.MaxRazorLineCount,
        ConfigKeyHint: "rules.json → Web.Razor.MaxRazorLineCount (Web.IsEnabled muss true sein)"
    );

    private static RuleMetadata BuildRazorMaxRazorCodeBlockLines() => new(
        RuleId: LinterRuleIds.RAZOR_MaxRazorCodeBlockLines,
        DisplayName: "Razor @code-Block-Groesse",
        GetShortDescription: c => $"@code-Block ueberschreitet das Zeilenlimit (max. {c.Web.Razor.MaxRazorCodeBlockLines}).",
        Warum: "Guard-Regel fuer Faelle, in denen trotz BlazorRequireCodeBehind ein @code-Block existiert. C#-Logik gehoert in die Code-Behind-Datei (.razor.cs).",
        Alternativen:
        [
            "**Logik in Code-Behind verschieben**: 'partial class' in '.razor.cs' verwenden (Empfehlung).",
            "**@functions extrahieren**: Bei sehr grossen Helper-Klassen separate Service-Klasse anlegen.",
            "**Suppression** (bei Legacy): `@* ainetlinter-disable RAZOR_MaxRazorCodeBlockLines *@`."
        ],
        SicherheitsHinweis: null,
        Intent: "agent-context",
        Severity: "warning",
        CursorHint: "@code-Block in Code-Behind-Datei (.razor.cs) verschieben.",
        HasAutoFix: false,
        IsEnabled: c => c.Web.IsEnabled && c.Web.Razor.MaxRazorCodeBlockLines > 0,
        IsMetric: true,
        IncludeInCursorRules: true,
        GetMetricLimit: c => c.Web.Razor.MaxRazorCodeBlockLines,
        ConfigKeyHint: "rules.json → Web.Razor.MaxRazorCodeBlockLines"
    );

    private static RuleMetadata BuildRazorMaxMarkupNestingDepth() => new(
        RuleId: LinterRuleIds.RAZOR_MaxMarkupNestingDepth,
        DisplayName: "Razor Markup-Verschachtelung",
        GetShortDescription: c => $"HTML-Verschachtelungstiefe zu hoch (max. {c.Web.Razor.MaxMarkupNestingDepth} Ebenen).",
        Warum: "Tiefe HTML-Hierarchien fuehren bei Agenten zu Tag-Mismatch-Halluzinationen — falsch geschlossene oder verschobene Elemente. KIs koennen die Tag-Hierarchie ueber mehrere Ebenen nicht zuverlaessig rekonstruieren.",
        Alternativen:
        [
            "**Innere Bereiche extrahieren**: Komplexe Sub-Bereiche in eigene Blazor-Komponenten mit klar definierter API verschieben.",
            "**Flachere Struktur anstreben**: Wiederkehrende Container-Klassen als CSS-Klasse statt als verschachteltes DIV.",
            "**Suppression** (bei semantisch notwendiger Verschachtelung): `@* ainetlinter-disable RAZOR_MaxMarkupNestingDepth *@`."
        ],
        SicherheitsHinweis: null,
        Intent: "agent-context",
        Severity: "warning",
        CursorHint: "Innere Bereiche in eigene Komponenten extrahieren.",
        HasAutoFix: false,
        IsEnabled: c => c.Web.IsEnabled && c.Web.Razor.MaxMarkupNestingDepth > 0,
        IsMetric: true,
        IncludeInCursorRules: true,
        GetMetricLimit: c => c.Web.Razor.MaxMarkupNestingDepth,
        ConfigKeyHint: "rules.json → Web.Razor.MaxMarkupNestingDepth"
    );

    private static RuleMetadata BuildRazorBanInlineEventLambdas() => new(
        RuleId: LinterRuleIds.RAZOR_BanInlineEventLambdas,
        DisplayName: "Inline-Event-Lambdas verbieten",
        GetShortDescription: _ => "Mehrzeilige Inline-Event-Lambdas im Markup gefunden.",
        Warum: "C#-Ausdruecke innerhalb von HTML-Attributen sind ein Mixed-Context, in dem Agenten regelmaessig Syntaxfehler produzieren (fehlende Klammern, falsche Anfuehrungszeichen).",
        Alternativen:
        [
            "**Methoden-Referenz verwenden**: '@onclick=\"HandleClick\"' statt '@onclick=\"() => { ... }\"'.",
            "**Triviales Einzeiler-Lambda** (erlaubt): '@onclick=\"() => Count++\"' ohne Semikolon und ohne Block.",
            "**Logik in Code-Behind extrahieren**: Methode in '.razor.cs' anlegen und referenzieren."
        ],
        SicherheitsHinweis: null,
        Intent: "agent-context",
        Severity: "warning",
        CursorHint: "Inline-Lambda durch Methoden-Referenz oder Code-Behind-Methode ersetzen.",
        HasAutoFix: false,
        IsEnabled: c => c.Web.IsEnabled && c.Web.Razor.BanInlineEventLambdas,
        IsMetric: false,
        IncludeInCursorRules: true,
        ConfigKeyHint: "rules.json → Web.Razor.BanInlineEventLambdas"
    );

    private static RuleMetadata BuildRazorMaxControlFlowBlocks() => new(
        RuleId: LinterRuleIds.RAZOR_MaxControlFlowBlocks,
        DisplayName: "Razor Control-Flow-Komplexitaet",
        GetShortDescription: c => $"Zu viele Control-Flow-Bloecke (max. {c.Web.Razor.MaxControlFlowBlocks}).",
        Warum: "Viele @if/@foreach/@switch-Bloecke signalisieren zu viel konditionale Render-Logik. Agenten koennen bei komplexem konditionalen Rendering nicht vorhersagen, welche HTML-Elemente tatsaechlich ausgegeben werden.",
        Alternativen:
        [
            "**Teilbereiche extrahieren**: Konditionale Bereiche in eigene Komponenten mit klar definierten Parametern auslagern.",
            "**Render-Fragments verwenden**: '@ChildContent' / 'RenderFragment' fuer flexible Wiederverwendung.",
            "**Suppression** (bei Legacy-Komponenten): `@* ainetlinter-disable RAZOR_MaxControlFlowBlocks *@`."
        ],
        SicherheitsHinweis: null,
        Intent: "agent-context",
        Severity: "warning",
        CursorHint: "Teilbereiche in eigenstaendige Komponenten extrahieren.",
        HasAutoFix: false,
        IsEnabled: c => c.Web.IsEnabled && c.Web.Razor.MaxControlFlowBlocks > 0,
        IsMetric: true,
        IncludeInCursorRules: true,
        GetMetricLimit: c => c.Web.Razor.MaxControlFlowBlocks,
        ConfigKeyHint: "rules.json → Web.Razor.MaxControlFlowBlocks"
    );

    private static RuleMetadata BuildRazorMaxForeachNestingDepth() => new(
        RuleId: LinterRuleIds.RAZOR_MaxForeachNestingDepth,
        DisplayName: "Razor @foreach-Verschachtelung",
        GetShortDescription: c => $"@foreach-Verschachtelungstiefe zu hoch (max. {c.Web.Razor.MaxForeachNestingDepth} Ebenen).",
        Warum: "Verschachtelte @foreach-Schleifen multiplizieren die Agent-Komplexitaet bei der Render-Vorhersage. Jede Ebene fuegt eine Collection-Iteration hinzu, die der Agent konsistent durchdenken muss.",
        Alternativen:
        [
            "**Innere Schleife in Kind-Komponente extrahieren**: '<InnerLoop Items=\"@innerItems\" />' statt direkt verschachteln.",
            "**Daten vorab aggregieren**: 'GroupBy' / 'SelectMany' in der Code-Behind-Datei und das Ergebnis in einer flachen Schleife rendern.",
            "**Suppression** (bei notwendiger Hierarchie): `@* ainetlinter-disable RAZOR_MaxForeachNestingDepth *@`."
        ],
        SicherheitsHinweis: null,
        Intent: "agent-context",
        Severity: "warning",
        CursorHint: "Innere Schleife in eigene Komponente extrahieren.",
        HasAutoFix: false,
        IsEnabled: c => c.Web.IsEnabled && c.Web.Razor.MaxForeachNestingDepth > 0,
        IsMetric: true,
        IncludeInCursorRules: true,
        GetMetricLimit: c => c.Web.Razor.MaxForeachNestingDepth,
        ConfigKeyHint: "rules.json → Web.Razor.MaxForeachNestingDepth"
    );

    private static RuleMetadata BuildRazorMaxComponentParameterCount() => new(
        RuleId: LinterRuleIds.RAZOR_MaxComponentParameterCount,
        DisplayName: "Razor Komponenten-Parameter",
        GetShortDescription: c => $"Komponentenaufruf hat zu viele Parameter (max. {c.Web.Razor.MaxComponentParameterCount}).",
        Warum: "Ein Komponenten-Aufruf mit vielen Parametern ist das Markup-Aequivalent zu 'MaxMethodParameterCount'. Agenten verlieren die Zuordnung von Werten zu Parametern und generieren haeufig falsch geordnete oder vergessene Bindings.",
        Alternativen:
        [
            "**Parameter-Objekt einfuehren**: Verwandte Parameter in einem 'record' buendeln ('<MyComp Config=\"@cfg\" />').",
            "**Oeffentliche API reduzieren**: Nicht zwingend benoetigte Properties aus der Komponente entfernen.",
            "**Suppression** (bei Legacy-Komponenten): `@* ainetlinter-disable RAZOR_MaxComponentParameterCount *@`."
        ],
        SicherheitsHinweis: null,
        Intent: "agent-context",
        Severity: "warning",
        CursorHint: "Verwandte Parameter in Parameter-Objekt zusammenfassen.",
        HasAutoFix: false,
        IsEnabled: c => c.Web.IsEnabled && c.Web.Razor.MaxComponentParameterCount > 0,
        IsMetric: true,
        IncludeInCursorRules: true,
        GetMetricLimit: c => c.Web.Razor.MaxComponentParameterCount,
        ConfigKeyHint: "rules.json → Web.Razor.MaxComponentParameterCount"
    );

    private static RuleMetadata BuildRazorBanInlineTernaryInAttributes() => new(
        RuleId: LinterRuleIds.RAZOR_BanInlineTernaryInAttributes,
        DisplayName: "Ternary in Attributen verbieten",
        GetShortDescription: _ => "Ternary-Ausdruck im HTML-Attributwert gefunden.",
        Warum: "Ternary-Ausdruecke innerhalb von HTML-Attributwerten erzeugen Mixed-Context zwischen HTML-String-Kontext und C#-Expressions-Kontext. Agenten muessen beide Kontexte gleichzeitig aufloesen und produzieren typische Fehler (fehlende Anfuehrungszeichen, vertauschte Klammern).",
        Alternativen:
        [
            "**Attributwert in Property berechnen**: 'private string CssClass => isActive ? \"base active\" : \"base\";' und dann 'class=\"@CssClass\"'.",
            "**Hilfsmethode verwenden**: 'GetCssClass(bool isActive)' in der Code-Behind-Datei.",
            "**Suppression** (bei trivialen Bedingungen): `@* ainetlinter-disable RAZOR_BanInlineTernaryInAttributes *@`."
        ],
        SicherheitsHinweis: null,
        Intent: "agent-context",
        Severity: "warning",
        CursorHint: "Ternary-Ausdruck in Property oder Methode der Code-Behind-Datei extrahieren.",
        HasAutoFix: false,
        IsEnabled: c => c.Web.IsEnabled && c.Web.Razor.BanInlineTernaryInAttributes,
        IsMetric: false,
        IncludeInCursorRules: true,
        ConfigKeyHint: "rules.json → Web.Razor.BanInlineTernaryInAttributes"
    );
}