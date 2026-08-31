#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace AiNetLinter.Configuration;

internal static class ExternalSourceMappingValidator
{
    private const string RepositoriesName = "repositories";
    private const string UrlName = "url";
    private const string SolutionPathName = "solutionPath";
    private const string AssembliesName = "assemblies";

    internal static ExternalSourceConfigurationLoadResult Validate(JsonElement root, string sourcePath)
    {
        var diagnostics = new List<ExternalSourceConfigurationDiagnostic>();
        if (root.ValueKind is not JsonValueKind.Object)
        {
            diagnostics.Add(ExternalSourceConfigurationDiagnostic.CreateError(
                ExternalSourceConfigurationDiagnosticCodes.MappingsRootInvalid,
                "Die Mapping-Datei muss ein JSON-Objekt sein.",
                sourcePath,
                "$"));
            return ExternalSourceConfigurationLoadResult.Failure(diagnostics);
        }

        var rootValidation = ExternalSourceJsonValidation.InspectObject(
            root,
            sourcePath,
            "$",
            [RepositoriesName]);
        diagnostics.AddRange(rootValidation.Diagnostics);

        var repositoriesProperty = rootValidation.GetProperty(RepositoriesName);
        if (repositoriesProperty.Status is not ExternalSourceJsonPropertyStatus.Unique)
        {
            if (repositoriesProperty.Status is ExternalSourceJsonPropertyStatus.Missing)
            {
                diagnostics.Add(ExternalSourceConfigurationDiagnostic.CreateError(
                    ExternalSourceConfigurationDiagnosticCodes.RequiredFieldMissing,
                    $"Das erforderliche Feld '{RepositoriesName}' fehlt.",
                    sourcePath,
                    "$"));
            }

            return ExternalSourceConfigurationLoadResult.Failure(diagnostics);
        }

        var repositories = repositoriesProperty.Value;
        if (repositories.ValueKind is not JsonValueKind.Array)
        {
            diagnostics.Add(ExternalSourceConfigurationDiagnostic.CreateError(
                ExternalSourceConfigurationDiagnosticCodes.InvalidFieldType,
                $"Das Feld '{RepositoriesName}' muss ein JSON-Array sein.",
                sourcePath,
                "$.repositories"));
            return ExternalSourceConfigurationLoadResult.Failure(diagnostics);
        }

        return ValidateRepositories(repositories, sourcePath, diagnostics);
    }

    private static ExternalSourceConfigurationLoadResult ValidateRepositories(
        JsonElement repositories,
        string sourcePath,
        List<ExternalSourceConfigurationDiagnostic> diagnostics)
    {
        var mappings = new List<ExternalSourceMapping>();
        var assemblyOwners = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < repositories.GetArrayLength(); index++)
        {
            var repository = repositories[index];
            var mapping = ParseRepository(repository, sourcePath, index, diagnostics);
            if (mapping is null)
            {
                continue;
            }

            AddAssemblyOwners(mapping, index, sourcePath, assemblyOwners, diagnostics);
            mappings.Add(mapping);
        }

