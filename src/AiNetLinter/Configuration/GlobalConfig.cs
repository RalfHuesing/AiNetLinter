#nullable enable
namespace AiNetLinter.Configuration;

public sealed record GlobalConfig
{
    public bool EnforceSealedClasses { get; init; } = true;
    public bool AllowUnsealedPartialClasses { get; init; } = false;
    public bool AllowDynamic { get; init; } = false;
    public bool AllowOutParameters { get; init; } = false;
    public bool EnforceValueObjectContracts { get; init; } = true;
    public bool EnableTestSentinel { get; init; } = true;
    public bool EnforcePascalCase { get; init; } = true;
    public bool EnforceAsciiIdentifiers { get; init; } = true;
    public bool EnforceXmlDocumentation { get; init; } = false;
    public bool EnforceSemanticNaming { get; init; } = true;
    public bool EnforceNullableEnable { get; init; } = true;
    public bool EnforceNoSilentCatch { get; init; } = true;
    public bool AllowTryPatternOutParameters { get; init; } = true;
    public bool AllowCancellationShutdownCatch { get; init; } = true;

    public bool AllowOutParametersInPrivateMethods { get; init; } = true;

    public IReadOnlyCollection<string> SemanticNamingExemptMethodNames { get; init; }
        = ["Equals", "CompareTo", "GetHashCode"];

    public bool SemanticNamingAllowSubstringOfMethodName { get; init; } = true;

    /// <summary>
    /// Exception-Typen, die lautlos abgefangen werden dürfen (leerer catch-Block ohne Variable).
    /// Analogon zu AllowCancellationShutdownCatch für projektspezifische Exception-Typen.
    /// Nur der einfache Typname, kein Namespace (z.B. "JSDisconnectedException").
    /// </summary>
    public IReadOnlyList<string> AllowedSilentCatchExceptionTypes { get; init; } = ["ObjectDisposedException"];
    public bool EnforceMinimalApiAsParameters { get; init; } = false;
    public bool EnforceResultPatternOverExceptions { get; init; } = false;

    public IReadOnlyCollection<string> ResultPatternAllowThrowInNamespaceSuffixes { get; init; }
        = ["Infrastructure", "Endpoints", "Middleware", "Program"];

    public bool ResultPatternAllowCatchRethrow { get; init; } = true;
    public bool EnforceExplicitStateImmutability { get; init; } = false;
    public IReadOnlyCollection<string> AllowedExceptions { get; init; } = new[]
    {
        "ArgumentException",
        "ArgumentNullException",
        "ArgumentOutOfRangeException",
        "InvalidOperationException",
        "NotSupportedException",
        "KeyNotFoundException",
        "IndexOutOfRangeException",
        "TimeoutException",
        "ObjectDisposedException",
        "NotImplementedException"
    };
    public bool PreventContextDependentOverloads { get; init; } = false;
    public bool EnforceNamespaceDirectoryMapping { get; init; } = true;

    public string NamespaceDirectoryMappingMode { get; init; } = "suffix-match";

    public IReadOnlyCollection<string> NamespaceDirectoryMappingIgnorePathSegments { get; init; }
        = ["src", "Source", "Domains", "Handlers"];

    public int NamespaceDirectoryMappingRequiredTrailingSegments { get; init; } = 2;

    public bool DetectAndBanPhantomDependencies { get; init; } = false;

    /// <summary>
    /// Verbietet oeffentliche (public/internal) nested Typen innerhalb von Klassen, Records und Structs.
    /// Verbessert die Grep-/File-Listing-Navigation fuer KI-Agenten und verhindert FQN-Halluzinationen
    /// (z. B. <c>PaymentStatus</c> statt <c>PaymentProcessor.PaymentStatus</c>).
    /// Standard: <c>true</c>. Private nested Typen bleiben erlaubt (Implementierungsdetail).
    /// </summary>
    public bool BanPublicNestedTypes { get; init; } = true;

    /// <summary>
    /// Wenn <c>true</c> (Standard): <c>private</c> nested Typen bleiben erlaubt, weil sie kein
    /// externes Grep-Target fuer Agenten darstellen. Auf <c>false</c> setzen, um auch private
    /// nested Typen zu melden (strikter Greenfield-Modus).
    /// </summary>
    public bool BanPublicNestedTypesAllowPrivate { get; init; } = true;

    /// <summary>
    /// Erkennt und meldet Klassen, die primär als Weiterleitungsschicht ("Middle Man") agieren,
    /// da sie die Indirektionstiefe für Agenten unnötig erhöhen.
    /// </summary>
    public bool AvoidExcessiveMiddleMen { get; init; } = true;

