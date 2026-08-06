#nullable enable

using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Core;
using AiNetLinter.Mcp;
using AiNetLinter.Output;
using Microsoft.CodeAnalysis;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Tools;

/// <summary>
/// MCP-Tool <c>get_impact</c>: findet Aufrufstellen geaenderter C#-Signaturen. Zwei gegenseitig
/// exklusive Eingabe-Modi — entweder <paramref name="gitRef"/> (optional, leer = uncommittete
/// Aenderungen; delegiert an <see cref="DiffImpactAnalyzer.AnalyzeAsync"/>) oder
/// <paramref name="symbolIdentifier"/> (delegiert an
/// <see cref="FindReferencesTool.ResolveSymbolAsync"/> + <see cref="DiffImpactAnalyzer.FindCallSitesAsync"/>).
/// Optionaler <c>depth</c>-Parameter (Default 1, hard cap 3) wirkt nur im Symbol-Branch; der
/// Git-Branch ignoriert ihn, weil eine Git-Diff-Symboltiefe nicht sinnvoll definiert ist.
/// Bewusst duenner Dispatch ohne eigene Analyse-/Parsing-Logik. Deckt nur .cs-Dateien ab.
/// </summary>
internal static class GetImpactTool
{
    internal static async Task<CallToolResult> ExecuteAsync(
        McpCodeGraphServer state, GetImpactInput input, CancellationToken ct)
    {
        if (state.LoadState == ServerLoadState.Loading) return McpToolResults.Loading();
        var solution = state.GetCurrentSolution();
        if (solution is null) return McpToolResults.SolutionNotLoaded();
        var hasGitRef = !string.IsNullOrEmpty(input.GitRef);
        var hasSymbolIdentifier = !string.IsNullOrEmpty(input.SymbolIdentifier);
        if (hasGitRef && hasSymbolIdentifier)
        {
            return McpToolResults.InvalidArgument(
                "gitRef und symbolIdentifier sind gegenseitig exklusiv — genau einen angeben oder " +
                "beide weglassen fuer Git-Diff gegen uncommittete Aenderungen.");
        }
        return await (hasSymbolIdentifier
            ? ExecuteSymbolBranchAsync(solution, input, ct)
            : ExecuteGitRefBranchAsync(solution, input));
    }

    private static async Task<CallToolResult> ExecuteSymbolBranchAsync(Solution solution, GetImpactInput input, CancellationToken ct)
    {
        var (symbol, error) = await FindReferencesTool.ResolveSymbolAsync(
            solution, input.SymbolIdentifier!, ct);
        if (error is not null) return error;

        var warning = await FindSymbolTool.BuildAggregateWarningAsync(solution, ct);
        var effectiveMax = input.MaxResults < 1 ? 1 : input.MaxResults;
        var clampedDepth = Math.Clamp(input.Depth, 1, CallGraphTraversal.MaxRecursionDepth);
        string body;

        if (clampedDepth == 1)
        {
            var callSites = await DiffImpactAnalyzer.FindCallSitesAsync(symbol!, solution);
            if (callSites.Count == 0)
            {
                return McpToolResults.Text(FindSymbolTool.PrependWarning(
                    warning, $"Keine Aufrufstellen gefunden fuer '{input.SymbolIdentifier}'"));
            }
            body = McpTruncation.TruncateLines(callSites, callSites.Count, effectiveMax);
        }
        else
        {
            body = await CallGraphTraversal.ExpandAndFormatAsync(
                solution, symbol!, clampedDepth, effectiveMax, ct);
        }

        return McpToolResults.Text(FindSymbolTool.PrependWarning(warning, body));
    }

    private static async Task<CallToolResult> ExecuteGitRefBranchAsync(Solution solution, GetImpactInput input)
    {
        var targetPath = System.IO.Path.GetDirectoryName(solution.FilePath) ?? "";
        List<string> callSites;
        try
        {
            callSites = await DiffImpactAnalyzer.AnalyzeAsync(
                solution, targetPath, input.GitRef, verbose: false);
        }
        catch (GitDiffFailedException ex)
        {
            // Recoverable statt Error: eine nicht aufloesende gitRef ist ein behebbarer
            // Nutzereingabe-Fehler (Tippfehler, falscher Branch-Name), kein Tool-Malfunction —
            // siehe IsErrorPolicy.md.
            return McpToolResults.Recoverable(
                LinterErrorCodes.AnalysisFailed,
                $"Git-Diff fuer gitRef '{ex.GitRef}' fehlgeschlagen — Ref loest nicht auf.",
                context: ex.Message,
                hint: "gitRef pruefen (z. B. via 'git log'/'git branch') oder ohne gitRef aufrufen fuer uncommittete Aenderungen.");
        }
        var warning = await FindSymbolTool.BuildAggregateWarningAsync(solution, CancellationToken.None);
        var effectiveMax = input.MaxResults < 1 ? 1 : input.MaxResults;

        if (callSites.Count == 0)
        {
            var refLabel = string.IsNullOrEmpty(input.GitRef) ? "uncommittete Aenderungen" : input.GitRef;
            return McpToolResults.Text(FindSymbolTool.PrependWarning(
                warning, $"Keine betroffenen Aufrufstellen gefunden fuer '{refLabel}'"));
        }

        return McpToolResults.Text(FindSymbolTool.PrependWarning(
            warning, McpTruncation.TruncateLines(callSites, callSites.Count, effectiveMax)));
    }
}

/// <summary>
/// Parameter-Record fuer <see cref="GetImpactTool.ExecuteAsync"/>. Kapselt 4
/// Konfigurations-Eingaenge in einem Record, damit <c>MaxMethodParameterCount: 4</c>
/// eingehalten wird. Solution wird separat uebergeben, weil der Linter keine
/// internal nested types erlaubt.
/// </summary>
internal sealed record GetImpactInput(
    string? GitRef,
    string? SymbolIdentifier,
    int MaxResults,
    int Depth);
