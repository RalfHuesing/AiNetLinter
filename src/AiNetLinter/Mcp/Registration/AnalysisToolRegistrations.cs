#nullable enable

using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Projects;
using AiNetLinter.Mcp.Scope;
using AiNetLinter.Mcp.Tools;
using AiNetLinter.Mcp.Tools.Analysis;
using AiNetLinter.Mcp.Tools.DeadCode;
using AiNetLinter.Mcp.Tools.FeatureContext;
using AiNetLinter.Mcp.Tools.MagicValues;
using AiNetLinter.Mcp.Tools.MetricsLookup;
using AiNetLinter.Mcp.Tools.MetricsTree;
using AiNetLinter.Mcp.Tools.PatternDetect;
using AiNetLinter.Mcp.Tools.Safeguard;
using AiNetLinter.Mcp.Tools.TestContext;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace AiNetLinter.Mcp.Registration;

/// <summary>
/// Registriert die analyse-orientierten Tools (aktuell <c>get_violations</c>, <c>safeguard</c>,
/// <c>search_pattern</c>, <c>metrics_tree</c>, <c>metrics_lookup</c>, <c>pattern_detect</c>,
/// <c>find_magic_values</c>, <c>find_dead_code</c>, <c>get_feature_context</c> und <c>get_test_context</c>) an der von <see cref="McpServerOptionsFactory"/>
/// aufgebauten Tool-Collection.
/// </summary>
internal static class AnalysisToolRegistrations
{
    /// <summary>
    /// Fuegt <paramref name="tools"/> die analyse-orientierten Tools hinzu. Tools erreichen die
    /// residente Instanz ihres Keys per Lease-Closure - kein DI-Container
    /// (siehe <c>AiNetLinterRichtlinien.mdc</c> §2).
    /// </summary>
    internal static void Register(
        McpServerPrimitiveCollection<McpServerTool> tools,
        ProjectRegistry registry,
        AnalysisToolRoute? targetRoute = null)
    {
        AddGetViolations(tools, registry);
        AddSafeguard(tools, registry);
        AddSearchPattern(tools, registry);
        AddMetricsTree(tools, registry, targetRoute);
        AddMetricsLookup(tools, registry, targetRoute);
        AddPatternDetect(tools, registry);
        AddFindMagicValues(tools, registry);
        AddFindDeadCode(tools, registry);
        AddGetFeatureContext(tools, registry);
        AddGetTestContext(tools, registry);
    }

    private static void AddGetViolations(
        McpServerPrimitiveCollection<McpServerTool> tools,
        ProjectRegistry registry)
    {
        tools.Add(McpServerTool.Create(
            async (RequestContext<CallToolRequestParams> context, string targetPath, string? scopeFilter = null, string? ruleId = null, string? minSeverity = null, int maxResults = GetViolationsScanner.DefaultMaxResults, int contextLines = 2, bool includeSnippet = false, CancellationToken ct = default) =>
            {
                var unknownError = TargetPathToolRegistrationOptions.RejectUnknownArguments(context);
                if (unknownError is not null) return unknownError;
                return await ProjectAnalysisDispatcher.ExecuteConfiguredAsync(
                    registry,
                    new AnalysisTargetRequest(targetPath),
                    lease => GetViolationsTool.ExecuteAsync(lease.Server, new GetViolationsToolExecutionOptions(scopeFilter, maxResults, contextLines, includeSnippet, ruleId, minSeverity), ct));
            },
            TargetPathToolRegistrationOptions.SourceReadOnlyTool("get_violations", GetViolationsDescription)));
    }

    private const string GetViolationsDescription =
        "Lint-Verstoesse der Source-Solution; optional nach Scope, Regel und Severity filtern. Snippets sind opt-in.";

    private static void AddSafeguard(
        McpServerPrimitiveCollection<McpServerTool> tools,
        ProjectRegistry registry)
    {
        tools.Add(McpServerTool.Create(
            async (RequestContext<CallToolRequestParams> context, string targetPath, string? scopeFilter = null, double minScore = SafeguardScanner.DefaultMinScoreThreshold, int maxViolations = SafeguardScanner.DefaultMaxRemediationEntries, CancellationToken ct = default) =>
            {
                var unknownError = TargetPathToolRegistrationOptions.RejectUnknownArguments(context);
                if (unknownError is not null) return unknownError;
                return await ProjectAnalysisDispatcher.ExecuteConfiguredAsync(
                    registry,
                    new AnalysisTargetRequest(targetPath),
                    lease => SafeguardTool.ExecuteAsync(lease.Server, scopeFilter, minScore, maxViolations, ct));
            },
            TargetPathToolRegistrationOptions.SourceReadOnlyTool("safeguard", SafeguardDescription)));
    }

    private const string SafeguardDescription =
        "Deterministisches Quality-Gate mit Score, Schwellwert und priorisierten Verstoessen fuer die Source-Solution.";

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
        "Text/Regex-Suche ausserhalb Symbolgraph; Default 20. enrichCSharp=true opt-in (ambiguous/unavailable). " +
        "Default 8192, Cap 65536.";

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
        "Wann nutzen: Verzeichnis-Metriken; mode (Default code_size), depth, topN, fileFilter begrenzen Teilbaum.";

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
        "Metriken und Schwellwert-Abgleich fuer ein oder mehrere C#-Symbole; akzeptiert Handoff, Doc-ID, Position oder qualifizierten Namen.";

