#nullable enable
namespace AiNetLinter.Configuration;

/// <summary>
/// Wendet die <see cref="GlobalConfigOverride"/>-Sektion auf eine
/// <see cref="GlobalConfig"/>-Instanz an. Bewusst als statische Helper-Klasse
/// extrahiert (nicht als Instanzmethode auf <see cref="GlobalConfig"/>),
/// damit der <c>GlobalConfig</c>-Record selbst schmaler bleibt und die
/// <c>AIContextFootprint</c>-Last pro transitivem Konsumenten (z. B. die
/// <c>*ToolRegistrations</c>-Klassen im MCP-Pfad) sinkt.
/// <para/>
/// <see cref="Apply"/> ist eine fluent verkettete Pipeline aus einer <c>Apply*Overrides</c>-Methode
/// pro thematischer Sektion (Extension-Methoden auf <see cref="GlobalConfig"/>) — eine Methode pro
/// den bereits vorher etablierten Kommentar-Sektionen (Strukturregeln, Naming/Stil, Catch-Regeln,
/// Immutabilitaet, Namespace-/Analyse-Regeln, Middle-Man-Erkennung, Duplicate-Detection). Jede
/// Sektions-Methode bleibt Expression-bodied (<c>=&gt; config with {...}</c>) und endet direkt in
/// einem <c>with</c>-Ausdruck, damit <see cref="MethodClassifier.IsNullCoalescingInitializer"/>
/// weiterhin greift: der Klassifikator verlangt exakt dieses Muster (letzte/einzige Anweisung ist
/// <c>return X with {...}</c>), sonst zaehlen die vielen <c>??</c>-Fallbacks faelschlich als echte
/// Verzweigung fuer <c>MaxCyclomaticComplexity</c>/<c>MaxCognitiveComplexity</c>. Ein
/// zwischengeschalteter Methodenaufruf INNERHALB einer Sektions-Methode wuerde dasselbe Problem
/// reproduzieren — <see cref="Apply"/> selbst bleibt daher bewusst trivial (nur die Verkettung,
/// keine eigene Koaleszenz) und braucht die Ausnahme gar nicht. Einheitlich fuer ALLE Sektionen
/// (nicht nur Duplicate-Detection).
/// </summary>
internal static class GlobalConfigApplier
{
    public static GlobalConfig Apply(GlobalConfig config, GlobalConfigOverride? @override)
    {
        if (@override == null) return config;
        return config
            .ApplyStructureOverrides(@override)
            .ApplyNamingAndStyleOverrides(@override)
            .ApplyCatchRuleOverrides(@override)
            .ApplyImmutabilityOverrides(@override)
            .ApplyNamespaceAndAnalysisOverrides(@override)
            .ApplyMiddleManOverrides(@override)
            .ApplyDuplicateDetectionOverrides(@override);
    }

    private static GlobalConfig ApplyStructureOverrides(this GlobalConfig config, GlobalConfigOverride o) =>
        config with
        {
            EnforceSealedClasses               = o.EnforceSealedClasses               ?? config.EnforceSealedClasses,
            AllowUnsealedPartialClasses        = o.AllowUnsealedPartialClasses        ?? config.AllowUnsealedPartialClasses,
            AllowDynamic                       = o.AllowDynamic                       ?? config.AllowDynamic,
            AllowOutParameters                 = o.AllowOutParameters                 ?? config.AllowOutParameters,
            AllowTryPatternOutParameters       = o.AllowTryPatternOutParameters       ?? config.AllowTryPatternOutParameters,
            AllowOutParametersInPrivateMethods = o.AllowOutParametersInPrivateMethods ?? config.AllowOutParametersInPrivateMethods,
            SealedClassExemptSuffixes          = o.SealedClassExemptSuffixes          ?? config.SealedClassExemptSuffixes,
        };

