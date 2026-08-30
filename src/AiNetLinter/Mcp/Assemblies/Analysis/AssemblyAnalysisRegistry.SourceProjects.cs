#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Assemblies.Analysis.References;
using AiNetLinter.Mcp.Assemblies.ExternalSource.Snapshots;
using AiNetLinter.Mcp.Tools.AssemblyAnalysis;
using AiNetLinter.Output;
using Microsoft.CodeAnalysis;

namespace AiNetLinter.Mcp.Assemblies.Analysis;

internal sealed partial class AssemblyAnalysisRegistry
{
    private async Task<AssemblyAnalysisLeaseResult> LeaseSourceProjectAsync(
        AssemblySourceSelection? sourceSelection,
        AssemblyReferenceDto reference,
        CancellationToken cancellationToken)
    {
        if (!TryFindSourceProject(sourceSelection, reference, out var selection, out var project, out var error))
        {
            return Failure(error!, isError: false);
        }

        var key = BuildSourceProjectKey(selection, project);
        var creation = GetOrCreateSourceProjectEntry(key, selection, project);
        if (creation is null)
        {
            return Failure("Die Assembly-Registry wurde bereits beendet.");
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
        catch (Exception exception)
        {
            RemoveFailedEntry(key, creation);
            return Failure($"Source-Project-Session konnte nicht aufgebaut werden: {exception.Message}");
        }

        lock (gate)
        {
            if (!entries.TryGetValue(key, out var current)
                || !ReferenceEquals(current, creation))
            {
                return Failure("Die Source-Project-Session wurde während des Aufbaus ersetzt.", isError: false);
            }

            return entry.TryAcquireLease(out var lease)
                ? new(lease, null)
                : Failure("Die Source-Project-Session wird bereits beendet.");
        }
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
            if (Volatile.Read(ref disposed) != 0) return null;
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
                ObserveCreation(key, rejected);
                entries.Add(key, rejected);
                return rejected;
            }

            creation = new AssemblyAnalysisRegistryEntryCreation(
                creationLifetime,
                CreateSourceProjectEntryAsync(
                    key,
                    generation,
                    creationLifetime.Token,
                    resourceAcquisition.Lease,
                    parentSelection,
                    project));
            entries.Add(key, creation);
            ObserveCreation(key, creation);
            return creation;
        }
    }

    private async Task<AssemblyAnalysisEntry> CreateSourceProjectEntryAsync(
        string key,
        long generation,
        CancellationToken creationToken,
        ExternalResourceLease? resourceLease,
        AssemblySourceSelection parentSelection,
        Project project)
    {
        ExternalResourceOperationLease? operation = null;
        SourceSnapshotLease? projectLease = null;
        var resourceTransferred = false;
        var sourceTransferred = false;
        try
        {
            operation = resourceBudget.BeginOperation(creationToken);
            projectLease = parentSelection.SourceLease.AcquireSibling();
            var selection = parentSelection.ForProject(projectLease, project)
                ?? throw new InvalidOperationException("Source-Project-Selection konnte nicht erzeugt werden.");
            var targetPath = project.FilePath ?? key;
            var sourceResult = await AssemblyAnalysisContextFactory.CreateSourceProjectContextAsync(
                targetPath,
                project,
                selection,
                creationToken).ConfigureAwait(false);
            if (sourceResult.Context is null)
            {
                throw new InvalidOperationException(sourceResult.Error ?? "Source-Project-Context konnte nicht erzeugt werden.");
            }

            var context = sourceResult.Context with { Generation = generation };
            var entry = AssemblyAnalysisEntry.Create(new AssemblyAnalysisEntryCreateParameters(
                targetPath,
                parentSelection.SourceLease.Snapshot.Solution,
                context,
                projectLease,
                resourceLease,
                CreateReferenceLeaseFactory(selection)));
            resourceTransferred = true;
            sourceTransferred = true;
            projectLease = null;
            return entry;
        }
        finally
        {
            operation?.Dispose();
            if (!resourceTransferred) resourceLease?.Dispose();
            if (!sourceTransferred) projectLease?.Dispose();
        }
    }

    private static string BuildSourceProjectKey(
        AssemblySourceSelection selection,
        Project project) =>
        $"source:{selection.SourceLease.Snapshot.Identity.StableValue}:{project.Id}";
}
