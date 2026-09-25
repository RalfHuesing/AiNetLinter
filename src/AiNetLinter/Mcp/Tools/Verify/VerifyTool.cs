#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Baseline;
using AiNetLinter.Core.Git;
using AiNetLinter.Configuration;
using AiNetLinter.Mcp.Tools.Verify.DeadCode;
using AiNetLinter.Mcp.Tools.Verify.MagicValues;
using AiNetLinter.Mcp.Handoffs;
using AiNetLinter.Mcp;
using AiNetLinter.Models;
using AiNetLinter.Output;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Tools.Verify;

/// <summary>Der einzelne öffentliche Quality-Gate-Einstieg.</summary>
internal static class VerifyTool
{
    internal const int EvidenceLimit = 20;
    internal const int ResponseBudgetBytes = 4 * 1024;

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
        if (configSnapshot.Config is not Config config) return VerifyResponseFormatter.Error(
            "ANALYSIS_FAILURE", "Die Dead-Code-Konfiguration konnte nicht gelesen werden.", "Konfiguration neu laden und den identischen verify-Aufruf wiederholen.");

        var projection = scope == VerifyScope.Solution
            ? VerifyScopeProjector.ForSolution(solution)
            : VerifyScopeProjector.ForChanges(solution);
        if (projection.IncompleteReason is not null) return VerifyResponseFormatter.Incomplete(
            scope, projection.IncompleteReason.Value, projection.Recovery);

        var apiSurfaceIssues = DeadCodeAdvisoryScanner.ValidateApiSurface(
            solution,
            projection.ScopeFiles,
            config);
        if (apiSurfaceIssues.Count > 0) return VerifyResponseFormatter.ApiSurfaceNotConfigured(apiSurfaceIssues);

        var scoreResult = await VerifyGateScanner.ComputeScoreAsync(new VerifyGateScannerParameters(
            solution,
            config,
            server.Console,
            projection.ScopeFilter,
            cancellationToken,
            VerifyGateSummary.RequiredScore,
            EvidenceLimit,
            projection.ScopeFiles));
        if (scoreResult.IsMalfunction || scoreResult.Score is null) return VerifyResponseFormatter.Error(
            "ANALYSIS_FAILURE", "Der Verify-Gatekern konnte nicht vollständig bestimmt werden.", "Den identischen verify-Aufruf einmal erneut ausführen.");

