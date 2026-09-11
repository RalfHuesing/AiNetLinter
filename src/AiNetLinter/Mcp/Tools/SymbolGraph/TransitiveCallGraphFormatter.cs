#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System;
using AiNetLinter.Core;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Scope;
using AiNetLinter.Mcp.Tools.CallTree;
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
        AppendDiagnosticMetadata(metadata, projection);
        var finalBody = metadata.Count > 0
            ? request.Body + "\n\n" + string.Join("\n", metadata)
            : request.Body;

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
            new CallGraphResponseBudget.CallGraphResponseRequest(
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
        result = AddAssemblyNavigationCompatibility(result, effectiveNavigation);
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
        AppendDiagnosticMetadata(metadata, projection);
        if (metadata.Count == 0) return result;

        var text = result.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text ?? string.Empty;
        return McpToolResults.ReplaceText(
            result,
            text + "\n\n" + string.Join("\n", metadata));
    }

    private static CallToolResult AddAssemblyNavigationCompatibility(
        CallToolResult result,
        AssemblyNavigationSummary navigation)
    {
        if (result.StructuredContent is not { ValueKind: System.Text.Json.JsonValueKind.Object } structured)
        {
            return result;
        }

        var payload = JsonNode.Parse(structured.GetRawText()) as JsonObject ?? new JsonObject();
        payload["navigation"] = JsonSerializer.SerializeToNode(navigation, McpJsonOptions.Default);
        return new CallToolResult
        {
            IsError = result.IsError,
            Content = result.Content,
            StructuredContent = JsonSerializer.SerializeToElement(payload, McpJsonOptions.Default),
        };
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
        return $"{text}{origin}";
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
        return fullText;
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
    int MaxResponseBytes);

internal sealed record TransitiveCallGraphFormatResult(
    ReferenceTraversalResult Traversal,
    string Text,
    FindReferencesResultPayload StructuredPayload);
