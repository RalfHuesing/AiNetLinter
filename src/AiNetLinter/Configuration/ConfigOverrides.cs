namespace AiNetLinter.Configuration;

public sealed record GlobalConfigOverride
{
    public bool? EnforceSealedClasses { get; init; }

    public bool? AllowUnsealedPartialClasses { get; init; }

    public bool? AllowDynamic { get; init; }

    public bool? AllowOutParameters { get; init; }

    public bool? EnforceValueObjectContracts { get; init; }

    public bool? EnableTestSentinel { get; init; }

    public bool? EnforcePascalCase { get; init; }

    public bool? EnforceAsciiIdentifiers { get; init; }

    public bool? EnforceXmlDocumentation { get; init; }

    public bool? EnforceSemanticNaming { get; init; }

    public bool? EnforceNullableEnable { get; init; }

    public bool? EnforceNoSilentCatch { get; init; }

    public bool? AllowTryPatternOutParameters { get; init; }

    public bool? AllowCancellationShutdownCatch { get; init; }

    public IReadOnlyList<string>? AllowedSilentCatchExceptionTypes { get; init; }

    public bool? EnforceMinimalApiAsParameters { get; init; }

    public bool? EnforceResultPatternOverExceptions { get; init; }

    public bool? EnforceExplicitStateImmutability { get; init; }
    public IReadOnlyCollection<string>? AllowedExceptions { get; init; }
    public bool? PreventContextDependentOverloads { get; init; }
    public bool? EnforceNamespaceDirectoryMapping { get; init; }
    public string? NamespaceDirectoryMappingMode { get; init; }
    public IReadOnlyCollection<string>? NamespaceDirectoryMappingIgnorePathSegments { get; init; }
    public int? NamespaceDirectoryMappingRequiredTrailingSegments { get; init; }
    public bool? DetectAndBanPhantomDependencies { get; init; }
    public IReadOnlyCollection<string>? ImmutabilityExemptSuffixes { get; init; }
    public IReadOnlyCollection<string>? ImmutabilityExemptPatterns { get; init; }
    public bool? AllowedEmptyReads { get; init; }
    public IReadOnlyCollection<string>? SealedClassExemptSuffixes { get; init; }
    public IReadOnlyCollection<string>? ImmutabilityExemptBaseTypes { get; init; }
    public bool? ImmutabilityAllowPrivateBackingFields { get; init; }
    public IReadOnlyCollection<string>? ResultPatternAllowThrowInNamespaceSuffixes { get; init; }
    public bool? ResultPatternAllowCatchRethrow { get; init; }
    public bool? EnablePerformanceProfiling { get; init; }
    public bool? AllowOutParametersInPrivateMethods { get; init; }
    public IReadOnlyCollection<string>? SemanticNamingExemptMethodNames { get; init; }
    public bool? SemanticNamingAllowSubstringOfMethodName { get; init; }

    public bool? BanPublicNestedTypes { get; init; }

    public bool? BanPublicNestedTypesAllowPrivate { get; init; }

    public bool? AvoidExcessiveMiddleMen { get; init; }

    public double? MaxMiddleManForwardingRatio { get; init; }

    public int? MiddleManMinMemberCount { get; init; }

    public bool? MiddleManIncludePrivateMembers { get; init; }

    public IReadOnlyCollection<string>? MiddleManExemptSuffixes { get; init; }

    public IReadOnlyCollection<string>? MiddleManExemptBaseTypes { get; init; }

    public bool? BanAsyncVoid { get; init; }

    public bool? AsyncVoidAllowEventHandlers { get; init; }

    public bool? BanBlockingTaskAccess { get; init; }

    public bool? BanBlockingTaskAccessAllowInMain { get; init; }

    public bool? BanBlockingTaskAccessAllowInTests { get; init; }
}

public sealed record MetricsConfigOverride
{
    public int? MaxLineCount { get; init; }

    public int? MaxMethodParameterCount { get; init; }

    public int? MaxMethodParameterCountInTestFiles { get; init; }

    public IReadOnlyCollection<string>? MethodParameterCountIgnoreTypeNames { get; init; }

    public IReadOnlyCollection<string>? MethodParameterCountIgnoreTypePrefixes { get; init; }

    public bool? MaxMethodParameterCountAllowPrivate { get; init; }

    public int? MaxMethodParameterCountForNonPublic { get; init; }

    public int? MaxMethodLineCount { get; init; }

    public int? MaxCyclomaticComplexity { get; init; }

    public int? MaxCognitiveComplexity { get; init; }

    public int? MaxInheritanceDepth { get; init; }

    public int? MinCognitiveComplexityForTest { get; init; }

    public bool? AggregatePartialClassLineCount { get; init; }

    public int? MaxMethodOverloads { get; init; }

    public int? MaxConstructorDependencies { get; init; }

    public int? MaxAIContextFootprint { get; init; }

    public int? MaxDirectoryDepth { get; init; }

    public IReadOnlyCollection<string>? InheritanceDepthFrameworkPrefixes { get; init; }

    public IReadOnlyCollection<string>? ConstructorDependencyIgnoreTypePrefixes { get; init; }

    public IReadOnlyCollection<string>? ConstructorDependencyExemptClassSuffixes { get; init; }

    public int? ComplexityNearMissTolerance { get; init; }
    public bool? ExcludeSwitchDispatcherCases { get; init; }
    public int? SwitchDispatcherMaxCaseBodyLines { get; init; }
    public bool? ExcludeNullCoalescingInitializerComplexity { get; init; }
    public double? NullCoalescingInitializerMaxNonCoalescingRatio { get; init; }

    public int? MaxSwitchArms { get; init; }

    public bool? MaxSwitchArmsExcludeDispatcher { get; init; }

    public IReadOnlyCollection<string>? MaxSwitchArmsExemptTypes { get; init; }

    public IReadOnlyCollection<string>? FootprintIgnoreNamespacePrefixes { get; init; }

    public IReadOnlyCollection<string>? FootprintIgnoreTypeNames { get; init; }

    public int? MaxBoolParameterCount { get; init; }
    public bool? MaxBoolParameterCountAllowPrivate { get; init; }
    public IReadOnlyCollection<string>? MaxBoolParameterCountExemptMethodPrefixes { get; init; }

    public int? MaxDirectoryChildren { get; init; }
    public IReadOnlyCollection<string>? MaxDirectoryChildrenExemptNames { get; init; }

    public int? MaxPartialClassFiles { get; init; }
    public IReadOnlyCollection<string>? MaxPartialClassFilesExemptTypes { get; init; }

    public int? MaxPublicMembersPerType { get; init; }
    public IReadOnlyCollection<string>? MaxPublicMembersPerTypeExemptSuffixes { get; init; }
    public int? MaxLinqChainLength { get; init; }
    public IReadOnlyCollection<string>? LinqMethodNames { get; init; }
    public IReadOnlyList<CompoundSuppression>? CompoundSuppressions { get; init; }
}

public sealed record TestSentinelConfigOverride
{
    public IReadOnlyCollection<string>? ExemptClassNameSuffixes { get; init; }

    public IReadOnlyCollection<string>? ExemptWhenInheritsFrom { get; init; }

    public bool? ExemptStaticClasses { get; init; }

    public IReadOnlyList<string>? TestProjectNameSuffixes { get; init; }
}

