#nullable enable

using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using AiNetLinter.Configuration;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Assemblies.Analysis;
using AiNetLinter.Mcp.Tools;
using AiNetLinter.Mcp.Tools.AssemblyAnalysis;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace AiNetLinter.Mcp.Registration;

internal static class AssemblyAnalysisToolRegistrations
{
    internal static string ResolveTargetType(string? targetType, string targetPath) =>
        AssemblyPathValidation.IsSupportedAssemblyPath(targetPath)
            ? "assembly"
            : string.IsNullOrWhiteSpace(targetType) ? "assembly" : targetType;

    internal static void Register(
        McpServerPrimitiveCollection<McpServerTool> tools,
        AnalysisToolRoute assemblyRoute)
    {
        AddInspectAssembly(tools, assemblyRoute);
        AddFindAssemblyExtensions(tools, assemblyRoute);
        AddGetAssemblyContext(tools, assemblyRoute);
    }

    private static void AddInspectAssembly(
        McpServerPrimitiveCollection<McpServerTool> tools,
        AnalysisToolRoute assemblyRoute)
    {
        tools.Add(McpServerTool.Create(
            async (
                string targetPath,
                string? targetType = null,
                string? @namespace = null,
                string? typeName = null,
                string? memberName = null,
                bool publicOnly = true,
                int maxResults = AssemblyAnalysisService.DefaultMaxResults,
                bool exactTypeName = false,
                string[]? memberNames = null,
                int maxMembers = AssemblyAnalysisService.DefaultMaxMembers,
                bool includeReferences = false,
                int maxResponseBytes = 0,
                string? detailLevel = null,
                string? cursor = null,
                CancellationToken ct = default) =>
                await AnalysisToolCall.ExecuteRouted(
                    assemblyRoute,
                    new AnalysisToolCallRequest(
                        new AnalysisTargetRequest(ResolveTargetType(targetType, targetPath), targetPath),
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
                                    includeReferences,
                                    maxResponseBytes,
                                    detailLevel,
                                    cursor)),
                            ExpandAssemblyReferences: includeReferences),
                        ct)),
            McpToolRegistrationOptions.AssemblyTool("inspect_assembly", InspectAssemblyDescription)));
    }

    private const string InspectAssemblyDescription =
        "Wann nutzen: oeffentliche API einer exakt angegebenen lokalen .NET-Assembly metadata-only " +
        "ueber Roslyn untersuchen. targetPath mit absolutem .dll- oder .exe-Pfad ist Pflicht; " +
        "targetType ist optional und wird standardmaessig als 'assembly' behandelt. Ein Consumer-Projekt " +
        "wird in diesem Dispatch-Schritt nicht verwendet. " +
        "namespace, typeName und memberName filtern, publicOnly ist standardmaessig true, " +
        "exactTypeName schaltet fuer typeName von Teiltext- auf Exaktsuche um, memberNames " +
        "ergaenzt den Teiltextfilter memberName um eine exakte OR-Auswahl, " +
        "includeReferences (Default: bei Type-/Member-Filter false, sonst true) steuert " +
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
                string targetPath,
                string? targetType = null,
                string? receiverType = null,
                string? extensionName = null,
                string? @namespace = null,
                int maxResults = AssemblyAnalysisService.DefaultMaxResults,
                bool includeReferences = false,
                int maxResponseBytes = 0,
                string? detailLevel = null,
                string? cursor = null,
                CancellationToken ct = default) =>
                await AnalysisToolCall.ExecuteRouted(
                    assemblyRoute,
                    new AnalysisToolCallRequest(
                        new AnalysisTargetRequest(ResolveTargetType(targetType, targetPath), targetPath),
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
                                    cursor)),
                            ExpandAssemblyReferences: includeReferences),
                        ct)),
            McpToolRegistrationOptions.AssemblyTool("find_assembly_extensions", FindAssemblyExtensionsDescription)));
    }

    private const string FindAssemblyExtensionsDescription =
        "Wann nutzen: klassische C#-Extension-Methoden einer exakt angegebenen lokalen .NET-Assembly " +
        "metadata-only ueber Roslyn finden. targetPath mit absolutem .dll- oder .exe-Pfad ist Pflicht; " +
        "targetType ist optional und wird standardmaessig als 'assembly' behandelt. Ein Consumer-Projekt " +
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
            async (
                string targetPath,
                string? targetType = null,
                string? symbolIdentifier = null,
                string? symbol = null,
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
                CancellationToken ct = default) =>
                await AnalysisToolCall.ExecuteRouted(
                    assemblyRoute,
                    new AnalysisToolCallRequest(
                        new AnalysisTargetRequest(ResolveTargetType(targetType, targetPath), targetPath),
                        new AnalysisToolDispatch(
                            AssemblySessionCall: lease => AssemblyAnalysisContextTool.ExecuteAsync(
                                lease,
                                new AssemblyAnalysisContextArguments(
                                    symbolIdentifier ?? symbol,
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
                                    cursor),
                                ct),
                            ExpandAssemblyReferences: includeReferences || includeCallers || includeImpact),
                        ct)),
            McpToolRegistrationOptions.AssemblyTool("get_assembly_context", GetAssemblyContextDescription)));
    }

    private const string GetAssemblyContextDescription =
        "Wann nutzen: kompakter Assembly-spezifischer Composite-Einstieg fuer Agenten. " +
        "Liefert Identitaet, Scope, Vollstaendigkeit und auf Wunsch Metriken, Referenzen, " +
        "Caller/Impact, Body und Klassenstruktur in einer strukturierten Antwort. " +
        "targetType='assembly' und targetPath sind ein absoluter .dll- oder .exe-Pfad; targetType ist optional und wird inferiert; symbolIdentifier ist optional und " +
        "akzeptiert DocCommentId, Typname oder Datei:Zeile:Spalte. symbol ist ein Alias. " +
        "maxResponseBytes, detailLevel (compact/standard/full) und cursor steuern Budget und Paging; " +
        "unsupported/partial/complete sowie totalCount, returnedCount, isTruncated und continuationToken " +
        "bleiben maschinenlesbar sichtbar. Die Assembly wird weder geladen noch ausgefuehrt.";
}