    private static GlobalConfig ApplyNamingAndStyleOverrides(this GlobalConfig config, GlobalConfigOverride o) =>
        config with
        {
            EnforcePascalCase                        = o.EnforcePascalCase                        ?? config.EnforcePascalCase,
            EnforceAsciiIdentifiers                  = o.EnforceAsciiIdentifiers                  ?? config.EnforceAsciiIdentifiers,
            EnforceSemanticNaming                    = o.EnforceSemanticNaming                    ?? config.EnforceSemanticNaming,
            SemanticNamingExemptMethodNames          = o.SemanticNamingExemptMethodNames          ?? config.SemanticNamingExemptMethodNames,
            SemanticNamingAllowSubstringOfMethodName = o.SemanticNamingAllowSubstringOfMethodName ?? config.SemanticNamingAllowSubstringOfMethodName,
            EnforceNullableEnable                    = o.EnforceNullableEnable                    ?? config.EnforceNullableEnable,
            EnforceXmlDocumentation                  = o.EnforceXmlDocumentation                  ?? config.EnforceXmlDocumentation,
            EnforceMinimalApiAsParameters            = o.EnforceMinimalApiAsParameters            ?? config.EnforceMinimalApiAsParameters,
            EnableTestSentinel                       = o.EnableTestSentinel                       ?? config.EnableTestSentinel,
        };

    private static GlobalConfig ApplyCatchRuleOverrides(this GlobalConfig config, GlobalConfigOverride o) =>
        config with
        {
            EnforceNoSilentCatch                       = o.EnforceNoSilentCatch                       ?? config.EnforceNoSilentCatch,
            AllowCancellationShutdownCatch             = o.AllowCancellationShutdownCatch             ?? config.AllowCancellationShutdownCatch,
            AllowedSilentCatchExceptionTypes           = o.AllowedSilentCatchExceptionTypes           ?? config.AllowedSilentCatchExceptionTypes,
            EnforceResultPatternOverExceptions         = o.EnforceResultPatternOverExceptions         ?? config.EnforceResultPatternOverExceptions,
            ResultPatternAllowThrowInNamespaceSuffixes = o.ResultPatternAllowThrowInNamespaceSuffixes ?? config.ResultPatternAllowThrowInNamespaceSuffixes,
            ResultPatternAllowCatchRethrow             = o.ResultPatternAllowCatchRethrow             ?? config.ResultPatternAllowCatchRethrow,
            AllowedExceptions                          = o.AllowedExceptions                          ?? config.AllowedExceptions,
        };

    private static GlobalConfig ApplyImmutabilityOverrides(this GlobalConfig config, GlobalConfigOverride o) =>
        config with
        {
            EnforceValueObjectContracts           = o.EnforceValueObjectContracts           ?? config.EnforceValueObjectContracts,
            EnforceExplicitStateImmutability      = o.EnforceExplicitStateImmutability      ?? config.EnforceExplicitStateImmutability,
            ImmutabilityExemptSuffixes            = o.ImmutabilityExemptSuffixes            ?? config.ImmutabilityExemptSuffixes,
            ImmutabilityExemptPatterns            = o.ImmutabilityExemptPatterns            ?? config.ImmutabilityExemptPatterns,
            ImmutabilityExemptBaseTypes           = o.ImmutabilityExemptBaseTypes           ?? config.ImmutabilityExemptBaseTypes,
            ImmutabilityAllowPrivateBackingFields = o.ImmutabilityAllowPrivateBackingFields ?? config.ImmutabilityAllowPrivateBackingFields,
            AllowedEmptyReads                     = o.AllowedEmptyReads                     ?? config.AllowedEmptyReads,
        };

