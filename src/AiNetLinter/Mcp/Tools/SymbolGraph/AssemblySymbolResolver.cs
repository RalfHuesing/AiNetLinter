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
    // ainetlinter-disable MaxMethodLineCount — die Resolver-Pipeline bildet Diagnose, Identity und Navigation in einem Ergebnis.
    internal static async Task<(AssemblySymbolTarget? Target, CallToolResult? Error, AssemblyNavigationSummary Navigation)> ResolveAsync(
        AssemblyAnalysisLease root,
        string identifier,
        CancellationToken cancellationToken)
    {
        var leaseSet = AssemblyNavigationLeaseAccess.GetLeases(root);
        var leases = leaseSet.Leases;
        var looksLikeHandoff = SymbolHandoffIdentifier.HasWirePrefix(identifier)
            || SymbolHandoffIdentifier.HasUnsupportedPrefix(identifier);
        var providedIdentifier = default(SymbolHandoffIdentifier);
        if (looksLikeHandoff && !SymbolHandoffIdentifier.TryParse(identifier, out providedIdentifier))
        {
            return (null, McpToolResults.InvalidArgument(
                "Die Handoff-ID ist nicht kanonisch.",
                hint: "Eine ID aus dem StructuredContent des aktuellen Ergebnisses kopieren.",
                fieldPath: "$.symbolIdentifier"),
                AssemblyNavigationSupport.CreateSummary(new AssemblyNavigationSummaryRequest(
                    leaseSet.TotalAssemblyCount, leases.Count, leaseSet.AssembliesTruncated, Array.Empty<string>())));
        }

        if (looksLikeHandoff)
        {
            var targetLease = leases.FirstOrDefault(lease =>
                AssemblyNavigationSupport.MatchesLeaseTarget(
                    providedIdentifier,
                    AssemblyNavigationLeaseAccess.CreateView(lease).Identity));
            var exactSnapshot = targetLease is not null
                && AssemblyNavigationSupport.MatchesLeaseIdentity(
                    providedIdentifier,
                    AssemblyNavigationLeaseAccess.CreateView(targetLease).Identity);
            if (providedIdentifier.Origin != SymbolHandoffOrigin.Assembly || targetLease is null)
            {
                return (null, McpToolResults.TargetMismatch(SymbolHandoffIdentifier.ForError(identifier)),
                    AssemblyNavigationSupport.CreateSummary(new AssemblyNavigationSummaryRequest(
                        leaseSet.TotalAssemblyCount, leases.Count, leaseSet.AssembliesTruncated, Array.Empty<string>())));
            }

            if (!exactSnapshot)
            {
                return (null, McpToolResults.StaleSnapshot(SymbolHandoffIdentifier.ForError(identifier)),
                    AssemblyNavigationSupport.CreateSummary(new AssemblyNavigationSummaryRequest(
                        leaseSet.TotalAssemblyCount, leases.Count, leaseSet.AssembliesTruncated, Array.Empty<string>())));
            }
        }

        var diagnostics = AssemblyNavigationSupport.CreateExpansionDiagnostics(
            AssemblyNavigationLeaseAccess.CreateView(root));
        var candidates = await ResolveCandidatesAsync(
            leases,
            identifier,
            looksLikeHandoff ? providedIdentifier : null,
            diagnostics,
            cancellationToken).ConfigureAwait(false);

        var navigation = AssemblyNavigationSupport.CreateSummary(new AssemblyNavigationSummaryRequest(
            leaseSet.TotalAssemblyCount,
            leases.Count,
            leaseSet.AssembliesTruncated,
            diagnostics));
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
