#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Assemblies.Analysis.Factories;
using AiNetLinter.Mcp.Assemblies.Analysis.References;
using AiNetLinter.Mcp.Assemblies.ExternalSource.Snapshots;
using AiNetLinter.Mcp.Tools.AssemblyAnalysis;
using AiNetLinter.Output;
using Microsoft.CodeAnalysis;

namespace AiNetLinter.Mcp.Assemblies.Analysis;

internal sealed class AssemblyAnalysisSourceProjectLeaseCoordinator
{
    private readonly Lock gate;
    private readonly Dictionary<string, AssemblyAnalysisRegistryEntryCreation> entries;
    private readonly Dictionary<string, long> nextGenerations;
    private readonly Func<bool> isDisposed;
    private readonly AssemblyAnalysisResourceBudget resourceBudget;
    private readonly AssemblyAnalysisSourceProjectEntryFactory entryFactory;
    private readonly Action<string, AssemblyAnalysisRegistryEntryCreation> observeCreation;
    private readonly Action<string, AssemblyAnalysisRegistryEntryCreation> removeFailedEntry;
    private readonly Func<bool, string?, CancellationToken, Task<int>> runEvictionTick;
    private readonly Func<string, bool, AssemblyAnalysisLeaseResult> failure;

    internal AssemblyAnalysisSourceProjectLeaseCoordinator(
        Lock gate,
        Dictionary<string, AssemblyAnalysisRegistryEntryCreation> entries,
        Dictionary<string, long> nextGenerations,
        Func<bool> isDisposed,
        AssemblyAnalysisResourceBudget resourceBudget,
        AssemblyAnalysisSourceProjectEntryFactory entryFactory,
        Action<string, AssemblyAnalysisRegistryEntryCreation> observeCreation,
        Action<string, AssemblyAnalysisRegistryEntryCreation> removeFailedEntry,
        Func<bool, string?, CancellationToken, Task<int>> runEvictionTick,
        Func<string, bool, AssemblyAnalysisLeaseResult> failure)
    {
        this.gate = gate;
        this.entries = entries;
        this.nextGenerations = nextGenerations;
        this.isDisposed = isDisposed;
        this.resourceBudget = resourceBudget;
        this.entryFactory = entryFactory;
        this.observeCreation = observeCreation;
        this.removeFailedEntry = removeFailedEntry;
        this.runEvictionTick = runEvictionTick;
        this.failure = failure;
    }

    internal async Task<AssemblyAnalysisLeaseResult> LeaseAsync(
        AssemblySourceSelection? sourceSelection,
        AssemblyReferenceDto reference,
        CancellationToken cancellationToken)
    {
        if (!TryFindSourceProject(sourceSelection, reference, out var selection, out var project, out var error))
        {
            return failure(error!, false);
        }

        var key = BuildSourceProjectKey(selection, project);
        var resourcePath = project.FilePath ?? key;
        var acquisition = await AcquireSourceProjectEntryAsync(
                key,
                selection,
                project,
                resourcePath,
                cancellationToken)
            .ConfigureAwait(false);
        if (acquisition.Error is not null)
        {
            return failure(acquisition.Error, true);
        }

        var creation = acquisition.Creation!;
        var entry = acquisition.Entry!;
        lock (gate)
        {
            if (!entries.TryGetValue(key, out var current)
                || !ReferenceEquals(current, creation))
            {
                return failure("Die Source-Project-Session wurde während des Aufbaus ersetzt.", false);
            }

            return entry.TryAcquireLease(out var lease)
                ? new(lease, null)
                : failure("Die Source-Project-Session wird bereits beendet.", true);
        }
    }

