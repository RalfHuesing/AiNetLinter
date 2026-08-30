#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Assemblies.ExternalSource.Snapshots;
using AiNetLinter.Mcp.Tools.AssemblyAnalysis;
using AiNetLinter.Output;
using Serilog;

namespace AiNetLinter.Mcp.Assemblies.Analysis;

/// <summary>
/// Residente Registry fuer externe Assembly-Sessions. Der Pfad ist der einzige
/// Target-Key; parallele Erstzugriffe teilen die Creation-Task und erhalten
/// anschliessend eigene, read-only Leases auf denselben Roslyn-Snapshot.
/// </summary>
internal sealed class AssemblyAnalysisRegistry : IAsyncDisposable
{
    internal const int MaxFingerprintRetries = 3;

    private readonly Lock gate = new();
    private readonly Dictionary<string, AssemblyAnalysisRegistryEntryCreation> entries = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, long> nextGenerations = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<Task> retiredEntries = [];
    private readonly IAssemblySourceResolver? sourceOrchestrator;
    private readonly Func<string, AssemblyFingerprint>? fingerprintFactory;
    private int disposed;

    internal AssemblyAnalysisRegistry(
        IAssemblySourceResolver? sourceOrchestrator = null,
        Func<string, AssemblyFingerprint>? fingerprintFactory = null)
    {
        this.sourceOrchestrator = sourceOrchestrator;
        this.fingerprintFactory = fingerprintFactory;
    }

    internal int ResidentCount
    {
        get { lock (gate) return entries.Count; }
    }

    internal async Task<AssemblyAnalysisLeaseResult> LeaseAsync(
        string assemblyPath,
        CancellationToken cancellationToken = default)
    {
        if (Volatile.Read(ref disposed) != 0)
        {
            return Failure("Die Assembly-Registry wurde bereits beendet.");
        }

        var canonicalPath = Path.GetFullPath(assemblyPath);
        for (var retry = 0; retry <= MaxFingerprintRetries; retry++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!TryCreateFingerprint(canonicalPath, out var fingerprint, out var fingerprintDiagnostic))
            {
                return Failure(fingerprintDiagnostic?.Message ?? "Assembly-Fingerprint konnte nicht berechnet werden.");
            }

            var attempt = await TryLeaseCurrentAsync(
                canonicalPath,
                fingerprint!,
                cancellationToken,
                refreshOnMismatch: retry < MaxFingerprintRetries).ConfigureAwait(false);
            if (!attempt.Retry)
            {
                return attempt.Result!;
            }

            if (retry == MaxFingerprintRetries)
            {
                return Failure(
                    $"Die Assembly-Datei ändert sich während der Analyse wiederholt; nach {MaxFingerprintRetries} Retries wurde kontrolliert abgebrochen.",
                    isError: false);
            }
        }

