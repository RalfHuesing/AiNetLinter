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
                [ExternalSourceConfigurationDiagnostic.CreateError(
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
                [ExternalSourceConfigurationDiagnostic.CreateError(
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
                [ExternalSourceConfigurationDiagnostic.CreateError(
                    ExternalSourceConfigurationDiagnosticCodes.SettingsRootInvalid,
                    "appsettings.json muss ein JSON-Objekt sein.",
                    settingsPath,
                    "$")]);
        }

        var rootValidation = ExternalSourceJsonValidation.InspectObject(
            root,
            settingsPath,
            "$");
        var sectionProperty = rootValidation.GetProperty(ExternalSourcesSectionName);
        if (sectionProperty.Status is ExternalSourceJsonPropertyStatus.Missing)
        {
            return ExternalSourceConfigurationLoadResult.Success(ExternalSourceConfiguration.Empty);
        }

        if (sectionProperty.Status is ExternalSourceJsonPropertyStatus.Duplicate)
        {
            return ExternalSourceConfigurationLoadResult.Failure(rootValidation.Diagnostics);
        }

        var section = sectionProperty.Value;
        if (section.ValueKind is not JsonValueKind.Object)
        {
            return ExternalSourceConfigurationLoadResult.Failure(
                [ExternalSourceConfigurationDiagnostic.CreateError(
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
        var validation = ExternalSourceJsonValidation.InspectObject(
            section,
            settingsPath,
            "$.ExternalSources",
            [MappingsPathName]);
        if (!validation.Diagnostics.IsEmpty)
        {
            return ExternalSourceConfigurationLoadResult.Failure(validation.Diagnostics);
        }

        var pathProperty = validation.GetProperty(MappingsPathName);
        if (pathProperty.Status is ExternalSourceJsonPropertyStatus.Missing)
        {
            return ExternalSourceConfigurationLoadResult.Failure(
                [ExternalSourceConfigurationDiagnostic.CreateError(
                    ExternalSourceConfigurationDiagnosticCodes.MappingsPathMissing,
                    $"'{ExternalSourcesSectionName}:{MappingsPathName}' ist erforderlich, wenn der Abschnitt vorhanden ist.",
                    settingsPath,
                    "$.ExternalSources")]);
        }

        var pathElement = pathProperty.Value;
        if (pathElement.ValueKind is not JsonValueKind.String
            || string.IsNullOrWhiteSpace(pathElement.GetString()))
        {
            return ExternalSourceConfigurationLoadResult.Failure(
                [ExternalSourceConfigurationDiagnostic.CreateError(
                    ExternalSourceConfigurationDiagnosticCodes.MappingsPathInvalid,
                    $"'{ExternalSourcesSectionName}:{MappingsPathName}' muss ein nichtleerer String sein.",
                    settingsPath,
                    "$.ExternalSources.MappingsPath")]);
        }

        var mappingsPath = ResolveMappingsPath(settingsPath, pathElement.GetString()!.Trim());
        if (mappingsPath is null)
        {
            return ExternalSourceConfigurationLoadResult.Failure(
                [ExternalSourceConfigurationDiagnostic.CreateError(
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
                [ExternalSourceConfigurationDiagnostic.CreateError(
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
                [ExternalSourceConfigurationDiagnostic.CreateError(
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
            diagnostic = ExternalSourceConfigurationDiagnostic.CreateError(
                isSettingsFile
                    ? ExternalSourceConfigurationDiagnosticCodes.SettingsReadFailed
                    : ExternalSourceConfigurationDiagnosticCodes.MappingsReadFailed,
                $"Die Konfigurationsdatei konnte nicht gelesen werden: {exception.Message}",
                path,
                "$");
            return false;
        }
    }
}
