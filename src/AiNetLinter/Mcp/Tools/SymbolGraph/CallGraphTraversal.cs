#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Core;
using AiNetLinter.Output;
using AiNetLinter.Mcp.Tools.CallTree;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;

namespace AiNetLinter.Mcp.Tools.SymbolGraph;

/// <summary>
/// Iterativer BFS ueber <see cref="SymbolFinder.FindReferencesAsync"/> mit konfigurierbarer
/// Tiefe und separatem Knotenlimit. Wird von <see cref="FindReferencesTool"/> und
/// <see cref="GetImpactTool"/> (Symbol-Branch) fuer den bestehenden <c>depth</c>-Parameter
/// genutzt, damit transitive Aufrufstellen als kompakte Top-N-Antwort ermittelt werden koennen.
/// <see cref="ExpandAsync"/> sammelt die strukturierten Daten, waehrend
/// <see cref="TransitiveCallGraphFormatter"/> das kompatible Textformat erzeugt. Fuer strukturelle
/// Analyse (echter Eltern-Kind-Baum von <c>get_call_tree</c>) siehe <see cref="CallGraphTreeBuilder"/>,
/// der dieselben Referenzen mit eigener Struktur-, Richtungs- und Kappungslogik durchlaeuft.
/// </summary>
internal static class CallGraphTraversal
{
    internal const int MaxRecursionDepth = 3;
    internal const int MaxRecursionNodes = 200;

    internal static Task<ReferenceTraversalResult> ExpandAsync(
        Solution solution,
        ISymbol seedSymbol,
        int requestedDepth,
        int maxResults,
        CancellationToken ct) =>
        ExpandAsync(new ReferenceTraversalRequest(
            solution, seedSymbol, requestedDepth, maxResults, ct));

    internal static async Task<ReferenceTraversalResult> ExpandAsync(ReferenceTraversalRequest request)
    {
        var effectiveDepth = Math.Clamp(request.RequestedDepth, 1, MaxRecursionDepth);
        var effectiveNodeLimit = request.NodeLimit ?? MaxRecursionNodes;
        if (effectiveNodeLimit < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(request.NodeLimit));
        }

