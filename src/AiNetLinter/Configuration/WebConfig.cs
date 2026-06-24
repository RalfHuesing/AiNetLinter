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
    /// Wendet Projekt-Overrides an (siehe WebConfigOverride).
    /// </summary>
    public WebConfig Apply(WebConfigOverride? @override)
    {
        if (@override == null) return this;
        return this with
        {
            IsEnabled = @override.IsEnabled ?? IsEnabled,
            Css = Css.Apply(@override.Css),
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