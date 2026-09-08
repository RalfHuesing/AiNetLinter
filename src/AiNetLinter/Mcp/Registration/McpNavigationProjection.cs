#nullable enable

using System;
using System.Collections.Generic;
using System.Text.Json;
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
internal static class McpNavigationProjection
{
    internal static McpNavigationPayload Create(CallToolResult response, AnalysisTarget target)
    {
        var code = ReadString(response.StructuredContent, "code");
        var operationStatus = ResolveOperationStatus(code, response);
        var completeness = ResolveCompleteness(response.StructuredContent, code, operationStatus);
        var hint = ReadString(response.StructuredContent, "hint");
        return Create(target, operationStatus, completeness, hint, code);
    }

    // ainetlinter-disable MaxMethodParameterCount — die Projektion wird nur intern mit dem bereits normalisierten Status aufgerufen.
    internal static McpNavigationPayload Create(
        AnalysisTarget target,
        string operationStatus,
        string completeness,
        string? hint = null,
        string? code = null)
    {
        var next = CreateNext(operationStatus, completeness, hint);
        var lint = ResolveLintCapability(target, operationStatus);
        var snapshotFingerprint = target.AnalysisSnapshotFingerprint ?? string.Empty;
        var snapshotKind = target.AnalysisSnapshotFingerprint is null
            ? "unavailable"
            : target.AnalysisSnapshotKind;

        return new McpNavigationPayload(
            new McpNavigationTarget(target.CanonicalPath, target.AnalysisRoot, target.Fingerprint),
            target.Origin == AnalysisTargetOrigin.Source ? "source" : "decompiled",
            new McpNavigationSnapshot(
                snapshotFingerprint,
                snapshotKind,
                target.AnalysisSnapshotFingerprint is not null && target.AnalysisSnapshotFresh),
            new McpNavigationCapabilities(
                ToWire(target.Capabilities.Navigation),
                ToWire(lint)),
            operationStatus,
            new McpNavigationResult(
                Available: operationStatus is "ok"
                    && completeness is not ("empty" or "not_configured" or "unsupported"),
                Code: code),
            completeness,
            next);
    }

    internal static AnalysisTarget WithSourceSnapshot(AnalysisTarget target, McpCodeGraphServer server)
    {
        var identity = server.HandoffSymbolIdentity;
        return identity is null
            ? target
            : target with
            {
                AnalysisSnapshotFingerprint = identity.ContentHash,
                AnalysisSnapshotKind = "source-files",
                AnalysisSnapshotFresh = true,
            };
    }

    private static AnalysisCapabilityStatus ResolveLintCapability(
        AnalysisTarget target,
        string operationStatus) =>
        operationStatus == "configuration_error"
            ? AnalysisCapabilityStatus.NotConfigured
            : target.Capabilities.Lint;

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
            [ProjectErrorCodes.RulesInvalid] = "configuration_error",
            [LinterErrorCodes.ConfigInvalid] = "configuration_error",
            [LinterErrorCodes.ConfigNotFound] = "configuration_error",
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
        if (operationStatus == "not_configured") return "not_configured";
        if (operationStatus == "unsupported") return "unsupported";
        if (operationStatus == "configuration_error") return "configuration_error";
        if (operationStatus is "invalid_argument" or "target_mismatch" or "stale_snapshot" or
            "symbol_not_found" or "ambiguous_symbol" or "error") return "not_applicable";

