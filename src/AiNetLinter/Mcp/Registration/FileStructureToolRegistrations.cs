#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Projects;
using AiNetLinter.Mcp.Tools;
using AiNetLinter.Mcp.Tools.Common;
using AiNetLinter.Mcp.Tools.FileStructure;
using ModelContextProtocol.Server;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Registration;

/// <summary>
/// Registriert die dateistruktur-orientierten Tools (aktuell <c>get_file_tree</c>,
/// <c>get_file_skeleton</c>, <c>get_index_scope</c>, <c>get_hotspots</c>) an der von <see cref="McpServerOptionsFactory"/>
/// aufgebauten Tool-Collection. Aus <see cref="McpServerOptionsFactory"/> ausgelagert, damit dessen
/// eigener <c>AIContextFootprint</c> nicht mit jedem neu registrierten Tool waechst.
/// <c>get_violations</c>, <c>search_pattern</c> und <c>metrics_tree</c> sind in eine
/// eigene <see cref="AnalysisToolRegistrations"/>-Klasse ausgelagert, weil ihr <c>LinterEngine</c>-
/// bzw. Roslyn-Syntax-Pull-in den Footprint dieser Klasse ueber das 2500-Limit getrieben hat/haette.
/// Alle Lambdas sind zielgebunden: <c>targetType</c> und <c>targetPath</c> sind Pflicht und
/// werden am gemeinsamen <see cref="AnalysisToolCall"/> validiert.
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
        ProjectRegistry registry,
        AnalysisToolRoute? targetRoute = null)
    {
        AddGetNamespaceTree(tools, registry, targetRoute);
        AddGetFileTree(tools, targetRoute);
        AddGetFileSkeleton(tools, registry, targetRoute);
        AddGetClassStructure(tools, registry, targetRoute);
        AddGetIndexScope(tools, registry);
        AddGetHotspots(tools, registry);
    }

    private static void AddGetFileTree(
        McpServerPrimitiveCollection<McpServerTool> tools,
        AnalysisToolRoute? targetRoute)
    {
        tools.Add(McpServerTool.Create(
            async (
                string targetType,
                string targetPath,
                string? root = null,
                string? path = null,
                string? directory = null,
                string view = "tree",
                string[]? includeExtensions = null,
                string? fileFilter = null,
                string? filter = null,
                string? pattern = null,
                string[]? excludePatterns = null,
                int? maxDepth = null,
                int? treeDepth = null,
                int maxResults = GetFileTreeTool.DefaultMaxResults,
                string sortBy = "path",
                bool includeMetadata = true,
                bool includeLineCount = false,
                CancellationToken ct = default) =>
            {
                var rawRoot = root ?? path ?? directory ?? ".";
                var effectiveRoot = McpInputNormalizer.NormalizePathOrScope(rawRoot, targetPath);
                var effectiveFilter = fileFilter ?? filter ?? pattern;
                return await ExecuteFileTreeAsync(
                    targetRoute,
                    targetType,
                    targetPath,
                    new GetFileTreeInput(effectiveRoot, view, includeExtensions, effectiveFilter, excludePatterns, maxDepth, treeDepth, maxResults, sortBy, includeMetadata, includeLineCount),
                    ct);
            },
            McpToolRegistrationOptions.TargetedReadOnlyTool("get_file_tree", GetFileTreeDescription)));
    }

    private static Task<CallToolResult> ExecuteFileTreeAsync(
        AnalysisToolRoute? targetRoute,
        string targetType,
        string targetPath,
        GetFileTreeInput input,
        CancellationToken cancellationToken) =>
        string.Equals(targetType, "assembly", StringComparison.OrdinalIgnoreCase)
            ? AnalysisToolCall.ExecuteRouted(
                targetRoute!,
                new AnalysisToolCallRequest(
                    new AnalysisTargetRequest(targetType, targetPath),
                    new AnalysisToolDispatch(
                        AssemblySessionCall: lease => AssemblyGetFileTreeTool.ExecuteAsync(lease, input, cancellationToken)),
                    cancellationToken))
            : ProjectAnalysisDispatcher.ExecutePhysicalFilesystemAsync(
                new AnalysisTargetRequest(targetType, targetPath),
                canonicalRoot => GetFileTreeTool.ExecuteAsync(canonicalRoot, input, cancellationToken));


    private const string GetFileTreeDescription =
        "Wann nutzen: physische Dateilandkarte eines absoluten Projekt- oder dekompilierten " +
        "SourceRoots als ersten Discovery-Schritt fuer Agenten. root, fileFilter und " +
        "excludePatterns sind relativ zu " +
        "targetPath; fileFilter ist ein Pfad-Glob, keine Inhaltssuche. view: 'tree' [Default], " +
        "'summary', 'files'. includeExtensions: Extensionen wie ['.cs'] oder ['*']. " +
        "maxDepth und treeDepth: 0 bis 32 (effektive Tiefe = maxDepth ?? treeDepth; bei aktivem fileFilter, gezieltem Unterverzeichnis-Root oder view='summary' wird standardmaessig bis zum Limit gescannt, wenn weder maxDepth noch treeDepth gesetzt sind; maxDepth hat Vorrang). " +
        "targetType='project' oder targetType='assembly'; maxResults: Begrenzung (Default 200, Maximum 2000). Für Assembly-Ziele wird der " +
        "vorhandene Source- oder dekompilierte SourceRoot verwendet; ohne solchen Root ist die " +
        "Capability unsupported. Snapshot/Generation bleiben im Assembly-Response-Envelope sichtbar. " +
        "sortBy: 'path' [Default], 'size_desc', 'extension'. includeMetadata: Dateigroessen (Default true), " +
        "includeLineCount: Zeilenzaehlung (Default false). structuredContent liegt unter fileTree.";

    private static void AddGetNamespaceTree(
        McpServerPrimitiveCollection<McpServerTool> tools,
        ProjectRegistry registry,
        AnalysisToolRoute? targetRoute)
    {
        tools.Add(McpServerTool.Create(
            async (string targetType, string targetPath, string? project = null, string? namespacePrefix = null,
                int depth = GetNamespaceTreeTool.DefaultDepth,
                bool includeTypes = true,
                string? kind = "all",
                int maxResults = GetNamespaceTreeTool.DefaultMaxResults,
                int maxResponseBytes = 0,
                CancellationToken ct = default) =>
                await AnalysisToolCall.ExecuteRouted(
                    targetRoute!,
                    new AnalysisToolCallRequest(
                        new AnalysisTargetRequest(targetType, targetPath),
                        new AnalysisToolDispatch(
                            ProjectCall: lease => GetNamespaceTreeTool.ExecuteAsync(
                                lease.Server,
                                new GetNamespaceTreeInput(project, namespacePrefix, depth, includeTypes, kind, maxResults),
                                ct),
                            AssemblySessionCall: lease => GetNamespaceTreeTool.ExecuteAsync(
                                lease.Server,
                                new GetNamespaceTreeInput(project, namespacePrefix, depth, includeTypes, kind, maxResults),
                                ct),
                            MaxResponseBytes: maxResponseBytes),
                        ct)),
            McpToolRegistrationOptions.TargetedReadOnlyTool("get_namespace_tree", GetNamespaceTreeDescription)));
    }

    private static readonly string GetNamespaceTreeDescription =
        "Wann nutzen: hierarchische semantische Exploration einer C#-Codebase (Solution -> Projekte " +
        "-> Namespaces -> Typen) nach dem Progressive-Disclosure-Prinzip. Ohne Parameter: Projekt-" +
        "Uebersicht. project: Namespaces eines Projekts filtern. namespacePrefix: Einstiegspunkt fuer " +
        "Drilldown. depth: 1-3 Namespace-Ebenen (Default 1). includeTypes: Typen ausgeben (Default true) " +
        "oder nur Sub-Namespaces. kind: class/interface/record/struct/enum/all (Default all). " +
        "maxResults: Obergrenze der Eintraege (Default 50, Cap 200). " +
        "maxResponseBytes: Begrenzung des Antwortbudgets (Default 0 = Standardbudget).";

    private static void AddGetClassStructure(
        McpServerPrimitiveCollection<McpServerTool> tools,
        ProjectRegistry registry,
        AnalysisToolRoute? targetRoute)
    {
        tools.Add(McpServerTool.Create(
            async (string targetType, string targetPath, string? symbolIdentifier = null, string? symbol = null, string? className = null, string? identifier = null, string? type = null, string? name = null, string? sortBy = "lines",
                int maxMembers = GetClassStructureTool.DefaultMaxMembers,
                string? kindFilter = null,
                string? nameFilter = null,
                int maxResponseBytes = 0,
                CancellationToken ct = default) =>
            {
                var effectiveIdentifier = symbolIdentifier ?? symbol ?? className ?? identifier ?? type ?? name;
                return await AnalysisToolCall.ExecuteRouted(
                    targetRoute!,
                    new AnalysisToolCallRequest(
                        new AnalysisTargetRequest(targetType, targetPath),
                        new AnalysisToolDispatch(
                            ProjectCall: lease => GetClassStructureTool.ExecuteAsync(lease.Server, new GetClassStructureArgs(effectiveIdentifier, sortBy, maxMembers, kindFilter, nameFilter, symbol), ct),
                            AssemblySessionCall: lease => GetClassStructureTool.ExecuteAsync(lease.Server, new GetClassStructureArgs(effectiveIdentifier, sortBy, maxMembers, kindFilter, nameFilter, symbol), ct),
                            MaxResponseBytes: maxResponseBytes),
                        ct));
            },
            McpToolRegistrationOptions.TargetedReadOnlyTool("get_class_structure", GetClassStructureDescription)));
    }

    private static readonly string GetClassStructureDescription =
        "Wann nutzen: Tabellarische Uebersicht ueber alle Member einer Klasse/eines Typs inkl. " +
        "Kind, Name, Visibility, Start-/End-Zeile, Zeilenanzahl und Signatur (z. B. zur Analyse " +
        "vor Refactorings oder zur Identifikation langer Member; bei Records inkl. Primary-Constructor-Parametern). " +
        "symbolIdentifier (Pflicht, oder Aliase symbol, className, identifier, type, name): Typname, Datei.cs:Zeile:Spalte oder DocCommentId. " +
        "sortBy: 'lines' (Default), 'kind', 'name'. kindFilter: optionaler Filter nach Member-Kind (z. B. Method, Property, Field, Constructor, all). " +
        "nameFilter: optionaler Substring-Filter nach Member-Namen. maxMembers: Begrenzung der sichtbaren Member " +
        "(Default 50, Cap " + GetClassStructureTool.MaxMembersCap + "); bei Ueberschreitung " +
        "Truncation-Meta-Zeile und TotalMemberCount vs. ShownMemberCount im structuredContent. " +
        "maxResponseBytes: Begrenzung des Antwortbudgets (Default 0 = Standardbudget).";

    private static void AddGetFileSkeleton(
        McpServerPrimitiveCollection<McpServerTool> tools,
        ProjectRegistry registry,
        AnalysisToolRoute? targetRoute)
    {
        tools.Add(McpServerTool.Create(
            async (string targetType, string targetPath, string[]? filePaths = null, string? filePath = null, string? path = null, string? file = null, int maxResponseBytes = 0, CancellationToken ct = default) =>
            {
                var effectivePath = filePath ?? path ?? file;
                return await AnalysisToolCall.ExecuteRouted(
                    targetRoute!,
                    new AnalysisToolCallRequest(
                        new AnalysisTargetRequest(targetType, targetPath),
                        new AnalysisToolDispatch(
                            ProjectCall: lease => GetFileSkeletonTool.ExecuteAsync(lease.Server, ResolveFilePaths(filePaths, effectivePath), ct),
                            AssemblySessionCall: lease => GetFileSkeletonTool.ExecuteAsync(lease.Server, ResolveFilePaths(filePaths, effectivePath), ct),
                            MaxResponseBytes: maxResponseBytes),
                        ct));
            },
            McpToolRegistrationOptions.TargetedReadOnlyTool("get_file_skeleton", GetFileSkeletonDescription)));
    }

    private const string GetFileSkeletonDescription =
        "Wann nutzen: Ueberblick ueber Typen und Signaturen einer oder mehrerer C#-Dateien (Batch in 1 Turn) " +
        "ohne die Bodies zu lesen — jede Signatur traegt eine stabile id: fuer einen Folge-Call an get_symbol_body. " +
        "filePaths: Array von Dateipfaden (auch fuer genau eine Datei), relativ oder absolut; " +
        "filePath (oder Aliase path, file): String-Alias fuer genau eine Datei, wenn kein filePaths-Array uebergeben wird. " +
        "maxResponseBytes: Begrenzung des Antwortbudgets (Default 0 = Standardbudget).";

    private static string[]? ResolveFilePaths(string[]? filePaths, string? filePath) =>
        filePaths is { Length: > 0 }
            ? filePaths
            : string.IsNullOrWhiteSpace(filePath)
                ? filePaths
                : [filePath];

    private static void AddGetIndexScope(
        McpServerPrimitiveCollection<McpServerTool> tools,
        ProjectRegistry registry)
    {
        tools.Add(McpServerTool.Create(
            async (string targetType, string targetPath, CancellationToken ct = default) =>
                await ProjectAnalysisDispatcher.ExecuteAsync(
                    registry,
                    targetType,
                    targetPath,
                    lease => GetIndexScopeTool.ExecuteAsync(lease.Server, ct)),
            McpToolRegistrationOptions.ReadOnlyTool("get_index_scope", GetIndexScopeDescription)));
    }

    private const string GetIndexScopeDescription =
        "Wann nutzen: als ersten Discovery-Call vor find_symbol/search_pattern — Dateityp-" +
        "Aufschluesselung der Solution (.cs vom Symbolgraph abgedeckt, .css/.html/.js/.razor/" +
        ".xaml nicht abgedeckt, jeweils mit Dateianzahl).";

    private static void AddGetHotspots(
        McpServerPrimitiveCollection<McpServerTool> tools,
        ProjectRegistry registry)
    {
        tools.Add(McpServerTool.Create(
            async (
                string targetType,
                string targetPath,
                string? scopeFilter = null,
                int maxResults = GetHotspotsScanner.DefaultMaxResults,
                double minLinePercentage = GetHotspotsScanner.DefaultMinLinePercentage,
                string? scopeType = GetHotspotsScanner.DefaultScopeType,
                CancellationToken ct = default) =>
                await ProjectAnalysisDispatcher.ExecuteAsync(
                    registry,
                    targetType,
                    targetPath,
                    lease => GetHotspotsTool.ExecuteAsync(
                        new GetHotspotsRequest(
                            lease.Server,
                            scopeFilter,
                            maxResults,
                            minLinePercentage,
                            scopeType,
                            ct))),
            McpToolRegistrationOptions.ReadOnlyTool("get_hotspots", GetHotspotsDescription)));
    }

    private const string GetHotspotsDescription =
        "Wann nutzen: vor einem geplanten Edit pruefen, ob eine Datei/ein Projekt sich dem " +
        "Zeilen-Limit (MaxLineCount) naehert. scopeFilter: Projekt-Name oder Pfad-Substring zur Eingrenzung. " +
        "scopeType: 'production' [Default], 'tests' oder 'all' zur Auswahl von Produktions- bzw. Testdateien. " +
        "maxResults: sichtbare Hotspots (Default 50, Cap 200). minLinePercentage: untere " +
        "Auslastungsschwelle in Prozent (Default 80, Bereich 0-100). Ergebnisse bleiben " +
        "deterministisch nach absteigender Zeilenzahl und Pfad sortiert; StructuredContent " +
        "weist Gesamtzahl, Anzeigezahl und Trunkierung aus.";
}
