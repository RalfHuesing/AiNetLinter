#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Scope;
using AiNetLinter.Mcp.Tools.CallTree;
using AiNetLinter.Mcp.Tools.MetricsTree;
using AiNetLinter.Output;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;

namespace AiNetLinter.Mcp.Tools.SymbolGraph.CallGraph;

/// <summary>
/// Echter Eltern-Kind-Baum der transitiven Aufrufer eines Symbols (Caller-Tree von
/// <c>get_call_tree</c>, siehe <see cref="GetCallTreeTool"/>) mit eigenen, hoeheren Grenzwerten
/// (<see cref="MaxCallTreeDepth"/>/<see cref="MaxCallTreeNodes"/>) als die flache BFS-Aggregation
/// in <see cref="CallGraphTraversal"/>. Bewusst eigene Klasse statt Anhang an der flachen
/// Traversierung: beide durchlaufen dieselben Referenzen, aber mit unterschiedlicher
/// Struktur-, Kappungs- und Richtungslogik.
/// </summary>
internal static partial class CallGraphTreeBuilder
{
    internal const int MaxCallTreeDepth = 5;
    internal const int MaxCallTreeNodes = 250;

    /// <summary>
    /// Baut aus demselben Roslyn-Ausgangsmaterial wie der kompatible Baum eine deduplizierte
    /// Graph-Domain. Knoten werden bei ihrer ersten deterministischen Entdeckung lokal mit n1,
    /// n2 usw. benannt; Kanten werden nach From/To zusammengeführt und behalten alle Call-Sites.
    /// </summary>
    internal static async Task<CallGraphPayload> BuildGraphAsync(
        CallTreeBuildRequest request,
        CancellationToken ct)
    {
        var state = new GraphBuildState(request, Math.Clamp(request.RequestedDepth, 1, MaxCallTreeDepth));
        if (request.SeedSymbol is INamedTypeSymbol namedType)
        {
            return state.CreateTypeSeedPayload(namedType);
        }

        await TraverseGraphAsync(state, ct).ConfigureAwait(false);
        return state.CreatePayload();
    }

    private static async Task TraverseGraphAsync(GraphBuildState state, CancellationToken ct)
    {
        while (state.HasQueuedNodes && !state.HardCapTruncated)
        {
            ct.ThrowIfCancellationRequested();
            var (symbol, level) = state.Dequeue();
            if (level > state.Depth) continue;

            var groups = await BuildGraphGroupsAsync(state, symbol, ct).ConfigureAwait(false);
            AddGraphGroups(state, symbol, level, groups, ct);
        }
    }

    private static void AddGraphGroups(
        GraphBuildState state,
        ISymbol symbol,
        int level,
        IReadOnlyList<GraphExpansion> groups,
        CancellationToken ct)
    {
        if (groups.Count > state.TopN) state.MarkTopNTruncated(groups.Count - state.TopN);
        foreach (var group in groups.Take(state.TopN))
        {
            ct.ThrowIfCancellationRequested();
            var fromSymbol = group.Direction == CallTreeDirection.Incoming ? group.Symbol : symbol;
            var toSymbol = group.Direction == CallTreeDirection.Incoming ? symbol : group.Symbol;
            if (!state.TryGetOrAddNode(fromSymbol, out _)
                || !state.TryGetOrAddNode(toSymbol, out var targetNode))
            {
                state.MarkHardCapTruncated();
                break;
            }
            state.AddEdge(fromSymbol, targetNode, group);
            if (level < state.Depth) state.EnqueueIfNew(group.Symbol, level + 1);
        }
    }

