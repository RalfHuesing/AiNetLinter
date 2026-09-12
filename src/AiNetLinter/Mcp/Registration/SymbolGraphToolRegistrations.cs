#nullable enable

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Assemblies.Analysis;
using AiNetLinter.Mcp.Projects;
using AiNetLinter.Mcp.Scope;
using AiNetLinter.Mcp.Tools;
using AiNetLinter.Mcp.Tools.CallTree;
using AiNetLinter.Mcp.Tools.DependencyGraph;
using AiNetLinter.Mcp.Tools.SymbolGraph;
using AiNetLinter.Mcp.Validation;
using AiNetLinter.Mcp.Tools.TypeHierarchy;
using AiNetLinter.Mcp.Tools.TypeResolution;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace AiNetLinter.Mcp.Registration;

/// <summary>
/// Registriert die sechs reinen Symbolgraph-Tools (<c>find_symbol</c>, <c>find_references</c>,
/// <c>get_impact</c>, <c>get_type_hierarchy</c>, <c>get_call_tree</c>, <c>dependency_graph</c>) an
/// der von <see cref="McpServerOptionsFactory"/> aufgebauten Tool-Collection. Aus
/// <see cref="McpServerOptionsFactory"/> ausgelagert, damit dessen eigener <c>AIContextFootprint</c>
/// (siehe Linter-Regel <c>AIContextFootprint</c>) nicht mit jedem neu registrierten Tool waechst. Jedes Lambda ist
/// zielgebunden: <c>targetPath</c> ist Pflicht und wird am gemeinsamen
/// <see cref="AnalysisToolCall"/> validiert; die Route wird aus der Dateiendung bestimmt.
/// </summary>
internal static class SymbolGraphToolRegistrations
{
    /// <summary>
    /// Fuegt <paramref name="tools"/> die sechs Symbolgraph-Tools hinzu. Tools erreichen die
    /// residente Instanz ihres Keys per Lease-Closure - kein DI-Container
    /// (siehe <c>AiNetLinter-Richtlinien.mdc</c> §2).
    /// </summary>
    internal static void Register(
        McpServerPrimitiveCollection<McpServerTool> tools,
        AnalysisToolRoute targetRoute)
    {
        AddFindSymbol(tools, targetRoute);
        AddFindReferences(tools, targetRoute);
        AddGetCallTree(tools, targetRoute);
        AddGetImpact(tools, targetRoute);
        AddGetTypeHierarchy(tools, targetRoute);
        AddDependencyGraph(tools, targetRoute);
        AddResolveTypeOrigin(tools, targetRoute);
        AddFindImplementations(tools, targetRoute);
    }

