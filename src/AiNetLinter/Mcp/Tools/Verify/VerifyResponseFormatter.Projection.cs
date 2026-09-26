#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using ModelContextProtocol.Protocol;
using AiNetLinter.Mcp.Tools.Verify.DeadCode;

namespace AiNetLinter.Mcp.Tools.Verify;

internal static partial class VerifyResponseFormatter
{
    internal static CallToolResult ApiSurfaceNotConfigured(IReadOnlyList<DeadCodeApiSurfaceIssue> issues)
    {
        var lines = new List<string>
        {
            "Für jedes produktive Kandidatenprojekt muss die externe API-Oberfläche ausdrücklich eingeordnet werden.",
            $"projects: [{string.Join(", ", issues.Select(issue => issue.ProjectName))}]",
            "config: ainetlinter-rules.json; gültige Werte: closed_solution, external_library",
            "Wenn die Einordnung nicht aus Projektwissen eindeutig feststeht, beim Nutzer erfragen.",
            "Beispiele:",
            "\"ProjectOverrides\": {",
            "  \"PublicSdk\": { \"DeadCode\": { \"ApiSurface\": \"external_library\" } },",
            "  \"Application\": { \"DeadCode\": { \"ApiSurface\": \"closed_solution\" } }",
            "}",
        };

        foreach (var issue in issues.Where(issue => issue.Value is not null))
        {
            lines.Insert(2, $"invalid: {issue.FieldPath}={issue.Value}");
        }

        return Error(
            "DEAD_CODE_API_SURFACE_NOT_CONFIGURED",
            string.Join("\n", lines),
            "DeadCode.DefaultApiSurface setzen oder für jedes aufgeführte Projekt ProjectOverrides.<Muster>.DeadCode.ApiSurface konfigurieren.");
    }

    private static VerifySuccessPreparation PrepareSuccessResponse(VerifySuccessParameters parameters)
    {
        var score = parameters.Score;
        var gateEvidence = score.Violations
            .Select(ToEvidence)
            .OrderBy(entry => GateRank(entry.Severity))
            .ThenBy(entry => entry.SourcePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Line)
            .ThenBy(entry => entry.RuleOrCategory, StringComparer.Ordinal)
            .ToList();
        var verdict = score.Score == VerifyGateSummary.RequiredScore
            && score.TotalViolationCount == VerifyGateSummary.RequiredViolationCount
            ? VerifyVerdict.Pass
            : VerifyVerdict.Failed;
        var otherAdvisories = parameters.Advisory.Entries
            .Where(entry => entry.Kind != "advisory_candidate" || entry.RuleOrCategory != "dead_code")
            .ToList();
        var candidates = gateEvidence.Concat(otherAdvisories).ToList();
        var sourceTruncated = gateEvidence.Count < score.TotalViolationCount
            || otherAdvisories.Count < parameters.Advisory.TotalCount
            || candidates.Count > VerifyTool.EvidenceLimit;
        var response = new VerifyResponse(
            verdict,
            VerifyCompleteness.Complete,
            new VerifyGateSummary(
                score.Score,
                score.TotalViolationCount,
                GetDecisionReason(verdict, score.TotalViolationCount)),
            parameters.Scope,
            score.TotalViolationCount + parameters.Advisory.TotalCount,
            candidates.Take(VerifyTool.EvidenceLimit).ToList());

        return new VerifySuccessPreparation(
            response,
            gateEvidence.Count,
            sourceTruncated,
            verdict == VerifyVerdict.Failed && gateEvidence.Count == 0);
    }

    private static VerifyDecisionReason GetDecisionReason(VerifyVerdict verdict, int violationCount)
    {
        if (verdict == VerifyVerdict.Pass) return VerifyDecisionReason.RequirementsMet;
        return violationCount > 0 ? VerifyDecisionReason.ViolationsPresent : VerifyDecisionReason.ScoreBelowRequired;
    }

    private static CallToolResult ProjectWithinBudget(VerifySuccessParameters parameters, VerifySuccessPreparation prepared)
    {
        var projected = new List<VerifyEvidenceEntry>();
        var truncationReason = TruncationReason(prepared.SourceTruncated, true);
        foreach (var candidate in prepared.Response.Evidence)
        {
            var candidateResponse = prepared.Response with { Evidence = projected.Append(candidate).ToList() };
            if (RenderWithinBudget(
                    candidateResponse,
                    parameters.Exclusions,
                    prepared.GateEvidenceCount,
                    parameters.Advisory,
                    truncationReason).Text is null) break;
            projected.Add(candidate);
        }

        if (prepared.Response.Verdict == VerifyVerdict.Failed && projected.All(entry => entry.Kind != "gate_violation"))
        {
            return Incomplete(
                parameters.Requested,
                VerifyDecisionReason.GateEvidenceIncomplete,
                "Die vollständige Gate-Evidenz überschreitet das feste Antwortbudget; Scope präzisieren und erneut ausführen.");
        }

        var budgeted = RenderWithinBudget(
            prepared.Response with { Evidence = projected },
            parameters.Exclusions,
            prepared.GateEvidenceCount,
            parameters.Advisory,
            truncationReason);
        return budgeted.Text is not null
            ? Text(budgeted.Text)
            : Incomplete(
                parameters.Requested,
                VerifyDecisionReason.GateEvidenceIncomplete,
                "Die Verify-Antwort überschreitet das feste Antwortbudget; Scope präzisieren und erneut ausführen.");
    }
}

internal sealed record VerifySuccessPreparation(
    VerifyResponse Response,
    int GateEvidenceCount,
    bool SourceTruncated,
    bool RequiresIncomplete);
