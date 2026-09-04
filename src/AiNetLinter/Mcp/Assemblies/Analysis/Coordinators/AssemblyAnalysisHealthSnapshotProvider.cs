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
    private readonly string? daemonProfile;

    internal AssemblyAnalysisHealthSnapshotProvider(
        Lock gate,
        Dictionary<string, AssemblyAnalysisRegistryEntryCreation> entries,
        string? daemonProfile = null)
    {
        this.gate = gate;
        this.entries = entries;
        this.daemonProfile = daemonProfile;
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

    private async Task<AssemblyAnalysisHealthSnapshot> CreateSnapshotAsync(
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
                Diagnostics: exception is null ? Array.Empty<string>() : [exception.Message],
                DaemonProfile: daemonProfile,
                ErrorCode: "assembly-session-failed",
                ErrorPhase: "session-creation",
                ErrorCause: exception?.Message,
                NextAction: "Letzte Diagnose prüfen.");
        }

        if (creation.Task.IsCanceled)
        {
            return new(
                key,
                "failed",
                Diagnostics: ["Die Assembly-Session wurde abgebrochen."],
                DaemonProfile: daemonProfile,
                ErrorCode: "assembly-session-cancelled",
                ErrorPhase: "session-creation",
                ErrorCause: "Creation wurde abgebrochen.",
                NextAction: "Auftrag erneut ausführen.");
        }

        var entry = await creation.Task.ConfigureAwait(false);
        return CreateCompletedSnapshot(entry, daemonProfile);
    }

    private static AssemblyAnalysisHealthSnapshot CreateCompletedSnapshot(
        AssemblyAnalysisEntry entry,
        string? daemonProfile)
    {
        var context = entry.Context;
        return new(
            entry.CanonicalPath,
            context.Status.ToWireValue(),
            context.Origin.OriginKind,
            context.Origin.ContentHash,
            context.Origin.GeneratedDocumentPath,
            context.Origin.Confidence,
            context.Generation,
            context.Diagnostics,
            daemonProfile,
            "released",
            "bounded",
            "not-observed",
            context.Diagnostics.FirstOrDefault()?.Split(':').FirstOrDefault(),
            "decompilation",
            context.Diagnostics.FirstOrDefault(),
            "Keine Aktion erforderlich.");
    }
}
