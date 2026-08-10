#nullable enable
namespace AiNetLinter.Configuration;

/// <summary>
/// Wendet die <see cref="GlobalConfigOverride"/>-Sektion auf eine
/// <see cref="GlobalConfig"/>-Instanz an. Bewusst als statische Helper-Klasse
/// extrahiert (nicht als Instanzmethode auf <see cref="GlobalConfig"/>),
/// damit der <c>GlobalConfig</c>-Record selbst schmaler bleibt und die
/// <c>AIContextFootprint</c>-Last pro transitivem Konsumenten (z. B. die
/// <c>*ToolRegistrations</c>-Klassen im MCP-Pfad) sinkt.
/// </summary>
internal static class GlobalConfigApplier
{
    public static GlobalConfig Apply(GlobalConfig config, GlobalConfigOverride? @override)
    {
        if (@override == null) return config;
        var o = @override;
        return config with
        {
            // Strukturregeln
            EnforceSealedClasses                        = o.EnforceSealedClasses                        ?? config.EnforceSealedClasses,
            AllowUnsealedPartialClasses                 = o.AllowUnsealedPartialClasses                 ?? config.AllowUnsealedPartialClasses,
            AllowDynamic                                = o.AllowDynamic                                ?? config.AllowDynamic,
            AllowOutParameters                          = o.AllowOutParameters                          ?? config.AllowOutParameters,
            AllowTryPatternOutParameters                = o.AllowTryPatternOutParameters                ?? config.AllowTryPatternOutParameters,
            AllowOutParametersInPrivateMethods          = o.AllowOutParametersInPrivateMethods          ?? config.AllowOutParametersInPrivateMethods,
            SealedClassExemptSuffixes                   = o.SealedClassExemptSuffixes                   ?? config.SealedClassExemptSuffixes,

            // Naming und Stil
            EnforcePascalCase                           = o.EnforcePascalCase                           ?? config.EnforcePascalCase,
            EnforceAsciiIdentifiers                     = o.EnforceAsciiIdentifiers                     ?? config.EnforceAsciiIdentifiers,
            EnforceSemanticNaming                       = o.EnforceSemanticNaming                       ?? config.EnforceSemanticNaming,
            SemanticNamingExemptMethodNames             = o.SemanticNamingExemptMethodNames             ?? config.SemanticNamingExemptMethodNames,
            SemanticNamingAllowSubstringOfMethodName    = o.SemanticNamingAllowSubstringOfMethodName    ?? config.SemanticNamingAllowSubstringOfMethodName,
            EnforceNullableEnable                       = o.EnforceNullableEnable                       ?? config.EnforceNullableEnable,
            EnforceXmlDocumentation                     = o.EnforceXmlDocumentation                     ?? config.EnforceXmlDocumentation,
            EnforceMinimalApiAsParameters               = o.EnforceMinimalApiAsParameters               ?? config.EnforceMinimalApiAsParameters,
            EnableTestSentinel                          = o.EnableTestSentinel                          ?? config.EnableTestSentinel,

            // Catch-Regeln
            EnforceNoSilentCatch                        = o.EnforceNoSilentCatch                        ?? config.EnforceNoSilentCatch,
            AllowCancellationShutdownCatch              = o.AllowCancellationShutdownCatch              ?? config.AllowCancellationShutdownCatch,
            AllowedSilentCatchExceptionTypes            = o.AllowedSilentCatchExceptionTypes            ?? config.AllowedSilentCatchExceptionTypes,
            EnforceResultPatternOverExceptions          = o.EnforceResultPatternOverExceptions          ?? config.EnforceResultPatternOverExceptions,
            ResultPatternAllowThrowInNamespaceSuffixes  = o.ResultPatternAllowThrowInNamespaceSuffixes  ?? config.ResultPatternAllowThrowInNamespaceSuffixes,
            ResultPatternAllowCatchRethrow              = o.ResultPatternAllowCatchRethrow              ?? config.ResultPatternAllowCatchRethrow,
            AllowedExceptions                           = o.AllowedExceptions                           ?? config.AllowedExceptions,

            // Immutabilität
            EnforceValueObjectContracts                 = o.EnforceValueObjectContracts                 ?? config.EnforceValueObjectContracts,
            EnforceExplicitStateImmutability            = o.EnforceExplicitStateImmutability            ?? config.EnforceExplicitStateImmutability,
            ImmutabilityExemptSuffixes                  = o.ImmutabilityExemptSuffixes                  ?? config.ImmutabilityExemptSuffixes,
            ImmutabilityExemptPatterns                  = o.ImmutabilityExemptPatterns                  ?? config.ImmutabilityExemptPatterns,
            ImmutabilityExemptBaseTypes                 = o.ImmutabilityExemptBaseTypes                 ?? config.ImmutabilityExemptBaseTypes,
            ImmutabilityAllowPrivateBackingFields       = o.ImmutabilityAllowPrivateBackingFields       ?? config.ImmutabilityAllowPrivateBackingFields,
            AllowedEmptyReads                           = o.AllowedEmptyReads                           ?? config.AllowedEmptyReads,

            // Namespace- und Analyse-Regeln
            EnforceNamespaceDirectoryMapping            = o.EnforceNamespaceDirectoryMapping            ?? config.EnforceNamespaceDirectoryMapping,
            NamespaceDirectoryMappingMode               = o.NamespaceDirectoryMappingMode               ?? config.NamespaceDirectoryMappingMode,
            NamespaceDirectoryMappingIgnorePathSegments = o.NamespaceDirectoryMappingIgnorePathSegments ?? config.NamespaceDirectoryMappingIgnorePathSegments,
            NamespaceDirectoryMappingRequiredTrailingSegments = o.NamespaceDirectoryMappingRequiredTrailingSegments ?? config.NamespaceDirectoryMappingRequiredTrailingSegments,
            DetectAndBanPhantomDependencies             = o.DetectAndBanPhantomDependencies             ?? config.DetectAndBanPhantomDependencies,
            PreventContextDependentOverloads            = o.PreventContextDependentOverloads            ?? config.PreventContextDependentOverloads,
            BanPublicNestedTypes                        = o.BanPublicNestedTypes                        ?? config.BanPublicNestedTypes,
            BanPublicNestedTypesAllowPrivate            = o.BanPublicNestedTypesAllowPrivate            ?? config.BanPublicNestedTypesAllowPrivate,
            EnablePerformanceProfiling                  = o.EnablePerformanceProfiling                  ?? config.EnablePerformanceProfiling,
            BanAsyncVoid                                = o.BanAsyncVoid                                ?? config.BanAsyncVoid,
            AsyncVoidAllowEventHandlers                 = o.AsyncVoidAllowEventHandlers                 ?? config.AsyncVoidAllowEventHandlers,
            BanBlockingTaskAccess                       = o.BanBlockingTaskAccess                       ?? config.BanBlockingTaskAccess,
            BanBlockingTaskAccessAllowInMain            = o.BanBlockingTaskAccessAllowInMain            ?? config.BanBlockingTaskAccessAllowInMain,
            BanBlockingTaskAccessAllowInTests           = o.BanBlockingTaskAccessAllowInTests           ?? config.BanBlockingTaskAccessAllowInTests,

            AvoidExcessiveMiddleMen                     = o.AvoidExcessiveMiddleMen                     ?? config.AvoidExcessiveMiddleMen,
            MaxMiddleManForwardingRatio                 = o.MaxMiddleManForwardingRatio                 ?? config.MaxMiddleManForwardingRatio,
            MiddleManMinMemberCount                     = o.MiddleManMinMemberCount                     ?? config.MiddleManMinMemberCount,
            MiddleManExemptSuffixes                     = o.MiddleManExemptSuffixes                     ?? config.MiddleManExemptSuffixes,
            MiddleManExemptBaseTypes                    = o.MiddleManExemptBaseTypes                    ?? config.MiddleManExemptBaseTypes,
            MiddleManIncludePrivateMembers              = o.MiddleManIncludePrivateMembers              ?? config.MiddleManIncludePrivateMembers,
        };
    }
}
