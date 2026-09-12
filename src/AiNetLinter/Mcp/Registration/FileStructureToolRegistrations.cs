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
        "Wann nutzen: physische Dateilandkarte eines absoluten Projekt- oder dekompilierten " +
        "SourceRoots als ersten Discovery-Schritt fuer Agenten. root, fileFilter und " +
        "excludePatterns sind relativ zu " +
        "targetPath; fileFilter ist ein Pfad-Glob, keine Inhaltssuche. view: 'tree' [Default], " +
        "'summary', 'files'. includeExtensions: Extensionen wie ['.cs'] oder ['*']. " +
        "maxDepth und treeDepth: 0 bis 32 (effektive Tiefe = maxDepth ?? treeDepth; bei aktivem fileFilter, gezieltem Unterverzeichnis-Root oder view='summary' wird standardmaessig bis zum Limit gescannt, wenn weder maxDepth noch treeDepth gesetzt sind; maxDepth hat Vorrang). " +
        "maxResults: mindestens 1; 0 oder negative Werte liefern INVALID_ARGUMENT. Begrenzung der primaeren Dateitreffer (Default 20, Maximum 2000). " +
        "maxResponseBytes: serialisiertes Payload-Budget (Default 8192, Maximum 65536). Für Assembly-Ziele wird der " +
        "vorhandene Source- oder dekompilierte SourceRoot verwendet; ohne solchen Root ist die " +
        "Capability unsupported. Die Assembly-Navigation bleibt an Target und Snapshot gebunden. " +
        "sortBy: 'path' [Default], 'size_desc', 'extension'. includeMetadata: Dateigroessen (Default true), " +
        "includeLineCount: Zeilenzaehlung (Default false). Der Content enthält fileTree; " +
        "summary, exclusions und completeness unterscheiden physische Dateien, angefordert ausgeschlossene " +
        "Dateien sowie uebersprungene Standard- und Reparse-Point-Verzeichnisse.";

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
        "Wann nutzen: hierarchische semantische Exploration einer C#-Codebase (Solution -> Projekte " +
        "-> Namespaces -> Typen) nach dem Progressive-Disclosure-Prinzip. Ohne Parameter: Projekt-" +
        "Uebersicht. project: Namespaces eines Projekts filtern. namespacePrefix: Einstiegspunkt fuer " +
        "Drilldown. depth: mindestens 1 (0 oder negative Werte liefern INVALID_ARGUMENT), 1-3 Namespace-Ebenen (Default 1). includeTypes: Typen ausgeben (Default true) " +
        "oder nur Sub-Namespaces. kind: class/interface/record/struct/enum/all (Default all). " +
        "maxResults: mindestens 1 (0 oder negative Werte liefern INVALID_ARGUMENT), Obergrenze der Eintraege (Default 50, Cap 200). " +
        "maxResponseBytes: UTF-8-Grenze für finalen Content einschließlich Navigation und Trunkierungsfooter (Default 16384, Minimum 512, Maximum 65536). Das Budget wird nach der finalen Textprojektion geprüft.";

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
        "Wann nutzen: Tabellarische Uebersicht ueber alle Member einer Klasse/eines Typs inkl. " +
        "Kind, Name, Visibility, Start-/End-Zeile, Zeilenanzahl und Signatur (z. B. zur Analyse " +
        "vor Refactorings oder zur Identifikation langer Member; bei Records inkl. Primary-Constructor-Parametern). " +
        "symbolIdentifier (Pflicht): Typname, Datei.cs:Zeile:Spalte oder DocCommentId. " +
        "sortBy: 'lines' (Default), 'kind', 'name'. kindFilter: optionaler Filter nach Member-Kind (z. B. Method, Property, Field, Constructor, all). " +
        "nameFilter: optionaler Substring-Filter nach Member-Namen. maxMembers: Begrenzung der sichtbaren Member " +
        "(mindestens 1; 0 oder negative Werte liefern INVALID_ARGUMENT; Default 50, Cap " + GetClassStructureTool.MaxMembersCap + "); bei Ueberschreitung " +
        "Truncation-Meta-Zeile sowie TotalMemberCount und ShownMemberCount im Content. " +
        "scopeType: 'all' (Default), 'production' oder 'tests'; includeGenerated: false (Default). Der angefragte Typ bleibt als markierter Seed sichtbar, auch wenn eine Partial-Location außerhalb des Scopes liegt. " +
        "maxResponseBytes: UTF-8-Grenze für finalen Content einschließlich Navigation und Trunkierungsfooter (Default 16384, Minimum 512, Maximum 65536). Das Budget wird nach der finalen Textprojektion geprüft.";

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
        "Wann nutzen: Ueberblick ueber Typen und Signaturen einer oder mehrerer C#-Dateien (Batch in 1 Turn), " +
        "ohne die Bodies zu lesen. Der Content enthält stabile Handoff-IDs fuer direkte Folge-Calls an get_symbol_body. " +
        "filePaths: Array von Dateipfaden (auch fuer genau eine Datei), relativ oder absolut. " +
        "maxResponseBytes: kombinierte UTF-8-Grenze der finalen Wire-Nutzlast (Default 24576, Minimum 512, Maximum 65536); bei Trunkierung wird ein nächster Schritt genannt und nur an vollständigen Skeleton-Einheiten gekürzt.";

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
        "Wann nutzen: als ersten Discovery-Call vor find_symbol/search_pattern — Dateityp-" +
        "Aufschluesselung der Solution (.cs vom Symbolgraph abgedeckt, .css/.html/.js/.razor/" +
        ".xaml nicht abgedeckt, jeweils mit Dateianzahl). population trennt physische Dateien " +
        "von Roslyn-Dokumenten sowie generierten und Test-Dokumenten.";

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
        "Wann nutzen: vor einem geplanten Edit pruefen, ob eine Datei/ein Projekt sich dem " +
        "Zeilen-Limit (MaxLineCount) naehert. scopeFilter: Projekt-Name oder Pfad-Substring zur Eingrenzung. " +
        "scopeType: 'production' [Default], 'tests' oder 'all' zur Auswahl von Produktions- bzw. Testdateien. " +
        "maxResults: mindestens 1 (0 oder negative Werte liefern INVALID_ARGUMENT), sichtbare Hotspots (Default 50, Cap 200). minLinePercentage: untere " +
        "Auslastungsschwelle in Prozent (Default 80, Bereich 0-100). Ergebnisse bleiben " +
        "deterministisch nach absteigender Zeilenzahl und Pfad sortiert; der Content " +
        "weist Gesamtzahl, Anzeigezahl und Trunkierung aus.";

    private static async Task<CallToolResult> ExecuteWithUnknownArgumentGuardAsync(
        RequestContext<CallToolRequestParams> context,
        Func<Task<CallToolResult>> execute)
    {
        var unknownError = TargetPathToolRegistrationOptions.RejectUnknownArguments(context);
        return unknownError ?? await execute();
    }
}