    public double MaxMiddleManForwardingRatio { get; init; } = 0.60;

    public int MiddleManMinMemberCount { get; init; } = 5;

    public bool MiddleManIncludePrivateMembers { get; init; } = false;


    public IReadOnlyCollection<string> MiddleManExemptSuffixes { get; init; }
        = ["Extensions", "Proxy", "Adapter", "Facade"];

    public IReadOnlyCollection<string> MiddleManExemptBaseTypes { get; init; }
        = ["ComponentBase", "LayoutComponentBase"];

    public IReadOnlyCollection<string> ImmutabilityExemptSuffixes { get; init; } = new[]
    {
        "Dto", "Entity", "Model", "Request", "Response", "Command"
    };
    public IReadOnlyCollection<string> ImmutabilityExemptPatterns { get; init; } = Array.Empty<string>();
    public bool AllowedEmptyReads { get; init; } = false;
    public IReadOnlyCollection<string> SealedClassExemptSuffixes { get; init; } = ["Base", "Foundation", "Host"];
    public IReadOnlyCollection<string> ImmutabilityExemptBaseTypes { get; init; } =
    [
        "ComponentBase",
        "LayoutComponentBase",
        "ObservableObject",
        "ObservableRecipient",
        "BackgroundService",
        "AuthenticationStateProvider",
        "INotifyPropertyChanged"
    ];
    public bool ImmutabilityAllowPrivateBackingFields { get; init; } = true;
    public bool EnablePerformanceProfiling { get; init; } = true;

    public bool BanAsyncVoid { get; init; } = true;

    public bool AsyncVoidAllowEventHandlers { get; init; } = true;

    public bool BanBlockingTaskAccess { get; init; } = true;

    public bool BanBlockingTaskAccessAllowInMain { get; init; } = true;

    public bool BanBlockingTaskAccessAllowInTests { get; init; } = false;

