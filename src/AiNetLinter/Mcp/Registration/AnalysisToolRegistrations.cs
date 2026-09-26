#nullable enable

using System.Threading;
using System.Threading.Tasks;
using System.ComponentModel;
using System;
using System.Text.Json;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Projects;
using AiNetLinter.Mcp.Scope;
using AiNetLinter.Mcp.Tools;
using AiNetLinter.Mcp.Tools.Analysis;
using AiNetLinter.Mcp.Tools.FeatureContext;
using AiNetLinter.Mcp.Tools.MetricsLookup;
using AiNetLinter.Mcp.Tools.MetricsTree;
using AiNetLinter.Mcp.Tools.PatternDetect;
using AiNetLinter.Mcp.Tools.TestContext;
using AiNetLinter.Mcp.Tools.Verify;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace AiNetLinter.Mcp.Registration;

/// <summary>
/// Registriert die analyse-orientierten Tools an der von <see cref="McpServerOptionsFactory"/>
/// aufgebauten Tool-Collection.
/// </summary>
internal static class AnalysisToolRegistrations
{
    /// <summary>
    /// Fuegt <paramref name="tools"/> die analyse-orientierten Tools hinzu. Tools erreichen die
    /// residente Instanz ihres Keys per Lease-Closure - kein DI-Container
    /// (siehe <c>AiNetLinter-Richtlinien.mdc</c> §2).
    /// </summary>
    internal static void Register(
        McpServerPrimitiveCollection<McpServerTool> tools,
        ProjectRegistry registry,
        AnalysisToolRoute? targetRoute = null)
    {
        AddVerify(tools, registry);
        AddGetVerifyAdvisories(tools, registry);
        AddSearchPattern(tools, registry);
        AddMetricsTree(tools, registry, targetRoute);
        AddMetricsLookup(tools, registry, targetRoute);
        AddPatternDetect(tools, registry);
        AddGetFeatureContext(tools, registry);
        AddGetTestContext(tools, registry);
    }

    private static void AddGetVerifyAdvisories(
        McpServerPrimitiveCollection<McpServerTool> tools,
        ProjectRegistry registry)
    {
        tools.Add(McpServerTool.Create(
            async (RequestContext<CallToolRequestParams> context, string targetPath, string category, string? continuationToken = null, string? operationToken = null, CancellationToken ct = default) =>
            {
                var unknownError = TargetPathToolRegistrationOptions.RejectUnknownArguments(context);
                if (unknownError is not null) return unknownError;
                if (!string.Equals(category, GetVerifyAdvisoriesTool.DeadCodeCategory, StringComparison.Ordinal))
                {
                    return GetVerifyAdvisoriesTool.InvalidCategory(category);
                }
                if (!VerifyContract.TryValidateSourceSolutionTarget(targetPath, out var targetError)) return VerifyResponseFormatter.Error(
                    targetError!.Code, targetError.Message, targetError.Recovery, "$.targetPath");
                if (continuationToken is not null)
                {
                    if (operationToken is not null) return McpToolResults.InvalidArgument(
                        "continuationToken und operationToken dürfen nicht zusammen verwendet werden.",
                        "Den operationToken bis zum Scanergebnis verwenden; danach mit continuationToken die nächste Seite abrufen.");
                    return await ProjectToolCall.ExecuteAsync(registry, targetPath, lease =>
                        GetVerifyAdvisoriesTool.ExecuteAsync(lease.Server, ct, continuationToken));
                }
                return await LongRunningProjectToolCall.ExecuteAsync(
                    registry,
                    new LongRunningProjectCallRequest(targetPath, GetVerifyAdvisoriesTool.ToolName, category,
                        operationToken, (lease, lifetimeToken) => GetVerifyAdvisoriesTool.ExecuteAsync(lease.Server, lifetimeToken)),
                    ct);
            },
            TargetPathToolRegistrationOptions.SourceReadOnlyTool(
                GetVerifyAdvisoriesTool.ToolName,
                "Liefert Dead-Code-Advisories. Pflicht: targetPath (absoluter .sln/.slnx-Pfad), category=dead_code. Bei operation=running denselben Aufruf mit operationToken fortsetzen; danach optionale Folgeseiten mit continuationToken abrufen (Scan-Snapshot: 30 Minuten Leerlaufzeit). Bis 64 KiB (65.536 UTF-8-Bytes), nur ganze Einträge. listCompleteness und truncatedBy nennen den Ausgabestatus. symbolIdentifier direkt an find_references, get_symbol_body oder get_feature_context übergeben. Kandidaten einzeln gegenprüfen.")));
    }

