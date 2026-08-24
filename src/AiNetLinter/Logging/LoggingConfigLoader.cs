#nullable enable

using System;
using System.IO;
using System.Text.Json;

namespace AiNetLinter.Logging;

/// <summary>
/// Laedt die optionale appsettings.json (fehlend = Built-in-Defaults) und
/// validiert sie scharf: Defekte Dateien oder unbekannte Schluessel sind ein
/// harter Fehler mit klarer Meldung, kein stilles Zurueckfallen auf Defaults.
/// </summary>
internal static class LoggingConfigLoader
{
    public const string FileName = "appsettings.json";
    private const string SectionName = "Logging";

    internal static LoggingConfig Load() => Load(AppContext.BaseDirectory);

    internal static LoggingConfig Load(string baseDirectory)
    {
        var path = Path.Combine(baseDirectory, FileName);
        if (!File.Exists(path))
        {
            return LoggingConfig.CreateDefault();
        }

        string json;
        try
        {
            json = File.ReadAllText(path);
        }
        catch (Exception exception)
        {
            throw new InvalidDataException(
                $"[CONFIG]: {FileName} konnte nicht gelesen werden ({path}): {exception.Message}");
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                $"[CONFIG]: {FileName} ist kein gueltiges JSON ({path}): {exception.Message}");
        }

        using (document)
        {
            return Map(document.RootElement, path);
        }
    }

    private static LoggingConfig Map(JsonElement root, string path)
    {
        if (root.ValueKind is not JsonValueKind.Object)
        {
            throw new InvalidDataException(
                $"[CONFIG]: {FileName} muss ein JSON-Objekt sein ({path}).");
        }

        if (!root.TryGetProperty(SectionName, out var section))
        {
            return LoggingConfig.CreateDefault();
        }

        if (section.ValueKind is not JsonValueKind.Object)
        {
            throw new InvalidDataException(
                $"[CONFIG]: Abschnitt '{SectionName}' in {FileName} muss ein JSON-Objekt sein ({path}).");
        }

        ValidateKeys(section, path);

        var minimumLevel = ReadString(section, "MinimumLevel", LoggingConfig.DefaultMinimumLevel, path);
        var directory = ReadString(section, "Directory", LoggingConfig.DefaultDirectoryName, path);
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new InvalidDataException(
                $"[CONFIG]: '{SectionName}:Directory' darf nicht leer sein ({path}).");
        }

        var retainedFileCount = LoggingConfig.DefaultRetainedFileCount;
        if (section.TryGetProperty("RetainedFileCount", out var retainedElement))
        {
            retainedFileCount = ReadInt(retainedElement, 1, 365, path);
        }

        ValidateLevel(minimumLevel, path);
        return new LoggingConfig(minimumLevel, directory, retainedFileCount);
    }

    private static void ValidateKeys(JsonElement section, string path)
    {
        foreach (var property in section.EnumerateObject())
        {
            if (property.Name is not ("MinimumLevel" or "Directory" or "RetainedFileCount"))
            {
                throw new InvalidDataException(
                    $"[CONFIG]: Unbekannter Schluessel '{SectionName}:{property.Name}' ({path}). Gueltige Schluessel: MinimumLevel, Directory, RetainedFileCount.");
            }
        }
    }

    private static void ValidateLevel(string value, string path)
    {
        foreach (var candidate in LoggingConfig.AllowedLevels)
        {
            if (string.Equals(candidate, value, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        throw new InvalidDataException(
            $"[CONFIG]: Ungueltiges '{SectionName}:MinimumLevel' \"{value}\" ({path}). Erlaubt: {string.Join(", ", LoggingConfig.AllowedLevels)}.");
    }

    private static string ReadString(JsonElement section, string name, string fallback, string path)
    {
        if (!section.TryGetProperty(name, out var element))
        {
            return fallback;
        }

        if (element.ValueKind is not JsonValueKind.String)
        {
            throw new InvalidDataException(
                $"[CONFIG]: '{SectionName}:{name}' muss eine Zeichenkette sein ({path}).");
        }

        return element.GetString() ?? fallback;
    }

    private static int ReadInt(JsonElement element, int minimum, int maximum, string path)
    {
        if (element.ValueKind is not JsonValueKind.Number || !element.TryGetInt32(out var value))
        {
            throw new InvalidDataException(
                $"[CONFIG]: '{SectionName}:RetainedFileCount' muss eine ganze Zahl sein ({path}).");
        }

        if (value < minimum || value > maximum)
        {
            throw new InvalidDataException(
                $"[CONFIG]: '{SectionName}:RetainedFileCount' muss zwischen {minimum} und {maximum} liegen ({path}).");
        }

        return value;
    }
}
