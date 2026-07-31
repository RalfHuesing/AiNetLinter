#nullable enable

using System.Threading;
using System.Threading.Tasks;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Tools;

/// <summary>
/// MCP-Tool <c>get_violations</c>: liefert alle aktuellen Lint-Regelverstoesse der resident gehaltenen
/// Solution — dieselbe Kennzahl wie der bestehende CLI-Batch-Lint-Lauf
/// (<c>ainetlinter --config rules.json --path .</c>), aber granular gegen die geladene Solution statt
/// als Einmal-Komplettlauf, inkl. optionalem Scope-Filter (Projekt-Name oder solution-relativer Pfad)
/// und ohne Disk-Cache (siehe step-010). Bewusst duenner Dispatch auf
/// <see cref="GetViolationsScanner.BuildViolationsTextAsync"/> — keine eigene Lint- oder
/// Formatierungslogik (TD-005-Muster, analog zu <see cref="GetHotspotsTool"/>), damit dieser Klasse
/// eigener <c>AIContextFootprint</c> (siehe <c>AiNetLinter.mdc</c>) klein bleibt. <c>state.Console</c>
/// wird an den Scanner durchgereicht, damit <see cref="AiNetLinter.Core.LinterEngine"/> auf demselben
/// Kanal loggt wie der MCP-Server selbst (nicht stdout, wo es mit dem stdio-MCP-Verkehr kollidieren
/// wuerde).
/// </summary>
internal static class GetViolationsTool
{
    internal static async Task<CallToolResult> ExecuteAsync(
        McpCodeGraphServer state, string? scopeFilter, CancellationToken ct)
    {
        var solution = state.GetCurrentSolution();
        if (solution is null) return McpToolResults.SolutionNotLoaded();

        var text = await GetViolationsScanner.BuildViolationsTextAsync(
            solution, state.Config, state.Console, scopeFilter, ct);
        return McpToolResults.Text(text);
    }
}