    private static void AddFindSymbol(
        McpServerPrimitiveCollection<McpServerTool> tools,
        AnalysisToolRoute targetRoute)
    {
        tools.Add(McpServerTool.Create(
            async (RequestContext<CallToolRequestParams> context, string targetPath, string[]? namePatterns = null, string? pattern = null, string? kind = null, string scopeType = "all", bool includeGenerated = false, int maxResults = 50, bool includeReferences = false, int maxResponseBytes = FindSymbolTool.DefaultMaxResponseBytes, CancellationToken ct = default) =>
            {
                var patternError = FindSymbolTool.ValidatePatternArguments(
                    new FindSymbolPatternOptions(namePatterns, pattern));
                if (patternError is not null) return patternError;

                var patterns = FindSymbolTool.NormalizeNamePatterns(new FindSymbolPatternOptions(namePatterns, pattern));
                var namePatternsError = FindSymbolTool.ValidateNamePatterns(patterns);
                if (namePatternsError is not null) return namePatternsError;

                var maxResultsError = FindSymbolTool.ValidateMaxResults(maxResults);
                if (maxResultsError is not null) return maxResultsError;

                var kindError = FindSymbolTool.ValidateKind(kind);
                if (kindError is not null) return kindError;

                var scopeValidation = FindSymbolTool.ValidateScopeType(scopeType);
                if (scopeValidation.Error is not null) return scopeValidation.Error;

                return await TargetPathToolRegistrationOptions.ExecuteWithUnknownArgumentGuardAsync(context, () => AnalysisToolCall.ExecuteRouted(
                    targetRoute,
                    new AnalysisToolCallRequest(
                        new AnalysisTargetRequest(targetPath),
                        new AnalysisToolDispatch(
                            ProjectCall: lease => FindSymbolTool.ExecuteAsync(
                                new FindSymbolRequest(
                                    lease.Server,
                                    namePatterns,
                                    kind,
                                    maxResults,
                                    ct,
                                    pattern,
                                    scopeValidation.ScopeType,
                                    includeGenerated)),
                            AssemblySessionCall: lease => AssemblyFindSymbolTool.ExecuteAsync(
                                lease,
                                new AssemblyFindSymbolRequest(
                                    patterns.ToArray(),
                                    kind,
                                    maxResults,
                                    includeReferences,
                                    scopeValidation.ScopeType,
                                    includeGenerated),
                                ct),
                            ExpandAssemblyReferences: includeReferences,
                            MaxResponseBytes: maxResponseBytes,
                            PostNavigationResponseBudget: FindSymbolTool.ApplyFinalResponseBudget),
                        ct)));
            },
            TargetPathToolRegistrationOptions.TargetPathReadOnlyTool("find_symbol", FindSymbolDescription)));
    }

    private static readonly string FindSymbolDescription =
        "Fundstellen von C#-Symbolen per Namens-Substring finden. " +
        "namePatterns: Array von Mustern (max. 10) oder pattern fuer Einzelsuche. " +
        $"kind: optionaler Typfilter ({McpEnumValues.FindSymbolKindsHint}). " +
        "scopeType: 'all' (Default), 'production' oder 'tests'; includeGenerated: false (Default). " +
        "maxResults: Trefferbegrenzung (Default 50). " +
        "maxResponseBytes: Default 16 KiB, Cap 64 KiB. " +
        "includeReferences (Default false): bei Assemblies auch Referenzen durchsuchen.";

    private static void AddFindReferences(
        McpServerPrimitiveCollection<McpServerTool> tools,
        AnalysisToolRoute targetRoute)
    {
        tools.Add(McpServerTool.Create(
            async (RequestContext<CallToolRequestParams> context, string targetPath, string? symbolIdentifier = null, int maxResults = 50, int depth = 1, bool includeReferences = false, string scopeType = "all", bool includeGenerated = false, int maxResponseBytes = FindReferencesTool.DefaultMaxResponseBytes, CancellationToken ct = default) =>
            {
                var unknownError = TargetPathToolRegistrationOptions.RejectUnknownArguments(context);
                if (unknownError is not null) return unknownError;
                var scopeValidation = FindSymbolTool.ValidateScopeType(scopeType);
                if (scopeValidation.Error is not null) return scopeValidation.Error;
                return await AnalysisToolCall.ExecuteRouted(
                    targetRoute,
                    new AnalysisToolCallRequest(
                        new AnalysisTargetRequest(targetPath),
                        new AnalysisToolDispatch(
                            ProjectCall: lease => FindReferencesTool.ExecuteAsync(
                                lease.Server,
                                new FindReferencesRequest(
                                    symbolIdentifier,
                                    maxResults,
                                    depth,
                                    scopeValidation.ScopeType,
                                    includeGenerated,
                                    MaxResponseBytes: maxResponseBytes),
                                ct),
                            AssemblySessionCall: lease => AssemblyFindReferencesTool.ExecuteAsync(
                                lease,
                                new AssemblyFindReferencesRequest(
                                    symbolIdentifier,
                                    maxResults,
                                    depth,
                                    includeReferences,
                                    scopeValidation.ScopeType,
                                    includeGenerated,
                                    MaxResponseBytes: maxResponseBytes),
                                ct),
                            ExpandAssemblyReferences: includeReferences,
                            MaxResponseBytes: maxResponseBytes,
                            PostNavigationResponseBudget: FindReferencesTool.ApplyFinalResponseBudget,
                            ApplyAssemblyWireBudget: false),
                        ct));
            },
            TargetPathToolRegistrationOptions.TargetPathReadOnlyTool("find_references", FindReferencesDescription)));
    }

