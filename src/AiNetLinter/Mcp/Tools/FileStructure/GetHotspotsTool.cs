#nullable enable

using System.Threading;
using System.Threading.Tasks;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Tools.FileStructure;

internal sealed record GetHotspotsRequest(
    McpCodeGraphServer State,
    string? ScopeFilter,
    int MaxResults,
    double MinLinePercentage,
    string? ScopeType,
    CancellationToken CancellationToken);

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
            new GetHotspotsRequest(
                state,
                scopeFilter,
                GetHotspotsScanner.DefaultMaxResults,
                GetHotspotsScanner.DefaultMinLinePercentage,
                GetHotspotsScanner.DefaultScopeType,
                ct)).ConfigureAwait(false);

    internal static async Task<CallToolResult> ExecuteAsync(
        McpCodeGraphServer state,
        string? scopeFilter,
        int maxResults,
        double minLinePercentage,
        CancellationToken ct)
        => await ExecuteAsync(
            new GetHotspotsRequest(
                state,
                scopeFilter,
                maxResults,
                minLinePercentage,
                GetHotspotsScanner.DefaultScopeType,
                ct)).ConfigureAwait(false);

    internal static Task<CallToolResult> ExecuteAsync(
        GetHotspotsRequest request)
    {
        var state = request.State;
        if (state.LoadState == ServerLoadState.Loading) return Task.FromResult(McpToolResults.Loading());
        var solution = state.GetCurrentSolution();
        if (solution is null) return Task.FromResult(McpToolResults.SolutionNotLoaded());

        var normalizedScopeType = GetHotspotsScanner.NormalizeScopeType(request.ScopeType);
        if (!GetHotspotsScanner.IsValidScopeType(request.ScopeType))
        {
            return Task.FromResult(McpToolResults.InvalidArgument(
                $"Ungueltiger scopeType-Wert '{request.ScopeType}' — gueltig sind 'production', 'tests', 'all'.",
                hint: "scopeType='production' [Default], 'tests' oder 'all' angeben."));
        }

        var report = GetHotspotsScanner.BuildHotspots(
            solution,
            new HotspotScanOptions(
                state.MaxLineCount,
                request.ScopeFilter,
                request.MaxResults,
                request.MinLinePercentage,
                normalizedScopeType));
        // In ein Objekt gewrappt statt des nackten Arrays — MCP-Clients validieren structuredContent
        // schema-seitig als JSON-Objekt, ein Top-Level-Array liess den Tool-Call fehlschlagen.
        return Task.FromResult(McpToolResults.Text(
            report.Text,
            new HotspotsPayload(
                report.Entries,
                report.TotalHotspots,
                report.ShownHotspots,
                report.Truncated,
                report.MaxResults,
                report.MinLinePercentage,
                report.ScopeType)));
    }
}
