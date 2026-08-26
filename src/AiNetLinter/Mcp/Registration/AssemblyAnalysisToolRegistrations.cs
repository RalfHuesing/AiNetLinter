#nullable enable

using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Projects;
using AiNetLinter.Mcp.Tools;
using AiNetLinter.Mcp.Tools.AssemblyAnalysis;
using ModelContextProtocol.Server;

namespace AiNetLinter.Mcp.Registration;

internal static class AssemblyAnalysisToolRegistrations
{
    internal static void Register(
        McpServerPrimitiveCollection<McpServerTool> tools,
        ProjectRegistry registry)
    {
        AddInspectAssembly(tools, registry);
        AddFindAssemblyExtensions(tools, registry);
    }

    private static void AddInspectAssembly(
        McpServerPrimitiveCollection<McpServerTool> tools,
        ProjectRegistry registry)
    {
        tools.Add(McpServerTool.Create(
            async (
                string assemblyPath,
                string? projectRoot = null,
                string? @namespace = null,
                string? typeName = null,
                string? memberName = null,
                bool publicOnly = true,
                int maxResults = AssemblyAnalysisService.DefaultMaxResults,
                bool exactTypeName = false,
                string[]? memberNames = null,
                int maxMembers = AssemblyAnalysisService.DefaultMaxMembers,
                CancellationToken ct = default) =>
                projectRoot is null
                    ? await InspectAssemblyTool.ExecuteAsync(null, new InspectAssemblyArguments(assemblyPath, @namespace, typeName, memberName, publicOnly, maxResults, exactTypeName, memberNames, maxMembers), ct)
                    : await ProjectToolCall.ExecuteAsync(
                        registry,
                        projectRoot,
                        lease => InspectAssemblyTool.ExecuteAsync(lease.Server, new InspectAssemblyArguments(assemblyPath, @namespace, typeName, memberName, publicOnly, maxResults, exactTypeName, memberNames, maxMembers), ct)),
            McpToolRegistrationOptions.ReadOnlyTool("inspect_assembly", InspectAssemblyDescription)));
    }

    private const string InspectAssemblyDescription =
        "Wann nutzen: öffentliche API einer exakt angegebenen lokalen .NET-DLL metadata-only " +
        "über Roslyn untersuchen. assemblyPath ist Pflicht und muss absolut sein; projectRoot " +
        "ist optional und ordnet die Assembly gegen die geladene Consumer-Solution ein. " +
        "namespace, typeName und memberName filtern, publicOnly ist standardmäßig true, " +
        "exactTypeName schaltet für typeName von Teiltext- auf Exaktsuche um, memberNames " +
        "ergänzt den bestehenden Teiltextfilter memberName um eine exakte OR-Auswahl, " +
        "maxResults begrenzt Typen deterministisch (Default 100, Maximum 1000), " +
        "maxMembers begrenzt Member je Typ (Default 100, Maximum 1000). Identität, " +
        "Referenzen, Typen, Methoden, Properties, Felder, Events, Attribute und Diagnosen " +
        "werden ausgegeben; bei fehlenden Abhängigkeiten lautet completeness partial. " +
        "Die DLL wird weder geladen noch ausgeführt.";

    private static void AddFindAssemblyExtensions(
        McpServerPrimitiveCollection<McpServerTool> tools,
        ProjectRegistry registry)
    {
        tools.Add(McpServerTool.Create(
            async (
                string assemblyPath,
                string? projectRoot = null,
                string? receiverType = null,
                string? extensionName = null,
                string? @namespace = null,
                int maxResults = AssemblyAnalysisService.DefaultMaxResults,
                CancellationToken ct = default) =>
                projectRoot is null
                    ? await FindAssemblyExtensionsTool.ExecuteAsync(null, new FindAssemblyExtensionsArguments(assemblyPath, receiverType, extensionName, @namespace, maxResults), ct)
                    : await ProjectToolCall.ExecuteAsync(
                        registry,
                        projectRoot,
                        lease => FindAssemblyExtensionsTool.ExecuteAsync(lease.Server, new FindAssemblyExtensionsArguments(assemblyPath, receiverType, extensionName, @namespace, maxResults), ct)),
            McpToolRegistrationOptions.ReadOnlyTool("find_assembly_extensions", FindAssemblyExtensionsDescription)));
    }

    private const string FindAssemblyExtensionsDescription =
        "Wann nutzen: klassische C#-Extension-Methoden einer exakt angegebenen lokalen DLL " +
        "metadata-only über Roslyn finden. assemblyPath ist Pflicht und muss absolut sein; " +
        "projectRoot ist optional für die Consumer-Solution. receiverType löst einen konkreten " +
        "Consumer-Typ auf und prüft die tatsächliche Roslyn-Reduzierbarkeit einschließlich " +
        "Generics, Constraints und Konvertierungen. extensionName und namespace filtern, " +
        "maxResults begrenzt deterministisch (Default 100, Maximum 1000). Die Antwort trennt " +
        "applicable, not_applicable und not_decidable und markiert fehlende Abhängigkeiten " +
        "mit completeness partial. Methoden liefern zusätzlich strukturierte Parameterdaten. " +
        "Die DLL wird weder geladen noch ausgeführt.";
}
