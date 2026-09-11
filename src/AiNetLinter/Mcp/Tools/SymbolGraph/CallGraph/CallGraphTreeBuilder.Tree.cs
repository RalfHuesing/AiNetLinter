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

internal static partial class CallGraphTreeBuilder
{
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

    private static async Task ExpandNodeAsync(TreeBuildState state, CallTreeBuilderNode node, int level, CancellationToken ct)
    {
        if (node.Symbol is null) return;
        var groups = await BuildGroupsAsync(state, node.Symbol, ct);
        var recursed = 0;
        foreach (var (group, direction) in groups)
        {
            var child = AddChild(new AddChildRequest(node, group, state.Solution, direction,
                state.Direction == CallTreeDirection.Both, state.AbsolutePaths));
            if (recursed >= state.TopN || !CanExpand(state, level, group.CallerSymbol, direction)) continue;
            recursed++;
            EnqueueOrTruncate(state, child, level + 1);
        }
    }

    private static async Task<List<(CallerGroup Group, CallTreeDirection Direction)>> BuildGroupsAsync(TreeBuildState state, ISymbol symbol, CancellationToken ct)
    {
        var incoming = new List<CallerGroup>();
        var outgoing = new List<CallerGroup>();
        if (state.Direction is CallTreeDirection.Incoming or CallTreeDirection.Both)
        {
            var refs = await SymbolFinder.FindReferencesAsync(symbol, state.Solution, ct);
            incoming = await BuildSortedGroupsAsync(refs, state.Solution, ct);
        }
        if (state.Direction is CallTreeDirection.Outgoing or CallTreeDirection.Both)
            outgoing = await BuildSortedOutgoingGroupsAsync(symbol, state.Solution, state.IncludeBcl, ct);
        var directionalGroups = state.Direction == CallTreeDirection.Both
            ? InterleaveDirections(incoming, outgoing) : CreateDirectionalGroups(incoming, outgoing, state.Direction);
        return await FilterGroupsByScopeAsync(state, directionalGroups, ct).ConfigureAwait(false);
    }

    private static async Task<List<(CallerGroup Group, CallTreeDirection Direction)>> FilterGroupsByScopeAsync(TreeBuildState state, IReadOnlyList<(CallerGroup Group, CallTreeDirection Direction)> groups, CancellationToken ct)
    {
        var filtered = new List<(CallerGroup Group, CallTreeDirection Direction)>(groups.Count);
        foreach (var (group, direction) in groups)
        {
            var locations = new List<Location>(group.Locations.Count);
            foreach (var location in group.Locations)
            {
                ct.ThrowIfCancellationRequested();
                if (location.SourceTree is null) continue;
                var document = state.Solution.GetDocument(location.SourceTree);
                if (document is null) continue;
                var scope = await state.ScopeClassifier.ClassifyAsync(document, ct).ConfigureAwait(false);
                if (state.ScopeClassifier.MatchesScope(scope, state.ScopeType)
                    && (state.IncludeGenerated || scope.SourceKind != McpSourceKind.Generated)) locations.Add(location);
            }
            if (locations.Count != group.Locations.Count) state.MarkScopeFiltered();
            if (locations.Count > 0) filtered.Add((new CallerGroup(group.CallerSymbol, locations), direction));
        }
        return filtered;
    }

    private static List<(CallerGroup Group, CallTreeDirection Direction)> InterleaveDirections(IReadOnlyList<CallerGroup> incoming, IReadOnlyList<CallerGroup> outgoing)
    {
        var groups = new List<(CallerGroup Group, CallTreeDirection Direction)>(incoming.Count + outgoing.Count);
        for (var index = 0; index < Math.Max(incoming.Count, outgoing.Count); index++)
        {
            if (index < incoming.Count) groups.Add((incoming[index], CallTreeDirection.Incoming));
            if (index < outgoing.Count) groups.Add((outgoing[index], CallTreeDirection.Outgoing));
        }
        return groups;
    }

    private static List<(CallerGroup Group, CallTreeDirection Direction)> CreateDirectionalGroups(IReadOnlyList<CallerGroup> incoming, IReadOnlyList<CallerGroup> outgoing, CallTreeDirection direction) =>
        (direction == CallTreeDirection.Incoming ? incoming : outgoing).Select(group => (group, direction)).ToList();

    private static async Task<List<CallerGroup>> BuildSortedGroupsAsync(IEnumerable<ReferencedSymbol> refs, Solution solution, CancellationToken ct) =>
        SortGroups(await GroupByCallerAsync(refs, ct), solution);

    private static List<CallerGroup> SortGroups(IEnumerable<CallerGroup> groups, Solution solution) =>
        groups.OrderBy(g => FirstLocationPath(g, solution), StringComparer.Ordinal).ThenBy(FirstLocationLine).ToList();

    private static async Task<List<CallerGroup>> BuildSortedOutgoingGroupsAsync(ISymbol symbol, Solution solution, bool includeBcl, CancellationToken ct)
    {
        var groups = await OutgoingCallScanner.ScanAsync(symbol, solution, ct, includeBcl);
        return SortGroups(groups.Select(group => new CallerGroup(group.Symbol, group.Locations.ToList())), solution);
    }

