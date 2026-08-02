namespace AiNetLinter.Baseline;

public static class BaselineWriter
{
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
