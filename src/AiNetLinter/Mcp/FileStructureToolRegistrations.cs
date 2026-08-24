#nullable enable

using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Projects;
using AiNetLinter.Mcp.Tools;
using AiNetLinter.Mcp.Tools.FileStructure;
using ModelContextProtocol.Server;

namespace AiNetLinter.Mcp;

/// <summary>
/// Registriert die dateistruktur-orientierten Tools (aktuell <c>get_file_skeleton</c>,
/// <c>get_index_scope</c>, <c>get_hotspots</c>) an der von <see cref="McpServerOptionsFactory"/>
/// aufgebauten Tool-Collection. Aus <see cref="McpServerOptionsFactory"/> ausgelagert, damit dessen
/// eigener <c>AIContextFootprint</c> (siehe <c> nicht mit jedem neu registrierten Tool waechst
/// JIT-Kontext). <c>get_violations</c>, <c>search_pattern</c> und <c>metrics_tree</c> sind in eine
/// eigene <see cref="AnalysisToolRegistrations"/>-Klasse ausgelagert, weil ihr <c>LinterEngine</c>-
/// bzw. Roslyn-Syntax-Pull-in den Footprint dieser Klasse ueber das 2500-Limit getrieben hat/haette.
/// Alle Lambdas sind projektgebunden: <c>projectRoot</c> ist Pflicht und adressiert den
/// Lease-geschuetzten Registry-Key (<see cref="ProjectToolCall"/>).
/// </summary>
internal static class FileStructureToolRegistrations
{
    /// <summary>
    /// Fuegt <paramref name="tools"/> die dateistruktur-orientierten Tools hinzu. Tools erreichen die
    /// residente Instanz ihres Keys per Lease-Closure - kein DI-Container
    /// (siehe <c>AiNetLinterRichtlinien.mdc</c> §2).
    /// </summary>
    internal static void Register(
        McpServerPrimitiveCollection<McpServerTool> tools,
        ProjectRegistry registry)
    {
        AddGetNamespaceTree(tools, registry);
        AddGetFileSkeleton(tools, registry);
        AddGetClassStructure(tools, registry);
        AddGetIndexScope(tools, registry);
        AddGetHotspots(tools, registry);
    }

    private static void AddGetNamespaceTree(
        McpServerPrimitiveCollection<McpServerTool> tools,
        ProjectRegistry registry)
    {
        tools.Add(McpServerTool.Create(
            async (string projectRoot, string? project = null, string? namespacePrefix = null,
                int depth = GetNamespaceTreeTool.DefaultDepth,
                bool includeTypes = true,
                string? kind = "all",
                int maxResults = GetNamespaceTreeTool.DefaultMaxResults,
                CancellationToken ct = default) =>
                await ProjectToolCall.ExecuteAsync(
                    registry,
                    projectRoot,
                    lease =>
                    {
                        var input = new GetNamespaceTreeInput(project, namespacePrefix, depth, includeTypes, kind, maxResults);
                        return GetNamespaceTreeTool.ExecuteAsync(lease.Server, input, ct);
                    }),
            new McpServerToolCreateOptions
            {
                Name = "get_namespace_tree",
                Description = GetNamespaceTreeDescription,
            }));
    }

    private static readonly string GetNamespaceTreeDescription =
        "Wann nutzen: hierarchische semantische Exploration einer C#-Codebase (Solution -> Projekte " +
        "-> Namespaces -> Typen) nach dem Progressive-Disclosure-Prinzip. Ohne Parameter: Projekt-" +
        "Uebersicht. project: Namespaces eines Projekts. namespacePrefix: Einstiegspunkt fuer " +
        "Drilldown. depth: 1-3 Namespace-Ebenen. includeTypes: Typen ausgeben oder nur Sub-Namespaces. " +
        "kind: class/interface/record/struct/enum/all. maxResults: Obergrenze (Default 50, Cap 200).";

