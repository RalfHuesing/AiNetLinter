#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Assemblies.Analysis.References;
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

internal static class AssemblyAnalysisContextTool
{
    internal static async Task<CallToolResult> ExecuteAsync(
        AssemblyAnalysisLease lease,
        AssemblyAnalysisContextArguments arguments,
        CancellationToken cancellationToken)
    {
        try
        {
            var budget = AssemblyAnalysisResponseLimits.ResolveResponseBudget(
                arguments.MaxResponseBytes,
                arguments.DetailLevel,
                lease.Context.ResponseBudgetBytes);
            var sectionTexts = new Dictionary<string, string>(StringComparer.Ordinal);
            var root = CreateRoot(lease, arguments);
            await AddAssemblyAnalysisAsync(root, lease, arguments, budget).ConfigureAwait(false);
            await AddSymbolSectionsAsync(root, lease, arguments, sectionTexts, cancellationToken).ConfigureAwait(false);
            AddEnvelope(root);
            return AssemblyAnalysisResponse.ApplyWireBudget(
                McpToolResults.Text(RenderText(root, sectionTexts), root),
                budget,
                AssemblyPaging.ReadOffset(arguments.Cursor));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return McpToolResults.CompilationError(
                $"Unerwarteter Fehler in get_assembly_context: {exception.Message}",
                lease.CanonicalPath);
        }
    }

    private static JsonObject CreateRoot(AssemblyAnalysisLease lease, AssemblyAnalysisContextArguments arguments) => new()
    {
        ["contextId"] = $"asm:{lease.Context.Origin.ContentHash}:{lease.Context.Generation}",
        ["targetType"] = "assembly",
        ["targetPath"] = lease.CanonicalPath,
        ["scope"] = arguments.IncludeReferences || arguments.IncludeCallers || arguments.IncludeImpact ? "root+references" : "root",
        ["completeness"] = lease.Context.Status.ResolveEffectiveStatus(
            lease.Context.Diagnostics.Concat(lease.ReferenceExpansionDiagnostics).ToArray()).ToCompletenessLabel(),
        ["symbolIdentifier"] = arguments.SymbolIdentifier,
        ["identity"] = Serialize(lease.Context.Identity),
        ["origin"] = Serialize(lease.Context.Origin),
    };

    private static async Task AddAssemblyAnalysisAsync(
        JsonObject root,
        AssemblyAnalysisLease lease,
        AssemblyAnalysisContextArguments arguments,
        int budget)
    {
        var inspection = await InspectAssemblyTool.ExecuteAsync(
            lease,
            new InspectAssemblyArguments(
                lease.CanonicalPath, null, null, null, true,
                AssemblyAnalysisService.NormalizeLimit(arguments.MaxResults, AssemblyAnalysisService.DefaultMaxResults, AssemblyAnalysisService.MaxResults),
                false, null, AssemblyAnalysisService.DefaultMaxMembers, arguments.IncludeReferences,
                budget, arguments.DetailLevel, arguments.Cursor)).ConfigureAwait(false);
        root["assemblyAnalysis"] = Serialize(inspection.StructuredContent);
    }

