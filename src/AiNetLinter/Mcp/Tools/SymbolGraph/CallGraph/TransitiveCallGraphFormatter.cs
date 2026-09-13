#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System;
using AiNetLinter.Core;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Handoffs;
using AiNetLinter.Mcp.Scope;
using AiNetLinter.Mcp.Tools.CallTree;
using AiNetLinter.Mcp.Tools.MetricsTree;
using AiNetLinter.Mcp.Tools.SymbolGraph.Navigation;
using AiNetLinter.Output;
using Microsoft.CodeAnalysis;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Tools.SymbolGraph.CallGraph;

internal static class TransitiveCallGraphFormatter
{
    internal const int MaxDiagnosticSamples = 5;

    internal static ReferenceTraversalResult ProjectDiagnostics(ReferenceTraversalResult result)
    {
        var projection = CreateDiagnosticProjection(
            (result.Completeness.Diagnostics ?? Array.Empty<string>())
                .Concat(result.Navigation?.Diagnostics ?? Array.Empty<string>()));
        return result with
        {
            Completeness = result.Completeness with
            {
                Diagnostics = projection.Samples,
                DiagnosticTotalCount = projection.TotalCount,
                DiagnosticShownCount = projection.Samples.Count,
                DiagnosticsTruncated = projection.Truncated,
                DiagnosticsTruncatedBy = projection.TruncatedBy,
            },
            Navigation = result.Navigation is null
                ? null
                : result.Navigation with
                {
                    Diagnostics = projection.Samples,
                    DiagnosticTotalCount = projection.TotalCount,
                    DiagnosticShownCount = projection.Samples.Count,
                    DiagnosticsTruncated = projection.Truncated,
                    DiagnosticsTruncatedBy = projection.TruncatedBy,
                },
        };
    }

    internal static TransitiveCallGraphFormatResult FormatResponse(
        ReferenceTraversalResult result,
        string? emptyResultText = null)
    {
        var projected = ProjectDiagnostics(result);
        var completeness = projected.Completeness;
        var lines = projected.CallSites
            .Select(entry => FormatEntry(entry, completeness.EffectiveDepth > 1))
            .ToList();

        if (lines.Count == 0 && emptyResultText is not null)
        {
            lines.Insert(0, emptyResultText);
        }

        AppendLimitMessages(lines, completeness);
        var payload = ToFindReferencesPayload(projected);
        return new(projected, string.Join("\n", lines), payload);
    }

    internal static FindReferencesResultPayload ToFindReferencesPayload(ReferenceTraversalResult result) =>
        new(
            result.CallSites.Select(entry => new FindReferencesCallSiteEntry(
                entry.FilePath,
                entry.Line,
                entry.SymbolName,
                entry.ProjectName,
                entry.Depth,
                entry.ReachedFromSymbolId,
                entry.Id,
                entry.HandoffKind,
                entry.Origin,
                entry.ScopeType,
                entry.SourceKind)).ToList(),
            result.Completeness,
            result.Navigation,
            CreateHandoffPayload(result.CallSites),
            result.Scope);

    internal static TransitiveCallGraphFormatResult FormatPayload(FindReferencesResultPayload payload)
    {
        var callSites = payload.CallSites.Select(entry => new TransitiveCallSiteEntry(
            entry.FilePath, entry.Line, entry.SymbolName, entry.ProjectName, entry.Depth,
            entry.ReachedFromSymbolId, entry.Origin, entry.Id, entry.HandoffKind,
            entry.ScopeType, entry.SourceKind)).ToList();
        return FormatResponse(new ReferenceTraversalResult(callSites, payload.Completeness, payload.Navigation, payload.Scope));
    }

    private static SymbolHandoffPayload? CreateHandoffPayload(
        IReadOnlyList<TransitiveCallSiteEntry> callSites)
    {
        var kinds = callSites
            .Where(entry => entry.Id is not null && entry.HandoffKind is not null)
            .Select(entry => entry.HandoffKind!)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(kind => kind, StringComparer.Ordinal)
            .ToList();
        if (kinds.Count == 0) return null;

        var followUpsByKind = kinds.ToDictionary(
            kind => kind,
            HandoffFollowUpTools.ForKind,
            StringComparer.Ordinal);
        return new("symbolIdentifier", followUpsByKind);
    }

