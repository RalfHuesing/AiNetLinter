#nullable enable

using System.Threading;
using AiNetLinter.Mcp.Tools;
using ModelContextProtocol.Server;

namespace AiNetLinter.Mcp;

/// <summary>
/// Registriert die analyse-orientierten Tools (aktuell <c>get_violations</c> und
/// <c>search_pattern</c>) an der von <see cref="McpServerOptionsFactory"/> aufgebauten
/// Tool-Collection. Aus <see cref="FileStructureToolRegistrations"/> ausgelagert, weil
/// <c>get_violations</c> durch den transitiven Pull-in aus <c>LinterEngine</c> +
/// <c>LinterAnalyzer</c> + allen Checkern den <c>AIContextFootprint</c> (siehe
/// <c>AiNetLinter.mdc</c>) der <see cref="FileStructureToolRegistrations"/>-Klasse ueber das
/// 2500-Limit getrieben hat (siehe step-010 DoD-Footprint-Kontrolle: 2492 Zeilen, +4 ueber
/// Limit). <c>search_pattern</c> wurde 002 hier angegliedert, weil es ebenfalls
/// datei-inhalts-basiert arbeitet (wie <c>get_violations</c>) und damit semantisch nicht zu
/// <see cref="SymbolGraphToolRegistrations"/> (C#-Symbolgraph) oder
/// <see cref="FileStructureToolRegistrations"/> (Datei-Struktur) passt.
/// </summary>
internal static class AnalysisToolRegistrations
{
    /// <summary>
    /// Fuegt <paramref name="tools"/> die analyse-orientierten Tools hinzu. Tools erreichen den
    /// resident gehaltenen <paramref name="mcpState"/> per Delegate-Closure — kein DI-Container
    /// (siehe <c>AiNetLinterRichtlinien.mdc</c> §2).
    /// </summary>
    internal static void Register(McpServerPrimitiveCollection<McpServerTool> tools, McpCodeGraphServer mcpState)
    {
        tools.Add(McpServerTool.Create(
            (string? scopeFilter = null, CancellationToken ct = default) =>
                GetViolationsTool.ExecuteAsync(mcpState, scopeFilter, ct),
            new McpServerToolCreateOptions
            {
                Name = "get_violations",
                Description = "Liefert aktuelle Lint-Regelverstoesse der geladenen Solution — dieselbe " +
                    "Kennzahl wie 'ainetlinter --config rules.json --path .', aber granular gegen die " +
                    "resident gehaltene Solution. Deckt nur .cs-Dateien ab, keine .js/.razor/.xaml/.html/.css-Dateien. " +
                    "Kein Disk-Cache, laeuft direkt gegen die resident gehaltene Solution — die " +
                    "Cache-Isolation zu parallelen CLI-Lint-Laeufen auf derselben Solution ist garantiert. " +
                    "Optionaler scopeFilter matched gegen Projekt-Name oder solution-relativen Dateipfad.",
            }));

        tools.Add(McpServerTool.Create(
            (string pattern, bool isRegex = false, int maxResults = 50, CancellationToken ct = default) =>
                SearchPatternTool.ExecuteAsync(mcpState, pattern, isRegex, maxResults, ct),
            new McpServerToolCreateOptions
            {
                Name = "search_pattern",
                Description = "Plain-Text- oder Regex-Suche ueber den Solution-Dateibestand (alle Dateitypen, " +
                    "nicht nur C#) — Fallback fuer Namen, die kein C#-Symbol sind (z. B. JS-Funktion in " +
                    ".js, Razor-Komponente in .razor, WPF-Element in .xaml). Optionaler isRegex-Flag " +
                    "(default false = case-insensitive Substring). Trunkiert standardmaessig auf 50 " +
                    "Treffer, ueberschreibbar via maxResults. Trunkierungs-Meta-Zeile informiert ueber " +
                    "Gesamt-Trefferzahl.",
            }));
    }
}
