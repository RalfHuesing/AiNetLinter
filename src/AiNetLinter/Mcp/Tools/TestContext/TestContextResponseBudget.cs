#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using AiNetLinter.Core;
using AiNetLinter.Mcp.Wire;
using AiNetLinter.Mcp.Tools.Common;
using AiNetLinter.Mcp.Registration;
using AiNetLinter.Output;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Tools.TestContext;

/// <summary>
/// Fachliche Budgetprojektion fuer <c>get_test_context</c>. Sie waehlt ganze
/// Testkandidaten aus und baut Text und StructuredContent aus derselben Auswahl.
/// </summary>
internal static class TestContextResponseBudget
{
    internal const int DefaultMaxResponseBytes = 24 * 1024;
    private const int ProtectedConcreteCandidateCount = 3;

    internal static CallToolResult Apply(TestContextPayload payload, int maxResponseBytes)
    {
        var budget = NormalizeBudget(maxResponseBytes);
        var original = Prepare(payload);
        if (Fits(original, budget, null, null)) return CreateResult(original, null, null);

        var minimum = MarkResponseBudgetTruncation(TakeCandidates(original, ProtectedCandidateCount(original)), original);
        if (!Fits(minimum, budget, null, null)) return BudgetTooSmall(budget, SizeOf(minimum, null, null));

        var current = original;
        while (!Fits(MarkResponseBudgetTruncation(current, original), budget, null, null))
        {
            if (current.TestFiles.Count <= ProtectedCandidateCount(original))
            {
                return BudgetTooSmall(budget, SizeOf(minimum, null, null));
            }

            current = TakeCandidates(current, current.TestFiles.Count - 1);
        }

        return CreateResult(MarkResponseBudgetTruncation(current, original), null, null);
    }

    internal static CallToolResult ApplyFinal(CallToolResult result, int maxResponseBytes)
    {
        if (result.StructuredContent is not { ValueKind: JsonValueKind.Object } structured
            || result.Content.OfType<TextContentBlock>().FirstOrDefault() is not { } textBlock)
        {
            return result;
        }

        // Post-navigation budget callbacks also receive recoverable errors. Their
        // structured error envelope is not a TestContextPayload and must remain intact.
        if (!structured.TryGetProperty("testFiles", out _)) return result;

        TestContextPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<TestContextPayload>(structured.GetRawText(), McpJsonOptions.Default);
        }
        catch (JsonException)
        {
            return result;
        }

        if (payload is null) return result;
        var navigation = (JsonNode.Parse(structured.GetRawText()) as JsonObject)?["navigation"]?.DeepClone();
        var navigationText = ExtractNavigationText(textBlock.Text, payload);
        var budget = NormalizeBudget(maxResponseBytes);
        var original = Prepare(payload);
        if (Fits(original, budget, navigation, navigationText)) return result;

        var minimum = MarkResponseBudgetTruncation(TakeCandidates(original, ProtectedCandidateCount(original)), original);
        if (!Fits(minimum, budget, navigation, navigationText))
        {
            return BudgetTooSmall(budget, SizeOf(minimum, navigation, navigationText));
        }

        var current = original;
        while (!Fits(MarkResponseBudgetTruncation(current, original), budget, navigation, navigationText))
        {
            if (current.TestFiles.Count <= ProtectedCandidateCount(original))
            {
                return BudgetTooSmall(budget, SizeOf(minimum, navigation, navigationText));
            }

            current = TakeCandidates(current, current.TestFiles.Count - 1);
        }

