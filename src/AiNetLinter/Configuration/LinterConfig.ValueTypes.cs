namespace AiNetLinter.Configuration;

/// <summary>
/// Datei- und Verzeichnis-Ausschlüsse für die Linter-Analyse.
/// </summary>
public sealed record FileFiltersConfig
{
    /// <summary>
    /// Glob-Muster die gegen den Dateinamen (ohne Pfad) geprüft werden.
    /// Standard-Wildcards: * und ?
    /// </summary>
    public IReadOnlyCollection<string> ExcludeFilePatterns { get; init; }
        = Array.Empty<string>();

    /// <summary>
    /// Pfad-Segmente: Dateien die eines dieser Segmente im Pfad enthalten, werden übersprungen.
    /// </summary>
    public IReadOnlyCollection<string> ExcludeDirectoryPatterns { get; init; }
        = ["obj/", "bin/"];

    /// <summary>
    /// Wenn true, werden Klassen/Records/Structs mit dem GeneratedCodeAttribute-Attribut übersprungen.
    /// </summary>
    public bool SkipGeneratedCodeAttribute { get; init; } = false;
}

/// <summary>
/// Optionale Überschreibungen für die Web-Konfiguration (pro Projekt).
/// Wird in der rules.json unter ProjectOverrides.*.Web eingebunden.
/// </summary>
public sealed record WebConfigOverride
{
    /// <summary>
    /// Aktiviert/Deaktiviert die Web-Analyse fuer dieses Projekt.
    /// </summary>
    public bool? IsEnabled { get; init; }

    /// <summary>
    /// CSS-spezifische Overrides.
    /// </summary>
    public CssConfigOverride? Css { get; init; }

    /// <summary>
    /// JS-spezifische Overrides.
    /// </summary>
    public JsConfigOverride? Js { get; init; }

    /// <summary>
    /// Razor-spezifische Overrides (Phase 3 der Extend-Web-Features-Epic).
    /// </summary>
    public RazorConfigOverride? Razor { get; init; }
}

/// <summary>
/// Optionale Überschreibungen für die CSS-Konfiguration (pro Projekt).
/// Wird in der rules.json unter ProjectOverrides.*.Web.Css eingebunden.
/// </summary>
public sealed record CssConfigOverride
{
    /// <summary>
    /// Override fuer MaxCssLineCount.
    /// </summary>
    public int? MaxCssLineCount { get; init; }

    /// <summary>
    /// Override fuer PreferScopedCss.
    /// </summary>
    public bool? PreferScopedCss { get; init; }

    /// <summary>
    /// Override fuer PreferScopedCssMinRuleCount.
    /// </summary>
    public int? PreferScopedCssMinRuleCount { get; init; }

    /// <summary>
    /// Override fuer MaxCssSelectorComplexity.
    /// </summary>
    public int? MaxCssSelectorComplexity { get; init; }

    /// <summary>
    /// Override fuer ExemptPaths (vollstaendige Ersetzung, keine Merge).
    /// </summary>
    public IReadOnlyCollection<string>? ExemptPaths { get; init; }
}

/// <summary>
/// Optionale Überschreibungen für die JavaScript-Konfiguration (pro Projekt).
/// Wird in der rules.json unter ProjectOverrides.*.Web.Js eingebunden.
/// </summary>
public sealed record JsConfigOverride
{
    /// <summary>
    /// Override fuer MaxJsLineCount.
    /// </summary>
    public int? MaxJsLineCount { get; init; }

    /// <summary>
    /// Override fuer EnforceJsModules.
    /// </summary>
    public bool? EnforceJsModules { get; init; }

    /// <summary>
    /// Override fuer ExemptPaths (vollstaendige Ersetzung, keine Merge).
    /// </summary>
    public IReadOnlyCollection<string>? ExemptPaths { get; init; }
}

/// <summary>
/// Optionale Überschreibungen für die Razor-Konfiguration (pro Projekt).
/// Wird in der rules.json unter ProjectOverrides.*.Web.Razor eingebunden.
/// </summary>
public sealed record RazorConfigOverride
{
    /// <summary>
    /// Override fuer MaxRazorLineCount.
    /// </summary>
    public int? MaxRazorLineCount { get; init; }

    /// <summary>
    /// Override fuer MaxRazorCodeBlockLines.
    /// </summary>
    public int? MaxRazorCodeBlockLines { get; init; }

    /// <summary>
    /// Override fuer MaxMarkupNestingDepth.
    /// </summary>
    public int? MaxMarkupNestingDepth { get; init; }

    /// <summary>
    /// Override fuer BanInlineEventLambdas.
    /// </summary>
    public bool? BanInlineEventLambdas { get; init; }

    /// <summary>
    /// Override fuer MaxControlFlowBlocks.
    /// </summary>
    public int? MaxControlFlowBlocks { get; init; }

    /// <summary>
    /// Override fuer MaxForeachNestingDepth.
    /// </summary>
    public int? MaxForeachNestingDepth { get; init; }

    /// <summary>
    /// Override fuer MaxComponentParameterCount.
    /// </summary>
    public int? MaxComponentParameterCount { get; init; }

    /// <summary>
    /// Override fuer BanInlineTernaryInAttributes.
    /// </summary>
    public bool? BanInlineTernaryInAttributes { get; init; }
}