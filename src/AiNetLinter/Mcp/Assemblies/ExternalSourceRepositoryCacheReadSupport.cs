#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace AiNetLinter.Mcp.Assemblies;

internal static class ExternalSourceRepositoryCacheReadSupport
{
    private static readonly UTF8Encoding Utf8 = new(false, true);

    internal static ExternalSourceRepositoryCacheInventory ReadInventory(
        string inventoryPath,
        Func<string, Stream>? openReadStream = null)
    {
        ExternalSourceRepositoryCacheStorage.EnsureRegularFile(inventoryPath);
        var json = ReadBoundedText(
            inventoryPath,
            ExternalSourceRepositoryCacheContract.MaxInventoryJsonBytes,
            openReadStream);
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions
        {
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow,
            MaxDepth = ExternalSourceRepositoryCacheContract.MaxInventoryJsonDepth,
        });
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("Das Cacheinventar ist kein JSON-Objekt.");
        }

        var values = ReadInventoryProperties(document.RootElement);
        ValidateInventoryValues(values);
        return new ExternalSourceRepositoryCacheInventory(
            values.SchemaVersion!,
            values.CacheKey!,
            values.SolutionPath!,
            values.GenerationName!,
            values.FileCount,
            values.TotalBytes,
            values.Files!);
    }

    internal static IReadOnlyList<ExternalSourceRepositoryCacheFileEntry> ReadFiles(JsonElement value)
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

    internal static void ValidateGenerationLayout(string generationDirectory)
    {
        var allowedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ExternalSourceRepositoryCacheContract.ContentDirectoryName,
            ExternalSourceRepositoryCacheContract.ManifestFileName,
            ExternalSourceRepositoryCacheContract.InventoryFileName,
        };
        foreach (var entry in Directory.GetFileSystemEntries(generationDirectory))
        {
            if (ExternalSourceRepositoryPathGuard.ContainsReparsePointOnPath(entry)
                || !allowedNames.Contains(Path.GetFileName(entry)))
            {
                throw new InvalidDataException("Die Cachegeneration enthält einen unbekannten Eintrag.");
            }
        }
    }

    internal static void ValidateInventory(
        ExternalSourceRepositoryCacheInventoryValidationParameters parameters)
    {
        ValidateInventoryIdentity(parameters);
        var expectedSolutionPath = GetExpectedSolutionPath(parameters.Request);
        ValidateManifestAndInventory(parameters.Manifest.Files, parameters.Inventory.Files);
        var expected = CreateExpectedFiles(parameters.Inventory.Files);
        ValidateExpectedSolution(expected, expectedSolutionPath);
        ValidateContent(parameters.GenerationDirectory, expected, expectedSolutionPath);
    }

    private static void ValidateInventoryIdentity(
        ExternalSourceRepositoryCacheInventoryValidationParameters parameters)
    {
        var inventory = parameters.Inventory;
        var key = parameters.Request.Key;
        if (!string.Equals(inventory.CacheSchemaVersion, key.SchemaVersion, StringComparison.Ordinal)
            || !string.Equals(inventory.CacheKey, key.StableValue, StringComparison.Ordinal)
            || !string.Equals(inventory.SolutionPath, key.SolutionPath, StringComparison.Ordinal)
            || !string.Equals(inventory.GenerationName, parameters.GenerationName, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Das Cacheinventar ist inkompatibel.");
        }
    }

    private static string GetExpectedSolutionPath(ExternalSourceRepositoryCacheReadRequest request)
    {
        var expectedSolutionPath = request.ExpectedSolutionPath ?? request.Key.SolutionPath;
        if (!ExternalSourceRepositoryCacheKey.TryNormalizeSolutionPath(
                expectedSolutionPath,
                out var normalizedSolutionPath)
            || !string.Equals(normalizedSolutionPath, expectedSolutionPath, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Der erwartete Solution-Pfad ist unsicher.");
        }

        return expectedSolutionPath;
    }

    private static Dictionary<string, ExternalSourceRepositoryCacheFileEntry> CreateExpectedFiles(
        IReadOnlyList<ExternalSourceRepositoryCacheFileEntry> files) =>
        files.ToDictionary(file => file.RelativePath, StringComparer.OrdinalIgnoreCase);

    private static void ValidateExpectedSolution(
        IReadOnlyDictionary<string, ExternalSourceRepositoryCacheFileEntry> expected,
        string expectedSolutionPath)
    {
        if (!expected.TryGetValue(expectedSolutionPath, out var expectedSolution)
            || !string.Equals(expectedSolution.RelativePath, expectedSolutionPath, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Der erwartete Solution-Pfad fehlt im Cacheinventar.");
        }
    }

    private static void ValidateContent(
        string generationDirectory,
        IReadOnlyDictionary<string, ExternalSourceRepositoryCacheFileEntry> expected,
        string expectedSolutionPath)
    {
        var contentDirectory = ExternalSourceRepositoryCacheStorage.ResolveSafePath(
            generationDirectory,
            ExternalSourceRepositoryCacheContract.ContentDirectoryName);
        ExternalSourceRepositoryCacheStorage.EnsureSafeDirectory(contentDirectory);
        var actualPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        ExternalSourceRepositoryCacheStorage.WalkFiles(
            contentDirectory,
            (filePath, relativePath) =>
            {
                if (!actualPaths.Add(relativePath)
                    || !expected.TryGetValue(relativePath, out var expectedFile))
                {
                    throw new InvalidDataException("Cacheinventar und Inhalt stimmen nicht überein.");
                }

                return ValidateFileHash(filePath, expectedFile);
            },
            skipOwnershipMarkers: false,
            CancellationToken.None);
        if (actualPaths.Count != expected.Count
            || !actualPaths.SetEquals(expected.Keys)
            || !actualPaths.Contains(expectedSolutionPath))
        {
            throw new InvalidDataException("Cacheinventar und Inhalt stimmen nicht überein.");
        }
    }

    internal static string ReadBoundedText(
        string path,
        int maxBytes,
        Func<string, Stream>? openReadStream = null)
    {
        ExternalSourceRepositoryCacheStorage.EnsureRegularFile(path);
        using var stream = openReadStream?.Invoke(path) ?? new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            ExternalSourceRepositoryCacheContract.FileBufferSize,
            FileOptions.SequentialScan);
        if (stream.Length > maxBytes)
        {
            throw new InvalidDataException("Die Cachemetadaten überschreiten das Größenlimit.");
        }

        using var contents = new MemoryStream(Math.Min(maxBytes, ExternalSourceRepositoryCacheContract.FileBufferSize));
        var buffer = new byte[ExternalSourceRepositoryCacheContract.FileBufferSize];
        var totalBytes = 0L;
        while (true)
        {
            var remaining = maxBytes - totalBytes;
            var read = stream.Read(
                buffer,
                0,
                (int)Math.Min(buffer.Length, remaining + 1));
            if (read == 0)
            {
                break;
            }

            if (read > remaining)
            {
                throw new InvalidDataException("Die Cachemetadaten überschreiten das Größenlimit.");
            }

            contents.Write(buffer, 0, read);
            totalBytes = checked(totalBytes + read);
        }

        if (stream.Length != totalBytes)
        {
            throw new InvalidDataException("Die Cachemetadaten wurden während des Lesens verändert.");
        }

        return Utf8.GetString(
            contents.GetBuffer(),
            0,
            checked((int)contents.Length));
    }

    internal static string ReadString(JsonElement value) =>
        value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? throw new InvalidDataException("Cachemetadaten enthalten einen leeren String.")
            : throw new InvalidDataException("Cachemetadaten enthalten keinen String.");

    private static InventoryValues ReadInventoryProperties(JsonElement root)
    {
        var values = new InventoryValues();
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in root.EnumerateObject())
        {
            if (!names.Add(property.Name))
            {
                throw new InvalidDataException("Das Cacheinventar enthält doppelte Felder.");
            }

            switch (property.Name)
            {
                case "cacheSchemaVersion": values.SchemaVersion = ReadString(property.Value); break;
                case "cacheKey": values.CacheKey = ReadString(property.Value); break;
                case "solutionPath": values.SolutionPath = ReadString(property.Value); break;
                case "generationName": values.GenerationName = ReadString(property.Value); break;
                case "fileCount": values.FileCount = property.Value.GetInt32(); break;
                case "totalBytes": values.TotalBytes = property.Value.GetInt64(); break;
                case "files": values.Files = ReadFiles(property.Value); break;
                default: throw new InvalidDataException("Das Cacheinventar enthält ein unbekanntes Feld.");
            }
        }

        return values;
    }

    private static void ValidateInventoryValues(InventoryValues values)
    {
        ValidateInventoryRequiredValues(values);
        if (values.CacheKey!.Length != 64
            || values.CacheKey.Any(character => !ExternalSourceRepositoryCacheContract.IsLowerHexDigit(character))
            || !ExternalSourceRepositoryCacheContract.IsSafeGenerationName(values.GenerationName!)
            || !ExternalSourceRepositoryCacheKey.TryNormalizeSolutionPath(
                values.SolutionPath!,
                out var normalizedSolutionPath)
            || !string.Equals(normalizedSolutionPath, values.SolutionPath, StringComparison.Ordinal)
            || values.FileCount != values.Files!.Count)
        {
            throw new InvalidDataException("Das Cacheinventar enthält ungültige Identitäts- oder Grenzwerte.");
        }

        var totalBytes = 0L;
        foreach (var file in values.Files)
        {
            totalBytes = checked(totalBytes + file.Length);
        }

        if (totalBytes != values.TotalBytes)
        {
            throw new InvalidDataException("Die Gesamtgröße des Cacheinventars stimmt nicht überein.");
        }
    }

    private static void ValidateInventoryRequiredValues(InventoryValues values)
    {
        if (values.SchemaVersion is null
            || values.CacheKey is null
            || values.SolutionPath is null
            || values.GenerationName is null
            || values.Files is null
            || values.FileCount < 0
            || values.FileCount > ExternalSourceRepositoryCacheContract.MaxInventoryEntries
            || values.TotalBytes < 0
            || values.TotalBytes > ExternalSourceRepositoryCacheContract.MaxInventoryBytes)
        {
            throw new InvalidDataException("Das Cacheinventar ist unvollständig oder unsicher.");
        }
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

    private static void ValidateManifestAndInventory(
        IReadOnlyList<ExternalSourceRepositoryCacheFileEntry> manifestFiles,
        IReadOnlyList<ExternalSourceRepositoryCacheFileEntry> inventoryFiles)
    {
        if (manifestFiles.Count != inventoryFiles.Count)
        {
            throw new InvalidDataException("Manifest und unabhängiges Cacheinventar stimmen nicht überein.");
        }

        var inventory = inventoryFiles.ToDictionary(
            file => file.RelativePath,
            StringComparer.OrdinalIgnoreCase);
        foreach (var manifestFile in manifestFiles)
        {
            if (!inventory.TryGetValue(manifestFile.RelativePath, out var inventoryFile)
                || !string.Equals(
                    manifestFile.RelativePath,
                    inventoryFile.RelativePath,
                    StringComparison.Ordinal)
                || manifestFile.Length != inventoryFile.Length
                || !string.Equals(
                    manifestFile.ContentHash,
                    inventoryFile.ContentHash,
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException("Manifest und unabhängiges Cacheinventar stimmen nicht überein.");
            }
        }
    }

    private static long ValidateFileHash(
        string filePath,
        ExternalSourceRepositoryCacheFileEntry expected)
    {
        ExternalSourceRepositoryCacheStorage.EnsureRegularFile(filePath);
        using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            ExternalSourceRepositoryCacheContract.FileBufferSize,
            FileOptions.SequentialScan);
        if (stream.Length > ExternalSourceRepositoryCacheContract.MaxFileLength)
        {
            throw new InvalidDataException("Eine Cachedatei überschreitet das Größenlimit.");
        }

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[ExternalSourceRepositoryCacheContract.FileBufferSize];
        var length = 0L;
        while (length < expected.Length)
        {
            var remaining = expected.Length - length;
            var read = stream.Read(
                buffer,
                0,
                (int)Math.Min(buffer.Length, remaining));
            if (read == 0)
            {
                throw new InvalidDataException("Eine Cachedatei ist unerwartet verkürzt.");
            }

            length = checked(length + read);
            hash.AppendData(buffer, 0, read);
        }

        if (stream.ReadByte() >= 0 || stream.Length != length)
        {
            throw new InvalidDataException("Eine Cachedatei ist unerwartet gewachsen.");
        }

        var contentHash = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
        if (length != expected.Length || !string.Equals(contentHash, expected.ContentHash, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Eine Cachedatei weicht vom Manifest ab.");
        }

        return length;
    }

    private sealed class FileValues
    {
        internal string? Path { get; set; }
        internal long Length { get; set; } = -1;
        internal string? Hash { get; set; }
    }

    private sealed class InventoryValues
    {
        internal string? SchemaVersion { get; set; }
        internal string? CacheKey { get; set; }
        internal string? SolutionPath { get; set; }
        internal string? GenerationName { get; set; }
        internal int FileCount { get; set; } = -1;
        internal long TotalBytes { get; set; } = -1;
        internal IReadOnlyList<ExternalSourceRepositoryCacheFileEntry>? Files { get; set; }
    }

}
