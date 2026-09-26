#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Configuration;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Handoffs;
using AiNetLinter.Mcp.Tools.Verify.DeadCode;
using AiNetLinter.Mcp.Tools.Verify.MagicValues;

namespace AiNetLinter.Mcp.Tools.Verify;

internal sealed record VerifyAdvisoryProjection(
    int TotalCount,
    IReadOnlyList<VerifyEvidenceEntry> Entries,
    string Completeness,
    VerifyDeadCodeSummary? DeadCode = null,
    DeadCodeScanResult? ScanSnapshot = null,
    int BudgetBytes = 8192)
{
    internal static readonly VerifyAdvisoryProjection Empty = new(0, [], "not_requested");
}

internal sealed record VerifyDeadCodeSummary(
    string Status,
    int? Candidates,
    string? Cause = null,
    DeadCodeScanCoverage? Coverage = null,
    string? ContinuationToken = null);

internal static class VerifyAdvisoryProjector
{
    private const string AdvisoryKind = "advisory_candidate";
    private const string AdvisorySeverity = "advisory";
    private const string MagicValueCategoryPrefix = "magic_value:";
    private const string MagicValueConfidence = "medium";
    private const int UnboundedCandidateLimit = int.MaxValue;

    internal static async Task<VerifyAdvisoryProjection> CollectAsync(
        Microsoft.CodeAnalysis.Solution solution,
        IReadOnlySet<string>? scopeFiles,
        CancellationToken cancellationToken,
        VerifyAdvisorySettings? settings = null)
    {
        var config = settings?.Config;
        var deadCode = await ScanDeadCodeAsync(solution, new(scopeFiles, config, settings?.Identity, settings?.SolutionBudget ?? scopeFiles is null), cancellationToken);
        var magicValues = await ScanMagicValuesAsync(solution, scopeFiles, cancellationToken);
        var deadSummary = BuildDeadCodeSummary(deadCode);
        var completeness = deadCode.Result is null ? "unavailable"
            : deadSummary.Status == "partial" || magicValues.Completeness != "complete" ? "partial"
            : "complete";
        var entries = Rank(magicValues.Entries);

        return new(
            magicValues.Entries.Count,
            entries,
            completeness,
            deadSummary, deadCode.Result, Math.Clamp(config?.DeadCode.MaxResponseBytes ?? 8192, 512, 8192));
    }

    private static async Task<DeadCodeScanAttempt> ScanDeadCodeAsync(
        Microsoft.CodeAnalysis.Solution solution,
        ScanSettings settings,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await DeadCodeAdvisoryScanner.ScanAsync(
                solution,
                new DeadCodeAdvisoryOptions(
                    Accessibility: DeadCodeAccessibilityFilter.All,
                    Confidence: DeadCodeConfidenceFilter.Both,
                    Kind: DeadCodeKindFilter.All,
                    IncludeTests: false,
                    Mode: DeadCodeMode.Members,
                    MaxResults: UnboundedCandidateLimit,
                    ScopeFiles: settings.ScopeFiles,
                    Config: settings.Config,
                    HandoffIdentity: settings.Identity,
                    SolutionBudget: settings.SolutionBudget,
                    RequestedScope: settings.SolutionBudget ? "solution" : "changes"),
                cancellationToken);
            return new(result, null);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return new(null, exception.GetType().Name);
        }
    }

    private static async Task<MagicValueScanAttempt> ScanMagicValuesAsync(
        Microsoft.CodeAnalysis.Solution solution,
        IReadOnlySet<string>? scopeFiles,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await MagicValueAdvisoryScanner.ScanAsync(new MagicValueAdvisoryScannerParameters(
                solution,
                null,
                null,
                null,
                MinOccurrences: 2,
                MaxResults: UnboundedCandidateLimit,
                IgnoreNumbers: null,
                IncludeTests: false,
                IncludeSuppressed: false,
                ChangedOnly: false,
                cancellationToken,
                ScopeFiles: scopeFiles));
            return result.IsMalfunction
                ? new([], "partial")
                : new(result.Payload!.MagicValues.Select(ToMagicValueEvidence).ToList(), "complete");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return new([], "partial");
        }
    }

    private static VerifyDeadCodeSummary BuildDeadCodeSummary(DeadCodeScanAttempt attempt)
    {
        if (attempt.Result is not { } result)
        {
            return new("unavailable", null, attempt.Cause ?? "scan_failed");
        }

        return new(
            result.Summary.Status == "partial" ? "partial" : "complete",
            result.Summary.TotalDead,
            Coverage: result.Summary.Coverage);
    }

    private static IReadOnlyList<VerifyEvidenceEntry> Rank(IEnumerable<VerifyEvidenceEntry> entries) =>
        entries
            .OrderBy(entry => entry.ReviewPriority)
            .ThenBy(entry => entry.RuleOrCategory, StringComparer.Ordinal)
            .ThenBy(entry => entry.SourcePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Line)
            .ThenBy(entry => entry.HandoffId, StringComparer.Ordinal)
            .Take(VerifyTool.EvidenceLimit)
            .ToList();

    private static VerifyEvidenceEntry ToMagicValueEvidence(MagicValueEntry entry) => new(
        AdvisoryKind,
        MagicValueCategoryPrefix + entry.Category,
        AdvisorySeverity,
        entry.FilePath,
        entry.Line,
        $"{entry.Occurrences} statische Literalfunde im Änderungskontext.",
        $"{entry.FilePath}:{entry.Line}",
        RequiresAgentJudgment: true,
        Confidence: MagicValueConfidence,
        EvidenceBoundary: entry.EvidenceBoundary,
        CounterIndicators: ["Fachliche Semantik", "Laufzeitkonfiguration"]);

    private sealed record ScanSettings(IReadOnlySet<string>? ScopeFiles, Config? Config, AnalysisSymbolIdentity? Identity, bool SolutionBudget);

    private sealed record DeadCodeScanAttempt(DeadCodeScanResult? Result, string? Cause);

    private sealed record MagicValueScanAttempt(IReadOnlyList<VerifyEvidenceEntry> Entries, string Completeness);
}

internal sealed record VerifyAdvisorySettings(Config? Config = null, AnalysisSymbolIdentity? Identity = null, bool? SolutionBudget = null);
