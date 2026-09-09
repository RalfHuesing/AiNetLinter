#nullable enable

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Assemblies.Analysis;
using AiNetLinter.Mcp.Tools;
using AiNetLinter.Mcp.Tools.AssemblyAnalysis;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace AiNetLinter.Mcp.Registration;

internal static class AssemblyAnalysisToolRegistrations
{
    internal static void Register(
        McpServerPrimitiveCollection<McpServerTool> tools,
        AnalysisToolRoute assemblyRoute)
    {
        AddInspectAssembly(tools, assemblyRoute);
        AddFindAssemblyExtensions(tools, assemblyRoute);
        AddSearchAssembly(tools, assemblyRoute);
        AddGetAssemblyContext(tools, assemblyRoute);
    }

    private static void AddSearchAssembly(
        McpServerPrimitiveCollection<McpServerTool> tools,
        AnalysisToolRoute assemblyRoute)
    {
        tools.Add(McpServerTool.Create(
            async (
                RequestContext<CallToolRequestParams> context,
                string targetPath,
                string? pattern = null,
                bool? isRegex = null,
                string? searchKind = null,
                int maxResults = AssemblySearchTool.DefaultMaxResults,
                int maxFiles = 0,
                int contextLines = 0,
                string? fileFilter = null,
                int maxResponseBytes = 0,
                string? cursor = null,
                string? continuationToken = null,
                bool declarationOnly = false,
                string? kind = null,
                CancellationToken ct = default) =>
            {
                var unknownError = TargetPathToolRegistrationOptions.RejectUnknownArguments(context);
                if (unknownError is not null) return unknownError;
                var effectiveCursor = cursor ?? continuationToken;
                return await AnalysisToolCall.ExecuteRouted(
                    assemblyRoute,
                    new AnalysisToolCallRequest(
                        new AnalysisTargetRequest(targetPath),
                        new AnalysisToolDispatch(
                            AssemblySessionCall: lease => AssemblySearchTool.ExecuteAsync(
                                lease,
                                new AssemblySearchArguments(
                                    pattern,
                                    isRegex,
                                    searchKind,
                                    maxResults,
                                    maxFiles,
                                    contextLines,
                                    maxResponseBytes,
                                    fileFilter,
                                    effectiveCursor,
                                    continuationToken,
                                    declarationOnly,
                                    kind),
                                ct),
                            MaxResponseBytes: maxResponseBytes,
                            Cursor: effectiveCursor),
                        ct));
            },
            TargetPathToolRegistrationOptions.AssemblyTool("search_assembly", SearchAssemblyDescription)));
    }

    private const string SearchAssemblyDescription =
        "Wann nutzen: read-only Text-/Mustersuche im verifizierten Source- oder dekompilierten " +
        "Root einer lokalen Assembly. targetPath ist ein absoluter .dll- oder .exe-Pfad; " +
        "searchKind: 'text' fuer ein eigenes pattern, " +
        "'data_access' fuer typische Datenbank-/Datei-/Transaktionsaufrufe oder 'external_calls' " +
        "fuer typische HTTP-/RPC-/Socket-/Prozessaufrufe; die beiden Fachmodi verwenden ohne pattern " +
        "ein eingebautes, sichtbares Regex. isRegex gilt fuer ein eigenes pattern (Default null = 'auto' mit automatischer " +
        "Regex-Erkennung und Promotion; true = explizit Regex, false = explizit Plain-Substring). " +
        "declarationOnly: schliesst Treffer in Kommentaren, Strings und XML-Docs aus. " +
        "kind: schraenkt Treffer auf eine bestimmte Symbolart ein ('method', 'type', 'property'). " +
        "maxResults (Default 50, Cap 1000), maxFiles, contextLines (Cap 5), fileFilter als Glob (z. B. '*.cs', '!*Designer*') oder Regex, " +
        "maxResponseBytes und cursor begrenzen die Antwort. StructuredContent.assemblySearch liefert " +
        "relative Trefferpfade, stabile IDs, Matchbereiche, totalCount/returnedCount, " +
        "completeness, truncatedBy und continuationToken; analysis enthaelt Origin, Generation und " +
        "Source-Policy. Ohne verfügbaren SourceRoot ist die Capability explizit unsupported. " +
        "Die Assembly wird weder geladen noch ausgefuehrt.";

