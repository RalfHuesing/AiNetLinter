#nullable enable

using System.Threading;
using System.Threading.Tasks;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Tools;

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
    {
        if (state.LoadState == ServerLoadState.Loading) return McpToolResults.Loading();
        var solution = state.GetCurrentSolution();
        if (solution is null) return McpToolResults.SolutionNotLoaded();

        var (text, entries) = GetHotspotsScanner.BuildHotspots(solution, state.MaxLineCount, scopeFilter);
        var warning = await FindSymbolTool.BuildAggregateWarningAsync(solution, ct);
        return McpToolResults.Text(FindSymbolTool.PrependWarning(warning, text), entries);
    }
}
