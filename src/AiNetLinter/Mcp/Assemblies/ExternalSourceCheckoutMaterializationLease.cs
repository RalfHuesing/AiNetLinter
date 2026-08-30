#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace AiNetLinter.Mcp.Assemblies;

internal sealed class ExternalSourceCheckoutMaterializationLease : IDisposable
{
    private readonly IReadOnlyList<FileStream> lockedFiles;
    private int disposed;

    private ExternalSourceCheckoutMaterializationLease(IReadOnlyList<FileStream> lockedFiles)
    {
        this.lockedFiles = lockedFiles;
    }

    internal static bool TryAcquire(
        ExternalSourceCheckoutOwnership ownership,
        CancellationToken cancellationToken,
        out ExternalSourceCheckoutMaterializationLease? lease)
    {
        ArgumentNullException.ThrowIfNull(ownership);
        lease = null;
        if (!ExternalSourceRepositoryPathGuard.IsOwnedCheckout(ownership))
        {
            return false;
        }

        var lockedFiles = new List<FileStream>();
        try
        {
            ExternalSourceRepositoryCacheStorage.WalkFiles(
                ownership.CheckoutPath,
                (sourcePath, _) =>
                {
                    lockedFiles.Add(OpenReadLock(sourcePath));
                    return 0;
                },
                skipOwnershipMarkers: true,
                cancellationToken);

            ExternalSourceRepositoryCacheStorage.EnsureRegularFile(ownership.OwnershipMarkerPath);
            lockedFiles.Add(OpenReadLock(ownership.OwnershipMarkerPath));
            if (!ownership.HasValidToken())
            {
                DisposeFiles(lockedFiles);
                return false;
            }

            lease = new ExternalSourceCheckoutMaterializationLease(lockedFiles);
            return true;
        }
        catch (Exception exception) when (
            ExternalSourceRepositoryFailurePolicy.IsFileSystemException(exception))
        {
            DisposeFiles(lockedFiles);
            return false;
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
        {
            return;
        }

        DisposeFiles(lockedFiles);
    }

    private static FileStream OpenReadLock(string path) =>
        new(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            ExternalSourceRepositoryCacheContract.FileBufferSize,
            FileOptions.SequentialScan);

    private static void DisposeFiles(IEnumerable<FileStream> files)
    {
        foreach (var file in files)
        {
            file.Dispose();
        }
    }
}

internal sealed class ExternalSourceCheckoutMaterializationUse : IDisposable
{
    private readonly Action release;
    private int disposed;

    internal ExternalSourceCheckoutMaterializationUse(Action release)
    {
        this.release = release;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) == 0)
        {
            release();
        }
    }
}
