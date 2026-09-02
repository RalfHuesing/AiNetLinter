#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Configuration;
using AiNetLinter.Core;
using AiNetLinter.Mcp.Assemblies.Analysis.References;
using AiNetLinter.Mcp.Tools.SymbolGraph;
using AiNetLinter.Output;
using Microsoft.CodeAnalysis;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Tools.MetricsLookup;

/// <summary>
/// MCP-Tool <c>metrics_lookup</c>: liefert punktgenaue Metriken (LOC, Komplexität, Parameter,
/// AIContextFootprint, Member-Statistiken) und Schwellwert-Abgleiche für ein oder mehrere C#-Symbole.
/// Erwartet ausschliesslich das <c>symbolIdentifiers</c>-Array; ein einzelnes Symbol ist ein Array-Eintrag.
/// </summary>
internal static class MetricsLookupTool
{
    internal static async Task<CallToolResult> ExecuteAsync(
        ISolutionStateProvider state,
        string[]? symbolIdentifiers,
        CancellationToken ct)
    {
        if (state.LoadState == ServerLoadState.Loading) return McpToolResults.Loading();
        var solution = state.GetCurrentSolution();
        if (solution is null) return McpToolResults.SolutionNotLoaded();

        var identifiers = McpBatchArguments.Normalize(symbolIdentifiers, StringComparer.Ordinal);
        if (identifiers.Count == 0)
        {
            return McpToolResults.Recoverable(
                LinterErrorCodes.InvalidArgument,
                "Pflichtparameter 'symbolIdentifiers' fehlt oder ist leer.",
                hint: McpToolResults.SymbolIdentifiersBatchHint);
        }

        try
        {
            var configSnapshot = state.GetConfigSnapshot();
            return await RenderMetricsLookupsAsync(solution, configSnapshot.Config, identifiers, state.AssemblySymbolIdentity, ct);
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
        AnalysisSymbolIdentity? assemblyIdentity,
        CancellationToken ct)
    {
        var solutionRoot = Path.GetDirectoryName(solution.FilePath) ?? "";
        var mb = new MarkdownBuilder();
        var dtos = new List<MetricsLookupResultDto>();

        for (var i = 0; i < identifiers.Count; i++)
        {
            if (i > 0) mb.Divider();

            var (dto, earlyError) = await RenderSingleLookupAsync(
                new RenderSingleLookupRequest(
                    solution, config, identifiers[i], solutionRoot, mb, identifiers.Count, assemblyIdentity),
                ct);

            if (earlyError != null) return earlyError;
            if (dto != null) dtos.Add(dto);
        }

        var markdown = mb.Build().TrimEnd();
        var final = McpSufficiencyHints.Append(markdown);
        return McpToolResults.Text(final, new MetricsLookupBatchDto(dtos, identifiers.Count));
    }

    private static async Task<(MetricsLookupResultDto? Dto, CallToolResult? EarlyError)> RenderSingleLookupAsync(
        RenderSingleLookupRequest request,
        CancellationToken ct)
    {
        var solution = request.Solution;
        var config = request.Config;
        var identifier = request.Identifier;
        var solutionRoot = request.SolutionRoot;
        var mb = request.Markdown;
        var totalCount = request.TotalCount;
        var assemblyIdentity = request.AssemblyIdentity;
        var (symbol, error) = await FindReferencesTool.ResolveSymbolAsync(solution, identifier, ct, assemblyIdentity);

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

        var dto = MetricsLookupScanner.ScanSymbol(symbol, config, solutionRoot, ct, assemblyIdentity);
        var formattedMarkdown = MetricsLookupFormatter.Format(dto);
        mb.Line(formattedMarkdown.TrimEnd());

        return (dto, null);
    }

    private sealed record RenderSingleLookupRequest(
        Solution Solution,
        ILinterEngineConfig Config,
        string Identifier,
        string SolutionRoot,
        MarkdownBuilder Markdown,
        int TotalCount,
        AnalysisSymbolIdentity? AssemblyIdentity);
}
