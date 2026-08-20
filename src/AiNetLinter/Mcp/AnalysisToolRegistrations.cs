#nullable enable

using System.Threading;
using System.Threading.Tasks;
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

namespace AiNetLinter.Mcp;

/// <summary>
/// Registriert die analyse-orientierten Tools (aktuell <c>get_violations</c>, <c>safeguard</c>,
/// <c>search_pattern</c>, <c>metrics_tree</c>, <c>metrics_lookup</c>, <c>pattern_detect</c>,
/// <c>find_magic_values</c>, <c>find_dead_code</c>, <c>get_feature_context</c> und <c>get_test_context</c>) an der von <see cref="McpServerOptionsFactory"/>
/// aufgebauten Tool-Collection.
/// </summary>
internal static class AnalysisToolRegistrations
{
    /// <summary>
    /// Fuegt <paramref name="tools"/> die analyse-orientierten Tools hinzu. Tools erreichen den
    /// resident gehaltenen <paramref name="mcpState"/> per Delegate-Closure - kein DI-Container
    /// (siehe <c>AiNetLinterRichtlinien.mdc</c> §2).
    /// </summary>
    internal static void Register(
        McpServerPrimitiveCollection<McpServerTool> tools,
        McpCodeGraphServer mcpState)
    {
        AddGetViolations(tools, mcpState);
        AddSafeguard(tools, mcpState);
        AddSearchPattern(tools, mcpState);
        AddMetricsTree(tools, mcpState);
        AddMetricsLookup(tools, mcpState);
        AddPatternDetect(tools, mcpState);
        AddFindMagicValues(tools, mcpState);
        AddFindDeadCode(tools, mcpState);
        AddGetFeatureContext(tools, mcpState);
        AddGetTestContext(tools, mcpState);
    }

    private static void AddGetViolations(
        McpServerPrimitiveCollection<McpServerTool> tools,
        McpCodeGraphServer mcpState)
    {
        tools.Add(McpServerTool.Create(
            (string? scopeFilter = null, int maxResults = GetViolationsScanner.DefaultMaxResults, int contextLines = 2, bool includeSnippet = false, CancellationToken ct = default) =>
                GetViolationsTool.ExecuteAsync(mcpState, new GetViolationsToolExecutionOptions(scopeFilter, maxResults, contextLines, includeSnippet), ct),
            new McpServerToolCreateOptions
            {
                Name = "get_violations",
                Description = GetViolationsDescription,
            }));
    }

    private const string GetViolationsDescription =
        "Wann nutzen: aktuelle Lint-Regelverstoesse der Solution abfragen — nach jedem Edit " +
        "erneut aufrufbar, kein Disk-Cache. scopeFilter (Projekt-Name oder Pfad-Substring) " +
        "grenzt auf einen Teilbereich ein, maxResults begrenzt die Trefferliste (Default 50). " +
        "includeSnippet=true gibt den relevanten Quellcode-Ausschnitt mit (contextLines 0-5, Default 2).";

    private static void AddSafeguard(
        McpServerPrimitiveCollection<McpServerTool> tools,
        McpCodeGraphServer mcpState)
    {
        tools.Add(McpServerTool.Create(
            (string? scopeFilter = null, double minScore = SafeguardScanner.DefaultMinScoreThreshold, int maxViolations = SafeguardScanner.DefaultMaxRemediationEntries, CancellationToken ct = default) =>
                SafeguardTool.ExecuteAsync(mcpState, scopeFilter, minScore, maxViolations, ct),
            new McpServerToolCreateOptions
            {
                Name = "safeguard",
                Description = SafeguardDescription,
            }));
    }

    private const string SafeguardDescription =
        "Wann nutzen: Quality-Gate-Wert vor CI-Merge pruefen — deterministischer " +
        "0-10-Score + Pass/Fail-Threshold + Top-Violations + Remediation-Hints fuer " +
        "die geladene Solution. scopeFilter (Projekt-Name oder Pfad-Substring) " +
        "grenzt auf einen Teilbereich ein, minScore ueberschreibt den Default-Threshold " +
        "(8.0), maxViolations begrenzt die Top-Violations-Liste (Default 20).";

    private static void AddSearchPattern(
        McpServerPrimitiveCollection<McpServerTool> tools,
        McpCodeGraphServer mcpState)
    {
        tools.Add(McpServerTool.Create(
            (string? pattern = null, bool isRegex = false, int maxResults = 50, CancellationToken ct = default) =>
                SearchPatternTool.ExecuteAsync(mcpState, pattern, isRegex, maxResults, ct),
            new McpServerToolCreateOptions
            {
                Name = "search_pattern",
                Description = SearchPatternDescription,
            }));
    }

    private const string SearchPatternDescription =
        "Wann nutzen: Fallback fuer Namen/Strings ausserhalb des C#-Symbolgraphs (z. B. " +
        "JS-Funktion, Razor-Komponente, WPF-Element) oder allgemeine Textsuche. isRegex=true " +
        "fuer Regex statt case-insensitive Substring.";

