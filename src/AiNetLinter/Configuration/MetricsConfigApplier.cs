#nullable enable
namespace AiNetLinter.Configuration;

/// <summary>
/// Wendet die <see cref="MetricsConfigOverride"/>-Sektionen auf eine
/// <see cref="MetricsConfig"/>-Instanz an. Bewusst als statische Helper-Klasse
/// extrahiert (nicht als private Methoden auf <see cref="MetricsConfig"/>),
/// damit der <c>MetricsConfig</c>-Record selbst schmaler bleibt und die
/// <c>AIContextFootprint</c>-Last pro transitivem Konsumenten (z. B. die
/// <c>*ToolRegistrations</c>-Klassen im MCP-Pfad) sinkt.
/// </summary>
internal static class MetricsConfigApplier
{
    /// <summary>
    /// Einstiegspunkt: wendet alle Override-Sektionen nacheinander an.
    /// Nur gesetzte (nicht-null) Override-Felder werden angewendet.
    /// </summary>
    public static MetricsConfig Apply(MetricsConfig config, MetricsConfigOverride? @override)
    {
        if (@override == null) return config;
        return ApplyDirectoryAndMemberLimits(
            ApplyDependencyLimits(
                ApplyComplexityLimits(
                    ApplyLineLimits(config, @override),
                    @override),
                @override),
            @override);
    }

    public static MetricsConfig ApplyLineLimits(MetricsConfig config, MetricsConfigOverride o) => config with
    {
        MaxLineCount = o.MaxLineCount ?? config.MaxLineCount,
        MaxMethodLineCount = o.MaxMethodLineCount ?? config.MaxMethodLineCount,
        MaxMethodParameterCount = o.MaxMethodParameterCount ?? config.MaxMethodParameterCount,
        MaxMethodParameterCountInTestFiles = o.MaxMethodParameterCountInTestFiles ?? config.MaxMethodParameterCountInTestFiles,
        MethodParameterCountIgnoreTypeNames = o.MethodParameterCountIgnoreTypeNames ?? config.MethodParameterCountIgnoreTypeNames,
        MethodParameterCountIgnoreTypePrefixes = o.MethodParameterCountIgnoreTypePrefixes ?? config.MethodParameterCountIgnoreTypePrefixes,
        MaxMethodParameterCountAllowPrivate = o.MaxMethodParameterCountAllowPrivate ?? config.MaxMethodParameterCountAllowPrivate,
        MaxMethodParameterCountForNonPublic = o.MaxMethodParameterCountForNonPublic ?? config.MaxMethodParameterCountForNonPublic,
        MaxMethodOverloads = o.MaxMethodOverloads ?? config.MaxMethodOverloads,
        CompoundSuppressions = o.CompoundSuppressions ?? config.CompoundSuppressions,
        MaxLinqChainLength = o.MaxLinqChainLength ?? config.MaxLinqChainLength,
        LinqMethodNames = o.LinqMethodNames ?? config.LinqMethodNames,
    };

    public static MetricsConfig ApplyComplexityLimits(MetricsConfig config, MetricsConfigOverride o) => config with
    {
        MaxCyclomaticComplexity = o.MaxCyclomaticComplexity ?? config.MaxCyclomaticComplexity,
        MaxCognitiveComplexity = o.MaxCognitiveComplexity ?? config.MaxCognitiveComplexity,
        MinCognitiveComplexityForTest = o.MinCognitiveComplexityForTest ?? config.MinCognitiveComplexityForTest,
        AggregatePartialClassLineCount = o.AggregatePartialClassLineCount ?? config.AggregatePartialClassLineCount,
        ComplexityNearMissTolerance = o.ComplexityNearMissTolerance ?? config.ComplexityNearMissTolerance,
        ExcludeSwitchDispatcherCases = o.ExcludeSwitchDispatcherCases ?? config.ExcludeSwitchDispatcherCases,
        SwitchDispatcherMaxCaseBodyLines = o.SwitchDispatcherMaxCaseBodyLines ?? config.SwitchDispatcherMaxCaseBodyLines,
        ExcludeNullCoalescingInitializerComplexity = o.ExcludeNullCoalescingInitializerComplexity ?? config.ExcludeNullCoalescingInitializerComplexity,
        NullCoalescingInitializerMaxNonCoalescingRatio = o.NullCoalescingInitializerMaxNonCoalescingRatio ?? config.NullCoalescingInitializerMaxNonCoalescingRatio,
        MaxSwitchArms = o.MaxSwitchArms ?? config.MaxSwitchArms,
        MaxSwitchArmsExcludeDispatcher = o.MaxSwitchArmsExcludeDispatcher ?? config.MaxSwitchArmsExcludeDispatcher,
        MaxSwitchArmsExemptTypes = o.MaxSwitchArmsExemptTypes ?? config.MaxSwitchArmsExemptTypes,
    };

