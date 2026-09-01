#nullable enable

using AiNetLinter.Mcp.Assemblies.Analysis.References;

using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AiNetLinter.Output;
using AiNetLinter.Mcp.Tools.AssemblyAnalysis;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Assemblies.Analysis;

internal static class AssemblyAnalysisResponse
{
    internal static bool FitsResponseBudget(CallToolResult result, AssemblyAnalysisLease lease)
    {
        var enriched = Enrich(result, lease);
        var textBytes = enriched.Content
            .OfType<TextContentBlock>()
            .Sum(block => Encoding.UTF8.GetByteCount(block.Text));
        var structuredBytes = enriched.StructuredContent is { } structured
            ? Encoding.UTF8.GetByteCount(structured.GetRawText())
            : 0;
        return textBytes <= AssemblyAnalysisResponseLimits.MaxResponseBytes
            && structuredBytes <= AssemblyAnalysisResponseLimits.MaxResponseBytes;
    }

    internal static CallToolResult Enrich(CallToolResult result, AssemblyAnalysisLease lease)
    {
        var origin = lease.Context.Origin;
        var effectiveStatus = lease.Context.Status.ResolveEffectiveStatus(
            lease.Context.Diagnostics
                .Concat(lease.ReferenceExpansionDiagnostics)
                .ToArray());
        var metadata = new AssemblyResponseMetadata(
            lease.CanonicalPath,
            origin.OriginKind,
            origin.SourceProjectPath,
            origin.ContentHash,
            origin.GeneratedDocumentPath,
            origin.Confidence,
            origin.Trust,
            lease.Context.Generation,
            effectiveStatus.ToWireValue(),
            effectiveStatus.ToCompletenessLabel(),
            origin.SourceSnapshotIdentity);

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
            context: canonicalPath,
            hint: "Für dieses Assembly-Ziel eine unterstützte Roslyn-Abfrage oder targetType='project' verwenden.",
            targetType: "assembly",
            targetPath: canonicalPath);
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
        $"[ASSEMBLY] targetType=assembly; targetPath={metadata.TargetPath}; origin={metadata.Origin}; " +
        $"sourcePath={metadata.SourcePath ?? "none"}; snapshot={FormatSnapshot(metadata.SourceSnapshot)}; " +
        $"confidence={metadata.Confidence}; trust={metadata.Trust}; generation={metadata.Generation}; " +
        $"status={metadata.Status}; completeness={metadata.Completeness}\n\n";

    private static string FormatSnapshot(SourceSnapshotIdentity? snapshot) =>
        snapshot is null ? "none" : $"{snapshot.RepositoryUrl}@{snapshot.LoadedRevision}";

    private sealed record AssemblyResponseMetadata(
        string TargetPath,
        string Origin,
        string? SourcePath,
        string AssemblyHash,
        string GeneratedPath,
        string Confidence,
        string Trust,
        long Generation,
        string Status,
        string Completeness,
        SourceSnapshotIdentity? SourceSnapshot)
    {
        public string TargetType => "assembly";
    }
}
