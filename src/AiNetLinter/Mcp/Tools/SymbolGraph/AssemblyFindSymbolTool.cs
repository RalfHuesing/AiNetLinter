#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Assemblies.Analysis.References;
using AiNetLinter.Output;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Tools.SymbolGraph;

internal sealed record AssemblyFindSymbolRequest(
    string[]? NamePatterns,
    string? Kind,
    int MaxResults,
    bool IncludeReferences);

internal static class AssemblyFindSymbolTool
{
    internal static Task<CallToolResult> ExecuteAsync(
        AssemblyAnalysisLease lease,
        AssemblyFindSymbolRequest request,
        CancellationToken cancellationToken) =>
        request.IncludeReferences
            ? ExecuteWithReferencesAsync(lease, request, cancellationToken)
            : FindSymbolTool.ExecuteAsync(
                lease.Server,
                request.NamePatterns,
                request.Kind,
                request.MaxResults,
                cancellationToken);

    private static async Task<CallToolResult> ExecuteWithReferencesAsync(
        AssemblyAnalysisLease lease,
        AssemblyFindSymbolRequest request,
        CancellationToken cancellationToken)
    {
        var patterns = FindSymbolTool.NormalizeNamePatterns(request.NamePatterns);
        var validationError = FindSymbolTool.ValidateNamePatterns(patterns) ?? FindSymbolTool.ValidateKind(request.Kind);
        if (validationError is not null) return validationError;

        try
        {
            return await BuildResponseAsync(
                lease,
                patterns,
                request.Kind,
                request.MaxResults,
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return McpToolResults.CompilationError(
                $"Unerwarteter Fehler in find_symbol: {exception.Message}",
                context: "includeReferences=true");
        }
    }

    private static async Task<CallToolResult> BuildResponseAsync(
        AssemblyAnalysisLease lease,
        IReadOnlyList<string> patterns,
        string? kind,
        int maxResults,
        CancellationToken cancellationToken)
    {
        var results = new List<FindSymbolPatternResultDto>(patterns.Count);
        var markdown = new MarkdownBuilder();
        AssemblyNavigationSummary? navigation = null;
        foreach (var pattern in patterns)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (results.Count > 0) markdown.Divider();
            var search = await AssemblySymbolSearch.FindMatchesAsync(
                lease,
                pattern,
                kind,
                Math.Max(maxResults, 1),
                cancellationToken).ConfigureAwait(false);
            navigation = navigation is null
                ? search.Navigation
                : AssemblyNavigationSupport.MergeSummaries(navigation, search.Navigation);
            results.Add(new FindSymbolPatternResultDto(pattern, search.Entries));
            markdown.Heading(3, $"Symbol-Suche: {pattern}").BlankLine();
            markdown.Line(search.Entries.Count == 0
                ? $"Keine Treffer fuer '{pattern}' in Root- oder Referenz-Assemblies"
                : string.Join("\n", search.Entries.Select(FindSymbolTool.FormatEntry)));
        }

        var summary = navigation ?? new AssemblyNavigationSummary(true, 1, 0, false, "partial", []);
        markdown.Heading(3, "Assembly-Referenzsuche").BlankLine();
        markdown.Line(
            $"includeReferences=true; Assemblies: {summary.SearchedAssemblyCount} von {summary.TotalAssemblyCount}; " +
            $"Vollständigkeit: {summary.Completeness}");
        foreach (var diagnostic in summary.Diagnostics) markdown.Line($"- {diagnostic}");
        return McpToolResults.Text(
            markdown.Build().TrimEnd(),
            new FindSymbolBatchDto(results, summary));
    }
}