        return CreateResult(MarkResponseBudgetTruncation(current, original), navigation, navigationText);
    }

    private static TestContextPayload Prepare(TestContextPayload payload) => payload with
    {
        TestFiles = payload.TestFiles
            .OrderBy(file => TestEvidencePriorities.For(file.EvidenceKind))
            .ThenBy(file => file.FilePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(file => file.TestClassName, StringComparer.Ordinal)
            .ToList(),
    };

    private static int ProtectedCandidateCount(TestContextPayload payload) =>
        Math.Min(ProtectedConcreteCandidateCount, payload.TestFiles.Count);

    private static TestContextPayload TakeCandidates(TestContextPayload payload, int count)
    {
        var files = payload.TestFiles.Take(count).ToList();
        return payload with
        {
            TestFiles = files,
            RecommendedTestCommands = GetTestContextTool.BuildRecommendedCommands(
                files.Select(ToCoverageResult).ToList()),
            ReturnedTestFiles = files.Count,
            ReturnedTestMethods = files.Sum(file => file.TestMethods.Count),
        };
    }

    private static TestContextPayload MarkResponseBudgetTruncation(
        TestContextPayload candidate,
        TestContextPayload original)
    {
        if (candidate.TestFiles.Count >= original.TestFiles.Count) return candidate;
        var reasons = (candidate.TruncatedBy ?? [])
            .Append("responseBudget")
            .Distinct(StringComparer.Ordinal)
            .ToList();
        return candidate with
        {
            IsTruncated = true,
            Completeness = "truncated",
            TruncatedBy = reasons,
            NextStep = "maxResponseBytes erhöhen oder die Testauswahl gezielt verfeinern.",
        };
    }

    private static CallToolResult CreateResult(TestContextPayload payload, JsonNode? navigation, string? navigationText)
    {
        var text = TestContextFormatter.FormatReport(payload);
        if (!string.IsNullOrWhiteSpace(navigationText)) text += "\n" + navigationText;
        var structured = JsonSerializer.SerializeToNode(payload, McpJsonOptions.Default) as JsonObject ?? new JsonObject();
        if (navigation is not null) structured["navigation"] = navigation.DeepClone();
        return new CallToolResult
        {
            IsError = false,
            Content = [new TextContentBlock { Text = text }],
            StructuredContent = JsonSerializer.SerializeToElement(structured, McpJsonOptions.Default),
        };
    }

    private static bool Fits(TestContextPayload payload, int budget, JsonNode? navigation, string? navigationText) =>
        McpResponseSize.From(CreateResult(payload, navigation, navigationText)).TotalBytes <= budget;

    private static int SizeOf(TestContextPayload payload, JsonNode? navigation, string? navigationText) =>
        McpResponseSize.From(CreateResult(payload, navigation, navigationText)).TotalBytes;

    private static CallToolResult BudgetTooSmall(int budget, int minimumBytes) =>
        McpToolResults.Error(
            LinterErrorCodes.ResponseBudgetTooSmall,
            $"maxResponseBytes={budget} ist zu klein für bis zu drei konkrete Testkandidaten; Mindestwert: {minimumBytes} Bytes.",
            new McpErrorParameters(
                Hint: $"maxResponseBytes auf mindestens {minimumBytes} setzen; die Antwort wird nur an vollständigen Testkandidaten gekürzt.",
                FieldPath: "$.maxResponseBytes",
                RequestedBytes: budget,
                MinimumResponseBytes: minimumBytes));

    private static int NormalizeBudget(int requested) => requested <= 0 ? DefaultMaxResponseBytes : requested;

    private static string? ExtractNavigationText(string text, TestContextPayload payload)
    {
        var report = TestContextFormatter.FormatReport(payload);
        return text.StartsWith(report, StringComparison.Ordinal)
            ? text[report.Length..].Trim()
            : null;
    }

    private static TestFileCoverageResult ToCoverageResult(StaticTestCandidateFile file) => new(
        file.FilePath,
        file.TestClassName,
        file.Category,
        file.MatchReason,
        file.TestMethods,
        file.TotalClassTests,
        file.ProjectDirectory,
        ToEvidenceKind(file.EvidenceKind),
        file.Confidence,
        file.TotalTestCount,
        file.TestClassNames!);

    private static TestEvidenceKind ToEvidenceKind(string evidenceKind) => evidenceKind switch
    {
        "directInvocation" => TestEvidenceKind.DirectInvocation,
        "explicitMemberCoverage" => TestEvidenceKind.ExplicitMemberCoverage,
        "memberNameMatch" => TestEvidenceKind.MemberNameMatch,
        "directTypeUse" => TestEvidenceKind.DirectTypeUse,
        "explicitTypeCoverage" => TestEvidenceKind.ExplicitTypeCoverage,
        _ => TestEvidenceKind.TypeNamingConvention,
    };
}
