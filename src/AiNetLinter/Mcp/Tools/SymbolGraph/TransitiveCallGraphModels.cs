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
    AssemblyNavigationOrigin? Origin = null,
    string? Id = null,
    string? HandoffKind = null);

internal sealed record FindReferencesCallSiteEntry(
    string FilePath,
    int Line,
    string SymbolName,
    string ProjectName,
    int Depth,
    string ReachedFromSymbolId,
    string? Id = null,
    string? HandoffKind = null,
    AssemblyNavigationOrigin? Origin = null);

internal sealed record FindReferencesResultPayload(
    IReadOnlyList<FindReferencesCallSiteEntry> CallSites,
    TraversalCompleteness Completeness,
    AssemblyNavigationSummary? Navigation = null,
    SymbolHandoffPayload? Handoff = null);

internal sealed record SymbolHandoffPayload(
    string AcceptedAs,
    IReadOnlyDictionary<string, IReadOnlyList<string>> FollowUpsByKind);

internal sealed record TraversalCompleteness(
    int RequestedDepth,
    int EffectiveDepth,
    int VisitedNodeCount,
    int TotalCallSiteCount,
    int ShownCallSiteCount,
    bool TruncatedByMaxResults,
    bool TruncatedByNodeLimit,
    bool DepthWasClamped,
    IReadOnlyList<string>? Diagnostics = null,
    int DiagnosticTotalCount = 0,
    int DiagnosticShownCount = 0,
    bool DiagnosticsTruncated = false,
    IReadOnlyList<string>? DiagnosticsTruncatedBy = null);


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

internal sealed record SymbolImpactPayload(
    IReadOnlyList<TransitiveCallSiteEntry> CallSites,
    TraversalCompleteness Completeness,
    IReadOnlyList<string> AffectedProjects,
    SymbolTestImpactDto? TestImpact = null,
    AssemblyNavigationSummary? Navigation = null,
    SymbolHandoffPayload? Handoff = null);

internal sealed record SymbolTestImpactDto(
    int TotalMatchingTests,
    int TotalTestFiles,
    IReadOnlyList<AiNetLinter.Core.TestFileCoverageResult> TestFiles);
