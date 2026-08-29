#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using AiNetLinter.Configuration;

namespace AiNetLinter.Mcp.Assemblies;

internal static class ExternalSourceRepositoryCacheReader
{
    private static readonly UTF8Encoding Utf8 = new(false, true);

    internal static bool TryReadCurrent(
        ExternalSourceRepositoryCacheReadRequest request,
        out ExternalSourceRepositoryCacheReadResult? result)
    {
        ArgumentNullException.ThrowIfNull(request);
        result = null;
        if (File.Exists(request.EntryDirectory))
        {
            throw new InvalidDataException("Der Cacheeintrag ist kein Verzeichnis.");
        }

        if (!Directory.Exists(request.EntryDirectory))
        {
            return false;
        }

        ExternalSourceRepositoryCacheStorage.EnsureSafeDirectory(request.EntryDirectory);
        var pointerPath = Path.Combine(
            request.EntryDirectory,
            ExternalSourceRepositoryCacheContract.CurrentPointerFileName);
        if (!File.Exists(pointerPath))
        {
            if (Directory.Exists(pointerPath))
            {
                throw new InvalidDataException("Der Current-Pointer ist kein Dateipfad.");
            }

            return false;
        }

        var generationName = ReadPointer(pointerPath);
        result = ReadGeneration(request, generationName);
        return true;
    }

    internal static ExternalSourceRepositoryCacheReadResult ReadGeneration(
        ExternalSourceRepositoryCacheReadRequest request,
        string? generationName = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        var name = generationName ?? ReadPointer(Path.Combine(
            request.EntryDirectory,
            ExternalSourceRepositoryCacheContract.CurrentPointerFileName));
        if (!ExternalSourceRepositoryCacheContract.IsSafeGenerationName(name))
        {
            throw new InvalidDataException("Die Cachegeneration hat keinen sicheren Namen.");
        }

        var generationDirectory = ExternalSourceRepositoryCacheStorage.ResolveSafePath(
            request.EntryDirectory,
            name);
        ExternalSourceRepositoryCacheStorage.EnsureSafeDirectory(generationDirectory);
        if (ExternalSourceRepositoryPathGuard.ContainsReparsePointInTree(generationDirectory))
        {
            throw new InvalidDataException("Die Cachegeneration enthält einen Reparse-Punkt.");
        }

        var manifestPath = ExternalSourceRepositoryCacheStorage.ResolveSafePath(
            generationDirectory,
            ExternalSourceRepositoryCacheContract.ManifestFileName);
        var manifest = ReadManifest(manifestPath);
        ValidateManifestIdentity(request, name, manifest);
        ValidateInventory(generationDirectory, manifest.Files);
        return new ExternalSourceRepositoryCacheReadResult(manifest, generationDirectory);
    }

    private static void ValidateManifestIdentity(
        ExternalSourceRepositoryCacheReadRequest request,
        string generationName,
        ExternalSourceRepositoryCacheManifest manifest)
    {
        if (!string.Equals(manifest.GenerationName, generationName, StringComparison.Ordinal)
            || !string.Equals(manifest.CacheSchemaVersion, request.Key.SchemaVersion, StringComparison.Ordinal)
            || !string.Equals(manifest.CacheKey, request.Key.StableValue, StringComparison.Ordinal)
            || !string.Equals(manifest.CanonicalRepositoryUrl, request.Key.CanonicalRepositoryUrl, StringComparison.Ordinal)
            || !string.Equals(manifest.SolutionPath, request.Key.SolutionPath, StringComparison.Ordinal)
            || request.ExpectedRevision is not null
                && !string.Equals(manifest.LoadedRevision, request.ExpectedRevision, StringComparison.Ordinal)
            || request.ExpectedSolutionPath is not null
                && !string.Equals(manifest.SolutionPath, request.ExpectedSolutionPath, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Das Cachemanifest ist inkompatibel.");
        }
    }

    private static string ReadPointer(string pointerPath)
    {
        ExternalSourceRepositoryCacheStorage.EnsureRegularFile(pointerPath);
        var json = ReadBoundedText(
            pointerPath,
            ExternalSourceRepositoryCacheContract.MaxPointerJsonBytes);
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions
        {
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow,
            MaxDepth = 8,
        });
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("Der Current-Pointer ist kein JSON-Objekt.");
        }