    public static MetricsConfig ApplyDependencyLimits(MetricsConfig config, MetricsConfigOverride o) => config with
    {
        MaxConstructorDependencies = o.MaxConstructorDependencies ?? config.MaxConstructorDependencies,
        ConstructorDependencyIgnoreTypePrefixes = o.ConstructorDependencyIgnoreTypePrefixes ?? config.ConstructorDependencyIgnoreTypePrefixes,
        ConstructorDependencyExemptClassSuffixes = o.ConstructorDependencyExemptClassSuffixes ?? config.ConstructorDependencyExemptClassSuffixes,
        MaxInheritanceDepth = o.MaxInheritanceDepth ?? config.MaxInheritanceDepth,
        InheritanceDepthFrameworkPrefixes = o.InheritanceDepthFrameworkPrefixes ?? config.InheritanceDepthFrameworkPrefixes,
        MaxAIContextFootprint = o.MaxAIContextFootprint ?? config.MaxAIContextFootprint,
        FootprintIgnoreNamespacePrefixes = o.FootprintIgnoreNamespacePrefixes ?? config.FootprintIgnoreNamespacePrefixes,
        FootprintIgnoreTypeNames = o.FootprintIgnoreTypeNames ?? config.FootprintIgnoreTypeNames,
    };

    public static MetricsConfig ApplyDirectoryAndMemberLimits(MetricsConfig config, MetricsConfigOverride o) => config with
    {
        MaxDirectoryDepth = o.MaxDirectoryDepth ?? config.MaxDirectoryDepth,
        MaxDirectoryChildren = o.MaxDirectoryChildren ?? config.MaxDirectoryChildren,
        MaxDirectoryChildrenExemptNames = o.MaxDirectoryChildrenExemptNames ?? config.MaxDirectoryChildrenExemptNames,
        MaxBoolParameterCount = o.MaxBoolParameterCount ?? config.MaxBoolParameterCount,
        MaxBoolParameterCountAllowPrivate = o.MaxBoolParameterCountAllowPrivate ?? config.MaxBoolParameterCountAllowPrivate,
        MaxBoolParameterCountExemptMethodPrefixes = o.MaxBoolParameterCountExemptMethodPrefixes ?? config.MaxBoolParameterCountExemptMethodPrefixes,
        MaxPartialClassFiles = o.MaxPartialClassFiles ?? config.MaxPartialClassFiles,
        MaxPartialClassFilesExemptTypes = o.MaxPartialClassFilesExemptTypes ?? config.MaxPartialClassFilesExemptTypes,
        MaxPublicMembersPerType = o.MaxPublicMembersPerType ?? config.MaxPublicMembersPerType,
        MaxPublicMembersPerTypeExemptSuffixes = o.MaxPublicMembersPerTypeExemptSuffixes ?? config.MaxPublicMembersPerTypeExemptSuffixes,
        MaxPublicMembersPerTypeApplyToTestFiles = o.MaxPublicMembersPerTypeApplyToTestFiles ?? config.MaxPublicMembersPerTypeApplyToTestFiles,
    };
}