    private static void AddMetricsTree(
        McpServerPrimitiveCollection<McpServerTool> tools,
        McpCodeGraphServer mcpState)
    {
        tools.Add(McpServerTool.Create(
            (string? root = null, string? mode = null, int depth = 1, int topN = 10, string? fileFilter = null, CancellationToken ct = default) =>
                MetricsTreeTool.ExecuteAsync(mcpState, new MetricsTreeToolArgs(root, mode, depth, topN, fileFilter), ct),
            new McpServerToolCreateOptions
            {
                Name = "metrics_tree",
                Description = MetricsTreeDescription,
            }));
    }

    private const string MetricsTreeDescription =
        "Wann nutzen: Verzeichnishierarchie einer unbekannten/grossen Codebase Ebene fuer Ebene " +
        "erkunden statt Komplett-Dump zu lesen — aggregierte Werte pro Knoten + sortierte " +
        "Top-N-Kinder. mode: code_size, comment_density, violation_density, complexity. " +
        "root grenzt auf einen Teilbaum ein (Default: Solution-Root), depth (1-5) begrenzt die " +
        "Baumtiefe, top_n die sichtbaren Kinder pro Ebene, file_filter (Regex) auf den Pfad.";

    private static void AddMetricsLookup(
        McpServerPrimitiveCollection<McpServerTool> tools,
        McpCodeGraphServer mcpState)
    {
        tools.Add(McpServerTool.Create(
            (string? symbolIdentifier = null, string[]? symbolIdentifiers = null, CancellationToken ct = default) =>
                MetricsLookupTool.ExecuteAsync(mcpState, symbolIdentifier, symbolIdentifiers, ct),
            new McpServerToolCreateOptions
            {
                Name = "metrics_lookup",
                Description = MetricsLookupDescription,
            }));
    }

    private const string MetricsLookupDescription =
        "Wann nutzen: punktgenaue Metriken (LOC, zyklomatische/kognitive Komplexitaet, " +
        "Parameteranzahl, AI-Context-Footprint, Member-Statistiken) und Schwellwert-Abgleich " +
        "fuer ein oder mehrere C#-Symbole (Batch-Support in 1 Turn) abrufen. " +
        "symbolIdentifier (einzeln) ODER symbolIdentifiers (Array fuer Batch): " +
        "DocCommentId (\"M:Namespace.Class.Method\"), \"Datei.cs:Zeile:Spalte\", " +
        "\"Datei.cs:Zeile\" oder qualifizierter Name.";

    private static void AddPatternDetect(
        McpServerPrimitiveCollection<McpServerTool> tools,
        McpCodeGraphServer mcpState)
    {
        tools.Add(McpServerTool.Create(
            (string[]? patterns = null, string? scopeFilter = null, int maxResultsPerPattern = PatternDetectScanner.DefaultMaxResultsPerPattern, CancellationToken ct = default) =>
                PatternDetectTool.ExecuteAsync(mcpState, patterns, scopeFilter, maxResultsPerPattern, ct),
            new McpServerToolCreateOptions
            {
                Name = "pattern_detect",
                Description = PatternDetectDescription,
            }));
    }

    private const string PatternDetectDescription =
        "Wann nutzen: Solution-weite Audit-Suche nach Code-Patterns (God-Classes, async-void, " +
        "lange Methoden, Public-API ohne Doc, leere Catch-Bloecke, Feature-Envy/Middle-Man) " +
        "statt der flachen Datei-Liste von get_violations — nach Pattern-Kategorie gruppiert. " +
        "patterns (Default: alle 6) grenzt auf Pattern-IDs ein (god-class, async-void, " +
        "long-method, public-without-doc, empty-catch, feature-envy), scopeFilter " +
        "(Projekt-Name oder Pfad-Substring) grenzt auf einen Teilbereich ein, " +
        "maxResultsPerPattern begrenzt die Trefferliste je Pattern (Default 20).";

