#nullable enable

using System;

namespace AiNetLinter.Configuration;

/// <summary>
/// Web-Konfiguration fuer CSS-, JS- und Razor-Linting (Phase 1: CSS, Phase 2: JS, Phase 3: Razor).
/// Wird parallel zu Global/Metrics/TestSentinel/UiSeparation in der rules.json unter "Web" eingebunden.
/// </summary>
public sealed record WebConfig
{
    /// <summary>
    /// Aktiviert die Web-Analyse komplett (CSS + JS + Razor).
    /// Wenn false werden keine Web-Dateien analysiert und keine Violations gemeldet.
    /// </summary>
    public bool IsEnabled { get; init; } = false;

    /// <summary>
    /// Konfigurations-Subbereich fuer CSS-Dateien.
    /// </summary>
    public CssConfig Css { get; init; } = new();

    /// <summary>
    /// Konfigurations-Subbereich fuer JavaScript-Dateien (Phase 2 der Extend-Web-Features-Epic).
    /// </summary>
    public JsConfig Js { get; init; } = new();

    /// <summary>
    /// Konfigurations-Subbereich fuer Razor/Blazor-Komponenten (Phase 3 der Extend-Web-Features-Epic).
    /// </summary>
    public RazorConfig Razor { get; init; } = new();

    /// <summary>
    /// Wendet Projekt-Overrides an (siehe WebConfigOverride).
    /// </summary>
    public WebConfig Apply(WebConfigOverride? @override)
    {
        if (@override == null) return this;
        return this with
        {
            IsEnabled = @override.IsEnabled ?? IsEnabled,
            Css = Css.Apply(@override.Css),
            Js = Js.Apply(@override.Js),
            Razor = Razor.Apply(@override.Razor),
        };
    }
}

// Test-Sentinel: CssConfig ist ueber CssAnalyzerTests.cs mit // @covers abgedeckt
// (siehe Test-Datei; StaticTestSentinel akzeptiert @covers in Test-Dateien).
/// <summary>
/// CSS-spezifische Konfiguration. Wird in der rules.json unter Web.Css gepflegt.
/// </summary>
public sealed record CssConfig
{
    /// <summary>
    /// Maximale Anzahl Zeilen pro CSS-Datei (Standard: 300). Verhindert "Lost in the Middle"-Effekte
    /// bei grossen monolithischen Stylesheets in AI-Edit-Loops.
    /// </summary>
    public int MaxCssLineCount { get; init; } = 300;

    /// <summary>
    /// Wenn true (Standard), werden globale CSS-Dateien mit vielen Regeln zugunsten von
    /// Scoped CSS (.razor.css) abgemaahnt — verhindert "Butterfly-Effekte" bei AI-Edits.
    /// </summary>
    public bool PreferScopedCss { get; init; } = true;

    /// <summary>
    /// Schwellenwert: Ab dieser Anzahl Stil-Regeln in einer globalen CSS-Datei wird
    /// CSS_PreferScopedCss ausgeloest. CSS-Dateien mit weniger Regeln (Resets, Custom Properties)
    /// sind legitim global.
    /// </summary>
    public int PreferScopedCssMinRuleCount { get; init; } = 5;

    /// <summary>
    /// Maximale Tiefe eines CSS-Selektors (Anzahl Selektor-Segmente). Verhindert ueber-Engineered
    /// CSS-Selektoren die fuer Modelle schwer zuzuordnen sind.
    /// </summary>
    public int MaxCssSelectorComplexity { get; init; } = 3;

    /// <summary>
    /// Glob-Muster fuer Pfade, die von der CSS-Analyse ausgeschlossen werden
    /// (z. B. Bootstrap, MudBlazor, *.min.css).
    /// </summary>
    public IReadOnlyCollection<string> ExemptPaths { get; init; } = new[]
    {
        "**/wwwroot/lib/**",
        "**/node_modules/**",
        "**/*.min.css",
    };

    /// <summary>
    /// Wendet Css-spezifische Projekt-Overrides an.
    /// </summary>
    public CssConfig Apply(CssConfigOverride? @override)
    {
        if (@override == null) return this;
        return this with
        {
            MaxCssLineCount = @override.MaxCssLineCount ?? MaxCssLineCount,
            PreferScopedCss = @override.PreferScopedCss ?? PreferScopedCss,
            PreferScopedCssMinRuleCount = @override.PreferScopedCssMinRuleCount ?? PreferScopedCssMinRuleCount,
            MaxCssSelectorComplexity = @override.MaxCssSelectorComplexity ?? MaxCssSelectorComplexity,
            ExemptPaths = @override.ExemptPaths ?? ExemptPaths,
        };
    }
}

// Test-Sentinel: JsConfig ist ueber JsAnalyzerTests.cs mit // @covers abgedeckt
// (siehe Test-Datei; StaticTestSentinel akzeptiert @covers in Test-Dateien).
/// <summary>
/// JavaScript-spezifische Konfiguration (Phase 2 der Extend-Web-Features-Epic).
/// Wird in der rules.json unter Web.Js gepflegt.
/// </summary>
public sealed record JsConfig
{
    /// <summary>
    /// Maximale Anzahl Zeilen pro JavaScript-Datei (Standard: 150). Verhindert "Lost in the Middle"-Effekte
    /// bei grossen monolithischen JS-Interop-Dateien in AI-Edit-Loops. Komplexe Logik gehoert in C#.
    /// </summary>
    public int MaxJsLineCount { get; init; } = 150;

