#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace AiNetLinter.Configuration;

internal sealed record AssemblyAnalysisConfigurationOptions
{
    internal const string SectionName = "AssemblyAnalysis";
    internal const string CacheRootName = "CacheRoot";
    internal const string DecompilationTimeoutSecondsName = "DecompilationTimeoutSeconds";
    internal const string DefaultCacheDirectoryName = "cache";
    internal const string DefaultAssemblyCacheDirectoryName = "asm";
    internal const long DefaultDecompilationTimeoutSeconds = 180;
    internal static readonly long MaxDecompilationTimeoutSeconds =
        TimeSpan.MaxValue.Ticks / TimeSpan.TicksPerSecond;

    internal AssemblyAnalysisConfigurationOptions(
        string cacheRoot,
        TimeSpan decompilationTimeout)
    {
        CacheRoot = ExternalSourceConfigurationPath.TryCanonicalizeAbsoluteRoot(cacheRoot)
            ?? throw new ArgumentException("Die Assembly-Cache-Wurzel muss ein gültiger absoluter Pfad sein.", nameof(cacheRoot));
        if (decompilationTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(decompilationTimeout));
        }

        DecompilationTimeout = decompilationTimeout;
    }

    internal string CacheRoot { get; }

    internal TimeSpan DecompilationTimeout { get; }

    internal static AssemblyAnalysisConfigurationOptions Default(string? settingsPath = null) =>
        new(
            ResolveDefaultCacheRoot(settingsPath),
            TimeSpan.FromSeconds(DefaultDecompilationTimeoutSeconds));

    private static string ResolveDefaultCacheRoot(string? settingsPath)
    {
        var settingsDirectory = string.IsNullOrWhiteSpace(settingsPath)
            ? AppContext.BaseDirectory
            : Path.GetDirectoryName(Path.GetFullPath(settingsPath)) ?? AppContext.BaseDirectory;
        return Path.Combine(settingsDirectory, DefaultCacheDirectoryName, DefaultAssemblyCacheDirectoryName);
    }
}

internal sealed record AssemblyAnalysisConfigurationLoadResult(
    AssemblyAnalysisConfigurationOptions Options,
    IReadOnlyList<ExternalSourceConfigurationDiagnostic> Diagnostics)
{
    internal bool Succeeded => Diagnostics.Count == 0;
}

internal static class AssemblyAnalysisConfigurationLoader
{
    internal static AssemblyAnalysisConfigurationLoadResult Load() =>
        Load(Path.Combine(AppContext.BaseDirectory, ExternalSourceConfigurationLoader.AppSettingsFileName));

