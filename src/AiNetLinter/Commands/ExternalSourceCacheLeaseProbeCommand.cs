#nullable enable

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Configuration;
using AiNetLinter.Mcp.Assemblies.ExternalSource.Repository;

namespace AiNetLinter.Commands;

/// <summary>
/// Test-only IPC worker for proving the external-source cache lease across real processes.
/// It is intentionally not part of the public CLI command tree.
/// </summary>
internal static class ExternalSourceCacheLeaseProbeCommand
{
    internal const string Option = "--test-external-source-cache-lease";

    internal static bool IsMatch(string[] args) =>
        args.Length > 0
        && string.Equals(args[0], Option, StringComparison.Ordinal);

    internal static async Task<int> RunAsync(
        string[] args,
        CancellationToken cancellationToken)
    {
        if (args.Length != 14)
        {
            return 2;
        }

        var worker = new WorkerArguments
        {
            Role = args[1],
            CacheRoot = args[2],
            CheckoutPath = args[3],
            StagingRoot = args[4],
            RepositoryUrl = args[5],
            SolutionPath = args[6],
            Revision = args[7],
            OwnershipToken = args[8],
            ReadyPath = args[9],
            ContinuePath = args[10],
            CancelPath = args[11],
            ResultPath = args[12],
        };
        if (!int.TryParse(args[13], out var publishCount) || publishCount < 0)
        {
            return 2;
        }

        worker.PublishCount = publishCount;
        return worker.Role switch
        {
            "writer" => await RunWriterAsync(worker, cancellationToken)
                .ConfigureAwait(false),
            "reader" => await RunReaderAsync(worker, cancellationToken)
                .ConfigureAwait(false),
            _ => 2,
        };
    }

    private static async Task<int> RunWriterAsync(
        WorkerArguments worker,
        CancellationToken cancellationToken)
    {
        if (!ExternalSourceRepositoryCacheKey.TryCreate(
                worker.RepositoryUrl,
                worker.SolutionPath,
                out var key)
            || key is null)
        {
            return 2;
        }

        var ownership = new ExternalSourceCheckoutOwnership(
            worker.StagingRoot,
            worker.CheckoutPath,
            worker.OwnershipToken);
        var checkout = new ExternalSourceCheckoutHandle(
            ownership,
            Path.Combine(worker.CheckoutPath, worker.SolutionPath.Replace('/', Path.DirectorySeparatorChar)),
            worker.Revision,
            ExternalSourceCheckoutAttestation.ForTesting(worker.CheckoutPath, worker.Revision));
        var mapping = new ExternalSourceMapping(worker.RepositoryUrl, worker.SolutionPath, ["LeaseProbe"]);
        var request = new ExternalSourceRepositoryCachePublishRequest
        {
            Mapping = mapping,
            Checkout = checkout,
            CheckoutOwnership = ownership,
            CacheKey = key,
            SolutionPath = worker.SolutionPath,
            LoadedRevision = worker.Revision,
        };

        var writer = new LocalExternalSourceRepositoryCacheWriter(worker.CacheRoot);
        for (var index = 0; index < worker.PublishCount; index++)
        {
            var result = await writer.PublishAsync(request, cancellationToken).ConfigureAwait(false);
            if (!result.Succeeded)
            {
                File.WriteAllText(worker.ResultPath, $"failed:{result.FailureKind}");
                return 1;
            }
        }

        File.WriteAllText(worker.ResultPath, $"published:{worker.PublishCount}");
        return 0;
        // The source checkout is owned by the parent integration test. The worker must not
        // dispose it, otherwise the next independent writer process would lose its input.
    }

    private static async Task<int> RunReaderAsync(
        WorkerArguments worker,
        CancellationToken cancellationToken)
    {
        if (!ExternalSourceRepositoryCacheKey.TryCreate(
                worker.RepositoryUrl,
                worker.SolutionPath,
                out var key)
            || key is null)
        {
            return 2;
        }

        var reader = new LocalExternalSourceRepositoryCacheWriter(worker.CacheRoot);
        if (!reader.TryReadCurrent(key, out var readResult, out _)
            || readResult is null)
        {
            File.WriteAllText(worker.ResultPath, "failed:read");
            return 1;
        }

        using (readResult)
        {
            File.WriteAllText(worker.ReadyPath, readResult.Manifest.GenerationName);
            await WaitForSignalAsync(worker, cancellationToken).ConfigureAwait(false);

            if (File.Exists(worker.CancelPath)) return CancelReader(readResult, worker);

            if (!ExternalSourceRepositoryCheckoutReservation.TryCreate(
                    worker.StagingRoot,
                    out var ownership,
                    out _)
                || ownership is null)
            {
                File.WriteAllText(worker.ResultPath, "failed:reservation");
                return 1;
            }

            try
            {
                _ = ExternalSourceRepositoryCacheMaterializer.Materialize(
                    readResult,
                    ownership,
                    cancellationToken);
                File.WriteAllText(worker.ResultPath, $"materialized:{readResult.Manifest.GenerationName}");
                return 0;
            }
            finally
            {
                ownership.TryCleanup();
            }
        }
    }

    private static int CancelReader(
        ExternalSourceRepositoryCacheReadResult readResult,
        WorkerArguments worker)
    {
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        if (!ExternalSourceRepositoryCheckoutReservation.TryCreate(
                worker.StagingRoot,
                out var ownership,
                out _)
            || ownership is null)
        {
            File.WriteAllText(worker.ResultPath, "failed:reservation");
            return 1;
        }

        try
        {
            AssertCancellation(readResult, ownership, cancelled.Token);
            File.WriteAllText(worker.ResultPath, "cancelled");
            return 0;
        }
        finally
        {
            ownership.TryCleanup();
        }
    }

    private static void AssertCancellation(
        ExternalSourceRepositoryCacheReadResult readResult,
        ExternalSourceCheckoutOwnership ownership,
        CancellationToken cancellationToken)
    {
        try
        {
            _ = ExternalSourceRepositoryCacheMaterializer.Materialize(
                readResult,
                ownership,
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        throw new InvalidOperationException("Die Cache-Materialisierung wurde trotz Cancellation fortgesetzt.");
    }

    private static async Task WaitForSignalAsync(
        WorkerArguments worker,
        CancellationToken cancellationToken)
    {
        while (!File.Exists(worker.ContinuePath) && !File.Exists(worker.CancelPath))
        {
            await Task.Delay(TimeSpan.FromMilliseconds(25), cancellationToken).ConfigureAwait(false);
        }
    }

    private sealed class WorkerArguments
    {
        internal string Role { get; init; } = string.Empty;
        internal string CacheRoot { get; init; } = string.Empty;
        internal string CheckoutPath { get; init; } = string.Empty;
        internal string StagingRoot { get; init; } = string.Empty;
        internal string RepositoryUrl { get; init; } = string.Empty;
        internal string SolutionPath { get; init; } = string.Empty;
        internal string Revision { get; init; } = string.Empty;
        internal string OwnershipToken { get; init; } = string.Empty;
        internal string ReadyPath { get; init; } = string.Empty;
        internal string ContinuePath { get; init; } = string.Empty;
        internal string CancelPath { get; init; } = string.Empty;
        internal string ResultPath { get; init; } = string.Empty;
        internal int PublishCount { get; set; }
    }
}