    internal static CallToolResult FormatAssemblyCallTreeResponse(
        AssemblyCallTreeResponseRequest request)
    {
        var projection = CreateDiagnosticProjection(
            request.Navigation.Diagnostics.Concat(request.Diagnostics));
        var treeTruncated = request.Truncated || request.TopNTruncated;
        var effectiveNavigation = request.Navigation with
        {
            Completeness = request.Navigation.Completeness == "complete" &&
                           !treeTruncated &&
                           projection.TotalCount == 0
                ? "complete"
                : "partial",
            Diagnostics = projection.Samples,
            DiagnosticTotalCount = projection.TotalCount,
            DiagnosticShownCount = projection.Samples.Count,
            DiagnosticsTruncated = projection.Truncated,
            DiagnosticsTruncatedBy = projection.TruncatedBy,
        };

        var metadata = new List<string>();
        if (request.TreeTruncationMessage is not null)
        {
            metadata.Add(request.TreeTruncationMessage);
        }
        AppendDiagnosticMetadata(metadata, projection, request.IncludeDiagnostics, assemblyContract: true);
        var finalBody = metadata.Count > 0
            ? request.Body + "\n\n" + string.Join("\n", metadata)
            : request.Body;

        return McpToolResults.Text(
            finalBody);
    }

    internal static CallToolResult FormatAssemblyCallGraphResponse(
        AssemblyCallGraphResponseRequest request)
    {
        var projection = CreateDiagnosticProjection(
            request.Navigation.Diagnostics.Concat(request.Diagnostics));
        var graphTruncated = request.Truncated
            || request.Graph.TopNTruncated
            || request.Graph.HardCapTruncated;
        var effectiveNavigation = request.Navigation with
        {
            Completeness = request.Navigation.Completeness == "complete" &&
                           !graphTruncated &&
                           projection.TotalCount == 0
                ? "complete"
                : "partial",
            Diagnostics = projection.Samples,
            DiagnosticTotalCount = projection.TotalCount,
            DiagnosticShownCount = projection.Samples.Count,
            DiagnosticsTruncated = projection.Truncated,
            DiagnosticsTruncatedBy = projection.TruncatedBy,
        };

        var result = CallGraphResponseBudget.CreateResult(
            new CallGraphResponseRequest(
                request.Graph,
                request.Format,
                CallTreeDirectionNames.For(request.Direction),
                request.RequestedDepth,
                request.EffectiveDepth,
                request.TopN,
                request.ScopeType,
                request.IncludeGenerated,
                request.MaxResponseBytes,
                effectiveNavigation));
        result = AddAssemblyNavigationSummary(result, effectiveNavigation);
        var metadata = new List<string>();
        if (request.DepthWasClamped)
        {
            metadata.Add(
                $"[depth auf {request.EffectiveDepth} begrenzt — requestedDepth={request.RequestedDepth}]");
        }
        if (request.Graph.TopNTruncated)
        {
            metadata.Add("[Graph trunkiert — topN erhoehen fuer einen vollstaendigeren Graphen]");
        }
        if (request.Graph.HardCapTruncated)
        {
            metadata.Add(
                $"[Graph trunkiert — hard-cap {CallGraphTreeBuilder.MaxCallTreeNodes} Knoten erreicht]");
        }
        AppendDiagnosticMetadata(metadata, projection, request.IncludeDiagnostics, assemblyContract: true);
        if (metadata.Count == 0) return result;

        var text = result.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text ?? string.Empty;
        return McpToolResults.ReplaceText(
            result,
            text + "\n\n" + string.Join("\n", metadata));
    }

    private static CallToolResult AddAssemblyNavigationSummary(
        CallToolResult result,
        AssemblyNavigationSummary navigation)
    {
        return AssemblyScopeFormatter.Append(result, navigation);
    }

    internal static DiagnosticProjection CreateDiagnosticProjection(IEnumerable<string>? diagnostics)
    {
        var normalized = (diagnostics ?? Array.Empty<string>())
            .Where(diagnostic => !string.IsNullOrWhiteSpace(diagnostic))
            .Select(NormalizeDiagnostic)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var samples = normalized.Take(MaxDiagnosticSamples).ToList();
        var truncatedBy = normalized.Count > samples.Count
            ? new[] { "maxDiagnostics" }
            : Array.Empty<string>();
        return new DiagnosticProjection(normalized.Count, samples, truncatedBy.Length > 0, truncatedBy);
    }

    internal static string Format(ReferenceTraversalResult result)
        => FormatResponse(result).Text;

    private static string FormatEntry(TransitiveCallSiteEntry entry, bool transitive)
    {
        var text = transitive
            ? $"{entry.FilePath}:{entry.Line} - transitiver Aufrufer"
            : $"{entry.FilePath}:{entry.Line} - Aufruf von '{entry.SymbolName}' in Projekt '{entry.ProjectName}'";
        var origin = entry.Origin is null
            ? string.Empty
            : $" [assembly={entry.Origin.CanonicalPath}; origin={entry.Origin.OriginKind}]";
        var handoff = string.IsNullOrEmpty(entry.Id)
            ? string.Empty
            : $"; handoffId: `{HandoffHandleRegistry.Default.GetOpaqueHandleForOutputOrThrow(entry.Id)}`";
        return $"{text}{origin}{handoff}";
    }

