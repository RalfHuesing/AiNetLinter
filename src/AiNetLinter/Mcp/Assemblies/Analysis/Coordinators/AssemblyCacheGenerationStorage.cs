#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace AiNetLinter.Mcp.Assemblies.Analysis.Coordinators;

internal static class AssemblyCacheGenerationStorage
{
    internal static CachedDecompilationGeneration ReadGeneration(
        string generationDirectory,
        AssemblyCacheReadRequest request,
        JsonSerializerOptions jsonOptions,
        Encoding encoding)
    {
        var manifestPath = ResolveSafePath(generationDirectory, AssemblyCacheContract.ManifestFileName);
        var manifest = JsonSerializer.Deserialize<AssemblyDecompilationManifest>(File.ReadAllText(manifestPath, encoding), jsonOptions)
            ?? throw new InvalidDataException("Das Cachemanifest ist leer.");
        if (!IsManifestCompatible(manifest, request.Key, request.Fingerprint, request.References))
        {
            throw new InvalidDataException("Das Cachemanifest ist inkompatibel oder statusseitig inkonsistent.");
        }

        var documents = ReadDocuments(generationDirectory, manifest, encoding);
        var projectFilePath = FindProjectFile(generationDirectory)
            ?? throw new InvalidDataException("Die Cachegeneration enthält keine .csproj-Datei.");
        var updatedStatus = manifest.Status with { LastAccessUtc = DateTime.UtcNow };
        return new CachedDecompilationGeneration(manifest with { Status = updatedStatus }, documents, projectFilePath);
    }

