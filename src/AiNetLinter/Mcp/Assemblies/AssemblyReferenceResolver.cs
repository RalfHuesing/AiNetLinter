#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using AiNetLinter.Mcp.Tools.AssemblyAnalysis;
using Microsoft.CodeAnalysis;

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
            var candidatePaths = ResolveReferenceCandidates(metadata.References, Path.GetDirectoryName(canonicalPath), GetTrustedPlatformAssemblyPaths(), diagnostics);
            var allPaths = new List<string> { canonicalPath };
            allPaths.AddRange(candidatePaths.Where(candidate => candidate.Path is not null).Select(candidate => candidate.Path!));
            var metadataResult = CreateMetadataReferences(allPaths, diagnostics);
            var references = candidatePaths.Select(candidate =>
            {
                var path = candidate.Path;
                var resolved = path is not null && metadataResult.SuccessfulPaths.Contains(path);
                if (!resolved && path is not null)
                {
                    diagnostics.Add(new(
                        "assembly-reference-metadata-failed",
                        $"Referenz '{candidate.Reference.Name}' wurde nach Identitätsprüfung nicht als MetadataReference eingebunden: {path}.",
                        "warning"));
                }

                return candidate.Reference with
                {
                    Resolved = resolved,
                    ResolvedPath = resolved ? path : null,
                };
            }).ToList();
            var decompilerResolver = new ICSharpCode.Decompiler.Metadata.UniversalAssemblyResolver(
                canonicalPath,
                false,
                null,
                null,
                PEStreamOptions.PrefetchEntireImage,
                MetadataReaderOptions.None);
            return new AssemblyReferenceResolution(metadata.Identity, references, metadataResult.References, diagnostics, decompilerResolver);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or BadImageFormatException or InvalidOperationException or ArgumentException)
        {
            return FailedResolution("assembly-metadata-read-failed", $"Assembly-Metadaten konnten nicht gelesen werden: {ex.Message}", canonicalPath);
        }
    }

    private static IReadOnlyList<ReferenceCandidate> ResolveReferenceCandidates(
        IReadOnlyList<AssemblyReferenceDto> references,
        string? directory,
        IReadOnlyList<string> trustedPlatformAssemblies,
        ICollection<AssemblySessionDiagnostic> diagnostics)
    {
        var resolved = new List<ReferenceCandidate>(references.Count);
        foreach (var reference in references)
        {
            var path = FindReferencePath(reference, directory, trustedPlatformAssemblies, diagnostics);
            if (path is null)
            {
                diagnostics.Add(new(
                    "assembly-reference-unresolved",
                    $"Abhängigkeit nicht auflösbar: {reference.Name}, Version {reference.Version}, Kultur {reference.Culture}.",
                    "warning"));
            }

            resolved.Add(new ReferenceCandidate(reference, path));
        }

        return resolved;
    }

    private static string? FindReferencePath(
        AssemblyReferenceDto reference,
        string? directory,
        IReadOnlyList<string> trustedPlatformAssemblies,
        ICollection<AssemblySessionDiagnostic> diagnostics)
    {
        var candidates = EnumerateCandidatePaths(reference.Name, directory, trustedPlatformAssemblies, diagnostics);
        var mismatches = new List<string>();
        foreach (var candidate in candidates)
        {
            if (!TryReadIdentity(candidate, out var identity, diagnostics)) continue;
            if (IdentityMatches(reference, identity)) return candidate;
            mismatches.Add($"{candidate} ({identity.Version}, {identity.Culture})");
        }

        if (mismatches.Count > 0)
        {
            diagnostics.Add(new(
                "assembly-reference-identity-mismatch",
                $"Kein identitätsgleicher Kandidat für '{reference.Name}' gefunden. Erwartet: Version {reference.Version}, Kultur {reference.Culture}; geprüft: {string.Join(", ", mismatches.Take(5))}.",
                "warning"));
        }

        return null;
    }

    private static IReadOnlyList<string> EnumerateCandidatePaths(
        string referenceName,
        string? directory,
        IReadOnlyList<string> trustedPlatformAssemblies,
        ICollection<AssemblySessionDiagnostic> diagnostics)
    {
        var candidates = new List<string>();
        if (directory is not null && Directory.Exists(directory))
        {
            try
            {
                candidates.AddRange(Directory.EnumerateFiles(directory, referenceName + ".dll", SearchOption.TopDirectoryOnly)
                    .Select(Path.GetFullPath)
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                diagnostics.Add(new(
                    "assembly-reference-enumeration-failed",
                    $"Lokale Referenzen konnten nicht enumeriert werden: {directory}: {ex.Message}",
                    "warning"));
            }
        }

        candidates.AddRange(trustedPlatformAssemblies
            .Where(path => string.Equals(Path.GetFileNameWithoutExtension(path), referenceName, StringComparison.OrdinalIgnoreCase))
            .Select(Path.GetFullPath)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase));
        return candidates.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static MetadataReferenceResult CreateMetadataReferences(
        IEnumerable<string> paths,
        ICollection<AssemblySessionDiagnostic> diagnostics)
    {
        var references = new List<MetadataReference>();
        var successfulPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in paths.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                references.Add(MetadataReference.CreateFromFile(path));
                successfulPaths.Add(path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or BadImageFormatException or ArgumentException or InvalidOperationException)
            {
                diagnostics.Add(new("assembly-reference-invalid", $"Referenz konnte nicht geladen werden: {path}: {ex.Message}", "warning"));
            }
        }

        return new MetadataReferenceResult(references, successfulPaths);
    }

    private static bool TryReadIdentity(
        string path,
        out AssemblyIdentityDto identity,
        ICollection<AssemblySessionDiagnostic> diagnostics)
    {
        try
        {
            using var stream = File.OpenRead(path);
            using var peReader = new PEReader(stream);
            if (!peReader.HasMetadata) throw new BadImageFormatException("Keine .NET-Metadaten vorhanden.");
            identity = ReadIdentity(peReader.GetMetadataReader());
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or BadImageFormatException or InvalidOperationException or ArgumentException)
        {
            diagnostics.Add(new("assembly-reference-candidate-invalid", $"Referenzkandidat konnte nicht statisch geprüft werden: {path}: {ex.Message}", "warning"));
            identity = null!;
            return false;
        }
    }

    private static AssemblyMetadata ReadMetadata(MetadataReader reader)
    {
        var identity = ReadIdentity(reader);
        var references = reader.AssemblyReferences
            .Select(handle => reader.GetAssemblyReference(handle))
            .Select(reference => new AssemblyReferenceDto(
                reader.GetString(reference.Name),
                reference.Version.ToString(),
                NormalizeCulture(reference.Culture.IsNil ? "neutral" : reader.GetString(reference.Culture)),
                false))
            .OrderBy(reference => reference.Name, StringComparer.Ordinal)
            .ThenBy(reference => reference.Version, StringComparer.Ordinal)
            .ToList();
        return new AssemblyMetadata(identity, references);
    }

    private static AssemblyIdentityDto ReadIdentity(MetadataReader reader)
    {
        var definition = reader.GetAssemblyDefinition();
        return new AssemblyIdentityDto(
            reader.GetString(definition.Name),
            definition.Version.ToString(),
            NormalizeCulture(definition.Culture.IsNil ? "neutral" : reader.GetString(definition.Culture)),
            GetPublicKeyToken(reader, definition));
    }

    private static string GetPublicKeyToken(MetadataReader reader, AssemblyDefinition definition)
    {
        if (definition.PublicKey.IsNil) return string.Empty;
        var hash = SHA1.HashData(reader.GetBlobBytes(definition.PublicKey));
        var token = hash[^8..].Reverse().ToArray();
        return Convert.ToHexString(token);
    }

    private static bool IdentityMatches(AssemblyReferenceDto expected, AssemblyIdentityDto actual) =>
        string.Equals(expected.Name, actual.Name, StringComparison.OrdinalIgnoreCase)
        && string.Equals(expected.Version, actual.Version, StringComparison.Ordinal)
        && string.Equals(NormalizeCulture(expected.Culture), NormalizeCulture(actual.Culture), StringComparison.OrdinalIgnoreCase);

    private static string NormalizeCulture(string culture) =>
        string.IsNullOrWhiteSpace(culture) || string.Equals(culture, "neutral", StringComparison.OrdinalIgnoreCase)
            ? "neutral"
            : culture.Trim().ToLowerInvariant();

    private static AssemblyReferenceResolution FailedResolution(string code, string message, string canonicalPath)
    {
        var resolver = new ICSharpCode.Decompiler.Metadata.UniversalAssemblyResolver(
            canonicalPath,
            false,
            null,
            null,
            PEStreamOptions.PrefetchEntireImage,
            MetadataReaderOptions.None);
        return new AssemblyReferenceResolution(null, [], [], [new AssemblySessionDiagnostic(code, message, "error")], resolver);
    }

    private static IReadOnlyList<string> GetTrustedPlatformAssemblyPaths() =>
        AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") is string paths
            ? paths.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            : [];

    private sealed record AssemblyMetadata(AssemblyIdentityDto Identity, IReadOnlyList<AssemblyReferenceDto> References);

    private sealed record ReferenceCandidate(AssemblyReferenceDto Reference, string? Path);

    private sealed record MetadataReferenceResult(
        IReadOnlyList<MetadataReference> References,
        IReadOnlySet<string> SuccessfulPaths);
}