        return Failure("Assembly-Analyse konnte wegen instabiler Dateiidentität nicht abgeschlossen werden.");
    }

    private bool TryCreateFingerprint(
        string canonicalPath,
        out AssemblyFingerprint? fingerprint,
        out AssemblySessionDiagnostic? diagnostic)
    {
        if (fingerprintFactory is null)
        {
            return AssemblyFingerprintCalculator.TryCreate(canonicalPath, out fingerprint, out diagnostic);
        }

        try
        {
            fingerprint = fingerprintFactory(canonicalPath);
            diagnostic = null;
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            fingerprint = null;
            diagnostic = new(
                AssemblyDiagnosticCodes.For(nameof(AssemblyFingerprintCalculator), nameof(AssemblyFingerprintCalculator.TryCreate)),
                $"Assembly-Fingerprint konnte nicht berechnet werden: {exception.Message}",
                AssemblyDiagnosticSeverity.Error);
            return false;
        }
    }

    private async Task<RegistryLeaseAttempt> TryLeaseCurrentAsync(
        string canonicalPath,
        AssemblyFingerprint fingerprint,
        CancellationToken cancellationToken,
        bool refreshOnMismatch)
    {
        var creation = GetOrCreateEntry(canonicalPath);
        if (creation is null)
        {
            return new(Failure("Die Assembly-Registry wurde bereits beendet."), false);
        }

        AssemblyAnalysisEntry entry;
        try
        {
            entry = await creation.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            RemoveFailedEntry(canonicalPath, creation);
            return new(Failure("Die Assembly-Session wurde während des Aufbaus abgebrochen."), false);
        }
        catch (Exception exception)
        {
            RemoveFailedEntry(canonicalPath, creation);
            return new(Failure($"Assembly-Session konnte nicht aufgebaut werden: {exception.Message}"), false);
        }

        lock (gate)
        {
            if (!entries.TryGetValue(canonicalPath, out var current)
                || !ReferenceEquals(current, creation))
            {
                return new(null, true);
            }

            if (!entry.Matches(fingerprint))
            {
                if (!refreshOnMismatch)
                {
                    return new(null, true);
                }

                var refreshed = CreateEntry(canonicalPath);
                entries[canonicalPath] = refreshed;
                retiredEntries.Add(RetireEntryAsync(creation));
                return new(null, true);
            }

            return entry.TryAcquireLease(out var lease)
                ? new(new(lease, null), false)
                : new(Failure("Die Assembly-Session wird bereits beendet."), false);
        }
    }

    private AssemblyAnalysisRegistryEntryCreation? GetOrCreateEntry(string canonicalPath)
    {
        lock (gate)
        {
            if (Volatile.Read(ref disposed) != 0) return null;
            if (entries.TryGetValue(canonicalPath, out var creation)) return creation;

            creation = CreateEntry(canonicalPath);
            entries.Add(canonicalPath, creation);
            return creation;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;

        AssemblyAnalysisRegistryEntryCreation[] pending;
        Task[] retired;
        var failures = new List<Exception>();
        lock (gate)
        {
            pending = [.. entries.Values];
            entries.Clear();
            retired = [.. retiredEntries];
            retiredEntries.Clear();
        }

        CancelCreations(pending, failures);
        await DisposeRetiredEntriesAsync(retired, failures).ConfigureAwait(false);
        await DisposeEntriesAsync(pending, failures).ConfigureAwait(false);

        DisposeFailureAggregator.ThrowIfAny(failures);
    }

    private static void CancelCreations(
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

    private static async Task DisposeRetiredEntriesAsync(
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

    private static async Task DisposeEntriesAsync(
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

    private async Task<AssemblyAnalysisEntry> CreateEntryAsync(
        string canonicalPath,
        long targetGeneration,
        CancellationToken creationToken)
    {
        IDisposable? sourceScope = null;
        AssemblyAnalysisSession? session = null;
        try
        {
            var sourceAttempt = await TryCreateSourceEntryAsync(canonicalPath, targetGeneration, creationToken).ConfigureAwait(false);
            sourceScope = sourceAttempt.Scope;
            if (sourceAttempt.Entry is not null)
            {
                sourceScope = null;
                return sourceAttempt.Entry;
            }

            session = new AssemblyAnalysisSession(new AssemblyAnalysisSessionOptions(
                canonicalPath,
                GenerationStart: targetGeneration - 1));
            var refresh = await session.RefreshAsync(creationToken).ConfigureAwait(false);
            var sessionGeneration = session.CurrentGeneration;
            if (sessionGeneration is null)
            {
                throw new InvalidOperationException(string.Join(" ", refresh.Diagnostics));
            }

            var context = AssemblyAnalysisContextFactory.FromGeneration(sessionGeneration);
            context = context with
            {
                Diagnostics = CombineContextDiagnostics(context.Diagnostics, sourceAttempt.Diagnostics),
            };
            var fallbackEntry = AssemblyAnalysisEntry.Create(
                canonicalPath,
                sessionGeneration.Snapshot.Solution,
                context,
                session);
            session = null;
            return fallbackEntry;
        }
        finally
        {
            TryDispose(sourceScope, "Source-Selection-Scope");
            if (session is not null)
            {
                await TryDisposeAsync(session, "Assembly-Session").ConfigureAwait(false);
            }
        }
    }

    private AssemblyAnalysisRegistryEntryCreation CreateEntry(string canonicalPath)
    {
        var generation = nextGenerations.TryGetValue(canonicalPath, out var previous)
            ? checked(previous + 1)
            : 1;
        nextGenerations[canonicalPath] = generation;
        var creationLifetime = new CancellationTokenSource();
        var creation = new AssemblyAnalysisRegistryEntryCreation(
            creationLifetime,
            CreateEntryAsync(canonicalPath, generation, creationLifetime.Token));
        ObserveCreation(canonicalPath, creation);
        return creation;
    }

    private static async Task RetireEntryAsync(AssemblyAnalysisRegistryEntryCreation creation)
    {
        try
        {
            var entry = await creation.Task.ConfigureAwait(false);
            await entry.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            Log.Warning(exception, "Assembly-Registry-retired Entry konnte nicht vollständig freigegeben werden.");
        }
        finally
        {
            creation.DisposeCancellationSource();
        }
    }

    private async Task<SourceEntryAttempt> TryCreateSourceEntryAsync(
        string canonicalPath,
        long generation,
        CancellationToken creationToken)
    {
        if (sourceOrchestrator is null) return SourceEntryAttempt.None;

        var resolution = await sourceOrchestrator.ResolveForRegistryAsync(canonicalPath, creationToken).ConfigureAwait(false);
        var diagnostics = AssemblyAnalysisToolSupport.FormatExternalDiagnostics(resolution.Diagnostics).ToArray();
        if (resolution.Selection is null) return new(null, resolution.Lifetime, diagnostics);

        try
        {
            var sourceResult = await AssemblyAnalysisContextFactory.CreateAsync(
                new AssemblyAnalysisContextRequest(
                    canonicalPath,
                    ConsumerSolution: null,
                    ReceiverType: null,
                    resolution.Selection,
                    creationToken)).ConfigureAwait(false);
            if (sourceResult.Context is null) return new(null, resolution.Lifetime, diagnostics);

            var context = sourceResult.Context with
            {
                Generation = generation,
                Diagnostics = sourceResult.Context.Diagnostics
                    .Concat(diagnostics)
                    .Distinct(StringComparer.Ordinal)
                    .Take(100)
                    .ToList(),
            };
            var entry = AssemblyAnalysisEntry.Create(
                canonicalPath,
                resolution.Selection.SourceLease.Snapshot.Solution,
                context,
                resolution.Lifetime);
            return new(entry, null, diagnostics);
        }
        catch
        {
            TryDispose(resolution.Lifetime, "Source-Selection-Scope nach Creation-Fehler");
            throw;
        }
    }

    private void ObserveCreation(string canonicalPath, AssemblyAnalysisRegistryEntryCreation creation)
    {
        _ = creation.Task.ContinueWith(
            completed =>
            {
                try
                {
                    _ = completed.Exception;
                    if (completed.IsCanceled || completed.IsFaulted)
                    {
                        RemoveFailedEntry(canonicalPath, creation);
                    }
                }
                finally
                {
                    if (completed.IsCanceled || completed.IsFaulted)
                    {
                        creation.DisposeCancellationSource();
                    }
                }
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private void RemoveFailedEntry(string canonicalPath, AssemblyAnalysisRegistryEntryCreation creation)
    {
        lock (gate)
        {
            if (entries.TryGetValue(canonicalPath, out var current) && ReferenceEquals(current, creation))
            {
                entries.Remove(canonicalPath);
            }
        }
    }

    private static IReadOnlyList<string> CombineContextDiagnostics(
        IReadOnlyList<string> context,
        IReadOnlyList<string> source) =>
        context.Concat(source).Distinct(StringComparer.Ordinal).Take(100).ToList();

    private static void TryDispose(IDisposable? disposable, string resource)
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

    private static async ValueTask TryDisposeAsync(IAsyncDisposable disposable, string resource)
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

    private sealed record SourceEntryAttempt(
        AssemblyAnalysisEntry? Entry,
        IDisposable? Scope,
        IReadOnlyList<string> Diagnostics)
    {
        internal static SourceEntryAttempt None { get; } = new(null, null, Array.Empty<string>());
    }

    private sealed record RegistryLeaseAttempt(
        AssemblyAnalysisLeaseResult? Result,
        bool Retry);

    private static AssemblyAnalysisLeaseResult Failure(string message, bool isError = true) =>
        new(null, isError
            ? McpToolResults.Error(LinterErrorCodes.AnalysisFailed, message)
            : McpToolResults.Recoverable(LinterErrorCodes.AnalysisFailed, message));
}
