#nullable enable

using System.Collections.Generic;
using AiNetLinter.Mcp.Assemblies.Analysis.References;
using AiNetLinter.Mcp.Tools.MetricsTree;
using Microsoft.CodeAnalysis;

namespace AiNetLinter.Mcp.Tools.SymbolGraph;

internal sealed record AssemblySymbolTarget(
    ISymbol Symbol,
    AssemblyAnalysisLease Lease);

internal sealed record AssemblySymbolSearchResult(
    IReadOnlyList<SymbolLocationEntry> Entries,
    AssemblyNavigationSummary Navigation);

internal sealed record AssemblyNavigationLeaseSet(
    IReadOnlyList<AssemblyAnalysisLease> Leases,
    int TotalAssemblyCount,
    bool AssembliesTruncated);

internal sealed record AssemblyNavigationOrigin(
    string OriginKind,
    string CanonicalPath,
    string ContentHash,
    string GeneratedDocumentPath,
    string Confidence,
    string Trust);

internal sealed record AssemblyNavigationSummary(
    bool IncludeReferences,
    int TotalAssemblyCount,
    int SearchedAssemblyCount,
    bool AssembliesTruncated,
    string Completeness,
    IReadOnlyList<string> Diagnostics,
    int DiagnosticTotalCount = 0,
    int DiagnosticShownCount = 0,
    bool DiagnosticsTruncated = false,
    IReadOnlyList<string>? DiagnosticsTruncatedBy = null,
    bool ResultsTruncated = false);

internal sealed record AssemblyCallTreeResult(
    MetricsTreeNode Root,
    AssemblyNavigationSummary Navigation,
    bool Truncated);
