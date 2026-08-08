#nullable enable

using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Output;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Tools;

/// <summary>
/// MCP-Tool <c>metrics_tree</c>: liefert einen ASCII-Baum mit aggregierten Werten pro
/// Verzeichnisknoten und sortierten Top-N-Kindern — Ebene-fuer-Ebene-Exploration einer Solution statt
/// Komplett-Dump. Deckt in dieser Version die zwei Datei-Walk-Modi <c>code_size</c>/
/// <c>comment_density</c> ab (EPIC-02 ergaenzt die Roslyn-Modi). Bewusst duenner Dispatch: Validierung
/// hier (analog <see cref="FindSymbolTool.ExecuteAsync"/>), Scan-/Aggregationslogik in
/// <see cref="MetricsTreeScanner"/> — keine eigene Logik, damit dieser Klasse eigener
/// <c>AIContextFootprint</c> klein bleibt.
/// </summary>
internal static class MetricsTreeTool
{
    internal static async Task<CallToolResult> ExecuteAsync(
        McpCodeGraphServer state, string? root, string mode, int depth, int topN,
        string? fileFilter, CancellationToken ct)
    {
        if (state.LoadState == ServerLoadState.Loading) return McpToolResults.Loading();
        var solution = state.GetCurrentSolution();
        if (solution is null) return McpToolResults.SolutionNotLoaded();

        var parsedMode = MetricsTreeModeParser.TryParse(mode);
        if (parsedMode is null)
        {
            return McpToolResults.Recoverable(LinterErrorCodes.InvalidArgument,
                $"Unbekannter mode '{mode}'.",
                hint: "Gueltige Werte in dieser Version: code_size, comment_density.");
        }

        if (depth is < 1 or > 5)
        {
            return McpToolResults.Recoverable(LinterErrorCodes.InvalidArgument,
                "depth muss zwischen 1 und 5 liegen.", hint: "depth anpassen.");
        }

        if (topN < 1)
        {
            return McpToolResults.Recoverable(LinterErrorCodes.InvalidArgument,
                "top_n muss mindestens 1 sein.", hint: "top_n anpassen.");
        }

        var filterResult = TryBuildFileFilter(fileFilter);
        if (filterResult.Error is not null) return filterResult.Error;

        var text = MetricsTreeScanner.BuildTree(solution, root, parsedMode.Value, depth, topN, filterResult.Regex);
        var warning = await FindSymbolTool.BuildAggregateWarningAsync(solution, ct);
        var withHint = McpDrillDownHints.Append(text, depth);
        return McpToolResults.Text(FindSymbolTool.PrependWarning(warning, withHint));
    }

    private static (Regex? Regex, CallToolResult? Error) TryBuildFileFilter(string? fileFilter)
    {
        if (string.IsNullOrWhiteSpace(fileFilter)) return (null, null);

        try
        {
            return (new Regex(fileFilter, RegexOptions.IgnoreCase), null);
        }
        catch (ArgumentException ex)
        {
            return (null, McpToolResults.Recoverable(LinterErrorCodes.InvalidArgument,
                $"file_filter ist kein gueltiger regulaerer Ausdruck: {ex.Message}",
                hint: "Regex-Syntax pruefen."));
        }
    }
}
