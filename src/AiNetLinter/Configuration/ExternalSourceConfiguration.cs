#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json;

namespace AiNetLinter.Configuration;

internal sealed record ExternalSourceConfiguration
{
    internal ExternalSourceConfiguration(IEnumerable<ExternalSourceMapping> mappings)
    {
        ArgumentNullException.ThrowIfNull(mappings);
        Mappings = mappings.ToImmutableArray();
    }

    internal ImmutableArray<ExternalSourceMapping> Mappings { get; }

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
