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
    private const string CacheRootName = "CacheRoot";
    private const string RefreshIntervalMinutesName = "RefreshIntervalMinutes";

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
            [MappingsPathName, CacheRootName, RefreshIntervalMinutesName]);
        if (!validation.Diagnostics.IsEmpty)
        {
            return ExternalSourceConfigurationLoadResult.Failure(validation.Diagnostics);
        }

        if (!TryReadCacheOptions(
                validation,
                settingsPath,
                out var cacheOptions,
                out var cacheDiagnostic))
        {
            return ExternalSourceConfigurationLoadResult.Failure([cacheDiagnostic!]);
        }

        if (!TryResolveMappingsPath(
                validation,
                settingsPath,
                out var mappingsPath,
                out var mappingsDiagnostic))
        {
            return ExternalSourceConfigurationLoadResult.Failure([mappingsDiagnostic!]);
        }

        var mappingsResult = LoadMappingsFile(mappingsPath!);
        if (!mappingsResult.Succeeded)
        {
            return mappingsResult;
        }

        return ExternalSourceConfigurationLoadResult.Success(
            new ExternalSourceConfiguration(
                mappingsResult.Configuration!.Mappings,
                cacheOptions));
    }

    private static bool TryResolveMappingsPath(
        ExternalSourceJsonObjectValidation validation,
        string settingsPath,
        out string? mappingsPath,
        out ExternalSourceConfigurationDiagnostic? diagnostic)
    {
        mappingsPath = null;
        diagnostic = null;
        var pathProperty = validation.GetProperty(MappingsPathName);
        if (pathProperty.Status is ExternalSourceJsonPropertyStatus.Missing)
        {
            diagnostic = ExternalSourceConfigurationDiagnostic.CreateError(
                ExternalSourceConfigurationDiagnosticCodes.MappingsPathMissing,
                $"'{ExternalSourcesSectionName}:{MappingsPathName}' ist erforderlich, wenn der Abschnitt vorhanden ist.",
                settingsPath,
                "$.ExternalSources");
            return false;
        }

        var pathElement = pathProperty.Value;
        if (pathElement.ValueKind is not JsonValueKind.String
            || string.IsNullOrWhiteSpace(pathElement.GetString()))
        {
            diagnostic = ExternalSourceConfigurationDiagnostic.CreateError(
                ExternalSourceConfigurationDiagnosticCodes.MappingsPathInvalid,
                $"'{ExternalSourcesSectionName}:{MappingsPathName}' muss ein nichtleerer String sein.",
                settingsPath,
                "$.ExternalSources.MappingsPath");
            return false;
        }

        mappingsPath = ResolveMappingsPath(settingsPath, pathElement.GetString()!.Trim());
        if (mappingsPath is not null)
        {
            return true;
        }

        diagnostic = ExternalSourceConfigurationDiagnostic.CreateError(
            ExternalSourceConfigurationDiagnosticCodes.MappingsPathInvalid,
            $"'{ExternalSourcesSectionName}:{MappingsPathName}' ist kein gültiger Dateipfad.",
            settingsPath,
            "$.ExternalSources.MappingsPath");
        return false;
    }

    private static bool TryReadCacheOptions(
        ExternalSourceJsonObjectValidation validation,
        string settingsPath,
        out ExternalSourceCacheOptions? cacheOptions,
        out ExternalSourceConfigurationDiagnostic? diagnostic)
    {
        cacheOptions = ExternalSourceCacheOptions.Default;
        diagnostic = null;
        var cacheRootProperty = validation.GetProperty(CacheRootName);
        var intervalProperty = validation.GetProperty(RefreshIntervalMinutesName);
        if (!TryReadCacheRoot(
                cacheRootProperty,
                settingsPath,
                out var cacheRoot,
                out diagnostic)
            || !TryReadRefreshInterval(
                intervalProperty,
                settingsPath,
                out var refreshInterval,
                out diagnostic))
        {
            return false;
        }

        cacheOptions = new ExternalSourceCacheOptions(cacheRoot!, refreshInterval);
        return true;
    }

    private static bool TryReadCacheRoot(
        ExternalSourceJsonPropertyValidation property,
        string settingsPath,
        out string? cacheRoot,
        out ExternalSourceConfigurationDiagnostic? diagnostic)
    {
        cacheRoot = ExternalSourceCacheOptions.Default.CacheRoot;
        diagnostic = null;
        if (property.Status is ExternalSourceJsonPropertyStatus.Missing)
        {
            return true;
        }

        if (property.Value.ValueKind is not JsonValueKind.String
            || string.IsNullOrWhiteSpace(property.Value.GetString())
            || ExternalSourceConfigurationPath.TryResolveCacheRoot(
                settingsPath,
                property.Value.GetString()!.Trim()) is not { } resolvedRoot)
        {
            diagnostic = CreateCacheDiagnostic(
                ExternalSourceConfigurationDiagnosticCodes.CacheRootInvalid,
                "'ExternalSources:CacheRoot' muss ein sicherer, nichtleerer Pfad sein.",
                settingsPath,
                "$.ExternalSources.CacheRoot");
            return false;
        }

        cacheRoot = resolvedRoot;
        return true;
    }

    private static bool TryReadRefreshInterval(
        ExternalSourceJsonPropertyValidation property,
        string settingsPath,
        out TimeSpan refreshInterval,
        out ExternalSourceConfigurationDiagnostic? diagnostic)
    {
        refreshInterval = ExternalSourceCacheOptions.DefaultRefreshInterval;
        diagnostic = null;
        if (property.Status is ExternalSourceJsonPropertyStatus.Missing)
        {
            return true;
        }

        if (property.Value.ValueKind is not JsonValueKind.Number
            || !IsIntegralJsonNumber(property.Value)
            || !property.Value.TryGetInt64(out var minutes))
        {
            diagnostic = CreateCacheDiagnostic(
                ExternalSourceConfigurationDiagnosticCodes.InvalidFieldType,
                "'ExternalSources:RefreshIntervalMinutes' muss eine positive ganze Zahl sein.",
                settingsPath,
                "$.ExternalSources.RefreshIntervalMinutes");
            return false;
        }

        if (minutes <= 0 || minutes > ExternalSourceCacheOptions.MaxRefreshIntervalMinutes)
        {
            diagnostic = CreateCacheDiagnostic(
                ExternalSourceConfigurationDiagnosticCodes.RefreshIntervalInvalid,
                "'ExternalSources:RefreshIntervalMinutes' liegt außerhalb des zulässigen positiven Bereichs.",
                settingsPath,
                "$.ExternalSources.RefreshIntervalMinutes");
            return false;
        }

        refreshInterval = TimeSpan.FromTicks(minutes * TimeSpan.TicksPerMinute);
        return true;
    }

    private static bool IsIntegralJsonNumber(JsonElement value)
    {
        var rawValue = value.GetRawText();
        return rawValue.IndexOfAny(['.', 'e', 'E']) < 0;
    }

    private static ExternalSourceConfigurationDiagnostic CreateCacheDiagnostic(
        string code,
        string message,
        string settingsPath,
        string jsonPath) =>
        ExternalSourceConfigurationDiagnostic.CreateError(
            code,
            message,
            settingsPath,
            jsonPath);

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
