#nullable enable
using System.Collections.Generic;

namespace AiNetLinter.Cache;

internal sealed record AnalysisCacheEntry
{
    public required string RelativePath { get; init; }
    public required string Checksum { get; init; }
    public IReadOnlyList<RuleViolationDto> Violations { get; init; } = [];
    public IReadOnlyList<ClassInfoDto> Classes { get; init; } = [];
    public IReadOnlyList<PartialPartDto> PartialParts { get; init; } = [];
    public TestSignalsDto TestSignals { get; init; } = new();
}

internal sealed record RuleViolationDto(
    string FilePath, int LineNumber, string RuleName, string Details, string Guidance);

// ainetlinter-disable MaxConstructorDependencies
// Dieses Record dient als Datentransfer-Klasse (DTO) fuer Klassen-Metadaten und hat keine logischen Abhaengigkeiten.
internal sealed record ClassInfoDto(
    string Name, string FilePath, int LineNumber,
    int MaxCognitiveComplexity, int InheritanceDepth, int AiContextFootprint,
    IReadOnlyList<FootprintDetailDto> AiContextFootprintDetails,
    bool HasTestMethods, bool IsPartial, bool IsStatic,
    IReadOnlyList<string> BaseTypeNames, string? ProjectName);

internal sealed record FootprintDetailDto(string Name, int Lines);

internal sealed record PartialPartDto(
    string TypeName, string FilePath, int LineNumber, int FileLineCount);

internal sealed record TestSignalsDto
{
    public IReadOnlyList<string> TestClassNames { get; init; } = [];
    public IReadOnlyList<string> ReferencedTypeNames { get; init; } = [];
    public IReadOnlyList<string> CoversComments { get; init; } = [];
}
