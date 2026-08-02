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

    public CssConfigOverride? Css { get; init; }

    public JsConfigOverride? Js { get; init; }

    public RazorConfigOverride? Razor { get; init; }
}

/// <summary>
/// Optionale Überschreibungen für die CSS-Konfiguration (pro Projekt).
/// Wird in der rules.json unter ProjectOverrides.*.Web.Css eingebunden.
/// </summary>
public sealed record CssConfigOverride
{
    public int? MaxCssLineCount { get; init; }

    public bool? PreferScopedCss { get; init; }

    public int? PreferScopedCssMinRuleCount { get; init; }

    public int? MaxCssSelectorComplexity { get; init; }

    public IReadOnlyCollection<string>? ExemptPaths { get; init; }
}

/// <summary>
/// Optionale Überschreibungen für die JavaScript-Konfiguration (pro Projekt).
/// Wird in der rules.json unter ProjectOverrides.*.Web.Js eingebunden.
/// </summary>
public sealed record JsConfigOverride
{
    public int? MaxJsLineCount { get; init; }

    public bool? EnforceJsModules { get; init; }

    public IReadOnlyCollection<string>? ExemptPaths { get; init; }
}

/// <summary>
/// Optionale Überschreibungen für die Razor-Konfiguration (pro Projekt).
/// Wird in der rules.json unter ProjectOverrides.*.Web.Razor eingebunden.
/// </summary>
public sealed record RazorConfigOverride
{
    public int? MaxRazorLineCount { get; init; }

    public int? MaxRazorCodeBlockLines { get; init; }

    public int? MaxMarkupNestingDepth { get; init; }

    public bool? BanInlineEventLambdas { get; init; }

    public int? MaxControlFlowBlocks { get; init; }

    public int? MaxForeachNestingDepth { get; init; }

    public int? MaxComponentParameterCount { get; init; }

    public bool? BanInlineTernaryInAttributes { get; init; }
}