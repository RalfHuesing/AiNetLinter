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
using AiNetLinter.Mcp.Tools.Safeguard;
using AiNetLinter.Models;
using AiNetLinter.Output;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Tools.Verify;

/// <summary>Der einzelne öffentliche Quality-Gate-Einstieg.</summary>
internal static class VerifyTool
{
    private const int EvidenceLimit = 20;

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

        return VerifyResponseFormatter.Success(
            scope,
            projection.Scope,
            scoreResult.Score,
            projection.Exclusions);
    }
}

internal sealed record VerifyScopeResolution(
    VerifyScopeProjection Scope,
    string? ScopeFilter,
    VerifyDecisionReason? IncompleteReason,
    string? Recovery,
    IReadOnlyList<string> Exclusions);

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

            var relative = Path.GetRelativePath(root, changed[0]);
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
    internal static CallToolResult Success(
        VerifyScope requested,
        VerifyScopeProjection scope,
        ScoreResult score,
        IReadOnlyList<string> exclusions)
    {
        var count = score.TotalViolationCount;
        var verdict = score.Score == VerifyGateSummary.RequiredScore && count == VerifyGateSummary.RequiredViolationCount
            ? VerifyVerdict.Pass
            : VerifyVerdict.Failed;
        var reason = count > 0 ? VerifyDecisionReason.ViolationsPresent : VerifyDecisionReason.ScoreBelowRequired;
        var evidence = score.Violations.Select(ToEvidence).ToList();
        if (verdict == VerifyVerdict.Failed && evidence.Count == 0) return Incomplete(
            requested, VerifyDecisionReason.GateEvidenceIncomplete, "Den identischen verify-Aufruf erneut ausführen.");

        var response = new VerifyResponse(
            verdict,
            VerifyCompleteness.Complete,
            new VerifyGateSummary(score.Score, count, verdict == VerifyVerdict.Pass ? VerifyDecisionReason.RequirementsMet : reason),
            scope,
            count,
            evidence);
        return Text(Render(response, exclusions));
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

    private static string Render(VerifyResponse response, IReadOnlyList<string> exclusions)
    {
        var gate = response.Gate;
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
            "  entries:",
        };
        foreach (var entry in response.Evidence)
        {
            lines.Add($"  - kind: {entry.Kind}");
            lines.Add($"    rule: {entry.RuleOrCategory}");
            lines.Add($"    severity: {entry.Severity}");
            lines.Add($"    source: {entry.SourcePath}:{entry.Line}");
            lines.Add($"    reason: {entry.Reason}");
            lines.Add($"    handoffId: {entry.HandoffId}");
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
