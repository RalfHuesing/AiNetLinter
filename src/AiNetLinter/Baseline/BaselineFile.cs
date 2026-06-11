namespace AiNetLinter.Baseline;

/// <summary>
/// Persistiertes Baseline-Dokument mit Datei-Checksummen.
/// </summary>
public sealed record BaselineFile
{
    public int Version { get; init; } = 1;

    public required IReadOnlyDictionary<string, string> Files { get; init; }
}
