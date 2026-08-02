#nullable enable
namespace AiNetLinter.Configuration;

public sealed record MetricsConfig
{
    public int MaxLineCount { get; init; } = 700;
    public int MaxMethodParameterCount { get; init; } = 4;

    /// <summary>
    /// Maximale Parameteranzahl pro Methode in Testdateien.
    /// 0 = gleicher Grenzwert wie <see cref="MaxMethodParameterCount"/>.
    /// Empfehlung: 6–8, da Test-Hilfsmethoden (Arrange-Helfer, Browser-Asserts) naturgemäß breiter sind.
    /// </summary>
    public int MaxMethodParameterCountInTestFiles { get; init; } = 0;

    public IReadOnlyCollection<string> MethodParameterCountIgnoreTypeNames { get; init; }
        = ["CancellationToken"];

    public IReadOnlyCollection<string> MethodParameterCountIgnoreTypePrefixes { get; init; }
        = Array.Empty<string>();

    public bool MaxMethodParameterCountAllowPrivate { get; init; } = false;

    public int MaxMethodParameterCountForNonPublic { get; init; } = 6;

    public int MaxMethodLineCount { get; init; } = 60;
    public int MaxCyclomaticComplexity { get; init; } = 12;
    public int MaxCognitiveComplexity { get; init; } = 15;
    public int MaxInheritanceDepth { get; init; } = 2;
    public int MinCognitiveComplexityForTest { get; init; } = 3;
    public bool AggregatePartialClassLineCount { get; init; } = false;
    public int MaxMethodOverloads { get; init; } = 3;
    public int MaxConstructorDependencies { get; init; } = 5;
    public int MaxDirectoryDepth { get; init; } = 4;

    public IReadOnlyCollection<string> InheritanceDepthFrameworkPrefixes { get; init; }
        = Array.Empty<string>();

    public IReadOnlyCollection<string> ConstructorDependencyIgnoreTypePrefixes { get; init; }
        = [
            "ILogger",
            "IOptions",
            "IOptionsSnapshot",
            "IOptionsMonitor",
            "IHostEnvironment",
            "IWebHostEnvironment",
            "IConfiguration",
            "IServiceProvider",
            "IHttpContextAccessor"
        ];

    public IReadOnlyCollection<string> ConstructorDependencyExemptClassSuffixes { get; init; }
        = ["Exception"];

    public int MaxAIContextFootprint { get; init; } = 5000;

    public IReadOnlyCollection<string> FootprintIgnoreNamespacePrefixes { get; init; }
        = Array.Empty<string>();

    public IReadOnlyCollection<string> FootprintIgnoreTypeNames { get; init; }
        = Array.Empty<string>();

    public int ComplexityNearMissTolerance { get; init; } = 1;

    public bool ExcludeSwitchDispatcherCases { get; init; } = true;

    /// <summary>
    /// Max. Code-Zeilen pro Case/If-Zweig damit er als Dispatcher-Zweig gilt.
    /// </summary>
    public int SwitchDispatcherMaxCaseBodyLines { get; init; } = 3;

    public bool ExcludeNullCoalescingInitializerComplexity { get; init; } = true;

    /// <summary>
    /// Maximaler Anteil an nicht-null-coalescing-Ästen damit eine Methode
    /// als NullCoalescingInitializer gilt (0.0–1.0).
    /// Standard: 0.0 — alle Branches müssen ?? oder ?: sein.
    /// </summary>
    public double NullCoalescingInitializerMaxNonCoalescingRatio { get; init; } = 0.0;

    public int MaxSwitchArms { get; init; } = 10;

    public bool MaxSwitchArmsExcludeDispatcher { get; init; } = true;

    public IReadOnlyCollection<string> MaxSwitchArmsExemptTypes { get; init; }
        = Array.Empty<string>();

    public int MaxBoolParameterCount { get; init; } = 1;

    public bool MaxBoolParameterCountAllowPrivate { get; init; } = true;

    public IReadOnlyCollection<string> MaxBoolParameterCountExemptMethodPrefixes { get; init; }
        = ["Try"];

    public int MaxDirectoryChildren { get; init; } = 0;

    public IReadOnlyCollection<string> MaxDirectoryChildrenExemptNames { get; init; }
        = ["Migrations", "Generated", "wwwroot", "obj", "bin", ".git"];

    public int MaxPartialClassFiles { get; init; } = 2;

    public IReadOnlyCollection<string> MaxPartialClassFilesExemptTypes { get; init; }
        = Array.Empty<string>();

    public int MaxPublicMembersPerType { get; init; } = 15;