    internal static string? FindProjectFile(string root) =>
        Directory.EnumerateFiles(root, "*.csproj", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

    internal static string GetSafeRelativePath(string root, string fullPath) =>
        NormalizeDocumentPath(Path.GetRelativePath(root, fullPath));

    internal static string GetSafePathForAbsoluteInput(string root, string fullPath, string requiredExtension)
    {
        if (!Path.IsPathFullyQualified(fullPath)
            || !string.Equals(Path.GetExtension(fullPath), requiredExtension, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Die Cachegeneration enthält einen unsicheren Projektpfad.");
        }

        var relativePath = Path.GetRelativePath(root, fullPath);
        if (relativePath.StartsWith("..", StringComparison.Ordinal))
        {
            throw new InvalidDataException("Die Cachegeneration enthält einen Projektpfad außerhalb des Stagingverzeichnisses.");
        }

        return ResolveSafePath(root, relativePath);
    }

    internal static string ResolveSafePath(string root, string relativePath)
    {
        var normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var fullPath = Path.GetFullPath(Path.Combine(root, relativePath));
        if (!fullPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Der Cacheeintrag enthält einen unsicheren Dateipfad.");
        }

        return fullPath;
    }

    private static bool IsManifestCompatible(
        AssemblyDecompilationManifest manifest,
        AssemblyDecompilationCacheKey key,
        AssemblyFingerprint fingerprint,
        AssemblyReferenceResolution references) =>
        IsStatusCompatible(manifest.Status, manifest.Diagnostics)
        && IsInputCompatible(manifest.Input, key, fingerprint)
        && IsFormatCompatible(manifest.Format, key)
        && IsReferencesCompatible(manifest.References, manifest.Diagnostics, references);

    private static bool IsStatusCompatible(AssemblyManifestStatus status, AssemblyManifestDiagnostics diagnostics)
    {
        if (!AssemblySessionStatusExtensions.TryParsePersisted(status.Status, out var parsed)) return false;
        return status.Complete == (parsed == AssemblySessionStatus.Complete)
            && diagnostics.Errors.Count == 0
            && (!status.Complete || diagnostics.UnresolvedReferences.Count == 0)
            && AreMessagesValid(diagnostics.Warnings)
            && AreMessagesValid(diagnostics.Errors)
            && AreMessagesValid(diagnostics.UnresolvedReferences)
            && status.CreatedUtc != default
            && status.LastAccessUtc != default;
    }

    private static bool AreMessagesValid(IReadOnlyList<string> messages) =>
        messages.All(message => !string.IsNullOrWhiteSpace(message));

    private static bool IsInputCompatible(
        AssemblyManifestInput input,
        AssemblyDecompilationCacheKey key,
        AssemblyFingerprint fingerprint) =>
        !string.IsNullOrWhiteSpace(input.CacheKey)
        && Path.IsPathFullyQualified(input.CanonicalPath)
        && Path.IsPathFullyQualified(input.OriginalPath)
        && string.Equals(input.OriginalPath, fingerprint.CanonicalPath, StringComparison.OrdinalIgnoreCase)
        && string.Equals(input.CacheKey, key.StableValue, StringComparison.Ordinal)
        && string.Equals(input.CanonicalPath, fingerprint.CanonicalPath, StringComparison.OrdinalIgnoreCase)
        && string.Equals(input.Sha256, fingerprint.Sha256, StringComparison.OrdinalIgnoreCase)
        && input.Length >= 0
        && input.Length == fingerprint.Length;

    private static bool IsFormatCompatible(AssemblyManifestFormat format, AssemblyDecompilationCacheKey key) =>
        string.Equals(format.DecompilerVersion, key.DecompilerVersion, StringComparison.Ordinal)
        && string.Equals(format.OptionsIdentity, key.OptionsIdentity, StringComparison.Ordinal)
        && string.Equals(format.CacheSchemaVersion, key.CacheSchemaVersion, StringComparison.Ordinal)
        && string.Equals(format.Encoding, AssemblyCacheContract.Utf8EncodingName, StringComparison.OrdinalIgnoreCase);

    private static bool IsReferencesCompatible(
        AssemblyManifestReferences manifestReferences,
        AssemblyManifestDiagnostics diagnostics,
        AssemblyReferenceResolution references) =>
        diagnostics.UnresolvedReferences.SequenceEqual(
            references.References.Where(reference => !reference.Resolved).Select(reference => reference.Name),
            StringComparer.Ordinal)
        && manifestReferences.AssemblyIdentity is not null
        && IdentityEquals(manifestReferences.AssemblyIdentity, references.Identity)
        && ReferencesEqual(manifestReferences.References, references.References);

    private static IReadOnlyList<DecompiledDocument> ReadDocuments(
        string generationDirectory,
        AssemblyDecompilationManifest manifest,
        Encoding encoding)
    {
        var generatedFiles = manifest.Format.GeneratedFiles;
        if (generatedFiles.Count == 0) throw new InvalidDataException("Das Cachemanifest enthält keine Dokumente.");
        var normalizedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var documents = new List<DecompiledDocument>(generatedFiles.Count);
        foreach (var relativePath in generatedFiles)
        {
            var normalized = NormalizeDocumentPath(relativePath);
            if (!normalizedFiles.Add(normalized)) throw new InvalidDataException("Das Cachemanifest enthält doppelte Dokumentpfade.");
            var fullPath = ResolveSafePath(generationDirectory, normalized);
            if (!File.Exists(fullPath)) throw new InvalidDataException($"Das Cache-Dokument '{relativePath}' fehlt.");

            var source = File.ReadAllText(fullPath, encoding);
            if (string.IsNullOrWhiteSpace(source)) throw new InvalidDataException($"Das Cache-Dokument '{relativePath}' ist leer.");
            documents.Add(new DecompiledDocument(fullPath, Path.GetFileNameWithoutExtension(fullPath), source));
        }

        ValidateSourceFileSet(generationDirectory, normalizedFiles);
        return documents;
    }

    private static void ValidateSourceFileSet(string generationDirectory, HashSet<string> expected)
    {
        var actual = Directory.EnumerateFiles(generationDirectory, "*.cs", SearchOption.AllDirectories)
            .Select(path => NormalizeDocumentPath(Path.GetRelativePath(generationDirectory, path)))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!actual.SetEquals(expected)) throw new InvalidDataException("Manifest und vorhandene Cache-Dokumente stimmen nicht überein.");
    }

    private static string NormalizeDocumentPath(string relativePath)
    {
        var normalized = relativePath.Replace('\\', '/');
        if (Path.IsPathFullyQualified(normalized)
            || normalized.StartsWith("../", StringComparison.Ordinal)
            || normalized.Contains("/../", StringComparison.Ordinal)
            || normalized.EndsWith("/..", StringComparison.Ordinal)
            || normalized.StartsWith("./", StringComparison.Ordinal)
            || normalized.Contains("//", StringComparison.Ordinal)
            || !normalized.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Das Manifest enthält einen unsicheren oder ungültigen Dokumentpfad.");
        }

        return normalized;
    }

    private static bool ReferencesEqual(
        IReadOnlyList<AiNetLinter.Mcp.Tools.AssemblyAnalysis.AssemblyReferenceDto> expected,
        IReadOnlyList<AiNetLinter.Mcp.Tools.AssemblyAnalysis.AssemblyReferenceDto> actual) =>
        expected.Count == actual.Count
        && expected.Zip(actual).All(pair =>
            string.Equals(pair.First.Name, pair.Second.Name, StringComparison.Ordinal)
            && string.Equals(pair.First.Version, pair.Second.Version, StringComparison.Ordinal)
            && string.Equals(pair.First.Culture, pair.Second.Culture, StringComparison.OrdinalIgnoreCase)
            && pair.First.Resolved == pair.Second.Resolved
            && string.Equals(pair.First.ResolvedPath, pair.Second.ResolvedPath, StringComparison.OrdinalIgnoreCase)
            && string.Equals(pair.First.ResolutionState, pair.Second.ResolutionState, StringComparison.Ordinal)
            && pair.First.Depth == pair.Second.Depth
            && string.Equals(pair.First.Diagnostic, pair.Second.Diagnostic, StringComparison.Ordinal));

    private static bool IdentityEquals(
        AiNetLinter.Mcp.Tools.AssemblyAnalysis.AssemblyIdentityDto? expected,
        AiNetLinter.Mcp.Tools.AssemblyAnalysis.AssemblyIdentityDto? actual) =>
        expected is not null && actual is not null
        && string.Equals(expected.Name, actual.Name, StringComparison.Ordinal)
        && string.Equals(expected.Version, actual.Version, StringComparison.Ordinal)
        && string.Equals(expected.Culture, actual.Culture, StringComparison.OrdinalIgnoreCase)
        && string.Equals(expected.PublicKeyToken, actual.PublicKeyToken, StringComparison.OrdinalIgnoreCase);
}
