namespace AiNetLinter.Configuration;

public sealed record FileFiltersConfig
{
    public IReadOnlyCollection<string> ExcludeFilePatterns { get; init; }
        = Array.Empty<string>();

    public IReadOnlyCollection<string> ExcludeDirectoryPatterns { get; init; }
        = ["obj/", "bin/"];

    public bool SkipGeneratedCodeAttribute { get; init; } = false;
}

public sealed record WebConfigOverride
{
    public bool? IsEnabled { get; init; }

    public CssConfigOverride? Css { get; init; }

    public JsConfigOverride? Js { get; init; }

    public RazorConfigOverride? Razor { get; init; }
}

public sealed record CssConfigOverride
{
    public int? MaxCssLineCount { get; init; }

    public bool? PreferScopedCss { get; init; }

    public int? PreferScopedCssMinRuleCount { get; init; }

    public int? MaxCssSelectorComplexity { get; init; }

    public IReadOnlyCollection<string>? ExemptPaths { get; init; }
}

public sealed record JsConfigOverride
{
    public int? MaxJsLineCount { get; init; }

    public bool? EnforceJsModules { get; init; }

    public IReadOnlyCollection<string>? ExemptPaths { get; init; }
}

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