#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Configuration;
using AiNetLinter.Core;
using AiNetLinter.Mcp.Tools.SymbolGraph;
using AiNetLinter.Output;
using Microsoft.CodeAnalysis;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Tools.MetricsLookup;

/// <summary>
/// MCP-Tool <c>metrics_lookup</c>: liefert punktgenaue Metriken (LOC, Komplexität, Parameter,
/// AIContextFootprint, Member-Statistiken) und Schwellwert-Abgleiche für ein oder mehrere C#-Symbole.
/// Akzeptiert einzelne Identifiers oder Batch-Arrays fuer effizienten Schwellwert-Vergleich in 1 Turn.
/// </summary>
internal static class MetricsLookupTool
{
    internal static Task<CallToolResult> ExecuteAsync(
        McpCodeGraphServer state, string? symbolIdentifier, CancellationToken ct) =>
        ExecuteAsync(state, symbolIdentifier, null, ct);

    internal static async Task<CallToolResult> ExecuteAsync(
        McpCodeGraphServer state,
        string? symbolIdentifier,
        string[]? symbolIdentifiers,
        CancellationToken ct)
    {
        if (state.LoadState == ServerLoadState.Loading) return McpToolResults.Loading();
        var solution = state.GetCurrentSolution();
        if (solution is null) return McpToolResults.SolutionNotLoaded();

        var identifiers = ExtractIdentifiers(symbolIdentifier, symbolIdentifiers);
        if (identifiers.Count == 0)
        {
            return McpToolResults.Recoverable(
                LinterErrorCodes.InvalidArgument,
                "Pflichtparameter 'symbolIdentifier' oder 'symbolIdentifiers' fehlt oder ist leer.",
                hint: McpToolResults.SymbolIdentifierBatchHint);
        }

        try
        {
            var configSnapshot = state.GetConfigSnapshot();
            return await RenderMetricsLookupsAsync(solution, configSnapshot.Config, identifiers, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return McpToolResults.CompilationError(
                $"Unerwarteter Fehler in metrics_lookup: {ex.Message}",
                context: string.Join(", ", identifiers));
        }
    }

    private static async Task<CallToolResult> RenderMetricsLookupsAsync(
        Solution solution,
        ILinterEngineConfig config,
        IReadOnlyList<string> identifiers,
        CancellationToken ct)
    {
        var solutionRoot = Path.GetDirectoryName(solution.FilePath) ?? "";
        var warning = await FindSymbolTool.BuildAggregateWarningAsync(solution, ct);
        var mb = new MarkdownBuilder();
        var dtos = new List<MetricsLookupResultDto>();

        for (var i = 0; i < identifiers.Count; i++)
        {
            if (i > 0) mb.Divider();

            var (dto, earlyError) = await RenderSingleLookupAsync(
                solution, config, identifiers[i], solutionRoot, mb, identifiers.Count, ct);

            if (earlyError != null) return earlyError;
            if (dto != null) dtos.Add(dto);
        }

        var markdown = mb.Build().TrimEnd();
        var final = McpSufficiencyHints.Append(markdown);
        var finalText = FindSymbolTool.PrependWarning(warning, final);

        // Beide Zwecke serialisieren zu JSON-Objekten: Einzel-Symbol als nacktes DTO (bisheriger
        // Vertrag), Batch gewrappt — ein Top-Level-Array würde den Tool-Call clientseitig
        // scheitern lassen (siehe McpToolResults.Text``1 und MetricsLookupBatchDto).
        return identifiers.Count == 1 && dtos.Count == 1
            ? McpToolResults.Text(finalText, dtos[0])
            : McpToolResults.Text(finalText, new MetricsLookupBatchDto(dtos, identifiers.Count));
    }

    private static async Task<(MetricsLookupResultDto? Dto, CallToolResult? EarlyError)> RenderSingleLookupAsync(
        Solution solution,
        ILinterEngineConfig config,
        string identifier,
        string solutionRoot,
        MarkdownBuilder mb,
        int totalCount,
        CancellationToken ct)
    {
        var (symbol, error) = await FindReferencesTool.ResolveSymbolAsync(solution, identifier, ct);

        if (error is not null)
        {
            if (totalCount == 1) return (null, error);
            mb.Heading(3, $"Symbol `{identifier}` nicht aufgeloest").BlankLine();
            var errorText = error.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text ?? "Fehler beim Aufloesen.";
            mb.Line(errorText.Trim());
            return (null, null);
        }

        if (symbol is null)
        {
            if (totalCount == 1) return (null, McpToolResults.SymbolNotFound(identifier));
            mb.Heading(3, $"Symbol nicht gefunden: `{identifier}`").BlankLine();
            mb.Line($"[HINWEIS] Symbol '{identifier}' wurde im Symbolgraph nicht gefunden.");
            return (null, null);
        }

        var dto = MetricsLookupScanner.ScanSymbol(symbol, config, solutionRoot, ct);
        var formattedMarkdown = MetricsLookupFormatter.Format(dto);
        mb.Line(formattedMarkdown.TrimEnd());

        return (dto, null);
    }

    private static List<string> ExtractIdentifiers(string? symbolIdentifier, string[]? symbolIdentifiers) =>
        McpBatchArguments.Collect(symbolIdentifier, symbolIdentifiers, StringComparer.Ordinal);
}
