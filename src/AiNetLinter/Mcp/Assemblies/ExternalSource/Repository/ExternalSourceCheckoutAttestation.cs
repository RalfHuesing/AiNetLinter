#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace AiNetLinter.Mcp.Assemblies.ExternalSource.Repository;

internal sealed record ExternalSourceCheckoutVerification(
    ExternalSourceCheckoutTrust Trust)
{
    internal bool IsVerified => Trust is ExternalSourceCheckoutTrust.Clean;

    internal static ExternalSourceCheckoutVerification Clean { get; } =
        new(ExternalSourceCheckoutTrust.Clean);

    internal static ExternalSourceCheckoutVerification Dirty { get; } =
        new(ExternalSourceCheckoutTrust.Dirty);

    internal static ExternalSourceCheckoutVerification Unverified { get; } =
        new(ExternalSourceCheckoutTrust.Unverified);
}

internal sealed class ExternalSourceCheckoutAttestation
{
    private readonly string expectedCheckoutPath;
    private readonly Func<ExternalSourceCheckoutOwnership, CancellationToken, ValueTask<ExternalSourceCheckoutVerification>> verifier;

    private ExternalSourceCheckoutAttestation(
        string expectedCheckoutPath,
        string expectedRevision,
        Func<ExternalSourceCheckoutOwnership, CancellationToken, ValueTask<ExternalSourceCheckoutVerification>> verifier)
    {
        this.expectedCheckoutPath = Path.GetFullPath(expectedCheckoutPath);
        ExpectedRevision = expectedRevision;
        this.verifier = verifier;
    }

    internal string ExpectedRevision { get; }

    internal static async ValueTask<ExternalSourceCheckoutVerification> VerifyCheckoutAsync(
        ExternalSourceCheckoutHandle checkout,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(checkout);
        var attestation = checkout.CheckoutAttestation;
        if (attestation is null
            || !string.Equals(
                attestation.ExpectedRevision,
                checkout.LoadedRevision,
                StringComparison.Ordinal))
        {
            return ExternalSourceCheckoutVerification.Unverified;
        }

        using var materializationUse = checkout.TryAcquireMaterializationUse(cancellationToken);
        if (materializationUse is null)
        {
            return ExternalSourceCheckoutVerification.Unverified;
        }

        return await attestation.VerifyAsync(
                checkout.Ownership,
                cancellationToken)
            .ConfigureAwait(false);
    }

    internal async ValueTask<ExternalSourceCheckoutVerification> VerifyAsync(
        ExternalSourceCheckoutOwnership ownership,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ownership);
        cancellationToken.ThrowIfCancellationRequested();
        if (!string.Equals(
                Path.GetFullPath(ownership.CheckoutPath),
                expectedCheckoutPath,
                StringComparison.OrdinalIgnoreCase)
            || !ExternalSourceRepositoryPathGuard.IsOwnedCheckout(ownership))
        {
            return ExternalSourceCheckoutVerification.Unverified;
        }

        try
        {
            return await verifier(ownership, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            _ = exception;
            return ExternalSourceCheckoutVerification.Unverified;
        }
    }

    internal static ExternalSourceCheckoutAttestation FromTransport(
        string checkoutPath,
        string revision,
        Func<string, CancellationToken, Task<ExternalSourceRepositoryTransportResult>> verifyTransport)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(checkoutPath);
        ValidateRevision(revision);
        ArgumentNullException.ThrowIfNull(verifyTransport);
        return new(
            checkoutPath,
            revision,
            async (ownership, cancellationToken) =>
            {
                var result = await verifyTransport(
                        ownership.CheckoutPath,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (result.IsAvailable
                    && result.Health is ExternalSourceRepositoryHealth.Verified
                    && result.CheckoutTrust is ExternalSourceCheckoutTrust.Clean
                    && string.Equals(
                        result.LoadedRevision,
                        revision,
                        StringComparison.Ordinal)
                    && ExternalSourceRepositoryCacheKey.IsSafeRevision(result.LoadedRevision!))
                {
                    return ExternalSourceCheckoutVerification.Clean;
                }

                return result.CheckoutTrust is ExternalSourceCheckoutTrust.Dirty
                    ? ExternalSourceCheckoutVerification.Dirty
                    : ExternalSourceCheckoutVerification.Unverified;
            });
    }

