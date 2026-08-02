#nullable enable

using System;

namespace AiNetLinter.Configuration;

public sealed record WebConfig
{
    public bool IsEnabled { get; init; } = false;

    public CssConfig Css { get; init; } = new();

    public JsConfig Js { get; init; } = new();

    public RazorConfig Razor { get; init; } = new();

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

    public int PreferScopedCssMinRuleCount { get; init; } = 5;

    /// <summary>
    /// Maximale Tiefe eines CSS-Selektors (Anzahl Selektor-Segmente). Verhindert ueber-Engineered
    /// CSS-Selektoren die fuer Modelle schwer zuzuordnen sind.
    /// </summary>
    public int MaxCssSelectorComplexity { get; init; } = 3;

    public IReadOnlyCollection<string> ExemptPaths { get; init; } = new[]
    {
        "**/wwwroot/lib/**",
        "**/node_modules/**",
        "**/*.min.css",
    };

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
public sealed record JsConfig
{
    /// <summary>
    /// Maximale Anzahl Zeilen pro JavaScript-Datei (Standard: 150). Verhindert "Lost in the Middle"-Effekte
    /// bei grossen monolithischen JS-Interop-Dateien in AI-Edit-Loops. Komplexe Logik gehoert in C#.
    /// </summary>
    public int MaxJsLineCount { get; init; } = 150;

    public bool EnforceJsModules { get; init; } = true;

    public IReadOnlyCollection<string> ExemptPaths { get; init; } = new[]
    {
        "**/wwwroot/lib/**",
        "**/node_modules/**",
        "**/*.min.js",
    };

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

    public int MaxMarkupNestingDepth { get; init; } = 6;

    public bool BanInlineEventLambdas { get; init; } = true;

    public int MaxControlFlowBlocks { get; init; } = 8;

    public int MaxForeachNestingDepth { get; init; } = 2;

    /// <summary>
    /// Maximale Anzahl Parameter an einem Komponenten-Aufruf (Standard: 10).
    /// Markup-Aequivalent zu MaxMethodParameterCount; verhindert falsch geordnete Bindings.
    /// </summary>
    public int MaxComponentParameterCount { get; init; } = 10;

    public bool BanInlineTernaryInAttributes { get; init; } = true;

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