#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.Text;
using System;
using AiNetLinter.Core;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Tools.MetricsTree;
using AiNetLinter.Output;
using Microsoft.CodeAnalysis;
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
            : McpSufficiencyHints.Append(request.Body);

        return McpToolResults.Text(
            finalBody,
            new AssemblyCallTreeResult(
                request.Root,
                effectiveNavigation,
                treeTruncated,
                request.RequestedDepth,
                request.EffectiveDepth,
                request.DepthWasClamped));
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
        var handoff = entry.Handoff && entry.Id is not null
            ? $"handoff=true; id=`{entry.Id}`"
            : "handoff=false; followUpTools=[]";
        var origin = entry.Origin is null
            ? string.Empty
            : $" [assembly={entry.Origin.CanonicalPath}; origin={entry.Origin.OriginKind}]";
        return $"{text} [{handoff}]{origin}";
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

    internal static IReadOnlyList<string> ResolveAffectedProjects(
        Solution solution,
        ISymbol? symbol,
        IReadOnlyList<TransitiveCallSiteEntry> callSites)
    {
        var affectedProjects = callSites
            .Select(cs => cs.ProjectName)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (symbol?.ContainingAssembly is { } asm)
        {
            var sourceProj = solution.Projects.FirstOrDefault(p =>
                string.Equals(p.AssemblyName, asm.Name, StringComparison.OrdinalIgnoreCase)
                || string.Equals(p.Name, asm.Name, StringComparison.OrdinalIgnoreCase));
            if (sourceProj is not null && !affectedProjects.Contains(sourceProj.Name, StringComparer.OrdinalIgnoreCase))
            {
                affectedProjects.Insert(0, sourceProj.Name);
            }
        }

        return affectedProjects;
    }

    internal static string FormatSymbolImpactText(
        ISymbol symbol,
        IReadOnlyList<string> affectedProjects,
        TestCoverageScannerResult testCoverage,
        TransitiveCallGraphFormatResult formatted)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Auswirkungsanalyse (Impact): {symbol.ToDisplayString()}");
        var projText = affectedProjects.Count > 0 ? string.Join(", ", affectedProjects) : "keine";
        sb.AppendLine($"- **Betroffene Projekte ({affectedProjects.Count}):** {projText}");
        sb.AppendLine($"- **Zugeordnete Tests:** {testCoverage.TotalMatchingTests} Testmethode(n) in {testCoverage.TestFiles.Count} Testdatei(en)");
        sb.AppendLine($"- **Aufrufstellen:** {formatted.Traversal.Completeness.TotalCallSiteCount} statische Referenz(en) (gezeigt: {formatted.Traversal.CallSites.Count})");
        sb.AppendLine();
        sb.AppendLine("## Aufrufstellen");
        sb.AppendLine(formatted.Text);

        if (testCoverage.TestFiles.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"## Zugeordnete Testkandidaten ({testCoverage.TotalMatchingTests})");
            foreach (var testFile in testCoverage.TestFiles.Take(10))
            {
                var methodsPreview = testFile.TestMethods.Count > 0
                    ? $" ({string.Join(", ", testFile.TestMethods.Take(3))}{(testFile.TestMethods.Count > 3 ? ", ..." : "")})"
                    : "";
                sb.AppendLine($"- {testFile.FilePath}{methodsPreview}");
            }
        }

        var fullText = sb.ToString().TrimEnd();
        return IsComplete(formatted.Traversal)
            ? McpSufficiencyHints.Append(fullText)
            : fullText;
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
    bool DepthWasClamped = false);

internal sealed record TransitiveCallGraphFormatResult(
    ReferenceTraversalResult Traversal,
    string Text);