    private async Task<(
        AssemblyAnalysisEntry? Entry,
        AssemblyAnalysisRegistryEntryCreation? Creation,
        string? Error)> AcquireSourceProjectEntryAsync(
        string key,
        AssemblySourceSelection selection,
        Project project,
        string resourcePath,
        CancellationToken cancellationToken)
    {
        for (var retry = 0; ; retry++)
        {
            var creation = GetOrCreateSourceProjectEntry(key, selection, project);
            if (creation is null)
            {
                return (null, null, "Die Assembly-Registry wurde bereits beendet.");
            }

            try
            {
                var entry = await creation.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
                return (entry, creation, null);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (ExternalResourceCapacityException exception)
            {
                removeFailedEntry(key, creation);
                if (await TryRetireSourceProjectCapacityAsync(
                        resourcePath,
                        retry,
                        cancellationToken)
                    .ConfigureAwait(false)) continue;
                return (null, null, $"Source-Project-Session konnte nicht aufgebaut werden: {exception.Message}");
            }
            catch (Exception exception)
            {
                removeFailedEntry(key, creation);
                return (null, null, $"Source-Project-Session konnte nicht aufgebaut werden: {exception.Message}");
            }
        }
    }

    private async Task<bool> TryRetireSourceProjectCapacityAsync(
        string resourcePath,
        int retry,
        CancellationToken cancellationToken)
    {
        if (retry >= 1 || !resourceBudget.CanAccommodate(resourcePath)) return false;
        var retired = await runEvictionTick(true, resourcePath, cancellationToken).ConfigureAwait(false);
        return retired > 0;
    }

    private static bool TryFindSourceProject(
        AssemblySourceSelection? sourceSelection,
        AssemblyReferenceDto reference,
        out AssemblySourceSelection selection,
        out Project project,
        out string? error)
    {
        selection = sourceSelection!;
        project = null!;
        if (selection is null || selection.SourceLease.IsDisposed)
        {
            error = $"Die Source-Project-Referenz '{reference.Name}' besitzt keine aktive Source-Selection.";
            return false;
        }

        var projects = selection.SourceLease.Snapshot.Solution.Projects
            .Where(candidate => string.IsNullOrWhiteSpace(reference.SourceProjectPath)
                ? string.Equals(candidate.AssemblyName ?? candidate.Name, reference.Name, StringComparison.OrdinalIgnoreCase)
                : string.Equals(candidate.FilePath, reference.SourceProjectPath, StringComparison.OrdinalIgnoreCase))
            .OrderBy(candidate => candidate.FilePath ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (projects.Length != 1)
        {
            error = $"Die Source-Project-Referenz '{reference.Name}' ist in der gemappten Solution nicht eindeutig auflösbar.";
            return false;
        }

        error = null;
        project = projects[0];
        return true;
    }

    private AssemblyAnalysisRegistryEntryCreation? GetOrCreateSourceProjectEntry(
        string key,
        AssemblySourceSelection parentSelection,
        Project project)
    {
        lock (gate)
        {
            if (isDisposed()) return null;
            if (entries.TryGetValue(key, out var creation)) return creation;

            var generation = nextGenerations.TryGetValue(key, out var previous)
                ? checked(previous + 1)
                : 1;
            nextGenerations[key] = generation;
            var creationLifetime = new CancellationTokenSource();
            var resourceAcquisition = resourceBudget.Acquire(project.FilePath ?? key);
            if (resourceBudget.IsEnabled && resourceAcquisition.Lease is null)
            {
                var failed = Task.FromException<AssemblyAnalysisEntry>(
                    new ExternalResourceCapacityException(
                        resourceAcquisition.FailureReason ?? "Externe Ressourcen sind nicht verfügbar."));
                var rejected = new AssemblyAnalysisRegistryEntryCreation(creationLifetime, failed);
                observeCreation(key, rejected);
                entries.Add(key, rejected);
                return rejected;
            }

            creation = new AssemblyAnalysisRegistryEntryCreation(
                creationLifetime,
                entryFactory.CreateAsync(
                    new AssemblyAnalysisSourceProjectEntryCreationParameters(
                        key,
                        generation,
                        creationLifetime.Token,
                        resourceAcquisition.Lease,
                        parentSelection,
                        project)));
            entries.Add(key, creation);
            observeCreation(key, creation);
            return creation;
        }
    }

    private static string BuildSourceProjectKey(
        AssemblySourceSelection selection,
        Project project) =>
        $"source:{selection.SourceLease.Snapshot.Identity.StableValue}:{project.Id}";
}
