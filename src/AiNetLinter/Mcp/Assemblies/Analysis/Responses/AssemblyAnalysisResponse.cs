#nullable enable

using System;
using System.Linq;
using AiNetLinter.Configuration;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Assemblies.Analysis.References;
using AiNetLinter.Mcp.Tools.AssemblyAnalysis;
using AiNetLinter.Mcp.Wire;
using AiNetLinter.Output;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Assemblies.Analysis.Responses;

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
        if (ValidateResponseBudget(request.MaxResponseBytes) is { } budgetError) return budgetError;

        var enriched = CreateEnriched(result, lease);
        var budget = AssemblyAnalysisResponseLimits.ResolveResponseBudget(
            request.MaxResponseBytes,
            request.DetailLevel,
            lease.Context.ResponseBudgetBytes);
        if (!request.ApplyWireBudget)
        {
            return AssemblyPublicContract.Project(enriched);
        }

        // Tool-nahe projectors run after this common navigation/enrichment step.
        // Keeping this envelope intact prevents a generic JSON mutation from
        // removing a partial domain unit before its owner can select it.
        return AssemblyPublicContract.Project(enriched);
    }

    internal static CallToolResult? ValidateResponseBudget(int requestedBytes) =>
        !AssemblyAnalysisResponseLimits.IsBelowMinimumResponseBudget(requestedBytes)
            ? null
            : McpToolResults.Error(
                LinterErrorCodes.ResponseBudgetTooSmall,
                $"maxResponseBytes={requestedBytes} ist kleiner als das für eine vollständige minimale Assembly-Projektion erforderliche Budget.",
                new McpErrorParameters(
                    Hint: "maxResponseBytes erhöhen; die vollständige minimale Assembly-Projektion bleibt erhalten.",
                    FieldPath: "$.maxResponseBytes",
                    RequestedBytes: requestedBytes,
                    MinimumResponseBytes: AssemblyAnalysisResponseLimits.MinimumResponseBytes));

    private static CallToolResult CreateEnriched(CallToolResult result, AssemblyAnalysisLease lease)
    {
        var origin = lease.Context.Origin;
        var effectiveStatus = lease.Context.Status.ResolveEffectiveStatus(
            lease.Context.Diagnostics
                .Concat(lease.ReferenceExpansionDiagnostics)
                .ToArray());
        var metadata = new AssemblyResponseMetadata(
            origin.OriginKind,
            origin.ContentHash,
            origin.Confidence,
            effectiveStatus.ToWireValue(),
            effectiveStatus.ToCompletenessLabel(),
            origin.BodyAvailability,
            origin.ContentMode);

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
        };
    }

    private static string FormatHeader(AssemblyResponseMetadata metadata) =>
        $"[ASSEMBLY] origin={metadata.Origin}; " +
        $"confidence={metadata.Confidence}; " +
        $"status={metadata.Status}; completeness={metadata.Completeness}; " +
        $"bodyAvailability={metadata.BodyAvailability}; contentMode={metadata.ContentMode}\n\n";

    private sealed record AssemblyResponseMetadata(
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
