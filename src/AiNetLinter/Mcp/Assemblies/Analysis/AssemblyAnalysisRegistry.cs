#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Assemblies.Analysis.Factories;
using AiNetLinter.Mcp.Assemblies.Analysis.Coordinators;
using AiNetLinter.Mcp.Assemblies.Analysis.References;
using AiNetLinter.Configuration;
using AiNetLinter.Mcp.Tools.AssemblyAnalysis;
using AiNetLinter.Output;
using Serilog;

namespace AiNetLinter.Mcp.Assemblies.Analysis;

/// <summary>
/// Residente Registry fuer externe Assembly-Sessions. Der Pfad ist der einzige
/// Target-Key; parallele Erstzugriffe teilen die Creation-Task und erhalten
/// anschliessend eigene, read-only Leases auf denselben Roslyn-Snapshot.
/// </summary>
internal sealed partial class AssemblyAnalysisRegistry : IAssemblyAnalysisRegistry, IAssemblyAnalysisTemporaryReferenceEvictor
{
    internal const int MaxFingerprintRetries = 3;

    private readonly Lock gate = new();
    private readonly Dictionary<string, AssemblyAnalysisRegistryEntryCreation> entries = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, long> nextGenerations = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<Task> retiredEntries = [];
    private readonly Func<string, AssemblyFingerprint>? fingerprintFactory;
    private readonly AssemblyAnalysisResourceBudget resourceBudget;
    private readonly AssemblyAnalysisRegistryEntryFactory entryFactory;
    private readonly AssemblyAnalysisRegistryEvictionCandidates evictionCandidates;
    private readonly AssemblyAnalysisRegistryEvictionCoordinator evictionCoordinator;
    private readonly AssemblyAnalysisRegistryReferenceEviction referenceEviction;
    private readonly AssemblyAnalysisHealthSnapshotProvider healthSnapshotProvider;
    private readonly Func<AssemblyAnalysisEntry, Task>? beforeRetirementAsync;
    private readonly Func<string, long, CancellationToken, ExternalResourceLease?, Task<AssemblyAnalysisEntry>>? entryFactoryOverride;
    private int disposed;

    internal AssemblyAnalysisRegistry(
        Func<string, AssemblyFingerprint>? fingerprintFactory = null,
        ExternalResourceRegistry? resourceRegistry = null,
        Func<AssemblyAnalysisEntry, Task>? beforeRetirementAsync = null,
        AssemblyDecompilationConfiguration? decompilationConfiguration = null,
        Func<string, long, CancellationToken, ExternalResourceLease?, Task<AssemblyAnalysisEntry>>? entryFactoryOverride = null)
        : this(
            fingerprintFactory,
            resourceRegistry,
            beforeRetirementAsync,
            new AssemblyAnalysisRegistryRuntimeOptions(decompilationConfiguration),
            entryFactoryOverride)
    {
    }

    internal AssemblyAnalysisRegistry(
        Func<string, AssemblyFingerprint>? fingerprintFactory,
        ExternalResourceRegistry? resourceRegistry,
        Func<AssemblyAnalysisEntry, Task>? beforeRetirementAsync,
        AssemblyAnalysisRegistryRuntimeOptions runtimeOptions,
        Func<string, long, CancellationToken, ExternalResourceLease?, Task<AssemblyAnalysisEntry>>? entryFactoryOverride = null)
    {
        ArgumentNullException.ThrowIfNull(runtimeOptions);
        this.fingerprintFactory = fingerprintFactory;
        this.beforeRetirementAsync = beforeRetirementAsync;
        this.entryFactoryOverride = entryFactoryOverride;
        resourceBudget = new(resourceRegistry);
        entryFactory = new(
            resourceBudget,
            CreateReferenceLeaseFactory,
            RequestTemporaryReferenceEviction,
            runtimeOptions.DecompilationConfiguration);
        evictionCandidates = new(
            GetCompletedEvictionCreations,
            beforeRetirementAsync is null
                ? null
                : entry => beforeRetirementAsync((AssemblyAnalysisEntry)entry),
            entry => IsTemporaryReferenceEvictionRequested(entry),
            entry => ClearTemporaryReferenceEvictionRequest(entry));
        evictionCoordinator = new(new AssemblyAnalysisRegistryEvictionContext
        {
            ResourceBudget = resourceBudget,
            GetCandidates = evictionCandidates.GetCompletedEvictionCandidates,
            TryRetireCandidate = evictionCandidates.TryRetireCandidate,
        });
        referenceEviction = new(
            evictionCoordinator,
            evictionCandidates.GetCompletedTemporaryEvictionCandidates);
        healthSnapshotProvider = new(gate, entries, runtimeOptions.DaemonProfile);
    }

    internal int ResidentCount { get { lock (gate) return entries.Count; } }
    internal int TemporaryReferenceEvictionRequestCount => referenceEviction.RequestCount;

    int IAssemblyAnalysisRegistry.ResidentCount => ResidentCount;

    Task<AssemblyAnalysisLeaseResult> IAssemblyAnalysisRegistry.LeaseAsync(
        string assemblyPath,
        CancellationToken cancellationToken) => LeaseAsync(assemblyPath, cancellationToken);

