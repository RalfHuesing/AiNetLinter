#nullable enable

using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Assemblies.Analysis;
using AiNetLinter.Mcp.Projects;
using AiNetLinter.Mcp.Tools;
using AiNetLinter.Mcp.Tools.CallTree;
using AiNetLinter.Mcp.Tools.DependencyGraph;
using AiNetLinter.Mcp.Tools.SymbolGraph;
using ModelContextProtocol.Server;

namespace AiNetLinter.Mcp.Registration;

/// <summary>
/// Registriert die sechs reinen Symbolgraph-Tools (<c>find_symbol</c>, <c>find_references</c>,
/// <c>get_impact</c>, <c>get_type_hierarchy</c>, <c>get_call_tree</c>, <c>dependency_graph</c>) an
/// der von <see cref="McpServerOptionsFactory"/> aufgebauten Tool-Collection. Aus
/// <see cref="McpServerOptionsFactory"/> ausgelagert, damit dessen eigener <c>AIContextFootprint</c>
/// (siehe <c>AiNetLinter.mdc</c>) nicht mit jedem neu registrierten Tool waechst. Jedes Lambda ist
/// zielgebunden: <c>targetType</c> und <c>targetPath</c> sind Pflicht und werden am gemeinsamen
/// <see cref="AnalysisToolCall"/> validiert.
/// </summary>
internal static class SymbolGraphToolRegistrations
{
    /// <summary>
    /// Fuegt <paramref name="tools"/> die sechs Symbolgraph-Tools hinzu. Tools erreichen die
    /// residente Instanz ihres Keys per Lease-Closure - kein DI-Container
    /// (siehe <c>AiNetLinterRichtlinien.mdc</c> §2).
    /// </summary>
    internal static void Register(
        McpServerPrimitiveCollection<McpServerTool> tools,
        AnalysisToolRoute targetRoute)
    {
        AddFindSymbol(tools, targetRoute);
        AddFindReferences(tools, targetRoute);
        AddGetCallTree(tools, targetRoute);
        AddGetImpact(tools, targetRoute);
        AddGetTypeHierarchy(tools, targetRoute);
        AddDependencyGraph(tools, targetRoute);
    }

    private static void AddFindSymbol(
        McpServerPrimitiveCollection<McpServerTool> tools,
        AnalysisToolRoute targetRoute)
    {
        tools.Add(McpServerTool.Create(
            async (string targetType, string targetPath, string[]? namePatterns = null, string? kind = null, int maxResults = 50, CancellationToken ct = default) =>
                await AnalysisToolCall.ExecuteRouted(
                    targetRoute,
                    new AnalysisToolCallRequest(
                        new AnalysisTargetRequest(targetType, targetPath),
                        new AnalysisToolDispatch(
                            ProjectCall: lease => FindSymbolTool.ExecuteAsync(lease.Server, namePatterns, kind, maxResults, ct),
                            AssemblySessionCall: lease => FindSymbolTool.ExecuteAsync(lease.Server, namePatterns, kind, maxResults, ct)),
                        ct)),
            McpToolRegistrationOptions.TargetedReadOnlyTool("find_symbol", FindSymbolDescription)));
    }

    private const string FindSymbolDescription =
        "Wann nutzen: Fundstelle(n) von C#-Symbolen per Namens-Substring finden, wenn der " +
        "exakte Ort unbekannt ist. namePatterns: Array von Namens-Mustern (auch fuer genau einen Namen; " +
        "Batch loest N sequentielle Calls ab, max. 10 pro Call, z. B. namePatterns: [\"Greeter\"]). " +
        "kind: optionaler Typfilter (Class, Record, Method, Property, Interface, Struct, Enum; " +
        "deutsche und englische Werte). maxResults: Begrenzung der Trefferliste (Default 50). " +
        "Bei 0 C#-Treffern Hinweis auf Textfunde in Nicht-C#-Dateien (Fallback search_pattern). " +
        "Liefert strukturierte FindSymbolBatchDto in structuredContent.";

    private static void AddFindReferences(
        McpServerPrimitiveCollection<McpServerTool> tools,
        AnalysisToolRoute targetRoute)
    {
        tools.Add(McpServerTool.Create(
            async (string targetType, string targetPath, string? symbolIdentifier = null, int maxResults = 50, int depth = 1, CancellationToken ct = default) =>
                await AnalysisToolCall.ExecuteRouted(
                    targetRoute,
                    new AnalysisToolCallRequest(
                        new AnalysisTargetRequest(targetType, targetPath),
                        new AnalysisToolDispatch(
                            ProjectCall: lease => FindReferencesTool.ExecuteAsync(lease.Server, symbolIdentifier, maxResults, depth, ct),
                            AssemblySessionCall: lease => FindReferencesTool.ExecuteAsync(lease.Server, symbolIdentifier, maxResults, depth, ct)),
                        ct)),
            McpToolRegistrationOptions.TargetedReadOnlyTool("find_references", FindReferencesDescription)));
    }

