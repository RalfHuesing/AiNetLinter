#nullable enable

using AiNetLinter.Configuration;

namespace AiNetLinter.Core;

/// <summary>
/// Typisierte Konstanten für alle Linter-Regel-IDs.
/// Statt Magic Strings "EnforceSealedClasses" → LinterRuleIds.EnforceSealedClasses.
/// Änderungen an Config-Properties werden über nameof() automatisch propagiert.
/// </summary>
internal static class LinterRuleIds
{
    // --- Metriken (MetricsConfig) ---
    internal const string MaxLineCount              = nameof(MetricsConfig.MaxLineCount);
    internal const string MaxMethodLineCount        = nameof(MetricsConfig.MaxMethodLineCount);
    internal const string MaxMethodParameterCount   = nameof(MetricsConfig.MaxMethodParameterCount);
    internal const string MaxCyclomaticComplexity   = nameof(MetricsConfig.MaxCyclomaticComplexity);
    internal const string MaxCognitiveComplexity    = nameof(MetricsConfig.MaxCognitiveComplexity);
    internal const string MaxInheritanceDepth       = nameof(MetricsConfig.MaxInheritanceDepth);
    internal const string MaxMethodOverloads        = nameof(MetricsConfig.MaxMethodOverloads);
    internal const string MaxConstructorDependencies = nameof(MetricsConfig.MaxConstructorDependencies);
    internal const string MaxDirectoryDepth         = nameof(MetricsConfig.MaxDirectoryDepth);
    internal const string MaxDirectoryChildren      = nameof(MetricsConfig.MaxDirectoryChildren);
    internal const string MaxBoolParameterCount     = nameof(MetricsConfig.MaxBoolParameterCount);
    internal const string MaxPartialClassFiles      = nameof(MetricsConfig.MaxPartialClassFiles);
    internal const string MaxPublicMembersPerType   = nameof(MetricsConfig.MaxPublicMembersPerType);

    // RuleId weicht vom Property-Namen ab (MaxAIContextFootprint → AIContextFootprint)
    internal const string AIContextFootprint        = "AIContextFootprint";

    // --- Globale Regeln (GlobalConfig) ---
    internal const string EnforceSealedClasses              = nameof(GlobalConfig.EnforceSealedClasses);
    internal const string AllowUnsealedPartialClasses       = nameof(GlobalConfig.AllowUnsealedPartialClasses);
    internal const string BanPublicNestedTypes              = nameof(GlobalConfig.BanPublicNestedTypes);
    internal const string EnforceNoSilentCatch              = nameof(GlobalConfig.EnforceNoSilentCatch);
    internal const string EnforcePascalCase                 = nameof(GlobalConfig.EnforcePascalCase);
    internal const string EnforceXmlDocumentation           = nameof(GlobalConfig.EnforceXmlDocumentation);
    internal const string EnforceSemanticNaming             = nameof(GlobalConfig.EnforceSemanticNaming);
    internal const string EnforceNullableEnable             = nameof(GlobalConfig.EnforceNullableEnable);
    internal const string AllowDynamic                      = nameof(GlobalConfig.AllowDynamic);
    internal const string AllowOutParameters                = nameof(GlobalConfig.AllowOutParameters);
    internal const string AllowTryPatternOutParameters      = nameof(GlobalConfig.AllowTryPatternOutParameters);
    internal const string AllowCancellationShutdownCatch    = nameof(GlobalConfig.AllowCancellationShutdownCatch);
    internal const string AllowedEmptyReads                 = nameof(GlobalConfig.AllowedEmptyReads);
    internal const string EnforceMinimalApiAsParameters     = nameof(GlobalConfig.EnforceMinimalApiAsParameters);
    internal const string EnforceResultPatternOverExceptions = nameof(GlobalConfig.EnforceResultPatternOverExceptions);
    internal const string EnforceNamespaceDirectoryMapping  = nameof(GlobalConfig.EnforceNamespaceDirectoryMapping);
    internal const string DetectAndBanPhantomDependencies   = nameof(GlobalConfig.DetectAndBanPhantomDependencies);
    internal const string EnforceValueObjectContracts       = nameof(GlobalConfig.EnforceValueObjectContracts);
    internal const string PreventContextDependentOverloads  = nameof(GlobalConfig.PreventContextDependentOverloads);
    internal const string EnforceExplicitStateImmutability  = nameof(GlobalConfig.EnforceExplicitStateImmutability);

    // RuleId weicht vom Property-Namen ab (EnableTestSentinel → StaticTestSentinel)
    internal const string StaticTestSentinel                = "StaticTestSentinel";

    // Namespace-Abhängigkeitsregel (über ForbiddenNamespaceDependencies-Collection gesteuert)
    internal const string ForbiddenNamespaceDependency      = "ForbiddenNamespaceDependency";

    // UI-Separation (UiSeparationConfig)
    internal const string BlazorRequireCodeBehind           = "BlazorRequireCodeBehind";
    internal const string BlazorRequireCssIsolation         = "BlazorRequireCssIsolation";
    internal const string WpfRequireMinimalCodeBehind       = "WpfRequireMinimalCodeBehind";
}

// --- Metric-Namen für CompoundSuppressions.WhenAllOf ---
internal static class MetricNames
{
    internal const string CyclomaticComplexity    = "CyclomaticComplexity";
    internal const string CognitiveComplexity     = "CognitiveComplexity";
    internal const string ParameterCount          = "ParameterCount";
    internal const string LineCount               = "LineCount";
    internal const string ConstructorDependencies = "ConstructorDependencies";
    internal const string PublicMemberCount       = "PublicMemberCount";
}
