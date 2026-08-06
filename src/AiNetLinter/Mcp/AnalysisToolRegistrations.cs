#nullable enable

using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Tools;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace AiNetLinter.Mcp;

/// <summary>
/// Registriert die analyse-orientierten Tools (aktuell <c>get_violations</c> und
/// <c>search_pattern</c>) an der von <see cref="McpServerOptionsFactory"/> aufgebauten
/// Tool-Collection. Aus <see cref="FileStructureToolRegistrations"/> ausgelagert, weil
/// <c>get_violations</c> durch den transitiven Pull-in aus <c>LinterEngine</c> +
/// <c>LinterAnalyzer</c> + allen Checkern den <c>AIContextFootprint</c> (siehe
/// <c> der <see cref="FileStructureToolRegistrations"/>-Klasse ueber das
/// 2500-Limit getrieben hat
/// Limit). <c>search_pattern</c> wurde 002 hier angegliedert, weil es ebenfalls
/// datei-inhalts-basiert arbeitet (wie <c>get_violations</c>) und damit semantisch nicht zu
/// <see cref="SymbolGraphToolRegistrations"/> (C#-Symbolgraph) oder
/// <see cref="FileStructureToolRegistrations"/> (Datei-Struktur) passt.
/// </summary>
internal static class AnalysisToolRegistrations
{
    /// <summary>
    /// Fuegt <paramref name="tools"/> die analyse-orientierten Tools hinzu. Tools erreichen den
    /// resident gehaltenen <paramref name="mcpState"/> per Delegate-Closure - kein DI-Container
    /// (siehe <c>. Optionaler <paramref name="callLog"/> zeichnet jeden Tool-Aufruf auf, wenn
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
    }

    private static void AddGetViolations(
        McpServerPrimitiveCollection<McpServerTool> tools,
        McpCodeGraphServer mcpState,
        McpCallLog? callLog)
    {
        tools.Add(McpServerTool.Create(
            async (string? scopeFilter = null, CancellationToken ct = default) =>
            {
                if (callLog is null)
                {
                    return await GetViolationsTool.ExecuteAsync(mcpState, scopeFilter, ct);
                }
                return await callLog.ExecuteCallAsync("get_violations", scopeFilter ?? "",
                    () => GetViolationsTool.ExecuteAsync(mcpState, scopeFilter, ct));
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
        "grenzt auf einen Teilbereich ein.";

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
}
