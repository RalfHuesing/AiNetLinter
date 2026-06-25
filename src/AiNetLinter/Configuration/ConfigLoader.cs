#nullable enable

using System;
using System.IO;
using System.Text;
using System.Text.Json;
using AiNetLinter.Output;

namespace AiNetLinter.Configuration;

/// <summary>
/// Hilfsklasse zum Laden und Deserialisieren der Linter-Konfiguration.
/// </summary>
public static class ConfigLoader
{
    /// <summary>
    /// Versucht, die Konfiguration aus der angegebenen Datei zu laden.
    /// </summary>
    public static Config? TryLoadConfig(string? configPath, bool isRequired)
    {
        if (string.IsNullOrWhiteSpace(configPath))
        {
            if (isRequired)
            {
                Console.Error.WriteLine(LinterErrorFormatter.Format(
                    LinterErrorCodes.ConfigRequired,
                    "--config ist erforderlich fuer den Audit-Lauf.",
                    hint: "Nutze --config <pfad> um rules.json anzugeben."));
            }
            return null;
        }

        if (!File.Exists(configPath))
        {
            Console.Error.WriteLine(LinterErrorFormatter.Format(
                LinterErrorCodes.ConfigNotFound,
                "Konfigurationsdatei nicht gefunden.",
                context: configPath,
                hint: "Pfad pruefen oder rules.json anlegen."));
            return null;
        }

        var config = LoadConfig(configPath);
        if (config == null)
        {
            Console.Error.WriteLine(LinterErrorFormatter.Format(
                LinterErrorCodes.ConfigInvalid,
                "Konfigurationsdatei konnte nicht deserialisiert werden.",
                context: configPath));
            return null;
        }

        ConfigSyncer.SyncIfNeeded(configPath, config);
        return config;
    }

    private static Config? LoadConfig(string configPath)
    {
        try
        {
            var content = File.ReadAllText(configPath);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var config = JsonSerializer.Deserialize<Config>(content, options);
            if (config?.Global?.ImmutabilityExemptSuffixes != null && config.Global.ImmutabilityExemptSuffixes.Count > 30)
            {
                Console.Error.WriteLine(LinterErrorFormatter.Format(
                    LinterErrorCodes.ConfigSmell,
                    "Zu breite Ausnahme: mehr als 30 ImmutabilityExemptSuffixes.",
                    context: configPath,
                    hint: "Erwaege Wildcard-Muster zu nutzen."));
            }
            return config is null ? null : ConfigNormalizer.Normalize(config);
        }
        catch (InvalidOperationException ex)
        {
            Console.Error.WriteLine(LinterErrorFormatter.Format(
                LinterErrorCodes.ConfigInvalid,
                ex.Message,
                context: configPath));
            return null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load config: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Liest den Inhalt der Config-Datei für die Cache-Invalidierung.
    /// Gibt null zurück wenn der Pfad leer oder die Datei nicht vorhanden ist.
    /// </summary>
    public static string? LoadRulesJsonContent(string? configPath)
    {
        if (string.IsNullOrEmpty(configPath) || !File.Exists(configPath))
            return null;

        return File.ReadAllText(configPath, Encoding.UTF8);
    }
}
