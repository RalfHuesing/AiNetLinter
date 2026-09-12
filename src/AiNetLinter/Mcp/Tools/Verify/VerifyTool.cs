#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Baseline;
using AiNetLinter.Core.Git;
using AiNetLinter.Mcp.Tools.DeadCode;
using AiNetLinter.Mcp.Tools.MagicValues;
using AiNetLinter.Models;
using AiNetLinter.Output;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Tools.Verify;

/// <summary>Der einzelne öffentliche Quality-Gate-Einstieg.</summary>
internal static class VerifyTool
{
    internal const int EvidenceLimit = 20;

    internal static async Task<CallToolResult> ExecuteAsync(
        McpCodeGraphServer server,
        VerifyScope scope,
        CancellationToken cancellationToken)
    {
        if (server.LoadState == ServerLoadState.Loading) return McpToolResults.Loading();
        var solution = server.GetCurrentSolution();
        if (solution is null) return VerifyResponseFormatter.Error(
            "ANALYSIS_FAILURE", "Die Source-Solution steht nicht für verify bereit.", "Erneut aufrufen, sobald der Server die Solution geladen hat.");

        var configSnapshot = server.GetConfigSnapshot();
        if (configSnapshot.Config is null) return VerifyResponseFormatter.Incomplete(
            scope, VerifyDecisionReason.NotConfigured, "Die Lint-Regelkonfiguration ist nicht verfügbar.");

        var projection = scope == VerifyScope.Solution
            ? VerifyScopeProjector.ForSolution(solution)
            : VerifyScopeProjector.ForChanges(solution);
        if (projection.IncompleteReason is not null) return VerifyResponseFormatter.Incomplete(
            scope, projection.IncompleteReason.Value, projection.Recovery);

        var scoreResult = await SafeguardScanner.ComputeScoreAsync(new SafeguardScannerParameters(
            solution,
            configSnapshot.Config,
            server.Console,
            projection.ScopeFilter,
            cancellationToken,
            VerifyGateSummary.RequiredScore,
            EvidenceLimit));
        if (scoreResult.IsMalfunction || scoreResult.Score is null) return VerifyResponseFormatter.Error(
            "ANALYSIS_FAILURE", "Der Verify-Gatekern konnte nicht vollständig bestimmt werden.", "Den identischen verify-Aufruf einmal erneut ausführen.");

        var advisory = scope == VerifyScope.Changes
            ? await VerifyAdvisoryProjector.CollectAsync(solution, projection.ScopeFilter, cancellationToken)
            : VerifyAdvisoryProjection.Empty;

        return VerifyResponseFormatter.Success(new VerifySuccessParameters(
            scope,
            projection.Scope,
            scoreResult.Score,
            projection.Exclusions,
            advisory));
    }
}

internal sealed record VerifyScopeResolution(
    VerifyScopeProjection Scope,
    string? ScopeFilter,
    VerifyDecisionReason? IncompleteReason,
    string? Recovery,
    IReadOnlyList<string> Exclusions);

internal sealed record VerifySuccessParameters(
    VerifyScope Requested,
    VerifyScopeProjection Scope,
    ScoreResult Score,
    IReadOnlyList<string> Exclusions,
    VerifyAdvisoryProjection Advisory);

internal static class VerifyScopeProjector
{
    internal static VerifyScopeResolution ForSolution(Microsoft.CodeAnalysis.Solution solution) =>
        new(
            new VerifyScopeProjection(VerifyScope.Solution, VerifyScope.Solution, ["solution"], []),
            null,
            null,
            null,
            []);

    internal static VerifyScopeResolution ForChanges(Microsoft.CodeAnalysis.Solution solution)
    {
        var root = Path.GetDirectoryName(solution.FilePath);
        if (string.IsNullOrWhiteSpace(root)) return Indeterminate("Der Änderungs-Scope konnte nicht bestimmt werden.");

        try
        {
            var diff = GitDiffParser.RunGitDiff(root, null);
            var untracked = GitDiffParser.RunGitUntrackedFiles(root);
            var changed = GitDiffParser.ParseGitDiffHunkRanges(diff ?? string.Empty).Keys
                .Concat(SplitPaths(untracked))
                .Select(path => Path.GetFullPath(Path.Combine(root, path)))
                .Where(File.Exists)
                .Where(path => solution.Projects
                    .SelectMany(project => project.Documents)
                    .Any(document => SourceFileCatalog.IsValidDocument(document, root)
                        && string.Equals(document.FilePath, path, StringComparison.OrdinalIgnoreCase)))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (changed.Count == 0) return new(
                new VerifyScopeProjection(VerifyScope.Changes, VerifyScope.Changes, [], []),
                null,
                VerifyDecisionReason.EmptyChangeContext,
                "scope: solution verwenden.",
                []);

            if (changed.Count != 1) return Indeterminate("Mehrere geänderte Source-Dateien benötigen die vollständige Populationsprojektion.");

            var relative = Path.GetRelativePath(root, changed[0]).Replace('\\', '/');
            return new(
                new VerifyScopeProjection(VerifyScope.Changes, VerifyScope.Changes, [relative], []),
                relative,
                null,
                null,
                []);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return Indeterminate("Der Git-Änderungs-Scope konnte nicht zuverlässig bestimmt werden.");
        }
    }