    private const string FindReferencesDescription =
        "Wann nutzen: alle Aufrufstellen eines C#-Symbols finden, optional transitiv. " +
        "symbolIdentifier: \"M:Namespace.Klasse.Methode\" oder \"Datei.cs:42:10\" oder " +
        "\"Datei.cs:42\" (Zeile ohne Spalte — bei mehreren Symbolen auf der Zeile liefert das " +
        "Ergebnis eine Kandidatenliste statt eines Treffers) oder \"Klasse.Methode\". " +
        "depth (Default 1, hard cap 3) liefert immer structuredContent.callSites plus " +
        "completeness mit Tiefe, Herkunft und getrennten Trunkierungsgruenden; die " +
        "Traversierung ist hart auf 200 besuchte Knoten begrenzt.";

    private static void AddGetCallTree(
        McpServerPrimitiveCollection<McpServerTool> tools,
        AnalysisToolRoute targetRoute)
    {
        tools.Add(McpServerTool.Create(
            async (string targetType, string targetPath, string? symbolIdentifier = null, int depth = 2, string? format = null, int topN = 10, string? direction = null, CancellationToken ct = default) =>
                await AnalysisToolCall.ExecuteRouted(
                    targetRoute,
                    new AnalysisToolCallRequest(
                        new AnalysisTargetRequest(targetType, targetPath),
                        new AnalysisToolDispatch(
                            ProjectCall: lease => GetCallTreeTool.ExecuteAsync(lease.Server, new GetCallTreeInput(symbolIdentifier, depth, format, topN, direction), ct),
                            AssemblySessionCall: lease => GetCallTreeTool.ExecuteAsync(lease.Server, new GetCallTreeInput(symbolIdentifier, depth, format, topN, direction), ct)),
                        ct)),
            McpToolRegistrationOptions.TargetedReadOnlyTool("get_call_tree", GetCallTreeDescription)));
    }

    private const string GetCallTreeDescription =
        "Wann nutzen: echten Aufrufer- oder Aufgerufene-Baum eines C#-Symbols sehen (wer ruft " +
        "dieses Symbol auf bzw. wen ruft es auf), " +
        "transitiv, als Eltern-Kind-Struktur statt flacher Liste). symbolIdentifier wie " +
        "find_references. depth Default 2 (hard cap 5). format: \"ascii\" (Default) oder " +
        "\"mermaid\" (flowchart TD). direction: \"incoming\" (Default), \"outgoing\" oder " +
        "\"both\". topN (Default 10) begrenzt Fan-Out pro Ebene, Traversierung hart begrenzt " +
        "auf 250 Knoten.";

    private static void AddGetImpact(
        McpServerPrimitiveCollection<McpServerTool> tools,
        AnalysisToolRoute targetRoute)
    {
        tools.Add(McpServerTool.Create(
            async (string targetType, string targetPath, string? gitRef = null, string? symbolIdentifier = null, int maxResults = 50, int depth = 1,
                string? detailLevel = null,
                int maxChangedSymbols = ChangeContextContract.DefaultMaxChangedSymbols,
                int maxTestsPerSymbol = ChangeContextContract.DefaultMaxTestsPerSymbol,
                CancellationToken ct = default) =>
                await AnalysisToolCall.ExecuteRouted(
                    targetRoute,
                    new AnalysisToolCallRequest(
                        new AnalysisTargetRequest(targetType, targetPath),
                        new AnalysisToolDispatch(ProjectCall: lease => GetImpactTool.ExecuteAsync(
                            lease.Server,
                            new GetImpactInput(gitRef, symbolIdentifier, maxResults, depth, detailLevel, maxChangedSymbols, maxTestsPerSymbol),
                            ct)),
                        ct)),
            McpToolRegistrationOptions.ReadOnlyTool("get_impact", GetImpactDescription)));
    }

