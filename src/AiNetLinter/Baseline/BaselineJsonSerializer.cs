using System.Text.Json;

namespace AiNetLinter.Baseline;

/// <summary>
/// JSON-Serialisierung für Baseline-Dateien.
/// </summary>
internal static class BaselineJsonSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public static BaselineFile Deserialize(string json)
    {
        var document = JsonSerializer.Deserialize<BaselineFileDto>(json, Options);
        if (document?.Files == null)
        {
            throw new InvalidDataException("Ungültiges Baseline-Format: 'files' fehlt oder ist leer.");
        }

        return new BaselineFile
        {
            Version = document.Version,
            Files = document.Files,
        };
    }

    public static string Serialize(BaselineFile baseline)
    {
        var sorted = baseline.Files
            .OrderBy(static pair => pair.Key, StringComparer.Ordinal)
            .ToDictionary(static pair => pair.Key, static pair => pair.Value);

        var dto = new BaselineFileDto
        {
            Version = baseline.Version,
            Files = sorted,
        };

        return JsonSerializer.Serialize(dto, Options);
    }

    private sealed class BaselineFileDto
    {
        public int Version { get; init; } = 1;

        public Dictionary<string, string>? Files { get; init; }
    }
}
