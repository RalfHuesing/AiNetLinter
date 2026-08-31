#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Assemblies.Analysis.Factories;
using AiNetLinter.Mcp.Assemblies.Analysis.Coordinators;
using AiNetLinter.Mcp.Assemblies.ExternalSource.Snapshots;
using AiNetLinter.Mcp.Assemblies.Analysis.References;
using AiNetLinter.Mcp.Tools.AssemblyAnalysis;
using AiNetLinter.Output;
using Serilog;

namespace AiNetLinter.Mcp.Assemblies.Analysis;

/// <summary>
/// Residente Registry fuer externe Assembly-Sessions. Der Pfad ist der einzige
/// Target-Key; parallele Erstzugriffe teilen die Creation-Task und erhalten
/// anschliessend eigene, read-only Leases auf denselben Roslyn-Snapshot.
/// </summary>
internal sealed class AssemblyAnalysisRegistry : IAssemblyAnalysisRegistry
{
    internal const int MaxFingerprintRetries = 3;

    private readonly Lock gate = new();
    private readonly Dictionary<string, AssemblyAnalysisRegistryEntryCreation> entries = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, long> nextGenerations = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<Task> retiredEntries = [];
    private readonly Func<string, AssemblyFingerprint>? fingerprintFactory;
    private readonly IAssemblySourceResolver? sourceOrchestrator;
    private readonly AssemblyAnalysisResourceBudget resourceBudget;
    private readonly AssemblyAnalysisRegistryEntryFactory entryFactory;
    private readonly AssemblyAnalysisSourceProjectLeaseCoordinator sourceProjectLeaseCoordinator;
    private readonly AssemblyAnalysisRegistryEvictionCoordinator evictionCoordinator;
    private readonly AssemblyAnalysisHealthSnapshotProvider healthSnapshotProvider;
    private readonly Func<AssemblyAnalysisEntry, Task>? beforeRetirementAsync;
    private int disposed;

    internal AssemblyAnalysisRegistry(
        IAssemblySourceResolver? sourceOrchestrator = null,
        Func<string, AssemblyFingerprint>? fingerprintFactory = null,
        ExternalResourceRegistry? resourceRegistry = null, Func<AssemblyAnalysisEntry, Task>? beforeRetirementAsync = null)
    {
        this.sourceOrchestrator = sourceOrchestrator;
        this.fingerprintFactory = fingerprintFactory;
        this.beforeRetirementAsync = beforeRetirementAsync;
        resourceBudget = new(resourceRegistry);
        entryFactory = new(sourceOrchestrator, resourceBudget, CreateReferenceLeaseFactory);
        var sourceProjectEntryFactory = new AssemblyAnalysisSourceProjectEntryFactory(
            resourceBudget,
            CreateReferenceLeaseFactory);
        var coordinatorContext = new AssemblyAnalysisRegistryCoordinatorContext
        {
            Gate = gate,
            Entries = entries,
            NextGenerations = nextGenerations,
            RetiredEntries = retiredEntries,
            IsDisposed = () => Volatile.Read(ref disposed) != 0,
            ResourceBudget = resourceBudget,
            BeforeRetirementAsync = beforeRetirementAsync,
            RetireEntryAsync = RetireEntryAsync,
            SourceProjectEntryFactory = sourceProjectEntryFactory,
            ObserveCreation = ObserveCreation,
            RemoveFailedEntry = RemoveFailedEntry,
            RunEvictionTick = (forceCapacity, requiredPath, cancellationToken) =>
                RunEvictionTickAsync(forceCapacity, requiredPath, cancellationToken),
            Failure = Failure,
        };
        evictionCoordinator = new(new AssemblyAnalysisRegistryEvictionContext
        {
            Gate = gate,
            Entries = entries,
            RetiredEntries = retiredEntries,
            IsDisposed = () => Volatile.Read(ref disposed) != 0,
            ResourceBudget = resourceBudget,
            BeforeRetirementAsync = beforeRetirementAsync,
            RetireEntryAsync = RetireEntryAsync,
        });
        sourceProjectLeaseCoordinator = new(coordinatorContext);
        healthSnapshotProvider = new(gate, entries);
    }

    internal int ResidentCount { get { lock (gate) return entries.Count; } }

    int IAssemblyAnalysisRegistry.ResidentCount => ResidentCount;

    Task<AssemblyAnalysisLeaseResult> IAssemblyAnalysisRegistry.LeaseAsync(
        string assemblyPath,
        CancellationToken cancellationToken) => LeaseAsync(assemblyPath, cancellationToken);

