#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace AiNetLinter.Mcp.Assemblies.Analysis;

internal sealed class ExternalResourceCapacityContext<T>
{
    internal required ICollection<T> Entries { get; init; }

    internal required ExternalResourceRegistryOptions Options { get; init; }

    internal required long ReservedDiskBytes { get; init; }

    internal required long ReservedMemoryBytes { get; init; }

    internal required int ReservedResources { get; init; }

    internal required Func<T, long> DiskSelector { get; init; }

    internal required Func<T, long> MemorySelector { get; init; }
}

internal sealed class ExternalResourceHealthContext<T>
{
    internal required string? LastFailureReason { get; init; }

    internal required ICollection<T> Entries { get; init; }

    internal required ExternalResourceRegistryOptions Options { get; init; }

    internal required int ActiveOperations { get; init; }

    internal required Func<T, long> DiskSelector { get; init; }

    internal required Func<T, long> MemorySelector { get; init; }
}

internal static class ExternalResourceRegistrySupport
{
    internal static long Sum<T>(IEnumerable<T> values, Func<T, long> selector)
    {
        long total = 0;
        foreach (var value in values) total += selector(value);
        return total;
    }

    private static bool Exceeds(long current, long reserved, long requested, long maximum)
    {
        if (requested > maximum || reserved > maximum - requested) return true;
        return current > maximum - reserved - requested;
    }

    internal static DateTime UtcNow(TimeProvider clock) => clock.GetUtcNow().UtcDateTime;

    internal static string? CapacityReason<T>(
        ExternalResourceCapacityContext<T> context,
        ExternalResourceRequest request)
    {
        if (Exceeds(
                Sum(context.Entries, context.DiskSelector),
                context.ReservedDiskBytes,
                request.DiskBytes,
                context.Options.MaxDiskBytes))
        {
            return $"Das externe Diskbudget ist ausgeschöpft ({context.Options.MaxDiskBytes} Bytes).";
        }

        if (Exceeds(
                Sum(context.Entries, context.MemorySelector),
                context.ReservedMemoryBytes,
                request.MemoryBytes,
                context.Options.MaxMemoryBytes))
        {
            return $"Das externe Speicherbudget ist ausgeschöpft ({context.Options.MaxMemoryBytes} Bytes).";
        }

        return context.Entries.Count + context.ReservedResources >= context.Options.MaxResidentResources
            ? $"Das externe Ressourcenlimit ist ausgeschöpft ({context.Options.MaxResidentResources} Einträge)."
            : null;
    }

    internal static ExternalResourceHealthSnapshot CreateHealth<T>(
        ExternalResourceHealth health,
        ExternalResourceHealthContext<T> context)
    {
        if (health is ExternalResourceHealth.Healthy && context.LastFailureReason is not null)
        {
            health = ExternalResourceHealth.Degraded;
        }

        return new(
            health,
            context.Entries.Count,
            context.Options.MaxResidentResources,
            Sum(context.Entries, context.DiskSelector),
            context.Options.MaxDiskBytes,
            Sum(context.Entries, context.MemorySelector),
            context.Options.MaxMemoryBytes,
            context.ActiveOperations,
            context.Options.MaxParallelOperations,
            context.LastFailureReason);
    }

    internal static void ReleaseOperationSlot(SemaphoreSlim operationSlots)
    {
        try
        {
            operationSlots.Release();
        }
        catch (ObjectDisposedException)
        {
            // Dispose ist endgültig; danach darf keine neue Operation zugelassen werden.
        }
    }

    internal static void ValidateOptions(ExternalResourceRegistryOptions value)
    {
        if (value.MaxDiskBytes <= 0) throw new ArgumentOutOfRangeException(nameof(value.MaxDiskBytes));
        if (value.MaxMemoryBytes <= 0) throw new ArgumentOutOfRangeException(nameof(value.MaxMemoryBytes));
        if (value.MaxParallelOperations <= 0) throw new ArgumentOutOfRangeException(nameof(value.MaxParallelOperations));
        if (value.MaxResidentResources <= 0) throw new ArgumentOutOfRangeException(nameof(value.MaxResidentResources));
        if (value.IdleTtl < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(value.IdleTtl));
    }
}

internal static class AssemblyAnalysisRegistryDisposal
{
    internal static void CancelCreations(
        IEnumerable<AssemblyAnalysisRegistryEntryCreation> creations,
        List<Exception> failures)
    {
        foreach (var creation in creations)
        {
            try
            {
                creation.CancellationSource.Cancel();
            }
            catch (ObjectDisposedException)
            {
                Log.Debug("Assembly-Registry-Creation war beim Beenden bereits freigegeben.");
            }
            catch (Exception exception)
            {
                failures.Add(exception);
                Log.Warning(exception, "Assembly-Registry-Cancellation einer Creation fehlgeschlagen.");
            }
        }
    }

    internal static async Task DisposeRetiredEntriesAsync(
        IEnumerable<Task> retirements,
        List<Exception> failures)
    {
        foreach (var retirement in retirements)
        {
            try
            {
                await retirement.ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                failures.Add(exception);
                Log.Warning(exception, "Assembly-Registry-retired Entry konnte nicht vollständig freigegeben werden.");
            }
        }
    }

    internal static async Task DisposeEntriesAsync(
        IEnumerable<AssemblyAnalysisRegistryEntryCreation> creations,
        List<Exception> failures)
    {
        foreach (var creation in creations)
        {
            try
            {
                var entry = await creation.Task.ConfigureAwait(false);
                await entry.DisposeAsync().ConfigureAwait(false);
            }
            catch (OperationCanceledException exception)
            {
                Log.Debug(exception, "Assembly-Registry-Creation wurde beim Beenden abgebrochen.");
            }
            catch (Exception exception)
            {
                failures.Add(exception);
                Log.Warning(exception, "Assembly-Registry-Entry konnte beim Beenden nicht vollständig freigegeben werden.");
            }
            finally
            {
                creation.DisposeCancellationSource();
            }
        }
    }

    internal static void TryDispose(IDisposable? disposable, string resource)
    {
        try
        {
            disposable?.Dispose();
        }
        catch (Exception exception)
        {
            Log.Warning(exception, "Assembly-Registry-Cleanup fehlgeschlagen: Ressource={Resource}", resource);
        }
    }

    internal static async ValueTask TryDisposeAsync(IAsyncDisposable disposable, string resource)
    {
        try
        {
            await disposable.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            Log.Warning(exception, "Assembly-Registry-Cleanup fehlgeschlagen: Ressource={Resource}", resource);
        }
    }
}