    private static void AddGetClassStructure(
        McpServerPrimitiveCollection<McpServerTool> tools,
        ProjectRegistry registry)
    {
        tools.Add(McpServerTool.Create(
            async (string projectRoot, string? symbolIdentifier = null, string? sortBy = "lines",
                int maxMembers = GetClassStructureTool.DefaultMaxMembers,
                CancellationToken ct = default) =>
                await ProjectToolCall.ExecuteAsync(
                    registry,
                    projectRoot,
                    lease => GetClassStructureTool.ExecuteAsync(lease.Server, symbolIdentifier, sortBy, maxMembers, ct)),
            new McpServerToolCreateOptions
            {
                Name = "get_class_structure",
                Description = GetClassStructureDescription,
            }));
    }

    private static readonly string GetClassStructureDescription =
        "Wann nutzen: Tabellarische Uebersicht ueber alle Member einer Klasse/eines Typs inkl. " +
        "Kind, Name, Visibility, Start-/End-Zeile, Zeilenanzahl und Signatur (z. B. zur Analyse " +
        "vor Refactorings oder zur Identifikation langer Member). symbolIdentifier (Pflicht): " +
        "Typname, File:Line:Col oder DocCommentId. sortBy: 'lines' (Default), 'kind', 'name'. " +
        "maxMembers: Token-Budget-Limit (Default 50, Cap " +
        + GetClassStructureTool.MaxMembersCap + "); bei Ueberschreitung Truncation-Meta-Zeile " +
        "und TotalMemberCount vs. ShownMemberCount im StructuredContent.";

    private static void AddGetFileSkeleton(
        McpServerPrimitiveCollection<McpServerTool> tools,
        ProjectRegistry registry)
    {
        tools.Add(McpServerTool.Create(
            async (string projectRoot, string[]? filePaths = null, CancellationToken ct = default) =>
                await ProjectToolCall.ExecuteAsync(
                    registry,
                    projectRoot,
                    lease => GetFileSkeletonTool.ExecuteAsync(lease.Server, filePaths, ct)),
            new McpServerToolCreateOptions
            {
                Name = "get_file_skeleton",
                Description = GetFileSkeletonDescription,
            }));
    }

    private const string GetFileSkeletonDescription =
        "Wann nutzen: Ueberblick ueber Typen/Signaturen einer oder mehrerer C#-Dateien (Batch in 1 Turn) " +
        "ohne die Bodies zu lesen — jede Signatur traegt eine stabile id: fuer einen Folge-Call an get_symbol_body. " +
        "filePaths: Array von Dateipfaden (auch fuer genau eine Datei), relativ oder absolut.";

    private static void AddGetIndexScope(
        McpServerPrimitiveCollection<McpServerTool> tools,
        ProjectRegistry registry)
    {
        tools.Add(McpServerTool.Create(
            async (string projectRoot, CancellationToken ct = default) =>
                await ProjectToolCall.ExecuteAsync(
                    registry,
                    projectRoot,
                    lease => GetIndexScopeTool.ExecuteAsync(lease.Server, ct)),
            new McpServerToolCreateOptions
            {
                Name = "get_index_scope",
                Description = GetIndexScopeDescription,
            }));
    }

    private const string GetIndexScopeDescription =
        "Wann nutzen: als ersten Call vor find_symbol/search_pattern — Dateityp-" +
        "Aufschluesselung der Solution (.cs vom Symbolgraph abgedeckt, .css/.html/.js/.razor/" +
        ".xaml nicht, jeweils mit Anzahl).";

    private static void AddGetHotspots(
        McpServerPrimitiveCollection<McpServerTool> tools,
        ProjectRegistry registry)
    {
        tools.Add(McpServerTool.Create(
            async (string projectRoot, string? scopeFilter = null, CancellationToken ct = default) =>
                await ProjectToolCall.ExecuteAsync(
                    registry,
                    projectRoot,
                    lease => GetHotspotsTool.ExecuteAsync(lease.Server, scopeFilter, ct)),
            new McpServerToolCreateOptions
            {
                Name = "get_hotspots",
                Description = GetHotspotsDescription,
            }));
    }

    private const string GetHotspotsDescription =
        "Wann nutzen: vor einem geplanten Edit pruefen, ob eine Datei/ein Projekt sich dem " +
        "Zeilen-Limit (MaxLineCount) naehert. scopeFilter grenzt auf Projekt-Name oder " +
        "Pfad-Substring ein.";
}
