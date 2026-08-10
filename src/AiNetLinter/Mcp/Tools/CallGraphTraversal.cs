#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Tools.CallTree;
using AiNetLinter.Mcp.Tools.MetricsTree;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;

namespace AiNetLinter.Mcp.Tools;

/// <summary>
/// Iterativer BFS ueber <see cref="SymbolFinder.FindReferencesAsync"/> mit konfigurierbarer
/// Tiefe und separatem Knotenlimit. Wird von <see cref="FindReferencesTool"/> und
/// <see cref="GetImpactTool"/> (Symbol-Branch) fuer den bestehenden <c>depth</c>-Parameter
/// genutzt, damit transitive Aufrufstellen als kompakte Top-N-Antwort ermittelt werden koennen
/// (<see cref="ExpandAndFormatAsync"/>). Fuer strukturelle Analyse liefert diese flache Liste
/// jedoch keinen Baum — <see cref="BuildTreeAsync"/> (genutzt von <c>get_call_tree</c>, siehe
/// <see cref="GetCallTreeTool"/>) ergaenzt daher eine echte Eltern-Kind-Baumtraversierung mit
/// eigenen, hoeheren Grenzwerten (<see cref="MaxCallTreeDepth"/>/<see cref="MaxCallTreeNodes"/>),
/// ohne die bestehende flache Aggregation fuer <c>find_references</c>/<c>get_impact</c> zu
/// veraendern.
/// </summary>
internal static class CallGraphTraversal
{
    internal const int MaxRecursionDepth = 3;
    internal const int MaxRecursionNodes = 200;
    internal const int MaxCallTreeDepth = 5;
    internal const int MaxCallTreeNodes = 250;

    /// <summary>
    /// Erweitert <paramref name="seedSymbol"/> iterativ bis <paramref name="requestedDepth"/>
    /// Stufen und aggregiert die Fundstellen zu einer kompakten Top-N-Antwort.
    /// <paramref name="maxResults"/> beschraenkt die Anzahl der angezeigten Locations,
    /// unabhaengig vom <see cref="MaxRecursionNodes"/>-Hard-Cap, der exponentielle
    /// Explosion bei grossen Symbolgraphen verhindert.
    /// </summary>
    internal static async Task<string> ExpandAndFormatAsync(
        Solution solution,
        ISymbol seedSymbol,
        int requestedDepth,
        int maxResults,
        CancellationToken ct)
    {
        var depth = Math.Clamp(requestedDepth, 1, MaxRecursionDepth);
        var state = new TraversalState(seedSymbol, depth);
        await TraverseAsync(solution, state, ct);
        return AggregateAndTruncate(state.Locations, maxResults, depth);
    }

    private static async Task TraverseAsync(Solution solution, TraversalState state, CancellationToken ct)
    {
        while (state.HasMore && !state.IsAtNodeCap)
        {
            ct.ThrowIfCancellationRequested();
            var (current, level) = state.Dequeue();
            var refs = await SymbolFinder.FindReferencesAsync(current, solution, ct);
            AppendReferenceLocations(refs, solution, state);
            EnqueueChildren(refs, level, state);
        }
    }

    private static void AppendReferenceLocations(
        IEnumerable<ReferencedSymbol> refs, Solution solution, TraversalState state)
    {
        foreach (var reference in refs)
        {
            foreach (var referenceLocation in reference.Locations)
            {
                if (state.IsAtNodeCap) return;
                state.Add(FormatReferenceLocation(referenceLocation, solution));
            }
        }
    }

    private static void EnqueueChildren(
        IEnumerable<ReferencedSymbol> refs, int currentLevel, TraversalState state)
    {
        if (currentLevel >= state.Depth) return;
        foreach (var reference in refs)
        {
            if (state.MarkSeenAndEnqueue(reference.Definition, currentLevel + 1)) break;
        }
    }

    private sealed class TraversalState
    {
        private readonly Queue<(ISymbol Symbol, int Level)> _queue = new();
        private readonly HashSet<ISymbol> _seen;

        public TraversalState(ISymbol seed, int depth)
        {
            Depth = depth;
            _seen = new HashSet<ISymbol>(SymbolEqualityComparer.Default) { seed };
            _queue.Enqueue((seed, 1));
        }

