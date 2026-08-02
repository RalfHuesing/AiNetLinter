#nullable enable

namespace AiNetLinter.Models;

public sealed record ClassInfo
{
    public required string Name { get; init; }

    public required string FilePath { get; init; }

    public required int LineNumber { get; init; }

    public required int MaxCognitiveComplexity { get; init; }

    public required int InheritanceDepth { get; init; }

    public required int AIContextFootprint { get; init; }

    public IReadOnlyList<(string Name, int Lines)> AIContextFootprintDetails { get; init; } = Array.Empty<(string, int)>();

    public required bool HasTestMethods { get; init; }

    public bool IsPartial { get; init; }

    public bool IsStatic { get; init; }

    public IReadOnlyCollection<string> BaseTypeNames { get; init; } = Array.Empty<string>();

    public string? ProjectName { get; init; }
}
