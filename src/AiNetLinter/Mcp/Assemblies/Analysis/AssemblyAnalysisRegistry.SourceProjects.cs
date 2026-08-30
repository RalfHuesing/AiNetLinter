#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
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
                sourceProjectEntryFactory.CreateAsync(
                    new AssemblyAnalysisSourceProjectEntryCreationParameters(
                        key,
                        generation,
                        creationLifetime.Token,
                        resourceAcquisition.Lease,
                        parentSelection,
                        project)));
            entries.Add(key, creation);
            ObserveCreation(key, creation);
            return creation;
        }
    }

    private static string BuildSourceProjectKey(
        AssemblySourceSelection selection,
        Project project) =>
        $"source:{selection.SourceLease.Snapshot.Identity.StableValue}:{project.Id}";

    Task<IReadOnlyList<AssemblyAnalysisHealthSnapshot>> IAssemblyAnalysisRegistry.SnapshotsAsync() =>
        SnapshotsAsync();

    internal async Task<IReadOnlyList<AssemblyAnalysisHealthSnapshot>> SnapshotsAsync()
    {
        KeyValuePair<string, AssemblyAnalysisRegistryEntryCreation>[] current;
        lock (gate)
        {
            current = [.. entries];
        }

        var snapshots = await Task.WhenAll(
            current.Select(pair => CreateHealthSnapshotAsync(pair.Key, pair.Value))).ConfigureAwait(false);
        return snapshots
            .OrderBy(snapshot => snapshot.TargetPath, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static async Task<AssemblyAnalysisHealthSnapshot> CreateHealthSnapshotAsync(
        string key,
        AssemblyAnalysisRegistryEntryCreation creation)
    {
        if (!creation.Task.IsCompleted)
        {
            return new(key, "loading");
        }

        if (creation.Task.IsFaulted)
        {
            var exception = creation.Task.Exception?.GetBaseException();
            return new(
                key,
                "failed",
                Diagnostics: exception is null ? Array.Empty<string>() : [exception.Message]);
        }

        if (creation.Task.IsCanceled)
        {
            return new(key, "failed", Diagnostics: ["Die Assembly-Session wurde abgebrochen."]);
        }

        var entry = await creation.Task.ConfigureAwait(false);
        var context = entry.Context;
        return new(
            entry.CanonicalPath,
            context.Status.ToWireValue(),
            context.Origin.OriginKind,
            context.Origin.SourceProjectPath,
            context.Origin.SourceSnapshotIdentity,
            context.Origin.ContentHash,
            context.Origin.GeneratedDocumentPath,
            context.Origin.Confidence,
            context.Origin.Trust,
            context.Generation,
            context.Diagnostics);
    }
}
