#nullable enable
namespace AiNetLinter.Configuration;

public sealed record RuleMetadataEntry
{
    public string Severity { get; init; } = "error";
    public string Intent { get; init; } = "general";
}

public sealed record ProjectOverrideEntry
{
    public GlobalConfigOverride? Global { get; init; }

    public MetricsConfigOverride? Metrics { get; init; }

    public TestSentinelConfigOverride? TestSentinel { get; init; }

    public UiSeparationConfigOverride? UiSeparation { get; init; }

    public WebConfigOverride? Web { get; init; }
}
