#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AiNetLinter.Core;
using AiNetLinter.Mcp.Wire;
using AiNetLinter.Mcp.Tools.Common;
using AiNetLinter.Mcp.Tools.MetricsLookup;
using AiNetLinter.Output;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Tools.FeatureContext;

/// <summary>
/// Fachliche Budgetprojektion fuer <c>get_feature_context</c>. Die Auswahl wird einmal
/// typisiert getroffen und daraus werden Text und StructuredContent gemeinsam erzeugt.
/// </summary>
internal static class FeatureContextResponseBudget
{
    internal const int DefaultMaxResponseBytes = 64 * 1024;

    private const string BudgetReason = "responseBudget";
    private const int MinimumCallerCount = 1;
    private const int MinimumTestCount = 1;
    private const int MinimumViolationCount = 1;
    private const int MinimumMetricCount = 1;

    internal static CallToolResult Apply(FeatureContextPayload payload, int maxResponseBytes)
    {
        var budget = NormalizeBudget(maxResponseBytes);
        var candidate = Prepare(payload);
        if (Fits(candidate, budget, navigation: null, navigationText: null))
        {
            return CreateResult(candidate, navigation: null, navigationText: null);
        }

        var minimum = ReduceToMinimum(candidate);
        var truncated = MarkTruncated(minimum, candidate);
        if (!Fits(truncated, budget, navigation: null, navigationText: null))
        {
            return BudgetTooSmall(budget, MinimumBytes(truncated, navigation: null, navigationText: null));
        }

        var current = candidate;
        while (!Fits(MarkTruncated(current, candidate), budget, navigation: null, navigationText: null))
        {
            var next = RemoveLowestPriorityUnit(current, candidate);
            if (ReferenceEquals(next, current))
            {
                return BudgetTooSmall(budget, MinimumBytes(truncated, navigation: null, navigationText: null));
            }

            current = next;
        }

        return CreateResult(MarkTruncated(current, candidate), navigation: null, navigationText: null);
    }

    internal static CallToolResult ApplyFinal(CallToolResult result, int maxResponseBytes)
    {
        if (result.StructuredContent is not { ValueKind: JsonValueKind.Object } structured
            || !structured.TryGetProperty("declaration", out _)
            || result.Content.OfType<TextContentBlock>().FirstOrDefault() is not { } textBlock) return result;

        FeatureContextPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<FeatureContextPayload>(
                structured.GetRawText(), McpJsonOptions.Default);
        }
        catch (JsonException)
        {
            return result;
        }

        if (payload is null) return result;

        var budget = NormalizeBudget(maxResponseBytes);
        var structuredNode = JsonNode.Parse(structured.GetRawText()) as JsonObject;
        var navigation = structuredNode?["navigation"]?.DeepClone();
        var navigationText = ExtractNavigationText(textBlock.Text);
        var candidate = Prepare(payload);
        if (FitsFinal(candidate, budget, navigation, navigationText))
        {
            return WithWireBudget(result, budget, candidate);
        }

        var minimum = ReduceToMinimum(candidate);
        var truncated = MarkTruncated(minimum, candidate);
        if (!FitsFinal(truncated, budget, navigation, navigationText))
        {
            return BudgetTooSmall(budget, MinimumBytesFinal(truncated, navigation, navigationText));
        }

        var current = candidate;
        while (!FitsFinal(MarkTruncated(current, candidate), budget, navigation, navigationText))
        {
            var next = RemoveLowestPriorityUnit(current, candidate);
            if (ReferenceEquals(next, current))
            {
                return BudgetTooSmall(budget, MinimumBytesFinal(truncated, navigation, navigationText));
            }

            current = next;
        }

