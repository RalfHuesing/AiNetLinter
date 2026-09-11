#nullable enable

using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using AiNetLinter.Configuration;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Assemblies.Analysis.Factories;
using AiNetLinter.Mcp.Assemblies.Analysis.References;
using AiNetLinter.Mcp.Tools.AssemblyAnalysis;
using AiNetLinter.Mcp.Wire;
using AiNetLinter.Output;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Assemblies.Analysis;

internal sealed record AssemblyAnalysisResponseRequest(
    int MaxResponseBytes = 0,
    string? DetailLevel = null,
    string? Cursor = null,
    bool ApplyWireBudget = true);

internal static partial class AssemblyAnalysisResponse
{
    internal static bool FitsResponseBudget(CallToolResult result, AssemblyAnalysisLease lease, int responseBudgetBytes = 0)
    {
        var budget = AssemblyAnalysisResponseLimits.ResolveResponseBudget(
            responseBudgetBytes,
            null,
            lease.Context.ResponseBudgetBytes);
        return McpResponseSize.From(CreateEnriched(result, lease)).TotalBytes <= budget;
    }

    internal static CallToolResult Enrich(
        CallToolResult result,
        AssemblyAnalysisLease lease,
        AssemblyAnalysisResponseRequest request)
    {
        if (AssemblyAnalysisResponseLimits.IsBelowMinimumResponseBudget(request.MaxResponseBytes))
        {
            return McpToolResults.InvalidArgument(
                $"maxResponseBytes muss mindestens {AssemblyAnalysisResponseLimits.MinimumResponseBytes} Bytes betragen, damit ein maschinenlesbarer Assembly-Envelope mit Status und Budgetdaten repräsentierbar bleibt.",
                $"maxResponseBytes erhöhen oder den Parameter weglassen; dann gilt das konfigurierte Assembly-Budget von {lease.Context.ResponseBudgetBytes} Bytes.");
        }

        var enriched = CreateEnriched(result, lease);
        var budget = AssemblyAnalysisResponseLimits.ResolveResponseBudget(
            request.MaxResponseBytes,
            request.DetailLevel,
            lease.Context.ResponseBudgetBytes);
        if (!request.ApplyWireBudget)
        {
            return AssemblyPublicContract.Project(enriched);
        }

        return AssemblyPublicContract.Project(
            ApplyWireBudget(enriched, budget, AssemblyPaging.ReadOffset(request.Cursor)));
    }

    internal static CallToolResult ApplyWireBudget(CallToolResult result, int budget, int cursorOffset) =>
        AssemblyAnalysisWireBudgetProjection.Apply(result, budget, cursorOffset);

    private static CallToolResult CreateEnriched(CallToolResult result, AssemblyAnalysisLease lease)
    {
        var origin = lease.Context.Origin;
        var effectiveStatus = lease.Context.Status.ResolveEffectiveStatus(
            lease.Context.Diagnostics
                .Concat(lease.ReferenceExpansionDiagnostics)
                .ToArray());
        var metadata = new AssemblyResponseMetadata(
            lease.CanonicalPath,
            origin.OriginKind,
            origin.ContentHash,
            origin.Confidence,
            effectiveStatus.ToWireValue(),
            effectiveStatus.ToCompletenessLabel(),
            origin.BodyAvailability,
            origin.ContentMode);

        JsonElement? structured = result.StructuredContent;
        if (structured is { ValueKind: JsonValueKind.Object })
        {
            var node = JsonNode.Parse(structured.Value.GetRawText()) as JsonObject ?? new JsonObject();
            node["analysis"] = JsonSerializer.SerializeToNode(metadata, McpJsonOptions.Default);
            structured = JsonSerializer.SerializeToElement(node, McpJsonOptions.Default);
        }

        var content = result.Content
            .Select(block => block is TextContentBlock text
                ? new TextContentBlock
                {
                    Text = FormatHeader(metadata) + text.Text,
                }
                : block)
            .ToList();

        return new CallToolResult
        {
            IsError = result.IsError,
            Content = content,
            StructuredContent = structured,
        };
    }

    internal static CallToolResult Unsupported(string canonicalPath)
    {
        var result = McpToolResults.Recoverable(
            LinterErrorCodes.AssemblyTargetUnsupported,
            "Dieses Tool unterstützt das Assembly-Ziel nicht.",
            new McpErrorParameters(
                Context: canonicalPath,
                Hint: "Für dieses Assembly-Ziel eine unterstützte Roslyn-Abfrage oder targetPath auf eine .sln/.slnx-Datei verwenden.",
                TargetPath: canonicalPath));
        return new CallToolResult
        {
            IsError = result.IsError,
            Content = [new TextContentBlock
            {
                Text = "[ASSEMBLY] capability=unsupported; status=unsupported; " +
                       "origin=assembly-target\n\n" + result.Content.OfType<TextContentBlock>().Single().Text,
            }],
            StructuredContent = result.StructuredContent,
        };
    }

    private static string FormatHeader(AssemblyResponseMetadata metadata) =>
        $"[ASSEMBLY] targetPath={metadata.TargetPath}; origin={metadata.Origin}; " +
        $"confidence={metadata.Confidence}; " +
        $"status={metadata.Status}; completeness={metadata.Completeness}; " +
        $"bodyAvailability={metadata.BodyAvailability}; contentMode={metadata.ContentMode}\n\n";

    private sealed record AssemblyResponseMetadata(
        string TargetPath,
        string Origin,
        string AssemblyHash,
        string Confidence,
        string Status,
        string Completeness,
        string BodyAvailability,
        string ContentMode)
    {
    }

}
