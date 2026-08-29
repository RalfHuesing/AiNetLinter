#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace AiNetLinter.Mcp.Assemblies;

internal static class ExternalSourceRepositoryCacheMetadataStorage
{
    internal static void WriteManifest(
        string generationDirectory,
        ExternalSourceRepositoryCacheManifest manifest)
    {
        var manifestPath = ExternalSourceRepositoryCacheStorage.ResolveSafePath(
            generationDirectory,
            ExternalSourceRepositoryCacheContract.ManifestFileName);
        WriteJson(manifestPath, manifest);
    }

    internal static void WriteInventory(
        string generationDirectory,
        ExternalSourceRepositoryCacheKey key,
        string generationName,
        IReadOnlyList<ExternalSourceRepositoryCacheFileEntry> files)
    {
        var totalBytes = 0L;
        foreach (var file in files)
        {
            totalBytes = checked(totalBytes + file.Length);
        }

        var inventory = new ExternalSourceRepositoryCacheInventory(
            key.SchemaVersion,
            key.StableValue,
            key.SolutionPath,
            generationName,
            files.Count,
            totalBytes,
            files);
        var inventoryPath = ExternalSourceRepositoryCacheStorage.ResolveSafePath(
            generationDirectory,
            ExternalSourceRepositoryCacheContract.InventoryFileName);
        WriteJson(inventoryPath, inventory);
    }

    private static void WriteJson<T>(string path, T value)
    {
        using var stream = new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            ExternalSourceRepositoryCacheContract.FileBufferSize,
            FileOptions.WriteThrough);
        JsonSerializer.Serialize(stream, value, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
        });
        stream.Flush(flushToDisk: true);
    }
}