    internal ExternalResourceHealthSnapshot? ResourceHealth => resourceBudget.Health;
    internal Task<int> RunEvictionTickAsync() =>
        evictionCoordinator.RunAsync(false, null, CancellationToken.None);

    private Task<int> RunEvictionTickAsync(
        bool forceCapacity,
        string? requiredPath,
        CancellationToken cancellationToken) =>
        evictionCoordinator.RunAsync(forceCapacity, requiredPath, cancellationToken);

    internal async Task<AssemblyAnalysisLeaseResult> LeaseAsync(
        string assemblyPath,
        CancellationToken cancellationToken = default)
    {
        if (Volatile.Read(ref disposed) != 0)
        {
            return Failure("Die Assembly-Registry wurde bereits beendet.");
        }

        await RunEvictionTickAsync().ConfigureAwait(false);
        var canonicalPath = Path.GetFullPath(assemblyPath);
        for (var retry = 0; retry <= MaxFingerprintRetries; retry++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!AssemblyAnalysisRegistryIdentity.TryCreateFingerprint(
                    canonicalPath,
                    fingerprintFactory,
                    out var fingerprint,
                    out var fingerprintDiagnostic))
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

    private async Task<(AssemblyAnalysisLeaseResult? Result, bool Retry)> TryLeaseCurrentAsync(
        string canonicalPath,
        AssemblyFingerprint fingerprint,
        CancellationToken cancellationToken,
        bool refreshOnMismatch)
    {
        var creationResult = GetOrCreateEntry(canonicalPath);
        var creation = creationResult.Creation;
        if (creation is null)
        {
            return (Failure("Die Assembly-Registry wurde bereits beendet."), false);
        }

        var creationAttempt = await AwaitCreationAsync(
                canonicalPath,
                creation,
                cancellationToken)
            .ConfigureAwait(false);
        if (creationAttempt.Retry || creationAttempt.Result is not null)
        {
            return (creationAttempt.Result, creationAttempt.Retry);
        }

        var entry = creationAttempt.Entry!;
        var sourceSnapshotIdentity = creationResult.WasExisting
            ? await AssemblyAnalysisRegistryIdentity.ResolveCurrentSourceSnapshotIdentityAsync(
                    sourceOrchestrator,
                    canonicalPath,
                    cancellationToken)
                .ConfigureAwait(false)
            : entry.Context.Origin.SourceSnapshotIdentity?.StableValue;
        return TryLeaseEntry(
            canonicalPath,
            creation,
            entry,
            fingerprint,
            refreshOnMismatch,
            sourceSnapshotIdentity);
    }

    private async Task<(AssemblyAnalysisEntry? Entry, AssemblyAnalysisLeaseResult? Result, bool Retry)> AwaitCreationAsync(
        string canonicalPath,
        AssemblyAnalysisRegistryEntryCreation creation,
        CancellationToken cancellationToken)
    {
        try
        {
            return new(
                await creation.Task.WaitAsync(cancellationToken).ConfigureAwait(false),
                null,
                false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            RemoveFailedEntry(canonicalPath, creation);
            return new(
                null,
                Failure("Die Assembly-Session wurde während des Aufbaus abgebrochen."),
                false);
        }
        catch (ExternalResourceCapacityException exception)
        {
            RemoveFailedEntry(canonicalPath, creation);
            if (!resourceBudget.CanAccommodate(canonicalPath))
            {
                return new(
                    null,
                    Failure($"Assembly-Session konnte nicht aufgebaut werden: {exception.Message}"),
                    false);
            }

            var retired = await RunEvictionTickAsync(
                    forceCapacity: true,
                    requiredPath: canonicalPath,
                    cancellationToken)
                .ConfigureAwait(false);
            return retired > 0
                ? new(null, null, true)
                : new(
                    null,
                    Failure($"Assembly-Session konnte nicht aufgebaut werden: {exception.Message}"),
                    false);
        }
        catch (Exception exception)
        {
            RemoveFailedEntry(canonicalPath, creation);
            return new(
                null,
                Failure($"Assembly-Session konnte nicht aufgebaut werden: {exception.Message}"),
                false);
        }
    }

    private (AssemblyAnalysisLeaseResult? Result, bool Retry) TryLeaseEntry(
        string canonicalPath,
        AssemblyAnalysisRegistryEntryCreation creation,
        AssemblyAnalysisEntry entry,
        AssemblyFingerprint fingerprint,
        bool refreshOnMismatch,
        string? sourceSnapshotIdentity)
    {
        lock (gate)
        {
            if (!entries.TryGetValue(canonicalPath, out var current)
                || !ReferenceEquals(current, creation))
            {
                return (null, true);
            }

            if (!entry.Matches(
                    fingerprint,
                    sourceSnapshotIdentity,
                    compareSourceSnapshotIdentity: sourceOrchestrator is not null))
            {
                if (!refreshOnMismatch)
                {
                    return (null, true);
                }

                var refreshed = CreateEntry(canonicalPath);
                entries[canonicalPath] = refreshed;
                retiredEntries.Add(RetireEntryAsync(creation));
                return (null, true);
            }

            return entry.TryAcquireLease(out var lease)
                ? (new(lease, null), false)
                : (Failure("Die Assembly-Session wird bereits beendet."), false);
        }
    }

    private Task<AssemblyAnalysisLeaseResult> LeaseReferencedAsync(
        AssemblySourceSelection? sourceSelection,
        AssemblyReferenceDto reference,
        CancellationToken cancellationToken)
    {
        if (reference.ResolutionState == "source_project")
        {
            return sourceProjectLeaseCoordinator.LeaseAsync(sourceSelection, reference, cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(reference.ResolvedPath))
        {
            return Task.FromResult(Failure(
                $"Die Referenz '{reference.Name}' besitzt keinen auflösbaren Assembly-Pfad.",
                isError: false));
        }

        return LeaseAsync(reference.ResolvedPath, cancellationToken);
    }

    private AssemblyReferenceLeaseFactory CreateReferenceLeaseFactory(AssemblySourceSelection? sourceSelection) =>
        (reference, cancellationToken) =>
            LeaseReferencedAsync(sourceSelection, reference, cancellationToken);

    Task<IReadOnlyList<AssemblyAnalysisHealthSnapshot>> IAssemblyAnalysisRegistry.SnapshotsAsync() =>
        SnapshotsAsync();

    internal Task<IReadOnlyList<AssemblyAnalysisHealthSnapshot>> SnapshotsAsync() =>
        healthSnapshotProvider.GetSnapshotsAsync();

    private (AssemblyAnalysisRegistryEntryCreation? Creation, bool WasExisting) GetOrCreateEntry(
        string canonicalPath)
    {
        lock (gate)
        {
            if (Volatile.Read(ref disposed) != 0) return (null, false);
            if (entries.TryGetValue(canonicalPath, out var creation)) return (creation, true);

            creation = CreateEntry(canonicalPath);
            entries.Add(canonicalPath, creation);
            return (creation, false);
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

        AssemblyAnalysisRegistryDisposal.CancelCreations(pending, failures);
        await AssemblyAnalysisRegistryDisposal.DisposeRetiredEntriesAsync(retired, failures).ConfigureAwait(false);
        await AssemblyAnalysisRegistryDisposal.DisposeEntriesAsync(pending, failures).ConfigureAwait(false);

        DisposeFailureAggregator.ThrowIfAny(failures);
    }

    private AssemblyAnalysisRegistryEntryCreation CreateEntry(string canonicalPath)
    {
        var generation = nextGenerations.TryGetValue(canonicalPath, out var previous)
            ? checked(previous + 1)
            : 1;
        nextGenerations[canonicalPath] = generation;
        var creationLifetime = new CancellationTokenSource();
        var resourceAcquisition = resourceBudget.Acquire(canonicalPath);
        var resourceLease = resourceAcquisition.Lease;
        if (resourceBudget.IsEnabled && resourceLease is null)
        {
            var failed = Task.FromException<AssemblyAnalysisEntry>(
                new ExternalResourceCapacityException(
                    resourceAcquisition.FailureReason ?? "Externe Ressourcen sind nicht verfügbar."));
            var rejected = new AssemblyAnalysisRegistryEntryCreation(creationLifetime, failed);
            ObserveCreation(canonicalPath, rejected);
            return rejected;
        }

        var creation = new AssemblyAnalysisRegistryEntryCreation(
            creationLifetime,
            entryFactory.CreateAsync(canonicalPath, generation, creationLifetime.Token, resourceLease));
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

    private static AssemblyAnalysisLeaseResult Failure(string message, bool isError = true) =>
        new(null, isError
            ? McpToolResults.Error(LinterErrorCodes.AnalysisFailed, message)
            : McpToolResults.Recoverable(LinterErrorCodes.AnalysisFailed, message));

}
