#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Assemblies.Analysis.References;
using AiNetLinter.Mcp.Scope;
using AiNetLinter.Mcp.Tools.FileStructure;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;

namespace AiNetLinter.Mcp.Tools.SymbolGraph;

internal sealed record AssemblySymbolSearchRequest(
    AssemblyAnalysisLease Root,
    string NamePattern,
    string? Kind,
    int MaxResults,
    CancellationToken CancellationToken,
    McpScopeType ScopeType = McpScopeType.All,
    bool IncludeGenerated = false);

internal sealed record AssemblySymbolLeaseSearchRequest(
    AssemblyAnalysisLease Lease,
    Solution Solution,
    string NamePattern,
    string? Kind,
    McpScopeType ScopeType,
    bool IncludeGenerated,
    McpScopeClassifier ScopeClassifier,
    CancellationToken CancellationToken);

internal static class AssemblySymbolSearch
{
    internal static async Task<AssemblySymbolSearchResult> FindMatchesAsync(AssemblySymbolSearchRequest request)
    {
        var leaseSet = AssemblyNavigationLeaseAccess.GetLeases(request.Root);
        var leases = leaseSet.Leases;
        var entries = new List<SymbolLocationEntry>();
        var scopeClassifier = new McpScopeClassifier();
        var diagnostics = AssemblyNavigationSupport.CreateExpansionDiagnostics(
            AssemblyNavigationLeaseAccess.CreateView(request.Root));
        var leaseSearch = await SearchLeasesAsync(leases, request, scopeClassifier).ConfigureAwait(false);
        entries.AddRange(leaseSearch.Entries);
        diagnostics.AddRange(leaseSearch.Diagnostics);
        var searched = leaseSearch.Searched;

        var distinct = entries
            .DistinctBy(EntryKey, StringComparer.Ordinal)
            .OrderBy(entry => GetMatchRank(entry, request.NamePattern))
            .ThenBy(entry => entry.Origin?.CanonicalPath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.FilePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Line)
            .ThenBy(entry => entry.Name, StringComparer.Ordinal)
            .ToList();
        var shown = distinct.Take(Math.Max(request.MaxResults, 1)).ToList();
        if (distinct.Count > shown.Count)
        {
            diagnostics.Insert(0, $"Die Assembly-Symbolsuche ist auf {shown.Count} Treffer begrenzt.");
        }

        return new(
            shown,
            AssemblyNavigationSupport.CreateSummary(new AssemblyNavigationSummaryRequest(
                leaseSet.TotalAssemblyCount,
                searched,
                leaseSet.AssembliesTruncated,
                diagnostics,
                ResultsTruncated: distinct.Count > shown.Count)),
            distinct.Count,
            shown.Count,
            distinct.Count > shown.Count,
            distinct.Count > shown.Count ? ["maxResults"] : []);
    }

    private static async Task<(IReadOnlyList<SymbolLocationEntry> Entries, int Searched, IReadOnlyList<string> Diagnostics)> SearchLeasesAsync(
        IReadOnlyList<AssemblyAnalysisLease> leases,
        AssemblySymbolSearchRequest request,
        McpScopeClassifier scopeClassifier)
    {
        var entries = new List<SymbolLocationEntry>();
        var diagnostics = new List<string>();
        var searched = 0;
        foreach (var lease in leases)
        {
            request.CancellationToken.ThrowIfCancellationRequested();
            var solution = lease.Server.GetCurrentSolution();
            if (solution is null)
            {
                diagnostics.Add($"Assembly-Session '{lease.CanonicalPath}' besitzt keine lesbare Solution.");
                continue;
            }

            var search = await SearchLeaseAsync(new AssemblySymbolLeaseSearchRequest(
                lease,
                solution,
                request.NamePattern,
                request.Kind,
                request.ScopeType,
                request.IncludeGenerated,
                scopeClassifier,
                request.CancellationToken)).ConfigureAwait(false);
            entries.AddRange(search.Entries);
            if (search.Searched) searched++;
            if (search.Diagnostic is not null) diagnostics.Add(search.Diagnostic);
        }

        return (entries, searched, diagnostics);
    }

    private static async Task<(IReadOnlyList<SymbolLocationEntry> Entries, bool Searched, string? Diagnostic)> SearchLeaseAsync(
        AssemblySymbolLeaseSearchRequest request)
    {
        try
        {
            var view = AssemblyNavigationLeaseAccess.CreateView(request.Lease);
            var nameFilter = SymbolNameMatcher.CreateDeclarationNameFilter(request.NamePattern);
            var symbols = await SymbolFinder.FindSourceDeclarationsAsync(
                request.Solution,
                nameFilter,
                SymbolFilter.TypeAndMember,
                request.CancellationToken).ConfigureAwait(false);
            var outputRoot = Path.GetDirectoryName(request.Solution.FilePath) ?? string.Empty;
            var matchingSymbols = symbols
                .Where(symbol => request.Kind is null || SymbolKindClassifier.MatchesSymbolKind(symbol, request.Kind))
                .Where(symbol => SymbolNameMatcher.MatchesSymbol(symbol, request.NamePattern))
                .ToList();
            var entries = await FindSymbolScanner.BuildVisibleEntriesAsync(
                new FindSymbolScanRequest(
                    request.Solution,
                    request.NamePattern,
                    request.Kind,
                    int.MaxValue,
                    view.Identity,
                    request.ScopeType,
                    request.IncludeGenerated,
                    request.ScopeClassifier),
                matchingSymbols,
                outputRoot,
                request.CancellationToken).ConfigureAwait(false);
            entries = entries.Select(entry => entry with { Origin = view.Origin }).ToList();
            return (entries, true, null);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return (
                [],
                false,
                $"Assembly-Symbolsuche in '{request.Lease.CanonicalPath}' war unvollständig: {exception.Message}");
        }
    }

    private static string EntryKey(SymbolLocationEntry entry) =>
        $"{entry.Origin?.ContentHash}|{entry.FilePath}|{entry.Line}|{entry.Kind}|{entry.Name}";

    private static int GetMatchRank(SymbolLocationEntry entry, string pattern)
    {
        var clean = SymbolNameMatcher.CleanPattern(pattern);
        if (entry.Name.Equals(clean, StringComparison.OrdinalIgnoreCase)
            || entry.Name.EndsWith($".{clean}", StringComparison.OrdinalIgnoreCase)
            || entry.Name.EndsWith($".{clean}()", StringComparison.OrdinalIgnoreCase)
            || entry.Name.EndsWith($".{clean}(", StringComparison.OrdinalIgnoreCase)) return 0;
        if (clean.Contains('.')
            && (entry.Name.EndsWith($".{clean}", StringComparison.OrdinalIgnoreCase)
                || entry.Name.Contains($".{clean}(", StringComparison.OrdinalIgnoreCase))) return 1;
        if (entry.Name.StartsWith(clean, StringComparison.OrdinalIgnoreCase)) return 2;
        if (entry.Name.Contains(clean, StringComparison.OrdinalIgnoreCase)) return 3;
        return 4;
    }
}
