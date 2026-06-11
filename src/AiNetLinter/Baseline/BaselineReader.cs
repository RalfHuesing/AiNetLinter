namespace AiNetLinter.Baseline;

/// <summary>
/// Liest Baseline-Dateien vom Dateisystem.
/// </summary>
public static class BaselineReader
{
    /// <summary>
    /// Lädt und validiert eine Baseline-JSON-Datei.
    /// </summary>
    public static BaselineFile Read(string baselinePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baselinePath);

        if (!File.Exists(baselinePath))
        {
            throw new FileNotFoundException($"Baseline-Datei nicht gefunden: {baselinePath}");
        }

        var json = File.ReadAllText(baselinePath);
        return BaselineJsonSerializer.Deserialize(json);
    }
}
