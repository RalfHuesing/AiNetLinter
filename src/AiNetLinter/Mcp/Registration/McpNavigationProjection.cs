#nullable enable

using System.Collections.Generic;
using System.Text.Json.Nodes;
using AiNetLinter;
using AiNetLinter.Mcp.Projects;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Registration;

/// <summary>Builds the compact navigation hint that is rendered into a tool's text response.</summary>
internal static class McpNavigationProjection
{
    internal static McpNavigationPayload Create(
        CallToolResult response,
        AnalysisTarget? target,
        string? targetPath = null)
    {
        var text = response.Content.FirstOrDefault() is TextContentBlock textBlock ? textBlock.Text : null;
        var isError = response.IsError == true || IsErrorText(text);
        var hint = isError && text is not null ? ExtractAction(text) : null;
        return Create(new McpNavigationProjectionParameters(
            target,
            isError ? "error" : "ok",
            isError ? "not_applicable" : "complete",
            Hint: hint,
            TargetPath: targetPath));
    }

    private static bool IsErrorText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        return text.StartsWith("[ERROR]:", StringComparison.Ordinal)
            || text.StartsWith("verdict: error", StringComparison.Ordinal);
    }

    private static string? ExtractAction(string text)
    {
        string? hint = null;
        string? recovery = null;
        string? retry = null;

        foreach (var line in text.Split('\n'))
        {
            retry = TryExtractPrefix(line, "retry:") ?? retry;
            recovery = TryExtractPrefix(line, "recovery:") ?? recovery;
            hint = TryExtractPrefix(line, "hint:") ?? hint;
        }

        return retry ?? recovery ?? hint;
    }

    private static string? TryExtractPrefix(string line, string prefix)
    {
        var trimmed = line.Trim();
        if (!trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return null;

        var value = trimmed[prefix.Length..].Trim();
        return string.IsNullOrEmpty(value) ? null : value;
    }

    internal static McpNavigationPayload Create(McpNavigationProjectionParameters parameters)
    {
        var target = parameters.Target;
        var navigationTarget = target is not null
            ? new McpNavigationTarget(
                target.CanonicalPath,
                target.AnalysisRoot,
                target.Origin == AnalysisTargetOrigin.Source ? "source" : "assembly")
            : new McpNavigationTarget(parameters.TargetPath ?? string.Empty, string.Empty, "none");
        var snapshot = new McpNavigationSnapshot(
            target?.AnalysisSnapshotFingerprint ?? string.Empty,
            target?.AnalysisSnapshotFingerprint is null ? "unavailable" : "source",
            target?.AnalysisSnapshotFingerprint is not null && target.AnalysisSnapshotFresh);
        var analysis = target is null
            ? new McpNavigationAnalysis("not_applicable", "not_applicable", [])
            : new McpNavigationAnalysis(
                target.Origin == AnalysisTargetOrigin.Source ? "source" : "decompiled",
                "complete",
                []);
        return new McpNavigationPayload(
            2,
            navigationTarget,
            snapshot,
            new McpNavigationStatus(parameters.OperationStatus, parameters.Completeness, parameters.Code),
            analysis,
            null,
            parameters.Hint is null ? null : new McpNavigationNext("hint", parameters.Hint));
    }

    internal static AnalysisTarget WithSourceSnapshot(AnalysisTarget target, McpCodeGraphServer server)
    {
        var identity = server.HandoffSymbolIdentity;
        return identity is null
            ? target
            : target with
            {
                AnalysisSnapshotFingerprint = identity.ContentHash,
                AnalysisSnapshotKind = "source",
                AnalysisSnapshotFresh = true,
            };
    }
}

internal sealed record McpNavigationPayload(
    int ContractVersion,
    McpNavigationTarget Target,
    McpNavigationSnapshot Snapshot,
    McpNavigationStatus Status,
    McpNavigationAnalysis Analysis,
    JsonObject? Scope,
    McpNavigationNext? Next);

internal sealed record McpNavigationTarget(string TargetPath, string AnalysisRoot, string Origin);

internal sealed record McpNavigationSnapshot(string Fingerprint, string Kind, bool Fresh);

internal sealed record McpNavigationStatus(string Operation, string Completeness, string? Code);

internal sealed record McpNavigationAnalysis(
    string Mode,
    string Quality,
    IReadOnlyList<string> LimitationCodes);

internal sealed record McpNavigationNext(string Kind, string Action);