    private static void AddInspectAssembly(
        McpServerPrimitiveCollection<McpServerTool> tools,
        AnalysisToolRoute assemblyRoute)
    {
        tools.Add(McpServerTool.Create(
            async (
                RequestContext<CallToolRequestParams> context,
                string targetPath,
                string? @namespace = null,
                string? typeName = null,
                string? memberName = null,
                bool publicOnly = true,
                int maxResults = AssemblyAnalysisService.DefaultMaxResults,
                bool exactTypeName = false,
                string[]? memberNames = null,
                int maxMembers = AssemblyAnalysisService.DefaultMaxMembers,
                bool? includeReferences = null,
                int maxResponseBytes = 0,
                string? detailLevel = null,
                string? cursor = null,
                string? continuationToken = null,
                CancellationToken ct = default) =>
            {
                var unknownError = TargetPathToolRegistrationOptions.RejectUnknownArguments(context);
                if (unknownError is not null) return unknownError;
                var effectiveCursor = cursor ?? continuationToken;
                var effectiveIncludeReferences = includeReferences ?? (
                    string.IsNullOrWhiteSpace(@namespace)
                    && string.IsNullOrWhiteSpace(typeName)
                    && string.IsNullOrWhiteSpace(memberName)
                    && (memberNames is null || memberNames.All(string.IsNullOrWhiteSpace)));
                return await AnalysisToolCall.ExecuteRouted(
                    assemblyRoute,
                    new AnalysisToolCallRequest(
                        new AnalysisTargetRequest(targetPath),
                        new AnalysisToolDispatch(
                            AssemblySessionCall: lease => InspectAssemblyTool.ExecuteAsync(
                                lease,
                                new InspectAssemblyArguments(
                                    lease.CanonicalPath,
                                    @namespace,
                                    typeName,
                                    memberName,
                                    publicOnly,
                                    maxResults,
                                    exactTypeName,
                                    memberNames,
                                    maxMembers,
                                    effectiveIncludeReferences,
                                    maxResponseBytes,
                                    detailLevel,
                                    effectiveCursor)),
                            ExpandAssemblyReferences: effectiveIncludeReferences,
                            MaxResponseBytes: maxResponseBytes,
                            DetailLevel: detailLevel,
                            Cursor: effectiveCursor),
                        ct));
            },
            TargetPathToolRegistrationOptions.AssemblyTool("inspect_assembly", InspectAssemblyDescription)));
    }

    private const string InspectAssemblyDescription =
        "Wann nutzen: oeffentliche API einer exakt angegebenen lokalen .NET-Assembly metadata-only " +
        "ueber Roslyn untersuchen. targetPath mit absolutem .dll- oder .exe-Pfad ist Pflicht; " +
        "Ein Consumer-Projekt " +
        "wird in diesem Dispatch-Schritt nicht verwendet. " +
        "namespace, typeName und memberName filtern, publicOnly ist standardmaessig true, " +
        "exactTypeName schaltet fuer typeName von Teiltext- auf Exaktsuche um, memberNames " +
        "ergaenzt den Teiltextfilter memberName um eine exakte OR-Auswahl, " +
        "includeReferences (wenn weggelassen: bei Type-/Member-Filter false, sonst true; " +
        "true/false wird explizit respektiert) steuert " +
         "Referenzlisten und Referenz-Sessions; ohne Detailflag bleiben nur Summen sichtbar, " +
        "maxResults begrenzt Typen (Default 100, Maximum 1000), " +
        "maxMembers begrenzt Member je Typ (Default 100, Maximum 1000). Identitaet, " +
        "Referenzen, Typen, Methoden, Properties, Felder, Events, Attribute und Diagnosen " +
        "werden ausgegeben; Methoden und Indexer liefern zusaetzlich strukturierte " +
        "Parameterdaten. Eine verfuegbare explizite Source-Zuordnung wird source-backed " +
        "genutzt; ohne Zuordnung oder verfuegbaren Provider greift die statische Decompilation. Bei " +
        "fehlenden Abhaengigkeiten lautet completeness partial. " +
        "Die Assembly wird weder geladen noch ausgefuehrt.";