    /// <summary>
    /// Baut einen echten Eltern-Kind-Baum der transitiven Aufrufer von <paramref name="seedSymbol"/>
    /// (Caller-Tree, dieselbe Richtung wie <see cref="CallGraphTraversal.ExpandAsync"/>). Im
    /// Unterschied zur flachen Aggregation wird pro Aufrufstelle der einschliessende Symbol via
    /// <see cref="SemanticModel.GetEnclosingSymbol(int, CancellationToken)"/> ermittelt und als
    /// eigener Kindknoten weiterverfolgt — erst das ergibt eine echte Baumstruktur (die flache
    /// Aggregation liefert dieselben Aufrufer als kompakte, nach Tiefe sortierte Liste). Liefert die
    /// Baumstruktur als <see cref="MetricsTreeNode"/> (wiederverwendet aus <c>metrics_tree</c>,
    /// siehe <see cref="MetricsTreeRenderer"/>), damit ASCII- und Mermaid-Renderer dieselbe
    /// Struktur konsumieren koennen. <paramref name="topN"/> begrenzt den Fan-Out: nur die ersten
    /// topN Kinder eines Knotens (stabil sortiert nach Datei:Zeile) werden weiter rekursiv
    /// aufgeloest, alle gefundenen Kinder bleiben aber im Baum, damit der Renderer seine eigene
    /// "... und N weitere"-Kappung anwenden kann. <see cref="MaxCallTreeNodes"/> begrenzt zusaetzlich
    /// hart, wie viele Knoten insgesamt weiter expandiert (d. h. per <c>FindReferencesAsync</c>
    /// abgefragt) werden — bei Erreichen wird <c>Truncated</c> gesetzt statt weiter zu traversieren.
    /// </summary>
    private static async Task<List<GraphExpansion>> BuildGraphGroupsAsync(
        GraphBuildState state,
        ISymbol symbol,
        CancellationToken ct)
    {
        var groups = new List<GraphExpansion>();
        if (state.Direction is CallTreeDirection.Incoming or CallTreeDirection.Both)
        {
            var references = await SymbolFinder.FindReferencesAsync(symbol, state.Solution, ct).ConfigureAwait(false);
            var callers = await GroupByCallerAsync(references, ct).ConfigureAwait(false);
            groups.AddRange(callers.Where(group => group.CallerSymbol is not null).Select(group => new GraphExpansion(group.CallerSymbol!, group.Locations, CallTreeDirection.Incoming, ClassifyDispatchKind(symbol))));
        }
        if (state.Direction is CallTreeDirection.Outgoing or CallTreeDirection.Both)
        {
            var outgoing = await OutgoingCallScanner.ScanAsync(symbol, state.Solution, ct, state.IncludeBcl).ConfigureAwait(false);
            groups.AddRange(outgoing.Select(group => new GraphExpansion(group.Symbol, group.Locations, CallTreeDirection.Outgoing, ClassifyDispatchKind(group.Symbol))));
        }
        var scoped = new List<GraphExpansion>(groups.Count);
        foreach (var group in groups)
        {
            var locations = await FilterLocationsAsync(state, group.Locations, ct).ConfigureAwait(false);
            if (locations.Count > 0) scoped.Add(group with { Locations = locations });
        }
        if (scoped.Count < groups.Count) state.MarkScopeFiltered();
        return scoped.OrderBy(group => group.Direction)
            .ThenBy(group => CallGraphTraversal.GetStableSymbolId(group.Symbol), StringComparer.Ordinal)
            .ThenBy(group => CallGraphTraversal.FormatSymbolName(group.Symbol, CallTreeDirection.Outgoing), StringComparer.Ordinal)
            .ThenBy(group => FirstLocationPath(group.Locations, state.Solution), StringComparer.Ordinal)
            .ThenBy(group => FirstLocationLine(group.Locations)).ToList();
    }

    private static async Task<IReadOnlyList<Location>> FilterLocationsAsync(GraphBuildState state, IReadOnlyList<Location> locations, CancellationToken ct)
    {
        var visible = new List<Location>(locations.Count);
        foreach (var location in locations)
        {
            ct.ThrowIfCancellationRequested();
            if (location.SourceTree is null) continue;
            var document = state.Solution.GetDocument(location.SourceTree);
            if (document is null) continue;
            var scope = await state.ScopeClassifier.ClassifyAsync(document, ct).ConfigureAwait(false);
            if (state.ScopeClassifier.MatchesScope(scope, state.ScopeType) && (state.IncludeGenerated || scope.SourceKind != McpSourceKind.Generated)) visible.Add(location);
        }
        return visible;
    }

