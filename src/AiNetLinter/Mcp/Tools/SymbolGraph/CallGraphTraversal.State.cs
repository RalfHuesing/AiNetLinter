#nullable enable

using System;
using System.Collections.Generic;
using AiNetLinter.Mcp.Tools.CallTree;
using AiNetLinter.Mcp.Tools.MetricsTree;
using Microsoft.CodeAnalysis;

namespace AiNetLinter.Mcp.Tools.SymbolGraph;

internal static partial class CallGraphTraversal
{
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
    /// Buendelt alle Aufrufstellen, deren einschliessender Symbol identisch ist — ein Kindknoten pro
    /// Caller-Symbol statt pro einzelner Aufrufzeile.
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
    /// Veraenderlicher BFS-Zustand fuer <see cref="BuildTreeAsync"/> mit richtungsbewusstem Visited-Set.
    /// </summary>
    private sealed class TreeBuildState
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
            Root = new CallTreeBuilderNode(seedSymbol, FormatSymbolName(seedSymbol), FormatRootDisplay(seedSymbol, solution));
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
}