    internal static AssemblyAnalysisConfigurationLoadResult Load(string? settingsPath)
    {
        var canonicalPath = CanonicalizeSettingsPath(settingsPath);
        if (canonicalPath is null)
        {
            return Failure(settingsPath ?? ExternalSourceConfigurationLoader.AppSettingsFileName, "$", "Der Pfad der appsettings.json konnte nicht kanonisiert werden.");
        }

        if (!File.Exists(canonicalPath))
        {
            return Success(AssemblyAnalysisConfigurationOptions.Default(canonicalPath));
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(canonicalPath));
            return Parse(document.RootElement, canonicalPath);
        }
        catch (JsonException exception)
        {
            return Failure(canonicalPath, "$", $"appsettings.json ist kein gültiges JSON: {exception.Message}");
        }
        catch (IOException exception)
        {
            return Failure(canonicalPath, "$", $"appsettings.json konnte nicht gelesen werden: {exception.Message}");
        }
        catch (UnauthorizedAccessException exception)
        {
            return Failure(canonicalPath, "$", $"appsettings.json konnte nicht gelesen werden: {exception.Message}");
        }
    }

    private static AssemblyAnalysisConfigurationLoadResult Parse(JsonElement root, string settingsPath)
    {
        var defaults = AssemblyAnalysisConfigurationOptions.Default(settingsPath);
        if (root.ValueKind is not JsonValueKind.Object)
        {
            return Failure(settingsPath, "$", "appsettings.json muss ein JSON-Objekt sein.");
        }

        if (!root.TryGetProperty(AssemblyAnalysisConfigurationOptions.SectionName, out var section))
        {
            return Success(defaults);
        }

        if (section.ValueKind is not JsonValueKind.Object)
        {
            return Failure(settingsPath, "$.AssemblyAnalysis", "Der Abschnitt 'AssemblyAnalysis' muss ein JSON-Objekt sein.");
        }

        var properties = section.EnumerateObject().ToList();
        var propertyIssue = FindPropertyIssue(properties);
        if (propertyIssue is not null)
        {
            return Failure(settingsPath, "$.AssemblyAnalysis." + propertyIssue, $"Das Feld '{propertyIssue}' ist unbekannt oder doppelt vorhanden.");
        }

        var cacheRoot = defaults.CacheRoot;
        if (section.TryGetProperty(AssemblyAnalysisConfigurationOptions.CacheRootName, out var cacheRootElement)
            && (cacheRootElement.ValueKind is not JsonValueKind.String
                || !TryResolveCacheRoot(settingsPath, cacheRootElement.GetString(), out cacheRoot)))
        {
            return Failure(settingsPath, "$.AssemblyAnalysis.CacheRoot", "'AssemblyAnalysis:CacheRoot' muss ein gültiger, nichtleerer Pfad sein.");
        }

        var timeout = defaults.DecompilationTimeout;
        if (section.TryGetProperty(AssemblyAnalysisConfigurationOptions.DecompilationTimeoutSecondsName, out var timeoutElement)
            && !TryReadTimeout(timeoutElement, out timeout))
        {
            return Failure(settingsPath, "$.AssemblyAnalysis.DecompilationTimeoutSeconds", "'AssemblyAnalysis:DecompilationTimeoutSeconds' muss eine positive ganze Zahl sein.");
        }

        return Success(new AssemblyAnalysisConfigurationOptions(cacheRoot, timeout));
    }

    private static string? FindPropertyIssue(IReadOnlyList<JsonProperty> properties)
    {
        var duplicate = properties
            .GroupBy(property => property.Name, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null) return duplicate.Key;

        return properties
            .Where(property => property.Name is not AssemblyAnalysisConfigurationOptions.CacheRootName
                and not AssemblyAnalysisConfigurationOptions.DecompilationTimeoutSecondsName)
            .Select(property => property.Name)
            .FirstOrDefault();
    }

    private static bool TryReadTimeout(JsonElement element, out TimeSpan timeout)
    {
        timeout = default;
        if (element.ValueKind is not JsonValueKind.Number
            || element.GetRawText().IndexOfAny(['.', 'e', 'E']) >= 0
            || !element.TryGetInt64(out var seconds)
            || seconds <= 0
            || seconds > AssemblyAnalysisConfigurationOptions.MaxDecompilationTimeoutSeconds)
        {
            return false;
        }

        timeout = TimeSpan.FromTicks(checked(seconds * TimeSpan.TicksPerSecond));
        return true;
    }

    private static bool TryResolveCacheRoot(string settingsPath, string? value, out string cacheRoot)
    {
        cacheRoot = string.Empty;
        if (string.IsNullOrWhiteSpace(value)) return false;

        var trimmed = value.Trim();
        if (trimmed.Contains('?', StringComparison.Ordinal)
            || trimmed.Contains('#', StringComparison.Ordinal)
            || (!Path.IsPathFullyQualified(trimmed) && trimmed.Contains(':', StringComparison.Ordinal)))
        {
            return false;
        }

        try
        {
            var candidate = Path.IsPathFullyQualified(trimmed)
                ? trimmed
                : Path.Combine(Path.GetDirectoryName(settingsPath) ?? AppContext.BaseDirectory, trimmed);
            cacheRoot = ExternalSourceConfigurationPath.TryCanonicalizeAbsoluteRoot(candidate) ?? string.Empty;
            return cacheRoot.Length > 0;
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or NotSupportedException)
        {
            return false;
        }
    }

    private static string? CanonicalizeSettingsPath(string? settingsPath)
    {
        try
        {
            return Path.GetFullPath(
                string.IsNullOrWhiteSpace(settingsPath)
                    ? Path.Combine(AppContext.BaseDirectory, ExternalSourceConfigurationLoader.AppSettingsFileName)
                    : settingsPath);
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or NotSupportedException)
        {
            return null;
        }
    }

    private static AssemblyAnalysisConfigurationLoadResult Success(AssemblyAnalysisConfigurationOptions options) =>
        new(options, Array.Empty<ExternalSourceConfigurationDiagnostic>());

    private static AssemblyAnalysisConfigurationLoadResult Failure(
        string sourcePath,
        string jsonPath,
        string message) =>
        new(
            AssemblyAnalysisConfigurationOptions.Default(sourcePath),
            [new("assembly-analysis-configuration-invalid", message, "error", $"{sourcePath} ({jsonPath})")]);
}