    private static VerifyScopeResolution Indeterminate(string recovery) =>
        new(
            new VerifyScopeProjection(VerifyScope.Changes, VerifyScope.Changes, [], []),
            null,
            VerifyDecisionReason.ChangeContextIndeterminate,
            recovery,
            []);

    private static IEnumerable<string> SplitPaths(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

internal static class VerifyResponseFormatter
{
    internal static CallToolResult Success(VerifySuccessParameters parameters)
    {
        var requested = parameters.Requested;
        var scope = parameters.Scope;
        var score = parameters.Score;
        var advisory = parameters.Advisory;
        var count = score.TotalViolationCount;
        var verdict = score.Score == VerifyGateSummary.RequiredScore && count == VerifyGateSummary.RequiredViolationCount
            ? VerifyVerdict.Pass
            : VerifyVerdict.Failed;
        var reason = count > 0 ? VerifyDecisionReason.ViolationsPresent : VerifyDecisionReason.ScoreBelowRequired;
        var gateEvidence = score.Violations.Select(ToEvidence).ToList();
        if (verdict == VerifyVerdict.Failed && gateEvidence.Count == 0) return Incomplete(
            requested, VerifyDecisionReason.GateEvidenceIncomplete, "Den identischen verify-Aufruf erneut ausführen.");
        var evidence = gateEvidence
            .Concat(advisory.Entries)
            .Take(VerifyTool.EvidenceLimit)
            .ToList();

        var response = new VerifyResponse(
            verdict,
            VerifyCompleteness.Complete,
            new VerifyGateSummary(score.Score, count, verdict == VerifyVerdict.Pass ? VerifyDecisionReason.RequirementsMet : reason),
            scope,
            count + advisory.TotalCount,
            evidence);
        return Text(Render(response, parameters.Exclusions, count, advisory));
    }

    internal static CallToolResult Incomplete(VerifyScope scope, VerifyDecisionReason reason, string? recovery) =>
        Text($"verdict: incomplete\nstatus:\n  operation: verify\n  completeness: incomplete\ngate:\n  score: null\n  requiredScore: 10.0\n  violationCount: null\n  requiredViolationCount: 0\n  decisionReason: {ToWire(reason)}\nevidence:\n  totalCount: 0\n  returnedCount: 0\n  entries: []\nscope:\n  requested: {ToWire(scope)}\n  effective: {ToWire(scope)}\n  populations: []\n  exclusions: []\nrecovery: {recovery ?? "scope: solution verwenden."}");

    internal static CallToolResult Error(string code, string message, string recovery, string? fieldPath = null) =>
        new()
        {
            IsError = true,
            Content = [new TextContentBlock { Text = $"verdict: error\nstatus: operation=error, completeness=not_applicable\ncode: {code}\nmessage: {message}{(fieldPath is null ? string.Empty : $"\nfieldPath: {fieldPath}")}\nrecovery: {recovery}" }],
        };

    private static CallToolResult Text(string text) => new()
    {
        Content = [new TextContentBlock { Text = text }],
    };

    private static VerifyEvidenceEntry ToEvidence(ViolationEntry violation) => new(
        "gate_violation",
        violation.RuleName,
        violation.Severity,
        violation.FilePath,
        violation.LineNumber,
        violation.Details,
        $"{violation.FilePath}:{violation.LineNumber}");

    private static string Render(
        VerifyResponse response,
        IReadOnlyList<string> exclusions,
        int gateTotalCount,
        VerifyAdvisoryProjection advisory)
    {
        var gate = response.Gate;
        var advisoryReturnedCount = response.Evidence.Count(entry => entry.Kind == "advisory_candidate");
        var lines = new List<string>
        {
            $"verdict: {ToWire(response.Verdict)}",
            "status:",
            "  operation: verify",
            $"  completeness: {ToWire(response.Completeness)}",
            "gate:",
            $"  score: {gate.Score!.Value.ToString("F1", CultureInfo.InvariantCulture)}",
            "  requiredScore: 10.0",
            $"  violationCount: {gate.ViolationCount}",
            "  requiredViolationCount: 0",
            $"  decisionReason: {ToWire(gate.DecisionReason)}",
            "evidence:",
            $"  totalCount: {response.EvidenceTotalCount}",
            $"  returnedCount: {response.Evidence.Count}",
            $"  gateTotalCount: {gateTotalCount}",
            $"  advisoryTotalCount: {advisory.TotalCount}",
            $"  advisoryReturnedCount: {advisoryReturnedCount}",
            $"  advisoryCompleteness: {advisory.Completeness}",
            "  entries:",
        };
        foreach (var entry in response.Evidence)
        {
            lines.Add($"  - kind: {entry.Kind}");
            lines.Add(entry.Kind == "advisory_candidate"
                ? $"    category: {entry.RuleOrCategory}"
                : $"    rule: {entry.RuleOrCategory}");
            lines.Add($"    severity: {entry.Severity}");
            lines.Add($"    source: {entry.SourcePath}:{entry.Line}");
            lines.Add($"    reason: {entry.Reason}");
            lines.Add($"    handoffId: {entry.HandoffId}");
            if (entry.Kind != "advisory_candidate") continue;
            lines.Add($"    requiresAgentJudgment: {entry.RequiresAgentJudgment.ToString().ToLowerInvariant()}");
            lines.Add($"    confidence: {entry.Confidence}");
            lines.Add($"    evidenceBoundary: {entry.EvidenceBoundary}");
            lines.Add($"    counterIndicators: [{string.Join(", ", entry.CounterIndicators ?? [])}]");
        }
        if (response.Evidence.Count == 0) lines.Add("  []");
        lines.Add("scope:");
        lines.Add($"  requested: {ToWire(response.Scope.Requested)}");
        lines.Add($"  effective: {ToWire(response.Scope.Effective)}");
        lines.Add($"  populations: [{string.Join(", ", response.Scope.Populations)}]");
        lines.Add($"  exclusions: [{string.Join(", ", exclusions)}]");
        return string.Join("\n", lines);
    }

    private static string ToWire(object value) => value.ToString()!.ToLowerInvariant();
}

internal sealed record VerifyAdvisoryProjection(
    int TotalCount,
    IReadOnlyList<VerifyEvidenceEntry> Entries,
    string Completeness)
{
    internal static readonly VerifyAdvisoryProjection Empty = new(0, [], "not_requested");
}

internal static class VerifyAdvisoryProjector
{
    private const string AdvisoryKind = "advisory_candidate";
    private const string AdvisorySeverity = "advisory";
    private const string DeadCodeCategory = "dead_code";
    private const string MagicValueCategoryPrefix = "magic_value:";
    private const string MagicValueConfidence = "medium";

    internal static async Task<VerifyAdvisoryProjection> CollectAsync(
        Microsoft.CodeAnalysis.Solution solution,
        string? scopeFilter,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(scopeFilter)) return VerifyAdvisoryProjection.Empty;

        try
        {
            var deadCode = await FindDeadCodeScanner.ScanAsync(
                solution,
                new FindDeadCodeArgs(ScopeFilter: scopeFilter, MaxResults: VerifyTool.EvidenceLimit),
                cancellationToken);
            var magicValues = await FindMagicValuesScanner.ScanAsync(new FindMagicValuesScannerParameters(
                solution,
                scopeFilter,
                null,
                null,
                MinOccurrences: 2,
                MaxResults: VerifyTool.EvidenceLimit,
                IgnoreNumbers: null,
                IncludeTests: false,
                IncludeSuppressed: false,
                ChangedOnly: false,
                cancellationToken));

            if (magicValues.IsMalfunction) return new(
                deadCode.Summary.TotalDead,
                Rank(deadCode.DeadSymbols.Select(ToDeadCodeEvidence)),
                "partial");

            var entries = deadCode.DeadSymbols
                .Select(ToDeadCodeEvidence)
                .Concat(magicValues.Payload!.MagicValues.Select(ToMagicValueEvidence));
            return new(
                deadCode.Summary.TotalDead + magicValues.Payload!.Summary.Total,
                Rank(entries),
                "complete");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return new(0, [], "unavailable");
        }
    }

    private static IReadOnlyList<VerifyEvidenceEntry> Rank(IEnumerable<VerifyEvidenceEntry> entries) =>
        entries
            .OrderBy(entry => AdvisoryRank(entry))
            .ThenBy(entry => entry.RuleOrCategory, StringComparer.Ordinal)
            .ThenBy(entry => entry.SourcePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Line)
            .Take(VerifyTool.EvidenceLimit)
            .ToList();

    private static int AdvisoryRank(VerifyEvidenceEntry entry) => entry.Confidence switch
    {
        "high" => 0,
        "medium" => 1,
        _ => 2,
    };

    private static VerifyEvidenceEntry ToDeadCodeEvidence(DeadCodeEntry entry) => new(
        AdvisoryKind,
        DeadCodeCategory,
        AdvisorySeverity,
        entry.File,
        entry.Line,
        entry.Reason,
        $"{entry.File}:{entry.Line}",
        RequiresAgentJudgment: true,
        Confidence: entry.Confidence,
        EvidenceBoundary: entry.EvidenceBoundary,
        CounterIndicators: entry.Countercheck ?? ["Reflection", "DI", "Generatoren"]);

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
}
