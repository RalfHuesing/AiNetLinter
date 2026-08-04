#nullable enable

using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Core;
using AiNetLinter.Mcp;
using Microsoft.CodeAnalysis;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Tools;

/// <summary>
/// MCP-Tool <c>get_impact</c>: findet Aufrufstellen geaenderter C#-Signaturen. Zwei gegenseitig
/// exklusive Eingabe-Modi — entweder <paramref name="gitRef"/> (optional, leer = uncommittete
/// Aenderungen; delegiert an <see cref="DiffImpactAnalyzer.AnalyzeAsync"/>) oder
/// <paramref name="symbolIdentifier"/> (delegiert an
/// <see cref="FindReferencesTool.ResolveSymbolAsync"/> + <see cref="DiffImpactAnalyzer.FindCallSitesAsync"/>).
/// Bewusst duenner Dispatch ohne eigene Analyse-/Parsing-Logik. Deckt nur .cs-Dateien ab.
/// </summary>
internal static class GetImpactTool
{
    internal static async Task<CallToolResult> ExecuteAsync(
        McpCodeGraphServer state, string? gitRef, string? symbolIdentifier, int maxResults, CancellationToken ct)
    {
        if (state.LoadState == ServerLoadState.Loading) return McpToolResults.Loading();
        var solution = state.GetCurrentSolution();
        if (solution is null) return McpToolResults.SolutionNotLoaded();
        var hasGitRef = !string.IsNullOrEmpty(gitRef);
        var hasSymbolIdentifier = !string.IsNullOrEmpty(symbolIdentifier);
        if (hasGitRef && hasSymbolIdentifier)
        {
            return McpToolResults.InvalidArgument(
                "gitRef und symbolIdentifier sind gegenseitig exklusiv — genau einen angeben oder " +
                "beide weglassen fuer Git-Diff gegen uncommittete Aenderungen.");
        }
        return await (hasSymbolIdentifier
            ? ExecuteSymbolBranchAsync(solution, symbolIdentifier!, maxResults, ct)
            : ExecuteGitRefBranchAsync(solution, gitRef, maxResults));
    }

    private static async Task<CallToolResult> ExecuteSymbolBranchAsync(
        Solution solution, string symbolIdentifier, int maxResults, CancellationToken ct)
    {
        var (symbol, error) = await FindReferencesTool.ResolveSymbolAsync(solution, symbolIdentifier, ct);
        if (error is not null) return error;

        var callSites = await DiffImpactAnalyzer.FindCallSitesAsync(symbol!, solution);
        var warning = await FindSymbolTool.BuildAggregateWarningAsync(solution, ct);
        var effectiveMax = maxResults < 1 ? 1 : maxResults;

        if (callSites.Count == 0)
        {
            return McpToolResults.Text(FindSymbolTool.PrependWarning(
                warning, $"Keine Aufrufstellen gefunden fuer '{symbolIdentifier}'"));
        }

        return McpToolResults.Text(FindSymbolTool.PrependWarning(
            warning, McpTruncation.TruncateLines(callSites, callSites.Count, effectiveMax)));
    }

    private static async Task<CallToolResult> ExecuteGitRefBranchAsync(Solution solution, string? gitRef, int maxResults)
    {
        var targetPath = System.IO.Path.GetDirectoryName(solution.FilePath) ?? "";
        var callSites = await DiffImpactAnalyzer.AnalyzeAsync(solution, targetPath, gitRef, verbose: false);
        var warning = await FindSymbolTool.BuildAggregateWarningAsync(solution, CancellationToken.None);
        var effectiveMax = maxResults < 1 ? 1 : maxResults;

        if (callSites.Count == 0)
        {
            var refLabel = string.IsNullOrEmpty(gitRef) ? "uncommittete Aenderungen" : gitRef;
            return McpToolResults.Text(FindSymbolTool.PrependWarning(
                warning, $"Keine betroffenen Aufrufstellen gefunden fuer '{refLabel}'"));
        }

        return McpToolResults.Text(FindSymbolTool.PrependWarning(
            warning, McpTruncation.TruncateLines(callSites, callSites.Count, effectiveMax)));
    }
}
