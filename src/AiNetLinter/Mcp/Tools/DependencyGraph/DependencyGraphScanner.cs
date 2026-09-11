#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Core;
using AiNetLinter.Core.Documents;
using AiNetLinter.Mcp.Scope;
using AiNetLinter.Output;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;

namespace AiNetLinter.Mcp.Tools.DependencyGraph;

/// <summary>
/// Reine Scan-Logik fuer <c>dependency_graph</c> (Solution rein, Ergebnis-Records raus — keine
/// <c>CallToolResult</c>-Kenntnis, siehe <see cref="DependencyGraphTool"/> fuer den duennen
/// Dispatch). Knoten sind Dateien (Solution-relative Pfade), Kanten sind Datei-zu-Datei, annotiert
/// mit den ueberquerenden Typnamen — abgeleitet aus echten <see cref="SemanticModel"/>-Typreferenzen
/// (nicht nur <c>using</c>-Direktiven), gefiltert auf Typen, die in der geladenen Solution deklariert
/// sind (<see cref="IsDeclaredInSource"/>), um BCL-/NuGet-Rauschen auszuschliessen.
///
/// Zwei Einstiegspunkte: <see cref="ScanFileAsync"/> (Scope = ganze Datei, Union aller darin
/// deklarierten Typen) und <see cref="ScanTypeAsync"/> (Scope = ein einzelner Typ, enger als die
/// ganze Datei — siehe <c>get_type_hierarchy</c>/<c>find_references</c> fuer dasselbe
/// Praezisions-Prinzip). Ab dem zweiten BFS-Hop gibt es nur noch Dateien (kein Typ-Scope mehr),
/// daher nutzt die Traversierung ab Hop 2 ausschliesslich die datei-basierten Scan-Funktionen —
/// <see cref="ScanTypeOutgoingAsync"/>/<see cref="ScanTypeIncomingAsync"/> werden nur fuer Hop 1
/// im Typ-Scope aufgerufen.
/// </summary>
internal static class DependencyGraphScanner
{
    /// <summary>Hard-Cap fuer <c>depth</c> — analog <see cref="SymbolGraph.CallGraphTraversal.MaxRecursionDepth"/>.</summary>
    internal const int MaxDepth = 3;

    /// <summary>
    /// Hard-Cap fuer die Gesamtzahl besuchter (weiter expandierter) Dateien waehrend der BFS —
    /// Scan-Kosten-Grenze, unabhaengig von <c>maxResults</c> (das nur die angezeigten Kanten
    /// begrenzt). Analog <see cref="SymbolGraph.CallGraphTraversal.MaxRecursionNodes"/> (200),
    /// hier etwas niedriger gewaehlt, weil pro Datei mehrere <c>FindReferencesAsync</c>-Aufrufe
    /// (einer je deklariertem Typ) noetig sind statt einem pro Symbol.
    /// </summary>
    internal const int MaxVisitedFiles = 150;

    internal static async Task<DependencyGraphResult> ScanFileAsync(
        Document targetDocument, DependencyGraphScanRequest request, CancellationToken ct)
    {
        var targetFile = ToRelativePath(request.Solution, targetDocument.FilePath ?? "");
        var core = await ScanCoreAsync(
            targetFile,
            request.IncludeOutgoing ? c => ScanFileOutgoingAsync(request.Solution, targetDocument, c) : null,
            request.IncludeIncoming ? c => ScanFileIncomingAsync(request.Solution, targetDocument, c) : null,
            request, ct);
        return core;
    }

    internal static async Task<DependencyGraphResult> ScanTypeAsync(
        INamedTypeSymbol resolvedTypeSymbol, DependencyGraphScanRequest request, CancellationToken ct)
    {
        var targetFile = GetRelativeFilePath(request.Solution, resolvedTypeSymbol)
            ?? throw new InvalidOperationException(
                $"Zieltyp '{resolvedTypeSymbol.Name}' hat keine Quell-Location — dependency_graph erwartet einen aufgeloesten Typ mit Deklaration.");
        var core = await ScanCoreAsync(
            targetFile,
            request.IncludeOutgoing ? c => ScanTypeOutgoingAsync(request.Solution, resolvedTypeSymbol, c) : null,
            request.IncludeIncoming ? c => ScanTypeIncomingAsync(request.Solution, resolvedTypeSymbol, c) : null,
            request, ct);
        return core;
    }