        string? generation = null;
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in document.RootElement.EnumerateObject())
        {
            if (!names.Add(property.Name)
                || !string.Equals(
                    property.Name,
                    ExternalSourceRepositoryCacheContract.PointerGenerationPropertyName,
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException("Der Current-Pointer enthält ein ungültiges Feld.");
            }

            generation = ReadString(property.Value);
        }

        if (generation is null
            || !ExternalSourceRepositoryCacheContract.IsSafeGenerationName(generation))
        {
            throw new InvalidDataException("Der Current-Pointer enthält eine ungültige Generation.");
        }

        return generation!;
    }

    private static ExternalSourceRepositoryCacheManifest ReadManifest(string manifestPath)
    {
        ExternalSourceRepositoryCacheStorage.EnsureRegularFile(manifestPath);
        var json = ReadBoundedText(
            manifestPath,
            ExternalSourceRepositoryCacheContract.MaxManifestJsonBytes);
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions
        {
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow,
            MaxDepth = 16,
        });
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("Das Cachemanifest ist kein JSON-Objekt.");
        }

        var values = ReadManifestProperties(document.RootElement);
        ValidateManifestValues(values);
        return new ExternalSourceRepositoryCacheManifest(
            values.SchemaVersion!,
            values.CacheKey!,
            values.RepositoryUrl!,
            values.SolutionPath!,
            values.Revision!,
            values.GenerationName!,
            values.CreatedUtc,
            values.Files!);
    }

    private static ManifestValues ReadManifestProperties(JsonElement root)
    {
        var values = new ManifestValues();
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in root.EnumerateObject())
        {
            if (!names.Add(property.Name))
            {
                throw new InvalidDataException("Das Cachemanifest enthält doppelte Felder.");
            }

            switch (property.Name)
            {
                case "cacheSchemaVersion": values.SchemaVersion = ReadString(property.Value); break;
                case "cacheKey": values.CacheKey = ReadString(property.Value); break;
                case "canonicalRepositoryUrl": values.RepositoryUrl = ReadString(property.Value); break;
                case "solutionPath": values.SolutionPath = ReadString(property.Value); break;
                case "loadedRevision": values.Revision = ReadString(property.Value); break;
                case "generationName": values.GenerationName = ReadString(property.Value); break;
                case "createdUtc": values.CreatedUtc = property.Value.GetDateTime(); break;
                case "files": values.Files = ReadFiles(property.Value); break;
                default: throw new InvalidDataException("Das Cachemanifest enthält ein unbekanntes Feld.");
            }
        }

        return values;
    }

    private static void ValidateManifestValues(ManifestValues values)
    {
        if (!HasAllManifestValues(values))
        {
            throw new InvalidDataException("Das Cachemanifest ist unvollständig oder unsicher.");
        }

        ValidateManifestLimits(values);
        ValidateManifestIdentity(values);
    }

    private static bool HasAllManifestValues(ManifestValues values) =>
        values.SchemaVersion is not null
        && values.CacheKey is not null
        && values.RepositoryUrl is not null
        && values.SolutionPath is not null
        && values.Revision is not null
        && values.GenerationName is not null
        && values.CreatedUtc != default
        && values.Files is not null;

    private static void ValidateManifestLimits(ManifestValues values)
    {
        if (values.SchemaVersion!.Length > ExternalSourceRepositoryCacheContract.MaxSchemaVersionLength
            || values.RepositoryUrl!.Length > ExternalSourceRepositoryCacheContract.MaxRepositoryUrlLength
            || !ExternalSourceRepositoryCacheKey.IsSafeRevision(values.Revision!)
            || !ExternalSourceRepositoryCacheContract.IsSafeGenerationName(values.GenerationName!))
        {
            throw new InvalidDataException("Das Cachemanifest überschreitet ein Sicherheitslimit.");
        }

        if (values.CacheKey!.Length != 64
            || values.CacheKey.Any(character => !ExternalSourceRepositoryCacheContract.IsLowerHexDigit(character)))
        {
            throw new InvalidDataException("Das Cachemanifest enthält einen ungültigen Cache-Key.");
        }
    }

    private static void ValidateManifestIdentity(ManifestValues values)
    {
        if (!ExternalSourceRepositoryUrlPolicy.TryNormalize(values.RepositoryUrl!, out var normalizedUrl)
            || !string.Equals(normalizedUrl, values.RepositoryUrl, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Das Cachemanifest enthält eine ungültige Repository-URL.");
        }

        if (!ExternalSourceRepositoryCacheKey.TryNormalizeSolutionPath(
                values.SolutionPath!,
                out var normalizedSolution)
            || !string.Equals(normalizedSolution, values.SolutionPath, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Das Cachemanifest enthält einen ungültigen Solution-Pfad.");
        }
    }

    private static IReadOnlyList<ExternalSourceRepositoryCacheFileEntry> ReadFiles(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Array
            || value.GetArrayLength() > ExternalSourceRepositoryCacheContract.MaxInventoryEntries)
        {
            throw new InvalidDataException("Das Cachemanifest enthält ein unbegrenztes Datei-Inventar.");
        }

        var files = new List<ExternalSourceRepositoryCacheFileEntry>(value.GetArrayLength());
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var totalBytes = 0L;
        foreach (var item in value.EnumerateArray())
        {
            var file = ReadFile(item);
            if (!paths.Add(file.RelativePath)
                || totalBytes > ExternalSourceRepositoryCacheContract.MaxInventoryBytes - file.Length)
            {
                throw new InvalidDataException("Das Cacheinventar enthält einen ungültigen oder doppelten Pfad.");
            }

            totalBytes += file.Length;
            files.Add(file);
        }

        return files;
    }

    private static ExternalSourceRepositoryCacheFileEntry ReadFile(JsonElement item)
    {
        var values = ReadFileProperties(item);
        ValidateFileValues(values);
        return new ExternalSourceRepositoryCacheFileEntry(
            values.Path!,
            values.Length,
            values.Hash!);
    }

    private static FileValues ReadFileProperties(JsonElement item)
    {
        if (item.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("Das Cacheinventar enthält einen ungültigen Eintrag.");
        }

        var values = new FileValues();
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in item.EnumerateObject())
        {
            if (!names.Add(property.Name))
            {
                throw new InvalidDataException("Das Cacheinventar enthält doppelte Felder.");
            }

            switch (property.Name)
            {
                case "relativePath": values.Path = ReadString(property.Value); break;
                case "length": values.Length = property.Value.GetInt64(); break;
                case "contentHash": values.Hash = ReadString(property.Value); break;
                default: throw new InvalidDataException("Das Cacheinventar enthält ein unbekanntes Feld.");
            }
        }

        return values;
    }

    private static void ValidateFileValues(FileValues values)
    {
        if (values.Path is null
            || !ExternalSourceRepositoryCacheContract.TryNormalizeRelativeFilePath(
                values.Path,
                out var normalizedPath)
            || !string.Equals(values.Path, normalizedPath, StringComparison.Ordinal)
            || string.Equals(
                Path.GetFileName(values.Path),
                ExternalSourceCheckoutOwnership.OwnershipMarkerFileName,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Das Cacheinventar enthält einen unsicheren Pfad.");
        }

        if (values.Length < 0 || values.Length > ExternalSourceRepositoryCacheContract.MaxFileLength)
        {
            throw new InvalidDataException("Das Cacheinventar enthält eine ungültige Dateilänge.");
        }

        if (values.Hash is null
            || values.Hash.Length != 64
            || values.Hash.Any(character => !ExternalSourceRepositoryCacheContract.IsLowerHexDigit(character)))
        {
            throw new InvalidDataException("Das Cacheinventar enthält einen ungültigen Inhaltshash.");
        }
    }

    private static void ValidateInventory(
        string generationDirectory,
        IReadOnlyList<ExternalSourceRepositoryCacheFileEntry> expectedFiles)
    {
        var contentDirectory = ExternalSourceRepositoryCacheStorage.ResolveSafePath(
            generationDirectory,
            ExternalSourceRepositoryCacheContract.ContentDirectoryName);
        ExternalSourceRepositoryCacheStorage.EnsureSafeDirectory(contentDirectory);
        var actualPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var expected = expectedFiles.ToDictionary(file => file.RelativePath, StringComparer.OrdinalIgnoreCase);
        ExternalSourceRepositoryCacheStorage.WalkFiles(
            contentDirectory,
            (filePath, relativePath) =>
            {
                if (!actualPaths.Add(relativePath)
                    || !expected.TryGetValue(relativePath, out var expectedFile))
                {
                    throw new InvalidDataException("Cacheinventar und Inhalt stimmen nicht überein.");
                }

                ValidateFileHash(filePath, expectedFile);
            },
            skipOwnershipMarkers: false,
            CancellationToken.None);
        if (actualPaths.Count != expected.Count || !actualPaths.SetEquals(expected.Keys))
        {
            throw new InvalidDataException("Cacheinventar und Inhalt stimmen nicht überein.");
        }
    }

    private static void ValidateFileHash(
        string filePath,
        ExternalSourceRepositoryCacheFileEntry expected)
    {
        using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            ExternalSourceRepositoryCacheContract.FileBufferSize,
            FileOptions.SequentialScan);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[ExternalSourceRepositoryCacheContract.FileBufferSize];
        var length = 0L;
        int read;
        while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
        {
            length = checked(length + read);
            hash.AppendData(buffer, 0, read);
        }

        var contentHash = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
        if (length != expected.Length || !string.Equals(contentHash, expected.ContentHash, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Eine Cachedatei weicht vom Manifest ab.");
        }
    }

    private static string ReadBoundedText(string path, int maxBytes)
    {
        if (new FileInfo(path).Length > maxBytes)
        {
            throw new InvalidDataException("Die Cachemetadaten überschreiten das Größenlimit.");
        }

        return File.ReadAllText(path, Utf8);
    }

    private static string ReadString(JsonElement value) =>
        value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? throw new InvalidDataException("Cachemetadaten enthalten einen leeren String.")
            : throw new InvalidDataException("Cachemetadaten enthalten keinen String.");

    private sealed class ManifestValues
    {
        internal string? SchemaVersion { get; set; }
        internal string? CacheKey { get; set; }
        internal string? RepositoryUrl { get; set; }
        internal string? SolutionPath { get; set; }
        internal string? Revision { get; set; }
        internal string? GenerationName { get; set; }
        internal DateTime CreatedUtc { get; set; }
        internal IReadOnlyList<ExternalSourceRepositoryCacheFileEntry>? Files { get; set; }
    }

    private sealed class FileValues
    {
        internal string? Path { get; set; }
        internal long Length { get; set; } = -1;
        internal string? Hash { get; set; }
    }
}
