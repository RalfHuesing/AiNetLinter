#nullable enable

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Assemblies.Analysis;
using AiNetLinter.Mcp.Tools;
using AiNetLinter.Mcp.Tools.AssemblyAnalysis;
using AiNetLinter.Mcp.Validation;
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
                string? continuationToken = null,
                bool declarationOnly = false,
                string? kind = null,
                CancellationToken ct = default) =>
            {
                var unknownError = TargetPathToolRegistrationOptions.RejectUnknownArguments(context);
                if (unknownError is not null) return unknownError;
                var effectiveCursor = continuationToken;
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
                            Cursor: effectiveCursor,
                            ApplyAssemblyWireBudget: false),
                        ct));
            },
            TargetPathToolRegistrationOptions.AssemblyTool("search_assembly", SearchAssemblyDescription)));
    }

    private const string SearchAssemblyDescription =
        "Read-only Text-/Mustersuche im dekompilierten Root einer lokalen Assembly (.dll/.exe). " +
        "searchKind: 'text' [eigenes pattern], 'data_access' [DB/Datei], 'external_calls' [HTTP/RPC/Prozess]. " +
        "pattern: Suchbegriff (isRegex: auto/true/false). " +
        "declarationOnly: schliesst Kommentare/Strings/XML-Docs aus. " +
        "kind: 'method', 'type', 'property'. fileFilter: Glob oder Regex. " +
        "maxResults (Default 50, Cap 1000), contextLines (0-5), maxResponseBytes, continuationToken.";

    private static void AddInspectAssembly(
        McpServerPrimitiveCollection<McpServerTool> tools,
        AnalysisToolRoute assemblyRoute)
    {
        tools.Add(McpServerTool.Create(
            (
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
                string? continuationToken = null,
                CancellationToken ct = default) =>
                ExecuteInspectAssemblyAsync(
                    context,
                    assemblyRoute,
                    new InspectAssemblyExecutionParameters(
                        targetPath,
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
                        continuationToken,
                        ct)),
            TargetPathToolRegistrationOptions.AssemblyTool("inspect_assembly", InspectAssemblyDescription)));
    }

    private static async Task<CallToolResult> ExecuteInspectAssemblyAsync(
        RequestContext<CallToolRequestParams> context,
        AnalysisToolRoute assemblyRoute,
        InspectAssemblyExecutionParameters parameters)
    {
        var unknownError = TargetPathToolRegistrationOptions.RejectUnknownArguments(context);
        if (unknownError is not null) return unknownError;
        if (AssemblyAnalysisResponseLimits.ValidateDetailLevel(parameters.DetailLevel) is { } detailLevelError) return detailLevelError;
        var includeReferences = parameters.IncludeReferences ?? ShouldIncludeReferences(parameters);
        return await AnalysisToolCall.ExecuteRouted(
            assemblyRoute,
            new AnalysisToolCallRequest(
                new AnalysisTargetRequest(parameters.TargetPath),
                new AnalysisToolDispatch(
                    AssemblySessionCall: lease => InspectAssemblyTool.ExecuteAsync(
                        lease,
                        new InspectAssemblyArguments(
                            lease.CanonicalPath,
                            parameters.Namespace,
                            parameters.TypeName,
                            parameters.MemberName,
                            parameters.PublicOnly,
                            parameters.MaxResults,
                            parameters.ExactTypeName,
                            parameters.MemberNames,
                            parameters.MaxMembers,
                            includeReferences,
                            parameters.MaxResponseBytes,
                            parameters.DetailLevel,
                            parameters.ContinuationToken)),
                    ExpandAssemblyReferences: includeReferences,
                    MaxResponseBytes: parameters.MaxResponseBytes,
                    DetailLevel: parameters.DetailLevel,
                    Cursor: parameters.ContinuationToken),
                parameters.CancellationToken));
    }

    private static bool ShouldIncludeReferences(InspectAssemblyExecutionParameters parameters) =>
        string.IsNullOrWhiteSpace(parameters.Namespace)
        && string.IsNullOrWhiteSpace(parameters.TypeName)
        && string.IsNullOrWhiteSpace(parameters.MemberName)
        && (parameters.MemberNames is null || parameters.MemberNames.All(string.IsNullOrWhiteSpace));

    private sealed record InspectAssemblyExecutionParameters(
        string TargetPath,
        string? Namespace,
        string? TypeName,
        string? MemberName,
        bool PublicOnly,
        int MaxResults,
        bool ExactTypeName,
        string[]? MemberNames,
        int MaxMembers,
        bool? IncludeReferences,
        int MaxResponseBytes,
        string? DetailLevel,
        string? ContinuationToken,
        CancellationToken CancellationToken);

    private static readonly string InspectAssemblyDescription =
        "Oeffentliche API einer lokalen .NET-Assembly (.dll/.exe) metadata-only ueber Roslyn untersuchen. " +
        "namespace, typeName, memberName / memberNames filtern. exactTypeName: Exaktsuche fuer typeName. " +
        "publicOnly: Default true. " +
        "includeReferences: Referenzlisten/Sessions einbeziehen (Default: true ohne Type-/Member-Filter, sonst false). " +
        $"detailLevel: {McpEnumValues.AssemblyDetailLevelsHint}. " +
        "maxResults: Typen (Default 100, Max 1000). maxMembers: Member je Typ (Default 100, Max 1000).";

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
                string? continuationToken = null,
                CancellationToken ct = default) =>
            {
                var unknownError = TargetPathToolRegistrationOptions.RejectUnknownArguments(context);
                if (unknownError is not null) return unknownError;
                var detailLevelError = AssemblyAnalysisResponseLimits.ValidateDetailLevel(detailLevel);
                if (detailLevelError is not null) return detailLevelError;
                var effectiveCursor = continuationToken;
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

    private static readonly string FindAssemblyExtensionsDescription =
        "C#-Extension-Methoden einer lokalen .NET-Assembly (.dll/.exe) metadata-only ueber Roslyn finden. " +
        "receiverType: Empfaengertyp. extensionName, namespace: Filter. " +
        "includeReferences (Default false): Referenz-Assemblies einbeziehen. " +
        $"detailLevel: {McpEnumValues.AssemblyDetailLevelsHint}. " +
        "maxResults: Default 100, Max 1000.";

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
                string? continuationToken = null,
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
                        continuationToken,
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
        var detailLevelError = AssemblyAnalysisResponseLimits.ValidateDetailLevel(parameters.DetailLevel);
        if (detailLevelError is not null) return detailLevelError;
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
                            parameters.ContinuationToken),
                        parameters.CancellationToken),
                    ExpandAssemblyReferences: parameters.IncludeReferences,
                    MaxResponseBytes: parameters.MaxResponseBytes,
                    DetailLevel: parameters.DetailLevel,
                    Cursor: parameters.ContinuationToken),
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
        string? ContinuationToken,
        CancellationToken CancellationToken);

    private static readonly string GetAssemblyContextDescription =
        "Composite-Einstieg fuer lokale .NET-Assemblies (.dll/.exe): Identitaet, Scope, optional Metriken, Referenzen, Caller/Impact, Body und Klassenstruktur. " +
        "symbolIdentifier: DocCommentId, Typname oder Datei:Zeile:Spalte. " +
        "includeReferences (Default false): Referenz-Closure einbeziehen. " +
        "Flags: includeMetrics, includeReferences, includeCallers, includeImpact, includeBody, includeClassStructure. " +
        $"detailLevel: {McpEnumValues.AssemblyDetailLevelsHint}. " +
        "maxBodyLines (Cap 1000), maxCallers (Cap 200), depth (Cap 3), topN (Cap 200), maxResponseBytes, continuationToken.";
}