        var projected = MarkTruncated(current, candidate);
        return WithWireBudget(CreateResult(projected, navigation, navigationText), budget, projected);
    }

    private static int NormalizeBudget(int maxResponseBytes) =>
        maxResponseBytes <= 0 ? DefaultMaxResponseBytes : maxResponseBytes;

    private static FeatureContextPayload Prepare(FeatureContextPayload payload)
    {
        var callers = payload.Callers is null
            ? null
            : payload.Callers with
            {
                CallSites = payload.Callers.CallSites
                    .OrderBy(IsTestCaller)
                    .ThenBy(call => PathNormalizer.NormalizeSeparators(call.FilePath), StringComparer.OrdinalIgnoreCase)
                    .ThenBy(call => call.Line)
                    .ThenBy(call => call.ProjectName, StringComparer.Ordinal)
                    .ThenBy(call => call.CallerMemberName ?? string.Empty, StringComparer.Ordinal)
                    .ThenBy(call => call.SymbolName, StringComparer.Ordinal)
                    .ToList(),
            };
        var tests = payload.Tests is null
            ? null
            : payload.Tests with
            {
                TestFiles = payload.Tests.TestFiles
                    .OrderBy(file => TestEvidencePriorities.For(file.EvidenceKind))
                    .ThenBy(file => file.FilePath, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(file => file.TestClassName, StringComparer.Ordinal)
                    .ToList(),
            };
        var violations = payload.Violations is null
            ? null
            : payload.Violations with
            {
                Violations = payload.Violations.Violations
                    .OrderBy(item => item.IsDirectlyOnSymbol ? 0 : 1)
                    .ThenBy(item => item.Line)
                    .ThenBy(item => item.RuleId, StringComparer.Ordinal)
                    .ThenBy(item => item.Message, StringComparer.Ordinal)
                    .ToList(),
            };
        var metrics = payload.Metrics is null
            ? null
            : payload.Metrics with
            {
                ThresholdChecks = payload.Metrics.ThresholdChecks
                    .OrderBy(check => MetricPriority(check.Status))
                    .ThenBy(check => check.Metric, StringComparer.Ordinal)
                    .ThenBy(check => check.RuleId, StringComparer.Ordinal)
                    .ToList(),
            };

        return payload with { Metrics = metrics, Callers = callers, Tests = tests, Violations = violations };
    }

    private static FeatureContextPayload ReduceToMinimum(FeatureContextPayload payload)
    {
        var callers = payload.Callers is null
            ? null
            : payload.Callers with
            {
                CallSites = payload.Callers.CallSites.Take(Math.Min(MinimumCallerCount, payload.Callers.CallSites.Count)).ToList(),
            };
        var tests = payload.Tests is null
            ? null
            : payload.Tests with
            {
                TestFiles = payload.Tests.TestFiles.Take(Math.Min(MinimumTestCount, payload.Tests.TestFiles.Count)).ToList(),
                DisplayedTestMethods = payload.Tests.TestFiles.Take(Math.Min(MinimumTestCount, payload.Tests.TestFiles.Count))
                    .Sum(file => file.TestMethods.Count),
            };
        var violations = payload.Violations is null
            ? null
            : payload.Violations with
            {
                Violations = payload.Violations.Violations.Take(Math.Min(MinimumViolationCount, payload.Violations.Violations.Count)).ToList(),
            };
        var metrics = payload.Metrics is null
            ? null
            : payload.Metrics with
            {
                ThresholdChecks = payload.Metrics.ThresholdChecks
                    .Take(Math.Min(MinimumMetricCount, payload.Metrics.ThresholdChecks.Count)).ToList(),
            };
        var declaration = payload.Declaration with
        {
            Members = null,
            BaseTypes = null,
            Parameters = [],
        };
        return payload with { Declaration = declaration, Metrics = metrics, Callers = callers, Tests = tests, Violations = violations };
    }

    private static FeatureContextPayload RemoveLowestPriorityUnit(
        FeatureContextPayload current,
        FeatureContextPayload original)
    {
        return RemoveCaller(current, original)
            ?? RemoveTestFile(current, original)
            ?? RemoveViolation(current, original)
            ?? RemoveMetric(current, original)
            ?? RemoveDeclarationDetail(current)
            ?? RemoveTypeDependency(current)
            ?? current;
    }

    private static FeatureContextPayload? RemoveCaller(FeatureContextPayload current, FeatureContextPayload original) =>
        current.Callers is { CallSites.Count: > MinimumCallerCount } callers && original.Callers is { CallSites.Count: > MinimumCallerCount }
            ? current with { Callers = callers with { CallSites = callers.CallSites.Take(callers.CallSites.Count - 1).ToList() } } : null;
    private static FeatureContextPayload? RemoveTestFile(FeatureContextPayload current, FeatureContextPayload original)
    {
        if (current.Tests is not { TestFiles.Count: > MinimumTestCount } tests || original.Tests is not { TestFiles.Count: > MinimumTestCount }) return null;
        var files = tests.TestFiles.Take(tests.TestFiles.Count - 1).ToList();
        return current with { Tests = tests with { TestFiles = files, DisplayedTestMethods = files.Sum(file => file.TestMethods.Count) } };
    }
    private static FeatureContextPayload? RemoveViolation(FeatureContextPayload current, FeatureContextPayload original) =>
        current.Violations is { Violations.Count: > MinimumViolationCount } violations && original.Violations is { Violations.Count: > MinimumViolationCount }
            ? current with { Violations = violations with { Violations = violations.Violations.Take(violations.Violations.Count - 1).ToList() } } : null;
    private static FeatureContextPayload? RemoveMetric(FeatureContextPayload current, FeatureContextPayload original) =>
        current.Metrics is { ThresholdChecks.Count: > MinimumMetricCount } metrics && original.Metrics is { ThresholdChecks.Count: > MinimumMetricCount }
            ? current with { Metrics = metrics with { ThresholdChecks = metrics.ThresholdChecks.Take(metrics.ThresholdChecks.Count - 1).ToList() } } : null;
    private static FeatureContextPayload? RemoveDeclarationDetail(FeatureContextPayload current)
    {
        if (current.Declaration.Members is { Count: > 0 } members) return current with { Declaration = current.Declaration with { Members = members.Take(members.Count - 1).ToList() } };
        if (current.Declaration.BaseTypes is { Count: > 0 } baseTypes) return current with { Declaration = current.Declaration with { BaseTypes = baseTypes.Take(baseTypes.Count - 1).ToList() } };
        return current.Declaration.Parameters.Count > 0 ? current with { Declaration = current.Declaration with { Parameters = current.Declaration.Parameters.Take(current.Declaration.Parameters.Count - 1).ToList() } } : null;
    }
    private static FeatureContextPayload? RemoveTypeDependency(FeatureContextPayload current) =>
        current.Metrics?.TypeMetrics is { TopDependencies.Count: > 0 } typeMetrics
            ? current with { Metrics = current.Metrics with { TypeMetrics = typeMetrics with { TopDependencies = typeMetrics.TopDependencies.Take(typeMetrics.TopDependencies.Count - 1).ToList() } } } : null;

    private static FeatureContextPayload MarkTruncated(
        FeatureContextPayload candidate,
        FeatureContextPayload original)
    {
        var callers = candidate.Callers;
        if (callers is not null && original.Callers is not null && callers.CallSites.Count < original.Callers.CallSites.Count)
        {
            callers = callers with
            {
                IsTruncated = true,
                Completeness = FeatureContextStatus.Truncated,
                TruncatedBy = AddReason(callers.TruncatedBy),
                NextStep = "Abschnitt impact: maxResponseBytes erhöhen oder den Caller-Scope gezielt verfeinern.",
            };
        }

        var tests = candidate.Tests;
        if (tests is not null && original.Tests is not null
            && tests.TestFiles.Count < original.Tests.TestFiles.Count)
        {
            tests = tests with
            {
                IsTruncated = true,
                Completeness = FeatureContextStatus.Truncated,
                TruncatedBy = AddReason(tests.TruncatedBy),
                NextStep = "Abschnitt testContext: maxResponseBytes erhöhen oder die Testauswahl gezielt verfeinern.",
            };
        }

        var violations = candidate.Violations;
        if (violations is not null && original.Violations is not null
            && violations.Violations.Count < original.Violations.Violations.Count)
        {
            violations = violations with
            {
                IsTruncated = true,
                Status = FeatureContextStatus.Truncated,
                TruncatedBy = AddReason(violations.TruncatedBy),
                NextStep = "Abschnitt violations: maxResponseBytes erhöhen oder die Datei gezielt erneut prüfen.",
            };
        }

        return candidate with
        {
            Callers = callers,
            Tests = tests,
            Violations = violations,
            Completeness = FeatureContextStatus.Truncated,
            NextStep = "maxResponseBytes erhöhen oder eine gezielte Detailabfrage mit engerem Scope verwenden.",
        };
    }

    private static IReadOnlyList<string> AddReason(IReadOnlyList<string>? reasons) =>
        (reasons ?? [])
            .Append(BudgetReason)
            .Distinct(StringComparer.Ordinal)
            .ToList();

    private static bool Fits(
        FeatureContextPayload payload,
        int budget,
        JsonNode? navigation,
        string? navigationText) =>
        McpResponseSize.From(CreateResult(payload, navigation, navigationText)).TotalBytes <= budget;

    private static int MinimumBytes(
        FeatureContextPayload payload,
        JsonNode? navigation,
        string? navigationText) =>
        McpResponseSize.From(CreateResult(payload, navigation, navigationText)).TotalBytes;

    private static bool FitsFinal(
        FeatureContextPayload payload,
        int budget,
        JsonNode? navigation,
        string? navigationText) =>
        McpResponseSize.From(WithWireBudget(CreateResult(payload, navigation, navigationText), budget, payload)).TotalBytes <= budget;

    private static int MinimumBytesFinal(
        FeatureContextPayload payload,
        JsonNode? navigation,
        string? navigationText) =>
        McpResponseSize.From(WithWireBudget(CreateResult(payload, navigation, navigationText), 0, payload)).TotalBytes;

    private static CallToolResult CreateResult(
        FeatureContextPayload payload,
        JsonNode? navigation,
        string? navigationText)
    {
        var text = FeatureContextFormatter.FormatReport(payload);
        if (!string.IsNullOrWhiteSpace(navigationText)) text += "\n" + navigationText.Trim();

        var structured = JsonSerializer.SerializeToNode(payload, McpJsonOptions.Default) as JsonObject ?? new JsonObject();
        if (navigation is not null) structured["navigation"] = navigation.DeepClone();
        return new CallToolResult
        {
            IsError = false,
            Content = [new TextContentBlock { Text = text }],
            StructuredContent = JsonSerializer.SerializeToElement(structured, McpJsonOptions.Default),
        };
    }

    private static CallToolResult BudgetTooSmall(int budget, int minimumBytes) =>
        McpToolResults.Error(
            LinterErrorCodes.ResponseBudgetTooSmall,
            $"maxResponseBytes={budget} ist zu klein für die fachliche Mindestprojektion; Mindestwert: {minimumBytes} Bytes.",
            new McpErrorParameters(
                Hint: $"maxResponseBytes auf mindestens {minimumBytes} setzen; die Antwort wird nur an vollständigen Feature-/Caller-/Test-/Violation-Einheiten gekürzt.",
                FieldPath: "$.maxResponseBytes",
                RequestedBytes: budget,
                MinimumResponseBytes: minimumBytes));

    private static CallToolResult WithWireBudget(
        CallToolResult result,
        int budget,
        FeatureContextPayload payload)
    {
        var node = JsonNode.Parse(result.StructuredContent!.Value.GetRawText())!.AsObject();
        var textBytes = Encoding.UTF8.GetByteCount(result.Content.OfType<TextContentBlock>().First().Text);
        var truncated = HasResponseBudgetTruncation(payload);
        for (var attempt = 0; attempt < 8; attempt++)
        {
            node["wireBudget"] = new JsonObject
            {
                ["limitBytes"] = budget,
                ["textBytes"] = textBytes,
                ["structuredBytes"] = 0,
                ["totalBytes"] = 0,
                ["truncated"] = truncated,
            };
            var structuredBytes = Encoding.UTF8.GetByteCount(node.ToJsonString(McpJsonOptions.Default));
            var wireBudget = node["wireBudget"]!.AsObject();
            wireBudget["structuredBytes"] = structuredBytes;
            wireBudget["totalBytes"] = textBytes + structuredBytes;
            var projected = JsonSerializer.SerializeToElement(node, McpJsonOptions.Default);
            var candidate = ReplaceStructuredContent(result, projected);
            if (McpResponseSize.From(candidate).StructuredBytes == structuredBytes) return candidate;
        }

        return ReplaceStructuredContent(result, JsonSerializer.SerializeToElement(node, McpJsonOptions.Default));
    }

    private static CallToolResult ReplaceStructuredContent(CallToolResult result, JsonElement structuredContent) => new()
    {
        IsError = result.IsError,
        Content = result.Content,
        StructuredContent = structuredContent,
    };

    private static bool HasResponseBudgetTruncation(FeatureContextPayload payload) =>
        payload.Callers?.TruncatedBy?.Contains(BudgetReason, StringComparer.Ordinal) == true
        || payload.Tests?.TruncatedBy?.Contains(BudgetReason, StringComparer.Ordinal) == true
        || payload.Violations?.TruncatedBy?.Contains(BudgetReason, StringComparer.Ordinal) == true;

    private static string? ExtractNavigationText(string text)
    {
        var index = text.IndexOf("## Navigation", StringComparison.Ordinal);
        return index < 0 ? null : text[index..].Trim();
    }

    private static int MetricPriority(string status) =>
        status.Equals(ThresholdStatus.Violation, StringComparison.OrdinalIgnoreCase) ? 0
        : status.Equals(ThresholdStatus.Warn, StringComparison.OrdinalIgnoreCase) ? 1
        : 2;

    private static bool IsTestCaller(CallSiteEntry call) =>
        TestDetector.IsTestFile(call.FilePath)
        || call.ProjectName.Contains("test", StringComparison.OrdinalIgnoreCase);

}
