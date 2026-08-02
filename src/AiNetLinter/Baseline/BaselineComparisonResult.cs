namespace AiNetLinter.Baseline;

public sealed record BaselineComparisonResult
{
    public required IReadOnlySet<string> ChangedFiles { get; init; }

    public required IReadOnlySet<string> RemovedFiles { get; init; }

    public required bool HasAnyChange { get; init; }
}
