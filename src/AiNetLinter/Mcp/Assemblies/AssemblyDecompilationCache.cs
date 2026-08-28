#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AiNetLinter.Mcp.Assemblies;

internal sealed class AssemblyDecompilationCache
{
    private const string ManifestFileName = "manifest.json";
    private const string SourceDirectoryName = "source";
    private static readonly UTF8Encoding Utf8 = new(false);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    internal AssemblyDecompilationCache(string? cacheRoot = null)
    {
        RootPath = Path.GetFullPath(cacheRoot ?? Path.Combine(AppContext.BaseDirectory, "cache", "assembly"));
    }

    internal string RootPath { get; }

    internal string GetEntryDirectory(AssemblyDecompilationCacheKey key)
    {
        var pathHash = HashSegment(key.CanonicalPath, 16);
        var keyHash = HashSegment(key.StableValue, 32);
        return Path.Combine(RootPath, pathHash, keyHash);
    }

    internal bool TryRead(
        AssemblyDecompilationCacheKey key,
        out CachedDecompilationGeneration? generation,
        out AssemblySessionDiagnostic? diagnostic)
    {
        generation = null;
        diagnostic = null;
        var directory = GetEntryDirectory(key);
        var manifestPath = Path.Combine(directory, ManifestFileName);
        if (!File.Exists(manifestPath)) return false;

        try
        {
            var manifest = JsonSerializer.Deserialize<AssemblyDecompilationManifest>(
                File.ReadAllText(manifestPath, Utf8),
                JsonOptions);
            if (manifest is null || !IsManifestCompatible(manifest, key))
            {
                diagnostic = new("assembly-cache-invalid", "Der Assembly-Cacheeintrag hat einen inkompatiblen oder unvollständigen Schlüssel.");
                return false;
            }

            var documents = ReadDocuments(directory, manifest);
            generation = new CachedDecompilationGeneration(manifest with { LastAccessUtc = DateTime.UtcNow }, documents);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException or InvalidDataException)
        {
            diagnostic = new("assembly-cache-read-failed", $"Assembly-Cacheeintrag konnte nicht gelesen werden: {ex.Message}");
            return false;
        }
    }

