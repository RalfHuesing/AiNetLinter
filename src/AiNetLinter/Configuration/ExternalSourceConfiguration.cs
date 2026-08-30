#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace AiNetLinter.Configuration;

internal sealed record ExternalSourceCacheOptions
{
    internal const string InvalidCacheRootMessage =
        "Die externe Cache-Wurzel muss ein absoluter, gültiger Pfad sein.";
    internal const long DefaultRefreshIntervalMinutes = 60;
    internal const string DefaultCacheDirectoryName = "cache";
    internal static readonly long MaxRefreshIntervalMinutes =
        TimeSpan.MaxValue.Ticks / TimeSpan.TicksPerMinute;
    internal static readonly TimeSpan DefaultRefreshInterval =
        TimeSpan.FromMinutes(DefaultRefreshIntervalMinutes);

    internal ExternalSourceCacheOptions(string cacheRoot, TimeSpan refreshInterval)
    {
        ArgumentNullException.ThrowIfNull(cacheRoot);
        CacheRoot = ExternalSourceConfigurationPath.TryCanonicalizeCacheRoot(cacheRoot)
            ?? throw new ArgumentException(
                InvalidCacheRootMessage,
                nameof(cacheRoot));
        if (refreshInterval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(refreshInterval));
        }

        RefreshInterval = refreshInterval;
    }

    internal string CacheRoot { get; }

    internal TimeSpan RefreshInterval { get; }

    internal static ExternalSourceCacheOptions Default => new(
        Path.Combine(AppContext.BaseDirectory, DefaultCacheDirectoryName),
        DefaultRefreshInterval);
}

internal sealed record ExternalSourceConfiguration
{
    internal ExternalSourceConfiguration(
        IEnumerable<ExternalSourceMapping> mappings,
        ExternalSourceCacheOptions? cacheOptions = null)
    {
        ArgumentNullException.ThrowIfNull(mappings);
        Mappings = mappings.ToImmutableArray();
        CacheOptions = cacheOptions ?? ExternalSourceCacheOptions.Default;
    }

    internal ImmutableArray<ExternalSourceMapping> Mappings { get; }

    internal ExternalSourceCacheOptions CacheOptions { get; }

    internal bool IsEmpty => Mappings.IsEmpty;

    internal static ExternalSourceConfiguration Empty => new(Array.Empty<ExternalSourceMapping>());
}

internal sealed record ExternalSourceMapping
{
    internal ExternalSourceMapping(string url, string solutionPath, IEnumerable<string> assemblies)
    {
        ArgumentNullException.ThrowIfNull(url);
        ArgumentNullException.ThrowIfNull(solutionPath);
        ArgumentNullException.ThrowIfNull(assemblies);

        Url = url;
        SolutionPath = solutionPath;
        Assemblies = assemblies.ToImmutableArray();
    }

    internal string Url { get; }

    internal string SolutionPath { get; }

    internal ImmutableArray<string> Assemblies { get; }

}

internal sealed record ExternalSourceConfigurationDiagnostic(
    string Code,
    string Message,
    string Severity,
    string Location)
{
    internal static ExternalSourceConfigurationDiagnostic CreateError(
        string code,
        string message,
        string sourcePath,
        string jsonPath) =>
        new(code, message, "error", $"{sourcePath} ({jsonPath})");
}

internal sealed record ExternalSourceConfigurationLoadResult
{
    internal ExternalSourceConfigurationLoadResult(
        ExternalSourceConfiguration? configuration,
        IEnumerable<ExternalSourceConfigurationDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);

        Configuration = configuration;
        Diagnostics = diagnostics.ToImmutableArray();
    }

    internal ExternalSourceConfiguration? Configuration { get; }

    internal ImmutableArray<ExternalSourceConfigurationDiagnostic> Diagnostics { get; }

    internal bool Succeeded => Configuration is not null && Diagnostics.IsEmpty;

    internal static ExternalSourceConfigurationLoadResult Success(ExternalSourceConfiguration configuration) =>
        new(configuration, Array.Empty<ExternalSourceConfigurationDiagnostic>());

    internal static ExternalSourceConfigurationLoadResult Failure(
        IEnumerable<ExternalSourceConfigurationDiagnostic> diagnostics) =>
        new(null, diagnostics);
}

