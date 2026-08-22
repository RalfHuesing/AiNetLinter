#nullable enable

using System;
using System.Collections.Generic;
using AiNetLinter.Mcp.Tools.CallTree;
using AiNetLinter.Mcp.Tools.MetricsTree;
using Microsoft.CodeAnalysis;

namespace AiNetLinter.Mcp.Tools.SymbolGraph;

/// <summary>
/// Ein Aufrufer-Knoten während des Baum-Aufbaus (mutable, wird nach Abschluss in einen
/// unveränderlichen <see cref="MetricsTreeNode"/> überführt). <see cref="Symbol"/> ist
/// <see langword="null"/> nur für "&lt;unbekannt&gt;"-Blätter (Aufrufstelle ohne auflösbaren
/// einschließenden Symbol) — solche Knoten werden nie weiter expandiert.
/// </summary>
internal sealed class CallTreeBuilderNode
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
/// Bündelt alle Aufrufstellen, deren einschließender Symbol identisch ist — ein Kindknoten pro
/// Caller-Symbol statt pro einzelner Aufrufzeile.
/// </summary>
internal sealed class CallerGroup
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
/// Veränderlicher BFS-Zustand für <see cref="CallGraphTreeBuilder.BuildTreeAsync"/> mit richtungsbewusstem Visited-Set.
/// </summary>
internal sealed class TreeBuildState
{
    private readonly Queue<(CallTreeBuilderNode Node, int Level)> _queue = new();
    private readonly HashSet<(ISymbol Symbol, CallTreeDirection Direction)> _visited;

    internal TreeBuildState(
        Solution solution, ISymbol seedSymbol, int depth, int topN, CallTreeDirection direction)
    {
        Solution = solution;
        Depth = depth;
        TopN = topN;
        Direction = direction;
        Root = new CallTreeBuilderNode(seedSymbol, CallGraphTraversal.FormatSymbolName(seedSymbol), CallGraphTreeBuilder.FormatRootDisplay(seedSymbol, solution));
        _visited = new HashSet<(ISymbol Symbol, CallTreeDirection Direction)>(
            new DirectionAwareSymbolComparer())
        {
            (seedSymbol, direction),
        };
        _queue.Enqueue((Root, 1));
        NodeCount = 1;
    }

    internal Solution Solution { get; }
    internal int Depth { get; }
    internal int TopN { get; }
    internal CallTreeDirection Direction { get; }
    internal CallTreeBuilderNode Root { get; }
    internal int NodeCount { get; set; }
    internal bool Truncated { get; set; }
    internal bool HasQueuedNodes => _queue.Count > 0;

    internal (CallTreeBuilderNode Node, int Level) Dequeue() => _queue.Dequeue();
    internal void Enqueue(CallTreeBuilderNode node, int level) => _queue.Enqueue((node, level));
    internal bool MarkVisited(ISymbol symbol, CallTreeDirection direction) =>
        _visited.Add((symbol, direction));

    private sealed class DirectionAwareSymbolComparer : IEqualityComparer<(ISymbol Symbol, CallTreeDirection Direction)>
    {
        public bool Equals(
            (ISymbol Symbol, CallTreeDirection Direction) x,
            (ISymbol Symbol, CallTreeDirection Direction) y) =>
            x.Direction == y.Direction && SymbolEqualityComparer.Default.Equals(x.Symbol, y.Symbol);

        public int GetHashCode((ISymbol Symbol, CallTreeDirection Direction) obj) =>
            HashCode.Combine(SymbolEqualityComparer.Default.GetHashCode(obj.Symbol), obj.Direction);
    }
}