    private const string FindReferencesDescription =
        "Findet alle Aufrufstellen eines C#-Symbols, optional transitiv. " +
        "symbolIdentifier: \"M:Namespace.Klasse.Methode\", \"Datei.cs:Zeile:Spalte\", \"Datei.cs:Zeile\" oder \"Klasse.Methode\". " +
        "depth: Traversierungstiefe (Default 1, Cap 3, max. 200 Knoten). " +
        "maxResults: Trefferbegrenzung (Default 50). " +
        "scopeType: 'all' (Default), 'production' oder 'tests'; includeGenerated: false (Default). " +
        "includeReferences (Default false): bei Assemblies Referenzen einbeziehen.";

    private static void AddGetCallTree(
        McpServerPrimitiveCollection<McpServerTool> tools,
        AnalysisToolRoute targetRoute)
    {
        tools.Add(McpServerTool.Create(
            async (RequestContext<CallToolRequestParams> context, string targetPath, string? symbolIdentifier = null, int depth = 2, string? format = null, int topN = 10, string? direction = null, bool includeReferences = false, bool includeBcl = false, string scopeType = "all", bool includeGenerated = false, int maxResponseBytes = GetCallTreeTool.DefaultMaxResponseBytes, CancellationToken ct = default) =>
            {
                var unknownError = TargetPathToolRegistrationOptions.RejectUnknownArguments(context);
                if (unknownError is not null) return unknownError;
                var scopeValidation = FindSymbolTool.ValidateScopeType(scopeType);
                if (scopeValidation.Error is not null) return scopeValidation.Error;
                return await AnalysisToolCall.ExecuteRouted(
                    targetRoute,
                    new AnalysisToolCallRequest(
                        new AnalysisTargetRequest(targetPath),
                        new AnalysisToolDispatch(
                            ProjectCall: lease => GetCallTreeTool.ExecuteAsync(lease.Server, new GetCallTreeInput(symbolIdentifier, depth, format, topN, direction, IncludeBcl: includeBcl, ScopeType: scopeType, IncludeGenerated: includeGenerated, MaxResponseBytes: maxResponseBytes), ct),
                            AssemblySessionCall: lease => AssemblyGetCallTreeTool.ExecuteAsync(
                                lease,
                                new AssemblyGetCallTreeRequest(
                                    new GetCallTreeInput(symbolIdentifier, depth, format, topN, direction, IncludeBcl: includeBcl, ScopeType: scopeType, IncludeGenerated: includeGenerated, MaxResponseBytes: maxResponseBytes),
                                    includeReferences),
                                ct),
                            MaxResponseBytes: maxResponseBytes,
                            PostNavigationResponseBudget: CallGraphResponseBudget.ApplyFinalResponseBudget,
                            ExpandAssemblyReferences: includeReferences,
                            ApplyAssemblyWireBudget: false),
                        ct));
            },
            TargetPathToolRegistrationOptions.TargetPathReadOnlyTool("get_call_tree", GetCallTreeDescription)));
    }

    private const string GetCallTreeDescription =
        "Transitiver Aufrufer-/Aufgerufenen-Baum eines C#-Symbols als Eltern-Kind-Struktur. " +
        "symbolIdentifier: \"M:Namespace.Klasse.Methode\", \"Datei.cs:Zeile:Spalte\" oder \"Klasse.Methode\". " +
        "direction: 'incoming' [Default: wer ruft auf], 'outgoing' [wen ruft es auf], 'both'. " +
        "depth: Tiefe (Default 2, Cap 5). topN: Fan-Out je Ebene (Default 10, max. 250 Knoten). " +
        "format: 'ascii' [Default] oder 'mermaid'. " +
        "scopeType: 'all' (Default), 'production' oder 'tests'; includeGenerated: false (Default). " +
        "includeBcl: Framework-Symbole bei outgoing (Default false). " +
        "includeReferences (Default false): bei Assemblies Referenzen einbeziehen. " +
        "maxResponseBytes (Default 32768, Maximum 65536).";

