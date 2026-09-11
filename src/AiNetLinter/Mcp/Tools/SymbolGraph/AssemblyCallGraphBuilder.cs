#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Tools.CallTree;
using AiNetLinter.Mcp.Scope;
using Microsoft.CodeAnalysis;

namespace AiNetLinter.Mcp.Tools.SymbolGraph;

internal static class AssemblyCallGraphBuilder
{
    internal static async Task<AssemblyCallGraphResult> BuildAsync(
        IReadOnlyList<AssemblyNavigationSource> sources,
        ISymbol targetSymbol,
        GetCallTreeInput input,
        CancellationToken cancellationToken)
    {
        var graphs = new List<AssemblySourceGraph>();
        var diagnostics = new List<string>();
        var scope = FindSymbolTool.ValidateScopeType(input.ScopeType).ScopeType;
        foreach (var source in sources
                     .OrderBy(item => item.CanonicalPath, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(item => item.CanonicalPath, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var graph = await CallGraphTreeBuilder.BuildGraphAsync(
                    new CallTreeBuildRequest(
                        source.Solution,
                        source.Symbol,
                        input.Depth,
                        Math.Max(input.TopN, 1),
                        AssemblyNavigationSupport.ParseDirection(input.Direction),
                        AbsolutePaths: true,
                        IncludeBcl: input.IncludeBcl,
                        HandoffIdentity: source.Identity,
                        ScopeType: scope,
                        IncludeGenerated: input.IncludeGenerated),
                    cancellationToken).ConfigureAwait(false);
                graphs.Add(new(source, AddOrigin(graph, source)));
                if (graph.HardCapTruncated)
                {
                    diagnostics.Add(
                        $"Call-Graph in '{source.CanonicalPath}' erreicht das Knotenlimit.");
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                diagnostics.Add(
                    $"Call-Graph in '{source.CanonicalPath}' war unvollständig: {exception.Message}");
            }
        }

        var merged = Merge(graphs, targetSymbol);
        if (merged.HardCapTruncated)
        {
            diagnostics.Add(
                $"Zusammengeführter Assembly-Call-Graph auf {CallGraphTreeBuilder.MaxCallTreeNodes} Knoten/Kanten begrenzt.");
        }

        return new(merged, diagnostics.Count > 0, diagnostics);
    }

    private static CallGraphPayload Merge(
        IReadOnlyList<AssemblySourceGraph> graphs,
        ISymbol targetSymbol)
    {
        if (graphs.Count == 0)
        {
            return new(
                "n1",
                [new CallGraphNode(
                    "n1",
                    CallGraphTraversal.GetStableSymbolId(targetSymbol),
                    targetSymbol.Name,
                    string.Empty,
                    targetSymbol.Kind.ToString().ToLowerInvariant())],
                []);
        }

        var nodes = new List<CallGraphNode>();
        var nodeIds = new Dictionary<(string Source, string LocalId), string>();
        foreach (var graph in graphs)
        {
            foreach (var node in graph.Payload.Nodes)
            {
                var globalId = $"n{nodes.Count + 1}";
                nodeIds[(graph.Source.CanonicalPath, node.NodeId)] = globalId;
                nodes.Add(new CallGraphNode(
                    globalId,
                    node.SymbolId,
                    node.Name,
                    node.DisplayLine,
                    node.Kind));
            }
        }

        var root = nodeIds[(graphs[0].Source.CanonicalPath, graphs[0].Payload.RootNodeId)];
        var edges = new Dictionary<(string From, string To, string? Dispatch), List<CallGraphCallSite>>();
        foreach (var graph in graphs)
        {
            foreach (var edge in graph.Payload.Edges)
            {
                if (!nodeIds.TryGetValue((graph.Source.CanonicalPath, edge.FromNodeId), out var from)
                    || !nodeIds.TryGetValue((graph.Source.CanonicalPath, edge.ToNodeId), out var to))
                {
                    continue;
                }

                var key = (from, to, edge.DispatchKind);
                if (!edges.TryGetValue(key, out var callSites))
                {
                    callSites = [];
                    edges[key] = callSites;
                }

                callSites.AddRange(edge.CallSites);
            }
        }

        var mergedEdges = edges
            .OrderBy(pair => pair.Key.From, StringComparer.Ordinal)
            .ThenBy(pair => pair.Key.To, StringComparer.Ordinal)
            .ThenBy(pair => pair.Key.Dispatch, StringComparer.Ordinal)
            .Select(pair => new CallGraphEdge(
                pair.Key.From,
                pair.Key.To,
                pair.Value.Distinct().OrderBy(site => site.FilePath, StringComparer.Ordinal)
                    .ThenBy(site => site.Line)
                    .ThenBy(site => site.Column)
                    .ThenBy(site => site.ProjectName, StringComparer.Ordinal)
                    .ToList(),
                pair.Key.Dispatch))
            .ToList();
        return ApplyGlobalHardCap(new(
            root,
            nodes,
            mergedEdges,
            graphs.SelectMany(item => item.Payload.MethodHints ?? [])
                .GroupBy(hint => hint.SymbolId, StringComparer.Ordinal)
                .Select(group => group.First())
                .OrderBy(hint => hint.Name, StringComparer.Ordinal)
                .ThenBy(hint => hint.SymbolId, StringComparer.Ordinal)
                .Take(5)
                .ToList(),
            graphs.Any(item => item.Payload.TopNTruncated),
            graphs.Any(item => item.Payload.ScopeFiltered),
            graphs.Sum(item => item.Payload.HiddenEdgeCount),
            graphs.Any(item => item.Payload.HardCapTruncated)));
    }

    /// <summary>Begrenzt den bereits zusammengeführten Assembly-Graphen auf den globalen Hardcap.</summary>
    internal static CallGraphPayload ApplyGlobalHardCap(CallGraphPayload graph)
    {
        var root = graph.Nodes.FirstOrDefault(node =>
            string.Equals(node.NodeId, graph.RootNodeId, StringComparison.Ordinal));
        var orderedNodes = root is null
            ? graph.Nodes.ToList()
            : new[] { root }
                .Concat(graph.Nodes.Where(node => !ReferenceEquals(node, root)))
                .ToList();
        var nodes = orderedNodes.Take(CallGraphTreeBuilder.MaxCallTreeNodes).ToList();
        var nodeIds = nodes.Select(node => node.NodeId).ToHashSet(StringComparer.Ordinal);
        var edges = graph.Edges
            .Where(edge => nodeIds.Contains(edge.FromNodeId) && nodeIds.Contains(edge.ToNodeId))
            .Take(CallGraphTreeBuilder.MaxCallTreeNodes)
            .ToList();
        var truncated = graph.HardCapTruncated
            || nodes.Count < graph.Nodes.Count
            || edges.Count < graph.Edges.Count;
        var hiddenEdgeCount = graph.HiddenEdgeCount + Math.Max(0, graph.Edges.Count - edges.Count);
        return graph with
        {
            Nodes = nodes,
            Edges = edges,
            HiddenEdgeCount = hiddenEdgeCount,
            HardCapTruncated = truncated,
        };
    }

    private static CallGraphPayload AddOrigin(
        CallGraphPayload graph,
        AssemblyNavigationSource source) =>
        graph with
        {
            Nodes = graph.Nodes.Select(node => new CallGraphNode(
                node.NodeId,
                node.SymbolId,
                node.Name,
                $"{node.DisplayLine} [assembly={source.CanonicalPath}; origin={source.Origin.OriginKind}]",
                node.Kind)).ToList(),
            MethodHints = graph.MethodHints?.Select(hint => hint with
            {
                DisplayLine = $"{hint.DisplayLine} [assembly={source.CanonicalPath}; origin={source.Origin.OriginKind}]",
            }).ToList(),
        };

    private sealed record AssemblySourceGraph(
        AssemblyNavigationSource Source,
        CallGraphPayload Payload);
}
