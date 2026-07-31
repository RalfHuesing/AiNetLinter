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
/// eines Einmal-Filesystem-Scans, inkl. optionalem Namespace-/Projekt-Filter (siehe step-009). Bewusst
/// duenner Dispatch auf <see cref="GetHotspotsScanner.BuildHotspotsText"/> — keine eigene Scan-/
/// Formatierungslogik (TD-005-Muster, analog zu <see cref="GetIndexScopeTool"/>), damit dieser Klasse
/// eigener <c>AIContextFootprint</c> (siehe <c>AiNetLinter.mdc</c>) klein bleibt.
/// </summary>
internal static class GetHotspotsTool
{
    internal static Task<CallToolResult> ExecuteAsync(
        McpCodeGraphServer state, string? scopeFilter, CancellationToken ct)
    {
        var solution = state.GetCurrentSolution();
        if (solution is null) return Task.FromResult(McpToolResults.SolutionNotLoaded());

        var text = GetHotspotsScanner.BuildHotspotsText(solution, state.MaxLineCount, scopeFilter);
        return Task.FromResult(McpToolResults.Text(text));
    }
}