    /// <summary>
    /// Gemeinsame BFS-Orchestrierung fuer beide Einstiegspunkte. Hop 1 nutzt die uebergebenen
    /// Scope-spezifischen Scan-Funktionen (Typ- oder Datei-Scope), alle weiteren Hops expandieren
    /// ausschliesslich ueber die datei-basierten Scan-Funktionen (siehe Klassen-Doc-Kommentar).
    /// Zyklen (Datei A -> Datei B -> Datei A) werden ueber ein Visited-Set abgefangen: eine bereits
    /// besuchte Datei wird nicht erneut expandiert, aber die schliessende Kante bleibt im Ergebnis
    /// sichtbar (kein stillschweigendes Verwerfen).
    /// </summary>
    private static async Task<DependencyGraphResult> ScanCoreAsync(
        string targetFile,
        Func<CancellationToken, Task<Dictionary<string, DependencyGraphEdgeAccumulator>>>? hop1Outgoing,
        Func<CancellationToken, Task<Dictionary<string, DependencyGraphEdgeAccumulator>>>? hop1Incoming,
        DependencyGraphScanRequest request,
        CancellationToken ct)
    {
        var clampedDepth = Math.Clamp(request.Depth, 1, MaxDepth);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { targetFile };
        var edgeMap = new Dictionary<(string From, string To, string Direction), DependencyGraphEdgeAccumulator>();
        var scopeClassifier = request.ScopeClassifier ?? new McpScopeClassifier();
        var nodeScopes = new Dictionary<string, McpDocumentScope>(StringComparer.OrdinalIgnoreCase);
        var excludedNodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var excludedEdges = new HashSet<(string From, string To, string Direction)>();
        var scopeState = new DependencyGraphScopeState(
            scopeClassifier,
            nodeScopes,
            excludedNodes,
            excludedEdges);
        await DependencyGraphScopeProjection.ClassifyNodeAsync(
            request.Solution, targetFile, scopeClassifier, nodeScopes, ct);
        var frontier = new List<string>();
        var nodeCapReached = await ScanFirstHopAsync(
            new DependencyGraphFirstHopRequest(
                targetFile,
                hop1Outgoing,
                hop1Incoming,
                new DependencyGraphTraversalState(request, frontier, visited, edgeMap, scopeState)),
            ct);

        for (var level = 2; level <= clampedDepth && frontier.Count > 0 && !nodeCapReached; level++)
        {
            frontier = await ExpandFrontierAsync(
                new DependencyGraphTraversalState(
                    request,
                    frontier,
                    visited,
                    edgeMap,
                    scopeState),
                ct);
            nodeCapReached |= visited.Count >= MaxVisitedFiles && frontier.Count == 0;
        }

        return DependencyGraphScopeProjection.BuildResult(
            new DependencyGraphBuildRequest(
                request,
                targetFile,
                edgeMap,
                nodeScopes,
                excludedNodes,
                excludedEdges,
                clampedDepth,
                nodeCapReached));
    }

    private static async Task<bool> ScanFirstHopAsync(
        DependencyGraphFirstHopRequest firstHop,
        CancellationToken ct)
    {
        var nodeCapReached = false;
        nodeCapReached |= await ScanHopAsync(firstHop, firstHop.Outgoing, "outgoing", true, ct);
        nodeCapReached |= await ScanHopAsync(firstHop, firstHop.Incoming, "incoming", false, ct);
        return nodeCapReached;
    }

    private static async Task<bool> ScanHopAsync(
        DependencyGraphFirstHopRequest firstHop,
        Func<CancellationToken, Task<Dictionary<string, DependencyGraphEdgeAccumulator>>>? scan,
        string direction,
        bool isOutgoing,
        CancellationToken ct)
    {
        if (scan is null) return false;
        var discovered = await scan(ct);
        var filtered = await DependencyGraphScopeProjection.FilterDiscoveredEdgesAsync(
            new DependencyGraphFilterRequest(
                firstHop.State.Request,
                firstHop.TargetFile,
                direction,
                discovered,
                firstHop.State.ScopeState),
            ct);
        MergeHopEdges(firstHop.State.EdgeMap, firstHop.TargetFile, direction, filtered, isOutgoing);
        return TryEnqueueFrontier(filtered.Keys, firstHop.State.Visited, firstHop.State.Frontier);
    }

