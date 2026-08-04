#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;

namespace AiNetLinter.Mcp.Tools;

/// <summary>
/// Iterativer BFS ueber <see cref="SymbolFinder.FindReferencesAsync"/> mit konfigurierbarer
/// Tiefe und separatem Knotenlimit. Wird von <see cref="FindReferencesTool"/> und
/// <see cref="GetImpactTool"/> (Symbol-Branch) fuer den neuen <c>depth</c>-Parameter genutzt,
/// damit transitive Aufrufstellen ermittelt werden koennen, ohne eine zweite Tool-API
/// einzufuehren (Konzept-Vorgabe: bewusst kein <c>get_call_tree</c>). Aggregation zu einer
/// kompakten Top-N-Antwort, damit grosse transitive Graphen nicht das Token-Budget sprengen.
/// </summary>
internal static class CallGraphTraversal
{
    internal const int MaxRecursionDepth = 3;
    internal const int MaxRecursionNodes = 200;

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
}
