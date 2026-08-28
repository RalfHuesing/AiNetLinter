#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace AiNetLinter.Configuration;

internal static class ExternalSourceConfigurationLoader
{
    internal const string AppSettingsFileName = "appsettings.json";

    private const string ExternalSourcesSectionName = "ExternalSources";
    private const string MappingsPathName = "MappingsPath";

    internal static ExternalSourceConfigurationLoadResult Load() =>
        Load(Path.Combine(AppContext.BaseDirectory, AppSettingsFileName));

    internal static ExternalSourceConfigurationLoadResult Load(string? settingsPath)
    {
        var canonicalPath = CanonicalizeSettingsPath(settingsPath);
        if (canonicalPath is null)
        {
            return ExternalSourceConfigurationLoadResult.Failure(
                [Diagnostic(
                    ExternalSourceConfigurationDiagnosticCodes.SettingsPathInvalid,
                    "Der Pfad der appsettings.json konnte nicht kanonisiert werden.",
                    settingsPath ?? AppSettingsFileName,
                    "$")]);
        }

        if (!File.Exists(canonicalPath))
        {
            return ExternalSourceConfigurationLoadResult.Success(ExternalSourceConfiguration.Empty);
        }

        return LoadSettingsFile(canonicalPath);
    }

    private static ExternalSourceConfigurationLoadResult LoadSettingsFile(string settingsPath)
    {
        if (!TryRead(settingsPath, isSettingsFile: true, out var json, out var readDiagnostic))
        {
            return ExternalSourceConfigurationLoadResult.Failure([readDiagnostic!]);
        }

        try
        {
            using var document = JsonDocument.Parse(json!);
            return ParseSettings(document.RootElement, settingsPath);
        }
        catch (JsonException exception)
        {
            return ExternalSourceConfigurationLoadResult.Failure(
                [Diagnostic(
                    ExternalSourceConfigurationDiagnosticCodes.SettingsJsonInvalid,
                    $"appsettings.json ist kein gültiges JSON: {exception.Message}",
                    settingsPath,
                    "$")]);
        }
    }

    private static ExternalSourceConfigurationLoadResult ParseSettings(JsonElement root, string settingsPath)
    {
        if (root.ValueKind is not JsonValueKind.Object)
        {
            return ExternalSourceConfigurationLoadResult.Failure(
                [Diagnostic(
                    ExternalSourceConfigurationDiagnosticCodes.SettingsRootInvalid,
                    "appsettings.json muss ein JSON-Objekt sein.",
                    settingsPath,
                    "$")]);
        }

        var sectionDiagnostics = new List<ExternalSourceConfigurationDiagnostic>();
        var sectionQuery = new ExternalSourceJsonPropertyQuery(
            root,
            ExternalSourcesSectionName,
            settingsPath,
            "$");
        if (!ExternalSourceJsonValidation.TryGetUniqueProperty(sectionQuery, sectionDiagnostics, out var section))
        {
            return sectionDiagnostics.Count == 0
                ? ExternalSourceConfigurationLoadResult.Success(ExternalSourceConfiguration.Empty)
                : ExternalSourceConfigurationLoadResult.Failure(sectionDiagnostics);
        }

        if (section.ValueKind is not JsonValueKind.Object)
        {
            return ExternalSourceConfigurationLoadResult.Failure(
                [Diagnostic(
                    ExternalSourceConfigurationDiagnosticCodes.ExternalSourcesSectionInvalid,
                    $"Der Abschnitt '{ExternalSourcesSectionName}' muss ein JSON-Objekt sein.",
                    settingsPath,
                    "$.ExternalSources")]);
        }

        return LoadMappingsFromSection(section, settingsPath);
    }

