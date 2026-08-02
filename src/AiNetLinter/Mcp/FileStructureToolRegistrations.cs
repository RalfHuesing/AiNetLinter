#nullable enable

using System.Threading;
using AiNetLinter.Mcp.Tools;
using ModelContextProtocol.Server;

namespace AiNetLinter.Mcp;

/// <summary>
/// Registriert die dateistruktur-orientierten Tools (aktuell <c>get_file_skeleton</c>,
/// <c>get_index_scope</c>, <c>get_hotspots</c>) an der von
/// <see cref="McpServerOptionsFactory"/> aufgebauten Tool-Collection. Aus
/// <see cref="McpServerOptionsFactory"/> ausgelagert, damit dessen eigener <c>AIContextFootprint</c>
/// (siehe <c> nicht mit jedem neu registrierten Tool waechst 
/// JIT-Kontext). <c>get_violations</c> ist in eine eigene <see cref="AnalysisToolRegistrations"/>-
/// Klasse ausgelagert, weil sein <c>LinterEngine</c>-Pull-in den Footprint dieser Klasse ueber das
/// 2500-Limit getrieben hat.
/// </summary>
internal static class FileStructureToolRegistrations
{
    internal static void Register(McpServerPrimitiveCollection<McpServerTool> tools, McpCodeGraphServer mcpState)
    {
        tools.Add(McpServerTool.Create(
            (string filePath, CancellationToken ct = default) =>
                GetFileSkeletonTool.ExecuteAsync(mcpState, filePath, ct),
            new McpServerToolCreateOptions
            {
                Name = "get_file_skeleton",
                Description = "Liefert das Struktur-Skelett (Typen, Signaturen ohne " +
                    "Bodies) einer einzelnen C#-Datei per relativem Dateipfad. " +
                    "Deckt nur .cs-Dateien ab, keine .js/.razor/.xaml/.html/.css-Dateien.",
            }));

        tools.Add(McpServerTool.Create(
            (CancellationToken ct = default) =>
                GetIndexScopeTool.ExecuteAsync(mcpState, ct),
            new McpServerToolCreateOptions
            {
                Name = "get_index_scope",
                Description = "Liefert eine Dateityp-Aufschluesselung der geladenen Solution: " +
                    ".cs (voll vom Symbolgraph abgedeckt) sowie .css/.html/.js/.razor/.xaml " +
                    "(jeweils nicht vom Symbolgraph abgedeckt, mit Anzahl) — Orientierung, bevor " +
                    "andere Tools wie find_symbol/search_pattern aufgerufen werden.",
            }));

        tools.Add(McpServerTool.Create(
            (string? scopeFilter = null, CancellationToken ct = default) =>
                GetHotspotsTool.ExecuteAsync(mcpState, scopeFilter, ct),
            new McpServerToolCreateOptions
            {
                Name = "get_hotspots",
                Description = "Liefert .cs-Dateien der geladenen Solution, die sich ihrem " +
                    "konfigurierten Zeilen-Limit (MaxLineCount aus rules.json/Default) naehern " +
                    "oder es ueberschreiten — Drift-Signal vor einem geplanten Edit. Optionaler " +
                    "scopeFilter matched gegen Projekt-Name oder solution-relativen Dateipfad.",
            }));
    }
}
