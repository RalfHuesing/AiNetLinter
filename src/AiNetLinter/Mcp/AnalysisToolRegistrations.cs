#nullable enable

using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Tools;
using AiNetLinter.Mcp.Tools.Analysis;
using AiNetLinter.Mcp.Tools.MetricsTree;
using AiNetLinter.Mcp.Tools.PatternDetect;
using AiNetLinter.Mcp.Tools.Safeguard;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace AiNetLinter.Mcp;

/// <summary>
/// Registriert die analyse-orientierten Tools (aktuell <c>get_violations</c>, <c>search_pattern</c>
/// und <c>metrics_tree</c>) an der von <see cref="McpServerOptionsFactory"/> aufgebauten
/// Tool-Collection. Aus <see cref="FileStructureToolRegistrations"/> ausgelagert, weil
/// <c>get_violations</c> durch den transitiven Pull-in aus <c>LinterEngine</c> +
/// <c>LinterAnalyzer</c> + allen Checkern den <c>AIContextFootprint</c> (siehe
/// <c>AiNetLinter.mdc</c>) der <see cref="FileStructureToolRegistrations"/>-Klasse ueber das
/// 2500-Limit getrieben hat. <c>search_pattern</c> wurde 002 hier angegliedert, weil es ebenfalls
/// datei-inhalts-basiert arbeitet (wie <c>get_violations</c>) und damit semantisch nicht zu
/// <see cref="SymbolGraphToolRegistrations"/> (C#-Symbolgraph) oder
/// <see cref="FileStructureToolRegistrations"/> (Datei-Struktur) passt. <c>metrics_tree</c> ist in
/// EPIC-02 hierher gewandert, weil seine zwei neuen Roslyn-Modi (<c>violation_density</c>,
/// <c>complexity</c>) denselben <c>LinterEngine</c>-Pull-in wie <c>get_violations</c> haben —
/// derselbe Grund, aus dem <c>get_violations</c> hier registriert ist statt in
/// <see cref="FileStructureToolRegistrations"/>. <c>pattern_detect</c> ist in S2.2 hierher
/// gewandert, weil es denselben <c>LinterEngine</c>-Pull-in wie <c>get_violations</c> hat (reine
/// Aggregation bereits erzeugter <c>RuleViolation</c>-Objekte nach Pattern-Kategorie, siehe
/// <see cref="PatternCatalog"/>).
/// </summary>
internal static class AnalysisToolRegistrations
{
    /// <summary>
    /// Fuegt <paramref name="tools"/> die analyse-orientierten Tools hinzu. Tools erreichen den
    /// resident gehaltenen <paramref name="mcpState"/> per Delegate-Closure - kein DI-Container
    /// (siehe <c>AiNetLinterRichtlinien.mdc</c> §2). Optionaler <paramref name="callLog"/> zeichnet jeden Tool-Aufruf auf, wenn
    /// aktiv (kein Overhead bei deaktiviertem Log).
    /// </summary>
    internal static void Register(
        McpServerPrimitiveCollection<McpServerTool> tools,
        McpCodeGraphServer mcpState,
        McpCallLog? callLog = null)
    {
        AddGetViolations(tools, mcpState, callLog);
        AddSafeguard(tools, mcpState, callLog);
        AddSearchPattern(tools, mcpState, callLog);
        AddMetricsTree(tools, mcpState, callLog);
        AddPatternDetect(tools, mcpState, callLog);
    }

    private static void AddGetViolations(
        McpServerPrimitiveCollection<McpServerTool> tools,
        McpCodeGraphServer mcpState,
        McpCallLog? callLog)
    {
        tools.Add(McpServerTool.Create(
            async (string? scopeFilter = null, int maxResults = GetViolationsScanner.DefaultMaxResults, CancellationToken ct = default) =>
            {
                if (callLog is null)
                {
                    return await GetViolationsTool.ExecuteAsync(mcpState, scopeFilter, maxResults, ct);
                }
                return await callLog.ExecuteCallAsync("get_violations", $"{scopeFilter}|{maxResults}",
                    () => GetViolationsTool.ExecuteAsync(mcpState, scopeFilter, maxResults, ct));
            },
            new McpServerToolCreateOptions
            {
                Name = "get_violations",
                Description = GetViolationsDescription,
            }));
    }

