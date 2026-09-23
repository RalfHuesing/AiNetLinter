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
    VerifyDeadCodeSummary? DeadCode = null)
{
    internal static readonly VerifyAdvisoryProjection Empty = new(0, [], "not_requested");
}

internal sealed record VerifyDeadCodeSummary(
    string Status,
    int? Candidates,
    int? TestOnly,
    int? Unreferenced,
    int? ApiProtected,
    int? Undecidable,
    string? Cause = null);

internal static class VerifyAdvisoryProjector
{
    private const string AdvisoryKind = "advisory_candidate";
    private const string AdvisorySeverity = "advisory";
    private const string DeadCodeCategory = "dead_code";
    private const string MagicValueCategoryPrefix = "magic_value:";
    private const string MagicValueConfidence = "medium";
    private const int UnboundedCandidateLimit = int.MaxValue;

    internal static async Task<VerifyAdvisoryProjection> CollectAsync(
        Microsoft.CodeAnalysis.Solution solution,
        IReadOnlySet<string>? scopeFiles,
        CancellationToken cancellationToken,
        Config? config = null,
        AnalysisSymbolIdentity? handoffIdentity = null)
    {
        var deadCode = await ScanDeadCodeAsync(solution, scopeFiles, cancellationToken, config, handoffIdentity);
        var magicValues = await ScanMagicValuesAsync(solution, scopeFiles, cancellationToken);
        var deadEntries = deadCode.Result?.DeadSymbols.Select(ToDeadCodeEvidence).ToList() ?? [];
        var deadSummary = BuildDeadCodeSummary(deadCode);
        var completeness = deadCode.Result is null ? "unavailable"
            : deadSummary.Status == "partial" || magicValues.Completeness != "complete" ? "partial"
            : "complete";
        var entries = Rank(deadEntries.Concat(magicValues.Entries));

        return new(
            (deadCode.Result?.Summary.TotalDead ?? 0) + magicValues.Entries.Count,
            entries,
            completeness,
            deadSummary);
    }

    private static async Task<DeadCodeScanAttempt> ScanDeadCodeAsync(
        Microsoft.CodeAnalysis.Solution solution,
        IReadOnlySet<string>? scopeFiles,
        CancellationToken cancellationToken,
        Config? config,
        AnalysisSymbolIdentity? handoffIdentity)
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
                    ScopeFiles: scopeFiles,
                    Config: config,
                    HandoffIdentity: handoffIdentity),
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
            return new("unavailable", null, null, null, null, null, attempt.Cause ?? "scan_failed");
        }

        return new(
            result.Summary.Undecidable > 0 ? "partial" : "complete",
            result.Summary.TotalDead,
            result.DeadSymbols.Count(entry => entry.Usage == "test_only"),
            result.DeadSymbols.Count(entry => entry.Usage == "unreferenced"),
            result.Summary.ApiProtected,
            result.Summary.Undecidable);
    }

    private static IReadOnlyList<VerifyEvidenceEntry> Rank(IEnumerable<VerifyEvidenceEntry> entries) =>
        entries
            .OrderBy(AdvisoryRank)
            .ThenBy(entry => entry.RuleOrCategory, StringComparer.Ordinal)
            .ThenBy(entry => entry.SourcePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Line)
            .ThenBy(entry => entry.HandoffId, StringComparer.Ordinal)
            .Take(VerifyTool.EvidenceLimit)
            .ToList();

    private static int AdvisoryRank(VerifyEvidenceEntry entry) => entry.RuleOrCategory == DeadCodeCategory
        ? entry.Confidence == "high" ? 0 : 1
        : 2;

    private static VerifyEvidenceEntry ToDeadCodeEvidence(DeadCodeEntry entry) => new(
        AdvisoryKind,
        DeadCodeCategory,
        AdvisorySeverity,
        entry.File,
        entry.Line,
        entry.Reason,
        entry.InternalSymbolIdentifier is null
            ? string.Empty
            : HandoffHandleRegistry.Default.GetOpaqueHandleForOutputOrThrow(entry.InternalSymbolIdentifier),
        RequiresAgentJudgment: true,
        Confidence: entry.Confidence,
        EvidenceBoundary: entry.EvidenceBoundary,
        CounterIndicators: entry.Countercheck,
        Usage: entry.Usage,
        TestReferences: entry.TestReferences);

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

    private sealed record DeadCodeScanAttempt(DeadCodeScanResult? Result, string? Cause);

    private sealed record MagicValueScanAttempt(IReadOnlyList<VerifyEvidenceEntry> Entries, string Completeness);
}
