#nullable enable

using System;
using System.Linq;
using AiNetLinter.Mcp.Assemblies.Analysis.References;
using AiNetLinter.Mcp.Tools.AssemblyAnalysis;

namespace AiNetLinter.Mcp.Tools.SymbolGraph;

internal static class AssemblyNavigationLeaseAccess
{
    private const int MaxNavigationAssemblies = AssemblyAnalysisResponseLimits.MaxReferenceSessions;

    internal static AssemblyNavigationLeaseSet GetLeases(AssemblyAnalysisLease root)
    {
        var all = new[] { root }
            .Concat(root.ReferenceLeasesSnapshot())
            .Distinct()
            .ToList();
        return new(
            all.Take(MaxNavigationAssemblies).ToList(),
            all.Count,
            all.Count > MaxNavigationAssemblies);
    }

    internal static AssemblyNavigationLeaseView CreateView(AssemblyAnalysisLease lease)
    {
        var origin = lease.Context.Origin;
        return new(
            lease.CanonicalPath,
            lease.Server.GetCurrentSolution(),
            new(origin.ContentHash, lease.Context.Generation),
            new(
                origin.OriginKind,
                origin.CanonicalPath,
                origin.ContentHash,
                origin.GeneratedDocumentPath,
                origin.Confidence,
                origin.Trust),
            lease.ReferenceSessions,
            lease.ReferenceExpansionDiagnostics);
    }
}