    private static void AppendLimitMessages(
        List<string> lines,
        TraversalCompleteness completeness)
    {
        if (completeness.TruncatedByMaxResults)
        {
            lines.Add(CreateMaxResultsMessage(completeness));
        }

        if (completeness.TruncatedByResponseBudget)
        {
            lines.Add("[Antwort wegen maxResponseBytes begrenzt — weitere vollständige Referenzen nicht enthalten]");
        }

        if (completeness.TruncatedByNodeLimit)
        {
            lines.Add(
                $"[Traversal auf {CallGraphTraversal.MaxRecursionNodes} Knoten begrenzt — weitere Treffer nicht enthalten]");
        }

        if (completeness.DepthWasClamped)
        {
            lines.Add(
                $"[depth auf {completeness.EffectiveDepth} begrenzt — requestedDepth={completeness.RequestedDepth}]");
        }

        if (completeness.Diagnostics is { Count: > 0 })
        {
            AppendDiagnosticMetadata(
                lines,
                new DiagnosticProjection(
                    completeness.DiagnosticTotalCount,
                    completeness.Diagnostics,
                    completeness.DiagnosticsTruncated,
                    completeness.DiagnosticsTruncatedBy ?? Array.Empty<string>()));
        }
    }

    private static void AppendDiagnosticMetadata(
        List<string> lines,
        DiagnosticProjection projection,
        bool includeSamples = true,
        bool assemblyContract = false)
    {
        if (includeSamples)
        {
            lines.AddRange(projection.Samples.Select(diagnostic => $"[Assembly-Diagnostic] {diagnostic}"));
        }

        var truncatedBy = projection.TruncatedBy.Count == 0
            ? "keine"
            : string.Join(", ", projection.TruncatedBy);
        if (!assemblyContract)
        {
            if (projection.TotalCount == 0) return;
            lines.Add($"[{projection.TotalCount} Diagnosen gesamt, " +
                $"{projection.Samples.Count} Samples gezeigt — gekürzt: {truncatedBy}]");
            return;
        }

        var shown = includeSamples ? projection.Samples.Count : 0;
        lines.Add($"[diagnosticsCount={projection.TotalCount}; diagnosticsSamplesShown={shown}; " +
            $"diagnosticsTruncated={projection.Truncated.ToString().ToLowerInvariant()}; truncatedBy={truncatedBy}]");
    }

    private static string CreateMaxResultsMessage(TraversalCompleteness completeness)
    {
        return completeness.EffectiveDepth == 1
            ? $"[{completeness.TotalCallSiteCount} Treffer gesamt, " +
              $"{completeness.ShownCallSiteCount} gezeigt — Pattern verfeinern oder maxResults erhöhen]"
            : $"[{completeness.TotalCallSiteCount} Treffer gesamt " +
              $"(depth={completeness.EffectiveDepth}, hard-cap {CallGraphTraversal.MaxRecursionNodes}), " +
              $"{completeness.ShownCallSiteCount} gezeigt — depth reduzieren oder maxResults erhoehen]";
    }

    private static string NormalizeDiagnostic(string diagnostic) =>
        string.Join(' ', diagnostic.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}

internal sealed record DiagnosticProjection(
    int TotalCount,
    IReadOnlyList<string> Samples,
    bool Truncated,
    IReadOnlyList<string> TruncatedBy);

internal sealed record AssemblyCallTreeResponseRequest(
    MetricsTreeNode Root,
    string Body,
    AssemblyNavigationSummary Navigation,
    IReadOnlyList<string> Diagnostics,
    bool Truncated,
    bool TopNTruncated,
    string? TreeTruncationMessage,
    int RequestedDepth = 1,
    int EffectiveDepth = 1,
    bool DepthWasClamped = false,
    bool IncludeDiagnostics = false);

internal sealed record AssemblyCallGraphResponseRequest(
    CallGraphPayload Graph,
    CallTreeDirection Direction,
    string? Format,
    AssemblyNavigationSummary Navigation,
    IReadOnlyList<string> Diagnostics,
    bool Truncated,
    int RequestedDepth,
    int EffectiveDepth,
    bool DepthWasClamped,
    int TopN,
    McpScopeType ScopeType,
    bool IncludeGenerated,
    int MaxResponseBytes,
    bool IncludeDiagnostics = false);

internal sealed record TransitiveCallGraphFormatResult(
    ReferenceTraversalResult Traversal,
    string Text,
    FindReferencesResultPayload StructuredPayload);