    private static void AddFindMagicValues(
        McpServerPrimitiveCollection<McpServerTool> tools,
        McpCodeGraphServer mcpState)
    {
        tools.Add(McpServerTool.Create(
            (
                string? scopeFilter = null,
                string? valueType = "all",
                string? categoryFilter = "all",
                int minOccurrences = 1,
                int maxResults = FindMagicValuesScanner.DefaultMaxResults,
                int[]? ignoreNumbers = null,
                bool includeTests = false,
                bool includeSuppressed = false,
                bool changedOnly = false,
                CancellationToken ct = default) =>
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
                return FindMagicValuesTool.ExecuteAsync(mcpState, effective, ct);
            },
            new McpServerToolCreateOptions
            {
                Name = "find_magic_values",
                Description = FindMagicValuesDescription,
            }));
    }

    private const string FindMagicValuesDescription =
        "Wann nutzen: On-Demand-Audit nach Magic Values (Strings, Zahlen, URLs, Pfaden, " +
        "Timeouts, Format-Strings, Schwellenwerten, HTTP-Statuscodes) in C#-Quellcode. " +
        "valueType (Default 'all': strings, numbers, all) filtert nach Literal-Datentyp, " +
        "categoryFilter (Default 'all': config_candidates, constant_candidates, " +
        "enum_candidates, nameof_candidates, localization_candidates, standard_candidates, " +
        "security_candidates) filtert nach fachlichem Refactoring-Ziel, " +
        "minOccurrences (Default 1 — auch Einzelvorkommen), maxResults (Default 50), " +
        "ignoreNumbers (optional) ergaenzt die Trivial-Liste um projektspezifische Zahlen, " +
        "includeTests (Default false), includeSuppressed (Default false; No-op in aktueller " +
        "Version — Suppression-Logik kommt in einer Folgeversion), changedOnly (Default false; " +
        "No-op in aktueller Version), scopeFilter (Projekt-Name oder Pfad-Substring).";

    private static void AddFindDeadCode(
        McpServerPrimitiveCollection<McpServerTool> tools,
        McpCodeGraphServer mcpState)
    {
        tools.Add(McpServerTool.Create(
            (
                string? accessibility = "private_internal",
                string? confidence = "both",
                string? kind = "all",
                string? scopeFilter = null,
                bool includeTests = false,
                string? mode = "members",
                int maxResults = 50,
                CancellationToken ct = default) =>
            {
                var effective = new FindDeadCodeToolArgs(
                    Accessibility: accessibility,
                    Confidence: confidence,
                    Kind: kind,
                    ScopeFilter: scopeFilter,
                    IncludeTests: includeTests,
                    Mode: mode,
                    MaxResults: maxResults);
                return FindDeadCodeTool.ExecuteAsync(mcpState, effective, ct);
            },
            new McpServerToolCreateOptions
            {
                Name = "find_dead_code",
                Description = FindDeadCodeDescription,
            }));
    }

    private const string FindDeadCodeDescription =
        "Wann nutzen: Solution nach unreferenziertem/totem Code durchleuchten — findet ungenutzte " +
        "Typen, Methoden, Properties, Felder und Events mit Vertrauensstufen (high fuer direkt " +
        "entfernbaren privaten/internen Code, low fuer Public-API/Framework-Kandidaten). " +
        "accessibility (Default 'private_internal': all, private, internal, public, private_internal), " +
        "confidence (Default 'both': both, high, low), kind (Default 'all': all, type, class, method, " +
        "field, property, event, delegate), scopeFilter (Projekt-Name oder Pfad-Substring), " +
        "includeTests (Default false), mode (Default 'members': members, locals, both), maxResults (Default 50).";

    private static void AddGetFeatureContext(
        McpServerPrimitiveCollection<McpServerTool> tools,
        McpCodeGraphServer mcpState)
    {
        tools.Add(McpServerTool.Create(
            (string? symbolIdentifier = null, string? symbol = null, bool includeCallers = true, bool includeTests = true, bool includeMetrics = true, bool includeViolations = true, int maxCallers = 10, int maxTests = 10, CancellationToken ct = default) =>
                GetFeatureContextTool.ExecuteAsync(mcpState, new FeatureContextOptions(symbol, symbolIdentifier, includeCallers, includeTests, includeMetrics, includeViolations, maxCallers, maxTests), ct),
            new McpServerToolCreateOptions
            {
                Name = "get_feature_context",
                Description = GetFeatureContextDescription,
            }));
    }

    private const string GetFeatureContextDescription =
        "Wann nutzen: Composite One-Shot-Exploration fuer ein beliebiges C#-Symbol vor Edits oder Refactorings — " +
        "buendelt 5 Dimensionen (Deklaration, Metriken & Budget, direkte Aufrufer, statische Test-Zuordnung und Linter-Violations) " +
        "in einem einzigen residenten Aufruf. symbolIdentifier (oder symbol) akzeptiert 'Namespace.Klasse.Methode', 'Datei.cs:Zeile' oder DocCommentId. " +
        "Teilbereiche koennen ueber includeCallers, includeTests, includeMetrics, includeViolations zu-/abgewaehlt werden. " +
        "maxCallers und maxTests steuern das Limit (Default 10, Cap 50).";

    private static void AddGetTestContext(
        McpServerPrimitiveCollection<McpServerTool> tools,
        McpCodeGraphServer mcpState)
    {
        tools.Add(McpServerTool.Create(
            (string? symbol = null, string? symbolIdentifier = null, int maxResults = 30, CancellationToken ct = default) =>
                GetTestContextTool.ExecuteAsync(mcpState, new TestContextOptions(symbol, symbolIdentifier, maxResults), ct),
            new McpServerToolCreateOptions
            {
                Name = "get_test_context",
                Description = GetTestContextDescription,
            }));
    }

    private const string GetTestContextDescription =
        "Wann nutzen: Test-Dateien, Test-Klassen und Test-Methoden fuer ein gegebenes Produktions-Symbol (Klasse, Methode, Datei.cs:Zeile) abfragen. " +
        "symbol (oder symbolIdentifier) spezifiziert das Ziel-Symbol, maxResults (Default 30) begrenzt die Anzahl Testdateien. " +
        "Liefert statische Zuordnungsgruende, Test-Kategorien (Unit/Integration), kopierbare dotnet test Filterbefehle und einen Hinweis, wenn keine Tests statisch zugeordnet wurden.";
}
