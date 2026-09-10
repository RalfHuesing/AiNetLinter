#nullable enable

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using AiNetLinter;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Projects;
using AiNetLinter.Output;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Registration;

/// <summary>
/// Gemeinsame, kleine Statusprojektion fuer alle zielgebundenen Toolantworten. Fachpayloads
/// bleiben tool-spezifisch; diese Projektion beschreibt nur Ziel, Herkunft, Snapshot, Capability
/// und den naechsten sicheren Agentenschritt.
/// </summary>
internal static partial class McpNavigationProjection
{
    internal static McpNavigationPayload Create(CallToolResult response, AnalysisTarget target)
    {
        var code = ReadString(response.StructuredContent, "code");
        var operationStatus = ResolveOperationStatus(code, response);
        if (operationStatus == "ok" && HasFeatureContextSectionFailure(response.StructuredContent))
        {
            operationStatus = "error";
        }
        var completeness = ResolveCompleteness(response.StructuredContent, code, operationStatus);
        var hint = ReadString(response.StructuredContent, "hint")
            ?? ReadString(response.StructuredContent, "nextStep")
            ?? ReadNestedNextStep(response.StructuredContent);
        return Create(target, operationStatus, completeness, hint, code, response.StructuredContent);
    }

    // ainetlinter-disable MaxMethodParameterCount — die Projektion wird nur intern mit dem bereits normalisierten Status aufgerufen.
    internal static McpNavigationPayload Create(
        AnalysisTarget target,
        string operationStatus,
        string completeness,
        string? hint = null,
        string? code = null,
        JsonElement? structured = null)
    {
        var next = CreateNext(operationStatus, completeness, hint, structured);
        var snapshotFingerprint = target.AnalysisSnapshotFingerprint ?? string.Empty;
        var snapshotKind = target.AnalysisSnapshotFingerprint is null
            ? "unavailable"
            : NormalizeSnapshotKind(target.AnalysisSnapshotKind);

        return new McpNavigationPayload(
            ContractVersion: 1,
            new McpNavigationTarget(
                target.CanonicalPath,
                target.AnalysisRoot,
                target.Origin == AnalysisTargetOrigin.Source ? "source" : "assembly"),
            new McpNavigationSnapshot(
                snapshotFingerprint,
                snapshotKind,
                target.AnalysisSnapshotFingerprint is not null && target.AnalysisSnapshotFresh),
            new McpNavigationStatus(operationStatus, completeness, code),
            ReadOptionalObject(structured, "scope"),
            next,
            ReadOptionalObject(structured, "handoff"));
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

    private static string ResolveOperationStatus(string? code, CallToolResult response)
    {
        if (code is null) return IsLoading(response) ? "loading" : "ok";
        if (FixedStatuses.TryGetValue(code, out var status)) return status;
        return ResolveDynamicStatus(code, response.IsError == true);
    }

    private static readonly IReadOnlyDictionary<string, string> FixedStatuses =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [LinterErrorCodes.InvalidArgument] = "invalid_argument",
            [LinterErrorCodes.SymbolNotFound] = "symbol_not_found",
            [LinterErrorCodes.AmbiguousSymbol] = "ambiguous_symbol",
            [McpHandoffErrorCodes.TargetMismatch] = "target_mismatch",
            [McpHandoffErrorCodes.StaleSnapshot] = "stale_snapshot",
            [LinterErrorCodes.NotConfigured] = "not_configured",
            [LinterErrorCodes.AssemblyTargetUnsupported] = "unsupported",
            [LinterErrorCodes.ProjectTargetUnsupported] = "unsupported",
            [LinterErrorCodes.InvalidAssembly] = "invalid_assembly",
            [LinterErrorCodes.TargetUnreadable] = "target_unreadable",
            [ProjectErrorCodes.RulesInvalid] = "error",
            [LinterErrorCodes.ConfigInvalid] = "error",
            [LinterErrorCodes.ConfigNotFound] = "error",
            [LinterErrorCodes.ResourceNotFound] = "resource_not_found",
            [ProjectErrorCodes.ProjectNotInitialized] = "target_mismatch",
            [ProjectErrorCodes.ProjectLoadFailed] = "target_mismatch",
        };

    private static string ResolveDynamicStatus(string code, bool isError) =>
        code.Contains("STALE", StringComparison.OrdinalIgnoreCase)
            ? "stale_snapshot"
            : code.Contains("MISMATCH", StringComparison.OrdinalIgnoreCase)
                ? "target_mismatch"
                : isError ? "error" : "ok";

    private static bool IsLoading(CallToolResult response) =>
        response.Content is [{ } block]
        && block is TextContentBlock text
        && text.Text.Contains("Server laedt die Solution", StringComparison.OrdinalIgnoreCase);

    private static string ResolveCompleteness(
        JsonElement? structured,
        string? code,
        string operationStatus)
    {
        // Eine echte Wire-/responseBudget-Kürzung ist die unmittelbarere Aussage über
        // die ausgelieferte Antwort als ein fachlicher Partial-Status. Der Root-Payload
        // behält die fachliche Ursache (z. B. violations.status=error), während die
        // Navigation dem Agenten zusätzlich signalisiert, dass die Wire-Antwort selbst
        // unvollständig ist.
        if (HasWireBudgetTruncation(structured)) return "truncated";

        var terminal = ResolveTerminalCompleteness(structured, operationStatus);
        if (terminal is not null) return terminal;
        if (TryReadToolOwnedNavigationCompleteness(structured, out var navigationCompleteness))
            return navigationCompleteness;
        if (TryResolveAssemblyResultCompleteness(structured, out var assemblyCompleteness))
            return assemblyCompleteness;
        if (HasTruncation(structured)) return "truncated";
        return ResolvePayloadCompleteness(structured);
    }

    private static JsonObject? ReadOptionalObject(JsonElement? structured, string propertyName)
    {
        if (structured is not { ValueKind: JsonValueKind.Object } payload
            || !payload.TryGetProperty(propertyName, out var value)
            || value.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return JsonNode.Parse(value.GetRawText()) as JsonObject;
    }

    private static string NormalizeSnapshotKind(string value) =>
        value switch
        {
            "source-files" => "source",
            "decompiled" => "assembly",
            _ => value,
        };
}

internal sealed record McpNavigationPayload(
    int ContractVersion,
    McpNavigationTarget Target,
    McpNavigationSnapshot Snapshot,
    McpNavigationStatus Status,
    JsonObject? Scope,
    McpNavigationNext? Next,
    JsonObject? Handoff);

internal sealed record McpNavigationTarget(string TargetPath, string AnalysisRoot, string Origin);

internal sealed record McpNavigationSnapshot(string Fingerprint, string Kind, bool Fresh);

internal sealed record McpNavigationStatus(string Operation, string Completeness, string? Code);

internal sealed record McpNavigationNext(string Kind, string Action);