    private const string GetImpactDescription =
        "Wann nutzen: pruefen, was eine geplante oder bereits gemachte Aenderung betrifft. " +
        "Ohne Parameter: uncommittete lokale Aenderungen (Default). Sonst gitRef (Commit-Ref) " +
        "ODER symbolIdentifier angeben, nie beide — Identifikator-Format wie find_references. " +
        "detailLevel: 'callers' (Default) oder 'change-context'. change-context ist nur im " +
        "Git-Diff-Modus zulaessig und nie zusammen mit symbolIdentifier (fuer den Kontext eines " +
        "einzelnen Symbols get_feature_context nutzen) und liefert ein strukturiertes Objekt mit " +
        "geaenderten Dateien/Symbolen, Call-Sites, statisch zugeordneten Tests, diffbezogenen " +
        "Violations und empfohlenen dotnet test-Befehlen; maxChangedSymbols (Default 20, Cap 100) " +
        "kappt die geaenderten Symbole deterministisch VOR Call-Site-/Test-/Violation-Analyse, " +
        "maxTestsPerSymbol (Default 10, Cap 50) die Testmethoden je Symbol, maxResults kappet nur " +
        "die Text-Topliste. depth wirkt nur im Symbol-Branch und ist im gesamten Git-Branch " +
        "(callers UND change-context) wirkungslos; im Symbol-Branch liefert depth (Default 1, " +
        "hard cap 3) dieselbe structuredContent.callSites/completeness-Struktur wie " +
        "find_references; die Traversierung ist hart auf 200 besuchte Knoten begrenzt.";

    private static void AddGetTypeHierarchy(
        McpServerPrimitiveCollection<McpServerTool> tools,
        AnalysisToolRoute targetRoute)
    {
        tools.Add(McpServerTool.Create(
            async (string targetType, string targetPath, string? symbolIdentifier = null, int maxResults = GetTypeHierarchyTool.DefaultMaxResults, CancellationToken ct = default) =>
                await AnalysisToolCall.ExecuteRouted(
                    targetRoute,
                    new AnalysisToolCallRequest(
                        new AnalysisTargetRequest(targetType, targetPath),
                        new AnalysisToolDispatch(
                            ProjectCall: lease => GetTypeHierarchyTool.ExecuteAsync(lease.Server, symbolIdentifier, maxResults, ct),
                            AssemblySessionCall: lease => GetTypeHierarchyTool.ExecuteAsync(lease.Server, symbolIdentifier, maxResults, ct)),
                        ct)),
            McpToolRegistrationOptions.TargetedReadOnlyTool("get_type_hierarchy", GetTypeHierarchyDescription)));
    }

    private const string GetTypeHierarchyDescription =
        "Wann nutzen: Vererbungs-/Interface-Baum eines C#-Typs sehen (Basisklassen, " +
        "Interfaces, abgeleitete/implementierende Typen, heuristische DI-Registrierungen). " +
        "symbolIdentifier: \"T:Namespace.Klasse\" oder \"Datei.cs:10:5\" oder \"Datei.cs:10\" " +
        "(Zeile ohne Spalte, siehe find_references) oder \"Klasse\". " +
        "maxResults begrenzt die abgeleiteten/implementierenden Typen (Default 50).";

    private static void AddDependencyGraph(
        McpServerPrimitiveCollection<McpServerTool> tools,
        AnalysisToolRoute targetRoute)
    {
        tools.Add(McpServerTool.Create(
            async (string targetType, string targetPath, string? filePath = null, string? symbolIdentifier = null, string? direction = null,
                int depth = 1, int maxResults = 50, CancellationToken ct = default) =>
                await AnalysisToolCall.ExecuteRouted(
                    targetRoute,
                    new AnalysisToolCallRequest(
                        new AnalysisTargetRequest(targetType, targetPath),
                        new AnalysisToolDispatch(
                            ProjectCall: lease => DependencyGraphTool.ExecuteAsync(lease.Server, new DependencyGraphInput(filePath, symbolIdentifier, direction, depth, maxResults), ct),
                            AssemblySessionCall: lease => DependencyGraphTool.ExecuteAsync(lease.Server, new DependencyGraphInput(filePath, symbolIdentifier, direction, depth, maxResults), ct)),
                        ct)),
            McpToolRegistrationOptions.TargetedReadOnlyTool("dependency_graph", DependencyGraphDescription)));
    }

    private const string DependencyGraphDescription =
        "Wann nutzen: welche Dateien/Typen von einer Datei oder einem Typ abhaengen (echte " +
        "SemanticModel-Typreferenzen, nicht nur using-Direktiven) — beantwortet 'wer haengt von X " +
        "ab' direkt statt mehrerer find_references-Umwege. filePath (ganze Datei) ODER " +
        "symbolIdentifier (ein Typ, engerer Scope) angeben, nie beide — symbolIdentifier-Format wie " +
        "find_references. direction: \"incoming\", \"outgoing\" oder \"both\" (Default). depth " +
        "(Default 1, hard cap 3) traversiert transitiv auf Datei-Ebene, hart begrenzt auf 150 " +
        "besuchte Dateien. maxResults begrenzt die angezeigten Kanten (Default 50).";
}
