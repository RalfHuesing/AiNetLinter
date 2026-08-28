#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Microsoft.CodeAnalysis;
using AiNetLinter.Mcp.Tools.AssemblyAnalysis;

namespace AiNetLinter.Mcp.Assemblies;

internal sealed class AssemblyReferenceResolver
{
    internal AssemblyReferenceResolution Resolve(string assemblyPath)
    {
        var canonicalPath = AssemblyFingerprintCalculator.Canonicalize(assemblyPath);
        try
        {
            using var stream = File.OpenRead(canonicalPath);
            using var peReader = new PEReader(stream);
            if (!peReader.HasMetadata)
            {
                return FailedResolution("assembly-metadata-missing", "Die Datei enthält keine .NET-Metadaten.", canonicalPath);
            }

            var metadata = ReadMetadata(peReader.GetMetadataReader());
            var diagnostics = new List<AssemblySessionDiagnostic>();
            var paths = new List<string> { canonicalPath };
            var trustedPlatformAssemblies = GetTrustedPlatformAssemblyPaths();
            var directory = Path.GetDirectoryName(canonicalPath);
            var references = ResolveReferencePaths(
                metadata.References,
                directory,
                trustedPlatformAssemblies,
                paths,
                diagnostics);
            var metadataReferences = CreateMetadataReferences(paths, diagnostics);
            var decompilerResolver = new ICSharpCode.Decompiler.Metadata.UniversalAssemblyResolver(
                canonicalPath,
                false,
                null,
                null,
                PEStreamOptions.PrefetchEntireImage,
                MetadataReaderOptions.None);
            return new AssemblyReferenceResolution(
                metadata.Identity,
                references,
                metadataReferences,
                diagnostics,
                decompilerResolver);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or BadImageFormatException or InvalidOperationException or ArgumentException)
        {
            return FailedResolution(
                "assembly-metadata-read-failed",
                $"Assembly-Metadaten konnten nicht gelesen werden: {ex.Message}",
                canonicalPath);
        }
    }

    private static IReadOnlyList<AssemblyReferenceDto> ResolveReferencePaths(
        IReadOnlyList<AssemblyReferenceDto> references,
        string? directory,
        IReadOnlyList<string> trustedPlatformAssemblies,
        ICollection<string> paths,
        ICollection<AssemblySessionDiagnostic> diagnostics)
    {
        var resolved = new List<AssemblyReferenceDto>(references.Count);
        foreach (var reference in references)
        {
            var candidate = FindReferencePath(directory, reference.Name, trustedPlatformAssemblies);
            if (candidate is null)
            {
                diagnostics.Add(new(
                    "assembly-reference-unresolved",
                    $"Abhängigkeit nicht auflösbar: {reference.Name}, Version {reference.Version}.",
                    "warning"));
                resolved.Add(reference);
                continue;
            }

            paths.Add(candidate);
            resolved.Add(reference with { Resolved = true });
        }

        return resolved;
    }

    private static IReadOnlyList<MetadataReference> CreateMetadataReferences(
        IEnumerable<string> paths,
        ICollection<AssemblySessionDiagnostic> diagnostics)
    {
        var references = new List<MetadataReference>();
        foreach (var path in paths.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                references.Add(MetadataReference.CreateFromFile(path));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or BadImageFormatException or ArgumentException)
            {
                diagnostics.Add(new("assembly-reference-invalid", $"Referenz konnte nicht geladen werden: {path}: {ex.Message}"));
            }
        }

        return references;
    }

    private static AssemblyMetadata ReadMetadata(MetadataReader reader)
    {
        var definition = reader.GetAssemblyDefinition();
        var identity = new AssemblyIdentityDto(
            reader.GetString(definition.Name),
            definition.Version.ToString(),
            definition.Culture.IsNil ? "neutral" : reader.GetString(definition.Culture),
            string.Empty);
        var references = reader.AssemblyReferences
            .Select(handle => reader.GetAssemblyReference(handle))
            .Select(reference => new AssemblyReferenceDto(
                reader.GetString(reference.Name),
                reference.Version.ToString(),
                reference.Culture.IsNil ? "neutral" : reader.GetString(reference.Culture),
                false))
            .OrderBy(reference => reference.Name, StringComparer.Ordinal)
            .ThenBy(reference => reference.Version, StringComparer.Ordinal)
            .ToList();
        return new AssemblyMetadata(identity, references);
    }

    private static AssemblyReferenceResolution FailedResolution(
        string code,
        string message,
        string canonicalPath)
    {
        var resolver = new ICSharpCode.Decompiler.Metadata.UniversalAssemblyResolver(
            canonicalPath,
            false,
            null,
            null,
            PEStreamOptions.PrefetchEntireImage,
            MetadataReaderOptions.None);
        return new AssemblyReferenceResolution(
            null,
            [],
            [],
            [new AssemblySessionDiagnostic(code, message, "error")],
            resolver);
    }

    private static IReadOnlyList<string> GetTrustedPlatformAssemblyPaths() =>
        AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") is string paths
            ? paths.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            : [];

    private static string? FindReferencePath(
        string? directory,
        string referenceName,
        IReadOnlyList<string> trustedPlatformAssemblies)
    {
        if (directory is not null && Directory.Exists(directory))
        {
            try
            {
                var local = Directory.EnumerateFiles(directory, referenceName + ".dll", SearchOption.TopDirectoryOnly)
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                    .FirstOrDefault();
                if (local is not null) return Path.GetFullPath(local);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                // Trusted platform assemblies remain a safe fallback when local enumeration fails.
                _ = ex;
            }
        }

        return trustedPlatformAssemblies
            .Where(path => string.Equals(Path.GetFileNameWithoutExtension(path), referenceName, StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    private sealed record AssemblyMetadata(
        AssemblyIdentityDto Identity,
        IReadOnlyList<AssemblyReferenceDto> References);
}
