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
                var file = CopyFile(sourcePath, destinationPath, relativePath, cancellationToken);
                files.Add(file);
                return file.Length;
            },
            skipOwnershipMarkers: true,
            cancellationToken);
        files.Sort(static (left, right) =>
            StringComparer.Ordinal.Compare(left.RelativePath, right.RelativePath));
        return files;
    }
    internal static void WalkFiles(
        string sourceRoot,
        Func<string, string, long> onFile,
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
        Func<string, string, long> onFile,
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
        Func<string, string, long> onFile,
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
        Func<string, string, long> onFile,
        ref int fileCount,
        ref long totalBytes)
    {
        if (fileCount >= ExternalSourceRepositoryCacheContract.MaxInventoryEntries)
        {
            throw new InvalidDataException("Der Source-Checkout überschreitet das Cache-Limit.");
        }
        var length = onFile(filePath, relativePath);
        if (length < 0
            || length > ExternalSourceRepositoryCacheContract.MaxFileLength
            || totalBytes > ExternalSourceRepositoryCacheContract.MaxInventoryBytes - length)
        {
            throw new InvalidDataException("Der Source-Checkout überschreitet das Cache-Limit.");
        }
        fileCount++;
        totalBytes += length;
    }

    internal static ExternalSourceRepositoryCacheFileEntry CopyFile(
        string sourcePath,
        string destinationPath,
        string relativePath,
        CancellationToken cancellationToken)
    {
        if (File.Exists(destinationPath) || Directory.Exists(destinationPath))
        {
            throw new IOException("Die Cachegeneration enthält bereits einen Zielpfad.");
        }

        EnsureRegularFile(sourcePath);
        using var source = new FileStream(
            sourcePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            ExternalSourceRepositoryCacheContract.FileBufferSize,
            FileOptions.SequentialScan);
        if (source.Length > ExternalSourceRepositoryCacheContract.MaxFileLength)
        {
            throw new InvalidDataException("Eine Datei überschreitet das Cache-Limit.");
        }
        using var destination = new FileStream(
            destinationPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            ExternalSourceRepositoryCacheContract.FileBufferSize,
            FileOptions.SequentialScan | FileOptions.WriteThrough);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var length = CopyBoundedFile(source, destination, hash, cancellationToken);

        destination.Flush(flushToDisk: true);
        if (source.Length != length)
        {
            throw new InvalidDataException("Eine Datei wurde während des Kopierens verändert.");
        }

        return new(
            relativePath,
            length,
            Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant());
    }

    private static long CopyBoundedFile(
        FileStream source,
        FileStream destination,
        IncrementalHash hash,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[ExternalSourceRepositoryCacheContract.FileBufferSize];
        var length = 0L;
        while (true)
        {
            var remaining = ExternalSourceRepositoryCacheContract.MaxFileLength - length;
            var readCount = (int)Math.Min(buffer.Length, remaining + 1);
            var read = source.Read(buffer, 0, readCount);
            if (read == 0)
            {
                return length;
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (read > remaining)
            {
                throw new InvalidDataException("Eine Datei überschreitet das Cache-Limit.");
            }

            destination.Write(buffer, 0, read);
            hash.AppendData(buffer, 0, read);
            length = checked(length + read);
        }
    }

    internal static bool TryPublishPointer(
        string entryDirectory,
        string generationName,
        string? expectedCurrentGeneration,
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
                if (!HasExpectedCurrentPointer(pointerPath, expectedCurrentGeneration))
                {
                    failure = ExternalSourceRepositoryCachePublishResult.Failure(
                        expectedCurrentGeneration is null
                            ? ExternalSourceRepositoryCachePublishFailureKind.PointerPublishFailed
                            : ExternalSourceRepositoryCachePublishFailureKind.CurrentChanged);
                    return false;
                }

                if (ContainsReparsePoint(pointerPath))
                {
                    throw new InvalidDataException("Der Current-Pointer ist unsicher.");
                }

                File.Replace(temporaryPointer, pointerPath, null, ignoreMetadataErrors: true);
            }
            else
            {
                if (expectedCurrentGeneration is not null)
                {
                    failure = ExternalSourceRepositoryCachePublishResult.Failure(
                        ExternalSourceRepositoryCachePublishFailureKind.CurrentChanged);
                    return false;
                }

                if (Directory.Exists(pointerPath))
                {
                    throw new IOException("Der Current-Pointer ist kein Dateipfad.");
                }

                File.Move(temporaryPointer, pointerPath);
            }

            return true;
        }
        catch (Exception ignored) when (IsCacheException(ignored))
        {
            failure = ExternalSourceRepositoryCachePublishResult.Failure(
                ExternalSourceRepositoryCachePublishFailureKind.PointerPublishFailed);
            return false;
        }
        finally
        {
            ExternalSourceRepositoryCacheCleanup.TryDeleteFile(temporaryPointer);
        }
    }

    private static bool HasExpectedCurrentPointer(
        string pointerPath,
        string? expectedCurrentGeneration) =>
        ExternalSourceRepositoryCacheReader.TryReadPointer(
            pointerPath,
            out var currentGeneration)
        && string.Equals(
            currentGeneration,
            expectedCurrentGeneration,
            StringComparison.Ordinal);
    internal static void RestorePreviousCurrent(
        string entryDirectory,
        string failedGeneration,
        string? previousGeneration)
    {
        try
        {
            var pointerPath = Path.Combine(
                entryDirectory,
                ExternalSourceRepositoryCacheContract.CurrentPointerFileName);
            if (!ExternalSourceRepositoryCacheReader.TryReadPointer(
                    pointerPath,
                    out var currentGeneration)
                || !string.Equals(
                    currentGeneration,
                    failedGeneration,
                    StringComparison.Ordinal))
            {
                return;
            }

            if (previousGeneration is null)
            {
                ExternalSourceRepositoryCacheCleanup.TryDeleteFile(pointerPath);
                return;
            }

            _ = TryPublishPointer(
                entryDirectory,
                previousGeneration,
                failedGeneration,
                out _);
        }
        catch (Exception ignored) when (IsCacheException(ignored))
        {
        }
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
            var generationName = Path.GetFileName(generationDirectory);
            if (Directory.Exists(generationDirectory)
                && ExternalSourceRepositoryPathGuard.IsDescendantPath(entryDirectory, generationDirectory)
                && IsSafeGenerationName(generationName)
                && !IsCurrentGeneration(entryDirectory, generationName)
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

    private static bool IsCurrentGeneration(string entryDirectory, string generationName)
    {
        var pointerPath = Path.Combine(
            entryDirectory,
            ExternalSourceRepositoryCacheContract.CurrentPointerFileName);
        return ExternalSourceRepositoryCacheReader.TryReadPointer(
            pointerPath,
            out var currentGeneration)
            && string.Equals(currentGeneration, generationName, StringComparison.Ordinal);
    }

    private static bool IsSafeGenerationName(string? value) =>
        value is not null
        && ExternalSourceRepositoryCacheContract.IsSafeGenerationName(value);

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

}

internal sealed class ExternalSourceRepositoryCacheUnsafeSourceException : Exception;