    private static async Task<List<CallerGroup>> GroupByCallerAsync(IEnumerable<ReferencedSymbol> refs, CancellationToken ct)
    {
        var byCaller = new Dictionary<ISymbol, CallerGroup>(SymbolEqualityComparer.Default);
        var ungrouped = new List<CallerGroup>();
        foreach (var reference in refs)
            foreach (var referenceLocation in reference.Locations)
                await AddLocationToGroupAsync(referenceLocation, byCaller, ungrouped, ct);
        return byCaller.Values.Concat(ungrouped).ToList();
    }

    private static async Task AddLocationToGroupAsync(ReferenceLocation referenceLocation, Dictionary<ISymbol, CallerGroup> byCaller, List<CallerGroup> ungrouped, CancellationToken ct)
    {
        var location = referenceLocation.Location;
        if (!location.IsInSource || location.SourceTree is null) return;
        var callerSymbol = await CallGraphTraversal.ResolveEnclosingMemberAsync(referenceLocation, ct);
        if (callerSymbol is null) { ungrouped.Add(new CallerGroup(null, new List<Location> { location })); return; }
        if (!byCaller.TryGetValue(callerSymbol, out var group))
        {
            group = new CallerGroup(callerSymbol, new List<Location>());
            byCaller[callerSymbol] = group;
        }
        group.Locations.Add(location);
    }

    private static CallTreeBuilderNode AddChild(AddChildRequest request)
    {
        var name = request.Group.CallerSymbol is null ? "<unbekannt>" : CallGraphTraversal.FormatSymbolName(request.Group.CallerSymbol, request.Direction);
        if (request.IncludeDirection) name = $"[{CallTreeDirectionNames.For(request.Direction)}] {name}";
        var child = new CallTreeBuilderNode(request.Group.CallerSymbol, name, FormatGroupDisplay(request.Group, request.Solution, request.AbsolutePaths));
        request.Parent.Children.Add(child);
        return child;
    }

    private static bool CanExpand(TreeBuildState state, int level, ISymbol? callerSymbol, CallTreeDirection direction) =>
        level < state.Depth && callerSymbol is not null && callerSymbol.Locations.Any(l => l.IsInSource) && state.MarkVisited(callerSymbol, direction);

    private static void EnqueueOrTruncate(TreeBuildState state, CallTreeBuilderNode child, int nextLevel)
    {
        if (state.NodeCount >= MaxCallTreeNodes) { state.Truncated = true; return; }
        state.NodeCount++;
        state.Enqueue(child, nextLevel);
    }

    private static string FormatGroupDisplay(CallerGroup group, Solution solution, bool absolutePaths)
    {
        var path = FormatPath(group.Locations[0], solution, absolutePaths);
        var line = FirstLocationLine(group);
        return group.Locations.Count > 1 ? $"{path}:{line} (+{group.Locations.Count - 1} weitere Aufrufe)" : $"{path}:{line}";
    }

    internal static string FormatRootDisplay(ISymbol seedSymbol, Solution solution, bool absolutePaths = false)
    {
        var declaringLocation = seedSymbol.Locations.FirstOrDefault(l => l.IsInSource);
        if (declaringLocation is null) return seedSymbol.ToDisplayString();
        return $"{FormatPath(declaringLocation, solution, absolutePaths)}:{declaringLocation.GetLineSpan().StartLinePosition.Line + 1}";
    }

    private static string FormatSymbolDisplayLine(ISymbol symbol, Solution solution, bool absolutePaths)
    {
        var location = symbol.Locations.FirstOrDefault(candidate => candidate.IsInSource);
        return location is null ? string.Empty : $"{FormatPath(location, solution, absolutePaths)}:{location.GetLineSpan().StartLinePosition.Line + 1}";
    }

    private static string FirstLocationPath(CallerGroup group, Solution solution) => FormatPath(group.Locations[0], solution);
    private static int FirstLocationLine(CallerGroup group) => group.Locations[0].GetLineSpan().StartLinePosition.Line + 1;
    private static string FormatPath(Location location, Solution solution, bool absolutePaths = false)
    {
        var outputRoot = Path.GetDirectoryName(solution.FilePath) ?? "";
        return absolutePaths ? Path.GetFullPath(location.SourceTree!.FilePath) : PathNormalizer.ToRelative(outputRoot, location.SourceTree!.FilePath);
    }

    private static MetricsTreeNode ToMetricsTreeNode(CallTreeBuilderNode node, AnalysisSymbolIdentity? identity = null)
    {
        var id = node.Symbol is null || identity is null ? null : identity.FormatHandoff(node.Symbol);
        var handoff = id is not null;
        return new(node.Name, "", 0, 0, node.DisplayLine, node.Children.Select(child => ToMetricsTreeNode(child, identity)).ToList(),
            Handoff: handoff, Id: handoff ? id : null, TargetPath: handoff ? identity!.CanonicalPath : null,
            Snapshot: handoff ? identity!.ContentHash : null, SymbolKind: node.Symbol?.Kind.ToString().ToLowerInvariant(),
            AllowedFollowUpTools: handoff ? HandoffFollowUpTools.For(node.Symbol!) : []);
    }
}

internal sealed record AddChildRequest(
    CallTreeBuilderNode Parent,
    CallerGroup Group,
    Solution Solution,
    CallTreeDirection Direction,
    bool IncludeDirection,
    bool AbsolutePaths);