    private static string? ClassifyDispatchKind(ISymbol symbol) => symbol switch
    {
        IMethodSymbol { ContainingType.TypeKind: TypeKind.Interface } => "interface",
        IMethodSymbol { IsOverride: true } => "override",
        IMethodSymbol { IsVirtual: true } => "virtual",
        IMethodSymbol { IsAbstract: true } => "virtual",
        _ => null,
    };

    private static string FirstLocationPath(IReadOnlyList<Location> locations, Solution solution) => locations.Count == 0 ? string.Empty : FormatPath(locations[0], solution);
    private static int FirstLocationLine(IReadOnlyList<Location> locations) => locations.Count == 0 ? 0 : locations[0].GetLineSpan().StartLinePosition.Line + 1;
    private sealed record GraphExpansion(
        ISymbol Symbol,
        IReadOnlyList<Location> Locations,
        CallTreeDirection Direction,
        string? DispatchKind);

    private sealed class GraphBuildState
    {
        private readonly Queue<(ISymbol Symbol, int Level)> _queue = new();
        private readonly HashSet<ISymbol> _queued = new(SymbolEqualityComparer.Default);
        private readonly Dictionary<ISymbol, CallGraphNode> _nodesBySymbol =
            new(SymbolEqualityComparer.Default);
        private readonly List<CallGraphNode> _nodes = new();
        private readonly Dictionary<(string From, string To), GraphEdgeAccumulator> _edges = new();

        internal GraphBuildState(CallTreeBuildRequest request, int depth)
        {
            Solution = request.Solution;
            Depth = depth;
            Direction = request.Direction;
            TopN = Math.Max(request.TopN, 1);
            IncludeBcl = request.IncludeBcl;
            ScopeType = request.ScopeType;
            IncludeGenerated = request.IncludeGenerated;
            ScopeClassifier = request.ScopeClassifier ?? new McpScopeClassifier();
            AbsolutePaths = request.AbsolutePaths;
            HandoffIdentity = request.HandoffIdentity;
            RootNodeId = GetOrAddNode(request.SeedSymbol).NodeId;
            _queue.Enqueue((request.SeedSymbol, 1));
            _queued.Add(request.SeedSymbol);
        }

        internal Solution Solution { get; }
        internal int Depth { get; }
        internal CallTreeDirection Direction { get; }
        internal int TopN { get; }
        internal bool IncludeBcl { get; }
        internal McpScopeType ScopeType { get; }
        internal bool IncludeGenerated { get; }
        internal McpScopeClassifier ScopeClassifier { get; }
        internal bool AbsolutePaths { get; }
        internal AnalysisSymbolIdentity? HandoffIdentity { get; }
        internal string RootNodeId { get; }
        internal bool HasQueuedNodes => _queue.Count > 0;
        internal bool ScopeFiltered { get; private set; }
        internal bool TopNTruncated { get; private set; }
        internal bool HardCapTruncated { get; private set; }

        internal (ISymbol Symbol, int Level) Dequeue() => _queue.Dequeue();

        internal CallGraphNode GetOrAddNode(ISymbol symbol)
        {
            if (_nodesBySymbol.TryGetValue(symbol, out var existing)) return existing;

            var node = new CallGraphNode(
                $"n{_nodes.Count + 1}",
                symbol,
                CallGraphTraversal.GetStableSymbolId(symbol, HandoffIdentity),
                CallGraphTraversal.FormatSymbolName(symbol, CallTreeDirection.Outgoing),
                FormatSymbolDisplayLine(symbol, Solution, AbsolutePaths));
            _nodesBySymbol[symbol] = node;
            _nodes.Add(node);
            return node;
        }

        internal bool TryGetOrAddNode(ISymbol symbol, out CallGraphNode node)
        {
            if (_nodesBySymbol.TryGetValue(symbol, out node!)) return true;
            if (_nodes.Count >= MaxCallTreeNodes)
            {
                node = null!;
                return false;
            }

            node = GetOrAddNode(symbol);
            return true;
        }

        internal void EnqueueIfNew(ISymbol symbol, int level)
        {
            if (_queued.Add(symbol)) _queue.Enqueue((symbol, level));
        }

