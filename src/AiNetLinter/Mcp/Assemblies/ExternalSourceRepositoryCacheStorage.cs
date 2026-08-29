#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using AiNetLinter.Configuration;

namespace AiNetLinter.Mcp.Assemblies;

internal static class ExternalSourceRepositoryCacheStorage
{
    internal static IReadOnlyList<ExternalSourceRepositoryCacheFileEntry> CopySource(
        string sourceRoot,
        string generationDirectory,
        CancellationToken cancellationToken)
    {
        var contentDirectory = Path.Combine(
            generationDirectory,
            ExternalSourceRepositoryCacheContract.ContentDirectoryName);
        Directory.CreateDirectory(contentDirectory);
        EnsureSafeDirectory(contentDirectory);
        var files = new List<ExternalSourceRepositoryCacheFileEntry>();
        WalkFiles(
            sourceRoot,
            (sourcePath, relativePath) =>
            {
                var destinationPath = ResolveSafePath(contentDirectory, relativePath);
                var destinationDirectory = Path.GetDirectoryName(destinationPath)!;
                Directory.CreateDirectory(destinationDirectory);
                EnsureSafeDirectory(destinationDirectory);
                files.Add(CopyFile(sourcePath, destinationPath, relativePath, cancellationToken));
            },
            skipOwnershipMarkers: true,
            cancellationToken);
        files.Sort(static (left, right) =>
            StringComparer.Ordinal.Compare(left.RelativePath, right.RelativePath));
        return files;
    }

    internal static void WalkFiles(
        string sourceRoot,
        Action<string, string> onFile,
        bool skipOwnershipMarkers,
        CancellationToken cancellationToken)
    {
        EnsureSafeDirectory(sourceRoot);
        var fileCount = 0;
        var totalBytes = 0L;
        WalkDirectory(
            sourceRoot,
            sourceRoot,
            onFile,
            skipOwnershipMarkers,
            ref fileCount,
            ref totalBytes,
            cancellationToken);
    }

    private static void WalkDirectory(
        string sourceRoot,
        string currentDirectory,
        Action<string, string> onFile,
        bool skipOwnershipMarkers,
        ref int fileCount,
        ref long totalBytes,
        CancellationToken cancellationToken)
    {
        EnsureSafeDirectory(currentDirectory);
        var entries = Directory.GetFileSystemEntries(currentDirectory);
        Array.Sort(entries, StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries)
        {
            VisitEntry(
                sourceRoot,
                entry,
                onFile,
                skipOwnershipMarkers,
                ref fileCount,
                ref totalBytes,
                cancellationToken);
        }
    }

    private static void VisitEntry(
        string sourceRoot,
        string entry,
        Action<string, string> onFile,
        bool skipOwnershipMarkers,
        ref int fileCount,
        ref long totalBytes,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var relativePath = Path.GetRelativePath(sourceRoot, entry).Replace('\\', '/');
        if (!ExternalSourceRepositoryCacheContract.TryNormalizeRelativeFilePath(
                relativePath,
                out var normalizedPath))
        {
            throw new InvalidDataException("Der Source-Checkout enthält einen unsicheren Pfad.");
        }

        if (ContainsReparsePoint(entry))
        {
            throw new InvalidDataException("Der Source-Checkout enthält einen Reparse-Punkt.");
        }

        if (skipOwnershipMarkers
            && string.Equals(
                Path.GetFileName(normalizedPath),
                ExternalSourceCheckoutOwnership.OwnershipMarkerFileName,
                StringComparison.OrdinalIgnoreCase)
            && File.Exists(entry))
        {
            return;
        }

        if (Directory.Exists(entry))
        {
            WalkDirectory(
                sourceRoot,
                entry,
                onFile,
                skipOwnershipMarkers,
                ref fileCount,
                ref totalBytes,
                cancellationToken);
            return;
        }

        if (!File.Exists(entry))
        {
            throw new InvalidDataException("Der Source-Checkout enthält einen unbekannten Dateisystemeintrag.");
        }

        AddFile(entry, normalizedPath!, onFile, ref fileCount, ref totalBytes);
    }

