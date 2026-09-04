#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using AiNetLinter.Mcp.Assemblies.Analysis;
using AiNetLinter.Mcp.Assemblies.Analysis.References;
using AiNetLinter.Mcp.Tools.AssemblyAnalysis;

namespace AiNetLinter.Mcp.Tools.ServerMaintenance.Projection;

internal static class AssemblyHealthProjection
{
    internal static AssemblyHealthEntry FromSnapshot(AssemblyAnalysisHealthSnapshot snapshot) =>
        new(
            snapshot.TargetPath,
            ResolveEffectiveStatus(snapshot.LoadState, snapshot.Diagnostics ?? Array.Empty<string>()),
            snapshot.OriginKind,
            snapshot.SourceProjectPath,
            snapshot.SourceSnapshot,
            snapshot.ContentHash,
            snapshot.GeneratedDocumentPath,
            snapshot.Confidence,
            snapshot.Trust,
            snapshot.Generation,
            snapshot.Diagnostics,
            LogicalCheckoutKey: snapshot.LogicalCheckoutKey,
            RepositoryId: snapshot.RepositoryId,
            Revision: snapshot.Revision,
            CheckoutStatus: snapshot.CheckoutStatus,
            MappingStatus: snapshot.MappingStatus,
            AnalysisOrigin: snapshot.AnalysisOrigin,
            SourcePolicy: snapshot.SourcePolicy,
            DaemonProfile: snapshot.DaemonProfile,
            LockStatus: snapshot.LockStatus,
            LeaseStatus: snapshot.LeaseStatus,
            CleanupStatus: snapshot.CleanupStatus,
            QuarantineStatus: snapshot.QuarantineStatus,
            ErrorCode: snapshot.ErrorCode,
            ErrorPhase: snapshot.ErrorPhase,
            ErrorCause: snapshot.ErrorCause,
            NextAction: snapshot.NextAction);

    internal static AssemblyHealthEntry FromLease(AssemblyAnalysisLease lease)
    {
        var origin = lease.Context.Origin;
        var diagnostics = lease.Context.Diagnostics
            .Concat(lease.ReferenceExpansionDiagnostics)
            .ToArray();
        var effectiveStatus = lease.Context.Status.ResolveEffectiveStatus(diagnostics);
        var source = origin.SourceSnapshotIdentity;
        return new(
            lease.CanonicalPath,
            effectiveStatus.ToWireValue(),
            origin.OriginKind,
            origin.SourceProjectPath,
            origin.SourceSnapshotIdentity,
            origin.ContentHash,
            origin.GeneratedDocumentPath,
            origin.Confidence,
            origin.Trust,
            lease.Context.Generation,
            lease.Context.Diagnostics,
            Completeness: effectiveStatus.ToCompletenessLabel(),
            TransitiveDiagnostics: lease.ReferenceExpansionDiagnostics,
            LogicalCheckoutKey: source is null ? null : CreateCheckoutKey(source.StableValue),
            RepositoryId: source is null ? null : CreateRepositoryId(source.RepositoryUrl),
            Revision: source?.LoadedRevision,
            CheckoutStatus: origin.IsDecompiled ? "not-applicable" : "verified",
            MappingStatus: source is null ? "not-configured" : "verified",
            AnalysisOrigin: origin.OriginKind,
            SourcePolicy: origin.SourcePolicy,
            LockStatus: "released",
            LeaseStatus: "bounded",
            CleanupStatus: "not-observed",
            QuarantineStatus: "none",
            ErrorPhase: origin.IsDecompiled ? "decompilation" : "source-analysis",
            NextAction: origin.IsDecompiled ? "Source-Mapping/Provider prüfen." : "Keine Aktion erforderlich.");
    }

    internal static AssemblyHealthEntry Project(
        AssemblyHealthEntry assembly,
        bool includeDiagnostics,
        int maxDiagnostics)
    {
        var summary = AssemblyAnalysisResponseLimits.ProjectDiagnostics(
            assembly.Diagnostics,
            assembly.TransitiveDiagnostics,
            maxDiagnostics);
        var diagnostics = (assembly.Diagnostics ?? Array.Empty<string>())
            .Concat(assembly.TransitiveDiagnostics ?? Array.Empty<string>())
            .ToArray();
        var effectiveLoadState = ResolveEffectiveStatus(assembly.LoadState, diagnostics);
        var effectiveCompleteness = ResolveEffectiveStatus(
            assembly.Completeness ?? effectiveLoadState,
            diagnostics);
        if (!includeDiagnostics) summary = AssemblyAnalysisResponseLimits.WithoutSamples(summary);
        return assembly with
        {
            LoadState = effectiveLoadState,
            Diagnostics = includeDiagnostics ? summary.Samples : null,
            DiagnosticsSummary = summary,
            Completeness = effectiveCompleteness,
            TransitiveDiagnostics = null,
        };
    }

    internal static IReadOnlyDictionary<string, int> CountStatuses(
        IEnumerable<AssemblyHealthEntry> assemblies) =>
        assemblies
            .GroupBy(assembly => assembly.LoadState, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

    private static string ResolveEffectiveStatus(
        string statusValue,
        IReadOnlyCollection<string> diagnostics)
    {
        if (!Enum.TryParse<AssemblySessionStatus>(statusValue, ignoreCase: true, out var status)) return statusValue;
        return status.ResolveEffectiveStatus(diagnostics).ToWireValue();
    }

    private static string CreateRepositoryId(string repositoryUrl) =>
        "repo-" + Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(repositoryUrl)))
            .ToLowerInvariant()[..12];

    private static string CreateCheckoutKey(string identity) =>
        "checkout-" + Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(identity)))
            .ToLowerInvariant()[..16];
}