    public IReadOnlyCollection<string> MaxPublicMembersPerTypeExemptSuffixes { get; init; }
        = ["Extensions", "Mapper", "Constants", "Config", "ConfigOverride", "Args"];

    public int MaxLinqChainLength { get; init; } = 0;

    /// <summary>
    /// LINQ-Methoden-Namen, die als Teil einer LINQ-Kette gewertet werden.
    /// Nicht-LINQ-Chains (z. B. Builder-Chains) werden damit von der Prüfung ausgeschlossen.
    /// Konfigurierbar für projektspezifische LINQ-ähnliche APIs (z. B. EF Core Fluent API).
    /// </summary>
    public IReadOnlyCollection<string> LinqMethodNames { get; init; } =
    [
        "Where", "Select", "SelectMany",
        "GroupBy", "GroupJoin", "Join",
        "OrderBy", "OrderByDescending", "ThenBy", "ThenByDescending",
        "Take", "TakeWhile", "Skip", "SkipWhile",
        "First", "FirstOrDefault", "Last", "LastOrDefault",
        "Single", "SingleOrDefault",
        "Count", "LongCount", "Any", "All",
        "Distinct", "DistinctBy", "Union", "UnionBy",
        "Intersect", "IntersectBy", "Except", "ExceptBy",
        "Aggregate", "Sum", "Min", "Max", "Average", "MinBy", "MaxBy",
        "ToList", "ToArray", "ToDictionary", "ToHashSet", "ToLookup",
        "Cast", "OfType", "Append", "Prepend", "Reverse",
        "Zip", "Chunk", "Flatten"
    ];

    public IReadOnlyList<CompoundSuppression> CompoundSuppressions { get; init; } =
    [
        new CompoundSuppression
        {
            TargetRule = "MaxMethodLineCount",
            WhenAllOf =
            [
                new MetricCondition { Metric = "CyclomaticComplexity", AtMost = 3 },
                new MetricCondition { Metric = "CognitiveComplexity",  AtMost = 5 }
            ],
            RelaxedLimit = 150,
            Reason = "Initialisierungs- und Builder-Methoden sind semantisch flach. LOC bei CC≤3 ist nicht mit Fehleranfälligkeit korreliert (Palomba et al., 2018)."
        }
    ];

    public MetricsConfig Apply(MetricsConfigOverride? @override)
    {
        if (@override == null) return this;
        return ApplyLineLimits(@override)
            .ApplyComplexityLimits(@override)
            .ApplyDependencyLimits(@override)
            .ApplyDirectoryAndMemberLimits(@override);
    }

    private MetricsConfig ApplyLineLimits(MetricsConfigOverride o) => this with
    {
        MaxLineCount = o.MaxLineCount ?? MaxLineCount,
        MaxMethodLineCount = o.MaxMethodLineCount ?? MaxMethodLineCount,
        MaxMethodParameterCount = o.MaxMethodParameterCount ?? MaxMethodParameterCount,
        MaxMethodParameterCountInTestFiles = o.MaxMethodParameterCountInTestFiles ?? MaxMethodParameterCountInTestFiles,
        MethodParameterCountIgnoreTypeNames = o.MethodParameterCountIgnoreTypeNames ?? MethodParameterCountIgnoreTypeNames,
        MethodParameterCountIgnoreTypePrefixes = o.MethodParameterCountIgnoreTypePrefixes ?? MethodParameterCountIgnoreTypePrefixes,
        MaxMethodParameterCountAllowPrivate = o.MaxMethodParameterCountAllowPrivate ?? MaxMethodParameterCountAllowPrivate,
        MaxMethodParameterCountForNonPublic = o.MaxMethodParameterCountForNonPublic ?? MaxMethodParameterCountForNonPublic,
        MaxMethodOverloads = o.MaxMethodOverloads ?? MaxMethodOverloads,
        CompoundSuppressions = o.CompoundSuppressions ?? CompoundSuppressions,
        MaxLinqChainLength = o.MaxLinqChainLength ?? MaxLinqChainLength,
        LinqMethodNames = o.LinqMethodNames ?? LinqMethodNames,
    };