internal static class ExternalSourceConfigurationDiagnosticCodes
{
    internal const string SettingsPathInvalid = "external-source-settings-path-invalid";
    internal const string SettingsReadFailed = "external-source-settings-read-failed";
    internal const string SettingsJsonInvalid = "external-source-settings-json-invalid";
    internal const string SettingsRootInvalid = "external-source-settings-root-invalid";
    internal const string ExternalSourcesSectionInvalid = "external-source-section-invalid";
    internal const string MappingsPathMissing = "external-source-mappings-path-missing";
    internal const string MappingsPathInvalid = "external-source-mappings-path-invalid";
    internal const string MappingsReadFailed = "external-source-mappings-read-failed";
    internal const string MappingsJsonInvalid = "external-source-mappings-json-invalid";
    internal const string MappingsRootInvalid = "external-source-mappings-root-invalid";
    internal const string RequiredFieldMissing = "external-source-required-field-missing";
    internal const string InvalidFieldType = "external-source-invalid-field-type";
    internal const string UnknownField = "external-source-unknown-field";
    internal const string DuplicateField = "external-source-duplicate-field";
    internal const string CacheRootInvalid = "external-source-cache-root-invalid";
    internal const string RefreshIntervalInvalid = "external-source-refresh-interval-invalid";
    internal const string UrlInvalid = "external-source-url-invalid";
    internal const string SolutionPathInvalid = "external-source-solution-path-invalid";
    internal const string AssemblyListInvalid = "external-source-assembly-list-invalid";
    internal const string AssemblyNameInvalid = "external-source-assembly-name-invalid";
    internal const string DuplicateAssembly = "external-source-duplicate-assembly";
    internal const string AmbiguousAssembly = "external-source-ambiguous-assembly";
    internal const string ProviderUnavailable = "external-source-provider-unavailable";
    internal const string AuthenticationRequired = "external-source-authentication-required";
    internal const string AccessDenied = "external-source-access-denied";
    internal const string RepositoryNotFound = "external-source-repository-not-found";
    internal const string NetworkUnavailable = "external-source-network-unavailable";
    internal const string Timeout = "external-source-timeout";
    internal const string InvalidResponse = "external-source-invalid-response";
    internal const string RepositoryMappingInvalid = "external-source-repository-mapping-invalid";
    internal const string RepositoryStagingRootInvalid = "external-source-repository-staging-root-invalid";
    internal const string RepositoryCheckoutPathInvalid = "external-source-repository-checkout-path-invalid";
    internal const string RepositoryCheckoutInvalid = "external-source-repository-checkout-invalid";
    internal const string RepositorySolutionPathInvalid = "external-source-repository-solution-path-invalid";
    internal const string RepositorySolutionInvalid = "external-source-repository-solution-invalid";
    internal const string RepositoryTransportResultInvalid = "external-source-repository-transport-result-invalid";
    internal const string RepositoryTransportFailed = "external-source-repository-transport-failed";
    internal const string RepositoryCapabilityUnavailable = "external-source-repository-capability-unavailable";
    internal const string RepositoryCleanupFailed = "external-source-repository-cleanup-failed";
    internal const string RepositoryCheckoutDirty = "external-source-repository-checkout-dirty";
    internal const string RepositoryCheckoutUnverified = "external-source-repository-checkout-unverified";
    internal const string RepositoryRefreshDegraded = "external-source-repository-refresh-degraded";
}