    internal AssemblyCachePublishResult Publish(AssemblyCachePublishRequest request)
    {
        var targetDirectory = GetEntryDirectory(request.CacheKey);
        var tempDirectory = targetDirectory + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            Directory.CreateDirectory(tempDirectory);
            var documents = WriteDocuments(tempDirectory, request.Decompilation.Documents);
            var manifest = CreateManifest(request, documents);
            var manifestPath = Path.Combine(tempDirectory, ManifestFileName);
            File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, JsonOptions), Utf8);
            Directory.CreateDirectory(Path.GetDirectoryName(targetDirectory)!);

            if (Directory.Exists(targetDirectory))
            {
                if (TryRead(request.CacheKey, out _, out _))
                {
                    return new AssemblyCachePublishResult(true, targetDirectory, null);
                }

                var retiredDirectory = targetDirectory + ".retired-" + Guid.NewGuid().ToString("N");
                Directory.Move(targetDirectory, retiredDirectory);
                Directory.Move(tempDirectory, targetDirectory);
                TryDeleteDirectory(retiredDirectory);
                return new AssemblyCachePublishResult(true, targetDirectory, null);
            }

            Directory.Move(tempDirectory, targetDirectory);
            return new AssemblyCachePublishResult(true, targetDirectory, null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return new AssemblyCachePublishResult(
                false,
                null,
                new("assembly-cache-publish-failed", $"Assembly-Cachegeneration konnte nicht atomar veröffentlicht werden: {ex.Message}", "error"));
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    private static bool IsManifestCompatible(AssemblyDecompilationManifest manifest, AssemblyDecompilationCacheKey key) =>
        manifest.Status is "complete" or "partial" or "degraded"
        && manifest.Complete == string.Equals(manifest.Status, "complete", StringComparison.Ordinal)
        && string.Equals(manifest.CacheKey, key.StableValue, StringComparison.Ordinal)
        && string.Equals(manifest.CanonicalPath, key.CanonicalPath, StringComparison.OrdinalIgnoreCase)
        && string.Equals(manifest.Sha256, key.ContentHash, StringComparison.OrdinalIgnoreCase)
        && string.Equals(manifest.DecompilerVersion, key.DecompilerVersion, StringComparison.Ordinal)
        && string.Equals(manifest.OptionsIdentity, key.OptionsIdentity, StringComparison.Ordinal)
        && string.Equals(manifest.CacheSchemaVersion, key.CacheSchemaVersion, StringComparison.Ordinal);

    private static IReadOnlyList<DecompiledDocument> ReadDocuments(
        string entryDirectory,
        AssemblyDecompilationManifest manifest)
    {
        var documents = new List<DecompiledDocument>(manifest.GeneratedFiles.Count);
        foreach (var relativePath in manifest.GeneratedFiles)
        {
            var fullPath = ResolveSafePath(entryDirectory, relativePath);
            var source = File.ReadAllText(fullPath, Utf8);
            var typeName = Path.GetFileNameWithoutExtension(fullPath);
            documents.Add(new DecompiledDocument(fullPath, typeName, source));
        }

        return documents;
    }

    private static IReadOnlyList<string> WriteDocuments(
        string tempDirectory,
        IReadOnlyList<DecompiledDocument> documents)
    {
        var paths = new List<string>(documents.Count);
        foreach (var (document, index) in documents.Select((value, index) => (value, index)))
        {
            var fileName = $"{index:D5}-{SanitizeFileName(document.TypeMetadataName)}.cs";
            var relativePath = Path.Combine(SourceDirectoryName, fileName);
            var fullPath = ResolveSafePath(tempDirectory, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllText(fullPath, document.CSharpSource, Utf8);
            paths.Add(relativePath.Replace(Path.DirectorySeparatorChar, '/'));
        }

        return paths;
    }

    private static AssemblyDecompilationManifest CreateManifest(
        AssemblyCachePublishRequest request,
        IReadOnlyList<string> generatedFiles) =>
        new()
        {
            CacheKey = request.CacheKey.StableValue,
            CanonicalPath = request.Fingerprint.CanonicalPath,
            OriginalPath = request.Fingerprint.CanonicalPath,
            Length = request.Fingerprint.Length,
            MtimeUtc = request.Fingerprint.MtimeUtc,
            Sha256 = request.Fingerprint.Sha256,
            AssemblyIdentity = request.References.Identity,
            References = request.References.References,
            DecompilerVersion = request.CacheKey.DecompilerVersion,
            OptionsIdentity = request.CacheKey.OptionsIdentity,
            CacheSchemaVersion = request.CacheKey.CacheSchemaVersion,
            GeneratedFiles = generatedFiles,
            Encoding = "utf-8",
            Warnings = request.Decompilation.Diagnostics.Where(IsWarning).Select(diagnostic => diagnostic.Message).ToList(),
            Errors = request.Decompilation.Diagnostics.Where(diagnostic => !IsWarning(diagnostic)).Select(diagnostic => diagnostic.Message).ToList(),
            UnresolvedReferences = request.References.References.Where(reference => !reference.Resolved).Select(reference => reference.Name).ToList(),
            CreatedUtc = DateTime.UtcNow,
            LastAccessUtc = DateTime.UtcNow,
            Status = request.Status.ToString().ToLowerInvariant(),
            Complete = request.Status == AssemblySessionStatus.Complete,
        };

    private static bool IsWarning(AssemblySessionDiagnostic diagnostic) =>
        !string.Equals(diagnostic.Severity, "error", StringComparison.OrdinalIgnoreCase);

    private static string ResolveSafePath(string root, string relativePath)
    {
        var normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var fullPath = Path.GetFullPath(Path.Combine(root, relativePath));
        if (!fullPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Der Cacheeintrag enthält einen unsicheren Dateipfad.");
        }

        return fullPath;
    }

    private static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            builder.Append(invalid.Contains(character) || character is '.' or '+' or '`' ? '_' : character);
        }

        return builder.Length == 0 ? "assembly" : builder.ToString();
    }

    private static string HashSegment(string value, int length)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Utf8.GetBytes(value))).ToLowerInvariant();
        return hash[..Math.Min(length, hash.Length)];
    }

    private static void TryDeleteDirectory(string directory)
    {
        try
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            System.Diagnostics.Debug.WriteLine($"Assembly-Cache-Temp konnte nicht entfernt werden: {ex.Message}");
        }
    }
}