    private static void AddFile(
        string filePath,
        string relativePath,
        Action<string, string> onFile,
        ref int fileCount,
        ref long totalBytes)
    {
        var length = new FileInfo(filePath).Length;
        if (length > ExternalSourceRepositoryCacheContract.MaxFileLength
            || fileCount >= ExternalSourceRepositoryCacheContract.MaxInventoryEntries
            || totalBytes > ExternalSourceRepositoryCacheContract.MaxInventoryBytes - length)
        {
            throw new InvalidDataException("Der Source-Checkout überschreitet das Cache-Limit.");
        }

        fileCount++;
        totalBytes += length;
        onFile(filePath, relativePath);
    }

    private static ExternalSourceRepositoryCacheFileEntry CopyFile(
        string sourcePath,
        string destinationPath,
        string relativePath,
        CancellationToken cancellationToken)
    {
        if (File.Exists(destinationPath) || Directory.Exists(destinationPath))
        {
            throw new IOException("Die Cachegeneration enthält bereits einen Zielpfad.");
        }

        using var source = new FileStream(
            sourcePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            ExternalSourceRepositoryCacheContract.FileBufferSize,
            FileOptions.SequentialScan);
        using var destination = new FileStream(
            destinationPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            ExternalSourceRepositoryCacheContract.FileBufferSize,
            FileOptions.SequentialScan | FileOptions.WriteThrough);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[ExternalSourceRepositoryCacheContract.FileBufferSize];
        var length = 0L;
        int read;
        while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            destination.Write(buffer, 0, read);
            hash.AppendData(buffer, 0, read);
            length = checked(length + read);
        }

        destination.Flush(flushToDisk: true);
        if (length > ExternalSourceRepositoryCacheContract.MaxFileLength)
        {
            throw new InvalidDataException("Eine Datei überschreitet das Cache-Limit.");
        }

