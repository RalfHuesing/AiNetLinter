#nullable enable
namespace AiNetLinter.Configuration;

/// <summary>
/// Wendet die <see cref="UiSeparationConfigOverride"/>-Sektion auf eine
/// <see cref="UiSeparationConfig"/>-Instanz an. Bewusst als statische Helper-Klasse
/// extrahiert (nicht als Instanzmethode auf <see cref="UiSeparationConfig"/>),
/// damit der <c>UiSeparationConfig</c>-Record selbst schmaler bleibt und die
/// <c>AIContextFootprint</c>-Last pro transitivem Konsumenten (z. B. die
/// <c>*ToolRegistrations</c>-Klassen im MCP-Pfad) sinkt.
/// </summary>
internal static class UiSeparationConfigApplier
{
    public static UiSeparationConfig Apply(UiSeparationConfig config, UiSeparationConfigOverride? @override)
    {
        if (@override == null) return config;
        return config with
        {
            BlazorRequireCodeBehind = @override.BlazorRequireCodeBehind ?? config.BlazorRequireCodeBehind,
            BlazorRequireCssIsolation = @override.BlazorRequireCssIsolation ?? config.BlazorRequireCssIsolation,
            WpfRequireMinimalCodeBehind = @override.WpfRequireMinimalCodeBehind ?? config.WpfRequireMinimalCodeBehind,
            WpfCodeBehindBaseTypes = @override.WpfCodeBehindBaseTypes ?? config.WpfCodeBehindBaseTypes,
            BlazorExcludeFileNames = @override.BlazorExcludeFileNames ?? config.BlazorExcludeFileNames,
            WpfExcludeClassNames = @override.WpfExcludeClassNames ?? config.WpfExcludeClassNames,
            BlazorCssIsolationOnlyWhenStylesNeeded = @override.BlazorCssIsolationOnlyWhenStylesNeeded ?? config.BlazorCssIsolationOnlyWhenStylesNeeded,
        };
    }
}
