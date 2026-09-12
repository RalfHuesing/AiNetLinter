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
        string? targetPath = null) =>
        Create(
            target,
            response.IsError == true ? "error" : "ok",
            response.IsError == true ? "not_applicable" : "complete",
            targetPath: targetPath);

    internal static McpNavigationPayload Create(
        AnalysisTarget? target,
        string operationStatus,
        string completeness,
        string? hint = null,
        string? code = null,
        string? targetPath = null)
    {
        var navigationTarget = target is not null
            ? new McpNavigationTarget(
                target.CanonicalPath,
                target.AnalysisRoot,
                target.Origin == AnalysisTargetOrigin.Source ? "source" : "assembly")
            : new McpNavigationTarget(targetPath ?? string.Empty, string.Empty, "none");
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
            new McpNavigationStatus(operationStatus, completeness, code),
            analysis,
            null,
            hint is null ? null : new McpNavigationNext("hint", hint));
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
