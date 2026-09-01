#nullable enable

using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Tools.SymbolGraph;
using AiNetLinter.Output;
using Microsoft.CodeAnalysis;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Tools.MetricsTree;

/// <summary>Rohe, noch ungeparste <c>metrics_tree</c>-Toolargumente vor der Validierung in <see cref="MetricsTreeTool.ExecuteAsync"/>.</summary>
internal sealed record MetricsTreeToolArgs(
    string? Root, string? Mode, int Depth, int TopN, string? FileFilter);

/// <summary>
/// MCP-Tool <c>metrics_tree</c>: liefert einen ASCII-Baum mit aggregierten Werten pro
/// Verzeichnisknoten und sortierten Top-N-Kindern — Ebene-fuer-Ebene-Exploration einer Solution statt
/// Komplett-Dump. Deckt alle vier Modi ab: die zwei Datei-Walk-Modi <c>code_size</c>/
/// <c>comment_density</c> (synchron, <see cref="MetricsTreeScanner"/>) und die zwei Roslyn-Modi
/// <c>violation_density</c>/<c>complexity</c> (async, <see cref="MetricsTreeRoslynScanner"/>). Bewusst
/// duenner Dispatch: Validierung hier (analog <see cref="FindSymbolTool.ExecuteAsync"/>), Scan-/
/// Aggregationslogik in den zwei Scanner-Klassen — keine eigene Logik, damit dieser Klasse eigener
/// <c>AIContextFootprint</c> klein bleibt.
/// </summary>
internal static class MetricsTreeTool
{
    internal static async Task<CallToolResult> ExecuteAsync(
        McpCodeGraphServer state, MetricsTreeToolArgs args, CancellationToken ct)
    {
        if (state.LoadState == ServerLoadState.Loading) return McpToolResults.Loading();
        var solution = state.GetCurrentSolution();
        if (solution is null) return McpToolResults.SolutionNotLoaded();

        if (string.IsNullOrWhiteSpace(args.Mode))
        {
            return McpToolResults.Recoverable(LinterErrorCodes.InvalidArgument,
                "Pflichtparameter 'mode' fehlt oder ist leer.",
                hint: "Gueltige Werte: code_size, comment_density, violation_density, complexity.");
        }

        var parsedMode = MetricsTreeModeParser.TryParse(args.Mode);
        if (parsedMode is null)
        {
            return McpToolResults.Recoverable(LinterErrorCodes.InvalidArgument,
                $"Unbekannter mode '{args.Mode}'.",
                hint: "Gueltige Werte: code_size, comment_density, violation_density, complexity.");
        }

        if (args.Depth is < 1 or > 5)
        {
            return McpToolResults.Recoverable(LinterErrorCodes.InvalidArgument,
                "depth muss zwischen 1 und 5 liegen.", hint: "depth anpassen.");
        }

        if (args.TopN < 1)
        {
            return McpToolResults.Recoverable(LinterErrorCodes.InvalidArgument,
                "top_n muss mindestens 1 sein.", hint: "top_n anpassen.");
        }

        var filterResult = TryBuildFileFilter(args.FileFilter);
        if (filterResult.Error is not null) return filterResult.Error;

        var query = new MetricsTreeQuery(args.Root, parsedMode.Value, args.Depth, args.TopN, filterResult.Regex);
        var scan = await BuildTreeResultAsync(state, solution, query, ct);
        if (scan.Root is null)
        {
            return McpToolResults.Text(scan.Message!);
        }

        var text = MetricsTreeRenderer.Render(
            scan.Root,
            query.TopN,
            MetricsTreeScanner.IsSortDescending(query.Mode));
        var warning = await FindSymbolTool.BuildAggregateWarningAsync(solution, ct);
        var withHint = McpDrillDownHints.Append(text, args.Depth);
        return McpToolResults.Text(
            FindSymbolTool.PrependWarning(warning, withHint),
            new MetricsTreePayload(
                MetricsTreeModeParser.ToWireValue(query.Mode),
                query.Root,
                query.Depth,
                query.TopN,
                scan.Root));
    }

    /// <summary>Dispatcht auf den passenden Scanner: die zwei Datei-Modi laufen synchron ohne
    /// Config/Console-Overhead, die zwei Roslyn-Modi brauchen <see cref="McpCodeGraphServer.GetConfigSnapshot"/>
    /// (fuer <c>LinterEngine</c>) und <see cref="McpCodeGraphServer.Console"/> (damit <c>LinterEngine</c>
    /// auf demselben Kanal loggt wie der MCP-Server selbst, analog <see cref="GetViolationsTool"/>).</summary>
    private static async Task<MetricsTreeScanResult> BuildTreeResultAsync(
        McpCodeGraphServer state, Solution solution, MetricsTreeQuery query, CancellationToken ct)
    {
        if (query.Mode is MetricsTreeMode.CodeSize or MetricsTreeMode.CommentDensity)
        {
            return MetricsTreeScanner.BuildTreeResult(solution, query);
        }

        var configSnapshot = state.GetConfigSnapshot();
        return await MetricsTreeRoslynScanner.BuildTreeResultAsync(
            new MetricsTreeRoslynScanParameters(solution, configSnapshot.Config, state.Console, ct), query);
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
