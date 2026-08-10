#nullable enable

using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Tools.SymbolGraph;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Tools.FileStructure;

/// <summary>
/// MCP-Tool <c>get_index_scope</c>: liefert eine Dateityp-Aufschluesselung der resident gehaltenen
/// Solution — .cs (voll vom Symbolgraph abgedeckt) sowie .css/.html/.js/.razor/.xaml (jeweils nicht
/// vom Symbolgraph abgedeckt, mit Anzahl). Orientierungspunkt, den ein Agent laut konzept.md aufrufen
/// soll, bevor er ueberhaupt mit find_symbol/search_pattern zu suchen beginnt. Bewusst
/// duenner Dispatch auf <see cref="GetIndexScopeScanner.BuildBreakdown"/> — keine eigene Zaehl-/
/// Formatierungslogik, damit dieser
/// Klasse eigener <c>AIContextFootprint</c> (siehe <c> klein bleibt.
/// </summary>
internal static class GetIndexScopeTool
{
    internal static async Task<CallToolResult> ExecuteAsync(McpCodeGraphServer state, CancellationToken ct)
    {
        if (state.LoadState == ServerLoadState.Loading) return McpToolResults.Loading();
        var solution = state.GetCurrentSolution();
        if (solution is null) return McpToolResults.SolutionNotLoaded();

        var (text, entries) = GetIndexScopeScanner.BuildBreakdown(solution);
        var warning = await FindSymbolTool.BuildAggregateWarningAsync(solution, ct);
        return McpToolResults.Text(FindSymbolTool.PrependWarning(warning, text), entries);
    }
}
