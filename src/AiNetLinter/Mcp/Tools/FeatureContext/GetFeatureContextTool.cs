#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Core;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Tools.SymbolGraph;
using AiNetLinter.Output;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Tools.FeatureContext;

/// <summary>
/// MCP-Tool <c>get_feature_context</c>: Liefert in einem einzigen residenten One-Shot-Call
/// die 5 wesentlichen Dimensionen fuer ein beliebiges C#-Symbol (Deklaration, Metriken & Budget,
/// direkte Aufrufer, zugehoerige Tests und offene Linter-Violations).
/// </summary>
internal static class GetFeatureContextTool
{
    internal static async Task<CallToolResult> ExecuteAsync(
        McpCodeGraphServer state,
        FeatureContextOptions options,
        CancellationToken ct)
    {
        if (state.LoadState == ServerLoadState.Loading) return McpToolResults.Loading();
        var solution = state.GetCurrentSolution();
        if (solution is null) return McpToolResults.SolutionNotLoaded();

        var targetSymbol = options.EffectiveSymbol;
        if (string.IsNullOrWhiteSpace(targetSymbol))
        {
            return McpToolResults.Recoverable(
                LinterErrorCodes.InvalidArgument,
                "Pflichtparameter 'symbolIdentifier' (oder 'symbol') fehlt oder ist leer.",
                hint: "symbolIdentifier angeben: z. B. \"Namespace.Klasse.Methode\", \"Datei.cs:42\" oder DocCommentId.");
        }

        try
        {
            var (symbol, error) = await FindReferencesTool.ResolveSymbolAsync(solution, targetSymbol, ct);
            if (error is not null) return error;
            if (symbol is null) return McpToolResults.SymbolNotFound(targetSymbol);

            var scanContext = new FeatureContextScanContext(solution, state.Config, state.Console, options);
            var payload = await FeatureContextScanner.ScanAsync(symbol, scanContext, ct);

            var markdown = FeatureContextFormatter.FormatReport(payload);
            return McpToolResults.Text(markdown, payload);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return McpToolResults.CompilationError(
                $"Unerwarteter Fehler in get_feature_context: {ex.Message}",
                context: targetSymbol);
        }
    }
}
