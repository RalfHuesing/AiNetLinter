#nullable enable

using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Tools;
using AiNetLinter.Mcp.Tools.CallTree;
using AiNetLinter.Mcp.Tools.DependencyGraph;
using AiNetLinter.Mcp.Tools.SymbolGraph;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace AiNetLinter.Mcp;

/// <summary>
/// Registriert die sechs reinen Symbolgraph-Tools (<c>find_symbol</c>, <c>find_references</c>,
/// <c>get_impact</c>, <c>get_type_hierarchy</c>, <c>get_call_tree</c>, <c>dependency_graph</c>) an
/// der von <see cref="McpServerOptionsFactory"/> aufgebauten Tool-Collection. Aus
/// <see cref="McpServerOptionsFactory"/> ausgelagert, damit dessen eigener <c>AIContextFootprint</c>
/// (siehe <c>AiNetLinter.mdc</c>) nicht mit jedem neu registrierten Tool waechst.
/// </summary>
internal static class SymbolGraphToolRegistrations
{
    /// <summary>
    /// Fuegt <paramref name="tools"/> die sechs Symbolgraph-Tools hinzu. Tools erreichen den
    /// resident gehaltenen <paramref name="mcpState"/> per Delegate-Closure - kein DI-Container
    /// (siehe <c>AiNetLinterRichtlinien.mdc</c> §2).
    /// </summary>
    internal static void Register(
        McpServerPrimitiveCollection<McpServerTool> tools,
        McpCodeGraphServer mcpState)
    {
        AddFindSymbol(tools, mcpState);
        AddFindReferences(tools, mcpState);
        AddGetCallTree(tools, mcpState);
        AddGetImpact(tools, mcpState);
        AddGetTypeHierarchy(tools, mcpState);
        AddDependencyGraph(tools, mcpState);
    }

    private static void AddFindSymbol(
        McpServerPrimitiveCollection<McpServerTool> tools,
        McpCodeGraphServer mcpState)
    {
        tools.Add(McpServerTool.Create(
            (string? namePattern = null, string? kind = null, int maxResults = 50, CancellationToken ct = default) =>
                FindSymbolTool.ExecuteAsync(mcpState, namePattern, kind, maxResults, ct),
            new McpServerToolCreateOptions
            {
                Name = "find_symbol",
                Description = FindSymbolDescription,
            }));
    }

    private const string FindSymbolDescription =
        "Wann nutzen: Fundstelle(n) eines C#-Symbols per Namens-Substring finden, wenn der " +
        "exakte Ort unbekannt ist. Beispiel: namePattern: \"Greeter\", kind: \"Class\". Bei 0 " +
        "C#-Treffern Hinweis auf Textfunde in Nicht-C#-Dateien.";

    private static void AddFindReferences(
        McpServerPrimitiveCollection<McpServerTool> tools,
        McpCodeGraphServer mcpState)
    {
        tools.Add(McpServerTool.Create(
            (string? symbolIdentifier = null, int maxResults = 50, int depth = 1, CancellationToken ct = default) =>
                FindReferencesTool.ExecuteAsync(mcpState, symbolIdentifier, maxResults, depth, ct),
            new McpServerToolCreateOptions
            {
                Name = "find_references",
                Description = FindReferencesDescription,
            }));
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
        McpCodeGraphServer mcpState)
    {
        tools.Add(McpServerTool.Create(
            (string? symbolIdentifier = null, int depth = 2, string? format = null, int topN = 10, string? direction = null, CancellationToken ct = default) =>
                GetCallTreeTool.ExecuteAsync(mcpState, new GetCallTreeInput(symbolIdentifier, depth, format, topN, direction), ct),
            new McpServerToolCreateOptions
            {
                Name = "get_call_tree",
                Description = GetCallTreeDescription,
            }));
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
        McpCodeGraphServer mcpState)
    {
        tools.Add(McpServerTool.Create(
            (string? gitRef = null, string? symbolIdentifier = null, int maxResults = 50, int depth = 1, CancellationToken ct = default) =>
                GetImpactTool.ExecuteAsync(mcpState, new GetImpactInput(gitRef, symbolIdentifier, maxResults, depth), ct),
            new McpServerToolCreateOptions
            {
                Name = "get_impact",
                Description = GetImpactDescription,
            }));
    }

    private const string GetImpactDescription =
        "Wann nutzen: pruefen, was eine geplante oder bereits gemachte Aenderung betrifft. " +
        "Ohne Parameter: uncommittete lokale Aenderungen (Default). Sonst gitRef (Commit-Ref) " +
        "ODER symbolIdentifier angeben, nie beide — Identifikator-Format wie find_references. " +
        "depth (hard cap 3) wirkt nur im Symbol-Branch und liefert dort dieselbe " +
        "structuredContent.callSites/completeness-Struktur wie find_references; die " +
        "Traversierung ist hart auf 200 besuchte Knoten begrenzt.";

    private static void AddGetTypeHierarchy(
        McpServerPrimitiveCollection<McpServerTool> tools,
        McpCodeGraphServer mcpState)
    {
        tools.Add(McpServerTool.Create(
            (string? symbolIdentifier = null, int maxResults = GetTypeHierarchyTool.DefaultMaxResults, CancellationToken ct = default) =>
                GetTypeHierarchyTool.ExecuteAsync(mcpState, symbolIdentifier, maxResults, ct),
            new McpServerToolCreateOptions
            {
                Name = "get_type_hierarchy",
                Description = GetTypeHierarchyDescription,
            }));
    }

    private const string GetTypeHierarchyDescription =
        "Wann nutzen: Vererbungs-/Interface-Baum eines C#-Typs sehen (Basisklassen, " +
        "Interfaces, abgeleitete/implementierende Typen, heuristische DI-Registrierungen). " +
        "symbolIdentifier: \"T:Namespace.Klasse\" oder \"Datei.cs:10:5\" oder \"Datei.cs:10\" " +
        "(Zeile ohne Spalte, siehe find_references) oder \"Klasse\". " +
        "maxResults begrenzt die abgeleiteten/implementierenden Typen (Default 50).";

    private static void AddDependencyGraph(
        McpServerPrimitiveCollection<McpServerTool> tools,
        McpCodeGraphServer mcpState)
    {
        tools.Add(McpServerTool.Create(
            (string? filePath = null, string? symbolIdentifier = null, string? direction = null,
                int depth = 1, int maxResults = 50, CancellationToken ct = default) =>
                DependencyGraphTool.ExecuteAsync(mcpState, new DependencyGraphInput(filePath, symbolIdentifier, direction, depth, maxResults), ct),
            new McpServerToolCreateOptions
            {
                Name = "dependency_graph",
                Description = DependencyGraphDescription,
            }));
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