    /// <summary>
    /// Wenn true (Standard), werden JS-Dateien ohne ES6-`export` und mit `window.*`-Zuweisungen
    /// gemeldet. Blazor Dynamic Import erwartet Module; globale Script-Dateien sind nicht
    /// robust isoliert importierbar.
    /// </summary>
    public bool EnforceJsModules { get; init; } = true;

    /// <summary>
    /// Glob-Muster fuer Pfade, die von der JS-Analyse ausgeschlossen werden
    /// (z. B. jQuery, Bootstrap-Bundle, *.min.js).
    /// </summary>
    public IReadOnlyCollection<string> ExemptPaths { get; init; } = new[]
    {
        "**/wwwroot/lib/**",
        "**/node_modules/**",
        "**/*.min.js",
    };

    /// <summary>
    /// Wendet JS-spezifische Projekt-Overrides an.
    /// </summary>
    public JsConfig Apply(JsConfigOverride? @override)
    {
        if (@override == null) return this;
        return this with
        {
            MaxJsLineCount = @override.MaxJsLineCount ?? MaxJsLineCount,
            EnforceJsModules = @override.EnforceJsModules ?? EnforceJsModules,
            ExemptPaths = @override.ExemptPaths ?? ExemptPaths,
        };
    }
}

// Test-Sentinel: RazorConfig ist ueber RazorAnalyzerTests.cs mit // @covers abgedeckt
// (siehe Test-Datei; StaticTestSentinel akzeptiert @covers in Test-Dateien).
/// <summary>
/// Razor/Blazor-spezifische Konfiguration (Phase 3 der Extend-Web-Features-Epic).
/// Wird in der rules.json unter Web.Razor gepflegt.
/// Implementiert die Markup-Qualitaets-Regeln aus Research/Extend-Web-Features/03_Razor_Linting.md.
/// </summary>
public sealed record RazorConfig
{
    /// <summary>
    /// Maximale Anzahl Zeilen pro Razor-Datei (Standard: 300). Verhindert "Lost in the Middle"-Effekte
    /// bei grossen monolithischen Blazor-Komponenten in AI-Edit-Loops.
    /// </summary>
    public int MaxRazorLineCount { get; init; } = 300;

    /// <summary>
    /// Maximale Zeilenanzahl eines @code-Blocks (Standard: 20). Guard-Regel fuer den Fall,
    /// dass jemand trotz BlazorRequireCodeBehind einen @code-Block anlegt (z. B. nach Suppression).
    /// </summary>
    public int MaxRazorCodeBlockLines { get; init; } = 20;

    /// <summary>
    /// Maximale Verschachtelungstiefe des HTML-Markups (Standard: 6 Ebenen). Tiefe Strukturen
    /// fuehren bei KI-Agenten zu Tag-Mismatch-Halluzinationen.
    /// </summary>
    public int MaxMarkupNestingDepth { get; init; } = 6;

    /// <summary>
    /// Wenn true (Standard), werden mehrzeilige Inline-Event-Lambdas
    /// (`@onclick="() => { ... ; ... ; }"`) gemeldet. Mixed-Context ist eine haeufige
    /// KI-Fehlerquelle.
    /// </summary>
    public bool BanInlineEventLambdas { get; init; } = true;

    /// <summary>
    /// Maximale Anzahl Control-Flow-Bloecke (@if, @else if, @foreach, @for, @while, @switch)
    /// pro Razor-Datei (Standard: 8). Viele Bloecke signalisieren zu viel konditionale Render-Logik.
    /// </summary>
    public int MaxControlFlowBlocks { get; init; } = 8;

    /// <summary>
    /// Maximale Verschachtelungstiefe von @foreach-Schleifen im Markup (Standard: 2).
    /// Jede Ebene multipliziert die KI-Komplexitaet bei der Render-Vorhersage.
    /// </summary>
    public int MaxForeachNestingDepth { get; init; } = 2;

    /// <summary>
    /// Maximale Anzahl Parameter an einem Komponenten-Aufruf (Standard: 10).
    /// Markup-Aequivalent zu MaxMethodParameterCount; verhindert falsch geordnete Bindings.
    /// </summary>
    public int MaxComponentParameterCount { get; init; } = 10;

    /// <summary>
    /// Wenn true (Standard), werden Ternary-Ausdruecke in HTML-Attributwerten
    /// (`class="base @(flag ? 'a' : 'b')"`) gemeldet. Mixed-Context zwischen HTML und C#.
    /// </summary>
    public bool BanInlineTernaryInAttributes { get; init; } = true;

    /// <summary>
    /// Wendet Razor-spezifische Projekt-Overrides an.
    /// </summary>
    public RazorConfig Apply(RazorConfigOverride? @override)
    {
        if (@override == null) return this;
        return this with
        {
            MaxRazorLineCount = @override.MaxRazorLineCount ?? MaxRazorLineCount,
            MaxRazorCodeBlockLines = @override.MaxRazorCodeBlockLines ?? MaxRazorCodeBlockLines,
            MaxMarkupNestingDepth = @override.MaxMarkupNestingDepth ?? MaxMarkupNestingDepth,
            BanInlineEventLambdas = @override.BanInlineEventLambdas ?? BanInlineEventLambdas,
            MaxControlFlowBlocks = @override.MaxControlFlowBlocks ?? MaxControlFlowBlocks,
            MaxForeachNestingDepth = @override.MaxForeachNestingDepth ?? MaxForeachNestingDepth,
            MaxComponentParameterCount = @override.MaxComponentParameterCount ?? MaxComponentParameterCount,
            BanInlineTernaryInAttributes = @override.BanInlineTernaryInAttributes ?? BanInlineTernaryInAttributes,
        };
    }
}