    public GlobalConfig Apply(GlobalConfigOverride? @override)
    {
        if (@override == null) return this;
        var o = @override;
        return this with
        {
            EnforceSealedClasses                        = o.EnforceSealedClasses                        ?? EnforceSealedClasses,
            AllowUnsealedPartialClasses                 = o.AllowUnsealedPartialClasses                 ?? AllowUnsealedPartialClasses,
            AllowDynamic                                = o.AllowDynamic                                ?? AllowDynamic,
            AllowOutParameters                          = o.AllowOutParameters                          ?? AllowOutParameters,
            AllowTryPatternOutParameters                = o.AllowTryPatternOutParameters                ?? AllowTryPatternOutParameters,
            AllowOutParametersInPrivateMethods          = o.AllowOutParametersInPrivateMethods          ?? AllowOutParametersInPrivateMethods,
            SealedClassExemptSuffixes                   = o.SealedClassExemptSuffixes                   ?? SealedClassExemptSuffixes,

            EnforcePascalCase                           = o.EnforcePascalCase                           ?? EnforcePascalCase,
            EnforceAsciiIdentifiers                     = o.EnforceAsciiIdentifiers                     ?? EnforceAsciiIdentifiers,
            EnforceSemanticNaming                       = o.EnforceSemanticNaming                       ?? EnforceSemanticNaming,
            SemanticNamingExemptMethodNames             = o.SemanticNamingExemptMethodNames             ?? SemanticNamingExemptMethodNames,
            SemanticNamingAllowSubstringOfMethodName    = o.SemanticNamingAllowSubstringOfMethodName    ?? SemanticNamingAllowSubstringOfMethodName,
            EnforceNullableEnable                       = o.EnforceNullableEnable                       ?? EnforceNullableEnable,
            EnforceXmlDocumentation                     = o.EnforceXmlDocumentation                     ?? EnforceXmlDocumentation,
            EnforceMinimalApiAsParameters               = o.EnforceMinimalApiAsParameters               ?? EnforceMinimalApiAsParameters,
            EnableTestSentinel                          = o.EnableTestSentinel                          ?? EnableTestSentinel,

            EnforceNoSilentCatch                        = o.EnforceNoSilentCatch                        ?? EnforceNoSilentCatch,
            AllowCancellationShutdownCatch              = o.AllowCancellationShutdownCatch              ?? AllowCancellationShutdownCatch,
            AllowedSilentCatchExceptionTypes            = o.AllowedSilentCatchExceptionTypes            ?? AllowedSilentCatchExceptionTypes,
            EnforceResultPatternOverExceptions          = o.EnforceResultPatternOverExceptions          ?? EnforceResultPatternOverExceptions,
            ResultPatternAllowThrowInNamespaceSuffixes  = o.ResultPatternAllowThrowInNamespaceSuffixes  ?? ResultPatternAllowThrowInNamespaceSuffixes,
            ResultPatternAllowCatchRethrow              = o.ResultPatternAllowCatchRethrow              ?? ResultPatternAllowCatchRethrow,
            AllowedExceptions                           = o.AllowedExceptions                           ?? AllowedExceptions,

            EnforceValueObjectContracts                 = o.EnforceValueObjectContracts                 ?? EnforceValueObjectContracts,
            EnforceExplicitStateImmutability            = o.EnforceExplicitStateImmutability            ?? EnforceExplicitStateImmutability,
            ImmutabilityExemptSuffixes                  = o.ImmutabilityExemptSuffixes                  ?? ImmutabilityExemptSuffixes,
            ImmutabilityExemptPatterns                  = o.ImmutabilityExemptPatterns                  ?? ImmutabilityExemptPatterns,
            ImmutabilityExemptBaseTypes                 = o.ImmutabilityExemptBaseTypes                 ?? ImmutabilityExemptBaseTypes,
            ImmutabilityAllowPrivateBackingFields       = o.ImmutabilityAllowPrivateBackingFields       ?? ImmutabilityAllowPrivateBackingFields,
            AllowedEmptyReads                           = o.AllowedEmptyReads                           ?? AllowedEmptyReads,

            EnforceNamespaceDirectoryMapping            = o.EnforceNamespaceDirectoryMapping            ?? EnforceNamespaceDirectoryMapping,
            NamespaceDirectoryMappingMode               = o.NamespaceDirectoryMappingMode               ?? NamespaceDirectoryMappingMode,
            NamespaceDirectoryMappingIgnorePathSegments = o.NamespaceDirectoryMappingIgnorePathSegments ?? NamespaceDirectoryMappingIgnorePathSegments,
            NamespaceDirectoryMappingRequiredTrailingSegments = o.NamespaceDirectoryMappingRequiredTrailingSegments ?? NamespaceDirectoryMappingRequiredTrailingSegments,
            DetectAndBanPhantomDependencies             = o.DetectAndBanPhantomDependencies             ?? DetectAndBanPhantomDependencies,
            PreventContextDependentOverloads            = o.PreventContextDependentOverloads            ?? PreventContextDependentOverloads,
            BanPublicNestedTypes                        = o.BanPublicNestedTypes                        ?? BanPublicNestedTypes,
            BanPublicNestedTypesAllowPrivate            = o.BanPublicNestedTypesAllowPrivate            ?? BanPublicNestedTypesAllowPrivate,
            EnablePerformanceProfiling                  = o.EnablePerformanceProfiling                  ?? EnablePerformanceProfiling,
            BanAsyncVoid                                = o.BanAsyncVoid                                ?? BanAsyncVoid,
            AsyncVoidAllowEventHandlers                 = o.AsyncVoidAllowEventHandlers                 ?? AsyncVoidAllowEventHandlers,
            BanBlockingTaskAccess                       = o.BanBlockingTaskAccess                       ?? BanBlockingTaskAccess,
            BanBlockingTaskAccessAllowInMain            = o.BanBlockingTaskAccessAllowInMain            ?? BanBlockingTaskAccessAllowInMain,
            BanBlockingTaskAccessAllowInTests           = o.BanBlockingTaskAccessAllowInTests           ?? BanBlockingTaskAccessAllowInTests,

            AvoidExcessiveMiddleMen                     = o.AvoidExcessiveMiddleMen                     ?? AvoidExcessiveMiddleMen,
            MaxMiddleManForwardingRatio                 = o.MaxMiddleManForwardingRatio                 ?? MaxMiddleManForwardingRatio,
            MiddleManMinMemberCount                     = o.MiddleManMinMemberCount                     ?? MiddleManMinMemberCount,
            MiddleManExemptSuffixes                     = o.MiddleManExemptSuffixes                     ?? MiddleManExemptSuffixes,
            MiddleManExemptBaseTypes                    = o.MiddleManExemptBaseTypes                    ?? MiddleManExemptBaseTypes,
            MiddleManIncludePrivateMembers              = o.MiddleManIncludePrivateMembers              ?? MiddleManIncludePrivateMembers,
        };
    }
}