        public int Depth { get; }
        public List<string> Locations { get; } = new();
        public bool HasMore => _queue.Count > 0;
        public bool IsAtNodeCap => Locations.Count >= MaxRecursionNodes;

        public (ISymbol Symbol, int Level) Dequeue() => _queue.Dequeue();
        public void Add(string location) => Locations.Add(location);
        public bool MarkSeenAndEnqueue(ISymbol definition, int level)
        {
            if (!_seen.Add(definition)) return false;
            _queue.Enqueue((definition, level));
            return false;
        }
    }

    private static string AggregateAndTruncate(IReadOnlyList<string> allLocations, int maxResults, int depth)
    {
        if (allLocations.Count <= maxResults)
        {
            return string.Join("\n", allLocations);
        }

        var shown = allLocations.Take(maxResults).ToList();
        var meta = $"[{allLocations.Count} Treffer gesamt (depth={depth}, hard-cap {MaxRecursionNodes}), " +
                   $"{maxResults} gezeigt — depth reduzieren oder maxResults erhoehen]";
        return string.Join("\n", shown) + "\n" + meta;
    }

    private static string FormatReferenceLocation(ReferenceLocation referenceLocation, Solution solution)
    {
        var location = referenceLocation.Location;
        if (!location.IsInSource || location.SourceTree is null) return string.Empty;
        var outputRoot = Path.GetDirectoryName(solution.FilePath) ?? "";
        var path = Path.GetRelativePath(outputRoot, location.SourceTree.FilePath).Replace('\\', '/');
        var lineSpan = location.GetLineSpan();
        var line = lineSpan.StartLinePosition.Line + 1;
        return $"{path}:{line} - transitiver Aufrufer";
    }

    /// <summary>
    /// Baut einen echten Eltern-Kind-Baum der transitiven Aufrufer von <paramref name="seedSymbol"/>
    /// (Caller-Tree, dieselbe Richtung wie <see cref="ExpandAndFormatAsync"/>). Im Unterschied zur
    /// flachen Aggregation wird pro Aufrufstelle der einschliessende Symbol via
    /// <see cref="SemanticModel.GetEnclosingSymbol(int, CancellationToken)"/> ermittelt und als
    /// eigener Kindknoten weiterverfolgt — erst das ergibt eine echte Baumstruktur (die flache
    /// Aggregation sammelt stattdessen nur Definitions-Varianten wie Overrides ein). Liefert die
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
    {
        var depth = Math.Clamp(requestedDepth, 1, MaxCallTreeDepth);
        var state = new TreeBuildState(solution, seedSymbol, depth, topN);
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

        var refs = await SymbolFinder.FindReferencesAsync(node.Symbol, state.Solution, ct);
        var sortedGroups = await BuildSortedGroupsAsync(refs, state.Solution, ct);

        var recursed = 0;
        foreach (var group in sortedGroups)
        {
            var child = AddChild(node, group, state.Solution);
            if (recursed >= state.TopN || !CanExpand(state, level, group.CallerSymbol)) continue;
            recursed++;
            EnqueueOrTruncate(state, child, level + 1);
        }
    }

    private static async Task<List<CallerGroup>> BuildSortedGroupsAsync(
        IEnumerable<ReferencedSymbol> refs, Solution solution, CancellationToken ct)
    {
        var groups = await GroupByCallerAsync(refs, ct);
        return groups
            .OrderBy(g => FirstLocationPath(g, solution), StringComparer.Ordinal)
            .ThenBy(FirstLocationLine)
            .ToList();
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

        var semanticModel = await referenceLocation.Document.GetSemanticModelAsync(ct);
        var callerSymbol = semanticModel is null
            ? null
            : NormalizeCallerSymbol(semanticModel.GetEnclosingSymbol(location.SourceSpan.Start, ct));

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

    private static ISymbol? NormalizeCallerSymbol(ISymbol? symbol) =>
        symbol is IMethodSymbol { AssociatedSymbol: { } owner } ? owner : symbol;

    private static CallTreeBuilderNode AddChild(CallTreeBuilderNode parent, CallerGroup group, Solution solution)
    {
        var displayLine = FormatGroupDisplay(group, solution);
        var name = group.CallerSymbol is null ? "<unbekannt>" : FormatSymbolName(group.CallerSymbol);
        var child = new CallTreeBuilderNode(group.CallerSymbol, name, displayLine);
        parent.Children.Add(child);
        return child;
    }

    private static bool CanExpand(TreeBuildState state, int level, ISymbol? callerSymbol) =>
        level < state.Depth && callerSymbol is not null && state.MarkVisited(callerSymbol);

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

    private static string FormatSymbolName(ISymbol symbol) =>
        symbol.ContainingType is { } containingType ? $"{containingType.Name}.{symbol.Name}" : symbol.Name;

    private static string FormatGroupDisplay(CallerGroup group, Solution solution)
    {
        var path = FormatPath(group.Locations[0], solution);
        var line = FirstLocationLine(group);
        return group.Locations.Count > 1
            ? $"{path}:{line} (+{group.Locations.Count - 1} weitere Aufrufe)"
            : $"{path}:{line}";
    }

    private static string FormatRootDisplay(ISymbol seedSymbol, Solution solution)
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
        return Path.GetRelativePath(outputRoot, location.SourceTree!.FilePath).Replace('\\', '/');
    }

