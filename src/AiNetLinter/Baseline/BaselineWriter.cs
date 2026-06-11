namespace AiNetLinter.Baseline;

/// <summary>
/// Schreibt Baseline-Dateien auf das Dateisystem.
/// </summary>
public static class BaselineWriter
{
    /// <summary>
    /// Schreibt Checksummen als sortierte Baseline-JSON.
    /// </summary>
    public static void Write(string baselinePath, IReadOnlyDictionary<string, string> fileChecksums)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baselinePath);

        var baseline = new BaselineFile
        {
            Files = fileChecksums,
        };

        var json = BaselineJsonSerializer.Serialize(baseline);
        File.WriteAllText(baselinePath, json);
    }
}
