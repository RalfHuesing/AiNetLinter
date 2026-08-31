#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Assemblies.Analysis.References;
using AiNetLinter.Mcp.Tools.FileStructure;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;

namespace AiNetLinter.Mcp.Tools.SymbolGraph;

internal static class AssemblySymbolSearch
{
    internal static async Task<AssemblySymbolSearchResult> FindMatchesAsync(
        AssemblyAnalysisLease root,
        string namePattern,
        string? kind,
        int maxResults,
        CancellationToken cancellationToken)
    {
        var leaseSet = AssemblyNavigationSupport.GetLeases(root);
        var leases = leaseSet.Leases;
        var entries = new List<SymbolLocationEntry>();
        var diagnostics = AssemblyNavigationSupport.CreateExpansionDiagnostics(root);
        var searched = 0;

        foreach (var lease in leases)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var solution = lease.Server.GetCurrentSolution();
            if (solution is null)
            {
                diagnostics.Add($"Assembly-Session '{lease.CanonicalPath}' besitzt keine lesbare Solution.");
                continue;
            }

            var search = await SearchLeaseAsync(lease, solution, namePattern, kind, cancellationToken).ConfigureAwait(false);
            entries.AddRange(search.Entries);
            if (search.Searched) searched++;
            if (search.Diagnostic is not null) diagnostics.Add(search.Diagnostic);
        }

        var distinct = entries
            .DistinctBy(EntryKey, StringComparer.Ordinal)
            .OrderBy(entry => entry.Origin?.CanonicalPath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.FilePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Line)
            .ThenBy(entry => entry.Name, StringComparer.Ordinal)
            .ToList();
        var shown = distinct.Take(Math.Max(maxResults, 1)).ToList();
        if (distinct.Count > shown.Count)
        {
            diagnostics.Add($"Die Assembly-Symbolsuche ist auf {shown.Count} Treffer begrenzt.");
        }

        return new(
            shown,
            AssemblyNavigationSupport.CreateSummary(
                leaseSet.TotalAssemblyCount,
                searched,
                leaseSet.AssembliesTruncated || distinct.Count > shown.Count,
                diagnostics));
    }

    private static async Task<(IReadOnlyList<SymbolLocationEntry> Entries, bool Searched, string? Diagnostic)> SearchLeaseAsync(
        AssemblyAnalysisLease lease,
        Solution solution,
        string namePattern,
        string? kind,
        CancellationToken cancellationToken)
    {
        try
        {
            var symbols = await SymbolFinder.FindSourceDeclarationsAsync(
                solution,
                name => name.Contains(namePattern, StringComparison.OrdinalIgnoreCase),
                SymbolFilter.TypeAndMember,
                cancellationToken).ConfigureAwait(false);
            var outputRoot = Path.GetDirectoryName(solution.FilePath) ?? string.Empty;
            var entries = symbols
                .Where(symbol => kind is null || SymbolKindClassifier.MatchesSymbolKind(symbol, kind))
                .SelectMany(symbol => FindSymbolTool.FormatSymbolLocationEntries(symbol, outputRoot))
                .Select(entry => entry with { Origin = AssemblyNavigationSupport.CreateOrigin(lease) })
                .ToList();
            return (entries, true, null);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return (
                [],
                false,
                $"Assembly-Symbolsuche in '{lease.CanonicalPath}' war unvollständig: {exception.Message}");
        }
    }

    private static string EntryKey(SymbolLocationEntry entry) =>
        $"{entry.Origin?.ContentHash}|{entry.FilePath}|{entry.Line}|{entry.Kind}|{entry.Name}";
}
