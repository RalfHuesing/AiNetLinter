#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Assemblies.Analysis.References;
using AiNetLinter.Mcp.Handoffs;

namespace AiNetLinter.Mcp.Tools.SymbolGraph;

internal enum AssemblySearchMode
{
    RootOnly,
    SymbolOwnerOnly,
    BoundedReferenceClosure,
}

internal sealed record AssemblySearchPlan(
    bool RequestedIncludeReferences,
    AssemblySearchMode Mode,
    SymbolHandoffIdentifier? Handoff)
{
    internal static AssemblySearchPlan Create(string? symbolIdentifier, bool includeReferences)
    {
        var handoff = default(SymbolHandoffIdentifier);
        var hasAssemblyHandoff = symbolIdentifier is not null
            && SymbolHandoffIdentifier.TryParse(symbolIdentifier, out handoff)
            && handoff.Origin == SymbolHandoffOrigin.Assembly;
        return new(
            includeReferences,
            includeReferences
                ? AssemblySearchMode.BoundedReferenceClosure
                : hasAssemblyHandoff
                    ? AssemblySearchMode.SymbolOwnerOnly
                    : AssemblySearchMode.RootOnly,
            hasAssemblyHandoff ? handoff : null);
    }

    internal string ToWireValue() => Mode switch
    {
        AssemblySearchMode.RootOnly => "root_only",
        AssemblySearchMode.SymbolOwnerOnly => "symbol_owner_only",
        AssemblySearchMode.BoundedReferenceClosure => "bounded_reference_closure",
        _ => throw new InvalidOperationException($"Unbekannter Assembly-Suchmodus: {Mode}."),
    };
}

internal sealed record AssemblyNavigationSearchOutcome(
    int SearchedAssemblyCount,
    IEnumerable<string> Diagnostics,
    bool ResultsTruncated = false);

internal static class AssemblySearchRouting
{
    internal static async Task<AssemblyAnalysisLease?> ResolveScopeRootAsync(
        AssemblyAnalysisLease root,
        AssemblySearchPlan plan,
        CancellationToken cancellationToken)
    {
        if (plan.Mode == AssemblySearchMode.RootOnly) return root;

        if (plan.Handoff is not { } handoff)
        {
            await root.ExpandReferencesAsync(cancellationToken).ConfigureAwait(false);
            return root;
        }

        if (AssemblyNavigationSupport.MatchesLeaseTarget(
                handoff,
                AssemblyNavigationLeaseAccess.CreateView(root).Identity))
        {
            if (plan.Mode == AssemblySearchMode.BoundedReferenceClosure)
            {
                await root.ExpandReferencesAsync(cancellationToken).ConfigureAwait(false);
            }
            return root;
        }

        var owner = await root.LeaseNavigationOwnerAsync(
            handoff,
            plan.ToWireValue(),
            cancellationToken).ConfigureAwait(false);
        if (owner?.Lease is null) return null;

        if (plan.Mode == AssemblySearchMode.BoundedReferenceClosure)
        {
            await owner.Lease.ExpandReferencesAsync(cancellationToken).ConfigureAwait(false);
        }

        return owner.Lease;
    }

    internal static AssemblyNavigationLeaseSet GetScopeLeases(
        AssemblyAnalysisLease root,
        AssemblyAnalysisLease scopeRoot,
        AssemblySearchPlan plan) =>
        plan.Mode switch
        {
            AssemblySearchMode.RootOnly => new([root], 1, false),
            AssemblySearchMode.SymbolOwnerOnly => new([scopeRoot], 1, false),
            AssemblySearchMode.BoundedReferenceClosure => AssemblyNavigationLeaseAccess.GetLeases(scopeRoot),
            _ => throw new InvalidOperationException($"Unbekannter Assembly-Suchmodus: {plan.Mode}."),
        };

    internal static AssemblyNavigationSummary CreateSummary(
        AssemblyNavigationLeaseSet leaseSet,
        AssemblySearchPlan plan,
        AssemblyNavigationSearchOutcome outcome) =>
        AssemblyNavigationSupport.CreateSummary(new AssemblyNavigationSummaryRequest(
            leaseSet.TotalAssemblyCount,
            outcome.SearchedAssemblyCount,
            leaseSet.AssembliesTruncated,
            outcome.Diagnostics,
            outcome.ResultsTruncated,
            plan.RequestedIncludeReferences,
            plan.ToWireValue()));
}
