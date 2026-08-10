#nullable enable

using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Output;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Tools.Analysis;

/// <summary>
/// MCP-Tool <c>get_violations</c>: liefert alle aktuellen Lint-Regelverstoesse der resident gehaltenen
/// Solution — dieselbe Kennzahl wie der bestehende CLI-Batch-Lint-Lauf
/// (<c>ainetlinter --config rules.json --path .</c>), aber granular gegen die geladene Solution statt
/// als Einmal-Komplettlauf, inkl. optionalem Scope-Filter (Projekt-Name oder solution-relativer Pfad)
/// und ohne Disk-Cache. Bewusst duenner Dispatch auf
/// <see cref="GetViolationsScanner.BuildViolationsTextAsync"/> — keine eigene Lint- oder
/// Formatierungslogik, damit dieser Klasse
/// eigener <c>AIContextFootprint</c> (siehe <c> klein bleibt. <c>state.Console</c>
/// wird an den Scanner durchgereicht, damit <see cref="AiNetLinter.Core.LinterEngine"/> auf demselben
/// Kanal loggt wie der MCP-Server selbst (nicht stdout, wo es mit dem stdio-MCP-Verkehr kollidieren
/// wuerde).
/// </summary>
internal static class GetViolationsTool
{
    internal static async Task<CallToolResult> ExecuteAsync(
        McpCodeGraphServer state, string? scopeFilter, CancellationToken ct)
    {
        if (state.LoadState == ServerLoadState.Loading) return McpToolResults.Loading();
        var solution = state.GetCurrentSolution();
        if (solution is null) return McpToolResults.SolutionNotLoaded();

        // Config + UsedDefaultConfig als atomarer Schnappschuss (state.GetConfigSnapshot()) statt
        // zweier getrennter Property-Zugriffe: ein gleichzeitiger reload_config-Aufruf koennte
        // sonst eine zerrissene Kombination liefern (Config schon neu, UsedDefaultConfig noch alt).
        var configSnapshot = state.GetConfigSnapshot();
        var result = await GetViolationsScanner.BuildViolationsTextAsync(
            new GetViolationsScannerParameters(
                Solution: solution,
                Config: configSnapshot.Config,
                Console: state.Console,
                ScopeFilter: scopeFilter,
                CancellationToken: ct,
                UsedDefaultConfig: configSnapshot.UsedDefaultConfig));

        // Echte Malfunction (unerwartete Exception in der LinterEngine) -> IsError=true mit
        // Retry-once-Hinweis, siehe IsErrorPolicy.md. Normale Reports (auch "0 Violations" oder
        // "Keine Dateien im Scope") sind kein Malfunction und bekommen stattdessen den
        // Sufficiency-Hinweis, weil der Report immer vollstaendig fuer den Scope ist (kein
        // Trunkierungs-Parameter existiert fuer get_violations).
        // StructuredContent (S1.3) additiv zum Text — nur fuer den Normalfall gesetzt, weil eine
        // Malfunction keine sinnvolle Teil-Violations-Liste hat (result.Violations ist dann null).
        // In ein Objekt gewrappt (nicht das nackte Array), weil MCP-Clients structuredContent
        // schema-seitig als JSON-Objekt validieren — ein Top-Level-Array liess den gesamten
        // Tool-Call clientseitig fehlschlagen (siehe McpToolResultsTests fuer die Regression).
        return result.IsMalfunction
            ? McpToolResults.Error(
                LinterErrorCodes.AnalysisFailed,
                result.Text,
                context: result.Context,
                hint: "Einmal erneut versuchen — bleibt der Fehler bestehen, LinterEngine-Log pruefen (workspace-load-Diagnosen?).")
            : McpToolResults.Text(McpSufficiencyHints.Append(result.Text), new { Violations = result.Violations! });
    }
}