    private static GlobalConfig ApplyNamespaceAndAnalysisOverrides(this GlobalConfig config, GlobalConfigOverride o) =>
        config with
        {
            EnforceNamespaceDirectoryMapping                  = o.EnforceNamespaceDirectoryMapping                  ?? config.EnforceNamespaceDirectoryMapping,
            NamespaceDirectoryMappingMode                     = o.NamespaceDirectoryMappingMode                     ?? config.NamespaceDirectoryMappingMode,
            NamespaceDirectoryMappingIgnorePathSegments       = o.NamespaceDirectoryMappingIgnorePathSegments       ?? config.NamespaceDirectoryMappingIgnorePathSegments,
            NamespaceDirectoryMappingRequiredTrailingSegments = o.NamespaceDirectoryMappingRequiredTrailingSegments ?? config.NamespaceDirectoryMappingRequiredTrailingSegments,
            DetectAndBanPhantomDependencies                   = o.DetectAndBanPhantomDependencies                   ?? config.DetectAndBanPhantomDependencies,
            PreventContextDependentOverloads                  = o.PreventContextDependentOverloads                  ?? config.PreventContextDependentOverloads,
            BanPublicNestedTypes                              = o.BanPublicNestedTypes                              ?? config.BanPublicNestedTypes,
            BanPublicNestedTypesAllowPrivate                  = o.BanPublicNestedTypesAllowPrivate                  ?? config.BanPublicNestedTypesAllowPrivate,
            EnablePerformanceProfiling                        = o.EnablePerformanceProfiling                        ?? config.EnablePerformanceProfiling,
            BanAsyncVoid                                      = o.BanAsyncVoid                                      ?? config.BanAsyncVoid,
            AsyncVoidAllowEventHandlers                       = o.AsyncVoidAllowEventHandlers                       ?? config.AsyncVoidAllowEventHandlers,
            BanBlockingTaskAccess                             = o.BanBlockingTaskAccess                             ?? config.BanBlockingTaskAccess,
            BanBlockingTaskAccessAllowInMain                  = o.BanBlockingTaskAccessAllowInMain                  ?? config.BanBlockingTaskAccessAllowInMain,
            BanBlockingTaskAccessAllowInTests                 = o.BanBlockingTaskAccessAllowInTests                 ?? config.BanBlockingTaskAccessAllowInTests,
        };

    private static GlobalConfig ApplyMiddleManOverrides(this GlobalConfig config, GlobalConfigOverride o) =>
        config with
        {
            AvoidExcessiveMiddleMen         = o.AvoidExcessiveMiddleMen         ?? config.AvoidExcessiveMiddleMen,
            MaxMiddleManForwardingRatio     = o.MaxMiddleManForwardingRatio     ?? config.MaxMiddleManForwardingRatio,
            MiddleManMinMemberCount         = o.MiddleManMinMemberCount         ?? config.MiddleManMinMemberCount,
            MiddleManExemptSuffixes         = o.MiddleManExemptSuffixes         ?? config.MiddleManExemptSuffixes,
            MiddleManExemptBaseTypes        = o.MiddleManExemptBaseTypes        ?? config.MiddleManExemptBaseTypes,
            MiddleManIncludePrivateMembers  = o.MiddleManIncludePrivateMembers  ?? config.MiddleManIncludePrivateMembers,
        };

    private static GlobalConfig ApplyDuplicateDetectionOverrides(this GlobalConfig config, GlobalConfigOverride o) =>
        config with
        {
            EnableDuplicateCodeCheck         = o.EnableDuplicateCodeCheck         ?? config.EnableDuplicateCodeCheck,
            DuplicateCodeMinTokens           = o.DuplicateCodeMinTokens           ?? config.DuplicateCodeMinTokens,
            DuplicateCodeNgramSize           = o.DuplicateCodeNgramSize           ?? config.DuplicateCodeNgramSize,
            DuplicateCodeMinSharedNgrams     = o.DuplicateCodeMinSharedNgrams     ?? config.DuplicateCodeMinSharedNgrams,
            DuplicateCodeExactThreshold      = o.DuplicateCodeExactThreshold      ?? config.DuplicateCodeExactThreshold,
            DuplicateCodeNearThreshold       = o.DuplicateCodeNearThreshold       ?? config.DuplicateCodeNearThreshold,
            DuplicateCodeFuzzyThreshold      = o.DuplicateCodeFuzzyThreshold      ?? config.DuplicateCodeFuzzyThreshold,
            DuplicateCodeNormalizeIdentifiers = o.DuplicateCodeNormalizeIdentifiers ?? config.DuplicateCodeNormalizeIdentifiers,
            DuplicateCodeMaxResults          = o.DuplicateCodeMaxResults          ?? config.DuplicateCodeMaxResults,
            StructuralDuplicateExactThreshold = o.StructuralDuplicateExactThreshold ?? config.StructuralDuplicateExactThreshold,
            StructuralDuplicateNearThreshold  = o.StructuralDuplicateNearThreshold  ?? config.StructuralDuplicateNearThreshold,
            StructuralDuplicateFuzzyThreshold = o.StructuralDuplicateFuzzyThreshold ?? config.StructuralDuplicateFuzzyThreshold,
        };
}
