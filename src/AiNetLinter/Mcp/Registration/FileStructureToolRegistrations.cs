#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Projects;
using AiNetLinter.Mcp.Scope;
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
/// Alle Lambdas sind zielgebunden: <c>targetPath</c> ist Pflicht und
/// werden am gemeinsamen <see cref="AnalysisToolCall"/> validiert.
/// </summary>
internal static class FileStructureToolRegistrations
{
    /// <summary>
    /// Fuegt <paramref name="tools"/> die dateistruktur-orientierten Tools hinzu. Tools erreichen die
    /// residente Instanz ihres Keys per Lease-Closure - kein DI-Container
    /// (siehe <c>AiNetLinter-Richtlinien.mdc</c> §2).
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
                RequestContext<CallToolRequestParams> context,
                string targetPath,
                string? root = null,
                string view = "tree",
                string[]? includeExtensions = null,
                string? fileFilter = null,
                string[]? excludePatterns = null,
                int? maxDepth = null,
                int? treeDepth = null,
                int maxResults = GetFileTreeTool.DefaultMaxResults,
                string sortBy = "path",
                bool includeMetadata = true,
                bool includeLineCount = false,
                int maxResponseBytes = GetFileTreeTool.DefaultMaxResponseBytes,
                CancellationToken ct = default) =>
            {
                var unknownError = TargetPathToolRegistrationOptions.RejectUnknownArguments(context);
                if (unknownError is not null) return unknownError;
                var rawRoot = root ?? ".";
                var effectiveRoot = McpInputNormalizer.NormalizePathOrScope(rawRoot, targetPath);
                return await ExecuteFileTreeAsync(
                    targetRoute,
                    targetPath,
                    new GetFileTreeInput(effectiveRoot, view, includeExtensions, fileFilter, excludePatterns, maxDepth, treeDepth, maxResults, sortBy, includeMetadata, includeLineCount, maxResponseBytes),
                    ct);
            },
            TargetPathToolRegistrationOptions.TargetPathReadOnlyTool("get_file_tree", GetFileTreeDescription)));
    }

    private static Task<CallToolResult> ExecuteFileTreeAsync(
        AnalysisToolRoute? targetRoute,
        string targetPath,
        GetFileTreeInput input,
        CancellationToken cancellationToken) =>
        AnalysisToolCall.ExecuteRouted(
            targetRoute!,
                    new AnalysisToolCallRequest(
                        new AnalysisTargetRequest(targetPath),
                        new AnalysisToolDispatch(
                            ProjectCall: lease => GetFileTreeTool.ExecuteAsync(lease.RootPath, input, cancellationToken),
                            AssemblySessionCall: lease => AssemblyGetFileTreeTool.ExecuteAsync(lease, input, cancellationToken),
                            ApplyAssemblyWireBudget: false),
                        cancellationToken));


    private const string GetFileTreeDescription =
        "Physische Dateilandkarte des Source- oder dekompilierten Roots. " +
        "root, fileFilter und excludePatterns sind relativ zu targetPath. " +
        "view: 'tree' [Default], 'summary', 'files'. " +
        "sortBy: 'path' [Default], 'size_desc', 'extension'. " +
        "includeExtensions: z. B. ['.cs']. maxDepth/treeDepth: 0-32. " +
        "maxResults (Default 20, Max 2000), maxResponseBytes (Default 8192, Max 65536). " +
        "includeMetadata: Dateigroessen (Default true), includeLineCount (Default false).";

    private static void AddGetNamespaceTree(
        McpServerPrimitiveCollection<McpServerTool> tools,
        ProjectRegistry registry,
        AnalysisToolRoute? targetRoute)
    {
        tools.Add(McpServerTool.Create(
            async (RequestContext<CallToolRequestParams> context, string targetPath, string? project = null, string? namespacePrefix = null,
                int depth = GetNamespaceTreeTool.DefaultDepth,
                bool includeTypes = true,
                string? kind = "all",
                int maxResults = GetNamespaceTreeTool.DefaultMaxResults,
                int maxResponseBytes = McpResponseBudgetLimits.DefaultBytes,
                CancellationToken ct = default) =>
                await ExecuteWithUnknownArgumentGuardAsync(
                    context,
                    () => AnalysisToolCall.ExecuteRouted(
                    targetRoute!,
                    new AnalysisToolCallRequest(
                        new AnalysisTargetRequest(targetPath),
                        new AnalysisToolDispatch(
                            ProjectCall: lease => GetNamespaceTreeTool.ExecuteAsync(
                                lease.Server,
                                new GetNamespaceTreeInput(project, namespacePrefix, depth, includeTypes, kind, maxResults, maxResponseBytes, DeferResponseBudgetToNavigation: true),
                                ct),
                            AssemblySessionCall: lease => GetNamespaceTreeTool.ExecuteAsync(
                                lease.Server,
                                new GetNamespaceTreeInput(project, namespacePrefix, depth, includeTypes, kind, maxResults, maxResponseBytes),
                                ct),
                            MaxResponseBytes: maxResponseBytes),
                        ct))),
            TargetPathToolRegistrationOptions.TargetPathReadOnlyTool("get_namespace_tree", GetNamespaceTreeDescription)));
    }

    private static readonly string GetNamespaceTreeDescription =
        "Hierarchische semantische Exploration (Solution -> Projekte -> Namespaces -> Typen). " +
        "Ohne Filter: Projektuebersicht. project: Projektfilter. namespacePrefix: Namespace-Drilldown. " +
        "depth: 1-3 (Default 1). includeTypes: Typen anzeigen (Default true). " +
        "kind: class/interface/record/struct/enum/all (Default all). " +
        "maxResults (Default 50, Cap 200), maxResponseBytes (Default 16384).";

    private static void AddGetClassStructure(
        McpServerPrimitiveCollection<McpServerTool> tools,
        ProjectRegistry registry,
        AnalysisToolRoute? targetRoute)
    {
        tools.Add(McpServerTool.Create(
            async (RequestContext<CallToolRequestParams> context, string targetPath, string? symbolIdentifier = null, string? sortBy = "lines",
                int maxMembers = GetClassStructureTool.DefaultMaxMembers,
                string? kindFilter = null,
                string? nameFilter = null,
                string scopeType = "all",
                bool includeGenerated = false,
                int maxResponseBytes = McpResponseBudgetLimits.DefaultBytes,
                CancellationToken ct = default) =>
            {
                var unknownError = TargetPathToolRegistrationOptions.RejectUnknownArguments(context);
                if (unknownError is not null) return unknownError;
                if (!McpScopeTypeValidator.TryParse(scopeType, out var parsedScope, out var fieldPath))
                {
                    return McpToolResults.InvalidArgument(
                        "scopeType muss 'all', 'production' oder 'tests' sein.",
                        "Einen der veröffentlichten scopeType-Werte angeben.",
                        fieldPath);
                }
                var scope = new McpScopeInput(parsedScope, includeGenerated);
                return await AnalysisToolCall.ExecuteRouted(
                    targetRoute!,
                    new AnalysisToolCallRequest(
                        new AnalysisTargetRequest(targetPath),
                        new AnalysisToolDispatch(
                            ProjectCall: lease => GetClassStructureTool.ExecuteAsync(lease.Server, new GetClassStructureArgs(symbolIdentifier, sortBy, maxMembers, kindFilter, nameFilter, maxResponseBytes, scope), ct),
                            AssemblySessionCall: lease => GetClassStructureTool.ExecuteAsync(lease.Server, new GetClassStructureArgs(symbolIdentifier, sortBy, maxMembers, kindFilter, nameFilter, maxResponseBytes, scope), ct),
                            MaxResponseBytes: maxResponseBytes,
                            PostNavigationResponseBudget: GetClassStructureResponseBudget.ApplyFinalResponseBudget),
                        ct));
            },
            TargetPathToolRegistrationOptions.TargetPathReadOnlyTool("get_class_structure", GetClassStructureDescription)));
    }

    private static readonly string GetClassStructureDescription =
        "Tabellarische Uebersicht aller Member eines Typs (Kind, Name, Sichtbarkeit, Zeilen, Signatur). " +
        "symbolIdentifier: Typname, Datei.cs:Zeile:Spalte oder DocCommentId. " +
        "sortBy: 'lines' (Default), 'kind', 'name'. kindFilter: Method, Property, Field, Constructor, all. " +
        "nameFilter: Substring-Filter. maxMembers: Default 50, Cap " + GetClassStructureTool.MaxMembersCap + ". " +
        "scopeType: 'all' (Default), 'production' oder 'tests'; includeGenerated: false (Default). " +
        "maxResponseBytes (Default 16384).";

    private static void AddGetFileSkeleton(
        McpServerPrimitiveCollection<McpServerTool> tools,
        ProjectRegistry registry,
        AnalysisToolRoute? targetRoute)
    {
        tools.Add(McpServerTool.Create(
            async (RequestContext<CallToolRequestParams> context, string targetPath, string[]? filePaths = null, int maxResponseBytes = GetFileSkeletonTool.DefaultMaxResponseBytes, CancellationToken ct = default) =>
            {
                var unknownError = TargetPathToolRegistrationOptions.RejectUnknownArguments(context);
                if (unknownError is not null) return unknownError;
                return await AnalysisToolCall.ExecuteRouted(
                    targetRoute!,
                    new AnalysisToolCallRequest(
                        new AnalysisTargetRequest(targetPath),
                        new AnalysisToolDispatch(
                            // The final budget is applied after the fixed navigation envelope is projected.
                            ProjectCall: lease => GetFileSkeletonTool.ExecuteAsync(lease.Server, filePaths, maxResponseBytes, ct),
                            AssemblySessionCall: lease => GetFileSkeletonTool.ExecuteAsync(lease.Server, filePaths, maxResponseBytes, ct),
                            MaxResponseBytes: maxResponseBytes),
                        ct));
            },
            TargetPathToolRegistrationOptions.TargetPathReadOnlyTool("get_file_skeleton", GetFileSkeletonDescription)));
    }

    private const string GetFileSkeletonDescription =
        "Ueberblick ueber Typen und Signaturen von C#-Dateien ohne Bodies (Batch in 1 Turn). " +
        "filePaths: Array von Dateipfaden (relativ oder absolut). " +
        "Liefert stabile Handoff-IDs fuer direkte Folge-Calls an get_symbol_body. " +
        "maxResponseBytes (Default 24576, Min 512, Max 65536).";

    private static void AddGetIndexScope(
        McpServerPrimitiveCollection<McpServerTool> tools,
        ProjectRegistry registry)
    {
        tools.Add(McpServerTool.Create(
            async (RequestContext<CallToolRequestParams> context, string targetPath, CancellationToken ct = default) =>
            {
                var unknownError = TargetPathToolRegistrationOptions.RejectUnknownArguments(context);
                if (unknownError is not null) return unknownError;
                return await ProjectAnalysisDispatcher.ExecuteAsync(
                    registry,
                    new AnalysisTargetRequest(targetPath),
                    lease => GetIndexScopeTool.ExecuteAsync(lease.Server, ct));
            },
            TargetPathToolRegistrationOptions.SourceReadOnlyTool("get_index_scope", GetIndexScopeDescription)));
    }

    private const string GetIndexScopeDescription =
        "Dateityp-Aufschluesselung der Solution (.cs durch Symbolgraph abgedeckt vs. Nicht-C#-Dateien). " +
        "Zeigt Dateianzahl und trennt physische Dateien, generierte Dokumente und Tests.";

    private static void AddGetHotspots(
        McpServerPrimitiveCollection<McpServerTool> tools,
        ProjectRegistry registry)
    {
        tools.Add(McpServerTool.Create(
            async (
                RequestContext<CallToolRequestParams> context,
                string targetPath,
                string? scopeFilter = null,
                int maxResults = GetHotspotsScanner.DefaultMaxResults,
                double minLinePercentage = GetHotspotsScanner.DefaultMinLinePercentage,
                string? scopeType = GetHotspotsScanner.DefaultScopeType,
                CancellationToken ct = default) =>
                {
                    var unknownError = TargetPathToolRegistrationOptions.RejectUnknownArguments(context);
                    if (unknownError is not null) return unknownError;
                    return await ProjectAnalysisDispatcher.ExecuteAsync(
                        registry,
                        new AnalysisTargetRequest(targetPath),
                        lease => GetHotspotsTool.ExecuteAsync(
                            new GetHotspotsRequest(
                                lease.Server,
                                scopeFilter,
                                maxResults,
                                minLinePercentage,
                                scopeType,
                                ct)));
                },
            TargetPathToolRegistrationOptions.SourceReadOnlyTool("get_hotspots", GetHotspotsDescription)));
    }

    private const string GetHotspotsDescription =
        "Prueft, welche Dateien sich dem Zeilenlimit (MaxLineCount) naehern. " +
        "scopeFilter: Projektname oder Pfad-Substring. " +
        "scopeType: 'production' [Default], 'tests' oder 'all'. " +
        "maxResults: sichtbare Hotspots (Default 50, Cap 200). " +
        "minLinePercentage: Auslastungsschwelle in Prozent (Default 80, Bereich 0-100).";

    private static async Task<CallToolResult> ExecuteWithUnknownArgumentGuardAsync(
        RequestContext<CallToolRequestParams> context,
        Func<Task<CallToolResult>> execute)
    {
        var unknownError = TargetPathToolRegistrationOptions.RejectUnknownArguments(context);
        return unknownError ?? await execute();
    }
}
