#nullable enable

using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using AiNetLinter.Mcp.Tools.AssemblyAnalysis;
using AiNetLinter.Output;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Assemblies.Analysis;

internal static class AssemblyAnalysisResponse
{
    internal static CallToolResult Enrich(CallToolResult result, AssemblyAnalysisLease lease)
    {
        var origin = lease.Context.Origin;
        var metadata = new AssemblyResponseMetadata(
            lease.CanonicalPath,
            origin.OriginKind,
            origin.SourceProjectPath,
            origin.ContentHash,
            origin.GeneratedDocumentPath,
            origin.Confidence,
            origin.Trust,
            lease.Context.Generation,
            lease.Context.Status.ToWireValue(),
            lease.Context.Status.ToCompletenessLabel(),
            lease.Context.Diagnostics);

        var content = result.Content
            .Select(block => block is TextContentBlock text
                ? new TextContentBlock { Text = FormatHeader(metadata) + text.Text }
                : block)
            .ToList();
        JsonElement? structured = result.StructuredContent;
        if (structured is { ValueKind: JsonValueKind.Object })
        {
            var node = JsonNode.Parse(structured.Value.GetRawText()) as JsonObject ?? new JsonObject();
            node["analysis"] = JsonSerializer.SerializeToNode(metadata, McpJsonOptions.Default);
            structured = JsonSerializer.SerializeToElement(node, McpJsonOptions.Default);
        }

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
            hint: "Für dieses Assembly-Ziel eine unterstützte Roslyn-Abfrage oder targetType='project' verwenden.");
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
        $"[ASSEMBLY] targetType=assembly; targetPath={metadata.TargetPath}; origin={metadata.Origin}; " +
        $"confidence={metadata.Confidence}; trust={metadata.Trust}; generation={metadata.Generation}; " +
        $"status={metadata.Status}; completeness={metadata.Completeness}\n\n";

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
        IReadOnlyList<string> Diagnostics)
    {
        public string TargetType => "assembly";
    }
}
