#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Assemblies.Analysis.References;
using AiNetLinter.Mcp.Handoffs;
using AiNetLinter.Mcp.Tools.FileStructure;
using AiNetLinter.Mcp.Tools.MetricsLookup;
using AiNetLinter.Mcp.Tools.SymbolGraph;
using AiNetLinter.Output;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Tools.AssemblyAnalysis;

internal sealed record AssemblyAnalysisContextArguments(
    string? SymbolIdentifier,
    bool IncludeMetrics,
    bool IncludeReferences,
    bool IncludeCallers,
    bool IncludeImpact,
    bool IncludeBody,
    bool IncludeClassStructure,
    int MaxResults,
    int MaxBodyLines,
    int MaxCallers,
    int Depth,
    int TopN,
    int MaxResponseBytes,
    string? DetailLevel,
    string? Cursor);

internal sealed record AssemblyAnalysisContextTextModel(
    int TotalCount,
    int ReturnedCount,
    bool IsTruncated,
    string? ContinuationToken,
    string Scope,
    string Completeness,
    string? SymbolIdentifier,
    IReadOnlyDictionary<string, string> Sections);

internal static class AssemblyAnalysisContextTool
{
    internal const int MaxBodyLinesCap = 1_000;
    internal const int MaxCallersCap = 200;
    internal const int MaxDepthCap = 3;
    internal const int MaxTopNCap = 200;

    internal static async Task<CallToolResult> ExecuteAsync(
        AssemblyAnalysisLease lease,
        AssemblyAnalysisContextArguments arguments,
        CancellationToken cancellationToken)
    {
        var detailLevelError = AssemblyAnalysisResponseLimits.ValidateDetailLevel(arguments.DetailLevel);
        if (detailLevelError is not null) return detailLevelError;

        try
        {
            var budget = AssemblyAnalysisResponseLimits.ResolveResponseBudget(
                arguments.MaxResponseBytes,
                arguments.DetailLevel,
                lease.Context.ResponseBudgetBytes);
            var inspection = InspectAssemblyTool.BuildPayload(
                lease,
                new InspectAssemblyArguments(
                    lease.CanonicalPath, null, null, null, true,
                    AssemblyAnalysisService.NormalizeLimit(arguments.MaxResults, AssemblyAnalysisService.DefaultMaxResults, AssemblyAnalysisService.MaxResults),
                    false, null, AssemblyAnalysisService.DefaultMaxMembers, arguments.IncludeReferences,
                    budget, arguments.DetailLevel, arguments.Cursor));
            var sectionTexts = new Dictionary<string, string>(StringComparer.Ordinal);
            var symbolError = await AddSymbolSectionsAsync(lease, arguments, sectionTexts, cancellationToken).ConfigureAwait(false);
            if (symbolError is not null) return symbolError;
            return McpToolResults.Text(RenderText(CreateTextModel(lease, arguments, inspection, sectionTexts)));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return McpToolResults.CompilationError(
                $"Unerwarteter Fehler in get_assembly_context: {exception.Message}",
                lease.CanonicalPath);
        }
    }

