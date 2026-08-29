#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace AiNetLinter.Mcp.Assemblies;

internal static class ExternalSourceRepositoryCacheMaterializer
{
    internal static string Materialize(
        ExternalSourceRepositoryCacheReadResult readResult,
        ExternalSourceCheckoutOwnership ownership,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(readResult);
        ArgumentNullException.ThrowIfNull(ownership);
        if (!ExternalSourceRepositoryPathGuard.IsOwnedCheckout(ownership))
        {
            throw new InvalidDataException("Der reservierte Checkout ist nicht mehr im Besitz.");
        }

        var expected = readResult.Manifest.Files.ToDictionary(
            file => file.RelativePath,
            StringComparer.OrdinalIgnoreCase);
        var sourceContent = ExternalSourceRepositoryCacheStorage.ResolveSafePath(
            readResult.GenerationPath,
            ExternalSourceRepositoryCacheContract.ContentDirectoryName);
        ExternalSourceRepositoryCacheStorage.EnsureSafeDirectory(sourceContent);
        var copied = new Dictionary<string, ExternalSourceRepositoryCacheFileEntry>(
            StringComparer.OrdinalIgnoreCase);
        ExternalSourceRepositoryCacheStorage.WalkFiles(
            sourceContent,
            (sourcePath, relativePath) => CopyExpectedFile(
                sourcePath,
                relativePath,
                expected,
                copied,
                ownership.CheckoutPath,
                cancellationToken),
            skipOwnershipMarkers: false,
            cancellationToken);

        if (copied.Count != expected.Count || !copied.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase).SetEquals(expected.Keys))
        {
            throw new InvalidDataException("Der Cacheinhalt konnte nicht vollständig materialisiert werden.");
        }

        var solutionPath = ExternalSourceRepositoryCacheStorage.ResolveSafePath(
            ownership.CheckoutPath,
            readResult.Manifest.SolutionPath);
        ExternalSourceRepositoryCacheStorage.EnsureRegularFile(solutionPath);
        if (!ExternalSourceRepositoryPathGuard.IsOwnedCheckout(ownership))
        {
            throw new InvalidDataException("Der reservierte Checkout ist nach der Materialisierung unsicher.");
        }

        return solutionPath;
    }

    private static long CopyExpectedFile(
        string sourcePath,
        string relativePath,
        IReadOnlyDictionary<string, ExternalSourceRepositoryCacheFileEntry> expected,
        IDictionary<string, ExternalSourceRepositoryCacheFileEntry> copied,
        string checkoutPath,
        CancellationToken cancellationToken)
    {
        if (!expected.TryGetValue(relativePath, out var expectedFile)
            || !copied.TryAdd(relativePath, expectedFile))
        {
            throw new InvalidDataException("Der Cacheinhalt stimmt nicht mit dem Inventar überein.");
        }

        var destinationPath = ExternalSourceRepositoryCacheStorage.ResolveSafePath(
            checkoutPath,
            relativePath);
        var destinationDirectory = Path.GetDirectoryName(destinationPath)!;
        Directory.CreateDirectory(destinationDirectory);
        ExternalSourceRepositoryCacheStorage.EnsureSafeDirectory(destinationDirectory);
        var copiedFile = ExternalSourceRepositoryCacheStorage.CopyFile(
            sourcePath,
            destinationPath,
            relativePath,
            cancellationToken);
        if (copiedFile.Length != expectedFile.Length
            || !string.Equals(
                copiedFile.ContentHash,
                expectedFile.ContentHash,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException("Der materialisierte Cacheinhalt weicht vom Inventar ab.");
        }

        return copiedFile.Length;
    }
}
