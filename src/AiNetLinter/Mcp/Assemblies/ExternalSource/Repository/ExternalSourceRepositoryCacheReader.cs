#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using AiNetLinter.Configuration;

namespace AiNetLinter.Mcp.Assemblies.ExternalSource.Repository;

internal static class ExternalSourceRepositoryCacheReader
{
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

        var generationName = ReadPointer(pointerPath, request.OpenReadStream);
        result = ReadGeneration(request, generationName);
        return true;
    }

    internal static bool TryReadPointer(string pointerPath, out string? generationName)
    {
        generationName = null;
        if (!File.Exists(pointerPath))
        {
            return false;
        }

        generationName = ReadPointer(pointerPath);
        return true;
    }

    internal static ExternalSourceRepositoryCacheReadResult ReadGeneration(
        ExternalSourceRepositoryCacheReadRequest request,
        string? generationName = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        var name = generationName ?? ReadPointer(
            Path.Combine(
                request.EntryDirectory,
                ExternalSourceRepositoryCacheContract.CurrentPointerFileName),
            request.OpenReadStream);
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

        ExternalSourceRepositoryCacheReadSupport.ValidateGenerationLayout(generationDirectory);
        var manifestPath = ExternalSourceRepositoryCacheStorage.ResolveSafePath(
            generationDirectory,
            ExternalSourceRepositoryCacheContract.ManifestFileName);
        var manifest = ReadManifest(manifestPath, request.OpenReadStream);
        ValidateManifestIdentity(request, name, manifest);
        var inventoryPath = ExternalSourceRepositoryCacheStorage.ResolveSafePath(
            generationDirectory,
            ExternalSourceRepositoryCacheContract.InventoryFileName);
        var inventory = ExternalSourceRepositoryCacheReadSupport.ReadInventory(
            inventoryPath,
            request.OpenReadStream);
        ExternalSourceRepositoryCacheReadSupport.ValidateInventory(
            new ExternalSourceRepositoryCacheInventoryValidationParameters
            {
                Request = request,
                GenerationName = name,
                GenerationDirectory = generationDirectory,
                Manifest = manifest,
                Inventory = inventory,
            });
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

    private static string ReadPointer(
        string pointerPath,
        Func<string, Stream>? openReadStream = null)
    {
        ExternalSourceRepositoryCacheStorage.EnsureRegularFile(pointerPath);
        var json = ExternalSourceRepositoryCacheReadSupport.ReadBoundedText(
            pointerPath,
            ExternalSourceRepositoryCacheContract.MaxPointerJsonBytes,
            openReadStream);
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions
        {
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow,
            MaxDepth = ExternalSourceRepositoryCacheContract.MaxPointerJsonDepth,
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

            generation = ExternalSourceRepositoryCacheReadSupport.ReadString(property.Value);
        }

        if (generation is null
            || !ExternalSourceRepositoryCacheContract.IsSafeGenerationName(generation))
        {
            throw new InvalidDataException("Der Current-Pointer enthält eine ungültige Generation.");
        }

        return generation;
    }

    private static ExternalSourceRepositoryCacheManifest ReadManifest(
        string manifestPath,
        Func<string, Stream>? openReadStream = null)
    {
        ExternalSourceRepositoryCacheStorage.EnsureRegularFile(manifestPath);
        var json = ExternalSourceRepositoryCacheReadSupport.ReadBoundedText(
            manifestPath,
            ExternalSourceRepositoryCacheContract.MaxManifestJsonBytes,
            openReadStream);
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions
        {
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow,
            MaxDepth = ExternalSourceRepositoryCacheContract.MaxManifestJsonDepth,
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
                case "cacheSchemaVersion": values.SchemaVersion = ExternalSourceRepositoryCacheReadSupport.ReadString(property.Value); break;
                case "cacheKey": values.CacheKey = ExternalSourceRepositoryCacheReadSupport.ReadString(property.Value); break;
                case "canonicalRepositoryUrl": values.RepositoryUrl = ExternalSourceRepositoryCacheReadSupport.ReadString(property.Value); break;
                case "solutionPath": values.SolutionPath = ExternalSourceRepositoryCacheReadSupport.ReadString(property.Value); break;
                case "loadedRevision": values.Revision = ExternalSourceRepositoryCacheReadSupport.ReadString(property.Value); break;
                case "generationName": values.GenerationName = ExternalSourceRepositoryCacheReadSupport.ReadString(property.Value); break;
                case "createdUtc": values.CreatedUtc = property.Value.GetDateTime(); break;
                case "files": values.Files = ExternalSourceRepositoryCacheReadSupport.ReadFiles(property.Value); break;
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
        if (!ExternalSourceUrlPolicy.TryNormalize(values.RepositoryUrl!, out var normalizedUrl)
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
}
