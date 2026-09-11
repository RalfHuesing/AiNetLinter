#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AiNetLinter.Core;
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
    internal const int DefaultMaxResponseBytes = 32 * 1024;

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
            || result.Content.OfType<TextContentBlock>().FirstOrDefault() is not { } textBlock)
        {
            return result;
        }

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
        if (Fits(candidate, budget, navigation, navigationText)) return result;

        var minimum = ReduceToMinimum(candidate);
        var truncated = MarkTruncated(minimum, candidate);
        if (!Fits(truncated, budget, navigation, navigationText))
        {
            return BudgetTooSmall(budget, MinimumBytes(truncated, navigation, navigationText));
        }

        var current = candidate;
        while (!Fits(MarkTruncated(current, candidate), budget, navigation, navigationText))
        {
            var next = RemoveLowestPriorityUnit(current, candidate);
            if (ReferenceEquals(next, current))
            {
                return BudgetTooSmall(budget, MinimumBytes(truncated, navigation, navigationText));
            }

            current = next;
        }

        return CreateResult(MarkTruncated(current, candidate), navigation, navigationText);
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
                    .OrderBy(file => TestEvidencePriority(file.EvidenceKind))
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
        if (current.Callers is { CallSites.Count: > MinimumCallerCount } callers
            && original.Callers is { CallSites.Count: > MinimumCallerCount })
        {
            return current with { Callers = callers with { CallSites = callers.CallSites.Take(callers.CallSites.Count - 1).ToList() } };
        }

        if (current.Tests is { TestFiles.Count: > MinimumTestCount } tests
            && original.Tests is { TestFiles.Count: > MinimumTestCount })
        {
            var files = tests.TestFiles.Take(tests.TestFiles.Count - 1).ToList();
            return current with
            {
                Tests = tests with
                {
                    TestFiles = files,
                    DisplayedTestMethods = files.Sum(file => file.TestMethods.Count),
                },
            };
        }

        if (current.Violations is { Violations.Count: > MinimumViolationCount } violations
            && original.Violations is { Violations.Count: > MinimumViolationCount })
        {
            return current with { Violations = violations with { Violations = violations.Violations.Take(violations.Violations.Count - 1).ToList() } };
        }

        if (current.Metrics is { ThresholdChecks.Count: > MinimumMetricCount } metrics
            && original.Metrics is { ThresholdChecks.Count: > MinimumMetricCount })
        {
            return current with { Metrics = metrics with { ThresholdChecks = metrics.ThresholdChecks.Take(metrics.ThresholdChecks.Count - 1).ToList() } };
        }

        if (current.Declaration.Members is { Count: > 0 } members)
        {
            return current with { Declaration = current.Declaration with { Members = members.Take(members.Count - 1).ToList() } };
        }

        if (current.Declaration.BaseTypes is { Count: > 0 } baseTypes)
        {
            return current with { Declaration = current.Declaration with { BaseTypes = baseTypes.Take(baseTypes.Count - 1).ToList() } };
        }

        if (current.Declaration.Parameters.Count > 0)
        {
            return current with { Declaration = current.Declaration with { Parameters = current.Declaration.Parameters.Take(current.Declaration.Parameters.Count - 1).ToList() } };
        }

        if (current.Metrics?.TypeMetrics is { TopDependencies.Count: > 0 } typeMetrics)
        {
            return current with
            {
                Metrics = current.Metrics with
                {
                    TypeMetrics = typeMetrics with
                    {
                        TopDependencies = typeMetrics.TopDependencies.Take(typeMetrics.TopDependencies.Count - 1).ToList(),
                    },
                },
            };
        }

        return current;
    }

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
        CombinedBytes(CreateResult(payload, navigation, navigationText)) <= budget;

    private static int MinimumBytes(
        FeatureContextPayload payload,
        JsonNode? navigation,
        string? navigationText) =>
        CombinedBytes(CreateResult(payload, navigation, navigationText));

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
        McpToolResults.Recoverable(
            LinterErrorCodes.ResponseBudgetTooSmall,
            $"maxResponseBytes={budget} ist zu klein für die fachliche Mindestprojektion; Mindestwert: {minimumBytes} Bytes.",
            new McpErrorParameters(
                Hint: $"maxResponseBytes auf mindestens {minimumBytes} setzen; die Antwort wird nur an vollständigen Feature-/Caller-/Test-/Violation-Einheiten gekürzt.",
                FieldPath: "$.maxResponseBytes"));

    private static int CombinedBytes(CallToolResult result)
    {
        var textBytes = result.Content.OfType<TextContentBlock>().Sum(block => Encoding.UTF8.GetByteCount(block.Text));
        var structuredBytes = result.StructuredContent is { } structured
            ? Encoding.UTF8.GetByteCount(structured.GetRawText())
            : 0;
        return textBytes + structuredBytes;
    }

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

    private static int TestEvidencePriority(string evidenceKind) => evidenceKind switch
    {
        "directInvocation" => 0,
        "explicitMemberCoverage" => 1,
        "memberNameMatch" => 2,
        "directTypeUse" => 3,
        "explicitTypeCoverage" => 4,
        "typeNamingConvention" => 5,
        _ => 6,
    };
}