        return diagnostics.Count == 0
            ? ExternalSourceConfigurationLoadResult.Success(new ExternalSourceConfiguration(mappings))
            : ExternalSourceConfigurationLoadResult.Failure(diagnostics);
    }

    private static ExternalSourceMapping? ParseRepository(
        JsonElement repository,
        string sourcePath,
        int index,
        List<ExternalSourceConfigurationDiagnostic> diagnostics)
    {
        var repositoryPath = $"$.repositories[{index}]";
        if (repository.ValueKind is not JsonValueKind.Object)
        {
            diagnostics.Add(ExternalSourceConfigurationDiagnostic.CreateError(
                ExternalSourceConfigurationDiagnosticCodes.InvalidFieldType,
                "Jeder Repository-Eintrag muss ein JSON-Objekt sein.",
                sourcePath,
                repositoryPath));
            return null;
        }

        var initialDiagnosticCount = diagnostics.Count;
        var validation = ExternalSourceJsonValidation.InspectObject(
            repository,
            sourcePath,
            repositoryPath,
            [UrlName, SolutionPathName, AssembliesName]);
        diagnostics.AddRange(validation.Diagnostics);

        var context = new ExternalSourceValidationContext(sourcePath, repositoryPath, diagnostics);
        var hasUrl = TryReadRequiredString(validation, UrlName, context, out var url);
        var hasSolutionPath = TryReadRequiredString(validation, SolutionPathName, context, out var solutionPath);
        var assembliesProperty = validation.GetProperty(AssembliesName);
        var hasAssemblies = assembliesProperty.Status is ExternalSourceJsonPropertyStatus.Unique;
        if (assembliesProperty.Status is ExternalSourceJsonPropertyStatus.Missing)
        {
            diagnostics.Add(ExternalSourceConfigurationDiagnostic.CreateError(
                ExternalSourceConfigurationDiagnosticCodes.RequiredFieldMissing,
                $"Das erforderliche Feld '{AssembliesName}' fehlt.",
                sourcePath,
                repositoryPath));
        }

        var normalizedUrl = hasUrl ? NormalizeUrl(url!, context) : null;
        var normalizedSolutionPath = hasSolutionPath ? NormalizeSolutionPath(solutionPath!, context) : null;
        var normalizedAssemblies = hasAssemblies ? NormalizeAssemblies(assembliesProperty.Value, context) : null;

        return diagnostics.Count == initialDiagnosticCount
            && normalizedUrl is not null
            && normalizedSolutionPath is not null
            && normalizedAssemblies is not null
            ? new ExternalSourceMapping(normalizedUrl, normalizedSolutionPath, normalizedAssemblies)
            : null;
    }

    private static void AddAssemblyOwners(
        ExternalSourceMapping mapping,
        int repositoryIndex,
        string sourcePath,
        Dictionary<string, int> assemblyOwners,
        List<ExternalSourceConfigurationDiagnostic> diagnostics)
    {
        foreach (var assembly in mapping.Assemblies)
        {
            if (assemblyOwners.TryGetValue(assembly, out var ownerIndex))
            {
                diagnostics.Add(ExternalSourceConfigurationDiagnostic.CreateError(
                    ExternalSourceConfigurationDiagnosticCodes.AmbiguousAssembly,
                    $"Die Assembly '{assembly}' ist bereits im Repository-Eintrag {ownerIndex} gemappt und dadurch mehrdeutig.",
                    sourcePath,
                    $"$.repositories[{repositoryIndex}].{AssembliesName}"));
                continue;
            }

            assemblyOwners.Add(assembly, repositoryIndex);
        }
    }

    private static bool TryReadRequiredString(
        ExternalSourceJsonObjectValidation validation,
        string propertyName,
        ExternalSourceValidationContext context,
        out string? value)
    {
        value = null;
        var propertyValidation = validation.GetProperty(propertyName);
        if (propertyValidation.Status is not ExternalSourceJsonPropertyStatus.Unique)
        {
            if (propertyValidation.Status is ExternalSourceJsonPropertyStatus.Missing)
            {
                context.Diagnostics.Add(ExternalSourceConfigurationDiagnostic.CreateError(
                    ExternalSourceConfigurationDiagnosticCodes.RequiredFieldMissing,
                    $"Das erforderliche Feld '{propertyName}' fehlt.",
                    context.SourcePath,
                    context.ObjectPath));
            }

            return false;
        }

        var property = propertyValidation.Value;
        if (property.ValueKind is not JsonValueKind.String
            || string.IsNullOrWhiteSpace(property.GetString()))
        {
            context.Diagnostics.Add(ExternalSourceConfigurationDiagnostic.CreateError(
                ExternalSourceConfigurationDiagnosticCodes.InvalidFieldType,
                $"Das Feld '{propertyName}' muss ein nichtleerer String sein.",
                context.SourcePath,
                context.ObjectPath + "." + propertyName));
            return false;
        }

        value = property.GetString()!.Trim();
        return true;
    }

    private static string? NormalizeUrl(string value, ExternalSourceValidationContext context)
    {
        if (!ExternalSourceUrlPolicy.TryNormalize(value, out var normalizedUrl))
        {
            context.Diagnostics.Add(ExternalSourceConfigurationDiagnostic.CreateError(
                ExternalSourceConfigurationDiagnosticCodes.UrlInvalid,
                "Die Repository-URL muss eine absolute HTTP(S)-URL ohne Credentials, Query oder Fragment sein.",
                context.SourcePath,
                context.ObjectPath + "." + UrlName));
            return null;
        }

        return normalizedUrl;
    }

    private static string? NormalizeSolutionPath(string value, ExternalSourceValidationContext context)
    {
        var path = value.Replace('\\', '/');
        if (path.Length == 0 || Path.IsPathRooted(path) || path.StartsWith("/", StringComparison.Ordinal) || ExternalSourcePathRules.IsDriveQualified(path))
        {
            AddInvalidSolutionPath(value, context);
            return null;
        }

        var segments = NormalizeSolutionSegments(path, value, context);
        if (segments is null)
        {
            return null;
        }

        var normalized = string.Join('/', segments);
        if (normalized.Length == 0
            || !(normalized.EndsWith(".sln", StringComparison.OrdinalIgnoreCase)
                || normalized.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase)))
        {
            AddInvalidSolutionPath(value, context);
            return null;
        }

        return normalized;
    }

    private static List<string>? NormalizeSolutionSegments(
        string path,
        string originalValue,
        ExternalSourceValidationContext context)
    {
        var segments = new List<string>();
        foreach (var segment in path.Split('/'))
        {
            if (segment.Length == 0 || segment is ".")
            {
                continue;
            }

            if (segment is "..")
            {
                if (segments.Count == 0)
                {
                    AddInvalidSolutionPath(originalValue, context);
                    return null;
                }

                segments.RemoveAt(segments.Count - 1);
                continue;
            }

            if (segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                AddInvalidSolutionPath(originalValue, context);
                return null;
            }

            segments.Add(segment);
        }

        return segments;
    }

    private static List<string>? NormalizeAssemblies(
        JsonElement element,
        ExternalSourceValidationContext context)
    {
        if (element.ValueKind is not JsonValueKind.Array)
        {
            context.Diagnostics.Add(ExternalSourceConfigurationDiagnostic.CreateError(
                ExternalSourceConfigurationDiagnosticCodes.AssemblyListInvalid,
                $"Das Feld '{AssembliesName}' muss ein nichtleeres JSON-Array sein.",
                context.SourcePath,
                context.ObjectPath + "." + AssembliesName));
            return null;
        }

        var normalized = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < element.GetArrayLength(); index++)
        {
            var assembly = NormalizeAssembly(element[index], index, context);
            if (assembly is null)
            {
                continue;
            }

            if (!seen.Add(assembly))
            {
                context.Diagnostics.Add(ExternalSourceConfigurationDiagnostic.CreateError(
                    ExternalSourceConfigurationDiagnosticCodes.DuplicateAssembly,
                    $"Der Assembly-Name '{assembly}' ist innerhalb des Repository-Eintrags doppelt vorhanden.",
                    context.SourcePath,
                    $"{context.ObjectPath}.{AssembliesName}[{index}]"));
                continue;
            }

            normalized.Add(assembly);
        }

        if (element.GetArrayLength() == 0)
        {
            context.Diagnostics.Add(ExternalSourceConfigurationDiagnostic.CreateError(
                ExternalSourceConfigurationDiagnosticCodes.AssemblyListInvalid,
                $"Das Feld '{AssembliesName}' darf nicht leer sein.",
                context.SourcePath,
                context.ObjectPath + "." + AssembliesName));
        }

        return normalized;
    }

    private static string? NormalizeAssembly(
        JsonElement element,
        int index,
        ExternalSourceValidationContext context)
    {
        var itemPath = $"{context.ObjectPath}.{AssembliesName}[{index}]";
        if (element.ValueKind is not JsonValueKind.String
            || string.IsNullOrWhiteSpace(element.GetString()))
        {
            context.Diagnostics.Add(ExternalSourceConfigurationDiagnostic.CreateError(
                ExternalSourceConfigurationDiagnosticCodes.AssemblyNameInvalid,
                "Jeder Assembly-Name muss ein nichtleerer String sein.",
                context.SourcePath,
                itemPath));
            return null;
        }

        var assembly = element.GetString()!.Trim();
        if (assembly.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        {
            assembly = assembly[..^4];
        }

        if (assembly.Length == 0
            || assembly.Contains('/', StringComparison.Ordinal)
            || assembly.Contains('\\', StringComparison.Ordinal)
            || assembly.Contains('\0', StringComparison.Ordinal))
        {
            context.Diagnostics.Add(ExternalSourceConfigurationDiagnostic.CreateError(
                ExternalSourceConfigurationDiagnosticCodes.AssemblyNameInvalid,
                "Ein Assembly-Name darf kein leerer Wert und kein Pfad sein.",
                context.SourcePath,
                itemPath));
            return null;
        }

        return assembly;
    }

    private static void AddInvalidSolutionPath(
        string value,
        ExternalSourceValidationContext context) =>
        context.Diagnostics.Add(ExternalSourceConfigurationDiagnostic.CreateError(
            ExternalSourceConfigurationDiagnosticCodes.SolutionPathInvalid,
            $"Der Solution-Pfad muss repository-relativ sein, darf nicht aus dem Repository ausbrechen und muss auf .sln oder .slnx enden: '{value}'.",
            context.SourcePath,
            context.ObjectPath + "." + SolutionPathName));

    private sealed record ExternalSourceValidationContext(
        string SourcePath,
        string ObjectPath,
        List<ExternalSourceConfigurationDiagnostic> Diagnostics);
}

internal static class ExternalSourceUrlPolicy
{
    internal static bool TryNormalize(string value, out string? normalizedUrl)
    {
        normalizedUrl = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmedValue = value.Trim();
        if (!Uri.TryCreate(trimmedValue, UriKind.Absolute, out var uri)
            || uri is null
            || uri.Host.Length == 0
            || uri.UserInfo.Length > 0
            || uri.Query.Length > 0
            || uri.Fragment.Length > 0
            || !IsHttpScheme(uri.Scheme))
        {
            return false;
        }

        normalizedUrl = uri.AbsoluteUri;
        return true;
    }

    private static bool IsHttpScheme(string scheme) =>
        string.Equals(scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
        || string.Equals(scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
}
