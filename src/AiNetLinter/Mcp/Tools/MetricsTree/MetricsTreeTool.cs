#nullable enable

using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Assemblies.Analysis.References;
using AiNetLinter.Mcp.Tools.Common;
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
/// duenner Dispatch: Validierung hier, Scan-/
/// Aggregationslogik in den zwei Scanner-Klassen — keine eigene Logik, damit dieser Klasse eigener
/// <c>AIContextFootprint</c> klein bleibt.
/// </summary>
internal static class MetricsTreeTool
{
    internal const int MaxDepthCap = 5;

    internal static async Task<CallToolResult> ExecuteAsync(
        ISolutionStateProvider state, MetricsTreeToolArgs args, CancellationToken ct)
    {
        if (state.LoadState == ServerLoadState.Loading) return McpToolResults.Loading();
        var solution = state.GetCurrentSolution();
        if (solution is null) return McpToolResults.SolutionNotLoaded();

        var queryResult = TryBuildQuery(args);
        if (queryResult.Error is not null) return queryResult.Error;
        var query = queryResult.Query!;
        if (query.Mode is MetricsTreeMode.ViolationDensity or MetricsTreeMode.Complexity
            && state.GetConfigSnapshot().Config is null)
        {
            return McpToolResults.NotConfigured(solution.FilePath);
        }
        var scan = await BuildTreeResultAsync(state, solution, query, ct);
        if (scan.Root is null)
        {
            return McpToolResults.Text(scan.Message!);
        }

        var sortDescending = MetricsTreeScanner.IsSortDescending(query.Mode);
        var visibleTree = MetricsTreeProjection.Project(scan.Root, query.TopN, sortDescending);
        var totalCount = MetricsTreeProjection.CountNodes(scan.Root);
        var returnedCount = MetricsTreeProjection.CountVisibleNodes(visibleTree);
        var truncated = returnedCount < totalCount;
        var completeness = new MetricsTreeCompleteness(
            truncated ? "truncated" : "complete",
            totalCount,
            returnedCount,
            truncated,
            truncated ? ["topN"] : Array.Empty<string>());
        var next = truncated
            ? new MetricsTreeNext("request_detail", "topN erhöhen oder root/fileFilter verfeinern, um weitere Knoten zu sehen.")
            : new MetricsTreeNext("none", "Kein weiterer Schritt erforderlich.");
        var text = MetricsTreeRenderer.Render(visibleTree, int.MaxValue, sortDescending);
        var withHint = McpDrillDownHints.Append(text, args.Depth);
        return McpToolResults.Text(
            withHint);
    }

    /// <summary>Dispatcht auf den passenden Scanner: die zwei Datei-Modi laufen synchron ohne
    /// Config/Console-Overhead, die zwei Roslyn-Modi brauchen <see cref="ISolutionStateProvider.GetConfigSnapshot"/>
    /// (fuer <c>LinterEngine</c>) und <see cref="ISolutionStateProvider.Console"/> (damit <c>LinterEngine</c>
    /// auf demselben Kanal loggt wie der MCP-Server selbst, analog <see cref="GetViolationsTool"/>).</summary>
    private static async Task<MetricsTreeScanResult> BuildTreeResultAsync(
        ISolutionStateProvider state, Solution solution, MetricsTreeQuery query, CancellationToken ct)
    {
        if (query.Mode is MetricsTreeMode.CodeSize or MetricsTreeMode.CommentDensity)
        {
            return MetricsTreeScanner.BuildTreeResult(solution, query);
        }

        var configSnapshot = state.GetConfigSnapshot();
        return await MetricsTreeRoslynScanner.BuildTreeResultAsync(
            new MetricsTreeRoslynScanParameters(solution, state.GetConfigSnapshot().Config!, state.Console, ct), query);
    }

    private static (MetricsTreeQuery? Query, CallToolResult? Error) TryBuildQuery(MetricsTreeToolArgs args)
    {
        var mode = MetricsTreeModeParser.TryParse(args.Mode ?? "code_size");
        if (mode is null) return (null, McpToolResults.Recoverable(LinterErrorCodes.InvalidArgument, $"Unbekannter mode '{args.Mode}'.", hint: "Gueltige Werte: code_size, comment_density, violation_density, complexity."));
        if (args.Depth is < 1 or > MaxDepthCap) return (null, McpToolResults.Recoverable(LinterErrorCodes.InvalidArgument, $"depth muss zwischen 1 und {MaxDepthCap} liegen.", hint: "depth anpassen."));
        if (args.TopN < 1) return (null, McpToolResults.Recoverable(LinterErrorCodes.InvalidArgument, "top_n muss mindestens 1 sein.", hint: "top_n anpassen."));
        var filter = TryBuildFileFilter(args.FileFilter);
        return filter.Error is not null ? (null, filter.Error) : (new MetricsTreeQuery(args.Root, mode.Value, args.Depth, args.TopN, filter.Regex), null);
    }

    private static (Regex? Regex, CallToolResult? Error) TryBuildFileFilter(string? fileFilter)
    {
        if (string.IsNullOrWhiteSpace(fileFilter)) return (null, null);

        if (!RegexAutoDetector.TryCreateFilterRegex(fileFilter, out var regex, out _, out var errorMessage))
        {
            return (null, McpToolResults.Recoverable(LinterErrorCodes.InvalidArgument,
                $"file_filter ist kein gueltiger regulaerer Ausdruck: {errorMessage}",
                hint: "Glob- oder Regex-Syntax pruefen."));
        }

        return (regex, null);
    }
}
