#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Assemblies.Analysis.References;
using AiNetLinter.Mcp.Handoffs;
using AiNetLinter.Output;
using Microsoft.CodeAnalysis;
using ModelContextProtocol.Protocol;
using AiNetLinter.Mcp.Tools.SymbolGraph.Navigation;

namespace AiNetLinter.Mcp.Tools.SymbolGraph;

internal static class AssemblySymbolResolver
{
    internal static async Task<(AssemblySymbolTarget? Target, CallToolResult? Error, AssemblyNavigationSummary Navigation)> ResolveAsync(
        AssemblyAnalysisLease root,
        string identifier,
        AssemblySearchPlan plan,
        CancellationToken cancellationToken)
    {
        var handoff = ParseHandoff(identifier);
        if (handoff.Error is not null)
        {
            return (null, handoff.Error,
                AssemblySearchRouting.CreateSummary(new([root], 1, false), plan, new(0, Array.Empty<string>())));
        }

        var scopeRoot = await AssemblySearchRouting.ResolveScopeRootAsync(root, plan, cancellationToken)
            .ConfigureAwait(false);
        var allLeases = scopeRoot is null
            ? new AssemblyNavigationLeaseSet([root], 1, false)
            : AssemblySearchRouting.GetScopeLeases(root, scopeRoot, plan);
        if (GetHandoffScopeError(identifier, handoff, scopeRoot, allLeases, plan) is { } handoffError)
        {
            return (null, handoffError,
                AssemblySearchRouting.CreateSummary(allLeases, plan, new(0, Array.Empty<string>())));
        }

        if (scopeRoot is null)
        {
            return (null, McpToolResults.TargetMismatch(SymbolHandoffIdentifier.ForError(identifier)),
                AssemblySearchRouting.CreateSummary(allLeases, plan, new(0, Array.Empty<string>())));
        }

        var leaseSet = AssemblySearchRouting.GetScopeLeases(root, scopeRoot, plan);
        var leases = leaseSet.Leases;
        var diagnostics = AssemblyNavigationSupport.CreateExpansionDiagnostics(
            AssemblyNavigationLeaseAccess.CreateView(
                scopeRoot));
        var candidates = await ResolveCandidatesAsync(
            leases,
            identifier,
            handoff.Value,
            diagnostics,
            cancellationToken).ConfigureAwait(false);

        var navigation = AssemblySearchRouting.CreateSummary(leaseSet, plan, new(leases.Count, diagnostics));
        if (candidates.Count == 0)
        {
            return (null, McpToolResults.SymbolNotFound(identifier), navigation);
        }

        var distinct = candidates
            .GroupBy(
                candidate => candidate.Lease.Context.Origin.ContentHash + "|" +
                             CallGraphTraversal.GetStableSymbolId(candidate.Symbol),
                StringComparer.Ordinal)
            .Select(group => group.First())
            .ToList();
        if (distinct.Count > 1)
        {
            var lines = distinct
                .SelectMany(AssemblySymbolCandidateFormatter.FormatLocations)
                .OrderBy(line => line, StringComparer.Ordinal)
                .ToList();
            return (null, McpToolResults.AmbiguousSymbol(identifier, lines), navigation);
        }

        return (distinct[0], null, navigation);
    }

    private static (SymbolHandoffIdentifier? Value, CallToolResult? Error) ParseHandoff(string identifier)
    {
        var looksLikeHandoff = SymbolHandoffIdentifier.HasWirePrefix(identifier)
            || SymbolHandoffIdentifier.HasUnsupportedPrefix(identifier);
        if (!looksLikeHandoff) return (null, null);
        return SymbolHandoffIdentifier.TryParse(identifier, out var handoff)
            ? (handoff, null)
            : (null, McpToolResults.InvalidArgument(
                "Die Handoff-ID ist nicht kanonisch.",
                hint: "Eine ID aus dem StructuredContent des aktuellen Ergebnisses kopieren.",
                fieldPath: "$.symbolIdentifier"));
    }

    private static CallToolResult? GetHandoffScopeError(
        string identifier,
        (SymbolHandoffIdentifier? Value, CallToolResult? Error) handoff,
        AssemblyAnalysisLease? scopeRoot,
        AssemblyNavigationLeaseSet allLeases,
        AssemblySearchPlan plan)
    {
        if (handoff.Value is not { } value) return null;
        if (value.Origin != SymbolHandoffOrigin.Assembly || scopeRoot is null)
        {
            return McpToolResults.TargetMismatch(SymbolHandoffIdentifier.ForError(identifier));
        }

        return AssemblyNavigationSupport.MatchesLeaseIdentity(
            value,
            AssemblyNavigationLeaseAccess.CreateView(scopeRoot).Identity)
            ? null
            : McpToolResults.StaleSnapshot(SymbolHandoffIdentifier.ForError(identifier));
    }

    private static async Task<List<AssemblySymbolTarget>> ResolveCandidatesAsync(
        IReadOnlyList<AssemblyAnalysisLease> leases,
        string identifier,
        SymbolHandoffIdentifier? handoff,
        List<string> diagnostics,
        CancellationToken cancellationToken)
    {
        var candidates = new List<AssemblySymbolTarget>();
        var hasAssemblyId = handoff is { Origin: SymbolHandoffOrigin.Assembly };
        foreach (var lease in leases)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var view = AssemblyNavigationLeaseAccess.CreateView(lease);
            if (hasAssemblyId && !AssemblyNavigationSupport.MatchesLeaseIdentity(handoff!.Value, view.Identity)) continue;

            var solution = lease.Server.GetCurrentSolution();
            if (solution is null)
            {
                diagnostics.Add($"Assembly-Session '{lease.CanonicalPath}' besitzt keine lesbare Solution.");
                continue;
            }

            var resolved = await ResolveLeaseAsync(solution, lease, identifier, cancellationToken).ConfigureAwait(false);
            if (resolved.Symbol is not null) candidates.Add(new(resolved.Symbol, lease));
            else if (resolved.Diagnostic is not null && !hasAssemblyId) diagnostics.Add(resolved.Diagnostic);
        }

        return candidates;
    }

    private static async Task<(ISymbol? Symbol, string? Diagnostic)> ResolveLeaseAsync(
        Solution solution,
        AssemblyAnalysisLease lease,
        string identifier,
        CancellationToken cancellationToken)
    {
        try
        {
            return await FindInLeaseAsync(
                solution,
                lease,
                identifier,
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return (null, $"Symbolauflösung in '{lease.CanonicalPath}' war unvollständig: {exception.Message}");
        }
    }

    private static async Task<(ISymbol? Symbol, string? Diagnostic)> FindInLeaseAsync(
        Solution solution,
        AssemblyAnalysisLease lease,
        string identifier,
        CancellationToken cancellationToken)
    {
        var (symbol, error) = await FindReferencesTool.ResolveSymbolAsync(
            solution,
            identifier,
            cancellationToken,
            AssemblyNavigationLeaseAccess.CreateView(lease).Identity).ConfigureAwait(false);
        if (symbol is not null)
        {
            return (symbol, null);
        }

        var diagnostic = error?.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text;
        return IsExpectedResolutionMiss(diagnostic)
            ? (null, null)
            : (null, diagnostic);
    }

    private static bool IsExpectedResolutionMiss(string? diagnostic) =>
        diagnostic?.StartsWith(
            $"[ERROR]: {LinterErrorCodes.SymbolNotFound}:",
            StringComparison.Ordinal) == true;

}