        var advisory = await VerifyAdvisoryProjector.CollectAsync(
            solution,
            scope == VerifyScope.Changes ? projection.ScopeFiles : null,
            cancellationToken,
            config,
            server.HandoffSymbolIdentity);

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
    IReadOnlySet<string>? ScopeFiles,
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
            null,
            []);

    internal static VerifyScopeResolution ForChanges(Microsoft.CodeAnalysis.Solution solution)
    {
        var root = Path.GetDirectoryName(solution.FilePath);
        if (string.IsNullOrWhiteSpace(root)) return Indeterminate("Der Änderungs-Scope konnte nicht bestimmt werden.");

        try
        {
            var (diffExitCode, diff, _) = GitDiffParser.RunGitProcess(root, "diff --name-only HEAD");
            var (untrackedExitCode, untracked, _) = GitDiffParser.RunGitProcess(root, "ls-files --others --exclude-standard");
            if (diffExitCode != 0 || untrackedExitCode != 0) return Indeterminate("Der Git-Änderungs-Scope konnte nicht zuverlässig bestimmt werden.");

            var changedPaths = SplitPaths(diff)
                .Concat(SplitPaths(untracked))
                .Select(path => path.Replace('/', Path.DirectorySeparatorChar))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (HasConservativeScopeExpansion(changedPaths, solution, root)) return FullSolution();

            var sourceDocuments = solution.Projects
                .SelectMany(project => project.Documents)
                .Where(document => SourceFileCatalog.IsValidDocument(document, root) && document.FilePath is not null)
                .Select(document => Path.GetFullPath(document.FilePath!))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var changedSourcePaths = changedPaths
                .Where(path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                .Select(path => Path.GetFullPath(Path.Combine(root, path)))
                .Where(path => !SourceFileCatalog.IsGeneratedPath(path))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (changedSourcePaths.Any(path => !sourceDocuments.Contains(path))) return FullSolution();

            var changed = changedSourcePaths;
            if (changed.Count == 0) return new(
                new VerifyScopeProjection(VerifyScope.Changes, VerifyScope.Changes, [], []),
                null,
                null,
                VerifyDecisionReason.EmptyChangeContext,
                "scope: solution verwenden.",
                []);

            var relative = changed.Select(path => Path.GetRelativePath(root, path).Replace('\\', '/')).ToList();
            return new(
                new VerifyScopeProjection(VerifyScope.Changes, VerifyScope.Changes, relative, []),
                null,
                changed.ToHashSet(StringComparer.OrdinalIgnoreCase),
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
            null,
            VerifyDecisionReason.ChangeContextIndeterminate,
            recovery,
            []);

    private static VerifyScopeResolution FullSolution() =>
        new(
            new VerifyScopeProjection(VerifyScope.Changes, VerifyScope.Solution, ["solution"], []),
            null,
            null,
            null,
            null,
            []);

    private static IEnumerable<string> SplitPaths(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static bool HasConservativeScopeExpansion(
        IReadOnlyList<string> changedPaths,
        Microsoft.CodeAnalysis.Solution solution,
        string solutionRoot)
    {
        var structuralPaths = solution.Projects
            .Select(project => project.FilePath)
            .Append(solution.FilePath)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => Path.GetRelativePath(solutionRoot, path!).Replace('\\', '/'))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return changedPaths.Any(path =>
            structuralPaths.Contains(path.Replace('\\', '/'))
            || string.Equals(Path.GetFileName(path), "ainetlinter-rules.json", StringComparison.OrdinalIgnoreCase));
    }
}

internal static partial class VerifyResponseFormatter
{
    internal static CallToolResult Success(VerifySuccessParameters parameters)
    {
        var prepared = PrepareSuccessResponse(parameters);
        if (prepared.RequiresIncomplete) return Incomplete(
            parameters.Requested, VerifyDecisionReason.GateEvidenceIncomplete, "Den identischen verify-Aufruf erneut ausführen.");

        var unbounded = RenderWithinBudget(
            prepared.Response,
            parameters.Exclusions,
            prepared.GateEvidenceCount,
            parameters.Advisory,
            TruncationReason(prepared.SourceTruncated, false));
        if (unbounded.Text is not null && !unbounded.ScopeProjected) return Text(unbounded.Text);

        return ProjectWithinBudget(parameters, prepared);
    }

    internal static CallToolResult Incomplete(VerifyScope scope, VerifyDecisionReason reason, string? recovery) =>
        Text($"verdict: incomplete\ncompleteness: incomplete\nisGateResult: false\nreason: {ToWire(reason)}\nscope: {ToWire(scope)}\nrecovery: {recovery ?? "scope: solution verwenden."}");

    internal static CallToolResult Error(string code, string message, string recovery, string? fieldPath = null) =>
        Text(
            $"verdict: error\ncode: {code}\nmessage: {message}{(fieldPath is null ? string.Empty : $"\nfield: {fieldPath}")}\nrecovery: {recovery}\nStatus: operation=error, completeness=not_applicable, analysisQuality=not_applicable; Aktion: {recovery}",
            isError: true);

    private static CallToolResult Text(string text, bool isError = false)
    {
        var result = new CallToolResult
        {
            Content = [new TextContentBlock { Text = BoundContent(text, isError) }],
        };
        if (isError) result.IsError = true;
        return result;
    }

    private static string BoundContent(string text, bool isError)
    {
        if (Encoding.UTF8.GetByteCount(text) <= VerifyTool.ResponseBudgetBytes) return text;
        return isError
            ? "verdict: error\ncode: RESPONSE_BUDGET_EXCEEDED\nrecovery: Scope präzisieren und erneut ausführen.\nStatus: operation=error, completeness=not_applicable, analysisQuality=not_applicable; Aktion: Scope präzisieren und erneut ausführen."
            : "verdict: incomplete\ncompleteness: incomplete\nisGateResult: false\nreason: gateevidenceincomplete\nscope: changes\nrecovery: Die Verify-Antwort überschreitet das feste Antwortbudget; Scope präzisieren und erneut ausführen.";
    }

    private static VerifyEvidenceEntry ToEvidence(ViolationEntry violation) => new(
        "gate_violation",
        violation.RuleName,
        violation.Severity,
        violation.FilePath,
        violation.LineNumber,
        violation.Details,
        $"{violation.FilePath}:{violation.LineNumber}");

    private static int GateRank(string severity) => severity switch
    {
        "error" => 0,
        "warning" => 1,
        _ => 2,
    };

    private static string TruncationReason(bool sourceTruncated, bool budgetTruncated) =>
        sourceTruncated
            ? budgetTruncated ? "evidence_limit, response_budget" : "evidence_limit"
            : budgetTruncated ? "response_budget" : "none";

    private static VerifyRenderAttempt RenderWithinBudget(
        VerifyResponse response,
        IReadOnlyList<string> exclusions,
        int gateTotalCount,
        VerifyAdvisoryProjection advisory,
        string truncationReason)
    {
        var text = Render(response, exclusions, gateTotalCount, advisory, truncationReason);
        if (Encoding.UTF8.GetByteCount(text) <= VerifyTool.ResponseBudgetBytes) return new(text, ScopeProjected: false);

        var compactScope = response.Scope with
        {
            Populations = [response.Scope.Effective == VerifyScope.Solution
                ? "solution"
                : $"changed_source_files:{response.Scope.Populations.Count}"],
        };
        text = Render(response with { Scope = compactScope }, exclusions, gateTotalCount, advisory, truncationReason);
        return Encoding.UTF8.GetByteCount(text) <= VerifyTool.ResponseBudgetBytes
            ? new(text, ScopeProjected: true)
            : new(null, ScopeProjected: true);
    }

    private static string Render(
        VerifyResponse response,
        IReadOnlyList<string> exclusions,
        int gateTotalCount,
        VerifyAdvisoryProjection advisory,
        string truncationReason)
    {
        var lines = CreateSummary(response, exclusions);
        AppendEvidence(lines, response, gateTotalCount, advisory, truncationReason);
        return string.Join("\n", lines);
    }

    private static List<string> CreateSummary(VerifyResponse response, IReadOnlyList<string> exclusions)
    {
        var gate = response.Gate;
        var scope = ToWire(response.Scope.Requested);
        if (response.Scope.Requested != response.Scope.Effective)
        {
            scope += $" -> {ToWire(response.Scope.Effective)}";
        }

        if (exclusions.Count > 0) scope += $"; exclusions=[{string.Join(", ", exclusions)}]";
        return
        [
            $"verdict: {ToWire(response.Verdict)}",
            $"completeness: {ToWire(response.Completeness)}",
            $"score: {gate.Score!.Value.ToString("F1", CultureInfo.InvariantCulture)}",
            $"violationCount: {gate.ViolationCount}",
            $"scope: {scope}",
        ];
    }

    private static void AppendEvidence(
        List<string> lines,
        VerifyResponse response,
        int gateTotalCount,
        VerifyAdvisoryProjection advisory,
        string truncationReason)
    {
        if (response.Evidence.Count > 0 || truncationReason != "none")
        {
            lines.Add($"evidence: returned={response.Evidence.Count}/{response.EvidenceTotalCount}; truncation={truncationReason}");
        }

        AppendFindings(lines, response.Evidence, gateTotalCount);
        AppendAdvisories(lines, response.Evidence, advisory);
    }

    private static void AppendFindings(
        List<string> lines,
        IReadOnlyList<VerifyEvidenceEntry> evidence,
        int gateTotalCount)
    {
        var findings = evidence.Where(entry => entry.Kind == "gate_violation").ToList();
        if (findings.Count == 0) return;

        lines.Add($"findings: count={findings.Count}/{gateTotalCount}");
        foreach (var entry in findings)
        {
            lines.Add($"- rule={entry.RuleOrCategory}; severity={entry.Severity}; ref={entry.HandoffId}; reason={entry.Reason}");
        }
    }

    private static void AppendAdvisories(
        List<string> lines,
        IReadOnlyList<VerifyEvidenceEntry> evidence,
        VerifyAdvisoryProjection advisory)
    {
        AppendDeadCodeSummary(lines, evidence, advisory.DeadCode);
        var advisories = evidence.Where(entry => entry.Kind == "advisory_candidate").ToList();
        if (advisories.Count == 0)
        {
            if (advisory.Completeness != "not_requested") lines.Add($"advisories: {advisory.Completeness}");
            return;
        }

        lines.Add($"advisories: count={advisories.Count}; completeness={advisory.Completeness}; review_required; static_evidence");
        foreach (var entry in advisories)
        {
            if (entry.RuleOrCategory == "dead_code")
            {
                var razorStatus = entry.Reason.Contains("Razor-Referenzen nicht entscheidbar", StringComparison.Ordinal)
                    ? " razorEvidence=unavailable;"
                    : string.Empty;
                lines.Add($"- category=dead_code; symbolIdentifier={entry.HandoffId}; ref={entry.SourcePath}:{entry.Line}; usage={entry.Usage}; confidence={entry.Confidence}; reason=no_production_static_reference;{(entry.TestReferences is > 0 ? $" testReferences={entry.TestReferences};" : string.Empty)}{razorStatus} countercheck=reflection,DI,generators,dynamic,markup/config,external_consumers");
            }
            else
            {
                lines.Add($"- category={entry.RuleOrCategory}; ref={entry.HandoffId}; confidence={entry.Confidence}; reason={entry.Reason}");
            }
        }
    }

    private static void AppendDeadCodeSummary(
        List<string> lines,
        IReadOnlyList<VerifyEvidenceEntry> evidence,
        VerifyDeadCodeSummary? summary)
    {
        if (summary is null) return;
        var shown = evidence.Count(entry => entry.RuleOrCategory == "dead_code");
        int? truncatedBy = summary.Candidates is int candidates ? Math.Max(0, candidates - shown) : null;
        lines.Add($"deadCode: status={summary.Status}; candidates={ToCount(summary.Candidates)}; testOnly={ToCount(summary.TestOnly)}; unreferenced={ToCount(summary.Unreferenced)}; apiProtected={ToCount(summary.ApiProtected)}; undecidable={ToCount(summary.Undecidable)}; shown={shown}; truncatedBy={ToCount(truncatedBy)}; next={(summary.Candidates is > 0 ? "review_now" : "none")}");
        if (summary.Candidates is > 0) lines.Add("deadCodeHint: Statischer Kandidat; Fehlalarm möglich. Vor Entfernen gegenprüfen.");
        if (truncatedBy is > 0) lines.Add("deadCodeAdvisoryHint: get_verify_advisories(category=dead_code)");
        if (summary.Cause is not null) lines.Add($"deadCodeCause: {summary.Cause}");
    }

    private static string ToCount(int? count) => count?.ToString(CultureInfo.InvariantCulture) ?? "unknown";

    private static string ToWire(object value) => value.ToString()!.ToLowerInvariant();
}

internal sealed record VerifyRenderAttempt(string? Text, bool ScopeProjected);
