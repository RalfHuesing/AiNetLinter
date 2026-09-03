#nullable enable

using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Projects;
using AiNetLinter.Mcp.Tools;
using AiNetLinter.Mcp.Tools.FileStructure;
using ModelContextProtocol.Server;

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
        AddGetFileTree(tools);
        AddGetFileSkeleton(tools, registry, targetRoute);
        AddGetClassStructure(tools, registry, targetRoute);
        AddGetIndexScope(tools, registry);
        AddGetHotspots(tools, registry);
    }

    private static void AddGetFileTree(
        McpServerPrimitiveCollection<McpServerTool> tools)
    {
        tools.Add(McpServerTool.Create(
            async (
                string targetType,
                string targetPath,
                string? root = null,
                string view = "tree",
                string[]? includeExtensions = null,
                string? fileFilter = null,
                string[]? excludePatterns = null,
                int? maxDepth = null,
                int treeDepth = 2,
                int maxResults = GetFileTreeTool.DefaultMaxResults,
                string sortBy = "path",
                bool includeMetadata = true,
                bool includeLineCount = false,
                CancellationToken ct = default) =>
                await ProjectAnalysisDispatcher.ExecutePhysicalFilesystemAsync(
                    new AnalysisTargetRequest(targetType, targetPath),
                    canonicalRoot => GetFileTreeTool.ExecuteAsync(
                        canonicalRoot,
                        new GetFileTreeInput(
                            root ?? ".",
                            view,
                            includeExtensions,
                            fileFilter,
                            excludePatterns,
                            maxDepth,
                            treeDepth,
                            maxResults,
                            sortBy,
                            includeMetadata,
                            includeLineCount),
                         ct)),
            McpToolRegistrationOptions.ReadOnlyTool("get_file_tree", GetFileTreeDescription)));
    }

    private const string GetFileTreeDescription =
        "Wann nutzen: physische Dateilandkarte eines absoluten Projekt- oder dekompilierten " +
        "SourceRoots als ersten Discovery-Schritt fuer Agenten. root, fileFilter und " +
        "excludePatterns sind relativ zu " +
        "targetPath; fileFilter ist ein Pfad-Glob, keine Inhaltssuche. view: 'tree' [Default], " +
        "'summary', 'files'. includeExtensions: Extensionen wie ['.cs'] oder ['*']. " +
        "maxDepth und treeDepth: 0 bis 32 (effektive Tiefe = maxDepth ?? treeDepth, 0 = Root-Ebene, Default treeDepth 2; maxDepth hat Vorrang). " +
        "maxResults: Begrenzung (Default 200, Maximum 2000). " +
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
                                ct)),
                        ct)),
            McpToolRegistrationOptions.TargetedReadOnlyTool("get_namespace_tree", GetNamespaceTreeDescription)));
    }

    private static readonly string GetNamespaceTreeDescription =
        "Wann nutzen: hierarchische semantische Exploration einer C#-Codebase (Solution -> Projekte " +
        "-> Namespaces -> Typen) nach dem Progressive-Disclosure-Prinzip. Ohne Parameter: Projekt-" +
        "Uebersicht. project: Namespaces eines Projekts filtern. namespacePrefix: Einstiegspunkt fuer " +
        "Drilldown. depth: 1-3 Namespace-Ebenen (Default 1). includeTypes: Typen ausgeben (Default true) " +
        "oder nur Sub-Namespaces. kind: class/interface/record/struct/enum/all (Default all). " +
        "maxResults: Obergrenze der Eintraege (Default 50, Cap 200).";

    private static void AddGetClassStructure(
        McpServerPrimitiveCollection<McpServerTool> tools,
        ProjectRegistry registry,
        AnalysisToolRoute? targetRoute)
    {
        tools.Add(McpServerTool.Create(
            async (string targetType, string targetPath, string? symbolIdentifier = null, string? sortBy = "lines",
                int maxMembers = GetClassStructureTool.DefaultMaxMembers,
                string? kindFilter = null,
                string? nameFilter = null,
                CancellationToken ct = default) =>
                await AnalysisToolCall.ExecuteRouted(
                    targetRoute!,
                    new AnalysisToolCallRequest(
                        new AnalysisTargetRequest(targetType, targetPath),
                        new AnalysisToolDispatch(
                            ProjectCall: lease => GetClassStructureTool.ExecuteAsync(lease.Server, new GetClassStructureArgs(symbolIdentifier, sortBy, maxMembers, kindFilter, nameFilter), ct),
                            AssemblySessionCall: lease => GetClassStructureTool.ExecuteAsync(lease.Server, new GetClassStructureArgs(symbolIdentifier, sortBy, maxMembers, kindFilter, nameFilter), ct)),
                        ct)),
            McpToolRegistrationOptions.TargetedReadOnlyTool("get_class_structure", GetClassStructureDescription)));
    }

    private static readonly string GetClassStructureDescription =
        "Wann nutzen: Tabellarische Uebersicht ueber alle Member einer Klasse/eines Typs inkl. " +
        "Kind, Name, Visibility, Start-/End-Zeile, Zeilenanzahl und Signatur (z. B. zur Analyse " +
        "vor Refactorings oder zur Identifikation langer Member; bei Records inkl. Primary-Constructor-Parametern). " +
        "symbolIdentifier (Pflicht): Typname, Datei.cs:Zeile:Spalte oder DocCommentId. " +
        "sortBy: 'lines' (Default), 'kind', 'name'. kindFilter: optionaler Filter nach Member-Kind (z. B. Method, Property, Field, Constructor, all). " +
        "nameFilter: optionaler Substring-Filter nach Member-Namen. maxMembers: Begrenzung der sichtbaren Member " +
        "(Default 50, Cap " + GetClassStructureTool.MaxMembersCap + "); bei Ueberschreitung " +
        "Truncation-Meta-Zeile und TotalMemberCount vs. ShownMemberCount im structuredContent.";

    private static void AddGetFileSkeleton(
        McpServerPrimitiveCollection<McpServerTool> tools,
        ProjectRegistry registry,
        AnalysisToolRoute? targetRoute)
    {
        tools.Add(McpServerTool.Create(
            async (string targetType, string targetPath, string[]? filePaths = null, string? filePath = null, CancellationToken ct = default) =>
                await AnalysisToolCall.ExecuteRouted(
                    targetRoute!,
                    new AnalysisToolCallRequest(
                        new AnalysisTargetRequest(targetType, targetPath),
                        new AnalysisToolDispatch(
                            ProjectCall: lease => GetFileSkeletonTool.ExecuteAsync(lease.Server, ResolveFilePaths(filePaths, filePath), ct),
                            AssemblySessionCall: lease => GetFileSkeletonTool.ExecuteAsync(lease.Server, ResolveFilePaths(filePaths, filePath), ct)),
                        ct)),
            McpToolRegistrationOptions.TargetedReadOnlyTool("get_file_skeleton", GetFileSkeletonDescription)));
    }

    private const string GetFileSkeletonDescription =
        "Wann nutzen: Ueberblick ueber Typen und Signaturen einer oder mehrerer C#-Dateien (Batch in 1 Turn) " +
        "ohne die Bodies zu lesen — jede Signatur traegt eine stabile id: fuer einen Folge-Call an get_symbol_body. " +
        "filePaths: Array von Dateipfaden (auch fuer genau eine Datei), relativ oder absolut; " +
        "filePath: String-Alias fuer genau eine Datei, wenn kein filePaths-Array uebergeben wird.";

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
