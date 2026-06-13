#nullable enable

using System;
using System.IO;
using System.Text.Json;

namespace AiNetLinter.Configuration;

/// <summary>
/// Hilfsklasse zum Laden und Deserialisieren der Linter-Konfiguration.
/// </summary>
public static class LinterConfigLoader
{
    /// <summary>
    /// Versucht, die Konfiguration aus der angegebenen Datei zu laden.
    /// </summary>
    /// <param name="configPath">Der Pfad zur Konfigurationsdatei.</param>
    /// <param name="isRequired">Gibt an, ob das Fehlen der Datei als Fehler gewertet werden soll.</param>
    /// <returns>Die geladene Konfiguration oder null bei Fehlern.</returns>
    public static LinterConfig? TryLoadConfig(string? configPath, bool isRequired)
    {
        if (string.IsNullOrWhiteSpace(configPath))
        {
            if (isRequired)
            {
                Console.Error.WriteLine("[ERROR]: --config ist erforderlich fuer den Audit-Lauf.");
            }
            return null;
        }

        if (!File.Exists(configPath))
        {
            Console.Error.WriteLine($"[ERROR]: Die Konfigurationsdatei wurde nicht gefunden: {configPath}");
            return null;
        }

        var config = LoadConfig(configPath);
        if (config == null)
        {
            Console.Error.WriteLine("[ERROR]: Die Konfigurationsdatei konnte nicht deserialisiert werden.");
        }

        return config;
    }

    private static LinterConfig? LoadConfig(string configPath)
    {
        try
        {
            var content = File.ReadAllText(configPath);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var config = JsonSerializer.Deserialize<LinterConfig>(content, options);
            return config is null ? null : LinterConfigNormalizer.Normalize(config);
        }
        catch (InvalidOperationException ex)
        {
            Console.Error.WriteLine($"[ERROR]: Ungueltige Konfiguration in '{configPath}': {ex.Message}");
            return null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load config: {ex.Message}");
            return null;
        }
    }
}