    private static void AddVerify(
        McpServerPrimitiveCollection<McpServerTool> tools,
        ProjectRegistry registry)
    {
        tools.Add(McpServerTool.Create(
            async (RequestContext<CallToolRequestParams> context, string targetPath = "", [Description("changes (Default) oder solution")] string? scope = null, string? operationToken = null, CancellationToken ct = default) =>
            {
                var unknownError = TargetPathToolRegistrationOptions.RejectUnknownArguments(context);
                if (unknownError is not null) return VerifyResponseFormatter.Error(
                    "INVALID_ARGUMENT", "Der Request enthält ein unbekanntes Argument.", "Nur targetPath, scope und operationToken verwenden.");
                if (!VerifyContract.TryParseScope(scope, out var parsedScope)) return VerifyResponseFormatter.Error(
                    "INVALID_ARGUMENT", "scope muss changes oder solution sein.", "scope auf changes oder solution setzen.", "$.scope");
                if (!VerifyContract.TryValidateSourceSolutionTarget(targetPath, out var targetError)) return VerifyResponseFormatter.Error(
                    targetError!.Code, targetError.Message, targetError.Recovery, "$.targetPath");
                return await LongRunningProjectToolCall.ExecuteAsync(
                    registry,
                    new LongRunningProjectCallRequest(targetPath, VerifyContract.ToolName, parsedScope.ToString(),
                        operationToken, (lease, lifetimeToken) => VerifyTool.ExecuteAsync(lease.Server, parsedScope, lifetimeToken)),
                    ct);
            },
            TargetPathToolRegistrationOptions.SourceReadOnlyTool(
                VerifyContract.ToolName,
                "Fester Source-Quality-Gate: pass nur bei Score 10.0 und 0 Lint-Verstößen. scope: changes (Default) oder solution. Bei operation=running denselben Aufruf mit operationToken fortsetzen; das endgültige Gate-Ergebnis bleibt unverändert.")));
    }

    private static void AddSearchPattern(
        McpServerPrimitiveCollection<McpServerTool> tools,
        ProjectRegistry registry)
    {
        tools.Add(McpServerTool.Create(
            async (
                RequestContext<CallToolRequestParams> context,
                string targetPath,
                string? pattern = null,
                bool? isRegex = null,
                int maxResults = SearchPatternTool.DefaultMaxResults,
                int maxFiles = 0,
                int contextLines = 0,
                int maxResponseBytes = SearchPatternTool.DefaultMaxResponseBytes,
                string? scope = null,
                string[]? includePatterns = null,
                string[]? excludePatterns = null,
                bool enrichCSharp = false,
                string? scopeType = null,
                CancellationToken ct = default) =>
            {
                var unknownError = TargetPathToolRegistrationOptions.RejectUnknownArguments(context);
                if (unknownError is not null) return unknownError;
                var effectiveIncludes = includePatterns;

                return await ProjectAnalysisDispatcher.ExecuteAsync(
                    registry,
                    new AnalysisTargetRequest(targetPath),
                    lease => SearchPatternTool.ExecuteAsync(
                        lease.Server,
                        new SearchPatternToolArguments(
                            pattern,
                            isRegex,
                            maxResults,
                            maxFiles,
                            contextLines,
                            maxResponseBytes,
                            scope,
                            effectiveIncludes,
                            excludePatterns,
                            enrichCSharp,
                            scopeType),
                        ct));
            },
            TargetPathToolRegistrationOptions.SourceReadOnlyTool("search_pattern", SearchPatternDescription)));
    }

    private const string SearchPatternDescription =
        "Text- und Regex-Suche in Quell- und Nicht-C#-Dateien. " +
        "pattern: Suchbegriff (isRegex: auto/true/false). " +
        "scope: Relativer Pfad. includePatterns/excludePatterns: Globs. " +
        "maxResults (Default 20), contextLines (Default 0), maxResponseBytes (Default 8192, Cap 65536). " +
        "enrichCSharp: ergaenzt C#-Symbolevidenz (Default false).";

