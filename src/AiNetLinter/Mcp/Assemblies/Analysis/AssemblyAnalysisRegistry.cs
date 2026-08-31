#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Assemblies.Analysis.Factories;
using AiNetLinter.Mcp.Assemblies.ExternalSource.Snapshots;
using AiNetLinter.Mcp.Assemblies.Analysis.References;
using AiNetLinter.Mcp.Tools.AssemblyAnalysis;
using AiNetLinter.Output;
using Microsoft.CodeAnalysis;
using Serilog;

namespace AiNetLinter.Mcp.Assemblies.Analysis;

/// <summary>
/// Residente Registry fuer externe Assembly-Sessions. Der Pfad ist der einzige
/// Target-Key; parallele Erstzugriffe teilen die Creation-Task und erhalten
/// anschliessend eigene, read-only Leases auf denselben Roslyn-Snapshot.
/// </summary>
internal sealed partial class AssemblyAnalysisRegistry : IAssemblyAnalysisRegistry
{
    internal const int MaxFingerprintRetries = 3;

    private readonly Lock gate = new();
    private readonly Dictionary<string, AssemblyAnalysisRegistryEntryCreation> entries = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, long> nextGenerations = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<Task> retiredEntries = [];
    private readonly Func<string, AssemblyFingerprint>? fingerprintFactory;
    private readonly AssemblyAnalysisResourceBudget resourceBudget;
    private readonly AssemblyAnalysisRegistryEntryFactory entryFactory;
    private readonly AssemblyAnalysisSourceProjectEntryFactory sourceProjectEntryFactory;
    private int disposed;

    internal AssemblyAnalysisRegistry(
        IAssemblySourceResolver? sourceOrchestrator = null,
        Func<string, AssemblyFingerprint>? fingerprintFactory = null,
        ExternalResourceRegistry? resourceRegistry = null)
    {
        this.fingerprintFactory = fingerprintFactory;
        resourceBudget = new(resourceRegistry);
        entryFactory = new(sourceOrchestrator, resourceBudget, CreateReferenceLeaseFactory);
        sourceProjectEntryFactory = new(resourceBudget, CreateReferenceLeaseFactory);
    }

    internal int ResidentCount
    {
        get { lock (gate) return entries.Count; }
    }

    int IAssemblyAnalysisRegistry.ResidentCount => ResidentCount;

    Task<AssemblyAnalysisLeaseResult> IAssemblyAnalysisRegistry.LeaseAsync(
        string assemblyPath,
        CancellationToken cancellationToken) => LeaseAsync(assemblyPath, cancellationToken);

    internal ExternalResourceHealthSnapshot? ResourceHealth => resourceBudget.Health;

    internal async Task<int> RunEvictionTickAsync()
    {
        var retirements = new List<Task>();
        var now = resourceBudget.UtcNow;
        AssemblyAnalysisRegistryEntryCreation[] candidates;
        lock (gate)
        {
            if (Volatile.Read(ref disposed) != 0) return 0;
            candidates = entries.Values
                .Where(creation => creation.Task.IsCompletedSuccessfully)
                .ToArray();
        }

        foreach (var creation in candidates)
        {
            var entry = await creation.Task.ConfigureAwait(false);
            if (!entry.IsIdle(now, resourceBudget.IdleTtl))
            {
                continue;
            }

            lock (gate)
            {
                var key = entries.FirstOrDefault(pair => ReferenceEquals(pair.Value, creation)).Key;
                if (key is null || !entries.Remove(key)) continue;
                var retirement = RetireEntryAsync(creation);
                retiredEntries.Add(retirement);
                retirements.Add(retirement);
            }
        }

        foreach (var retirement in retirements) await retirement.ConfigureAwait(false);
        resourceBudget.EvictIdle();
        return retirements.Count;
    }

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

    private async Task<(AssemblyAnalysisLeaseResult? Result, bool Retry)> TryLeaseCurrentAsync(
        string canonicalPath,
        AssemblyFingerprint fingerprint,
        CancellationToken cancellationToken,
        bool refreshOnMismatch)
    {
        var creation = GetOrCreateEntry(canonicalPath);
        if (creation is null)
        {
            return (Failure("Die Assembly-Registry wurde bereits beendet."), false);
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
            return (Failure("Die Assembly-Session wurde während des Aufbaus abgebrochen."), false);
        }
        catch (Exception exception)
        {
            RemoveFailedEntry(canonicalPath, creation);
            return (Failure($"Assembly-Session konnte nicht aufgebaut werden: {exception.Message}"), false);
        }

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
            return LeaseSourceProjectAsync(sourceSelection, reference, cancellationToken);
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