    private static void AddGetImpact(
        McpServerPrimitiveCollection<McpServerTool> tools,
        AnalysisToolRoute targetRoute)
    {
        tools.Add(McpServerTool.Create(
            async (RequestContext<CallToolRequestParams> context, string targetPath, string? gitRef = null, string? symbolIdentifier = null, int maxResults = 50, int depth = 1,
                string? detailLevel = null,
                int maxChangedSymbols = ChangeContextContract.DefaultMaxChangedSymbols,
                int maxTestsPerSymbol = ChangeContextContract.DefaultMaxTestsPerSymbol,
                bool includeReferences = false,
                CancellationToken ct = default) =>
            {
                var unknownError = TargetPathToolRegistrationOptions.RejectUnknownArguments(context);
                if (unknownError is not null) return unknownError;
                return await AnalysisToolCall.ExecuteRouted(
                    targetRoute,
                    new AnalysisToolCallRequest(
                        new AnalysisTargetRequest(targetPath),
                        new AnalysisToolDispatch(ProjectCall: lease => GetImpactTool.ExecuteAsync(
                            lease.Server,
                            new GetImpactInput(gitRef, symbolIdentifier, maxResults, depth, detailLevel, maxChangedSymbols, maxTestsPerSymbol, includeReferences),
                            ct),
                            AssemblySessionCall: lease => GetImpactTool.ExecuteAsync(
                                lease,
                                new GetImpactInput(gitRef, symbolIdentifier, maxResults, depth, detailLevel, maxChangedSymbols, maxTestsPerSymbol, includeReferences),
                                ct),
                            ExpandAssemblyReferences: includeReferences),
                        ct));
            },
            TargetPathToolRegistrationOptions.TargetPathReadOnlyTool("get_impact", GetImpactDescription)));
    }

    private const string GetImpactDescription =
        "Analysiert die Auswirkung von Aenderungen (Git-Diff oder Einzelsymbol). " +
        "Ohne Parameter: uncommittete Aenderungen. Alternativ gitRef ODER symbolIdentifier angeben (nicht beide; Assemblies nur symbolIdentifier). " +
        "detailLevel: 'callers' [Default] oder 'change-context' (Git-Diff-Modus: geaenderte Symbole, Call-Sites, Tests, Violations, dotnet-test-Filter). " +
        "depth: Tiefe im Symbol-Modus (Default 1, Cap 3, max. 200 Knoten). " +
        "maxResults: Trefferlimit (Default 50). " +
        "maxChangedSymbols (Default 20, Cap 100), maxTestsPerSymbol (Default 10, Cap 50). " +
        "includeReferences (Default false): bei Assemblies Referenzen einbeziehen.";