        var state = new TraversalState(request.SeedSymbol, effectiveDepth, effectiveNodeLimit);
        await TraverseAsync(request.Solution, state, request.CancellationToken);
        return state.CreateResult(
            request.RequestedDepth, effectiveDepth, Math.Max(request.MaxResults, 1));
    }

    private static async Task TraverseAsync(Solution solution, TraversalState state, CancellationToken ct)
    {
        while (state.HasMore)
        {
            ct.ThrowIfCancellationRequested();
            if (state.IsAtNodeCap)
            {
                state.TruncatedByNodeLimit = true;
                break;
            }

            var (current, level) = state.Dequeue();
            state.MarkVisited();
            var refs = await SymbolFinder.FindReferencesAsync(current, solution, ct);
            AppendReferenceLocations(refs, solution, current, level, state);
            await EnqueueChildrenAsync(refs, level, state, ct);
        }
    }

    private static void AppendReferenceLocations(
        IEnumerable<ReferencedSymbol> refs,
        Solution solution,
        ISymbol reachedFromSymbol,
        int depth,
        TraversalState state)
    {
        foreach (var reference in refs)
        {
            foreach (var referenceLocation in reference.Locations)
            {
                if (!referenceLocation.Location.IsInSource || referenceLocation.Location.SourceTree is null)
                {
                    continue;
                }

                state.Add(CreateCallSiteEntry(
                    reference, referenceLocation, solution, reachedFromSymbol, depth));
            }
        }
    }

    private static TransitiveCallSiteEntry CreateCallSiteEntry(
        ReferencedSymbol reference,
        ReferenceLocation referenceLocation,
        Solution solution,
        ISymbol reachedFromSymbol,
        int depth)
    {
        var location = referenceLocation.Location;
        var outputRoot = Path.GetDirectoryName(solution.FilePath) ?? string.Empty;
        var filePath = PathNormalizer.ToRelative(outputRoot, location.SourceTree!.FilePath);
        var line = location.GetLineSpan().StartLinePosition.Line + 1;
        return new TransitiveCallSiteEntry(
            filePath,
            line,
            FormatSymbolName(reference.Definition),
            referenceLocation.Document.Project.Name,
            depth,
            GetStableSymbolId(reachedFromSymbol));
    }

    /// <summary>
    /// Gemeinsame Quelle stabiler Symbol-IDs ueber Traversal- und Diff-Impact-Analyse hinaus:
    /// DocCommentId, sonst deterministischer FullyQualified-Fallback. Lokale Funktionen
    /// (<see cref="MethodKind.LocalFunction"/>) erhalten eine kollisionsfreie ID aus der stabilen
    /// ID des naechsten einschliessenden Members plus dem deterministischen Suffix
    /// <c>#lf:&lt;Name&gt;@&lt;Zeile&gt;:&lt;Spalte&gt;</c> (1-basierte Deklarationsstartposition) —
    /// ihre DocumentationCommentId wuerde sonst die des einschliessenden Members liefern, sodass
    /// alle lokalen Funktionen einer Methode auf derselben ID kollidieren wuerden.
    /// </summary>
    internal static string GetStableSymbolId(ISymbol symbol) =>
        symbol is IMethodSymbol { MethodKind: MethodKind.LocalFunction } localFunction
            ? FormatLocalFunctionId(localFunction)
            : DocumentationCommentId.CreateDeclarationId(symbol) ??
              symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

    // Verschachtelte lokale Funktionen steigen ueber ContainingSymbol bis zum naechsten
    // nicht-lokalen Member auf und teilen sich dessen Basis-ID — das Positionssuffix macht sie
    // dennoch einzeln unterscheidbar, gleicher Codezustand liefert dieselbe ID.
    private static string FormatLocalFunctionId(IMethodSymbol localFunction)
    {
        var container = localFunction.ContainingSymbol;
        while (container is IMethodSymbol { MethodKind: MethodKind.LocalFunction })
        {
            container = container.ContainingSymbol;
        }

        var start = localFunction.Locations.First(location => location.IsInSource)
            .GetLineSpan().StartLinePosition;
        return $"{GetStableSymbolId(container!)}#lf:{localFunction.Name}@{start.Line + 1}:{start.Character + 1}";
    }

    /// <summary>
    /// Enqueued fuer die naechste BFS-Ebene das tatsaechlich einschliessende Aufrufer-Member je
    /// Referenzlocation — nicht die referenzierte Definition: die ist bei
    /// <c>SymbolFinder.FindReferencesAsync(current)</c> meist <c>current</c> selbst, steht damit
    /// bereits in <c>_seen</c> und liesse depth &gt; 1 sonst nie ueber echte Aufruferketten
    /// expandieren. Locations ohne aufloesbares Enclosing-Symbol (z. B. Top-Level-Statements)
    /// bleiben Call-Sites der aktuellen Ebene und werden nicht expandiert.
    /// </summary>
    private static async Task EnqueueChildrenAsync(
        IEnumerable<ReferencedSymbol> refs, int currentLevel, TraversalState state, CancellationToken ct)
    {
        if (currentLevel >= state.Depth) return;
        foreach (var reference in refs)
        {
            foreach (var referenceLocation in reference.Locations)
            {
                if (!referenceLocation.Location.IsInSource || referenceLocation.Location.SourceTree is null)
                {
                    continue;
                }

                var caller = await ResolveEnclosingMemberAsync(referenceLocation, ct);
                if (caller is not null)
                {
                    state.MarkSeenAndEnqueue(caller, currentLevel + 1);
                }
            }
        }
    }

    /// <summary>
    /// Gemeinsame Enclosing-Aufloesung der flachen BFS (<see cref="EnqueueChildrenAsync"/>) und des
    /// Caller-Baums (<see cref="CallGraphTreeBuilder"/>): SemanticModel des Referenzdokuments laden
    /// und das einschliessende Member der Aufrufstelle bestimmen.
    /// </summary>
    internal static async Task<ISymbol?> ResolveEnclosingMemberAsync(
        ReferenceLocation referenceLocation, CancellationToken ct)
    {
        var semanticModel = await referenceLocation.Document.GetSemanticModelAsync(ct);
        return semanticModel is null
            ? null
            : semanticModel.GetEnclosingSymbol(referenceLocation.Location.SourceSpan.Start, ct)
                .NormalizeToOwningMember();
    }

    internal static string FormatSymbolName(ISymbol symbol, CallTreeDirection direction = CallTreeDirection.Incoming)
    {
        // Lambdas/anonyme Methoden (z. B. Aufrufer innerhalb Task.Run(() => ...)) haben ein leeres
        // ISymbol.Name — ohne Sonderbehandlung entsteht ein nichtssagendes "Klasse." Label. Statt
        // dessen entlang ContainingSymbol zum naechsten benannten einschliessenden Member laufen.
        var effectiveName = string.IsNullOrEmpty(symbol.Name)
            ? DescribeAnonymousMethod(symbol)
            : symbol.Name;
        if (direction == CallTreeDirection.Outgoing && symbol is IMethodSymbol { MethodKind: MethodKind.Constructor })
        {
            effectiveName = symbol.ContainingType?.Name ?? effectiveName;
        }
        return symbol.ContainingType is { } containingType
            ? direction == CallTreeDirection.Outgoing && symbol is IMethodSymbol { MethodKind: MethodKind.Constructor }
                ? containingType.Name
                : $"{containingType.Name}.{effectiveName}"
            : effectiveName;
    }

    private static string DescribeAnonymousMethod(ISymbol symbol)
    {
        var current = symbol.ContainingSymbol;
        while (current is not null && string.IsNullOrEmpty(current.Name))
        {
            current = current.ContainingSymbol;
        }
        return current is null ? "<lambda>" : $"<lambda in {current.Name}>";
    }

    private sealed class TraversalState
    {
        private readonly Queue<(ISymbol Symbol, int Level)> _queue = new();
        private readonly HashSet<ISymbol> _seen;

        public TraversalState(ISymbol seed, int depth, int nodeLimit)
        {
            Depth = depth;
            NodeLimit = nodeLimit;
            _seen = new HashSet<ISymbol>(SymbolEqualityComparer.Default) { seed };
            _queue.Enqueue((seed, 1));
        }

        public int Depth { get; }
        public int NodeLimit { get; }
        public List<TransitiveCallSiteEntry> Locations { get; } = new();
        public int VisitedNodeCount { get; private set; }
        public bool TruncatedByNodeLimit { get; set; }
        public bool HasMore => _queue.Count > 0;
        public bool IsAtNodeCap => VisitedNodeCount >= NodeLimit;

        public (ISymbol Symbol, int Level) Dequeue() => _queue.Dequeue();
        public void MarkVisited() => VisitedNodeCount++;
        public void Add(TransitiveCallSiteEntry location) => Locations.Add(location);
        public void MarkSeenAndEnqueue(ISymbol symbol, int level)
        {
            if (_seen.Add(symbol))
            {
                _queue.Enqueue((symbol, level));
            }
        }

        public ReferenceTraversalResult CreateResult(
            int requestedDepth, int effectiveDepth, int maxResults)
        {
            var ordered = Locations
                .Distinct()
                .OrderBy(location => location.Depth)
                .ThenBy(location => location.FilePath, StringComparer.OrdinalIgnoreCase)
                .ThenBy(location => location.FilePath, StringComparer.Ordinal)
                .ThenBy(location => location.Line)
                .ThenBy(location => location.SymbolName, StringComparer.Ordinal)
                .ThenBy(location => location.ProjectName, StringComparer.Ordinal)
                .ThenBy(location => location.ReachedFromSymbolId, StringComparer.Ordinal)
                .ToList();
            var shown = ordered.Take(maxResults).ToList();
            var completeness = new TraversalCompleteness(
                requestedDepth,
                effectiveDepth,
                VisitedNodeCount,
                ordered.Count,
                shown.Count,
                ordered.Count > maxResults,
                TruncatedByNodeLimit,
                requestedDepth != effectiveDepth);
            return new ReferenceTraversalResult(shown, completeness);
        }
    }
}