    private static void AddMetricsTree(
        McpServerPrimitiveCollection<McpServerTool> tools,
        ProjectRegistry registry,
        AnalysisToolRoute? targetRoute)
    {
        tools.Add(McpServerTool.Create(
            async (RequestContext<CallToolRequestParams> context, string targetPath, string? root = null, string? mode = "code_size", int depth = 1, int topN = 10, string? fileFilter = null, CancellationToken ct = default) =>
                await TargetPathToolRegistrationOptions.ExecuteWithUnknownArgumentGuardAsync(context, () => AnalysisToolCall.ExecuteRouted(
                    targetRoute!,
                    new AnalysisToolCallRequest(
                        new AnalysisTargetRequest(targetPath),
                        new AnalysisToolDispatch(
                            ProjectCall: lease => MetricsTreeTool.ExecuteAsync(lease.Server, new MetricsTreeToolArgs(root, mode, depth, topN, fileFilter), ct),
                            AssemblySessionCall: assemblyLease => MetricsTreeTool.ExecuteAsync(assemblyLease.Server, new MetricsTreeToolArgs(root, mode, depth, topN, fileFilter), ct)),
                        ct))),
            TargetPathToolRegistrationOptions.TargetPathReadOnlyTool("metrics_tree", MetricsTreeDescription)));
    }

    private const string MetricsTreeDescription =
        "Aggregierte Verzeichnis- und Dateimetriken. " +
        "mode: 'code_size' [Default], 'complexity'. depth: Teilbaumtiefe (Default 1). " +
        "topN: Top-Eintraege je Ebene (Default 10). fileFilter: Pfad-Glob.";

    private static void AddMetricsLookup(
        McpServerPrimitiveCollection<McpServerTool> tools,
        ProjectRegistry registry,
        AnalysisToolRoute? targetRoute)
    {
        tools.Add(McpServerTool.Create(
            async (RequestContext<CallToolRequestParams> context, string targetPath, string[]? symbolIdentifiers = null, CancellationToken ct = default) =>
                await TargetPathToolRegistrationOptions.ExecuteWithUnknownArgumentGuardAsync(context, () => AnalysisToolCall.ExecuteRouted(
                    targetRoute!,
                    new AnalysisToolCallRequest(
                        new AnalysisTargetRequest(targetPath),
                        new AnalysisToolDispatch(
                            ProjectCall: lease => MetricsLookupTool.ExecuteAsync(lease.Server, symbolIdentifiers, ct),
                            AssemblySessionCall: lease => MetricsLookupTool.ExecuteAsync(lease.Server, symbolIdentifiers, ct)),
                        ct))),
            TargetPathToolRegistrationOptions.TargetPathReadOnlyTool("metrics_lookup", MetricsLookupDescription)));
    }

    private const string MetricsLookupDescription =
        "Metriken und Schwellwert-Abgleich fuer ein oder mehrere C#-Symbole. symbolIdentifiers: Array; h:…-Werte aus vorherigen Toolantworten unverändert übernehmen, alternativ Doc-ID, Position oder qualifizierter Name. Navigierbare Einträge enthalten h:… für direkte Folgeparameter.";

    private static void AddPatternDetect(
        McpServerPrimitiveCollection<McpServerTool> tools,
        ProjectRegistry registry)
    {
        tools.Add(McpServerTool.Create(
            async (RequestContext<CallToolRequestParams> context, string targetPath, string[]? patterns = null, string? scopeFilter = null, int maxResultsPerPattern = PatternDetectScanner.DefaultMaxResultsPerPattern, string? operationToken = null, CancellationToken ct = default) =>
            {
                var unknownError = TargetPathToolRegistrationOptions.RejectUnknownArguments(context);
                if (unknownError is not null) return unknownError;
                var argumentsKey = JsonSerializer.Serialize(new { patterns, scopeFilter, maxResultsPerPattern });
                return await LongRunningProjectToolCall.ExecuteRoutedAsync(
                    registry,
                    new LongRunningRegistryCallRequest(
                        targetPath, "pattern_detect", argumentsKey, operationToken,
                        lifetimeToken => ProjectAnalysisDispatcher.ExecuteConfiguredAsync(
                            registry, new AnalysisTargetRequest(targetPath),
                            lease => PatternDetectTool.ExecuteAsync(lease.Server, patterns, scopeFilter,
                                maxResultsPerPattern, lifetimeToken))),
                    ct);
            },
            TargetPathToolRegistrationOptions.SourceReadOnlyTool("pattern_detect", PatternDetectDescription)));
    }

    private const string PatternDetectDescription =
        "Solution-weite, nach Pattern gruppierte Heuristiken wie God-Class, async-void oder lange Methoden; kein Ersatz fuer Regelverstosse. Bei operation=running denselben Aufruf mit operationToken fortsetzen.";