    private static async Task AddSymbolSectionsAsync(
        JsonObject root,
        AssemblyAnalysisLease lease,
        AssemblyAnalysisContextArguments arguments,
        Dictionary<string, string> sectionTexts,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(arguments.SymbolIdentifier)) return;
        if (arguments.IncludeMetrics)
        {
            var result = await MetricsLookupTool.ExecuteAsync(
                lease.Server, [arguments.SymbolIdentifier], cancellationToken).ConfigureAwait(false);
            root["metrics"] = Serialize(result.StructuredContent);
            RecordText(sectionTexts, "metrics", result);
        }
        if (arguments.IncludeBody)
        {
            var result = await GetSymbolBodyTool.ExecuteAsync(
                lease, [arguments.SymbolIdentifier], Math.Clamp(arguments.MaxBodyLines, 1, 1000), cancellationToken).ConfigureAwait(false);
            root["body"] = Serialize(result.StructuredContent);
            RecordText(sectionTexts, "body", result);
        }
        if (arguments.IncludeClassStructure)
        {
            var result = await GetClassStructureTool.ExecuteAsync(
                lease.Server,
                new GetClassStructureArgs(arguments.SymbolIdentifier, "lines", Math.Clamp(arguments.MaxResults, 1, GetClassStructureTool.MaxMembersCap)),
                cancellationToken).ConfigureAwait(false);
            root["classStructure"] = Serialize(result.StructuredContent);
            RecordText(sectionTexts, "classStructure", result);
        }
        if (arguments.IncludeCallers)
        {
            var result = await AssemblyFindReferencesTool.ExecuteAsync(
                lease,
                new AssemblyFindReferencesRequest(arguments.SymbolIdentifier, SelectionLimit(arguments), Math.Clamp(arguments.Depth, 1, 3), true),
                cancellationToken).ConfigureAwait(false);
            root["callers"] = Serialize(result.StructuredContent);
            RecordText(sectionTexts, "callers", result);
        }
        if (arguments.IncludeImpact)
        {
            var result = await GetImpactTool.ExecuteAsync(
                lease.Server,
                new GetImpactInput(null, arguments.SymbolIdentifier, SelectionLimit(arguments), Math.Clamp(arguments.Depth, 1, 3)),
                cancellationToken).ConfigureAwait(false);
            root["impact"] = Serialize(result.StructuredContent);
            RecordText(sectionTexts, "impact", result);
        }
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
            Math.Clamp(arguments.MaxCallers, 1, 200),
            Math.Max(arguments.TopN, 1));

    private static void AddEnvelope(JsonObject root)
    {
        var analysis = root["assemblyAnalysis"];
        var analysisTotalCount = analysis?["totalCount"]?.GetValue<int>() ?? 0;
        var analysisReturnedCount = analysis?["returnedCount"]?.GetValue<int>() ?? 0;
        root["totalCount"] = analysisTotalCount > 0
            ? analysisTotalCount
            : analysis?["totalTypes"]?.GetValue<int>() ?? 0;
        root["returnedCount"] = analysisReturnedCount > 0
            ? analysisReturnedCount
            : analysis?["shownCount"]?.GetValue<int>() ?? 0;
        root["isTruncated"] = analysis?["isTruncated"]?.GetValue<bool>() ?? analysis?["truncated"]?.GetValue<bool>() ?? false;
        root["continuationToken"] = analysis?["continuationToken"]?.GetValue<string>();
        root["truncatedBy"] = analysis?["truncatedBy"]?.DeepClone() ?? new JsonArray();
    }

    private static JsonNode? Serialize(JsonElement? element) =>
        element is { } value ? JsonNode.Parse(value.GetRawText()) : null;

    private static JsonNode? Serialize<T>(T value) =>
        value is null ? null : JsonSerializer.SerializeToNode(value, McpJsonOptions.Default);

    private static string RenderText(JsonObject root, Dictionary<string, string> sectionTexts)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Assembly-Kontext: {root["returnedCount"]} von {root["totalCount"]}");
        builder.AppendLine($"Scope: {root["scope"]}; Vollständigkeit: {root["completeness"]}");
        if (root["symbolIdentifier"] is not null) builder.AppendLine($"Symbol: {root["symbolIdentifier"]}");
        foreach (var property in root.Select(pair => pair.Key)
            .Where(key => key is not ("contextId" or "targetType" or "targetPath" or "scope" or "completeness" or "symbolIdentifier" or "identity" or "origin" or "assemblyAnalysis" or "analysis" or "totalCount" or "returnedCount" or "isTruncated" or "continuationToken" or "wireBudget" or "truncatedBy")))
        {
            builder.AppendLine($"Abschnitt: {property}");
            if (sectionTexts.TryGetValue(property, out var content) && !string.IsNullOrWhiteSpace(content))
            {
                builder.AppendLine(content.Trim());
            }
        }
        if (root["isTruncated"]?.GetValue<bool>() == true)
        {
            builder.AppendLine("Antwort gekürzt; continuationToken für die Fortsetzung verwenden.");
        }
        return builder.ToString().TrimEnd();
    }
}
