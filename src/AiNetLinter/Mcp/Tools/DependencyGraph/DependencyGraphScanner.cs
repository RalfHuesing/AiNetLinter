#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Core;
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
        INamedTypeSymbol targetType, DependencyGraphScanRequest request, CancellationToken ct)
    {
        var targetFile = GetRelativeFilePath(request.Solution, targetType)
            ?? throw new InvalidOperationException(
                $"Zieltyp '{targetType.Name}' hat keine Quell-Location — dependency_graph erwartet einen aufgeloesten Typ mit Deklaration.");
        var core = await ScanCoreAsync(
            targetFile,
            request.IncludeOutgoing ? c => ScanTypeOutgoingAsync(request.Solution, targetType, c) : null,
            request.IncludeIncoming ? c => ScanTypeIncomingAsync(request.Solution, targetType, c) : null,
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
        Func<CancellationToken, Task<Dictionary<string, EdgeAccumulator>>>? hop1Outgoing,
        Func<CancellationToken, Task<Dictionary<string, EdgeAccumulator>>>? hop1Incoming,
        DependencyGraphScanRequest request,
        CancellationToken ct)
    {
        var clampedDepth = Math.Clamp(request.Depth, 1, MaxDepth);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { targetFile };
        var edgeMap = new Dictionary<(string From, string To, string Direction), EdgeAccumulator>();
        var nodeCapReached = false;
        var frontier = new List<string>();

        if (hop1Outgoing is not null)
        {
            var discovered = await hop1Outgoing(ct);
            MergeHopEdges(edgeMap, targetFile, "outgoing", discovered, isOutgoing: true);
            nodeCapReached |= TryEnqueueFrontier(discovered.Keys, visited, frontier);
        }
        if (hop1Incoming is not null)
        {
            var discovered = await hop1Incoming(ct);
            MergeHopEdges(edgeMap, targetFile, "incoming", discovered, isOutgoing: false);
            nodeCapReached |= TryEnqueueFrontier(discovered.Keys, visited, frontier);
        }

        for (var level = 2; level <= clampedDepth && frontier.Count > 0 && !nodeCapReached; level++)
        {
            frontier = await ExpandFrontierAsync(request, frontier, visited, edgeMap, ct);
            nodeCapReached |= visited.Count >= MaxVisitedFiles && frontier.Count == 0;
        }

        return BuildResult(request, targetFile, edgeMap, clampedDepth, nodeCapReached);
    }

    private static async Task<List<string>> ExpandFrontierAsync(
        DependencyGraphScanRequest request,
        List<string> frontier,
        HashSet<string> visited,
        Dictionary<(string From, string To, string Direction), EdgeAccumulator> edgeMap,
        CancellationToken ct)
    {
        var nextFrontier = new List<string>();
        var capReached = false;
        foreach (var file in frontier)
        {
            ct.ThrowIfCancellationRequested();
            if (capReached) break;
            var document = ResolveDocumentByRelativePath(request.Solution, file);
            if (document is null) continue;

            if (request.IncludeOutgoing)
            {
                var discovered = await ScanFileOutgoingAsync(request.Solution, document, ct);
                MergeHopEdges(edgeMap, file, "outgoing", discovered, isOutgoing: true);
                capReached |= TryEnqueueFrontier(discovered.Keys, visited, nextFrontier);
            }
            if (request.IncludeIncoming)
            {
                var discovered = await ScanFileIncomingAsync(request.Solution, document, ct);
                MergeHopEdges(edgeMap, file, "incoming", discovered, isOutgoing: false);
                capReached |= TryEnqueueFrontier(discovered.Keys, visited, nextFrontier);
            }
        }
        return capReached ? new List<string>() : nextFrontier;
    }

    // --- Hop 1, Typ-Scope: nur die Deklarationsknoten des Zieltyps selbst (partial-faehig ueber
    // alle DeclaringSyntaxReferences), enger als eine ganze Datei. ---

    private static async Task<Dictionary<string, EdgeAccumulator>> ScanTypeOutgoingAsync(
        Solution solution, INamedTypeSymbol targetType, CancellationToken ct)
    {
        var edges = new Dictionary<string, EdgeAccumulator>(StringComparer.OrdinalIgnoreCase);
        foreach (var syntaxRef in targetType.DeclaringSyntaxReferences)
        {
            var document = solution.GetDocument(syntaxRef.SyntaxTree);
            var semanticModel = document is null ? null : await document.GetSemanticModelAsync(ct);
            if (semanticModel is null) continue;
            var node = await syntaxRef.GetSyntaxAsync(ct);

            foreach (var referencedType in CollectReferencedTypes(node, semanticModel, ct))
            {
                AddOutgoingTypeEdgeIfEligible(solution, edges, targetType, referencedType);
            }
        }
        return edges;
    }

    /// <summary>Reine Filter-/Edge-Logik pro referenziertem Typ. Eigene Methode, damit
    /// <see cref="ScanTypeOutgoingAsync"/> unter <c>MaxCognitiveComplexity</c> bleibt (sonst zwei
    /// verschachtelte Ebenen).</summary>
    private static void AddOutgoingTypeEdgeIfEligible(
        Solution solution, Dictionary<string, EdgeAccumulator> edges, INamedTypeSymbol targetType, INamedTypeSymbol referencedType)
    {
        if (SymbolEqualityComparer.Default.Equals(referencedType, targetType)) return;
        if (!IsDeclaredInSource(referencedType)) return;
        var declFile = GetRelativeFilePath(solution, referencedType);
        if (declFile is null) return;
        AddEdge(edges, declFile, referencedType.Name);
    }

    private static async Task<Dictionary<string, EdgeAccumulator>> ScanTypeIncomingAsync(
        Solution solution, INamedTypeSymbol targetType, CancellationToken ct)
    {
        var edges = new Dictionary<string, EdgeAccumulator>(StringComparer.OrdinalIgnoreCase);
        var ownSpans = targetType.DeclaringSyntaxReferences
            .Select(r => (r.SyntaxTree, r.Span))
            .ToList();

        var refs = await SymbolFinder.FindReferencesAsync(targetType, solution, ct);
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
                AddEdge(edges, file, targetType.Name);
            }
        }
        return edges;
    }

    // --- Datei-Scope: Union aller im Dokument deklarierten Typen. Wird sowohl fuer Hop 1 im
    // Datei-Scope als auch fuer jeden weiteren BFS-Hop (beide Scopes) genutzt. ---

    private static async Task<Dictionary<string, EdgeAccumulator>> ScanFileOutgoingAsync(
        Solution solution, Document document, CancellationToken ct)
    {
        var edges = new Dictionary<string, EdgeAccumulator>(StringComparer.OrdinalIgnoreCase);
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

    private static async Task<Dictionary<string, EdgeAccumulator>> ScanFileIncomingAsync(
        Solution solution, Document document, CancellationToken ct)
    {
        var edges = new Dictionary<string, EdgeAccumulator>(StringComparer.OrdinalIgnoreCase);
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
        Solution solution, Dictionary<string, EdgeAccumulator> edges, INamedTypeSymbol type,
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

    /// <summary>Heuristik ueber den Solution-relativen Pfad — Test-Projekte folgen der Konvention
    /// <c>&lt;ProjektName&gt;.Tests</c> (siehe <c>AiNetLinter.Tests</c>), erkennbar am Pfadsegment
    /// <c>.Tests/</c>. Nur fuer die Truncation-Sortierreihenfolge relevant, siehe
    /// <see cref="BuildResult"/>.</summary>
    private static bool IsTestProjectFile(string relativePath) =>
        relativePath.Contains(".Tests/", StringComparison.Ordinal);

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
        var solutionDir = Path.GetDirectoryName(solution.FilePath) ?? "";
        return Path.GetRelativePath(solutionDir, absolutePath).Replace('\\', '/');
    }

    private static Document? ResolveDocumentByRelativePath(Solution solution, string relativePath)
    {
        var solutionDir = Path.GetDirectoryName(solution.FilePath) ?? "";
        var absolutePath = Path.GetFullPath(Path.Combine(solutionDir, relativePath));
        return DiffImpactAnalyzer.FindDocumentByPath(solution, absolutePath);
    }

    private static void AddEdge(Dictionary<string, EdgeAccumulator> edges, string file, string typeName)
    {
        if (!edges.TryGetValue(file, out var acc))
        {
            acc = new EdgeAccumulator();
            edges[file] = acc;
        }
        acc.TypeNames.Add(typeName);
        acc.ReferenceCount++;
    }

    private static void MergeHopEdges(
        Dictionary<(string From, string To, string Direction), EdgeAccumulator> edgeMap,
        string anchorFile,
        string direction,
        Dictionary<string, EdgeAccumulator> discovered,
        bool isOutgoing)
    {
        foreach (var (otherFile, acc) in discovered)
        {
            var key = isOutgoing ? (anchorFile, otherFile, direction) : (otherFile, anchorFile, direction);
            if (!edgeMap.TryGetValue(key, out var existing))
            {
                existing = new EdgeAccumulator();
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

    private static DependencyGraphResult BuildResult(
        DependencyGraphScanRequest request,
        string targetFile,
        Dictionary<(string From, string To, string Direction), EdgeAccumulator> edgeMap,
        int clampedDepth,
        bool nodeCapReached)
    {
        var effectiveMax = request.MaxResults < 1 ? 1 : request.MaxResults;
        // Test-Projekt-Kanten NACH Produktionscode-Kanten einsortieren (nicht rein alphabetisch):
        // "src/AiNetLinter.Tests/..." sortiert ordinal VOR "src/AiNetLinter/..." ('.' < '/'), weil
        // .Tests alphabetisch zufaellig zuerst kommt. Bei einer stark referenzierten Datei (z. B.
        // McpCodeGraphServer.cs: 30 Test- + 38 Produktions-Kanten) fraesste die reine
        // Ordinal-Sortierung sonst zuerst ALLE Test-Kanten in die maxResults-Kappung, bevor
        // ueberhaupt Produktionscode-Kanten drankommen — genau umgekehrt zur eigentlichen
        // Blast-Radius-Frage, bei der Produktionscode-Kopplung die relevantere ist. Test-Kopplung
        // ist erwartet/risikoarm, Produktionscode-Kopplung ist das, was beim Aendern der Zieldatei
        // tatsaechlich bricht.
        var allEdges = edgeMap
            .OrderBy(kv => kv.Key.Direction == "outgoing" ? 0 : 1)
            .ThenBy(kv => IsTestProjectFile(kv.Key.Direction == "outgoing" ? kv.Key.To : kv.Key.From) ? 1 : 0)
            .ThenBy(kv => kv.Key.From, StringComparer.Ordinal)
            .ThenBy(kv => kv.Key.To, StringComparer.Ordinal)
            .Select(kv => new DependencyEdge(
                kv.Key.From,
                kv.Key.To,
                kv.Key.Direction,
                kv.Value.TypeNames.OrderBy(n => n, StringComparer.Ordinal).ToList(),
                kv.Value.ReferenceCount))
            .ToList();

        var totalEdgeCount = allEdges.Count;
        var shown = totalEdgeCount <= effectiveMax ? allEdges : allEdges.Take(effectiveMax).ToList();
        var truncated = totalEdgeCount > effectiveMax || nodeCapReached;

        return new DependencyGraphResult(
            Edges: shown,
            TotalEdgeCount: totalEdgeCount,
            ProjectReferences: BuildProjectReferences(request.Solution, targetFile),
            IncludeOutgoing: request.IncludeOutgoing,
            IncludeIncoming: request.IncludeIncoming,
            ClampedDepth: clampedDepth,
            NodeCapReached: nodeCapReached,
            Truncated: truncated);
    }

    /// <summary>
    /// Optionale Projekt-Ebene (siehe Roadmap-Scope-Entscheidung): guenstig zu ermitteln
    /// (<c>Project.ProjectReferences</c>, keine NuGet-Aufrufe), daher immer mitgeliefert, wenn das
    /// Zielprojekt aufloest. Liefert genau einen Eintrag (das Zielprojekt selbst mit seinen
    /// direkten Projekt-Referenzen) statt eines vollstaendigen Projektgraphen — das waere ausserhalb
    /// des Scopes dieses Tools.
    /// </summary>
    private static IReadOnlyList<ProjectReferenceEntry> BuildProjectReferences(Solution solution, string targetFile)
    {
        var document = ResolveDocumentByRelativePath(solution, targetFile);
        var project = document?.Project;
        if (project is null) return Array.Empty<ProjectReferenceEntry>();

        var refs = project.ProjectReferences
            .Select(pr => solution.GetProject(pr.ProjectId)?.Name)
            .Where(name => name is not null)
            .Select(name => name!)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        return refs.Count == 0
            ? Array.Empty<ProjectReferenceEntry>()
            : new[] { new ProjectReferenceEntry(project.Name, refs) };
    }

    private sealed class EdgeAccumulator
    {
        internal HashSet<string> TypeNames { get; } = new(StringComparer.Ordinal);
        internal int ReferenceCount { get; set; }
    }
}
