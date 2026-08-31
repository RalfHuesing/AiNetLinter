#nullable enable

using System.Collections.Generic;
using System.Threading;
using Microsoft.CodeAnalysis;
using AiNetLinter.Mcp;

namespace AiNetLinter.Mcp.Tools.SymbolGraph;

internal sealed record TransitiveCallSiteEntry(
    string FilePath,
    int Line,
    string SymbolName,
    string ProjectName,
    int Depth,
    string ReachedFromSymbolId,
    AssemblyNavigationOrigin? Origin = null);

internal sealed record TraversalCompleteness(
    int RequestedDepth,
    int EffectiveDepth,
    int VisitedNodeCount,
    int TotalCallSiteCount,
    int ShownCallSiteCount,
    bool TruncatedByMaxResults,
    bool TruncatedByNodeLimit,
    bool DepthWasClamped,
    IReadOnlyList<string>? Diagnostics = null);

internal sealed record ReferenceTraversalResult(
    IReadOnlyList<TransitiveCallSiteEntry> CallSites,
    TraversalCompleteness Completeness,
    AssemblyNavigationSummary? Navigation = null);

internal sealed record ReferenceTraversalRequest(
    Solution Solution,
    ISymbol SeedSymbol,
    int RequestedDepth,
    int MaxResults,
    CancellationToken CancellationToken,
    int? NodeLimit = null,
    AnalysisSymbolIdentity? AssemblySymbolIdentity = null);
