#nullable enable

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Tools.SymbolGraph;
using AiNetLinter.Output;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Tools.MetricsLookup;

/// <summary>
/// MCP-Tool <c>metrics_lookup</c>: liefert punktgenaue Metriken (LOC, Komplexität, Parameter,
/// AIContextFootprint, Member-Statistiken) und Schwellwert-Abgleiche für ein einzelnes C#-Symbol.
/// </summary>
internal static class MetricsLookupTool
{
    internal static async Task<CallToolResult> ExecuteAsync(
        McpCodeGraphServer state,
        string? symbolIdentifier,
        CancellationToken ct)
    {
        if (state.LoadState == ServerLoadState.Loading) return McpToolResults.Loading();
        var solution = state.GetCurrentSolution();
        if (solution is null) return McpToolResults.SolutionNotLoaded();

        if (string.IsNullOrEmpty(symbolIdentifier))
        {
            return McpToolResults.Recoverable(
                LinterErrorCodes.InvalidArgument,
                "Pflichtparameter 'symbolIdentifier' fehlt oder ist leer.",
                hint: "symbolIdentifier angeben: \"M:Namespace.Klasse.Methode\", \"Datei.cs:42:10\" oder \"Klasse.Methode\".");
        }

        try
        {
            var (symbol, error) = await FindReferencesTool.ResolveSymbolAsync(solution, symbolIdentifier, ct);
            if (error is not null) return error;
            if (symbol is null) return McpToolResults.SymbolNotFound(symbolIdentifier);

            var configSnapshot = state.GetConfigSnapshot();
            var solutionRoot = Path.GetDirectoryName(solution.FilePath) ?? "";
            var resultDto = MetricsLookupScanner.ScanSymbol(symbol, configSnapshot.Config, solutionRoot, ct);
            var warning = await FindSymbolTool.BuildAggregateWarningAsync(solution, ct);
            var markdown = MetricsLookupFormatter.Format(resultDto);
            var final = McpSufficiencyHints.Append(markdown);
            var finalText = FindSymbolTool.PrependWarning(warning, final);

            return McpToolResults.Text(finalText, resultDto);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return McpToolResults.CompilationError(
                $"Unerwarteter Fehler in metrics_lookup: {ex.Message}",
                context: symbolIdentifier);
        }
    }
}
