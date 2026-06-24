#nullable enable

using System;

namespace AiNetLinter.Configuration;

/// <summary>
/// Web-Konfiguration fuer CSS-, JS- und Razor-Linting (Phase 1 der Extend-Web-Features-Epic).
/// Wird parallel zu Global/Metrics/TestSentinel/UiSeparation in der rules.json unter "Web" eingebunden.
/// </summary>
public sealed record WebConfig
{
    /// <summary>
    /// Aktiviert die Web-Analyse komplett (CSS + spaeter JS + Razor).
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