    private static async Task<List<string>> ExpandFrontierAsync(
        DependencyGraphTraversalState state,
        CancellationToken ct)
    {
        var nextFrontier = new List<string>();
        var capReached = false;
        foreach (var file in state.Frontier)
        {
            ct.ThrowIfCancellationRequested();
            if (capReached) break;
            var document = ResolveDocumentByRelativePath(state.Request.Solution, file);
            if (document is null) continue;

            if (state.Request.IncludeOutgoing)
            {
                var discovered = await ScanFileOutgoingAsync(state.Request.Solution, document, ct);
                var filtered = await DependencyGraphScopeProjection.FilterDiscoveredEdgesAsync(
                    new DependencyGraphFilterRequest(
                        state.Request,
                        file,
                        "outgoing",
                        discovered,
                        state.ScopeState),
                    ct);
                MergeHopEdges(state.EdgeMap, file, "outgoing", filtered, isOutgoing: true);
                capReached |= TryEnqueueFrontier(filtered.Keys, state.Visited, nextFrontier);
            }
            if (state.Request.IncludeIncoming)
            {
                var discovered = await ScanFileIncomingAsync(state.Request.Solution, document, ct);
                var filtered = await DependencyGraphScopeProjection.FilterDiscoveredEdgesAsync(
                    new DependencyGraphFilterRequest(
                        state.Request,
                        file,
                        "incoming",
                        discovered,
                        state.ScopeState),
                    ct);
                MergeHopEdges(state.EdgeMap, file, "incoming", filtered, isOutgoing: false);
                capReached |= TryEnqueueFrontier(filtered.Keys, state.Visited, nextFrontier);
            }
        }
        return capReached ? new List<string>() : nextFrontier;
    }

    // --- Hop 1, Typ-Scope: nur die Deklarationsknoten des Zieltyps selbst (partial-faehig ueber
    // alle DeclaringSyntaxReferences), enger als eine ganze Datei. ---

    private static async Task<Dictionary<string, DependencyGraphEdgeAccumulator>> ScanTypeOutgoingAsync(
        Solution solution, INamedTypeSymbol resolvedTypeSymbol, CancellationToken ct)
    {
        var edges = new Dictionary<string, DependencyGraphEdgeAccumulator>(StringComparer.OrdinalIgnoreCase);
        foreach (var syntaxRef in resolvedTypeSymbol.DeclaringSyntaxReferences)
        {
            var document = solution.GetDocument(syntaxRef.SyntaxTree);
            var semanticModel = document is null ? null : await document.GetSemanticModelAsync(ct);
            if (semanticModel is null) continue;
            var node = await syntaxRef.GetSyntaxAsync(ct);

            foreach (var referencedType in CollectReferencedTypes(node, semanticModel, ct))
            {
                AddOutgoingTypeEdgeIfEligible(solution, edges, resolvedTypeSymbol, referencedType);
            }
        }
        return edges;
    }

    /// <summary>Reine Filter-/Edge-Logik pro referenziertem Typ. Eigene Methode, damit
    /// <see cref="ScanTypeOutgoingAsync"/> unter <c>MaxCognitiveComplexity</c> bleibt (sonst zwei
    /// verschachtelte Ebenen).</summary>
    private static void AddOutgoingTypeEdgeIfEligible(
        Solution solution, Dictionary<string, DependencyGraphEdgeAccumulator> edges, INamedTypeSymbol resolvedTypeSymbol, INamedTypeSymbol referencedType)
    {
        if (SymbolEqualityComparer.Default.Equals(referencedType, resolvedTypeSymbol)) return;
        if (!IsDeclaredInSource(referencedType)) return;
        var declFile = GetRelativeFilePath(solution, referencedType);
        if (declFile is null) return;
        AddEdge(edges, declFile, referencedType.Name);
    }

    private static async Task<Dictionary<string, DependencyGraphEdgeAccumulator>> ScanTypeIncomingAsync(
        Solution solution, INamedTypeSymbol resolvedTypeSymbol, CancellationToken ct)
    {
        var edges = new Dictionary<string, DependencyGraphEdgeAccumulator>(StringComparer.OrdinalIgnoreCase);
        var ownSpans = resolvedTypeSymbol.DeclaringSyntaxReferences
            .Select(r => (r.SyntaxTree, r.Span))
            .ToList();

        var refs = await SymbolFinder.FindReferencesAsync(resolvedTypeSymbol, solution, ct);
        foreach (var reference in refs)
        {
            foreach (var referenceLocation in reference.Locations)
            {
                var location = referenceLocation.Location;
                if (!location.IsInSource || location.SourceTree is null) continue;
                // Eigene Deklaration ausschliessen (z. B. ein Singleton-Feld "static Foo Instance =
                // new Foo();" innerhalb von Foo selbst) — praeziser als "ganze Datei ausschliessen",
                // siehe Klassen-Doc-Kommentar: andere Typen in derselben Datei duerfen den Typ
                // durchaus referenzieren, das ist im Typ-Scope kein Selbstbezug.
                if (ownSpans.Any(s => s.SyntaxTree == location.SourceTree && s.Span.Contains(location.SourceSpan))) continue;
                var file = ToRelativePath(solution, location.SourceTree.FilePath);
                AddEdge(edges, file, resolvedTypeSymbol.Name);
            }
        }
        return edges;
    }

