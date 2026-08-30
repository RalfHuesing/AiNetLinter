#nullable enable

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
    }

    private static void AddInspectAssembly(
        McpServerPrimitiveCollection<McpServerTool> tools,
        AnalysisToolRoute assemblyRoute)
    {
        tools.Add(McpServerTool.Create(
            async (
                string targetType,
                string targetPath,
                string? @namespace = null,
                string? typeName = null,
                string? memberName = null,
                bool publicOnly = true,
                int maxResults = AssemblyAnalysisService.DefaultMaxResults,
                bool exactTypeName = false,
                string[]? memberNames = null,
                int maxMembers = AssemblyAnalysisService.DefaultMaxMembers,
                CancellationToken ct = default) =>
                await AnalysisToolCall.ExecuteRouted(
                    assemblyRoute,
                    new AnalysisToolCallRequest(
                        new AnalysisTargetRequest(targetType, targetPath),
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
                                    maxMembers))),
                        ct)),
            McpToolRegistrationOptions.AssemblyTool("inspect_assembly", InspectAssemblyDescription)));
    }

    private const string InspectAssemblyDescription =
        "Wann nutzen: öffentliche API einer exakt angegebenen lokalen .NET-DLL metadata-only " +
        "über Roslyn untersuchen. targetType='assembly' und targetPath mit absolutem DLL-Pfad " +
        "sind Pflicht; ein Consumer-Projekt wird in diesem Dispatch-Schritt nicht verwendet. " +
        "namespace, typeName und memberName filtern, publicOnly ist standardmäßig true, " +
        "exactTypeName schaltet für typeName von Teiltext- auf Exaktsuche um, memberNames " +
        "ergänzt den bestehenden Teiltextfilter memberName um eine exakte OR-Auswahl, " +
        "maxResults begrenzt Typen deterministisch (Default 100, Maximum 1000), " +
        "maxMembers begrenzt Member je Typ (Default 100, Maximum 1000). Identität, " +
        "Referenzen, Typen, Methoden, Properties, Felder, Events, Attribute und Diagnosen " +
        "werden ausgegeben; Methoden und Indexer liefern zusätzlich strukturierte " +
        "Parameterdaten. Eine verfügbare explizite Source-Zuordnung wird source-backed " +
        "genutzt; ohne Zuordnung oder verfügbaren Provider greift die statische Decompilation. Bei " +
        "fehlenden Abhängigkeiten lautet completeness partial. " +
        "Die DLL wird weder geladen noch ausgeführt.";

    private static void AddFindAssemblyExtensions(
        McpServerPrimitiveCollection<McpServerTool> tools,
        AnalysisToolRoute assemblyRoute)
    {
        tools.Add(McpServerTool.Create(
            async (
                string targetType,
                string targetPath,
                string? receiverType = null,
                string? extensionName = null,
                string? @namespace = null,
                int maxResults = AssemblyAnalysisService.DefaultMaxResults,
                CancellationToken ct = default) =>
                await AnalysisToolCall.ExecuteRouted(
                    assemblyRoute,
                    new AnalysisToolCallRequest(
                        new AnalysisTargetRequest(targetType, targetPath),
                        new AnalysisToolDispatch(
                            AssemblySessionCall: lease => FindAssemblyExtensionsTool.ExecuteAsync(
                                lease,
                                new FindAssemblyExtensionsArguments(
                                    lease.CanonicalPath,
                                    receiverType,
                                    extensionName,
                                    @namespace,
                                    maxResults))),
                        ct)),
            McpToolRegistrationOptions.AssemblyTool("find_assembly_extensions", FindAssemblyExtensionsDescription)));
    }

    private const string FindAssemblyExtensionsDescription =
        "Wann nutzen: klassische C#-Extension-Methoden einer exakt angegebenen lokalen DLL " +
        "metadata-only über Roslyn finden. targetType='assembly' und targetPath mit absolutem " +
        "DLL-Pfad sind Pflicht; ein Consumer-Projekt wird in diesem Dispatch-Schritt nicht verwendet. " +
        "receiverType grenzt den gewünschten Empfänger-Typ ein; ohne Consumer-Projekt " +
        "wird seine Roslyn-Anwendbarkeit als not_decidable ausgewiesen. extensionName und namespace filtern, " +
        "Generics, Constraints und Konvertierungen werden dabei metadata-only berücksichtigt. " +
        "Eine verfügbare explizite Source-Zuordnung wird source-backed genutzt; sonst greift " +
        "die statische Decompilation. " +
        "maxResults begrenzt deterministisch (Default 100, Maximum 1000). Die Antwort trennt " +
        "applicable, not_applicable und not_decidable und markiert fehlende Abhängigkeiten " +
        "mit completeness partial. Methoden liefern zusätzlich strukturierte Parameterdaten. " +
        "Die DLL wird weder geladen noch ausgeführt.";
}
