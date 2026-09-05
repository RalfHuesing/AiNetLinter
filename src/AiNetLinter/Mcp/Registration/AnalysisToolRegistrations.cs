#nullable enable

using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Projects;
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
            async (string targetType, string targetPath, string? scopeFilter = null, string? scope = null, string? path = null, string? ruleId = null, string? rule = null, string? minSeverity = null, int maxResults = GetViolationsScanner.DefaultMaxResults, int contextLines = 2, bool includeSnippet = false, CancellationToken ct = default) =>
            {
                var effectiveScope = scopeFilter ?? scope ?? path;
                var effectiveRule = ruleId ?? rule;
                return await ProjectAnalysisDispatcher.ExecuteAsync(
                    registry,
                    targetType,
                    targetPath,
                    lease => GetViolationsTool.ExecuteAsync(lease.Server, new GetViolationsToolExecutionOptions(effectiveScope, maxResults, contextLines, includeSnippet, effectiveRule, minSeverity), ct));
            },
            McpToolRegistrationOptions.ReadOnlyTool("get_violations", GetViolationsDescription)));
    }

    private const string GetViolationsDescription =
        "Wann nutzen: aktuelle Lint-Regelverstoesse der Solution abfragen — nach jedem Edit " +
        "erneut aufrufbar, kein Disk-Cache. scopeFilter (oder Aliase scope, path): Projekt-Name oder Pfad-Substring zur " +
        "Eingrenzung. ruleId (oder Alias rule): Filter auf bestimmte Regel (z. B. 'ANL0021'). minSeverity: 'info', 'warning' oder 'error'. " +
        "maxResults: Begrenzung der Trefferliste (Default 50). " +
        "includeSnippet=true gibt den Quellcode-Ausschnitt mit (contextLines 0-5, Default 2).";

    private static void AddSafeguard(
        McpServerPrimitiveCollection<McpServerTool> tools,
        ProjectRegistry registry)
    {
        tools.Add(McpServerTool.Create(
            async (string targetType, string targetPath, string? scopeFilter = null, string? scope = null, string? path = null, double minScore = SafeguardScanner.DefaultMinScoreThreshold, int maxViolations = SafeguardScanner.DefaultMaxRemediationEntries, CancellationToken ct = default) =>
            {
                var effectiveScope = scopeFilter ?? scope ?? path;
                return await ProjectAnalysisDispatcher.ExecuteAsync(
                    registry,
                    targetType,
                    targetPath,
                    lease => SafeguardTool.ExecuteAsync(lease.Server, effectiveScope, minScore, maxViolations, ct));
            },
            McpToolRegistrationOptions.ReadOnlyTool("safeguard", SafeguardDescription)));
    }

    private const string SafeguardDescription =
        "Wann nutzen: Quality-Gate-Wert vor CI-Merge pruefen — deterministischer " +
        "0-10-Score + Pass/Fail-Threshold + Top-Violations + Remediation-Hints fuer " +
        "die geladene Solution. scopeFilter (oder Aliase scope, path): Projekt-Name oder Pfad-Substring zur " +
        "Eingrenzung, minScore: Schwellwert (Default 8.0), maxViolations: Begrenzung " +
        "der Top-Violations-Liste (Default 20).";

    private static void AddSearchPattern(
        McpServerPrimitiveCollection<McpServerTool> tools,
        ProjectRegistry registry)
    {
        tools.Add(McpServerTool.Create(
            async (
                string targetType,
                string targetPath,
                string? pattern = null,
                string? query = null,
                string? searchPattern = null,
                bool? isRegex = null,
                int maxResults = 50,
                int maxFiles = 0,
                int contextLines = 0,
                int maxResponseBytes = 0,
                string? scope = null,
                string[]? includePatterns = null,
                string[]? excludePatterns = null,
                string? fileFilter = null,
                string? includePattern = null,
                bool enrichCSharp = false,
                string? scopeType = null,
                CancellationToken ct = default) =>
            {
                var effectivePattern = pattern ?? query ?? searchPattern;
                var effectiveIncludes = includePatterns;
                if (effectiveIncludes is null || effectiveIncludes.Length == 0)
                {
                    var singleInclude = fileFilter ?? includePattern;
                    if (!string.IsNullOrWhiteSpace(singleInclude))
                    {
                        effectiveIncludes = [singleInclude];
                    }
                }

                return await ProjectAnalysisDispatcher.ExecuteAsync(
                    registry,
                    targetType,
                    targetPath,
                    lease => SearchPatternTool.ExecuteAsync(
                        lease.Server,
                        new SearchPatternToolArguments(
                            effectivePattern,
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
            McpToolRegistrationOptions.ReadOnlyTool("search_pattern", SearchPatternDescription)));
    }

    private const string SearchPatternDescription =
        "Wann nutzen: Fallback fuer Namen/Strings ausserhalb des C#-Symbolgraphs (z. B. " +
        "JS-Funktionen, Razor-Komponenten, WPF-Elemente, Config-Eintraege) oder allgemeine Textsuche. " +
        "pattern: Suchtext oder Regex (Aliase: query, searchPattern). isRegex: optional (Default null = 'auto' mit automatischer Regex-Erkennung " +
        "und Promotion bei 0 Treffern; true = explizit Regex, false = explizit Plain-Substring). " +
        "scopeType: 'all' (Default), 'production' (schliesst Tests aus) oder 'tests'. " +
        "maxResults: Treffer-Limit (Default 50). maxFiles, contextLines und maxResponseBytes begrenzen " +
        "die strukturierte Nutzlast. scope, includePatterns (oder fileFilter als String) und excludePatterns steuern den Scope. " +
        "enrichCSharp=true reichert sichtbare C#-Treffer opt-in semantisch an (semantic-Feld; resolution: resolved, not_applicable, unknown, ambiguous, unavailable).";

    private static void AddMetricsTree(
        McpServerPrimitiveCollection<McpServerTool> tools,
        ProjectRegistry registry,
        AnalysisToolRoute? targetRoute)
    {
        tools.Add(McpServerTool.Create(
            async (string targetType, string targetPath, string? root = null, string? mode = "code_size", int depth = 1, int topN = 10, string? fileFilter = null, CancellationToken ct = default) =>
                await AnalysisToolCall.ExecuteRouted(
                    targetRoute!,
                    new AnalysisToolCallRequest(
                        new AnalysisTargetRequest(targetType, targetPath),
                        new AnalysisToolDispatch(
                            ProjectCall: lease => MetricsTreeTool.ExecuteAsync(lease.Server, new MetricsTreeToolArgs(root, mode, depth, topN, fileFilter), ct),
                            AssemblySessionCall: assemblyLease => MetricsTreeTool.ExecuteAsync(assemblyLease.Server, new MetricsTreeToolArgs(root, mode, depth, topN, fileFilter), ct)),
                        ct)),
            McpToolRegistrationOptions.TargetedReadOnlyTool("metrics_tree", MetricsTreeDescription)));
    }

    private const string MetricsTreeDescription =
        "Wann nutzen: Verzeichnishierarchie einer unbekannten/grossen Codebase Ebene fuer Ebene " +
        "erkunden statt Komplett-Dump zu lesen — aggregierte Werte pro Knoten + sortierte " +
        "Top-N-Kinder. mode: code_size [Default], comment_density, violation_density, complexity. " +
        "root: Teilbaum-Eingrenzung (Default: Root), depth: Baumtiefe (1-5, Default 1), " +
        "topN: sichtbare Kinder pro Ebene (Default 10), fileFilter: Regex-Filter auf den Pfad.";

    private static void AddMetricsLookup(
        McpServerPrimitiveCollection<McpServerTool> tools,
        ProjectRegistry registry,
        AnalysisToolRoute? targetRoute)
    {
        tools.Add(McpServerTool.Create(
            async (string targetType, string targetPath, string[]? symbolIdentifiers = null, string? symbolIdentifier = null, string? symbol = null, CancellationToken ct = default) =>
                await AnalysisToolCall.ExecuteRouted(
                    targetRoute!,
                    new AnalysisToolCallRequest(
                        new AnalysisTargetRequest(targetType, targetPath),
                        new AnalysisToolDispatch(
                            ProjectCall: lease => MetricsLookupTool.ExecuteAsync(lease.Server, ResolveMetricsLookupIdentifiers(symbolIdentifiers, symbolIdentifier, symbol), ct),
                            AssemblySessionCall: lease => MetricsLookupTool.ExecuteAsync(lease.Server, ResolveMetricsLookupIdentifiers(symbolIdentifiers, symbolIdentifier, symbol), ct)),
                        ct)),
            McpToolRegistrationOptions.TargetedReadOnlyTool("metrics_lookup", MetricsLookupDescription)));
    }

    private static string[]? ResolveMetricsLookupIdentifiers(string[]? symbolIdentifiers, string? symbolIdentifier, string? symbol) =>
        symbolIdentifiers is { Length: > 0 }
            ? symbolIdentifiers
            : !string.IsNullOrWhiteSpace(symbolIdentifier)
                ? [symbolIdentifier.Trim()]
                : !string.IsNullOrWhiteSpace(symbol)
                    ? [symbol.Trim()]
                    : null;

    private const string MetricsLookupDescription =
        "Wann nutzen: punktgenaue Metriken (LOC, zyklomatische/kognitive Komplexitaet, " +
        "Parameteranzahl, AI-Context-Footprint, Member-Statistiken) und Schwellwert-Abgleich " +
        "fuer ein oder mehrere C#-Symbole (Batch-Support in 1 Turn) abrufen. " +
        "symbolIdentifiers: Array von Symbol-IDs oder symbolIdentifier / symbol als String-Alias fuer genau ein Symbol: " +
        "DocCommentId (\"M:Namespace.Class.Method\"), \"Datei.cs:Zeile:Spalte\", " +
        "\"Datei.cs:Zeile\" oder qualifizierter Name. Liefert MetricsLookupBatchDto in structuredContent.";

    private static void AddPatternDetect(
        McpServerPrimitiveCollection<McpServerTool> tools,
        ProjectRegistry registry)
    {
        tools.Add(McpServerTool.Create(
            async (string targetType, string targetPath, string[]? patterns = null, string? pattern = null, string? scopeFilter = null, string? scope = null, string? path = null, int maxResultsPerPattern = PatternDetectScanner.DefaultMaxResultsPerPattern, CancellationToken ct = default) =>
            {
                var effectiveScope = scopeFilter ?? scope ?? path;
                var effectivePatterns = patterns ?? (pattern is not null ? [pattern] : null);
                return await ProjectAnalysisDispatcher.ExecuteAsync(
                    registry,
                    targetType,
                    targetPath,
                    lease => PatternDetectTool.ExecuteAsync(lease.Server, effectivePatterns, effectiveScope, maxResultsPerPattern, ct));
            },
            McpToolRegistrationOptions.ReadOnlyTool("pattern_detect", PatternDetectDescription)));
    }

    private const string PatternDetectDescription =
        "Wann nutzen: Solution-weite Audit-Suche nach Code-Patterns (God-Classes, async-void, " +
        "lange Methoden, Public-API ohne Doc, leere Catch-Bloecke, Feature-Envy/Middle-Man) " +
        "statt der flachen Datei-Liste von get_violations — nach Pattern-Kategorie gruppiert. " +
        "patterns (oder Alias pattern): Pattern-IDs (Default alle 6: god-class, async-void, long-method, public-without-doc, " +
        "empty-catch, feature-envy). scopeFilter (oder Aliase scope, path): Projekt-Name oder Pfad-Substring zur Eingrenzung, " +
        "maxResultsPerPattern: Begrenzung der Trefferliste je Pattern (Default 20).";

    private static void AddFindMagicValues(
        McpServerPrimitiveCollection<McpServerTool> tools,
        ProjectRegistry registry)
    {
        tools.Add(McpServerTool.Create(
            async (
                string targetType,
                string targetPath,
                string? scopeFilter = null,
                string? scope = null,
                string? path = null,
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
                var effectiveScope = scopeFilter ?? scope ?? path;
                return await ProjectAnalysisDispatcher.ExecuteAsync(
                    registry,
                    targetType,
                    targetPath,
                    lease =>
                    {
                        var effective = new FindMagicValuesToolArgs(
                            ScopeFilter: effectiveScope,
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
            McpToolRegistrationOptions.ReadOnlyTool("find_magic_values", FindMagicValuesDescription)));
    }

    private const string FindMagicValuesDescription =
        "Wann nutzen: On-Demand-Audit nach Magic Values (Strings, Zahlen, URLs, Pfaden, " +
        "Timeouts, Format-Strings, Schwellwerten, HTTP-Statuscodes) in C#-Quellcode. " +
        "valueType: Literal-Filter ('all' [Default], 'strings', 'numbers'). " +
        "categoryFilter: Refactoring-Kategorie ('all' [Default], 'config_candidates', 'constant_candidates', " +
        "'enum_candidates', 'nameof_candidates', 'localization_candidates', 'standard_candidates', 'security_candidates'). " +
        "minOccurrences: Mindestvorkommen (Default 2), maxResults: Begrenzung (Default 50). " +
        "ignoreNumbers: projektspezifische Ignorier-Zahlen. includeTests: Tests einbeziehen (Default false). " +
        "includeSuppressed: Fundstellen mit '// ainetlinter-disable MagicValues' einbeziehen (Default false). " +
        "changedOnly: Git-Diff-Einschraenkung auf geaenderte Dateien (Default false). " +
        "scopeFilter (oder Aliase scope, path): Projekt-Name oder Pfad-Substring zur Eingrenzung.";

    private static void AddFindDeadCode(
        McpServerPrimitiveCollection<McpServerTool> tools,
        ProjectRegistry registry)
    {
        tools.Add(McpServerTool.Create(
            async (
                string targetType,
                string targetPath,
                string? accessibility = "private_internal",
                string? confidence = "both",
                string? kind = "all",
                string? scopeFilter = null,
                string? scope = null,
                string? path = null,
                bool includeTests = false,
                string? mode = "members",
                int maxResults = 50,
                CancellationToken ct = default) =>
            {
                var effectiveScope = scopeFilter ?? scope ?? path;
                return await ProjectAnalysisDispatcher.ExecuteAsync(
                    registry,
                    targetType,
                    targetPath,
                    lease =>
                    {
                        var effective = new FindDeadCodeToolArgs(
                            Accessibility: accessibility,
                            Confidence: confidence,
                            Kind: kind,
                            ScopeFilter: effectiveScope,
                            IncludeTests: includeTests,
                            Mode: mode,
                            MaxResults: maxResults);
                        return FindDeadCodeTool.ExecuteAsync(lease.Server, effective, ct);
                    });
            },
            McpToolRegistrationOptions.ReadOnlyTool("find_dead_code", FindDeadCodeDescription)));
    }

    private const string FindDeadCodeDescription =
        "Wann nutzen: Solution nach unreferenziertem/totem Code durchleuchten — findet ungenutzte " +
        "Typen, Methoden, Properties, Felder und Events mit Vertrauensstufen (high fuer direkt " +
        "entfernbaren privaten/internen Code, low fuer Public-API/Framework-Kandidaten). " +
        "accessibility: 'private_internal' [Default], 'all', 'private', 'internal', 'public'. " +
        "confidence: 'both' [Default], 'high', 'low'. kind: 'all' [Default], 'type', 'class', 'method', " +
        "'field', 'property', 'event', 'delegate'. scopeFilter (oder Aliase scope, path): Projekt-Name oder Pfad-Substring. " +
        "includeTests: Tests einbeziehen (Default false). mode: 'members' [Default], 'locals', 'both'. maxResults: Begrenzung (Default 50).";

    private static void AddGetFeatureContext(
        McpServerPrimitiveCollection<McpServerTool> tools,
        ProjectRegistry registry)
    {
        tools.Add(McpServerTool.Create(
            async (string targetType, string targetPath, string? symbolIdentifier = null, string? symbol = null, string? identifier = null, string? name = null, bool includeCallers = true, bool includeTests = true, bool includeMetrics = true, bool includeViolations = true, int maxCallers = 10, int maxTests = 10, CancellationToken ct = default) =>
            {
                var effectiveIdentifier = symbolIdentifier ?? symbol ?? identifier ?? name;
                return await ProjectAnalysisDispatcher.ExecuteAsync(
                    registry,
                    targetType,
                    targetPath,
                    lease => GetFeatureContextTool.ExecuteAsync(
                        lease.Server,
                        new FeatureContextOptions(
                            Symbol: effectiveIdentifier,
                            SymbolIdentifier: effectiveIdentifier,
                            IncludeCallers: includeCallers,
                            IncludeTests: includeTests,
                            IncludeMetrics: includeMetrics,
                            IncludeViolations: includeViolations,
                            MaxCallers: maxCallers,
                            MaxTests: maxTests),
                        ct));
            },
            McpToolRegistrationOptions.ReadOnlyTool("get_feature_context", GetFeatureContextDescription)));
    }

    private const string GetFeatureContextDescription =
        "Wann nutzen: Composite One-Shot-Exploration fuer ein beliebiges C#-Symbol vor Edits oder Refactorings — " +
        "buendelt 5 Dimensionen (Deklaration, Metriken & Budget, direkte Aufrufer, statische Test-Zuordnung und Linter-Violations) " +
        "in einem einzigen residenten Aufruf. symbolIdentifier (primaer; Aliase symbol, identifier, name): 'Namespace.Klasse.Methode', 'Datei.cs:Zeile' oder DocCommentId. " +
        "includeCallers, includeTests, includeMetrics, includeViolations: Teilbereiche (Default true). " +
        "maxCallers und maxTests: Limits (Default 10, Cap 50).";

    private static void AddGetTestContext(
        McpServerPrimitiveCollection<McpServerTool> tools,
        ProjectRegistry registry)
    {
        tools.Add(McpServerTool.Create(
            async (string targetType, string targetPath, string? symbolIdentifier = null, string? symbol = null, string? identifier = null, string? name = null, int maxResults = 30, CancellationToken ct = default) =>
            {
                var effectiveIdentifier = symbolIdentifier ?? symbol ?? identifier ?? name;
                return await ProjectAnalysisDispatcher.ExecuteAsync(
                    registry,
                    targetType,
                    targetPath,
                    lease => GetTestContextTool.ExecuteAsync(
                        lease.Server,
                        new TestContextOptions(
                            Symbol: effectiveIdentifier,
                            SymbolIdentifier: effectiveIdentifier,
                            MaxResults: maxResults),
                        ct));
            },
            McpToolRegistrationOptions.ReadOnlyTool("get_test_context", GetTestContextDescription)));
    }

    private const string GetTestContextDescription =
        "Wann nutzen: Test-Dateien, Test-Klassen und Test-Methoden fuer ein gegebenes Produktions-Symbol " +
        "(Klasse, Methode, Datei.cs:Zeile oder DocCommentId) abfragen. symbolIdentifier (primaer; Aliase symbol, identifier, name): Ziel-Symbol, " +
        "maxResults: Begrenzung der Testdateien (Default 30). Liefert statische Zuordnungsgruende, Test-Kategorien " +
        "(Unit/Integration), kopierbare dotnet test Filterbefehle und Hinweis bei fehlender Zuordnung.";
}
