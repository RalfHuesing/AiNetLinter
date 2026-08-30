#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Serilog;

namespace AiNetLinter.Mcp.Assemblies.Analysis;

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