    private static MetricsTreeNode ToMetricsTreeNode(CallTreeBuilderNode node) =>
        new(node.Name, "", 0, 0, node.DisplayLine, node.Children.Select(ToMetricsTreeNode).ToList());

    /// <summary>
    /// Ein Aufrufer-Knoten waehrend des Baum-Aufbaus (mutable, wird nach Abschluss in einen
    /// unveraenderlichen <see cref="MetricsTreeNode"/> ueberfuehrt). <see cref="Symbol"/> ist
    /// <see langword="null"/> nur fuer "&lt;unbekannt&gt;"-Blaetter (Aufrufstelle ohne aufloesbaren
    /// einschliessenden Symbol) — solche Knoten werden nie weiter expandiert.
    /// </summary>
    private sealed class CallTreeBuilderNode
    {
        internal CallTreeBuilderNode(ISymbol? symbol, string name, string displayLine)
        {
            Symbol = symbol;
            Name = name;
            DisplayLine = displayLine;
        }

        internal ISymbol? Symbol { get; }
        internal string Name { get; }
        internal string DisplayLine { get; }
        internal List<CallTreeBuilderNode> Children { get; } = new();
    }

    /// <summary>
    /// Buendelt alle Aufrufstellen, deren einschliessender Symbol identisch ist (z. B. zwei Aufrufe
    /// derselben Methode im selben Caller) — ein Kindknoten pro Caller-Symbol statt pro Zeile.
    /// </summary>
    private sealed class CallerGroup
    {
        internal CallerGroup(ISymbol? callerSymbol, List<Location> locations)
        {
            CallerSymbol = callerSymbol;
            Locations = locations;
        }

        internal ISymbol? CallerSymbol { get; }
        internal List<Location> Locations { get; }
    }

    /// <summary>
    /// Veraenderlicher BFS-Zustand fuer <see cref="BuildTreeAsync"/> — analog zu
    /// <see cref="TraversalState"/>, aber mit Queue-Eintraegen, die auf echte Baumknoten statt auf
    /// eine flache Locations-Liste zeigen.
    /// </summary>
    private sealed class TreeBuildState
    {
        private readonly Queue<(CallTreeBuilderNode Node, int Level)> _queue = new();
        private readonly HashSet<ISymbol> _visited;

        internal TreeBuildState(Solution solution, ISymbol seedSymbol, int depth, int topN)
        {
            Solution = solution;
            Depth = depth;
            TopN = topN;
            Root = new CallTreeBuilderNode(seedSymbol, FormatSymbolName(seedSymbol), FormatRootDisplay(seedSymbol, solution));
            _visited = new HashSet<ISymbol>(SymbolEqualityComparer.Default) { seedSymbol };
            _queue.Enqueue((Root, 1));
            NodeCount = 1;
        }

        internal Solution Solution { get; }
        internal int Depth { get; }
        internal int TopN { get; }
        internal CallTreeBuilderNode Root { get; }
        internal int NodeCount { get; set; }
        internal bool Truncated { get; set; }
        internal bool HasQueuedNodes => _queue.Count > 0;

        internal (CallTreeBuilderNode Node, int Level) Dequeue() => _queue.Dequeue();
        internal void Enqueue(CallTreeBuilderNode node, int level) => _queue.Enqueue((node, level));
        internal bool MarkVisited(ISymbol symbol) => _visited.Add(symbol);
    }
}