    Task<int> IAssemblyAnalysisTemporaryReferenceEvictor.EvictTemporaryReferenceSessionsAsync(
        CancellationToken cancellationToken) => referenceEviction.EvictAsync(cancellationToken);

    private void RequestTemporaryReferenceEviction(AssemblyAnalysisEntry entry) => referenceEviction.Request(entry);

    private bool IsTemporaryReferenceEvictionRequested(IAssemblyAnalysisEvictionEntry entry) =>
        referenceEviction.IsRequested(entry);

    private void ClearTemporaryReferenceEvictionRequest(IAssemblyAnalysisEvictionEntry entry) =>
        referenceEviction.ClearRequest(entry);
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
        return TryLeaseEntry(
            canonicalPath,
            creation,
            entry,
            fingerprint,
            refreshOnMismatch);
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
        catch (AssemblyAnalysisRegistryRecoverableFailureException exception)
        {
            return new(null, RecoverableMetadataFailure(canonicalPath, creation, exception.Failure), false);
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
        bool refreshOnMismatch)
    {
        lock (gate)
        {
            if (!entries.TryGetValue(canonicalPath, out var current)
                || !ReferenceEquals(current, creation))
            {
                return (null, true);
            }

            if (!entry.Matches(fingerprint))
            {
                if (!refreshOnMismatch)
                {
                    return (null, true);
                }

                var refreshed = CreateEntry(canonicalPath);
                entries[canonicalPath] = refreshed;
                retiredEntries.Add(AssemblyAnalysisRegistryDisposal.RetireEntryAsync(creation, ClearTemporaryReferenceEvictionRequest));
                return (null, true);
            }

            return entry.TryAcquireLease(out var lease)
                ? (new(lease, null), false)
                : (Failure("Die Assembly-Session wird bereits beendet."), false);
        }
    }

    private Task<AssemblyAnalysisLeaseResult> LeaseReferencedAsync(
        AssemblyReferenceDto reference,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(reference.ResolvedPath))
        {
            return Task.FromResult(Failure(
                $"Die Referenz '{reference.Name}' besitzt keinen auflösbaren Assembly-Pfad.",
                isError: false));
        }

        return LeaseAsync(reference.ResolvedPath, cancellationToken);
    }

    private AssemblyReferenceLeaseFactory CreateReferenceLeaseFactory() =>
        (reference, cancellationToken) =>
            LeaseReferencedAsync(reference, cancellationToken);

    Task<IReadOnlyList<AssemblyAnalysisHealthSnapshot>> IAssemblyAnalysisRegistry.SnapshotsAsync() =>
        SnapshotsAsync();

    internal Task<IReadOnlyList<AssemblyAnalysisHealthSnapshot>> SnapshotsAsync() =>
        healthSnapshotProvider.GetSnapshotsAsync();

    private async Task<IReadOnlyList<AssemblyAnalysisEvictionCreation>> GetCompletedEvictionCreations()
    {
        KeyValuePair<string, AssemblyAnalysisRegistryEntryCreation>[] completedCreations;
        lock (gate)
        {
            if (Volatile.Read(ref disposed) != 0) return [];
            completedCreations = [.. entries.Where(pair => pair.Value.Task.IsCompletedSuccessfully)];
        }

        var completedEntries = await Task.WhenAll(
            completedCreations.Select(pair => pair.Value.Task)).ConfigureAwait(false);
        return completedCreations
            .Zip(completedEntries)
            .Select(item => new AssemblyAnalysisEvictionCreation(
                item.First.Key,
                item.Second,
                () => TryRetireEvictionEntry(item.First.Key, item.First.Value, item.Second)))
            .ToList();
    }

    private Task? TryRetireEvictionEntry(
        string key,
        AssemblyAnalysisRegistryEntryCreation creation,
        AssemblyAnalysisEntry entry)
    {
        lock (gate)
        {
            if (!entries.TryGetValue(key, out var current)
                || !ReferenceEquals(current, creation)
                || !entry.TryBeginRetirement()
                || !entries.Remove(key))
            {
                return null;
            }

            var retirement = AssemblyAnalysisRegistryDisposal.RetireEntryAsync(creation, ClearTemporaryReferenceEvictionRequest);
            retiredEntries.Add(retirement);
            return retirement;
        }
    }

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
        await AssemblyAnalysisRegistryDisposal.DisposeEntriesAsync(pending, failures, ClearTemporaryReferenceEvictionRequest).ConfigureAwait(false);
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

        var creationTask = entryFactoryOverride is not null
            ? entryFactoryOverride(canonicalPath, generation, creationLifetime.Token, resourceLease)
            : entryFactory.CreateAsync(canonicalPath, generation, creationLifetime.Token, resourceLease);
        var creation = new AssemblyAnalysisRegistryEntryCreation(
            creationLifetime,
            creationTask);
        ObserveCreation(canonicalPath, creation);
        return creation;
    }

    internal Task<AssemblyAnalysisEntry> CreateEntryDirectAsync(
        string canonicalPath,
        long targetGeneration,
        CancellationToken creationToken,
        ExternalResourceLease? resourceLease) =>
        entryFactory.CreateAsync(canonicalPath, targetGeneration, creationToken, resourceLease);

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

}