        if (TryFindCompleteness(structured, out var explicitValue))
        {
            return explicitValue == "complete" && IsKnownEmpty(structured)
                ? "empty"
                : explicitValue;
        }
        if (HasTruncation(structured)) return "truncated";
        if (IsKnownEmpty(structured)) return "empty";
        return "complete";
    }

    private static bool TryFindCompleteness(JsonElement? element, out string value)
    {
        value = string.Empty;
        if (element is not { ValueKind: JsonValueKind.Object } objectValue) return false;

        return TryReadNestedCompleteness(objectValue, "analysis", out value)
            || TryReadNestedCompleteness(objectValue, "navigation", out value)
            || TryReadCompletenessProperty(objectValue, out value);
    }

    private static bool TryReadNestedCompleteness(JsonElement owner, string propertyName, out string value)
    {
        value = string.Empty;
        return owner.TryGetProperty(propertyName, out var nested)
            && nested.ValueKind == JsonValueKind.Object
            && TryReadStringProperty(nested, "completeness", out value);
    }

    private static bool TryReadCompletenessProperty(JsonElement owner, out string value)
    {
        value = string.Empty;
        if (!owner.TryGetProperty("completeness", out var completeness)) return false;
        if (completeness.ValueKind == JsonValueKind.String)
        {
            value = completeness.GetString()!.ToLowerInvariant();
            return true;
        }

        return completeness.ValueKind == JsonValueKind.Object
            && TryReadStringProperty(completeness, "status", out value);
    }

    private static bool TryReadStringProperty(JsonElement owner, string propertyName, out string value)
    {
        value = string.Empty;
        if (!owner.TryGetProperty(propertyName, out var property)
            || property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = property.GetString()!.ToLowerInvariant();
        return true;
    }

    private static bool HasTruncation(JsonElement? element)
    {
        if (element is not { } value) return false;
        return value.ValueKind switch
        {
            JsonValueKind.Object => HasTruncationInObject(value),
            JsonValueKind.Array => HasTruncationInArray(value),
            _ => false,
        };
    }

    private static bool HasTruncationInObject(JsonElement value)
    {
        if (HasTrueFlag(value, "truncated") || HasTrueFlag(value, "isTruncated") || HasTrueFlag(value, "wireTruncated")) return true;
        foreach (var property in value.EnumerateObject())
        {
            if (property.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array && HasTruncation(property.Value)) return true;
        }

        return false;
    }

    private static bool HasTruncationInArray(JsonElement value)
    {
        foreach (var item in value.EnumerateArray())
        {
            if (HasTruncation(item)) return true;
        }

        return false;
    }

    private static bool HasTrueFlag(JsonElement value, string propertyName) =>
        value.TryGetProperty(propertyName, out var flag) && flag.ValueKind == JsonValueKind.True;

    private static bool IsKnownEmpty(JsonElement? element)
    {
        return element is { ValueKind: JsonValueKind.Object } value
            && (HasEmptyArray(value, "matches")
                || HasEmptyArray(value, "violations")
                || HasEmptyArray(value, "callSites")
                || HasEmptyResults(value));
    }

    private static bool HasEmptyArray(JsonElement value, string propertyName) =>
        value.TryGetProperty(propertyName, out var property)
        && property.ValueKind == JsonValueKind.Array
        && property.GetArrayLength() == 0;

    private static bool HasEmptyResults(JsonElement value)
    {
        if (!value.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array) return false;
        if (results.GetArrayLength() == 0) return true;
        foreach (var result in results.EnumerateArray())
        {
            if (!HasEmptyArray(result, "matches")) return false;
        }

        return true;
    }

    private static McpNavigationNext CreateNext(string operationStatus, string completeness, string? hint)
    {
        if (operationStatus == "not_configured")
        {
            return new("request_detail", "ainetlinter-rules.json neben dem adressierten Target anlegen und den Lint-Call wiederholen.");
        }

        if (operationStatus == "unsupported")
        {
            return new("refine_scope", "Ein Tool verwenden, das die Target-Herkunft unterstützt.");
        }

        if (operationStatus == "configuration_error")
        {
            return new("request_detail", hint ?? "ainetlinter-rules.json korrigieren und denselben Target-Call wiederholen.");
        }

        if (operationStatus is "invalid_argument" or "target_mismatch" or "stale_snapshot" or
            "symbol_not_found" or "ambiguous_symbol" or "error")
        {
            return new(
                operationStatus is "symbol_not_found" or "ambiguous_symbol" ? "refine_scope" : "request_detail",
                hint ?? "Argumente und Target prüfen und den sicheren nächsten Schritt aus der Fehlermeldung ausführen.");
        }

        return completeness is "truncated" or "partial"
            ? new("request_detail", hint ?? "Scope oder Detaillevel verfeinern und die Antwort gezielt wiederholen.")
            : completeness == "empty"
                ? new("refine_scope", hint ?? "Keine Treffer im vollständig geprüften Scope; Suchmuster oder Scope verfeinern und erneut suchen.")
            : new("none", "Kein weiterer Schritt erforderlich.");
    }

    private static string ToWire(AnalysisCapabilityStatus status) =>
        status switch
        {
            AnalysisCapabilityStatus.Supported => "supported",
            AnalysisCapabilityStatus.NotConfigured => "not_configured",
            _ => "unsupported",
        };

    private static string? ReadString(JsonElement? structured, string propertyName) =>
        structured is { ValueKind: JsonValueKind.Object } value
        && value.TryGetProperty(propertyName, out var property)
        && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
}

internal sealed record McpNavigationPayload(
    McpNavigationTarget Target,
    string Origin,
    McpNavigationSnapshot Snapshot,
    McpNavigationCapabilities Capabilities,
    string OperationStatus,
    McpNavigationResult Result,
    string Completeness,
    McpNavigationNext Next);

internal sealed record McpNavigationTarget(string TargetPath, string AnalysisRoot, string Fingerprint);

internal sealed record McpNavigationSnapshot(string Fingerprint, string Kind, bool Fresh);

internal sealed record McpNavigationCapabilities(string Navigation, string Lint);

internal sealed record McpNavigationResult(bool Available, string? Code = null);

internal sealed record McpNavigationNext(string Kind, string Action);
