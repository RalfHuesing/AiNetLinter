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
        var operationStatus = code is null && !HasFeatureContextSectionFailure(response.StructuredContent)
            ? "ok"
            : "error";
        code = operationStatus == "ok" ? null : code ?? LinterErrorCodes.AnalysisFailed;
        var completeness = ResolveCompleteness(response.StructuredContent, code, operationStatus);
        var analysis = CreateAnalysis(target, operationStatus, response.StructuredContent);
        if (operationStatus == "ok" && analysis.Quality == "partial" && completeness == "complete")
        {
            completeness = "partial";
        }
        var hint = ReadString(response.StructuredContent, "hint")
            ?? ReadString(response.StructuredContent, "nextStep")
            ?? ReadNestedNextStep(response.StructuredContent);
        return Create(target, operationStatus, completeness, hint, code, response.StructuredContent, analysis);
    }

    // ainetlinter-disable MaxMethodParameterCount — die Projektion wird nur intern mit dem bereits normalisierten Status aufgerufen.
    internal static McpNavigationPayload Create(
        AnalysisTarget target,
        string operationStatus,
        string completeness,
        string? hint = null,
        string? code = null,
        JsonElement? structured = null,
        McpNavigationAnalysis? analysis = null)
    {
        var next = CreateNext(operationStatus, completeness, hint, structured);
        var snapshotFingerprint = target.AnalysisSnapshotFingerprint ?? string.Empty;
        var snapshotKind = target.AnalysisSnapshotFingerprint is null
            ? "unavailable"
            : NormalizeSnapshotKind(target.AnalysisSnapshotKind);

        return new McpNavigationPayload(
            ContractVersion: 2,
            new McpNavigationTarget(
                target.CanonicalPath,
                target.AnalysisRoot,
                target.Origin == AnalysisTargetOrigin.Source ? "source" : "assembly"),
            new McpNavigationSnapshot(
                snapshotFingerprint,
                snapshotKind,
                target.AnalysisSnapshotFingerprint is not null && target.AnalysisSnapshotFresh),
            new McpNavigationStatus(operationStatus, completeness, code),
            analysis ?? CreateAnalysis(target, operationStatus, structured),
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

    private static McpNavigationAnalysis CreateAnalysis(
        AnalysisTarget target,
        string operationStatus,
        JsonElement? structured)
    {
        if (operationStatus == "error") return new("not_applicable", "not_applicable", []);
        if (target.Origin == AnalysisTargetOrigin.Source) return new("source", "complete", []);

        var hasDiagnostics = HasAssemblyDiagnostics(structured);
        var isPartial = hasDiagnostics || HasAssemblyPartialStatus(structured);
        return new(
            "decompiled",
            isPartial ? "partial" : "complete",
            hasDiagnostics ? ["ASSEMBLY_DIAGNOSTICS"] : isPartial ? ["ASSEMBLY_ANALYSIS_PARTIAL"] : []);
    }

    private static bool HasAssemblyDiagnostics(JsonElement? structured) =>
        structured is { } value && HasNonEmptyProperty(value, "diagnostics")
        || structured is { } diagnostics && HasPositiveIntegerProperty(diagnostics, "diagnosticTotalCount");

    private static bool HasAssemblyPartialStatus(JsonElement? structured) =>
        structured is { } value && HasStringValue(value, "analysisQuality", "partial")
        || structured is { } status && HasStringValue(status, "status", "partial")
        || structured is { } completeness && HasStringValue(completeness, "completeness", "partial");

    private static bool HasNonEmptyProperty(JsonElement element, string propertyName) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(propertyName, out var property)
        && property.ValueKind == JsonValueKind.Array
        && property.GetArrayLength() > 0;

    private static bool HasPositiveIntegerProperty(JsonElement element, string propertyName) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(propertyName, out var property)
        && property.ValueKind == JsonValueKind.Number
        && property.TryGetInt32(out var value)
        && value > 0;

    private static bool HasStringValue(JsonElement element, string propertyName, string expected) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(propertyName, out var property)
        && property.ValueKind == JsonValueKind.String
        && string.Equals(property.GetString(), expected, StringComparison.OrdinalIgnoreCase);

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
    McpNavigationAnalysis Analysis,
    JsonObject? Scope,
    McpNavigationNext? Next,
    JsonObject? Handoff);

internal sealed record McpNavigationTarget(string TargetPath, string AnalysisRoot, string Origin);

internal sealed record McpNavigationSnapshot(string Fingerprint, string Kind, bool Fresh);

internal sealed record McpNavigationStatus(string Operation, string Completeness, string? Code);

internal sealed record McpNavigationAnalysis(
    string Mode,
    string Quality,
    IReadOnlyList<string> LimitationCodes);

internal sealed record McpNavigationNext(string Kind, string Action);
