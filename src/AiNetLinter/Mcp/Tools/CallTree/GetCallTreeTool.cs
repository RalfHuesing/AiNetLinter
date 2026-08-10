#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Tools;
using AiNetLinter.Mcp.Tools.MetricsTree;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Tools.CallTree;

/// <summary>
/// MCP-Tool <c>get_call_tree</c>: loest einen Symbol-Identifikator wie <see cref="FindReferencesTool"/>
/// auf und liefert dessen transitiven Caller-Baum als echte Eltern-Kind-Struktur — im Unterschied
/// zur flachen Top-N-Liste von <c>find_references</c>/<c>get_impact</c> mit <c>depth&gt;1</c>.
/// Traversierung via <see cref="CallGraphTraversal.BuildTreeAsync"/> (eigene, hoehere Grenzwerte
/// als die flache Aggregation), Ausgabe als ASCII-Baum (<see cref="MetricsTreeRenderer"/>,
/// wiederverwendet aus <c>metrics_tree</c>) oder Mermaid-Flowchart
/// (<see cref="CallTreeMermaidRenderer"/>). Deckt nur .cs-Dateien ab (Roslyn-Symbolgraph).
/// </summary>
internal static class GetCallTreeTool
{
    private const string MermaidFormat = "mermaid";

    /// <summary>
    /// Tool-Einstiegspunkt: prueft Solution-Ladezustand, loest den Identifikator auf, baut den
    /// Caller-Baum und rendert ihn im angeforderten Format. Fehlerbehandlung/Warnhinweis-Muster
    /// identisch zu <see cref="FindReferencesTool.ExecuteAsync"/> (Compile-Fehler-Warnhinweis,
    /// defensiver try/catch, Sufficiency-Hinweis nur fuer nicht-trunkierte Ergebnisse).
    /// </summary>
    internal static async Task<CallToolResult> ExecuteAsync(
        McpCodeGraphServer state, GetCallTreeInput input, CancellationToken ct)
    {
        if (state.LoadState == ServerLoadState.Loading) return McpToolResults.Loading();
        var solution = state.GetCurrentSolution();
        if (solution is null) return McpToolResults.SolutionNotLoaded();

        try
        {
            var (symbol, error) = await FindReferencesTool.ResolveSymbolAsync(solution, input.SymbolIdentifier, ct);
            if (error is not null) return error;

            var warning = await FindSymbolTool.BuildAggregateWarningAsync(solution, ct);
            var topN = input.TopN < 1 ? 1 : input.TopN;
            var (root, truncated) = await CallGraphTraversal.BuildTreeAsync(solution, symbol!, input.Depth, topN, ct);

            var body = RenderTree(root, input.Format, topN);
            var finalBody = truncated ? body + "\n\n" + BuildTruncationMeta() : McpSufficiencyHints.Append(body);

            return McpToolResults.Text(FindSymbolTool.PrependWarning(warning, finalBody));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return McpToolResults.CompilationError(
                $"Unerwarteter Fehler in get_call_tree: {ex.Message}",
                context: input.SymbolIdentifier);
        }
    }

    private static string RenderTree(MetricsTreeNode root, string? format, int topN) =>
        string.Equals(format, MermaidFormat, StringComparison.OrdinalIgnoreCase)
            ? CallTreeMermaidRenderer.Render(root, topN)
            : MetricsTreeRenderer.Render(root, topN, sortDescending: false);

    private static string BuildTruncationMeta() =>
        $"[Baum trunkiert — hard-cap {CallGraphTraversal.MaxCallTreeNodes} Knoten erreicht, " +
        "depth oder topN reduzieren fuer einen vollstaendigeren Teilbaum]";
}

/// <summary>
/// Parameter-Record fuer <see cref="GetCallTreeTool.ExecuteAsync"/>. Kapselt 4 Eingaben in einem
/// Record, damit <c>MaxMethodParameterCount: 4</c> auf <see cref="GetCallTreeTool.ExecuteAsync"/>
/// eingehalten wird — Solution wird separat ueber <c>state</c> aufgeloest.
/// </summary>
internal sealed record GetCallTreeInput(string SymbolIdentifier, int Depth, string? Format, int TopN);