    // --- Datei-Scope: Union aller im Dokument deklarierten Typen. Wird sowohl fuer Hop 1 im
    // Datei-Scope als auch fuer jeden weiteren BFS-Hop (beide Scopes) genutzt. ---

    private static async Task<Dictionary<string, DependencyGraphEdgeAccumulator>> ScanFileOutgoingAsync(
        Solution solution, Document document, CancellationToken ct)
    {
        var edges = new Dictionary<string, DependencyGraphEdgeAccumulator>(StringComparer.OrdinalIgnoreCase);
        var semanticModel = await document.GetSemanticModelAsync(ct);
        var root = await document.GetSyntaxRootAsync(ct);
        if (semanticModel is null || root is null) return edges;

        var selfFile = ToRelativePath(solution, document.FilePath ?? "");
        foreach (var referencedType in CollectReferencedTypes(root, semanticModel, ct))
        {
            if (!IsDeclaredInSource(referencedType)) continue;
            var declFile = GetRelativeFilePath(solution, referencedType);
            if (declFile is null || string.Equals(declFile, selfFile, StringComparison.OrdinalIgnoreCase)) continue;
            AddEdge(edges, declFile, referencedType.Name);
        }
        return edges;
    }

    private static async Task<Dictionary<string, DependencyGraphEdgeAccumulator>> ScanFileIncomingAsync(
        Solution solution, Document document, CancellationToken ct)
    {
        var edges = new Dictionary<string, DependencyGraphEdgeAccumulator>(StringComparer.OrdinalIgnoreCase);
        var selfFile = ToRelativePath(solution, document.FilePath ?? "");
        var types = await GetTypesDeclaredInDocumentAsync(document, ct);

        foreach (var type in types)
        {
            var refs = await SymbolFinder.FindReferencesAsync(type, solution, ct);
            AddIncomingTypeEdges(solution, edges, type, refs, selfFile);
        }
        return edges;
    }

    /// <summary>Doppelt verschachtelte Referenz-/Location-Traversierung als eigene Methode, damit
    /// <see cref="ScanFileIncomingAsync"/> unter <c>MaxCognitiveComplexity</c> bleibt (drei
    /// verschachtelte Schleifen statt zwei).</summary>
    private static void AddIncomingTypeEdges(
        Solution solution, Dictionary<string, DependencyGraphEdgeAccumulator> edges, INamedTypeSymbol type,
        IEnumerable<ReferencedSymbol> refs, string selfFile)
    {
        foreach (var reference in refs)
        {
            foreach (var referenceLocation in reference.Locations)
            {
                var location = referenceLocation.Location;
                if (!location.IsInSource || location.SourceTree is null) continue;
                var file = ToRelativePath(solution, location.SourceTree.FilePath);
                if (string.Equals(file, selfFile, StringComparison.OrdinalIgnoreCase)) continue;
                AddEdge(edges, file, type.Name);
            }
        }
    }

    private static async Task<List<INamedTypeSymbol>> GetTypesDeclaredInDocumentAsync(Document document, CancellationToken ct)
    {
        var result = new List<INamedTypeSymbol>();
        var semanticModel = await document.GetSemanticModelAsync(ct);
        var root = await document.GetSyntaxRootAsync(ct);
        if (semanticModel is null || root is null) return result;

        // BaseTypeDeclarationSyntax deckt class/struct/interface/enum/record gleichermassen ab.
        foreach (var typeDecl in root.DescendantNodes().OfType<BaseTypeDeclarationSyntax>())
        {
            if (semanticModel.GetDeclaredSymbol(typeDecl, ct) is INamedTypeSymbol namedType) result.Add(namedType);
        }
        return result;
    }