    private static async Task<CallToolResult?> AddSymbolSectionsAsync(
        AssemblyAnalysisLease lease,
        AssemblyAnalysisContextArguments arguments,
        Dictionary<string, string> sectionTexts,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(arguments.SymbolIdentifier)) return null;
        var resolvedSectionLease = await ResolveSectionLeaseAsync(lease, arguments, cancellationToken).ConfigureAwait(false);
        if (resolvedSectionLease.Error is not null) return resolvedSectionLease.Error;
        var symbolLease = resolvedSectionLease.Lease;
        if (arguments.IncludeMetrics)
        {
            var result = await MetricsLookupTool.ExecuteAsync(
                symbolLease.Server, [arguments.SymbolIdentifier], cancellationToken).ConfigureAwait(false);
            RecordText(sectionTexts, "metrics", result);
        }
        if (arguments.IncludeBody)
        {
            var result = await GetSymbolBodyTool.ExecuteAsync(
                (IAssemblyBodyContext)symbolLease,
                [arguments.SymbolIdentifier],
                Math.Clamp(arguments.MaxBodyLines, 1, MaxBodyLinesCap),
                cancellationToken).ConfigureAwait(false);
            RecordText(sectionTexts, "body", result);
        }
        if (arguments.IncludeClassStructure)
        {
            var result = await GetClassStructureTool.ExecuteAsync(
                symbolLease.Server,
                new GetClassStructureArgs(arguments.SymbolIdentifier, "lines", Math.Clamp(arguments.MaxResults, 1, GetClassStructureTool.MaxMembersCap)),
                cancellationToken).ConfigureAwait(false);
            RecordText(sectionTexts, "classStructure", result);
        }
        if (arguments.IncludeCallers)
        {
            var result = await AssemblyFindReferencesTool.ExecuteAsync(
                lease,
                new AssemblyFindReferencesRequest(arguments.SymbolIdentifier, SelectionLimit(arguments), Math.Clamp(arguments.Depth, 1, MaxDepthCap), arguments.IncludeReferences),
                cancellationToken).ConfigureAwait(false);
            RecordText(sectionTexts, "callers", result);
        }
        if (arguments.IncludeImpact)
        {
            var result = await GetImpactTool.ExecuteAsync(
                lease,
                new GetImpactInput(null, arguments.SymbolIdentifier, SelectionLimit(arguments), Math.Clamp(arguments.Depth, 1, MaxDepthCap), IncludeReferences: arguments.IncludeReferences),
                cancellationToken).ConfigureAwait(false);
            RecordText(sectionTexts, "impact", result);
        }
        return null;
    }

    private static async Task<(AssemblyAnalysisLease Lease, CallToolResult? Error)> ResolveSectionLeaseAsync(
        AssemblyAnalysisLease lease,
        AssemblyAnalysisContextArguments arguments,
        CancellationToken cancellationToken)
    {
        if (!arguments.IncludeReferences && !SymbolHandoffIdentifier.HasWirePrefix(arguments.SymbolIdentifier!))
        {
            return (lease, null);
        }

        var plan = AssemblySearchPlan.Create(arguments.SymbolIdentifier, arguments.IncludeReferences);
        var resolved = await AssemblySymbolResolver.ResolveAsync(
            lease,
            arguments.SymbolIdentifier!,
            plan,
            cancellationToken).ConfigureAwait(false);
        if (resolved.Error is not null) return (lease, resolved.Error);
        return resolved.Target is null
            ? (lease, McpToolResults.SymbolNotFound(arguments.SymbolIdentifier!))
            : (resolved.Target.Lease, null);
    }

    private static void RecordText(Dictionary<string, string> sectionTexts, string key, CallToolResult result)
    {
        var text = result.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text;
        if (!string.IsNullOrWhiteSpace(text))
        {
            sectionTexts[key] = text;
        }
    }

    private static int SelectionLimit(AssemblyAnalysisContextArguments arguments) =>
        Math.Min(
            Math.Clamp(arguments.MaxCallers, 1, MaxCallersCap),
            Math.Max(arguments.TopN, 1));

    private static AssemblyAnalysisContextTextModel CreateTextModel(
        AssemblyAnalysisLease lease,
        AssemblyAnalysisContextArguments arguments,
        InspectAssemblyPayload inspection,
        IReadOnlyDictionary<string, string> sectionTexts) =>
        new(
            inspection.TotalCount > 0 ? inspection.TotalCount : inspection.TotalTypes,
            inspection.ReturnedCount > 0 ? inspection.ReturnedCount : inspection.ShownCount,
            inspection.IsTruncated || inspection.Truncated,
            inspection.ContinuationToken,
            arguments.IncludeReferences ? "root+references" : "root",
            lease.Context.Status.ResolveEffectiveStatus(
                lease.Context.Diagnostics.Concat(lease.ReferenceExpansionDiagnostics).ToArray()).ToCompletenessLabel(),
            arguments.SymbolIdentifier,
            sectionTexts);

    internal static string RenderText(AssemblyAnalysisContextTextModel model)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Assembly-Kontext: {model.ReturnedCount} von {model.TotalCount}");
        builder.AppendLine($"Scope: {model.Scope}; Vollständigkeit: {model.Completeness}");
        if (model.SymbolIdentifier is not null) builder.AppendLine($"Symbol: {model.SymbolIdentifier}");
        foreach (var (section, content) in model.Sections)
        {
            if (!string.IsNullOrWhiteSpace(content))
            {
                builder.AppendLine($"Abschnitt: {section}");
                builder.AppendLine(content.Trim());
            }
        }
        if (model.IsTruncated)
        {
            if (!string.IsNullOrWhiteSpace(model.ContinuationToken))
            {
                builder.AppendLine($"Antwort gekürzt; continuationToken={model.ContinuationToken} für die Fortsetzung verwenden.");
            }
            else
            {
                builder.AppendLine("Antwort gekürzt; continuationToken für die Fortsetzung verwenden.");
            }
        }
        return builder.ToString().TrimEnd();
    }
}
