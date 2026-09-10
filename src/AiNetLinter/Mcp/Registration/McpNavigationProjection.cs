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
        if (operationStatus == "ok" && HasFeatureContextSectionFailure(response.StructuredContent))
        {
            operationStatus = "error";
        }
        var completeness = ResolveCompleteness(response.StructuredContent, code, operationStatus);
        var hint = ReadString(response.StructuredContent, "hint")
            ?? ReadString(response.StructuredContent, "nextStep")
            ?? ReadNestedNextStep(response.StructuredContent);
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

    private static bool TryReadToolOwnedNavigationCompleteness(JsonElement? structured, out string value)
    {
        value = string.Empty;
        return structured is { ValueKind: JsonValueKind.Object } payload
            && payload.TryGetProperty("navigation", out var navigation)
            && navigation.ValueKind == JsonValueKind.Object
            && TryReadStringProperty(navigation, "completeness", out value);
    }

    private static bool TryResolveAssemblyResultCompleteness(JsonElement? structured, out string value)
    {
        value = string.Empty;
        if (structured is not { ValueKind: JsonValueKind.Object } payload
            || !payload.TryGetProperty("assemblyPath", out _))
        {
            return false;
        }

        foreach (var (itemsName, totalName) in new[]
        {
            ("types", "totalTypes"),
            ("extensions", "totalExtensions"),
        })
        {
            if (!payload.TryGetProperty(itemsName, out var items)
                || items.ValueKind != JsonValueKind.Array
                || !payload.TryGetProperty(totalName, out var total)
                || total.ValueKind != JsonValueKind.Number
                || !total.TryGetInt32(out var totalCount))
            {
                continue;
            }

            value = items.GetArrayLength() < totalCount ? "truncated" : "complete";
            return true;
        }

        return false;
    }

    private static string? ResolveTerminalCompleteness(JsonElement? structured, string operationStatus) =>
        operationStatus switch
        {
            "not_configured" => "not_configured",
            "unsupported" => "unsupported",
            "configuration_error" => "configuration_error",
            "error" => HasFeatureContextSectionFailure(structured) ? "partial" : "not_applicable",
            "invalid_argument" or "target_mismatch" or "stale_snapshot" or
                "symbol_not_found" or "ambiguous_symbol" or "invalid_assembly" or
                "target_unreadable" or "resource_not_found" => "not_applicable",
            _ => null,
        };

    private static string ResolvePayloadCompleteness(JsonElement? structured)
    {
        if (!TryFindCompleteness(structured, out var explicitValue))
            return IsKnownEmpty(structured) ? "empty" : "complete";
        if (explicitValue == "complete"
            && TryFindNonCompleteNestedCompleteness(structured, out var nestedValue))
        {
            return nestedValue;
        }
        var normalized = explicitValue == "error" ? "partial" : explicitValue;
        return normalized;
    }

    private static bool TryFindCompleteness(JsonElement? element, out string value)
    {
        value = string.Empty;
        if (element is not { ValueKind: JsonValueKind.Object } objectValue) return false;

        return TryReadSummaryCompleteness(objectValue, out value)
            || TryReadNestedCompleteness(objectValue, "analysis", out value)
            || TryReadNestedCompleteness(objectValue, "navigation", out value)
            || TryReadCompletenessProperty(objectValue, out value);
    }

    private static bool TryReadSummaryCompleteness(JsonElement owner, out string value)
    {
        value = string.Empty;
        if (!owner.TryGetProperty("summary", out var summary)
            || summary.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (TryReadStringProperty(summary, "completeness", out value)) return true;
        if (!TryReadStringProperty(summary, "status", out var status)) return false;

        value = status switch
        {
            "checked" => "complete",
            "empty" or "truncated" or "partial" or "not_configured" or "not_decidable" => status,
            _ => string.Empty,
        };
        return value.Length > 0;
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
            && (TryReadStringProperty(completeness, "status", out value)
                || TryReadTraversalCompleteness(completeness, out value));
    }

    private static bool TryReadTraversalCompleteness(JsonElement completeness, out string value)
    {
        value = string.Empty;
        if (HasTrueFlag(completeness, "truncatedByMaxResults")
            || HasTrueFlag(completeness, "truncatedByNodeLimit")
            || HasTrueFlag(completeness, "depthWasClamped")
            || HasTrueFlag(completeness, "truncated"))
        {
            value = "truncated";
            return true;
        }

        if (TryReadNonNegativeInt64(completeness, "totalCallSiteCount", out var total)
            && TryReadNonNegativeInt64(completeness, "shownCallSiteCount", out var shown)
            && shown < total)
        {
            value = "truncated";
            return true;
        }

        return false;
    }

    private static bool TryReadNonNegativeInt64(JsonElement owner, string propertyName, out long value)
    {
        value = 0;
        return owner.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.Number
            && property.TryGetInt64(out value)
            && value >= 0;
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

    private static bool TryFindNonCompleteNestedCompleteness(JsonElement? element, out string value)
    {
        value = string.Empty;
        if (element is not { ValueKind: JsonValueKind.Object } owner) return false;

        string? candidate = null;
        foreach (var propertyName in new[] { "metrics", "impact", "testContext", "tests", "callers", "violations" })
        {
            if (TryReadNestedSectionStatus(owner, propertyName, out var sectionValue))
                candidate = SelectLessComplete(candidate, sectionValue);
        }

        if (candidate is null) return false;
        value = candidate;
        return true;
    }

    private static bool TryReadNestedSectionStatus(JsonElement owner, string propertyName, out string value)
    {
        value = string.Empty;
        if (!owner.TryGetProperty(propertyName, out var section)
            || section.ValueKind != JsonValueKind.Object) return false;
        if (!TryReadCompletenessProperty(section, out value)
            && !TryReadStringProperty(section, "status", out value)) return false;
        if (value is "complete" or "empty") return false;
        value = value == "error" ? "partial" : value;
        return true;
    }

    private static string SelectLessComplete(string? current, string candidate) =>
        current is null || CompletenessPriority(candidate) > CompletenessPriority(current)
            ? candidate
            : current;

    private static int CompletenessPriority(string value) => value switch
    {
        "error" => 5,
        "not_decidable" => 4,
        "not_configured" => 3,
        "not_applicable" => 2,
        "truncated" => 2,
        "partial" => 1,
        _ => 0,
    };

    private static bool HasFeatureContextSectionFailure(JsonElement? element)
    {
        if (element is not { ValueKind: JsonValueKind.Object } owner
            || !owner.TryGetProperty("violations", out var violations)
            || violations.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        return (TryReadStringProperty(violations, "status", out var status) && status == "error")
            || (TryReadStringProperty(violations, "reasonCode", out var reasonCode)
                && reasonCode == "violations-scan-failed");
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

    private static bool HasWireBudgetTruncation(JsonElement? element)
    {
        if (element is not { } value) return false;
        return value.ValueKind switch
        {
            JsonValueKind.Object => HasWireBudgetTruncationInObject(value),
            JsonValueKind.Array => value.EnumerateArray().Any(item => HasWireBudgetTruncation(item)),
            _ => false,
        };
    }

    private static bool HasWireBudgetTruncationInObject(JsonElement value)
    {
        if (HasTrueFlag(value, "wireTruncated")) return true;
        if (value.TryGetProperty("wireBudget", out var wireBudget)
            && wireBudget.ValueKind == JsonValueKind.Object
            && HasTrueFlag(wireBudget, "truncated")) return true;
        if (value.TryGetProperty("truncatedBy", out var truncatedBy)
            && truncatedBy.ValueKind == JsonValueKind.Array
            && truncatedBy.EnumerateArray().Any(item =>
                item.ValueKind == JsonValueKind.String
                && string.Equals(item.GetString(), "responseBudget", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        foreach (var property in value.EnumerateObject())
        {
            if (property.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array
                && HasWireBudgetTruncation(property.Value)) return true;
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
        return operationStatus switch
        {
            "not_configured" => new("request_detail", "ainetlinter-rules.json neben dem adressierten Target anlegen und den Lint-Call wiederholen."),
            "unsupported" => new("refine_scope", "Ein Tool verwenden, das die Target-Herkunft unterstützt."),
            "configuration_error" => new("request_detail", hint ?? "ainetlinter-rules.json korrigieren und denselben Target-Call wiederholen."),
            "invalid_argument" or "target_mismatch" or "stale_snapshot" or "symbol_not_found" or "ambiguous_symbol" or "error"
                => CreateErrorNext(operationStatus, hint),
            _ => CreateSuccessfulNext(completeness, hint),
        };
    }

    private static McpNavigationNext CreateErrorNext(string operationStatus, string? hint) =>
        new(operationStatus is "symbol_not_found" or "ambiguous_symbol" ? "refine_scope" : "request_detail",
            hint ?? "Argumente und Target prüfen und den sicheren nächsten Schritt aus der Fehlermeldung ausführen.");

    private static McpNavigationNext CreateSuccessfulNext(string completeness, string? hint)
    {
        if (!string.IsNullOrWhiteSpace(hint)) return new("request_detail", hint);
        if (completeness is "not_configured" or "not_decidable" or "configuration_error")
        {
            return new("request_detail", "Die Entscheidbarkeit des angeforderten Scopes ist begrenzt; Konfiguration oder Scope prüfen und den Aufruf gezielt wiederholen.");
        }

        return completeness is "truncated" or "partial"
            ? new("request_detail", "Scope oder Detaillevel verfeinern und die Antwort gezielt wiederholen.")
            : completeness == "empty"
                ? new("refine_scope", "Keine Treffer im vollständig geprüften Scope; Suchmuster oder Scope verfeinern und erneut suchen.")
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

    private static string? ReadNestedNextStep(JsonElement? structured)
    {
        if (structured is not { ValueKind: JsonValueKind.Object } value) return null;

        foreach (var propertyName in new[] { "summary", "impact", "testContext", "tests", "callers", "violations" })
        {
            if (value.TryGetProperty(propertyName, out var section)
                && section.ValueKind == JsonValueKind.Object
                && TryReadNextStep(section, out var nextStep))
            {
                return nextStep.GetString();
            }
        }

        return null;
    }

    private static bool TryReadNextStep(JsonElement section, out JsonElement nextStep)
    {
        if (section.TryGetProperty("nextStep", out nextStep)
            && nextStep.ValueKind == JsonValueKind.String)
        {
            return true;
        }

        if (section.TryGetProperty("next", out var next)
            && next.ValueKind == JsonValueKind.Object
            && TryReadNextReason(next, out var reason))
        {
            nextStep = reason;
            return true;
        }

        nextStep = default;
        return false;
    }

    private static bool TryReadNextReason(JsonElement next, out JsonElement reason)
    {
        if (next.TryGetProperty("reason", out reason)
            && reason.ValueKind == JsonValueKind.String)
        {
            return true;
        }

        reason = default;
        return false;
    }
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