    private static void AddGetTypeHierarchy(
        McpServerPrimitiveCollection<McpServerTool> tools,
        AnalysisToolRoute targetRoute)
    {
        tools.Add(McpServerTool.Create(
            async (RequestContext<CallToolRequestParams> context, string targetPath, string? symbolIdentifier = null, int maxResults = GetTypeHierarchyTool.DefaultMaxResults, string scopeType = "all", bool includeGenerated = false, int maxResponseBytes = GetTypeHierarchyTool.DefaultMaxResponseBytes, CancellationToken ct = default) =>
            {
                var unknownError = TargetPathToolRegistrationOptions.RejectUnknownArguments(context);
                if (unknownError is not null) return unknownError;
                var scopeValidation = FindSymbolTool.ValidateScopeType(scopeType);
                if (scopeValidation.Error is not null) return scopeValidation.Error;
                return await AnalysisToolCall.ExecuteRouted(
                    targetRoute,
                    new AnalysisToolCallRequest(
                        new AnalysisTargetRequest(targetPath),
                        new AnalysisToolDispatch(
                            ProjectCall: lease => GetTypeHierarchyTool.ExecuteAsync(
                                new GetTypeHierarchyRequest(
                                    lease.Server,
                                    symbolIdentifier,
                                    maxResults,
                                    scopeValidation.ScopeType,
                                    includeGenerated,
                                    ct,
                                    maxResponseBytes)),
                            AssemblySessionCall: lease => GetTypeHierarchyTool.ExecuteAsync(
                                new GetTypeHierarchyRequest(
                                    lease.Server,
                                    symbolIdentifier,
                                    maxResults,
                                    scopeValidation.ScopeType,
                                    includeGenerated,
                                    ct,
                                    maxResponseBytes)),
                            MaxResponseBytes: maxResponseBytes,
                            PostNavigationResponseBudget: GetTypeHierarchyTool.ApplyFinalResponseBudget,
                            ApplyAssemblyWireBudget: false),
                        ct));
            },
            TargetPathToolRegistrationOptions.TargetPathReadOnlyTool("get_type_hierarchy", GetTypeHierarchyDescription)));
    }

    private const string GetTypeHierarchyDescription =
        "Vererbungs- und Interface-Hierarchie eines Typs (Basisklassen, Interfaces, Subtypen, DI-Registrierungen). " +
        "symbolIdentifier: 'T:Namespace.Typ', 'Datei.cs:Zeile:Spalte' oder Typname. " +
        "maxResults: Limit abgeleiteter Typen (Default 50). " +
        "scopeType: 'all' (Default), 'production' oder 'tests'; includeGenerated: false (Default).";

    private static void AddDependencyGraph(
        McpServerPrimitiveCollection<McpServerTool> tools,
        AnalysisToolRoute targetRoute)
    {
        tools.Add(McpServerTool.Create(
            async (RequestContext<CallToolRequestParams> context, string targetPath, string? filePath = null, string? symbolIdentifier = null, string? direction = null,
                int depth = 1, int maxResults = 50, string scopeType = "all", bool includeGenerated = false,
                int maxResponseBytes = DependencyGraphTool.DefaultMaxResponseBytes, CancellationToken ct = default) =>
            {
                var unknownError = TargetPathToolRegistrationOptions.RejectUnknownArguments(context);
                if (unknownError is not null) return unknownError;
                return await AnalysisToolCall.ExecuteRouted(
                    targetRoute,
                    new AnalysisToolCallRequest(
                        new AnalysisTargetRequest(targetPath),
                        new AnalysisToolDispatch(
                            ProjectCall: lease => DependencyGraphTool.ExecuteAsync(
                                lease.Server,
                                new DependencyGraphInput(filePath, symbolIdentifier, direction, depth, maxResults, scopeType, includeGenerated, maxResponseBytes),
                                ct),
                            AssemblySessionCall: lease => DependencyGraphTool.ExecuteAsync(
                                lease.Server,
                                new DependencyGraphInput(filePath, symbolIdentifier, direction, depth, maxResults, scopeType, includeGenerated, maxResponseBytes),
                                ct),
                            MaxResponseBytes: maxResponseBytes,
                            PostNavigationResponseBudget: DependencyGraphTool.ApplyFinalResponseBudget),
                        ct));
            },
            TargetPathToolRegistrationOptions.TargetPathReadOnlyTool("dependency_graph", DependencyGraphDescription)));
    }

    private const string DependencyGraphDescription =
        "Semantischer Abhaengigkeitsgraph fuer Datei oder Typ (SemanticModel-Typreferenzen). " +
        "filePath (ganze Datei) ODER symbolIdentifier (Typ) angeben (nicht beide). " +
        "direction: 'incoming', 'outgoing', 'both' (Default). " +
        "depth: Traversierungstiefe (Default 1, Cap 3, max. 150 Dateien). " +
        "maxResults: Begrenzung der Kanten (Default 50). " +
        "scopeType: 'all' (Default), 'production' oder 'tests'; includeGenerated: false (Default). " +
        "maxResponseBytes (Default 24 KiB, Maximum 64 KiB).";

