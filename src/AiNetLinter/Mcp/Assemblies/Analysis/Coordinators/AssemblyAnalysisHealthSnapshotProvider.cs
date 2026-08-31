#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Assemblies.Analysis;

namespace AiNetLinter.Mcp.Assemblies.Analysis.Coordinators;

internal sealed class AssemblyAnalysisHealthSnapshotProvider
{
    private readonly Lock gate;
    private readonly Dictionary<string, AssemblyAnalysisRegistryEntryCreation> entries;

    internal AssemblyAnalysisHealthSnapshotProvider(
        Lock gate,
        Dictionary<string, AssemblyAnalysisRegistryEntryCreation> entries)
    {
        this.gate = gate;
        this.entries = entries;
    }

    internal async Task<IReadOnlyList<AssemblyAnalysisHealthSnapshot>> GetSnapshotsAsync()
    {
        KeyValuePair<string, AssemblyAnalysisRegistryEntryCreation>[] current;
        lock (gate)
        {
            current = [.. entries];
        }

        var snapshots = await Task.WhenAll(
            current.Select(pair => CreateSnapshotAsync(pair.Key, pair.Value))).ConfigureAwait(false);
        return snapshots
            .OrderBy(snapshot => snapshot.TargetPath, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static async Task<AssemblyAnalysisHealthSnapshot> CreateSnapshotAsync(
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