        return new(
            relativePath,
            length,
            Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant());
    }

    internal static void WriteManifest(
        string generationDirectory,
        ExternalSourceRepositoryCacheManifest manifest)
    {
        var manifestPath = ResolveSafePath(
            generationDirectory,
            ExternalSourceRepositoryCacheContract.ManifestFileName);
        using var stream = new FileStream(
            manifestPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            ExternalSourceRepositoryCacheContract.FileBufferSize,
            FileOptions.WriteThrough);
        JsonSerializer.Serialize(stream, manifest, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
        });
        stream.Flush(flushToDisk: true);
    }

    internal static bool TryPublishPointer(
        string entryDirectory,
        string generationName,
        out ExternalSourceRepositoryCachePublishResult? failure)
    {
        failure = null;
        var pointerPath = Path.Combine(
            entryDirectory,
            ExternalSourceRepositoryCacheContract.CurrentPointerFileName);
        var temporaryPointer = pointerPath + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            WritePointer(temporaryPointer, generationName);
            if (File.Exists(pointerPath))
            {
                if (ContainsReparsePoint(pointerPath))
                {
                    throw new InvalidDataException("Der Current-Pointer ist unsicher.");
                }

                File.Replace(temporaryPointer, pointerPath, null, ignoreMetadataErrors: true);
            }
            else
            {
                if (Directory.Exists(pointerPath))
                {
                    throw new IOException("Der Current-Pointer ist kein Dateipfad.");
                }

                File.Move(temporaryPointer, pointerPath);
            }

            return true;
        }
        catch (Exception exception) when (IsCacheException(exception))
        {
            failure = ExternalSourceRepositoryCachePublishResult.Failure(
                ExternalSourceRepositoryCachePublishFailureKind.PointerPublishFailed);
            return false;
        }
        finally
        {
            TryDeleteFile(temporaryPointer);
        }
    }

    internal static void RestorePreviousCurrent(
        string entryDirectory,
        string? previousGeneration)
    {
        var pointerPath = Path.Combine(
            entryDirectory,
            ExternalSourceRepositoryCacheContract.CurrentPointerFileName);
        if (previousGeneration is null)
        {
            TryDeleteFile(pointerPath);
            return;
        }

        _ = TryPublishPointer(entryDirectory, previousGeneration, out _);
    }

    internal static void PrepareDirectory(string directory)
    {
        if (File.Exists(directory))
        {
            throw new IOException("Die Cache-Wurzel ist kein Verzeichnis.");
        }

        Directory.CreateDirectory(directory);
        EnsureSafeDirectory(directory);
    }

    internal static void PrepareEntryDirectory(string cacheRoot, string entryDirectory)
    {
        if (!ExternalSourceRepositoryPathGuard.IsDescendantPath(cacheRoot, entryDirectory))
        {
            throw new InvalidDataException("Der Cacheeintrag liegt außerhalb der Cache-Wurzel.");
        }

        Directory.CreateDirectory(entryDirectory);
        EnsureSafeDirectory(entryDirectory);
    }

    internal static void ValidateSourceCheckout(
        ExternalSourceRepositoryCachePublishRequest request,
        ExternalSourceRepositoryCacheKey key)
    {
        var ownership = request.CheckoutOwnership;
        if (!ExternalSourceRepositoryPathGuard.IsOwnedCheckout(ownership)
            || ExternalSourceRepositoryPathGuard.ContainsReparsePointInTree(ownership.CheckoutPath))
        {
            throw new ExternalSourceRepositoryCacheUnsafeSourceException();
        }

        var solutionPath = ResolveSafePath(ownership.CheckoutPath, key.SolutionPath);
        if (!File.Exists(solutionPath)
            || ContainsReparsePoint(solutionPath)
            || !string.Equals(
                solutionPath,
                request.Checkout.SolutionPath,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ExternalSourceRepositoryCacheUnsafeSourceException();
        }
    }

    internal static string ResolveSafePath(string root, string relativePath)
    {
        if (!ExternalSourceRepositoryCacheContract.TryNormalizeRelativeFilePath(
                relativePath,
                out var normalizedPath))
        {
            throw new InvalidDataException("Der Cache enthält einen unsicheren relativen Pfad.");
        }

        var fullRoot = Path.GetFullPath(root);
        var candidate = Path.GetFullPath(Path.Combine(
            fullRoot,
            normalizedPath!.Replace('/', Path.DirectorySeparatorChar)));
        if (!ExternalSourceRepositoryPathGuard.IsDescendantPath(fullRoot, candidate))
        {
            throw new InvalidDataException("Der Cachepfad liegt außerhalb seines kontrollierten Roots.");
        }

        return candidate;
    }

    internal static void EnsureSafeDirectory(string path)
    {
        if (File.Exists(path)
            || !Directory.Exists(path)
            || ContainsReparsePoint(path))
        {
            throw new InvalidDataException("Ein kontrollierter Cachepfad ist unsicher.");
        }
    }

    internal static void EnsureRegularFile(string path)
    {
        if (!File.Exists(path)
            || Directory.Exists(path)
            || ContainsReparsePoint(path))
        {
            throw new InvalidDataException("Ein kontrollierter Cachedateipfad ist unsicher.");
        }
    }

    internal static void TryDeleteGeneration(string entryDirectory, string generationDirectory)
    {
        try
        {
            if (Directory.Exists(generationDirectory)
                && ExternalSourceRepositoryPathGuard.IsDescendantPath(entryDirectory, generationDirectory)
                && !ContainsReparsePoint(generationDirectory)
                && !ExternalSourceRepositoryPathGuard.ContainsReparsePointInTree(generationDirectory))
            {
                Directory.Delete(generationDirectory, recursive: true);
            }
        }
        catch (Exception ignored) when (IsCacheException(ignored))
        {
        }
    }

    internal static bool IsCacheException(Exception exception) =>
        exception is IOException
            or UnauthorizedAccessException
            or InvalidDataException
            or JsonException
            or InvalidOperationException
            or ArgumentException
            or NotSupportedException
            or OverflowException
            or FormatException;

    private static bool ContainsReparsePoint(string path) =>
        ExternalSourceRepositoryPathGuard.ContainsReparsePointOnPath(path);

    private static void WritePointer(string path, string generationName)
    {
        using var stream = new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            ExternalSourceRepositoryCacheContract.FileBufferSize,
            FileOptions.WriteThrough);
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
        writer.WriteStartObject();
        writer.WriteString(
            ExternalSourceRepositoryCacheContract.PointerGenerationPropertyName,
            generationName);
        writer.WriteEndObject();
        writer.Flush();
        stream.Flush(flushToDisk: true);
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path) && !ContainsReparsePoint(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ignored) when (IsCacheException(ignored))
        {
        }
    }
}

internal sealed class ExternalSourceRepositoryCacheUnsafeSourceException : Exception;
