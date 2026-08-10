#nullable enable
namespace AiNetLinter.Configuration;

/// <summary>
/// Wendet die <see cref="WebConfigOverride"/>-Sektion (inkl. Css/Js/Razor-Unterabschnitte)
/// auf eine <see cref="WebConfig"/>-Instanz an. Bewusst als statische Helper-Klasse
/// extrahiert (nicht als Instanzmethoden auf <see cref="WebConfig"/>, <see cref="CssConfig"/>,
/// <see cref="JsConfig"/>, <see cref="RazorConfig"/>), damit diese Record-Typen selbst schmaler
/// bleiben und die <c>AIContextFootprint</c>-Last pro transitivem Konsumenten (z. B. die
/// <c>*ToolRegistrations</c>-Klassen im MCP-Pfad) sinkt.
/// </summary>
internal static class WebConfigApplier
{
    public static WebConfig Apply(WebConfig config, WebConfigOverride? @override)
    {
        if (@override == null) return config;
        return config with
        {
            IsEnabled = @override.IsEnabled ?? config.IsEnabled,
            Css = ApplyCss(config.Css, @override.Css),
            Js = ApplyJs(config.Js, @override.Js),
            Razor = ApplyRazor(config.Razor, @override.Razor),
        };
    }

    public static CssConfig ApplyCss(CssConfig config, CssConfigOverride? @override)
    {
        if (@override == null) return config;
        return config with
        {
            MaxCssLineCount = @override.MaxCssLineCount ?? config.MaxCssLineCount,
            PreferScopedCss = @override.PreferScopedCss ?? config.PreferScopedCss,
            PreferScopedCssMinRuleCount = @override.PreferScopedCssMinRuleCount ?? config.PreferScopedCssMinRuleCount,
            MaxCssSelectorComplexity = @override.MaxCssSelectorComplexity ?? config.MaxCssSelectorComplexity,
            ExemptPaths = @override.ExemptPaths ?? config.ExemptPaths,
        };
    }

    public static JsConfig ApplyJs(JsConfig config, JsConfigOverride? @override)
    {
        if (@override == null) return config;
        return config with
        {
            MaxJsLineCount = @override.MaxJsLineCount ?? config.MaxJsLineCount,
            EnforceJsModules = @override.EnforceJsModules ?? config.EnforceJsModules,
            ExemptPaths = @override.ExemptPaths ?? config.ExemptPaths,
        };
    }

    public static RazorConfig ApplyRazor(RazorConfig config, RazorConfigOverride? @override)
    {
        if (@override == null) return config;
        return config with
        {
            MaxRazorLineCount = @override.MaxRazorLineCount ?? config.MaxRazorLineCount,
            MaxRazorCodeBlockLines = @override.MaxRazorCodeBlockLines ?? config.MaxRazorCodeBlockLines,
            MaxMarkupNestingDepth = @override.MaxMarkupNestingDepth ?? config.MaxMarkupNestingDepth,
            BanInlineEventLambdas = @override.BanInlineEventLambdas ?? config.BanInlineEventLambdas,
            MaxControlFlowBlocks = @override.MaxControlFlowBlocks ?? config.MaxControlFlowBlocks,
            MaxForeachNestingDepth = @override.MaxForeachNestingDepth ?? config.MaxForeachNestingDepth,
            MaxComponentParameterCount = @override.MaxComponentParameterCount ?? config.MaxComponentParameterCount,
            BanInlineTernaryInAttributes = @override.BanInlineTernaryInAttributes ?? config.BanInlineTernaryInAttributes,
        };
    }
}
