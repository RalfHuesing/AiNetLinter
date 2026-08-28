#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using AiNetLinter.Configuration;

namespace AiNetLinter.Mcp.Assemblies;

internal sealed class ExternalSourceCheckoutHandle : IDisposable
{
    private readonly string stagingRoot;
    private int disposed;

    internal ExternalSourceCheckoutHandle(
        string stagingRoot,
        string checkoutPath,
        string solutionPath,
        string loadedRevision)
    {
        this.stagingRoot = stagingRoot;
        CheckoutPath = checkoutPath;
        SolutionPath = solutionPath;
        LoadedRevision = loadedRevision;
    }

    internal string CheckoutPath { get; }

    internal string SolutionPath { get; }

    internal string LoadedRevision { get; }

    internal bool IsDisposed => Volatile.Read(ref disposed) != 0;

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
        {
            return;
        }

        ExternalSourceRepositoryPathGuard.TryDeleteOwnedCheckout(stagingRoot, CheckoutPath);
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
