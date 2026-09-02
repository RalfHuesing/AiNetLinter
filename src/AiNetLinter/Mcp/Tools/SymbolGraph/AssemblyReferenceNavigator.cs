#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Tools.CallTree;
using AiNetLinter.Mcp.Tools.MetricsTree;
using Microsoft.CodeAnalysis;

namespace AiNetLinter.Mcp.Tools.SymbolGraph;

internal sealed record AssemblyReferenceTraversalRequest(
    IReadOnlyList<AssemblyNavigationSource> Sources,
    int MaxResults,
    int RequestedDepth,
    AssemblyNavigationSummary Navigation);

internal static class AssemblyReferenceNavigator
{
    internal static async Task<ReferenceTraversalResult> FindReferencesAsync(
        AssemblyReferenceTraversalRequest request,
        CancellationToken cancellationToken)
    {
        var partialDiagnostics = request.Navigation.Diagnostics.ToList();
        var results = new List<ReferenceTraversalResult>();
        foreach (var source in request.Sources)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var traversal = await CallGraphTraversal.ExpandAsync(
                    new ReferenceTraversalRequest(
                        source.Solution,
                        source.Symbol,
                        request.RequestedDepth,
                        Math.Max(request.MaxResults, 1),
                        cancellationToken,
                        AssemblySymbolIdentity: source.Identity)).ConfigureAwait(false);
                results.Add(traversal with
                {
                    CallSites = traversal.CallSites
                        .Select(entry => entry with
                        {
                            Origin = source.Origin,
                        })
                        .ToList(),
                });
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                partialDiagnostics.Add(
                    $"Referenzsuche in '{source.CanonicalPath}' war unvollständig: {exception.Message}");
            }
        }

        return MergeTraversals(
            results,
            request.RequestedDepth,
            request.MaxResults,
            request.Navigation with
            {
                Completeness = partialDiagnostics.Count == 0 ? "complete" : "partial",
                Diagnostics = AssemblyNavigationSupport.DistinctDiagnostics(partialDiagnostics),
            });
    }

    internal static async Task<(MetricsTreeNode Root, bool Truncated, IReadOnlyList<string> Diagnostics)> BuildCallTreeAsync(
        IReadOnlyList<AssemblyNavigationSource> sources,
        ISymbol targetSymbol,
        GetCallTreeInput input,
        CancellationToken cancellationToken)
    {
        var trees = new List<(AssemblyNavigationSource Source, MetricsTreeNode Root)>();
        var diagnostics = new List<string>();
        foreach (var source in sources)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var (tree, truncated) = await CallGraphTreeBuilder.BuildTreeAsync(
                    new CallTreeBuildRequest(
                        source.Solution,
                        source.Symbol,
                        input.Depth,
                        Math.Max(input.TopN, 1),
                        AssemblyNavigationSupport.ParseDirection(input.Direction)),
                    cancellationToken).ConfigureAwait(false);
                trees.Add((source, AssemblyNavigationSupport.AddOrigin(tree, source.Origin)));
                if (truncated)
                {
                    diagnostics.Add(
                        $"Call-Tree in '{source.CanonicalPath}' erreicht das Knotenlimit.");
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                diagnostics.Add(
                    $"Call-Tree in '{source.CanonicalPath}' war unvollständig: {exception.Message}");
            }
        }

        if (trees.Count == 0)
        {
            return (
                new MetricsTreeNode(
                    targetSymbol.Name,
                    string.Empty,
                    0,
                    0,
                    "<kein Assembly-Symbol verfügbar>",
                    []),
                true,
                ["Kein kompatibler Root-Symbolkontext für die Assembly-Referenz gefunden."]);
        }

        var first = trees[0].Root;
        var children = trees.SelectMany(item => item.Root.Children).ToList();
        var rootDisplay = string.Join(
            "; ",
            trees.Select(item =>
                    $"{item.Root.DisplayLine} [assembly={item.Source.CanonicalPath}]")
                .Distinct(StringComparer.Ordinal));
        return (
            first with { DisplayLine = rootDisplay, Children = children },
            diagnostics.Count > 0,
            diagnostics);
    }

    private static ReferenceTraversalResult MergeTraversals(
        IReadOnlyList<ReferenceTraversalResult> traversals,
        int requestedDepth,
        int maxResults,
        AssemblyNavigationSummary navigation)
    {
        var effectiveDepth = traversals.Count == 0
            ? Math.Clamp(requestedDepth, 1, CallGraphTraversal.MaxRecursionDepth)
            : traversals.Min(item => item.Completeness.EffectiveDepth);
        var ordered = traversals
            .SelectMany(item => item.CallSites)
            .Distinct()
            .OrderBy(item => item.Depth)
            .ThenBy(item => item.Origin?.CanonicalPath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.FilePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Line)
            .ThenBy(item => item.SymbolName, StringComparer.Ordinal)
            .ToList();
        var shown = ordered.Take(Math.Max(maxResults, 1)).ToList();
        var diagnostics = navigation.Diagnostics;
        var completeness = new TraversalCompleteness(
            requestedDepth,
            effectiveDepth,
            traversals.Sum(item => item.Completeness.VisitedNodeCount),
            ordered.Count,
            shown.Count,
            ordered.Count > shown.Count || traversals.Any(item => item.Completeness.TruncatedByMaxResults),
            traversals.Any(item => item.Completeness.TruncatedByNodeLimit),
            requestedDepth != effectiveDepth || traversals.Any(item => item.Completeness.DepthWasClamped),
            diagnostics);
        return new(shown, completeness, navigation);
    }
}
