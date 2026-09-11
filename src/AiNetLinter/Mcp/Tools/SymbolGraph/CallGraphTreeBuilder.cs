#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Tools.CallTree;
using AiNetLinter.Mcp.Tools.MetricsTree;
using AiNetLinter.Output;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;

namespace AiNetLinter.Mcp.Tools.SymbolGraph;

/// <summary>
/// Echter Eltern-Kind-Baum der transitiven Aufrufer eines Symbols (Caller-Tree von
/// <c>get_call_tree</c>, siehe <see cref="GetCallTreeTool"/>) mit eigenen, hoeheren Grenzwerten
/// (<see cref="MaxCallTreeDepth"/>/<see cref="MaxCallTreeNodes"/>) als die flache BFS-Aggregation
/// in <see cref="CallGraphTraversal"/>. Bewusst eigene Klasse statt Anhang an der flachen
/// Traversierung: beide durchlaufen dieselben Referenzen, aber mit unterschiedlicher
/// Struktur-, Kappungs- und Richtungslogik.
/// </summary>
internal static class CallGraphTreeBuilder
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
        while (state.HasQueuedNodes)
        {
            ct.ThrowIfCancellationRequested();
            var (symbol, level) = state.Dequeue();
            if (level > state.Depth) continue;

            var groups = await BuildGraphGroupsAsync(state, symbol, ct).ConfigureAwait(false);
            foreach (var group in groups)
            {
                ct.ThrowIfCancellationRequested();
                var fromSymbol = group.Direction == CallTreeDirection.Incoming ? group.Symbol : symbol;
                var toSymbol = group.Direction == CallTreeDirection.Incoming ? symbol : group.Symbol;
                state.GetOrAddNode(fromSymbol);
                var targetNode = state.GetOrAddNode(toSymbol);
                state.AddEdge(fromSymbol, targetNode, group);
                if (level < state.Depth)
                {
                    state.EnqueueIfNew(group.Symbol, level + 1);
                }
            }
        }

        return state.CreatePayload();
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
    internal static async Task<(MetricsTreeNode Root, bool Truncated)> BuildTreeAsync(
        Solution solution,
        ISymbol seedSymbol,
        int requestedDepth,
        int topN,
        CancellationToken ct)
        => await BuildTreeAsync(
            new CallTreeBuildRequest(solution, seedSymbol, requestedDepth, topN, CallTreeDirection.Incoming), ct);

    private static async Task<List<GraphExpansion>> BuildGraphGroupsAsync(
        GraphBuildState state,
        ISymbol symbol,
        CancellationToken ct)
    {
        var groups = new List<GraphExpansion>();
        if (state.Direction is CallTreeDirection.Incoming or CallTreeDirection.Both)
        {
            var references = await SymbolFinder.FindReferencesAsync(symbol, state.Solution, ct)
                .ConfigureAwait(false);
            var callers = await GroupByCallerAsync(references, ct).ConfigureAwait(false);
            groups.AddRange(callers
                .Where(group => group.CallerSymbol is not null)
                .Select(group => new GraphExpansion(
                    group.CallerSymbol!,
                    group.Locations,
                    CallTreeDirection.Incoming,
                    ClassifyDispatchKind(symbol))));
        }

        if (state.Direction is CallTreeDirection.Outgoing or CallTreeDirection.Both)
        {
            var outgoing = await OutgoingCallScanner.ScanAsync(
                symbol, state.Solution, ct, state.IncludeBcl).ConfigureAwait(false);
            groups.AddRange(outgoing.Select(group => new GraphExpansion(
                group.Symbol,
                group.Locations,
                CallTreeDirection.Outgoing,
                ClassifyDispatchKind(group.Symbol))));
        }

        return groups
            .OrderBy(group => group.Direction)
            .ThenBy(group => CallGraphTraversal.GetStableSymbolId(group.Symbol), StringComparer.Ordinal)
            .ThenBy(group => CallGraphTraversal.FormatSymbolName(group.Symbol, CallTreeDirection.Outgoing), StringComparer.Ordinal)
            .ThenBy(group => FirstLocationPath(group.Locations, state.Solution), StringComparer.Ordinal)
            .ThenBy(group => FirstLocationLine(group.Locations))
            .ToList();
    }

    private static string? ClassifyDispatchKind(ISymbol symbol) => symbol switch
    {
        IMethodSymbol { ContainingType.TypeKind: TypeKind.Interface } => "interface",
        IMethodSymbol { IsOverride: true } => "override",
        IMethodSymbol { IsVirtual: true } => "virtual",
        IMethodSymbol { IsAbstract: true } => "virtual",
        _ => null,
    };

    private static string FirstLocationPath(IReadOnlyList<Location> locations, Solution solution) =>
        locations.Count == 0 ? string.Empty : FormatPath(locations[0], solution);

    private static int FirstLocationLine(IReadOnlyList<Location> locations) =>
        locations.Count == 0 ? 0 : locations[0].GetLineSpan().StartLinePosition.Line + 1;

    internal static async Task<(MetricsTreeNode Root, bool Truncated)> BuildTreeAsync(
        CallTreeBuildRequest request,
        CancellationToken ct)
    {
        var depth = Math.Clamp(request.RequestedDepth, 1, MaxCallTreeDepth);
        var state = new TreeBuildState(request, depth);
        state.SetPathDisplayMode(request.AbsolutePaths);
        await RunTreeBfsAsync(state, ct);
        return (ToMetricsTreeNode(state.Root, state.HandoffIdentity), state.Truncated);
    }

    private static async Task RunTreeBfsAsync(TreeBuildState state, CancellationToken ct)
    {
        while (state.HasQueuedNodes)
        {
            ct.ThrowIfCancellationRequested();
            var (node, level) = state.Dequeue();
            await ExpandNodeAsync(state, node, level, ct);
        }
    }

    private static async Task ExpandNodeAsync(
        TreeBuildState state, CallTreeBuilderNode node, int level, CancellationToken ct)
    {
        // Nur Knoten mit bekanntem Symbol koennen weiter aufgeloest werden — "<unbekannt>"-Blaetter
        // (siehe GroupByCallerAsync) werden nie enqueued, dieser Guard ist defensiv.
        if (node.Symbol is null) return;

        var groups = await BuildGroupsAsync(state, node.Symbol, ct);

        var recursed = 0;
        foreach (var (group, direction) in groups)
        {
            var child = AddChild(
                node,
                group,
                state.Solution,
                direction,
                state.Direction == CallTreeDirection.Both,
                state.AbsolutePaths,
                state.HandoffIdentity);
            if (recursed >= state.TopN || !CanExpand(state, level, group.CallerSymbol, direction)) continue;
            recursed++;
            EnqueueOrTruncate(state, child, level + 1);
        }
    }

    private static async Task<List<(CallerGroup Group, CallTreeDirection Direction)>> BuildGroupsAsync(
        TreeBuildState state, ISymbol symbol, CancellationToken ct)
    {
        var incoming = new List<CallerGroup>();
        var outgoing = new List<CallerGroup>();
        if (state.Direction is CallTreeDirection.Incoming or CallTreeDirection.Both)
        {
            var refs = await SymbolFinder.FindReferencesAsync(symbol, state.Solution, ct);
            incoming = await BuildSortedGroupsAsync(refs, state.Solution, ct);
        }

        if (state.Direction is CallTreeDirection.Outgoing or CallTreeDirection.Both)
        {
            outgoing = await BuildSortedOutgoingGroupsAsync(symbol, state.Solution, state.IncludeBcl, ct);
        }

        return state.Direction == CallTreeDirection.Both
            ? InterleaveDirections(incoming, outgoing)
            : CreateDirectionalGroups(incoming, outgoing, state.Direction);
    }

    private static List<(CallerGroup Group, CallTreeDirection Direction)> InterleaveDirections(
        IReadOnlyList<CallerGroup> incoming, IReadOnlyList<CallerGroup> outgoing)
    {
        var groups = new List<(CallerGroup Group, CallTreeDirection Direction)>(
            incoming.Count + outgoing.Count);
        var maxCount = Math.Max(incoming.Count, outgoing.Count);
        for (var index = 0; index < maxCount; index++)
        {
            if (index < incoming.Count)
            {
                groups.Add((incoming[index], CallTreeDirection.Incoming));
            }

            if (index < outgoing.Count)
            {
                groups.Add((outgoing[index], CallTreeDirection.Outgoing));
            }
        }

        return groups;
    }

    private static List<(CallerGroup Group, CallTreeDirection Direction)> CreateDirectionalGroups(
        IReadOnlyList<CallerGroup> incoming,
        IReadOnlyList<CallerGroup> outgoing,
        CallTreeDirection direction)
    {
        var selected = direction == CallTreeDirection.Incoming ? incoming : outgoing;
        return selected
            .Select(group => (group, direction))
            .ToList();
    }

    private static async Task<List<CallerGroup>> BuildSortedGroupsAsync(
        IEnumerable<ReferencedSymbol> refs, Solution solution, CancellationToken ct)
    {
        var groups = await GroupByCallerAsync(refs, ct);
        return SortGroups(groups, solution);
    }

    private static List<CallerGroup> SortGroups(
        IEnumerable<CallerGroup> groups, Solution solution) =>
        groups
            .OrderBy(g => FirstLocationPath(g, solution), StringComparer.Ordinal)
            .ThenBy(FirstLocationLine)
            .ToList();

    private static async Task<List<CallerGroup>> BuildSortedOutgoingGroupsAsync(
        ISymbol symbol, Solution solution, bool includeBcl, CancellationToken ct)
    {
        var groups = await OutgoingCallScanner.ScanAsync(symbol, solution, ct, includeBcl);
        return SortGroups(
            groups.Select(group => new CallerGroup(group.Symbol, group.Locations.ToList())), solution);
    }

    private static async Task<List<CallerGroup>> GroupByCallerAsync(
        IEnumerable<ReferencedSymbol> refs, CancellationToken ct)
    {
        var byCaller = new Dictionary<ISymbol, CallerGroup>(SymbolEqualityComparer.Default);
        var ungrouped = new List<CallerGroup>();

        foreach (var reference in refs)
        {
            foreach (var referenceLocation in reference.Locations)
            {
                await AddLocationToGroupAsync(referenceLocation, byCaller, ungrouped, ct);
            }
        }

        return byCaller.Values.Concat(ungrouped).ToList();
    }

    private static async Task AddLocationToGroupAsync(
        ReferenceLocation referenceLocation,
        Dictionary<ISymbol, CallerGroup> byCaller,
        List<CallerGroup> ungrouped,
        CancellationToken ct)
    {
        var location = referenceLocation.Location;
        if (!location.IsInSource || location.SourceTree is null) return;

        var callerSymbol = await CallGraphTraversal.ResolveEnclosingMemberAsync(referenceLocation, ct);

        if (callerSymbol is null)
        {
            // Aufrufstelle ohne aufloesbaren einschliessenden Symbol (z. B. Top-Level-Statements) —
            // als eigenstaendiges, nicht weiter aufloesbares Blatt aufnehmen statt zu verwerfen.
            ungrouped.Add(new CallerGroup(null, new List<Location> { location }));
            return;
        }

        if (!byCaller.TryGetValue(callerSymbol, out var group))
        {
            group = new CallerGroup(callerSymbol, new List<Location>());
            byCaller[callerSymbol] = group;
        }
        group.Locations.Add(location);
    }

    // ainetlinter-disable MaxMethodParameterCount — der rekursive Builder übergibt die Traversierungsinvarianten explizit.
    private static CallTreeBuilderNode AddChild(
        CallTreeBuilderNode parent,
        CallerGroup group,
        Solution solution,
        CallTreeDirection direction,
        bool includeDirection,
        bool absolutePaths,
        AnalysisSymbolIdentity? handoffIdentity)
    {
        var displayLine = FormatGroupDisplay(group, solution, absolutePaths);
        var name = group.CallerSymbol is null
            ? "<unbekannt>"
            : CallGraphTraversal.FormatSymbolName(group.CallerSymbol, direction);
        if (includeDirection)
        {
            name = $"[{CallTreeDirectionNames.For(direction)}] {name}";
        }
        var child = new CallTreeBuilderNode(group.CallerSymbol, name, displayLine);
        parent.Children.Add(child);
        return child;
    }

    private static bool CanExpand(
        TreeBuildState state, int level, ISymbol? callerSymbol, CallTreeDirection direction) =>
        level < state.Depth
        && callerSymbol is not null
        && callerSymbol.Locations.Any(l => l.IsInSource)
        && state.MarkVisited(callerSymbol, direction);

    private static void EnqueueOrTruncate(TreeBuildState state, CallTreeBuilderNode child, int nextLevel)
    {
        if (state.NodeCount >= MaxCallTreeNodes)
        {
            state.Truncated = true;
            return;
        }
        state.NodeCount++;
        state.Enqueue(child, nextLevel);
    }

    private static string FormatGroupDisplay(CallerGroup group, Solution solution, bool absolutePaths)
    {
        var path = FormatPath(group.Locations[0], solution, absolutePaths);
        var line = FirstLocationLine(group);
        return group.Locations.Count > 1
            ? $"{path}:{line} (+{group.Locations.Count - 1} weitere Aufrufe)"
            : $"{path}:{line}";
    }

    internal static string FormatRootDisplay(ISymbol seedSymbol, Solution solution, bool absolutePaths = false)
    {
        var declaringLocation = seedSymbol.Locations.FirstOrDefault(l => l.IsInSource);
        if (declaringLocation is null) return seedSymbol.ToDisplayString();
        var path = FormatPath(declaringLocation, solution, absolutePaths);
        var line = declaringLocation.GetLineSpan().StartLinePosition.Line + 1;
        return $"{path}:{line}";
    }

    private static string FirstLocationPath(CallerGroup group, Solution solution) =>
        FormatPath(group.Locations[0], solution);

    private static int FirstLocationLine(CallerGroup group) =>
        group.Locations[0].GetLineSpan().StartLinePosition.Line + 1;

    private static string FormatPath(Location location, Solution solution, bool absolutePaths = false)
    {
        var outputRoot = Path.GetDirectoryName(solution.FilePath) ?? "";
        return absolutePaths
            ? Path.GetFullPath(location.SourceTree!.FilePath)
            : PathNormalizer.ToRelative(outputRoot, location.SourceTree!.FilePath);
    }

    private static MetricsTreeNode ToMetricsTreeNode(CallTreeBuilderNode node, AnalysisSymbolIdentity? identity = null)
    {
        var id = node.Symbol is null || identity is null
            ? null
            : identity.FormatHandoff(node.Symbol);
        var handoff = id is not null;
        return new(
            node.Name,
            "",
            0,
            0,
            node.DisplayLine,
            node.Children.Select(child => ToMetricsTreeNode(child, identity)).ToList(),
            Handoff: handoff,
            Id: handoff ? id : null,
            TargetPath: handoff ? identity!.CanonicalPath : null,
            Snapshot: handoff ? identity!.ContentHash : null,
            SymbolKind: node.Symbol?.Kind.ToString().ToLowerInvariant(),
            AllowedFollowUpTools: handoff ? HandoffFollowUpTools.For(node.Symbol!) : []);
    }

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
            IncludeBcl = request.IncludeBcl;
            RootNodeId = GetOrAddNode(request.SeedSymbol).NodeId;
            _queue.Enqueue((request.SeedSymbol, 1));
            _queued.Add(request.SeedSymbol);
        }

        internal Solution Solution { get; }
        internal int Depth { get; }
        internal CallTreeDirection Direction { get; }
        internal bool IncludeBcl { get; }
        internal string RootNodeId { get; }
        internal bool HasQueuedNodes => _queue.Count > 0;

        internal (ISymbol Symbol, int Level) Dequeue() => _queue.Dequeue();

        internal CallGraphNode GetOrAddNode(ISymbol symbol)
        {
            if (_nodesBySymbol.TryGetValue(symbol, out var existing)) return existing;

            var node = new CallGraphNode(
                $"n{_nodes.Count + 1}",
                symbol,
                CallGraphTraversal.GetStableSymbolId(symbol));
            _nodesBySymbol[symbol] = node;
            _nodes.Add(node);
            return node;
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
            return new CallGraphPayload(RootNodeId, _nodes, edges);
        }
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
