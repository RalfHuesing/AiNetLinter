#nullable enable

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Assemblies.Analysis;
using AiNetLinter.Mcp.Projects;
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
        ProjectRegistry registry,
        AnalysisToolRoute assemblyRoute)
    {
        AddInspectAssembly(tools, registry, assemblyRoute);
        AddFindAssemblyExtensions(tools, registry, assemblyRoute);
        AddSearchAssembly(tools, registry, assemblyRoute);
        AddGetAssemblyContext(tools, registry, assemblyRoute);
    }

    private static void AddSearchAssembly(
        McpServerPrimitiveCollection<McpServerTool> tools,
        ProjectRegistry registry,
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
                string? operationToken = null,
                CancellationToken ct = default) =>
            {
                var unknownError = TargetPathToolRegistrationOptions.RejectUnknownArguments(context);
                if (unknownError is not null) return unknownError;
                var effectiveCursor = continuationToken;
                return await LongRunningAssemblyToolCall.ExecuteAsync(
                    new LongRunningAssemblyCallRequest(registry, context, targetPath, "search_assembly",
                        operationToken, lifetimeToken => AnalysisToolCall.ExecuteRouted(
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
                                lifetimeToken),
                            MaxResponseBytes: maxResponseBytes,
                            Cursor: effectiveCursor,
                            ApplyAssemblyWireBudget: false),
                        lifetimeToken))), ct);
            },
            TargetPathToolRegistrationOptions.AssemblyTool("search_assembly", SearchAssemblyDescription)));
    }

    private const string SearchAssemblyDescription =
        "Read-only Text-/Mustersuche im dekompilierten Root einer lokalen Assembly (.dll/.exe). " +
        "searchKind: 'text' [eigenes pattern], 'data_access' [DB/Datei], 'external_calls' [HTTP/RPC/Prozess]. " +
        "pattern: Suchbegriff (isRegex: auto/true/false). " +
        "declarationOnly: schliesst Kommentare/Strings/XML-Docs aus. " +
        "kind: 'method', 'type', 'property'; vollqualifizierte Typnamen aus inspect_assembly sind mit kind='type' suchbar. " +
        "Handoff-IDs direkt an get_symbol_body uebergeben. fileFilter: Glob oder Regex. " +
        "maxResults (Default 50, Cap 1000), contextLines (0-5), maxResponseBytes, continuationToken. " +
        "Bei operation=running denselben Aufruf mit operationToken fortsetzen.";

    private static void AddInspectAssembly(
        McpServerPrimitiveCollection<McpServerTool> tools,
        ProjectRegistry registry,
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
                string? operationToken = null,
                CancellationToken ct = default) =>
                ExecuteInspectAssemblyAsync(
                    context,
                    registry,
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
                        operationToken,
                        ct)),
            TargetPathToolRegistrationOptions.AssemblyTool("inspect_assembly", InspectAssemblyDescription)));
    }

    private static async Task<CallToolResult> ExecuteInspectAssemblyAsync(
        RequestContext<CallToolRequestParams> context,
        ProjectRegistry registry,
        AnalysisToolRoute assemblyRoute,
        InspectAssemblyExecutionParameters parameters)
    {
        var unknownError = TargetPathToolRegistrationOptions.RejectUnknownArguments(context);
        if (unknownError is not null) return unknownError;
        if (AssemblyAnalysisResponseLimits.ValidateDetailLevel(parameters.DetailLevel) is { } detailLevelError) return detailLevelError;
        var includeReferences = parameters.IncludeReferences ?? ShouldIncludeReferences(parameters);
        return await LongRunningAssemblyToolCall.ExecuteAsync(
            new LongRunningAssemblyCallRequest(registry, context, parameters.TargetPath, "inspect_assembly",
                parameters.OperationToken, lifetimeToken => AnalysisToolCall.ExecuteRouted(
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
                lifetimeToken))), parameters.CancellationToken);
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
        string? OperationToken,
        CancellationToken CancellationToken);

    private static readonly string InspectAssemblyDescription =
        "Oeffentliche API einer lokalen .NET-Assembly (.dll/.exe) metadata-only ueber Roslyn untersuchen. " +
        "namespace, typeName, memberName / memberNames filtern. exactTypeName: Exaktsuche fuer typeName. " +
        "publicOnly: Default true. " +
        "includeReferences: Referenzlisten/Sessions einbeziehen (Default: true ohne Type-/Member-Filter, sonst false). " +
        $"detailLevel: {McpEnumValues.AssemblyDetailLevelsHint}. " +
        "maxResults: Typen (Default 100, Max 1000). maxMembers: Member je Typ (Default 100, Max 1000). " +
        "Bei operation=running denselben Aufruf mit operationToken fortsetzen.";

    private static void AddFindAssemblyExtensions(
        McpServerPrimitiveCollection<McpServerTool> tools,
        ProjectRegistry registry,
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
                string? operationToken = null,
                CancellationToken ct = default) =>
            {
                var unknownError = TargetPathToolRegistrationOptions.RejectUnknownArguments(context);
                if (unknownError is not null) return unknownError;
                var detailLevelError = AssemblyAnalysisResponseLimits.ValidateDetailLevel(detailLevel);
                if (detailLevelError is not null) return detailLevelError;
                var effectiveCursor = continuationToken;
                return await LongRunningAssemblyToolCall.ExecuteAsync(
                    new LongRunningAssemblyCallRequest(registry, context, targetPath, "find_assembly_extensions",
                        operationToken, lifetimeToken => AnalysisToolCall.ExecuteRouted(
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
                        lifetimeToken))), ct);
            },
            TargetPathToolRegistrationOptions.AssemblyTool("find_assembly_extensions", FindAssemblyExtensionsDescription)));
    }

    private static readonly string FindAssemblyExtensionsDescription =
        "C#-Extension-Methoden einer lokalen .NET-Assembly (.dll/.exe) metadata-only ueber Roslyn finden. " +
        "receiverType: Empfaengertyp. extensionName, namespace: Filter. " +
        "includeReferences (Default false): Referenz-Assemblies einbeziehen. " +
        $"detailLevel: {McpEnumValues.AssemblyDetailLevelsHint}. " +
        "maxResults: Default 100, Max 1000. " +
        "Bei operation=running denselben Aufruf mit operationToken fortsetzen.";

    private static void AddGetAssemblyContext(
        McpServerPrimitiveCollection<McpServerTool> tools,
        ProjectRegistry registry,
        AnalysisToolRoute assemblyRoute)
    {
        tools.Add(McpServerTool.Create(
            (RequestContext<CallToolRequestParams> context,
                string targetPath,
                string? symbolIdentifier = null,
                bool includeMetrics = false,
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
                string? operationToken = null,
                CancellationToken ct = default) => ExecuteGetAssemblyContextAsync(
                    context,
                    registry,
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
                        operationToken,
                        ct)),
            TargetPathToolRegistrationOptions.AssemblyTool("get_assembly_context", GetAssemblyContextDescription)));
    }

    private static async Task<CallToolResult> ExecuteGetAssemblyContextAsync(
        RequestContext<CallToolRequestParams> context,
        ProjectRegistry registry,
        AnalysisToolRoute assemblyRoute,
        AssemblyContextExecutionParameters parameters)
    {
        var unknownError = TargetPathToolRegistrationOptions.RejectUnknownArguments(context);
        if (unknownError is not null) return unknownError;
        var detailLevelError = AssemblyAnalysisResponseLimits.ValidateDetailLevel(parameters.DetailLevel);
        if (detailLevelError is not null) return detailLevelError;
        return await LongRunningAssemblyToolCall.ExecuteAsync(
            new LongRunningAssemblyCallRequest(registry, context, parameters.TargetPath, "get_assembly_context",
                parameters.OperationToken, lifetimeToken => AnalysisToolCall.ExecuteRouted(
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
                        lifetimeToken),
                    ExpandAssemblyReferences: parameters.IncludeReferences,
                    MaxResponseBytes: parameters.MaxResponseBytes,
                    DetailLevel: parameters.DetailLevel,
                    Cursor: parameters.ContinuationToken),
                lifetimeToken))), parameters.CancellationToken);
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
        string? OperationToken,
        CancellationToken CancellationToken);

    private static readonly string GetAssemblyContextDescription =
        "Composite-Einstieg fuer lokale .NET-Assemblies (.dll/.exe): Identitaet, Scope, optional Metriken, Referenzen, Caller/Impact, Body und Klassenstruktur. " +
        "symbolIdentifier: Einzelwert; bevorzugt h:… aus vorheriger Toolantwort unverändert, alternativ Doc-ID, Position oder Typname. " +
        "includeMetrics: false (Default; dekompilierte Assembly-Metriken nicht verfügbar). " +
        "includeReferences (Default false): Referenz-Closure einbeziehen. " +
        "Flags: includeMetrics, includeReferences, includeCallers, includeImpact, includeBody, includeClassStructure. " +
        $"detailLevel: {McpEnumValues.AssemblyDetailLevelsHint}. " +
        "maxBodyLines (Cap 1000), maxCallers (Cap 200), depth (Cap 3), topN (Cap 200), maxResponseBytes, continuationToken. " +
        "Bei operation=running denselben Aufruf mit operationToken fortsetzen.";
}
