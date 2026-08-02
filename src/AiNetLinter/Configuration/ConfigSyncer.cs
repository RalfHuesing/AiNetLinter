#nullable enable

using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AiNetLinter.Configuration;

public static class ConfigSyncer
{
    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static bool SyncIfNeeded(string configPath, Config loadedConfig)
    {
        try
        {
            var originalContent = File.ReadAllText(configPath);
            var syncedContent = Serialize(loadedConfig);

            if (Normalize(originalContent) == Normalize(syncedContent))
                return false;

            File.WriteAllText(configPath, syncedContent);
            Console.WriteLine($"[INFO]: rules.json synchronisiert (neue/entfernte Optionen): {configPath}");
            return true;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[WARNING]: rules.json-Sync fehlgeschlagen: {ex.Message}");
            return false;
        }
    }

    internal static string Serialize(Config config) =>
        JsonSerializer.Serialize(config, WriteOptions);

    private static string Normalize(string json) =>
        json.Replace("\r\n", "\n").TrimEnd();
}
