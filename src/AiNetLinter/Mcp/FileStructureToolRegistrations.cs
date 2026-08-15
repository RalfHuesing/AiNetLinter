#nullable enable

using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Tools;
using AiNetLinter.Mcp.Tools.FileStructure;
using ModelContextProtocol.Protocol;
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
/// </summary>
internal static class FileStructureToolRegistrations
{
    /// <summary>
    /// Fuegt <paramref name="tools"/> die dateistruktur-orientierten Tools hinzu. Tools erreichen den
    /// resident gehaltenen <paramref name="mcpState"/> per Delegate-Closure - kein DI-Container
    /// (siehe <c>. Optionaler <paramref name="callLog"/> zeichnet jeden Tool-Aufruf auf, wenn
    /// aktiv (kein Overhead bei deaktiviertem Log).
    /// </summary>
    internal static void Register(
        McpServerPrimitiveCollection<McpServerTool> tools,
        McpCodeGraphServer mcpState,
        McpCallLog? callLog = null)
    {
        AddGetFileSkeleton(tools, mcpState, callLog);
        AddGetClassStructure(tools, mcpState, callLog);
        AddGetIndexScope(tools, mcpState, callLog);
        AddGetHotspots(tools, mcpState, callLog);
    }

    private static void AddGetClassStructure(
        McpServerPrimitiveCollection<McpServerTool> tools,
        McpCodeGraphServer mcpState,
        McpCallLog? callLog)
    {
        tools.Add(McpServerTool.Create(
            async (string? symbol = null, string? sortBy = "lines", CancellationToken ct = default) =>
            {
                if (callLog is null)
                {
                    return await GetClassStructureTool.ExecuteAsync(mcpState, symbol, sortBy, ct);
                }
                return await callLog.ExecuteCallAsync("get_class_structure", $"{symbol}|{sortBy}",
                    () => GetClassStructureTool.ExecuteAsync(mcpState, symbol, sortBy, ct));
            },
            new McpServerToolCreateOptions
            {
                Name = "get_class_structure",
                Description = GetClassStructureDescription,
            }));
    }

    private const string GetClassStructureDescription =
        "Wann nutzen: Tabellarische Uebersicht ueber alle Member einer Klasse/eines Typs inkl. " +
        "Kind, Name, Visibility, Start-/End-Zeile, Zeilenanzahl und Signatur (z. B. zur Analyse " +
        "vor Refactorings oder zur Identifikation langer Member). symbol (Pflicht): Typname, " +
        "File:Line:Col oder DocCommentId. sortBy: 'lines' (Default), 'kind', 'name'.";

    private static void AddGetFileSkeleton(
        McpServerPrimitiveCollection<McpServerTool> tools,
        McpCodeGraphServer mcpState,
        McpCallLog? callLog)
    {
        tools.Add(McpServerTool.Create(
            async (string? filePath = null, CancellationToken ct = default) =>
            {
                if (callLog is null)
                {
                    return await GetFileSkeletonTool.ExecuteAsync(mcpState, filePath, ct);
                }
                return await callLog.ExecuteCallAsync("get_file_skeleton", filePath ?? "",
                    () => GetFileSkeletonTool.ExecuteAsync(mcpState, filePath, ct));
            },
            new McpServerToolCreateOptions
            {
                Name = "get_file_skeleton",
                Description = GetFileSkeletonDescription,
            }));
    }

    private const string GetFileSkeletonDescription =
        "Wann nutzen: Ueberblick ueber Typen/Signaturen einer C#-Datei ohne die Bodies zu " +
        "lesen — jede Signatur traegt eine stabile id: fuer einen Folge-Call an get_symbol_body.";

    private static void AddGetIndexScope(
        McpServerPrimitiveCollection<McpServerTool> tools,
        McpCodeGraphServer mcpState,
        McpCallLog? callLog)
    {
        tools.Add(McpServerTool.Create(
            async (CancellationToken ct = default) =>
            {
                if (callLog is null)
                {
                    return await GetIndexScopeTool.ExecuteAsync(mcpState, ct);
                }
                return await callLog.ExecuteCallAsync("get_index_scope", "",
                    () => GetIndexScopeTool.ExecuteAsync(mcpState, ct));
            },
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
        McpCodeGraphServer mcpState,
        McpCallLog? callLog)
    {
        tools.Add(McpServerTool.Create(
            async (string? scopeFilter = null, CancellationToken ct = default) =>
            {
                if (callLog is null)
                {
                    return await GetHotspotsTool.ExecuteAsync(mcpState, scopeFilter, ct);
                }
                return await callLog.ExecuteCallAsync("get_hotspots", scopeFilter ?? "",
                    () => GetHotspotsTool.ExecuteAsync(mcpState, scopeFilter, ct));
            },
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