    private MetricsConfig ApplyComplexityLimits(MetricsConfigOverride o) => this with
    {
        MaxCyclomaticComplexity = o.MaxCyclomaticComplexity ?? MaxCyclomaticComplexity,
        MaxCognitiveComplexity = o.MaxCognitiveComplexity ?? MaxCognitiveComplexity,
        MinCognitiveComplexityForTest = o.MinCognitiveComplexityForTest ?? MinCognitiveComplexityForTest,
        AggregatePartialClassLineCount = o.AggregatePartialClassLineCount ?? AggregatePartialClassLineCount,
        ComplexityNearMissTolerance = o.ComplexityNearMissTolerance ?? ComplexityNearMissTolerance,
        ExcludeSwitchDispatcherCases = o.ExcludeSwitchDispatcherCases ?? ExcludeSwitchDispatcherCases,
        SwitchDispatcherMaxCaseBodyLines = o.SwitchDispatcherMaxCaseBodyLines ?? SwitchDispatcherMaxCaseBodyLines,
        ExcludeNullCoalescingInitializerComplexity = o.ExcludeNullCoalescingInitializerComplexity ?? ExcludeNullCoalescingInitializerComplexity,
        NullCoalescingInitializerMaxNonCoalescingRatio = o.NullCoalescingInitializerMaxNonCoalescingRatio ?? NullCoalescingInitializerMaxNonCoalescingRatio,
        MaxSwitchArms = o.MaxSwitchArms ?? MaxSwitchArms,
        MaxSwitchArmsExcludeDispatcher = o.MaxSwitchArmsExcludeDispatcher ?? MaxSwitchArmsExcludeDispatcher,
        MaxSwitchArmsExemptTypes = o.MaxSwitchArmsExemptTypes ?? MaxSwitchArmsExemptTypes,
    };

    private MetricsConfig ApplyDependencyLimits(MetricsConfigOverride o) => this with
    {
        MaxConstructorDependencies = o.MaxConstructorDependencies ?? MaxConstructorDependencies,
        ConstructorDependencyIgnoreTypePrefixes = o.ConstructorDependencyIgnoreTypePrefixes ?? ConstructorDependencyIgnoreTypePrefixes,
        ConstructorDependencyExemptClassSuffixes = o.ConstructorDependencyExemptClassSuffixes ?? ConstructorDependencyExemptClassSuffixes,
        MaxInheritanceDepth = o.MaxInheritanceDepth ?? MaxInheritanceDepth,
        InheritanceDepthFrameworkPrefixes = o.InheritanceDepthFrameworkPrefixes ?? InheritanceDepthFrameworkPrefixes,
        MaxAIContextFootprint = o.MaxAIContextFootprint ?? MaxAIContextFootprint,
        FootprintIgnoreNamespacePrefixes = o.FootprintIgnoreNamespacePrefixes ?? FootprintIgnoreNamespacePrefixes,
        FootprintIgnoreTypeNames = o.FootprintIgnoreTypeNames ?? FootprintIgnoreTypeNames,
    };

    private MetricsConfig ApplyDirectoryAndMemberLimits(MetricsConfigOverride o) => this with
    {
        MaxDirectoryDepth = o.MaxDirectoryDepth ?? MaxDirectoryDepth,
        MaxDirectoryChildren = o.MaxDirectoryChildren ?? MaxDirectoryChildren,
        MaxDirectoryChildrenExemptNames = o.MaxDirectoryChildrenExemptNames ?? MaxDirectoryChildrenExemptNames,
        MaxBoolParameterCount = o.MaxBoolParameterCount ?? MaxBoolParameterCount,
        MaxBoolParameterCountAllowPrivate = o.MaxBoolParameterCountAllowPrivate ?? MaxBoolParameterCountAllowPrivate,
        MaxBoolParameterCountExemptMethodPrefixes = o.MaxBoolParameterCountExemptMethodPrefixes ?? MaxBoolParameterCountExemptMethodPrefixes,
        MaxPartialClassFiles = o.MaxPartialClassFiles ?? MaxPartialClassFiles,
        MaxPartialClassFilesExemptTypes = o.MaxPartialClassFilesExemptTypes ?? MaxPartialClassFilesExemptTypes,
        MaxPublicMembersPerType = o.MaxPublicMembersPerType ?? MaxPublicMembersPerType,
        MaxPublicMembersPerTypeExemptSuffixes = o.MaxPublicMembersPerTypeExemptSuffixes ?? MaxPublicMembersPerTypeExemptSuffixes,
    };
}

public sealed record MetricCondition
{
    public required string Metric { get; init; }

    public int? AtMost { get; init; }

    public int? AtLeast { get; init; }
}

public sealed record CompoundSuppression
{
    public required string TargetRule { get; init; }

    /// <summary>
    /// Alle Bedingungen müssen erfüllt sein (AND-Verknüpfung) damit die Suppression aktiv wird.
    /// </summary>
    public required IReadOnlyList<MetricCondition> WhenAllOf { get; init; }

    public int? RelaxedLimit { get; init; }

    public string? SeverityOverride { get; init; }

    public string? Reason { get; init; }
}