    private const string GetViolationsDescription =
        "Wann nutzen: aktuelle Lint-Regelverstoesse der Solution abfragen — nach jedem Edit " +
        "erneut aufrufbar, kein Disk-Cache. scopeFilter (Projekt-Name oder Pfad-Substring) " +
        "grenzt auf einen Teilbereich ein, maxResults begrenzt die Trefferliste (Default 50).";

    private static void AddSafeguard(
        McpServerPrimitiveCollection<McpServerTool> tools,
        McpCodeGraphServer mcpState,
        McpCallLog? callLog)
    {
        tools.Add(McpServerTool.Create(
            async (string? scopeFilter = null, double minScore = SafeguardScanner.DefaultMinScoreThreshold, int maxViolations = SafeguardScanner.DefaultMaxRemediationEntries, CancellationToken ct = default) =>
            {
                if (callLog is null)
                {
                    return await SafeguardTool.ExecuteAsync(mcpState, scopeFilter, minScore, maxViolations, ct);
                }
                return await callLog.ExecuteCallAsync("safeguard", $"{scopeFilter}|{minScore}|{maxViolations}",
                    () => SafeguardTool.ExecuteAsync(mcpState, scopeFilter, minScore, maxViolations, ct));
            },
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
        McpCodeGraphServer mcpState,
        McpCallLog? callLog)
    {
        tools.Add(McpServerTool.Create(
            async (string pattern, bool isRegex = false, int maxResults = 50, CancellationToken ct = default) =>
            {
                if (callLog is null)
                {
                    return await SearchPatternTool.ExecuteAsync(mcpState, pattern, isRegex, maxResults, ct);
                }
                return await callLog.ExecuteCallAsync("search_pattern", $"{pattern}|{isRegex}|{maxResults}",
                    () => SearchPatternTool.ExecuteAsync(mcpState, pattern, isRegex, maxResults, ct));
            },
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
        McpCodeGraphServer mcpState,
        McpCallLog? callLog)
    {
        tools.Add(McpServerTool.Create(
            async (string? root, string mode, int depth = 1, int topN = 10, string? fileFilter = null, CancellationToken ct = default) =>
            {
                var args = new MetricsTreeToolArgs(root, mode, depth, topN, fileFilter);
                if (callLog is null)
                {
                    return await MetricsTreeTool.ExecuteAsync(mcpState, args, ct);
                }
                return await callLog.ExecuteCallAsync("metrics_tree", $"{root}|{mode}|{depth}|{topN}|{fileFilter}",
                    () => MetricsTreeTool.ExecuteAsync(mcpState, args, ct));
            },
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

    private static void AddPatternDetect(
        McpServerPrimitiveCollection<McpServerTool> tools,
        McpCodeGraphServer mcpState,
        McpCallLog? callLog)
    {
        tools.Add(McpServerTool.Create(
            async (string[]? patterns = null, string? scopeFilter = null, int maxResultsPerPattern = PatternDetectScanner.DefaultMaxResultsPerPattern, CancellationToken ct = default) =>
            {
                if (callLog is null)
                {
                    return await PatternDetectTool.ExecuteAsync(mcpState, patterns, scopeFilter, maxResultsPerPattern, ct);
                }
                return await callLog.ExecuteCallAsync("pattern_detect", $"{string.Join(",", patterns ?? [])}|{scopeFilter}|{maxResultsPerPattern}",
                    () => PatternDetectTool.ExecuteAsync(mcpState, patterns, scopeFilter, maxResultsPerPattern, ct));
            },
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
}
