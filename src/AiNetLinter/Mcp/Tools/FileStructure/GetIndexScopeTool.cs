#nullable enable

using System.Threading;
using System.Threading.Tasks;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Tools.FileStructure;

/// <summary>
/// MCP-Tool <c>get_index_scope</c>: liefert eine Dateityp-Aufschluesselung der resident gehaltenen
/// Solution — .cs (voll vom Symbolgraph abgedeckt) sowie .css/.html/.js/.razor/.xaml (jeweils nicht
/// vom Symbolgraph abgedeckt, mit Anzahl). Orientierungspunkt für Agent-Loops,
/// die ihn vor find_symbol/search_pattern aufrufen sollten. Bewusst
/// duenner Dispatch auf <see cref="GetIndexScopeScanner.BuildBreakdown"/> — keine eigene Zaehl-/
/// Formatierungslogik, damit dieser
/// Klasse eigener <c>AIContextFootprint</c> (siehe <c> klein bleibt.
/// </summary>
internal static class GetIndexScopeTool
{
    internal static Task<CallToolResult> ExecuteAsync(McpCodeGraphServer state, CancellationToken ct)
    {
        if (state.LoadState == ServerLoadState.Loading) return Task.FromResult(McpToolResults.Loading());
        var solution = state.GetCurrentSolution();
        if (solution is null) return Task.FromResult(McpToolResults.SolutionNotLoaded());

        var (text, entries) = GetIndexScopeScanner.BuildBreakdown(solution);
        // In ein Objekt gewrappt statt des nackten Arrays — MCP-Clients validieren structuredContent
        // schema-seitig als JSON-Objekt, ein Top-Level-Array liess den Tool-Call fehlschlagen.
        return Task.FromResult(McpToolResults.Text(text, new { Breakdown = entries }));
    }
}