    private static ExternalSourceConfigurationLoadResult LoadMappingsFromSection(
        JsonElement section,
        string settingsPath)
    {
        var fieldDiagnostics = ExternalSourceJsonValidation.ValidateKnownFields(
            section,
            settingsPath,
            "$.ExternalSources",
            MappingsPathName);
        if (fieldDiagnostics.Count > 0)
        {
            return ExternalSourceConfigurationLoadResult.Failure(fieldDiagnostics);
        }

        var propertyDiagnostics = new List<ExternalSourceConfigurationDiagnostic>();
        var propertyQuery = new ExternalSourceJsonPropertyQuery(
            section,
            MappingsPathName,
            settingsPath,
            "$.ExternalSources");
        if (!ExternalSourceJsonValidation.TryGetUniqueProperty(propertyQuery, propertyDiagnostics, out var pathElement))
        {
            return propertyDiagnostics.Count > 0
                ? ExternalSourceConfigurationLoadResult.Failure(propertyDiagnostics)
                : ExternalSourceConfigurationLoadResult.Failure(
                    [Diagnostic(
                        ExternalSourceConfigurationDiagnosticCodes.MappingsPathMissing,
                        $"'{ExternalSourcesSectionName}:{MappingsPathName}' ist erforderlich, wenn der Abschnitt vorhanden ist.",
                        settingsPath,
                        "$.ExternalSources")]);
        }

        if (pathElement.ValueKind is not JsonValueKind.String
            || string.IsNullOrWhiteSpace(pathElement.GetString()))
        {
            return ExternalSourceConfigurationLoadResult.Failure(
                [Diagnostic(
                    ExternalSourceConfigurationDiagnosticCodes.MappingsPathInvalid,
                    $"'{ExternalSourcesSectionName}:{MappingsPathName}' muss ein nichtleerer String sein.",
                    settingsPath,
                    "$.ExternalSources.MappingsPath")]);
        }

        var mappingsPath = ResolveMappingsPath(settingsPath, pathElement.GetString()!.Trim());
        if (mappingsPath is null)
        {
            return ExternalSourceConfigurationLoadResult.Failure(
                [Diagnostic(
                    ExternalSourceConfigurationDiagnosticCodes.MappingsPathInvalid,
                    $"'{ExternalSourcesSectionName}:{MappingsPathName}' ist kein gültiger Dateipfad.",
                    settingsPath,
                    "$.ExternalSources.MappingsPath")]);
        }

        return LoadMappingsFile(mappingsPath);
    }

    private static ExternalSourceConfigurationLoadResult LoadMappingsFile(string mappingsPath)
    {
        if (!File.Exists(mappingsPath))
        {
            return ExternalSourceConfigurationLoadResult.Failure(
                [Diagnostic(
                    ExternalSourceConfigurationDiagnosticCodes.MappingsPathInvalid,
                    $"Die externe Mapping-Datei wurde nicht gefunden: '{mappingsPath}'.",
                    mappingsPath,
                    "$")]);
        }

        if (!TryRead(mappingsPath, isSettingsFile: false, out var json, out var readDiagnostic))
        {
            return ExternalSourceConfigurationLoadResult.Failure([readDiagnostic!]);
        }

        try
        {
            using var document = JsonDocument.Parse(json!);
            return ExternalSourceMappingValidator.Validate(document.RootElement, mappingsPath);
        }
        catch (JsonException exception)
        {
            return ExternalSourceConfigurationLoadResult.Failure(
                [Diagnostic(
                    ExternalSourceConfigurationDiagnosticCodes.MappingsJsonInvalid,
                    $"Die Mapping-Datei ist kein gültiges JSON: {exception.Message}",
                    mappingsPath,
                    "$")]);
        }
    }

    private static string? CanonicalizeSettingsPath(string? settingsPath)
    {
        try
        {
            return Path.GetFullPath(
                string.IsNullOrWhiteSpace(settingsPath)
                    ? Path.Combine(AppContext.BaseDirectory, AppSettingsFileName)
                    : settingsPath);
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or NotSupportedException)
        {
            return null;
        }
    }

    private static string? ResolveMappingsPath(string settingsPath, string value)
    {
        try
        {
            if (Path.IsPathRooted(value))
            {
                return Path.GetFullPath(value);
            }

            var settingsDirectory = Path.GetDirectoryName(settingsPath) ?? AppContext.BaseDirectory;
            return Path.GetFullPath(Path.Combine(settingsDirectory, value));
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or NotSupportedException)
        {
            return null;
        }
    }

    private static bool TryRead(
        string path,
        bool isSettingsFile,
        out string? content,
        out ExternalSourceConfigurationDiagnostic? diagnostic)
    {
        try
        {
            content = File.ReadAllText(path);
            diagnostic = null;
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            content = null;
            diagnostic = Diagnostic(
                isSettingsFile
                    ? ExternalSourceConfigurationDiagnosticCodes.SettingsReadFailed
                    : ExternalSourceConfigurationDiagnosticCodes.MappingsReadFailed,
                $"Die Konfigurationsdatei konnte nicht gelesen werden: {exception.Message}",
                path,
                "$");
            return false;
        }
    }

    private static ExternalSourceConfigurationDiagnostic Diagnostic(
        string code,
        string message,
        string sourcePath,
        string jsonPath) =>
        new(code, message, "error", $"{sourcePath} ({jsonPath})");
}
