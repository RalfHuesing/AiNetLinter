#nullable enable

using System;
using System.Collections.Generic;
using System.IO;

namespace AiNetLinter.Mcp.Tools.Verify;

/// <summary>
/// Stabiler Eingabevertrag des einzelnen Quality-Gate-Tools.
/// </summary>
internal sealed record VerifyToolArguments(string? TargetPath, string? Scope = null);

/// <summary>
/// Die beiden bewusst kleinen Prüfungsradien des Verify-Vertrags.
/// </summary>
internal enum VerifyScope
{
    Changes,
    Solution,
}

/// <summary>
/// Das ausschließliche Gate-Urteil; nur <see cref="Pass"/> gibt eine Freigabe.
/// </summary>
internal enum VerifyVerdict
{
    Pass,
    Failed,
    Incomplete,
    Error,
}

/// <summary>
/// Aussagegrenze des Gatekerns, unabhängig von einer begrenzten Advisory-Projektion.
/// </summary>
internal enum VerifyCompleteness
{
    Complete,
    Incomplete,
    NotApplicable,
}

/// <summary>
/// Maschinenlesbare Begründung eines Verdicts ohne zweite Statushierarchie.
/// </summary>
internal enum VerifyDecisionReason
{
    RequirementsMet,
    ScoreBelowRequired,
    ViolationsPresent,
    EmptyChangeContext,
    ChangeContextIndeterminate,
    GateEvidenceIncomplete,
    NotConfigured,
    InvalidArgument,
    AssemblyTargetUnsupported,
    AnalysisFailure,
}

/// <summary>
/// Ergebnis der serverseitigen Git-Working-Tree-Bestimmung für <see cref="VerifyScope.Changes"/>.
/// </summary>
internal enum VerifyWorkingTreeResolution
{
    Determined,
    Empty,
    Indeterminate,
}

/// <summary>
/// Vollständige aktuelle Source-Dateien aus gestagtem, ungestagtem und neuem Arbeitsstand.
/// </summary>
internal sealed record VerifyWorkingTreeChangeSet(
    VerifyWorkingTreeResolution Resolution,
    IReadOnlyList<string> StagedSourceFiles,
    IReadOnlyList<string> UnstagedSourceFiles,
    IReadOnlyList<string> NewSourceFiles,
    IReadOnlyList<string> ExcludedPaths);

/// <summary>
/// Effektive Gatepopulation und transparent dokumentierte Ausschlüsse.
/// </summary>
internal sealed record VerifyScopeProjection(
    VerifyScope Requested,
    VerifyScope Effective,
    IReadOnlyList<string> Populations,
    IReadOnlyList<string> Exclusions);

/// <summary>
/// Feste, nicht vom Aufrufer konfigurierbare Gatebedingungen und ihre Messwerte.
/// </summary>
internal sealed record VerifyGateSummary(
    double? Score,
    int? ViolationCount,
    VerifyDecisionReason DecisionReason)
{
    internal const double RequiredScore = 10.0;
    internal const int RequiredViolationCount = 0;
}

/// <summary>
/// Eine vollständige, direkt bearbeitbare Evidenzeinheit mit kanonischem Handoff.
/// </summary>
internal sealed record VerifyEvidenceEntry(
    string Kind,
    string RuleOrCategory,
    string Severity,
    string SourcePath,
    int Line,
    string Reason,
    string HandoffId,
    bool RequiresAgentJudgment = false,
    string? Confidence = null,
    string? EvidenceBoundary = null,
    IReadOnlyList<string>? CounterIndicators = null);

/// <summary>
/// Gemeinsames internes Antwortmodell für die einzige Content-Projektion von verify.
/// </summary>
internal sealed record VerifyResponse(
    VerifyVerdict Verdict,
    VerifyCompleteness Completeness,
    VerifyGateSummary Gate,
    VerifyScopeProjection Scope,
    int EvidenceTotalCount,
    IReadOnlyList<VerifyEvidenceEntry> Evidence);

internal sealed record VerifyTargetValidationError(
    string Code,
    string Message,
    string Recovery);

internal static class VerifyContract
{
    internal const string ToolName = "verify";
    internal const string DefaultScopeValue = "changes";
    internal const string SolutionScopeValue = "solution";

    internal static bool TryParseScope(string? value, out VerifyScope scope)
    {
        if (string.IsNullOrWhiteSpace(value) || string.Equals(value, DefaultScopeValue, StringComparison.Ordinal))
        {
            scope = VerifyScope.Changes;
            return true;
        }

        if (string.Equals(value, SolutionScopeValue, StringComparison.Ordinal))
        {
            scope = VerifyScope.Solution;
            return true;
        }

        scope = default;
        return false;
    }

    internal static bool IsSourceSolutionTarget(string? targetPath) =>
        targetPath is not null
        && (targetPath.EndsWith(".sln", StringComparison.OrdinalIgnoreCase)
            || targetPath.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase));

    internal static bool TryValidateSourceSolutionTarget(
        string? targetPath,
        out VerifyTargetValidationError? error)
    {
        if (string.IsNullOrWhiteSpace(targetPath))
        {
            error = new(
                "INVALID_ARGUMENT",
                "targetPath ist erforderlich.",
                "targetPath als absoluten Pfad einer vorhandenen .sln- oder .slnx-Datei angeben.");
            return false;
        }

        if (!Path.IsPathFullyQualified(targetPath))
        {
            error = new(
                "INVALID_ARGUMENT",
                "targetPath muss ein absoluter Pfad sein.",
                "targetPath als absoluten Pfad einer vorhandenen .sln- oder .slnx-Datei angeben.");
            return false;
        }

        var extension = Path.GetExtension(targetPath);
        if (extension is ".dll" or ".exe")
        {
            error = new(
                "ASSEMBLY_TARGET_UNSUPPORTED",
                "verify akzeptiert ausschließlich .sln- oder .slnx-Source-Ziele.",
                "targetPath auf eine .sln oder .slnx setzen.");
            return false;
        }

        if (!IsSourceSolutionTarget(targetPath))
        {
            error = new(
                "INVALID_ARGUMENT",
                "targetPath muss auf eine .sln- oder .slnx-Source-Datei zeigen.",
                "targetPath als absoluten Pfad einer vorhandenen .sln- oder .slnx-Datei angeben.");
            return false;
        }

        try
        {
            if (File.Exists(Path.GetFullPath(targetPath)))
            {
                error = null;
                return true;
            }
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or NotSupportedException)
        {
            error = new(
                "INVALID_ARGUMENT",
                "targetPath ist kein gültiger Source-Dateipfad.",
                "targetPath als absoluten Pfad einer vorhandenen .sln- oder .slnx-Datei angeben.");
            return false;
        }

        error = new(
            "SOLUTION_NOT_FOUND",
            "Die angegebene Source-Solution wurde nicht gefunden.",
            "targetPath als absoluten Pfad einer vorhandenen .sln- oder .slnx-Datei angeben.");
        return false;
    }
}