    internal static ExternalSourceCheckoutAttestation FromCache(
        string checkoutPath,
        ExternalSourceRepositoryCacheManifest manifest)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(checkoutPath);
        ArgumentNullException.ThrowIfNull(manifest);
        ValidateRevision(manifest.LoadedRevision);
        return new(
            checkoutPath,
            manifest.LoadedRevision,
            (ownership, cancellationToken) =>
                new ValueTask<ExternalSourceCheckoutVerification>(
                    VerifyCacheContents(ownership.CheckoutPath, manifest, cancellationToken)));
    }

    internal static ExternalSourceCheckoutAttestation ForTesting(string checkoutPath, string revision) =>
        ForTesting(
            checkoutPath,
            revision,
            static (_, _) => new ValueTask<ExternalSourceCheckoutVerification>(
                ExternalSourceCheckoutVerification.Clean));

    internal static ExternalSourceCheckoutAttestation ForTesting(
        string checkoutPath,
        string revision,
        Func<ExternalSourceCheckoutOwnership, CancellationToken, ValueTask<ExternalSourceCheckoutVerification>> verifier)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(checkoutPath);
        ValidateRevision(revision);
        ArgumentNullException.ThrowIfNull(verifier);
        return new(
            checkoutPath,
            revision,
            verifier);
    }

    private static ExternalSourceCheckoutVerification VerifyCacheContents(
        string checkoutPath,
        ExternalSourceRepositoryCacheManifest manifest,
        CancellationToken cancellationToken)
    {
        var expected = new Dictionary<string, ExternalSourceRepositoryCacheFileEntry>(
            StringComparer.OrdinalIgnoreCase);
        foreach (var file in manifest.Files)
        {
            if (!expected.TryAdd(file.RelativePath, file))
            {
                return ExternalSourceCheckoutVerification.Unverified;
            }
        }

        var actual = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        ExternalSourceRepositoryCacheStorage.WalkFiles(
            checkoutPath,
            (sourcePath, relativePath) =>
            {
                if (!expected.TryGetValue(relativePath, out var expectedFile)
                    || !actual.Add(relativePath))
                {
                    throw new InvalidDataException("Der materialisierte Checkout weicht vom Cacheinventar ab.");
                }

                return VerifyFile(sourcePath, expectedFile, cancellationToken);
            },
            skipOwnershipMarkers: true,
            cancellationToken);
        return actual.Count == expected.Count
            ? ExternalSourceCheckoutVerification.Clean
            : ExternalSourceCheckoutVerification.Unverified;
    }

    private static long VerifyFile(
        string sourcePath,
        ExternalSourceRepositoryCacheFileEntry expected,
        CancellationToken cancellationToken)
    {
        ExternalSourceRepositoryCacheStorage.EnsureRegularFile(sourcePath);
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

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[ExternalSourceRepositoryCacheContract.FileBufferSize];
        var length = 0L;
        int read;
        while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            length = checked(length + read);
            if (length > ExternalSourceRepositoryCacheContract.MaxFileLength)
            {
                throw new InvalidDataException("Eine Datei überschreitet das Cache-Limit.");
            }

            hash.AppendData(buffer, 0, read);
        }

        var contentHash = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
        if (source.Length != length
            || expected.Length != length
            || !string.Equals(expected.ContentHash, contentHash, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Der materialisierte Checkout weicht vom Cacheinventar ab.");
        }

        return length;
    }

    private static void ValidateRevision(string revision)
    {
        if (!ExternalSourceRepositoryCacheKey.IsSafeRevision(revision))
        {
            throw new ArgumentException("Die Checkout-Attestation benötigt eine sichere Revision.", nameof(revision));
        }
    }
}

internal sealed class ExternalSourceRepositoryCacheUnsafeSourceException : Exception
{
    internal ExternalSourceRepositoryCacheUnsafeSourceException(
        ExternalSourceCheckoutTrust checkoutTrust = ExternalSourceCheckoutTrust.Unverified)
    {
        CheckoutTrust = ExternalSourceRepositorySourcePolicy.NormalizeFailureTrust(checkoutTrust);
    }

    internal ExternalSourceCheckoutTrust CheckoutTrust { get; }
}