    private static void AddFindAssemblyExtensions(
        McpServerPrimitiveCollection<McpServerTool> tools,
        AnalysisToolRoute assemblyRoute)
    {
        tools.Add(McpServerTool.Create(
            async (
                RequestContext<CallToolRequestParams> context,
                string targetPath,
                string? receiverType = null,
                string? extensionName = null,
                string? @namespace = null,
                int maxResults = AssemblyAnalysisService.DefaultMaxResults,
                bool includeReferences = false,
                int maxResponseBytes = 0,
                string? detailLevel = null,
                string? cursor = null,
                string? continuationToken = null,
                CancellationToken ct = default) =>
            {
                var unknownError = TargetPathToolRegistrationOptions.RejectUnknownArguments(context);
                if (unknownError is not null) return unknownError;
                var effectiveCursor = cursor ?? continuationToken;
                return await AnalysisToolCall.ExecuteRouted(
                    assemblyRoute,
                    new AnalysisToolCallRequest(
                        new AnalysisTargetRequest(targetPath),
                        new AnalysisToolDispatch(
                            AssemblySessionCall: lease => FindAssemblyExtensionsTool.ExecuteAsync(
                                lease,
                                new FindAssemblyExtensionsArguments(
                                    lease.CanonicalPath,
                                    receiverType,
                                    extensionName,
                                    @namespace,
                                    maxResults,
                                    includeReferences,
                                    maxResponseBytes,
                                    detailLevel,
                                    effectiveCursor)),
                            ExpandAssemblyReferences: includeReferences,
                            MaxResponseBytes: maxResponseBytes,
                            DetailLevel: detailLevel,
                            Cursor: effectiveCursor),
                        ct));
            },
            TargetPathToolRegistrationOptions.AssemblyTool("find_assembly_extensions", FindAssemblyExtensionsDescription)));
    }

    private const string FindAssemblyExtensionsDescription =
        "Wann nutzen: klassische C#-Extension-Methoden einer exakt angegebenen lokalen .NET-Assembly " +
        "metadata-only ueber Roslyn finden. targetPath mit absolutem .dll- oder .exe-Pfad ist Pflicht; " +
        "Ein Consumer-Projekt " +
        "wird in diesem Dispatch-Schritt nicht verwendet. " +
        "receiverType grenzt den gewuenschten Empfaenger-Typ ein; ohne Consumer-Projekt " +
        "wird seine Roslyn-Anwendbarkeit als not_decidable ausgewiesen. extensionName und namespace filtern, " +
        "includeReferences (Default false) steuert, ob bounded Referenz-Assemblies und Reference-Sessions " +
        "einbezogen werden. " +
        "Generics, Constraints und Konvertierungen werden dabei metadata-only beruecksichtigt. " +
        "Eine verfuegbare explizite Source-Zuordnung wird source-backed genutzt; sonst greift " +
        "die statische Decompilation. " +
        "maxResults begrenzt (Default 100, Maximum 1000). Die Antwort trennt " +
        "applicable, not_applicable und not_decidable und markiert fehlende Abhaengigkeiten " +
        "mit completeness partial. Methoden liefern zusaetzlich strukturierte Parameterdaten. " +
        "Die Assembly wird weder geladen noch ausgefuehrt.";