internal static class ExternalSourceConfigurationPath
{
    internal static string? TryResolveCacheRoot(string settingsPath, string value)
    {
        if (!IsSafeRawCacheRoot(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        try
        {
            var candidate = Path.IsPathFullyQualified(trimmed)
                ? trimmed
                : Path.Combine(
                    Path.GetDirectoryName(settingsPath) ?? AppContext.BaseDirectory,
                    trimmed);
            return TryCanonicalizeAbsoluteRoot(candidate);
        }
        catch (Exception exception) when (
            exception is ArgumentException or IOException or NotSupportedException)
        {
            return null;
        }
    }

    internal static string? TryCanonicalizeCacheRoot(string value)
    {
        if (!IsSafeRawCacheRoot(value)
            || !Path.IsPathFullyQualified(value.Trim()))
        {
            return null;
        }

        return TryCanonicalizeAbsoluteRoot(value);
    }

    internal static string? TryCanonicalizeAbsoluteRoot(string value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || !Path.IsPathFullyQualified(value.Trim()))
        {
            return null;
        }

        try
        {
            var fullPath = Path.GetFullPath(value.Trim());
            var pathRoot = Path.GetPathRoot(fullPath);
            if (string.IsNullOrEmpty(pathRoot))
            {
                return null;
            }

            return string.Equals(fullPath, pathRoot, StringComparison.OrdinalIgnoreCase)
                ? pathRoot
                : fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch (Exception exception) when (
            exception is ArgumentException or IOException or NotSupportedException)
        {
            return null;
        }
    }

    private static bool IsSafeRawCacheRoot(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim().Replace('\\', '/');
        return HasSafeRawSyntax(normalized) && HasSafeRawSegments(normalized);
    }

    private static bool HasSafeRawSyntax(string normalized)
    {
        var isUncPath = normalized.StartsWith("//", StringComparison.Ordinal);
        if (IsDevicePath(normalized)
            || normalized.IndexOf('?') >= 0
            || normalized.IndexOf('#') >= 0
            || (isUncPath
                && (normalized.IndexOf('@') >= 0
                    || normalized[2..].Split(
                        '/',
                        StringSplitOptions.RemoveEmptyEntries).Length < 2))
            || !HasValidColonUsage(normalized))
        {
            return false;
        }

        return !normalized.StartsWith("/", StringComparison.Ordinal)
            || isUncPath;
    }

    private static bool HasSafeRawSegments(string normalized)
    {
        var segmentStart = IsDrivePath(normalized) ? 2 : 0;
        foreach (var segment in normalized[segmentStart..].Split(
                     '/',
                     StringSplitOptions.RemoveEmptyEntries))
        {
            if (IsUnsafeRawSegment(segment))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsUnsafeRawSegment(string segment) =>
        segment is "." or ".."
        || IsReservedDeviceName(segment)
        || segment.EndsWith(' ')
        || segment.EndsWith('.')
        || segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0;

    private static bool HasValidColonUsage(string normalized)
    {
        var isDrivePath = IsDrivePath(normalized);
        for (var index = 0; index < normalized.Length; index++)
        {
            if (normalized[index] == ':'
                && !(isDrivePath && index == 1))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsDrivePath(string normalized) =>
        normalized.Length >= 3
        && IsAsciiLetter(normalized[0])
        && normalized[1] == ':'
        && normalized[2] == '/';

    private static bool IsAsciiLetter(char value) =>
        value is >= 'A' and <= 'Z' or >= 'a' and <= 'z';

    private static bool IsDevicePath(string normalized) =>
        normalized.StartsWith("//./", StringComparison.OrdinalIgnoreCase)
        || normalized.StartsWith("//?/", StringComparison.OrdinalIgnoreCase)
        || normalized.StartsWith("/device/", StringComparison.OrdinalIgnoreCase)
        || normalized.StartsWith("/??/", StringComparison.OrdinalIgnoreCase)
        || normalized.StartsWith("/globalroot/", StringComparison.OrdinalIgnoreCase);

    private static bool IsReservedDeviceName(string segment)
    {
        var extensionIndex = segment.IndexOf('.');
        var name = extensionIndex < 0 ? segment : segment[..extensionIndex];
        return name.Equals("CON", StringComparison.OrdinalIgnoreCase)
            || name.Equals("PRN", StringComparison.OrdinalIgnoreCase)
            || name.Equals("AUX", StringComparison.OrdinalIgnoreCase)
            || name.Equals("NUL", StringComparison.OrdinalIgnoreCase)
            || IsNumberedDeviceName(name, "COM")
            || IsNumberedDeviceName(name, "LPT");
    }

    private static bool IsNumberedDeviceName(string value, string prefix) =>
        value.Length == prefix.Length + 1
        && value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
        && value[^1] is >= '1' and <= '9';
}

internal static class ExternalSourceJsonValidation
{
    internal static ExternalSourceJsonObjectValidation InspectObject(
        JsonElement objectElement,
        string sourcePath,
        string jsonPath,
        string[]? allowedNames = null)
    {
        var diagnostics = new List<ExternalSourceConfigurationDiagnostic>();
        var properties = new Dictionary<string, ExternalSourceJsonPropertyValidation>(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var allowed = allowedNames is null
            ? null
            : new HashSet<string>(allowedNames, StringComparer.Ordinal);

        foreach (var property in objectElement.EnumerateObject())
        {
            var propertyPath = jsonPath + "." + property.Name;
            if (!seen.Add(property.Name))
            {
                if (properties[property.Name].Status is not ExternalSourceJsonPropertyStatus.Duplicate)
                {
                    properties[property.Name] = new(
                        ExternalSourceJsonPropertyStatus.Duplicate,
                        default);
                    diagnostics.Add(ExternalSourceConfigurationDiagnostic.CreateError(
                        ExternalSourceConfigurationDiagnosticCodes.DuplicateField,
                        $"Das Feld '{property.Name}' ist doppelt vorhanden.",
                        sourcePath,
                        propertyPath));
                }
            }
            else
            {
                properties.Add(
                    property.Name,
                    new(ExternalSourceJsonPropertyStatus.Unique, property.Value));
                if (allowed is not null && !allowed.Contains(property.Name))
                {
                    diagnostics.Add(ExternalSourceConfigurationDiagnostic.CreateError(
                        ExternalSourceConfigurationDiagnosticCodes.UnknownField,
                        $"Unbekanntes Feld '{property.Name}'.",
                        sourcePath,
                        propertyPath));
                }
            }
        }

        return new(properties.ToImmutableDictionary(StringComparer.Ordinal), diagnostics.ToImmutableArray());
    }
}

internal enum ExternalSourceJsonPropertyStatus
{
    Missing,
    Unique,
    Duplicate
}

internal sealed record ExternalSourceJsonPropertyValidation(
    ExternalSourceJsonPropertyStatus Status,
    JsonElement Value);

internal sealed record ExternalSourceJsonObjectValidation(
    ImmutableDictionary<string, ExternalSourceJsonPropertyValidation> Properties,
    ImmutableArray<ExternalSourceConfigurationDiagnostic> Diagnostics)
{
    internal ExternalSourceJsonPropertyValidation GetProperty(string propertyName) =>
        Properties.TryGetValue(propertyName, out var property)
            ? property
            : new(ExternalSourceJsonPropertyStatus.Missing, default);
}
