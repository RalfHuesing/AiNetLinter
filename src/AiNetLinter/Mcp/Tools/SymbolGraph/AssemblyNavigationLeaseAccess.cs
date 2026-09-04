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
        var all = new List<AssemblyAnalysisLease>();
        var pending = new Stack<AssemblyAnalysisLease>();
        var visited = new HashSet<AssemblyAnalysisLease>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var lease = pending.Pop();
            if (!visited.Add(lease)) continue;

            all.Add(lease);
            var children = lease.ReferenceLeasesSnapshot();
            for (var index = children.Count - 1; index >= 0; index--)
            {
                pending.Push(children[index]);
            }
        }

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
                origin.Confidence),
            lease.ReferenceSessions,
            lease.ReferenceExpansionDiagnostics);
    }
}
