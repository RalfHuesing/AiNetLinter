#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Assemblies.Analysis.References;
using AiNetLinter.Output;
using Microsoft.CodeAnalysis;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Tools.SymbolGraph;

internal static class AssemblySymbolResolver
{
    internal static async Task<(AssemblySymbolTarget? Target, CallToolResult? Error, AssemblyNavigationSummary Navigation)> ResolveAsync(
        AssemblyAnalysisLease root,
        string identifier,
        CancellationToken cancellationToken)
    {
        var leaseSet = AssemblyNavigationSupport.GetLeases(root);
        var leases = leaseSet.Leases;
        var diagnostics = AssemblyNavigationSupport.CreateExpansionDiagnostics(root);
        var candidates = new List<AssemblySymbolTarget>();
        var hasAssemblyId = identifier.StartsWith(AnalysisSymbolIdentity.Prefix, StringComparison.Ordinal);

        foreach (var lease in leases)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (hasAssemblyId && !AssemblyNavigationSupport.MatchesLeaseIdentity(identifier, lease)) continue;

            var solution = lease.Server.GetCurrentSolution();
            if (solution is null)
            {
                diagnostics.Add($"Assembly-Session '{lease.CanonicalPath}' besitzt keine lesbare Solution.");
                continue;
            }

            var resolved = await ResolveLeaseAsync(
                solution,
                lease,
                identifier,
                cancellationToken).ConfigureAwait(false);
            if (resolved.Symbol is not null)
            {
                candidates.Add(new(resolved.Symbol, lease));
            }
            else if (resolved.Diagnostic is not null && !hasAssemblyId)
            {
                diagnostics.Add(resolved.Diagnostic);
            }
        }

        var navigation = AssemblyNavigationSupport.CreateSummary(
            leaseSet.TotalAssemblyCount,
            leases.Count,
            leaseSet.AssembliesTruncated,
            diagnostics);
        if (candidates.Count == 0)
        {
            return (null, McpToolResults.SymbolNotFound(identifier), navigation);
        }

        var distinct = candidates
            .GroupBy(
                candidate => candidate.Lease.Context.Origin.ContentHash + "|" +
                             CallGraphTraversal.GetStableSymbolId(candidate.Symbol),
                StringComparer.Ordinal)
            .Select(group => group.First())
            .ToList();
        if (distinct.Count > 1)
        {
            var lines = distinct
                .SelectMany(FormatCandidateLocations)
                .OrderBy(line => line, StringComparer.Ordinal)
                .ToList();
            return (null, McpToolResults.AmbiguousSymbol(identifier, lines), navigation);
        }

        return (distinct[0], null, navigation);
    }

    private static async Task<(ISymbol? Symbol, string? Diagnostic)> ResolveLeaseAsync(
        Solution solution,
        AssemblyAnalysisLease lease,
        string identifier,
        CancellationToken cancellationToken)
    {
        try
        {
            return await FindInLeaseAsync(
                solution,
                lease,
                identifier,
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return (null, $"Symbolauflösung in '{lease.CanonicalPath}' war unvollständig: {exception.Message}");
        }
    }

    private static async Task<(ISymbol? Symbol, string? Diagnostic)> FindInLeaseAsync(
        Solution solution,
        AssemblyAnalysisLease lease,
        string identifier,
        CancellationToken cancellationToken)
    {
        var (symbol, error) = await FindReferencesTool.ResolveSymbolAsync(
            solution,
            identifier,
            cancellationToken,
            AssemblyNavigationSupport.GetIdentity(lease)).ConfigureAwait(false);
        if (symbol is not null)
        {
            return (symbol, null);
        }

        var diagnostic = error?.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text;
        return IsExpectedResolutionMiss(diagnostic)
            ? (null, null)
            : (null, diagnostic);
    }

    private static bool IsExpectedResolutionMiss(string? diagnostic) =>
        diagnostic?.StartsWith(
            $"[ERROR]: {LinterErrorCodes.SymbolNotFound}:",
            StringComparison.Ordinal) == true;

    private static IEnumerable<string> FormatCandidateLocations(AssemblySymbolTarget candidate)
    {
        var solution = candidate.Lease.Server.GetCurrentSolution();
        if (solution is null) return [];
        var outputRoot = Path.GetDirectoryName(solution.FilePath) ?? string.Empty;
        return FindSymbolTool.FormatSymbolLocationEntries(
                candidate.Symbol,
                outputRoot,
                AssemblyNavigationSupport.GetIdentity(candidate.Lease))
            .Select(entry => FormatLocation(entry with
            {
                Origin = AssemblyNavigationSupport.CreateOrigin(candidate.Lease),
            }));
    }

    private static string FormatLocation(SymbolLocationEntry entry) =>
        $"{entry.FilePath}:{entry.Line} - {entry.Kind}: {entry.Name} " +
        $"[assembly={entry.Origin?.CanonicalPath}; origin={entry.Origin?.OriginKind}]";
}
