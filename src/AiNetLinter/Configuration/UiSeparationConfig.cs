#nullable enable

using System;

namespace AiNetLinter.Configuration;

public sealed record UiSeparationConfig
{
    public bool BlazorRequireCodeBehind { get; init; } = true;

    public bool BlazorRequireCssIsolation { get; init; } = true;

    public bool WpfRequireMinimalCodeBehind { get; init; } = true;

    public IReadOnlyCollection<string> WpfCodeBehindBaseTypes { get; init; } = new[]
    {
        "Window", "UserControl", "Page", "NavigationWindow"
    };

    public IReadOnlyCollection<string> BlazorExcludeFileNames { get; init; } = new[]
    {
        "_Imports.razor"
    };

    public IReadOnlyCollection<string> WpfExcludeClassNames { get; init; } = Array.Empty<string>();

    public bool BlazorCssIsolationOnlyWhenStylesNeeded { get; init; } = true;

    public UiSeparationConfig Apply(UiSeparationConfigOverride? @override)
    {
        if (@override == null) return this;
        return this with
        {
            BlazorRequireCodeBehind = @override.BlazorRequireCodeBehind ?? BlazorRequireCodeBehind,
            BlazorRequireCssIsolation = @override.BlazorRequireCssIsolation ?? BlazorRequireCssIsolation,
            WpfRequireMinimalCodeBehind = @override.WpfRequireMinimalCodeBehind ?? WpfRequireMinimalCodeBehind,
            WpfCodeBehindBaseTypes = @override.WpfCodeBehindBaseTypes ?? WpfCodeBehindBaseTypes,
            BlazorExcludeFileNames = @override.BlazorExcludeFileNames ?? BlazorExcludeFileNames,
            WpfExcludeClassNames = @override.WpfExcludeClassNames ?? WpfExcludeClassNames,
            BlazorCssIsolationOnlyWhenStylesNeeded = @override.BlazorCssIsolationOnlyWhenStylesNeeded ?? BlazorCssIsolationOnlyWhenStylesNeeded,
        };
    }
}

public sealed record UiSeparationConfigOverride
{
    public bool? BlazorRequireCodeBehind { get; init; }
    public bool? BlazorRequireCssIsolation { get; init; }
    public bool? WpfRequireMinimalCodeBehind { get; init; }
    public IReadOnlyCollection<string>? WpfCodeBehindBaseTypes { get; init; }
    public IReadOnlyCollection<string>? BlazorExcludeFileNames { get; init; }
    public IReadOnlyCollection<string>? WpfExcludeClassNames { get; init; }
    public bool? BlazorCssIsolationOnlyWhenStylesNeeded { get; init; }
}
