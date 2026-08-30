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

    internal static async Task<(MetricsTreeNode Root, bool Truncated)> BuildTreeAsync(
        CallTreeBuildRequest request,
        CancellationToken ct)
    {
        var depth = Math.Clamp(request.RequestedDepth, 1, MaxCallTreeDepth);
        var state = new TreeBuildState(
            request.Solution, request.SeedSymbol, depth, request.TopN, request.Direction);
        await RunTreeBfsAsync(state, ct);
        return (ToMetricsTreeNode(state.Root), state.Truncated);
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
                node, group, state.Solution, direction, state.Direction == CallTreeDirection.Both);
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
            outgoing = await BuildSortedOutgoingGroupsAsync(symbol, state.Solution, ct);
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
        ISymbol symbol, Solution solution, CancellationToken ct)
    {
        var groups = await OutgoingCallScanner.ScanAsync(symbol, solution, ct);
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

    private static CallTreeBuilderNode AddChild(
        CallTreeBuilderNode parent,
        CallerGroup group,
        Solution solution,
        CallTreeDirection direction,
        bool includeDirection)
    {
        var displayLine = FormatGroupDisplay(group, solution);
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
        level < state.Depth && callerSymbol is not null && state.MarkVisited(callerSymbol, direction);

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

    private static string FormatGroupDisplay(CallerGroup group, Solution solution)
    {
        var path = FormatPath(group.Locations[0], solution);
        var line = FirstLocationLine(group);
        return group.Locations.Count > 1
            ? $"{path}:{line} (+{group.Locations.Count - 1} weitere Aufrufe)"
            : $"{path}:{line}";
    }

    internal static string FormatRootDisplay(ISymbol seedSymbol, Solution solution)
    {
        var declaringLocation = seedSymbol.Locations.FirstOrDefault(l => l.IsInSource);
        if (declaringLocation is null) return seedSymbol.ToDisplayString();
        var path = FormatPath(declaringLocation, solution);
        var line = declaringLocation.GetLineSpan().StartLinePosition.Line + 1;
        return $"{path}:{line}";
    }

    private static string FirstLocationPath(CallerGroup group, Solution solution) =>
        FormatPath(group.Locations[0], solution);

    private static int FirstLocationLine(CallerGroup group) =>
        group.Locations[0].GetLineSpan().StartLinePosition.Line + 1;

    private static string FormatPath(Location location, Solution solution)
    {
        var outputRoot = Path.GetDirectoryName(solution.FilePath) ?? "";
        return PathNormalizer.ToRelative(outputRoot, location.SourceTree!.FilePath);
    }

    private static MetricsTreeNode ToMetricsTreeNode(CallTreeBuilderNode node) =>
        new(node.Name, "", 0, 0, node.DisplayLine, node.Children.Select(ToMetricsTreeNode).ToList());
}
