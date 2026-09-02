#nullable enable

using System.Collections.Generic;
using System.Linq;
using System;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Tools.MetricsTree;
using AiNetLinter.Output;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Tools.SymbolGraph;

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
        return new(projected, string.Join("\n", lines));
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
        AppendDiagnosticMetadata(metadata, projection);
        var finalBody = metadata.Count > 0
            ? request.Body + "\n\n" + string.Join("\n", metadata)
            : request.SuppressSufficiencyHint
                ? McpSufficiencyHints.AppendDecompiledSignatureOnlyLimitation(request.Body)
                : McpSufficiencyHints.Append(request.Body);

        return McpToolResults.Text(
            finalBody,
            new AssemblyCallTreeResult(request.Root, effectiveNavigation, treeTruncated));
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

    internal static bool IsComplete(ReferenceTraversalResult result)
    {
        var completeness = result.Completeness;
        return !completeness.TruncatedByMaxResults &&
               !completeness.TruncatedByNodeLimit &&
               !completeness.DepthWasClamped &&
               completeness.Diagnostics is not { Count: > 0 };
    }

    internal static string Format(ReferenceTraversalResult result)
        => FormatResponse(result).Text;

    private static string FormatEntry(TransitiveCallSiteEntry entry, bool transitive)
    {
        var text = transitive
            ? $"{entry.FilePath}:{entry.Line} - transitiver Aufrufer"
            : $"{entry.FilePath}:{entry.Line} - Aufruf von '{entry.SymbolName}' in Projekt '{entry.ProjectName}'";
        return entry.Origin is null
            ? text
            : $"{text} [assembly={entry.Origin.CanonicalPath}; origin={entry.Origin.OriginKind}]";
    }

    private static void AppendLimitMessages(
        List<string> lines,
        TraversalCompleteness completeness)
    {
        if (completeness.TruncatedByMaxResults)
        {
            lines.Add(CreateMaxResultsMessage(completeness));
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
        DiagnosticProjection projection)
    {
        lines.AddRange(projection.Samples.Select(diagnostic => $"[Assembly-Diagnostic] {diagnostic}"));
        if (projection.TotalCount == 0) return;

        var truncatedBy = projection.TruncatedBy.Count == 0
            ? "keine"
            : string.Join(", ", projection.TruncatedBy);
        lines.Add($"[{projection.TotalCount} Diagnosen gesamt, " +
            $"{projection.Samples.Count} Samples gezeigt — gekürzt: {truncatedBy}]");
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
    bool SuppressSufficiencyHint = false);

internal sealed record TransitiveCallGraphFormatResult(
    ReferenceTraversalResult Traversal,
    string Text);
