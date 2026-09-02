#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
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
            snapshot.Diagnostics);

    internal static AssemblyHealthEntry FromLease(AssemblyAnalysisLease lease)
    {
        var origin = lease.Context.Origin;
        var diagnostics = lease.Context.Diagnostics
            .Concat(lease.ReferenceExpansionDiagnostics)
            .ToArray();
        var effectiveStatus = lease.Context.Status.ResolveEffectiveStatus(diagnostics);
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
            TransitiveDiagnostics: lease.ReferenceExpansionDiagnostics);
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
}