    private static void AddGetFeatureContext(
        McpServerPrimitiveCollection<McpServerTool> tools,
        ProjectRegistry registry)
    {
        tools.Add(McpServerTool.Create(
            async (RequestContext<CallToolRequestParams> context, string targetPath, string symbolIdentifier, int maxCallers = 10, int maxTests = 10, string scopeType = "all", bool includeGenerated = false, int maxResponseBytes = FeatureContextResponseBudget.DefaultMaxResponseBytes, CancellationToken ct = default) =>
            {
                var unknownError = TargetPathToolRegistrationOptions.RejectUnknownArguments(context);
                if (unknownError is not null) return unknownError;
                if (!McpScopeTypeValidator.TryParse(scopeType, out var parsedScope, out var fieldPath))
                {
                    return McpToolResults.InvalidArgument("scopeType muss 'all', 'production' oder 'tests' sein.", "Einen der veröffentlichten scopeType-Werte angeben.", fieldPath);
                }
                return await ProjectAnalysisDispatcher.ExecuteAsync(
                    registry,
                    new AnalysisTargetRequest(targetPath),
                    lease => GetFeatureContextTool.ExecuteAsync(
                        lease.Server,
                        new FeatureContextOptions(
                            SymbolIdentifier: symbolIdentifier,
                            MaxCallers: maxCallers,
                            MaxTests: maxTests,
                            MaxResponseBytes: maxResponseBytes,
                            Scope: new McpScopeInput(parsedScope, includeGenerated)),
                        ct),
                    new ProjectAnalysisExecutionOptions(
                        maxResponseBytes,
                        FeatureContextResponseBudget.ApplyFinal));
            },
            TargetPathToolRegistrationOptions.SourceReadOnlyTool("get_feature_context", GetFeatureContextDescription)));
    }

    private const string GetFeatureContextDescription =
        "Wann nutzen: One-Shot-Kontext fuer C#-Symbol (Deklaration, Metriken, Caller, Tests, Violations). " +
        "symbolIdentifier: Pflicht-Einzelwert; bevorzugt h:… aus vorheriger Toolantwort unverändert, alternativ Doc-ID oder Name. " +
        "Navigierbare Caller enthalten h:… für direkte Folgeparameter. " +
        "scopeType: 'all' (Default), 'production' oder 'tests'; includeGenerated: false (Default). " +
        "maxCallers (Default 10), maxTests (Default 10), maxResponseBytes.";

    private static void AddGetTestContext(
        McpServerPrimitiveCollection<McpServerTool> tools,
        ProjectRegistry registry)
    {
        tools.Add(McpServerTool.Create(
            async (RequestContext<CallToolRequestParams> context, string targetPath, string symbolIdentifier, int maxResults = 30, string scopeType = "all", bool includeGenerated = false, int maxResponseBytes = TestContextResponseBudget.DefaultMaxResponseBytes, CancellationToken ct = default) =>
            {
                var unknownError = TargetPathToolRegistrationOptions.RejectUnknownArguments(context);
                if (unknownError is not null) return unknownError;
                if (!McpScopeTypeValidator.TryParse(scopeType, out var parsedScope, out var fieldPath))
                {
                    return McpToolResults.InvalidArgument("scopeType muss 'all', 'production' oder 'tests' sein.", "Einen der veröffentlichten scopeType-Werte angeben.", fieldPath);
                }
                return await ProjectAnalysisDispatcher.ExecuteAsync(
                    registry,
                    new AnalysisTargetRequest(targetPath),
                    lease => GetTestContextTool.ExecuteAsync(
                        lease.Server,
                        new TestContextOptions(
                            SymbolIdentifier: symbolIdentifier,
                            MaxResults: maxResults,
                            MaxResponseBytes: maxResponseBytes,
                            Scope: new McpScopeInput(parsedScope, includeGenerated)),
                        ct),
                    new ProjectAnalysisExecutionOptions(
                        maxResponseBytes,
                        TestContextResponseBudget.ApplyFinal));
            },
            TargetPathToolRegistrationOptions.SourceReadOnlyTool("get_test_context", GetTestContextDescription)));
    }

    private const string GetTestContextDescription =
        "Wann nutzen: Statische Testkandidaten fuer ein C#-Symbol mit Zuordnung und Testkategorie. " +
        "symbolIdentifier: Pflicht-Einzelwert; bevorzugt h:… aus vorheriger Toolantwort unverändert, alternativ Doc-ID oder Name. " +
        "scopeType: 'all' (Default), 'production' oder 'tests'; includeGenerated: false (Default). " +
        "maxResults (Default 30), maxResponseBytes.";
}
