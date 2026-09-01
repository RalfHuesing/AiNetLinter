#nullable enable

using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Tools.SymbolGraph;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Tools.FileStructure;

/// <summary>
/// MCP-Tool <c>get_hotspots</c>: liefert Dateien der resident gehaltenen Solution, die sich ihrem
/// konfigurierten <see cref="McpCodeGraphServer.MaxLineCount"/>-Limit naehern oder es ueberschreiten —
/// dieselbe Kennzahl wie der bestehende CLI-Map-Typ <c>--map hotspots</c>
/// (<see cref="AiNetLinter.Maps.HotspotMapBuilder"/>), aber granular gegen die geladene Solution statt
/// eines Einmal-Filesystem-Scans, inkl. optionalem Namespace-/Projekt-Filter. Bewusst
/// duenner Dispatch auf <see cref="GetHotspotsScanner.BuildHotspots"/> — keine eigene Scan-/
/// Formatierungslogik, damit dieser Klasse
/// eigener <c>AIContextFootprint</c> (siehe <c> klein bleibt.
/// </summary>
internal static class GetHotspotsTool
{
    internal static async Task<CallToolResult> ExecuteAsync(
        McpCodeGraphServer state, string? scopeFilter, CancellationToken ct)
        => await ExecuteAsync(
            state,
            scopeFilter,
            GetHotspotsScanner.DefaultMaxResults,
            GetHotspotsScanner.DefaultMinLinePercentage,
            ct).ConfigureAwait(false);

    internal static async Task<CallToolResult> ExecuteAsync(
        McpCodeGraphServer state,
        string? scopeFilter,
        int maxResults,
        double minLinePercentage,
        CancellationToken ct)
    {
        if (state.LoadState == ServerLoadState.Loading) return McpToolResults.Loading();
        var solution = state.GetCurrentSolution();
        if (solution is null) return McpToolResults.SolutionNotLoaded();

        var report = GetHotspotsScanner.BuildHotspots(
            solution,
            new HotspotScanOptions(
                state.MaxLineCount,
                scopeFilter,
                maxResults,
                minLinePercentage));
        var warning = await FindSymbolTool.BuildAggregateWarningAsync(solution, ct);
        // In ein Objekt gewrappt statt des nackten Arrays — MCP-Clients validieren structuredContent
        // schema-seitig als JSON-Objekt, ein Top-Level-Array liess den Tool-Call fehlschlagen.
        return McpToolResults.Text(
            FindSymbolTool.PrependWarning(warning, report.Text),
            new HotspotsPayload(
                report.Entries,
                report.TotalHotspots,
                report.ShownHotspots,
                report.Truncated,
                report.MaxResults,
                report.MinLinePercentage));
    }
}