    private static void AddResolveTypeOrigin(
        McpServerPrimitiveCollection<McpServerTool> tools,
        AnalysisToolRoute targetRoute)
    {
        tools.Add(McpServerTool.Create(
            async (RequestContext<CallToolRequestParams> context, string targetPath, string typeName, CancellationToken ct = default) =>
                await TargetPathToolRegistrationOptions.ExecuteWithUnknownArgumentGuardAsync(context, () => AnalysisToolCall.ExecuteRouted(
                    targetRoute,
                    new AnalysisToolCallRequest(
                        new AnalysisTargetRequest(targetPath),
                        new AnalysisToolDispatch(
                            ProjectCall: lease => ResolveTypeOriginTool.ExecuteProjectAsync(lease.Server, typeName, ct),
                            AssemblySessionCall: lease => ResolveTypeOriginTool.ExecuteAssemblyAsync(lease, typeName, ct)),
                        ct))),
            TargetPathToolRegistrationOptions.TargetPathReadOnlyTool("resolve_type_origin", ResolveTypeOriginDescription)));
    }

    private const string ResolveTypeOriginDescription =
        "Ermittelt zu einem Typnamen die definierende Assembly (Name und Dateipfad der DLL), vollqualifizierten Namen und Symbol-Kind ueber Roslyn-Metadatenreferenzen.";

    private static void AddFindImplementations(
        McpServerPrimitiveCollection<McpServerTool> tools,
        AnalysisToolRoute targetRoute)
    {
        tools.Add(McpServerTool.Create(
            async (RequestContext<CallToolRequestParams> context, string targetPath, string? symbolIdentifier = null, int maxResults = FindImplementationsTool.DefaultMaxResults, string scopeType = "all", bool includeGenerated = false, int maxResponseBytes = FindImplementationsTool.DefaultMaxResponseBytes, CancellationToken ct = default) =>
            {
                var unknownError = TargetPathToolRegistrationOptions.RejectUnknownArguments(context);
                if (unknownError is not null) return unknownError;
                var scopeValidation = FindSymbolTool.ValidateScopeType(scopeType);
                if (scopeValidation.Error is not null) return scopeValidation.Error;
                return await AnalysisToolCall.ExecuteRouted(
                    targetRoute,
                    new AnalysisToolCallRequest(
                        new AnalysisTargetRequest(targetPath),
                        new AnalysisToolDispatch(
                            ProjectCall: lease => FindImplementationsTool.ExecuteAsync(
                                new FindImplementationsRequest(
                                    lease.Server,
                                    symbolIdentifier,
                                    maxResults,
                                    scopeValidation.ScopeType,
                                    includeGenerated,
                                    ct,
                                    maxResponseBytes)),
                            AssemblySessionCall: lease => FindImplementationsTool.ExecuteAsync(
                                new FindImplementationsRequest(
                                    lease.Server,
                                    symbolIdentifier,
                                    maxResults,
                                    scopeValidation.ScopeType,
                                    includeGenerated,
                                    ct,
                                    maxResponseBytes)),
                            MaxResponseBytes: maxResponseBytes,
                            PostNavigationResponseBudget: FindImplementationsTool.ApplyFinalResponseBudget,
                            ApplyAssemblyWireBudget: false),
                        ct));
            },
            TargetPathToolRegistrationOptions.TargetPathReadOnlyTool("find_implementations", FindImplementationsDescription)));
    }

    private const string FindImplementationsDescription =
        "Findet konkrete Implementierungen und Overrides von Interfaces, abstrakten Klassen, virtuellen Methoden oder Properties. " +
        "symbolIdentifier: 'IInterface', 'BaseClass.Method' oder 'M:Namespace.Klasse.Methode'. " +
        "maxResults: Trefferlimit (Default 50). " +
        "scopeType: 'all' (Default), 'production' oder 'tests'; includeGenerated: false (Default).";
}