    private static void AddPatternDetect(
        McpServerPrimitiveCollection<McpServerTool> tools,
        ProjectRegistry registry)
    {
        tools.Add(McpServerTool.Create(
            async (RequestContext<CallToolRequestParams> context, string targetPath, string[]? patterns = null, string? scopeFilter = null, int maxResultsPerPattern = PatternDetectScanner.DefaultMaxResultsPerPattern, CancellationToken ct = default) =>
            {
                var unknownError = TargetPathToolRegistrationOptions.RejectUnknownArguments(context);
                if (unknownError is not null) return unknownError;
                return await ProjectAnalysisDispatcher.ExecuteConfiguredAsync(
                    registry,
                    new AnalysisTargetRequest(targetPath),
                    lease => PatternDetectTool.ExecuteAsync(lease.Server, patterns, scopeFilter, maxResultsPerPattern, ct));
            },
            TargetPathToolRegistrationOptions.SourceReadOnlyTool("pattern_detect", PatternDetectDescription)));
    }

    private const string PatternDetectDescription =
        "Solution-weite, nach Pattern gruppierte Heuristiken wie God-Class, async-void oder lange Methoden; kein Ersatz fuer Regelverstosse.";

    private static void AddFindMagicValues(
        McpServerPrimitiveCollection<McpServerTool> tools,
        ProjectRegistry registry)
    {
        tools.Add(McpServerTool.Create(
            async (
                RequestContext<CallToolRequestParams> context,
                string targetPath,
                string? scopeFilter = null,
                string? valueType = "all",
                string? categoryFilter = "all",
                int minOccurrences = 2,
                int maxResults = FindMagicValuesScanner.DefaultMaxResults,
                int[]? ignoreNumbers = null,
                bool includeTests = false,
                bool includeSuppressed = false,
                bool changedOnly = false,
                CancellationToken ct = default) =>
            {
                var unknownError = TargetPathToolRegistrationOptions.RejectUnknownArguments(context);
                if (unknownError is not null) return unknownError;
                return await ProjectAnalysisDispatcher.ExecuteConfiguredAsync(
                    registry,
                    new AnalysisTargetRequest(targetPath),
                    lease =>
                    {
                        var effective = new FindMagicValuesToolArgs(
                            ScopeFilter: scopeFilter,
                            ValueType: valueType ?? "all",
                            CategoryFilter: categoryFilter ?? "all",
                            MinOccurrences: minOccurrences,
                            MaxResults: maxResults,
                            IgnoreNumbers: ignoreNumbers,
                            IncludeTests: includeTests,
                            IncludeSuppressed: includeSuppressed,
                            ChangedOnly: changedOnly);
                        return FindMagicValuesTool.ExecuteAsync(lease.Server, effective, ct);
                    });
            },
            TargetPathToolRegistrationOptions.SourceReadOnlyTool("find_magic_values", FindMagicValuesDescription)));
    }

    private const string FindMagicValuesDescription =
        "Wann nutzen: C#-Literal-Audit; Git-Diff und '// ainetlinter-disable MagicValues' filterbar. Keine Remediation.";

    private static void AddFindDeadCode(
        McpServerPrimitiveCollection<McpServerTool> tools,
        ProjectRegistry registry)
    {
        tools.Add(McpServerTool.Create(
            async (
                RequestContext<CallToolRequestParams> context,
                string targetPath,
                string? accessibility = "private_internal",
                string? confidence = "both",
                string? kind = "all",
                string? scopeFilter = null,
                bool includeTests = false,
                string? mode = "members",
                int maxResults = 50,
                CancellationToken ct = default) =>
            {
                var unknownError = TargetPathToolRegistrationOptions.RejectUnknownArguments(context);
                if (unknownError is not null) return unknownError;
                return await ProjectAnalysisDispatcher.ExecuteConfiguredAsync(
                    registry,
                    new AnalysisTargetRequest(targetPath),
                    lease =>
                    {
                        var effective = new FindDeadCodeToolArgs(
                            Accessibility: accessibility,
                            Confidence: confidence,
                            Kind: kind,
                            ScopeFilter: scopeFilter,
                            IncludeTests: includeTests,
                            Mode: mode,
                            MaxResults: maxResults);
                        return FindDeadCodeTool.ExecuteAsync(lease.Server, effective, ct);
                    });
            },
            TargetPathToolRegistrationOptions.SourceReadOnlyTool("find_dead_code", FindDeadCodeDescription)));
    }

    private const string FindDeadCodeDescription =
        "Kandidaten fuer unreferenzierten Code nach Sichtbarkeit, Confidence, Symbolart und Scope; Public- oder Framework-Code bleibt nur heuristisch.";

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
        "Wann nutzen: One-Shot-Kontext fuer C#-Symbol: Deklaration, Metriken, Caller, Tests, Violations. scopeType: 'all' (Default), 'production' oder 'tests'; includeGenerated: false (Default). Die Deklaration bleibt als markierter Seed sichtbar. Antwortbudget wahrt Einheiten.";

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
        "Wann nutzen: Statische Testkandidaten fuer C#-Symbol mit Zuordnung und Kategorie. scopeType: 'all' (Default), 'production' oder 'tests'; includeGenerated: false (Default). production liefert ohne definierte Production-Testevidenz eine echte leere Menge. Budget wahrt Kandidaten.";
}