        internal void AddEdge(ISymbol fromSymbol, CallGraphNode toNode, GraphExpansion expansion)
        {
            var fromNode = _nodesBySymbol[fromSymbol];
            var key = (fromNode.NodeId, toNode.NodeId);
            if (!_edges.TryGetValue(key, out var edge))
            {
                edge = new GraphEdgeAccumulator(expansion.DispatchKind);
                edge.SetNodeIds(fromNode.NodeId, toNode.NodeId);
                _edges[key] = edge;
            }
            edge.AddLocations(expansion.Locations, Solution);
            edge.DispatchKind ??= expansion.DispatchKind;
        }

        internal CallGraphPayload CreatePayload()
        {
            var edges = _edges.Values
                .OrderBy(edge => edge.FromNodeId, StringComparer.Ordinal)
                .ThenBy(edge => edge.ToNodeId, StringComparer.Ordinal)
                .Select(edge => edge.ToModel())
                .ToList();
            return new CallGraphPayload(
                RootNodeId,
                _nodes,
                edges,
                MethodHints: [],
                TopNTruncated: TopNTruncated,
                ScopeFiltered: ScopeFiltered,
                HiddenEdgeCount: HiddenEdgeCount,
                HardCapTruncated: HardCapTruncated);
        }

        internal CallGraphPayload CreateTypeSeedPayload(INamedTypeSymbol namedType)
        {
                var hints = namedType.GetMembers()
                .OfType<IMethodSymbol>()
                .Where(method => method.MethodKind == MethodKind.Ordinary)
                .Select(method => new CallGraphMethodHint(
                    CallGraphTraversal.FormatSymbolName(method, CallTreeDirection.Outgoing),
                    CallGraphTraversal.GetStableSymbolId(method, HandoffIdentity),
                    FormatSymbolDisplayLine(method, Solution, AbsolutePaths)))
                .OrderBy(hint => hint.Name, StringComparer.Ordinal)
                .ThenBy(hint => hint.SymbolId, StringComparer.Ordinal)
                .Take(5)
                .ToList();
            return new CallGraphPayload(
                RootNodeId,
                _nodes,
                [],
                hints,
                TopNTruncated: false,
                ScopeFiltered: ScopeFiltered,
                HiddenEdgeCount: 0,
                HardCapTruncated: false);
        }

        internal void MarkScopeFiltered() => ScopeFiltered = true;

        internal int HiddenEdgeCount { get; private set; }

        internal void MarkTopNTruncated(int hiddenCount)
        {
            TopNTruncated = true;
            HiddenEdgeCount += hiddenCount;
        }

        internal void MarkHardCapTruncated() => HardCapTruncated = true;
    }

    private sealed class GraphEdgeAccumulator
    {
        private readonly List<CallGraphCallSite> _callSites = new();

        internal GraphEdgeAccumulator(string? dispatchKind) => DispatchKind = dispatchKind;

        internal string FromNodeId { get; private set; } = string.Empty;
        internal string ToNodeId { get; private set; } = string.Empty;
        internal string? DispatchKind { get; set; }

        internal void AddLocations(IReadOnlyList<Location> locations, Solution solution)
        {
            foreach (var callSite in locations
                .Where(location => location.SourceTree is not null)
                .Select(location => CreateCallSite(location, solution))
                .Distinct())
            {
                if (!_callSites.Contains(callSite)) _callSites.Add(callSite);
            }
        }

        internal CallGraphEdge ToModel() => new(
            FromNodeId,
            ToNodeId,
            _callSites
                .OrderBy(site => site.FilePath, StringComparer.Ordinal)
                .ThenBy(site => site.Line)
                .ThenBy(site => site.Column)
                .ThenBy(site => site.ProjectName, StringComparer.Ordinal)
                .ToList(),
            DispatchKind);

        private static CallGraphCallSite CreateCallSite(Location location, Solution solution)
        {
            var path = FormatPath(location, solution);
            var lineSpan = location.GetLineSpan();
            return new CallGraphCallSite(
                path,
                lineSpan.StartLinePosition.Line + 1,
                lineSpan.StartLinePosition.Character + 1,
                solution.GetDocument(location.SourceTree!)?.Project.Name ?? string.Empty);
        }

        internal void SetNodeIds(string fromNodeId, string toNodeId)
        {
            FromNodeId = fromNodeId;
            ToNodeId = toNodeId;
        }
    }

}
