namespace AiNetLinter.Baseline;

public static class BaselineReader
{
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
