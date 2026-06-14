#nullable enable
using System.Collections.Generic;

namespace AiNetLinter.Cache;

internal sealed record AnalysisCacheFile
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;
    public Dictionary<string, AnalysisCacheEntry> Files { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}
