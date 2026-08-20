#nullable enable

using System.Collections.Generic;
using System.Threading;
using Microsoft.CodeAnalysis;

namespace AiNetLinter.Mcp.Tools.SymbolGraph;

internal sealed record TransitiveCallSiteEntry(
    string FilePath,
    int Line,
    string SymbolName,
    string ProjectName,
    int Depth,
    string ReachedFromSymbolId);

internal sealed record TraversalCompleteness(
    int RequestedDepth,
    int EffectiveDepth,
    int VisitedNodeCount,
    int TotalCallSiteCount,
    int ShownCallSiteCount,
    bool TruncatedByMaxResults,
    bool TruncatedByNodeLimit,
    bool DepthWasClamped);

internal sealed record ReferenceTraversalResult(
    IReadOnlyList<TransitiveCallSiteEntry> CallSites,
    TraversalCompleteness Completeness);

internal sealed record ReferenceTraversalRequest(
    Solution Solution,
    ISymbol SeedSymbol,
    int RequestedDepth,
    int MaxResults,
    CancellationToken CancellationToken,
    int? NodeLimit = null);
