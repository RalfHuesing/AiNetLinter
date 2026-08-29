#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Threading;
using AiNetLinter.Configuration;

namespace AiNetLinter.Mcp.Assemblies;

internal enum ExternalSourceCheckoutCleanupState
{
    NotAttempted,
    Succeeded,
    RepositoryCleanupFailed,
}

internal sealed class ExternalSourceCheckoutOwnership
{
    internal const string OwnershipMarkerFileName = ".ainetlinter-owner";

    internal ExternalSourceCheckoutOwnership(
        string stagingRoot,
        string checkoutPath,
        string ownershipToken)
    {
        StagingRoot = stagingRoot;
        CheckoutPath = checkoutPath;
        OwnershipToken = ownershipToken;
    }

    internal string StagingRoot { get; }

    internal string CheckoutPath { get; }

    internal string OwnershipToken { get; }

    internal string OwnershipMarkerPath =>
        Path.Combine(CheckoutPath, OwnershipMarkerFileName);

    internal bool HasValidToken()
    {
        try
        {
            return File.Exists(OwnershipMarkerPath)
                && string.Equals(
                    File.ReadAllText(OwnershipMarkerPath),
                    OwnershipToken,
                    StringComparison.Ordinal);
        }
        catch (Exception exception) when (
            ExternalSourceRepositoryFailurePolicy.IsFileSystemException(exception))
        {
            return false;
        }
    }

    internal bool TryCleanup() =>
        ExternalSourceRepositoryPathGuard.TryDeleteOwnedCheckout(this);
}

internal sealed class ExternalSourceCheckoutHandle : IExternalSourceCheckoutOwner
{
    private readonly ExternalSourceCheckoutOwnership ownership;
    private int disposed;
    private int cleanupState;

    internal ExternalSourceCheckoutHandle(
        ExternalSourceCheckoutOwnership ownership,
        string solutionPath,
        string loadedRevision)
    {
        ArgumentNullException.ThrowIfNull(ownership);
        this.ownership = ownership;
        SolutionPath = solutionPath;
        LoadedRevision = loadedRevision;
    }

    internal string CheckoutPath => ownership.CheckoutPath;

    internal string SolutionPath { get; }

    internal string LoadedRevision { get; }

    internal bool IsDisposed => Volatile.Read(ref disposed) != 0;

    internal ExternalSourceCheckoutCleanupState CleanupState =>
        (ExternalSourceCheckoutCleanupState)Volatile.Read(ref cleanupState);

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
        {
            return;
        }

        var cleanupSucceeded = ownership.TryCleanup();
        Volatile.Write(
            ref cleanupState,
            (int)(cleanupSucceeded
                ? ExternalSourceCheckoutCleanupState.Succeeded
                : ExternalSourceCheckoutCleanupState.RepositoryCleanupFailed));
    }
}

internal sealed record ExternalSourceRepositoryAcquisitionResult
{
    private ExternalSourceRepositoryAcquisitionResult(
        bool isAvailable,
        ExternalSourceCheckoutHandle? checkout,
        string? loadedRevision,
        ExternalSourceProviderFailureKind failureKind,
        IEnumerable<ExternalSourceConfigurationDiagnostic> diagnostics)
    {
        IsAvailable = isAvailable;
        Checkout = checkout;
        LoadedRevision = loadedRevision;
        FailureKind = failureKind;
        Diagnostics = diagnostics.ToImmutableArray();
    }

    internal bool IsAvailable { get; }

    internal ExternalSourceCheckoutHandle? Checkout { get; }

    internal string? LoadedRevision { get; }

    internal ExternalSourceProviderFailureKind FailureKind { get; }

    internal ImmutableArray<ExternalSourceConfigurationDiagnostic> Diagnostics { get; }

    internal static ExternalSourceRepositoryAcquisitionResult Success(
        ExternalSourceCheckoutHandle checkout,
        IEnumerable<ExternalSourceConfigurationDiagnostic> diagnostics) =>
        new(
            true,
            checkout,
            checkout.LoadedRevision,
            ExternalSourceProviderFailureKind.None,
            diagnostics);

    internal static ExternalSourceRepositoryAcquisitionResult Failure(
        ExternalSourceProviderFailureKind failureKind,
        IEnumerable<ExternalSourceConfigurationDiagnostic> diagnostics) =>
        new(
            false,
            null,
            null,
            failureKind is ExternalSourceProviderFailureKind.None
                ? ExternalSourceProviderFailureKind.InvalidResponse
                : failureKind,
            diagnostics);
}
