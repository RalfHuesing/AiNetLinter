#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Assemblies.Analysis.References;
using AiNetLinter.Mcp.Scope;
using AiNetLinter.Output;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Tools.SymbolGraph;

internal sealed record AssemblyFindSymbolRequest(
    string[]? NamePatterns,
    string? Kind,
    int MaxResults,
    bool IncludeReferences,
    McpScopeType ScopeType = McpScopeType.All,
    bool IncludeGenerated = false);

internal static class AssemblyFindSymbolTool
{
    internal static Task<CallToolResult> ExecuteAsync(
        AssemblyAnalysisLease lease,
        AssemblyFindSymbolRequest request,
        CancellationToken cancellationToken) =>
        request.IncludeReferences
            ? ExecuteWithReferencesAsync(lease, request, cancellationToken)
            : FindSymbolTool.ExecuteAsync(new FindSymbolRequest(
                lease.Server,
                request.NamePatterns,
                request.Kind,
                request.MaxResults,
                cancellationToken,
                ScopeType: request.ScopeType,
                IncludeGenerated: request.IncludeGenerated));

    private static async Task<CallToolResult> ExecuteWithReferencesAsync(
        AssemblyAnalysisLease lease,
        AssemblyFindSymbolRequest request,
        CancellationToken cancellationToken)
    {
        var patterns = FindSymbolTool.NormalizeNamePatterns(request.NamePatterns);
        var validationError = FindSymbolTool.ValidateMaxResults(request.MaxResults)
            ?? FindSymbolTool.ValidateNamePatterns(patterns)
            ?? FindSymbolTool.ValidateKind(request.Kind);
        if (validationError is not null) return validationError;

        try
        {
            return await BuildResponseAsync(
                lease,
                patterns,
                request.Kind,
                request.MaxResults,
                request.ScopeType,
                request.IncludeGenerated,
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
        McpScopeType scopeType,
        bool includeGenerated,
        CancellationToken cancellationToken)
    {
        var markdown = new MarkdownBuilder();
        var plan = AssemblySearchPlan.Create(null, includeReferences: true);
        AssemblyNavigationSummary? navigation = null;
        for (var patternIndex = 0; patternIndex < patterns.Count; patternIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (patternIndex > 0) markdown.Divider();
            var pattern = patterns[patternIndex];
            var search = await SearchPatternAsync(
                lease,
                pattern,
                kind,
                maxResults,
                requestScopeType: scopeType,
                includeGenerated: includeGenerated,
                cancellationToken).ConfigureAwait(false);
            search = search with
            {
                Navigation = search.Navigation with
                {
                    IncludeReferences = plan.RequestedIncludeReferences,
                    RequestedIncludeReferences = plan.RequestedIncludeReferences,
                    EffectiveSearchMode = plan.ToWireValue(),
                },
            };
            navigation = navigation is null
                ? search.Navigation
                : AssemblyNavigationSupport.MergeSummaries(navigation, search.Navigation);
            AppendPatternMarkdown(markdown, pattern, search);
        }

        var summary = navigation ?? new AssemblyNavigationSummary(true, 1, 0, false, "partial", []);
        AppendSummary(markdown, summary);
        AppendDiagnostics(markdown, summary.Diagnostics);
        return McpToolResults.Text(markdown.Build().TrimEnd());
    }

    private static Task<AssemblySymbolSearchResult> SearchPatternAsync(
        AssemblyAnalysisLease lease,
        string pattern,
        string? kind,
        int maxResults,
        McpScopeType requestScopeType,
        bool includeGenerated,
        CancellationToken cancellationToken) =>
        AssemblySymbolSearch.FindMatchesAsync(new AssemblySymbolSearchRequest(
            lease,
            pattern,
            kind,
            Math.Max(maxResults, 1),
            cancellationToken,
            requestScopeType,
            includeGenerated));

    private static void AppendPatternMarkdown(
        MarkdownBuilder markdown,
        string pattern,
        AssemblySymbolSearchResult search)
    {
        markdown.Heading(3, $"Symbol-Suche: {pattern}").BlankLine();
        markdown.Line(search.Entries.Count == 0
            ? $"Keine Treffer fuer '{pattern}' in Root- oder Referenz-Assemblies"
            : string.Join("\n", search.Entries.Select(FindSymbolTool.FormatEntry)));
    }

    private static void AppendSummary(MarkdownBuilder markdown, AssemblyNavigationSummary summary)
    {
        markdown.Heading(3, "Assembly-Referenzsuche").BlankLine();
        markdown.Line(
            $"includeReferences=true; Assemblies: {summary.SearchedAssemblyCount} von {summary.TotalAssemblyCount}; " +
            $"Vollständigkeit: {summary.Completeness}; " +
            $"Ergebnisse gekürzt: {summary.ResultsTruncated}");
    }

    private static void AppendDiagnostics(MarkdownBuilder markdown, IReadOnlyList<string> sourceDiagnostics)
    {
        var diagnostics = sourceDiagnostics
            .Where(d => !d.Contains("verbleibenden", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (diagnostics.Count == 0) return;

        var shown = diagnostics.Take(5).ToList();
        var suffix = diagnostics.Count > 5 ? $" ({shown.Count} von {diagnostics.Count} gezeigt)" : string.Empty;
        markdown.Line($"Diagnosen{suffix}:");
        foreach (var diagnostic in shown) markdown.Line($"- {diagnostic}");
    }

}