    /// <summary>
    /// Sammelt alle Typreferenzen unter <paramref name="root"/> ueber
    /// <see cref="SemanticModel.GetSymbolInfo(SyntaxNode, CancellationToken)"/>. Beschraenkt auf
    /// Identifier-/Generic-/Qualified-Name-Knoten (statt jeden Knoten zu pruefen) — deckt
    /// Feld-/Parameter-/Rueckgabetypen, Basisliste, Objekterzeugung, <c>typeof</c>,
    /// Attribut-Namen und generische Typargumente ab, weil deren Typ-Syntax letztlich immer auf
    /// einen dieser drei Knotentypen herunterbricht.
    /// </summary>
    private static IEnumerable<INamedTypeSymbol> CollectReferencedTypes(
        SyntaxNode root, SemanticModel semanticModel, CancellationToken ct)
    {
        foreach (var node in root.DescendantNodesAndSelf())
        {
            ct.ThrowIfCancellationRequested();
            if (node is not (IdentifierNameSyntax or GenericNameSyntax or QualifiedNameSyntax)) continue;
            if (semanticModel.GetSymbolInfo(node, ct).Symbol is INamedTypeSymbol namedType)
            {
                yield return namedType;
            }
        }
    }

    private static bool IsDeclaredInSource(INamedTypeSymbol type) => type.Locations.Any(l => l.IsInSource);

    /// <summary>
    /// Liefert den Solution-relativen Pfad der primaeren Deklaration von <paramref name="type"/>.
    /// Bei partiellen Typen (mehrere Quell-Locations) wird deterministisch die erste nach
    /// (Dateipfad, Zeile) sortierte Location gewaehlt, damit zwei Aufrufe mit identischem Input
    /// dasselbe Ergebnis liefern.
    /// </summary>
    private static string? GetRelativeFilePath(Solution solution, INamedTypeSymbol type)
    {
        var location = type.Locations
            .Where(l => l.IsInSource && l.SourceTree is not null)
            .OrderBy(l => l.SourceTree!.FilePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(l => l.GetLineSpan().StartLinePosition.Line)
            .FirstOrDefault();
        return location is null ? null : ToRelativePath(solution, location.SourceTree!.FilePath);
    }

    private static string ToRelativePath(Solution solution, string absolutePath)
    {
        if (string.IsNullOrEmpty(absolutePath)) return absolutePath;
        var solutionDir = SolutionDocumentPathResolver.GetSolutionDirectory(solution) ?? "";
        return PathNormalizer.ToRelative(solutionDir, absolutePath);
    }

    private static Document? ResolveDocumentByRelativePath(Solution solution, string relativePath)
        => DiffImpactAnalyzer.FindDocumentByPath(solution, relativePath);

    private static void AddEdge(Dictionary<string, DependencyGraphEdgeAccumulator> edges, string file, string typeName)
    {
        if (!edges.TryGetValue(file, out var acc))
        {
            acc = new DependencyGraphEdgeAccumulator();
            edges[file] = acc;
        }
        acc.TypeNames.Add(typeName);
        acc.ReferenceCount++;
    }

    private static void MergeHopEdges(
        Dictionary<(string From, string To, string Direction), DependencyGraphEdgeAccumulator> edgeMap,
        string anchorFile,
        string direction,
        Dictionary<string, DependencyGraphEdgeAccumulator> discovered,
        bool isOutgoing)
    {
        foreach (var (otherFile, acc) in discovered)
        {
            var key = isOutgoing ? (anchorFile, otherFile, direction) : (otherFile, anchorFile, direction);
            if (!edgeMap.TryGetValue(key, out var existing))
            {
                existing = new DependencyGraphEdgeAccumulator();
                edgeMap[key] = existing;
            }
            foreach (var typeName in acc.TypeNames) existing.TypeNames.Add(typeName);
            existing.ReferenceCount += acc.ReferenceCount;
        }
    }

    /// <summary>
    /// Fuegt neu entdeckte Dateien zum Visited-Set/zur naechsten Frontier hinzu, bis
    /// <see cref="MaxVisitedFiles"/> erreicht ist. Liefert <see langword="true"/>, sobald der
    /// Hard-Cap eine weitere Datei blockiert hat (Signal fuer <see cref="DependencyGraphResult.NodeCapReached"/>).
    /// </summary>
    private static bool TryEnqueueFrontier(IEnumerable<string> files, HashSet<string> visited, List<string> frontier)
    {
        var capHit = false;
        foreach (var file in files)
        {
            if (visited.Contains(file)) continue;
            if (visited.Count >= MaxVisitedFiles)
            {
                capHit = true;
                continue;
            }
            visited.Add(file);
            frontier.Add(file);
        }
        return capHit;
    }

}