    private static void AddGetAssemblyContext(
        McpServerPrimitiveCollection<McpServerTool> tools,
        AnalysisToolRoute assemblyRoute)
    {
        tools.Add(McpServerTool.Create(
            (RequestContext<CallToolRequestParams> context,
                string targetPath,
                string? symbolIdentifier = null,
                bool includeMetrics = true,
                bool includeReferences = false,
                bool includeCallers = false,
                bool includeImpact = false,
                bool includeBody = false,
                bool includeClassStructure = false,
                int maxResults = AssemblyAnalysisService.DefaultMaxResults,
                int maxBodyLines = GetSymbolBodyTool.DefaultMaxBodyLines,
                int maxCallers = 10,
                int depth = 1,
                int topN = 10,
                int maxResponseBytes = 0,
                string? detailLevel = null,
                string? cursor = null,
                CancellationToken ct = default) => ExecuteGetAssemblyContextAsync(
                    context,
                    assemblyRoute,
                    new AssemblyContextExecutionParameters(
                        targetPath,
                        symbolIdentifier,
                        includeMetrics,
                        includeReferences,
                        includeCallers,
                        includeImpact,
                        includeBody,
                        includeClassStructure,
                        maxResults,
                        maxBodyLines,
                        maxCallers,
                        depth,
                        topN,
                        maxResponseBytes,
                        detailLevel,
                        cursor,
                        ct)),
            TargetPathToolRegistrationOptions.AssemblyTool("get_assembly_context", GetAssemblyContextDescription)));
    }

    private static async Task<CallToolResult> ExecuteGetAssemblyContextAsync(
        RequestContext<CallToolRequestParams> context,
        AnalysisToolRoute assemblyRoute,
        AssemblyContextExecutionParameters parameters)
    {
        var unknownError = TargetPathToolRegistrationOptions.RejectUnknownArguments(context);
        if (unknownError is not null) return unknownError;
        return await AnalysisToolCall.ExecuteRouted(
            assemblyRoute,
            new AnalysisToolCallRequest(
                new AnalysisTargetRequest(parameters.TargetPath),
                new AnalysisToolDispatch(
                    AssemblySessionCall: lease => AssemblyAnalysisContextTool.ExecuteAsync(
                        lease,
                        new AssemblyAnalysisContextArguments(
                            parameters.SymbolIdentifier,
                            parameters.IncludeMetrics,
                            parameters.IncludeReferences,
                            parameters.IncludeCallers,
                            parameters.IncludeImpact,
                            parameters.IncludeBody,
                            parameters.IncludeClassStructure,
                            parameters.MaxResults,
                            parameters.MaxBodyLines,
                            parameters.MaxCallers,
                            parameters.Depth,
                            parameters.TopN,
                            parameters.MaxResponseBytes,
                            parameters.DetailLevel,
                            parameters.Cursor),
                        parameters.CancellationToken),
                    ExpandAssemblyReferences: parameters.IncludeReferences || parameters.IncludeCallers || parameters.IncludeImpact,
                    MaxResponseBytes: parameters.MaxResponseBytes,
                    DetailLevel: parameters.DetailLevel,
                    Cursor: parameters.Cursor),
                parameters.CancellationToken));
    }

    private sealed record AssemblyContextExecutionParameters(
        string TargetPath,
        string? SymbolIdentifier,
        bool IncludeMetrics,
        bool IncludeReferences,
        bool IncludeCallers,
        bool IncludeImpact,
        bool IncludeBody,
        bool IncludeClassStructure,
        int MaxResults,
        int MaxBodyLines,
        int MaxCallers,
        int Depth,
        int TopN,
        int MaxResponseBytes,
        string? DetailLevel,
        string? Cursor,
        CancellationToken CancellationToken);

    private const string GetAssemblyContextDescription =
        "Wann nutzen: kompakter Assembly-spezifischer Composite-Einstieg fuer Agenten. " +
        "Liefert Identitaet, Scope, Vollstaendigkeit und auf Wunsch Metriken, Referenzen, " +
        "Caller/Impact, Body und Klassenstruktur in einer strukturierten Antwort. " +
        "targetPath ist ein absoluter .dll- oder .exe-Pfad; symbolIdentifier ist optional und " +
        "akzeptiert DocCommentId, Typname oder Datei:Zeile:Spalte. " +
        "maxResponseBytes, detailLevel (compact/standard/full) und cursor steuern Budget und Paging; " +
        "unsupported/partial/complete sowie totalCount, returnedCount, isTruncated und continuationToken " +
        "bleiben maschinenlesbar sichtbar. Die Assembly wird weder geladen noch ausgefuehrt.";
}
