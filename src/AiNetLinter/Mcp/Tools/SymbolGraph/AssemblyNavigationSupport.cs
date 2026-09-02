#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Assemblies.Analysis;
using AiNetLinter.Mcp.Assemblies.Analysis.References;
using AiNetLinter.Mcp.Tools.AssemblyAnalysis;
using AiNetLinter.Mcp.Tools.CallTree;
using AiNetLinter.Mcp.Tools.MetricsTree;
using Microsoft.CodeAnalysis;

namespace AiNetLinter.Mcp.Tools.SymbolGraph;

internal sealed record AssemblyNavigationSummaryRequest(
    int TotalAssemblyCount,
    int SearchedAssemblyCount,
    bool AssembliesTruncated,
    IEnumerable<string> Diagnostics,
    bool ResultsTruncated = false);

internal static class AssemblyNavigationSupport
{
    private const int MaxNavigationAssemblies = AssemblyAnalysisResponseLimits.MaxReferenceSessions;
    private const int MaxNavigationDiagnostics = 100;

    internal static AssemblyNavigationLeaseSet GetLeases(AssemblyAnalysisLease root)
    {
        var all = new[] { root }
            .Concat(root.ReferenceLeasesSnapshot())
            .Distinct()
            .ToList();
        return new(
            all.Take(MaxNavigationAssemblies).ToList(),
            all.Count,
            all.Count > MaxNavigationAssemblies);
    }

    internal static List<string> CreateExpansionDiagnostics(AssemblyAnalysisLease root) =>
        root.ReferenceExpansionDiagnostics
            .Concat(root.ReferenceSessions.SelectMany(session => session.Diagnostics))
            .Where(diagnostic => !string.IsNullOrWhiteSpace(diagnostic))
            .Distinct(StringComparer.Ordinal)
            .Take(MaxNavigationDiagnostics)
            .ToList();

    internal static AssemblyNavigationSummary CreateSummary(AssemblyNavigationSummaryRequest request)
    {
        var distinct = DistinctDiagnostics(request.Diagnostics);
        return new(
            true,
            request.TotalAssemblyCount,
            request.SearchedAssemblyCount,
            request.AssembliesTruncated,
            !request.AssembliesTruncated && !request.ResultsTruncated && distinct.Count == 0 ? "complete" : "partial",
            distinct,
            ResultsTruncated: request.ResultsTruncated);
    }

    internal static AssemblyNavigationSummary MergeSummaries(
        AssemblyNavigationSummary first,
        AssemblyNavigationSummary second)
    {
        var assembliesTruncated = first.AssembliesTruncated || second.AssembliesTruncated;
        var resultsTruncated = first.ResultsTruncated || second.ResultsTruncated;
        var diagnostics = DistinctDiagnostics(first.Diagnostics.Concat(second.Diagnostics));
        return new(
            first.IncludeReferences || second.IncludeReferences,
            Math.Max(first.TotalAssemblyCount, second.TotalAssemblyCount),
            Math.Max(first.SearchedAssemblyCount, second.SearchedAssemblyCount),
            assembliesTruncated,
            !assembliesTruncated
                && !resultsTruncated
                && string.Equals(first.Completeness, "complete", StringComparison.Ordinal)
                && string.Equals(second.Completeness, "complete", StringComparison.Ordinal)
                && diagnostics.Count == 0
                ? "complete"
                : "partial",
            diagnostics,
            ResultsTruncated: resultsTruncated);
    }

    internal static IReadOnlyList<string> DistinctDiagnostics(IEnumerable<string> diagnostics) =>
        diagnostics
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .Take(MaxNavigationDiagnostics)
            .ToList();

    internal static void AddSource(
        ICollection<(AssemblyAnalysisLease Lease, ISymbol Symbol, Solution Solution)> sources,
        AssemblyAnalysisLease lease,
        ISymbol symbol)
    {
        if (lease.Server.GetCurrentSolution() is { } solution)
        {
            sources.Add((lease, symbol, solution));
        }
    }

    internal static void AddMappedRootSource(
        ICollection<(AssemblyAnalysisLease Lease, ISymbol Symbol, Solution Solution)> sources,
        AssemblyAnalysisLease root,
        ISymbol symbol)
    {
        var solution = root.Server.GetCurrentSolution();
        var mapped = solution is null ? null : MapToCompilation(symbol, root.Context.Compilation);
        if (solution is not null && mapped is not null)
        {
            sources.Add((root, mapped, solution));
        }
    }

    internal static AnalysisSymbolIdentity GetIdentity(AssemblyAnalysisLease lease) =>
        new(lease.Context.Origin.ContentHash, lease.Context.Generation);

    internal static AssemblyNavigationOrigin CreateOrigin(AssemblyAnalysisLease lease)
    {
        var origin = lease.Context.Origin;
        return new(
            origin.OriginKind,
            origin.CanonicalPath,
            origin.ContentHash,
            origin.GeneratedDocumentPath,
            origin.Confidence,
            origin.Trust);
    }

    internal static bool MatchesLeaseIdentity(string identifier, AssemblyAnalysisLease lease) =>
        AnalysisSymbolIdentity.TryParse(identifier, out var provided, out _)
        && provided is not null
        && GetIdentity(lease).Matches(provided);

    internal static MetricsTreeNode AddOrigin(MetricsTreeNode node, AssemblyAnalysisLease lease)
    {
        var origin = CreateOrigin(lease);
        return node with
        {
            DisplayLine = $"{node.DisplayLine} [assembly={origin.CanonicalPath}; origin={origin.OriginKind}]",
            Children = node.Children.Select(child => AddOrigin(child, lease)).ToList(),
        };
    }

    internal static CallTreeDirection ParseDirection(string? direction) =>
        string.Equals(direction, CallTreeDirectionNames.Outgoing, StringComparison.OrdinalIgnoreCase)
            ? CallTreeDirection.Outgoing
            : string.Equals(direction, CallTreeDirectionNames.Both, StringComparison.OrdinalIgnoreCase)
                ? CallTreeDirection.Both
                : CallTreeDirection.Incoming;

    private static ISymbol? MapToCompilation(ISymbol symbol, Compilation compilation)
    {
        var declarationId = DocumentationCommentId.CreateDeclarationId(symbol);
        return declarationId is null
            ? null
            : DocumentationCommentId.GetFirstSymbolForDeclarationId(declarationId, compilation);
    }
}
