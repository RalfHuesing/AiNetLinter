#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AiNetLinter.Mcp;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AiNetLinter.Mcp.Tools.MagicValues;

internal static partial class FindMagicValuesScanner
{
    private static FindMagicValuesPayload BuildPayload(
        IReadOnlyList<GroupedMagicValue> grouped,
        int maxResults,
        FindMagicValuesScannerParameters p,
        int matchingFileCount,
        string? scopeStatus,
        string? scopeCause)
    {
        var shown = grouped.Take(maxResults).ToList();
        var scope = BuildScopeDescription(p, matchingFileCount);
        var total = grouped.Count;
        var returnedCount = shown.Count;
        var totalOccurrences = grouped.Sum(g => g.Occurrences);
        var returnedOccurrences = shown.Sum(g => g.Occurrences);
        var status = scopeStatus ?? DetermineStatus(matchingFileCount, total, returnedCount);
        var cause = scopeCause ?? DetermineCause(status, matchingFileCount, total, returnedCount);
        var categoryContext = new CategoryBuildContext(grouped, shown, status, cause, scope, matchingFileCount);
        var categories = Enum.GetValues<MagicValueCategory>()
            .Select(category => BuildCategorySummary(category, categoryContext))
            .ToList();
        var summarySemantics = ("Statische Syntax-/Semantik-Heuristik über C#-Literale im angeforderten Scope; " +
            "keine Laufzeit-, externen Consumer- oder globale Abwesenheitsaussage.",
            status == "empty" ? null : "Kandidaten manuell prüfen; keine automatische Änderung aus dem Audit ableiten.");
        return new FindMagicValuesPayload(
            MagicValues: shown.Select(g => new MagicValueEntry(
                FilePath: g.FilePath,
                Line: g.FirstLine,
                Column: g.FirstColumn,
                ValueType: g.ValueType.ToStringValue(),
                Value: g.Value,
                Category: g.Category.ToStringValue(),
                Recommendation: g.Recommendation,
                ContextHint: g.ContextHint,
                Occurrences: g.Occurrences,
                EvidenceBoundary: g.Category.Semantics().EvidenceBoundary,
                Scope: scope)).ToList(),
            Categories: categories,
            Summary: new MagicValuesSummary(
                Total: total,
                ShownOccurrences: returnedCount,
                ByCategoryConfig: grouped.Count(g => g.Category == MagicValueCategory.ConfigCandidates),
                ByCategoryConstant: grouped.Count(g => g.Category == MagicValueCategory.ConstantCandidates),
                ByCategoryStandard: grouped.Count(g => g.Category == MagicValueCategory.StandardCandidates),
                ByCategoryEnum: grouped.Count(g => g.Category == MagicValueCategory.EnumCandidates),
                ByCategoryNameof: grouped.Count(g => g.Category == MagicValueCategory.NameofCandidates),
                ByCategoryLocalization: grouped.Count(g => g.Category == MagicValueCategory.LocalizationCandidates),
                ByCategorySecurity: grouped.Count(g => g.Category == MagicValueCategory.SecurityCandidates),
                ReturnedCount: returnedCount,
                TotalOccurrences: totalOccurrences,
                ReturnedOccurrences: returnedOccurrences,
                FilesInScope: matchingFileCount,
                Status: status,
                Cause: cause,
                Confidence: DetermineConfidence(status),
                Next: BuildNextAction(status),
                TruncatedBy: total - returnedCount,
                EvidenceBoundary: summarySemantics.Item1,
                Scope: scope,
                Recommendation: summarySemantics.Item2));
    }

    private static MagicValueCategorySummary BuildCategorySummary(
        MagicValueCategory category,
        CategoryBuildContext context)
    {
        var categoryGroups = context.Grouped.Where(g => g.Category == category).ToList();
        var returned = context.Shown.Count(g => g.Category == category);
        var total = categoryGroups.Count;
        var status = context.OverallStatus == "not_decidable"
            ? "not_decidable"
            : DetermineStatus(total, returned);
        var cause = context.OverallStatus == "not_decidable"
            ? context.OverallCause
            : DetermineCause(status, context.MatchingFileCount, total, returned);
        var semantics = category.Semantics();
        return new MagicValueCategorySummary(
            Category: category.ToStringValue(),
            Total: total,
            ReturnedCount: returned,
            Status: status,
            Cause: cause,
            Confidence: DetermineConfidence(status),
            Next: BuildNextAction(status),
            TruncatedBy: total - returned,
            EvidenceBoundary: semantics.EvidenceBoundary,
            Scope: context.Scope,
            Recommendation: status == "empty" ? null : semantics.Recommendation);
    }

    private sealed record CategoryBuildContext(
        IReadOnlyList<GroupedMagicValue> Grouped,
        IReadOnlyList<GroupedMagicValue> Shown,
        string OverallStatus,
        string OverallCause,
        string Scope,
        int MatchingFileCount);

    private static string DetermineStatus(int matchingFileCount, int total, int returnedCount) =>
        matchingFileCount == 0 ? "not_decidable" : DetermineStatus(total, returnedCount);

    private static string DetermineStatus(int total, int returnedCount) =>
        total == 0 ? "empty" : returnedCount < total ? "truncated" : "checked";

    private static string DetermineCause(string status, int matchingFileCount, int total, int returnedCount) => status switch
    {
        "not_decidable" => "Keine analysierbaren C#-Dateien im angeforderten Scope; dies ist keine Aussage außerhalb des Scopes.",
        "empty" => $"Keine Kandidaten in den {matchingFileCount} geprüften Dateien; dies ist keine globale Abwesenheitsbehauptung.",
        "truncated" => $"{returnedCount} von {total} Kandidaten gezeigt; weitere Kandidaten sind durch maxResults ausgeblendet.",
        _ => $"Alle {total} Kandidaten im angeforderten Scope wurden geprüft.",
    };

    private static string DetermineConfidence(string status) => status switch
    {
        "checked" or "empty" => "high",
        "truncated" => "medium",
        _ => "low",
    };

    private static MagicValueNextAction? BuildNextAction(string status) => status switch
    {
        "not_decidable" => new("inspect_scope", "Scope-Filter, IncludeTests und changedOnly prüfen; danach den Scan wiederholen."),
        "empty" => null,
        "truncated" => new("continue", "maxResults erhöhen oder den Scope beziehungsweise categoryFilter verfeinern."),
        _ => null,
    };

    private static string BuildScopeDescription(FindMagicValuesScannerParameters p, int matchingFileCount)
    {
        var filter = string.IsNullOrWhiteSpace(p.ScopeFilter) ? "ohne scopeFilter" : $"scopeFilter='{p.ScopeFilter}'";
        var tests = p.IncludeTests ? "Tests eingeschlossen" : "Tests ausgeschlossen";
        var changed = p.ChangedOnly ? "nur geänderte Dateien" : "alle passenden Dateien";
        return $"{matchingFileCount} analysierbare C#-Dateien ({filter}; {tests}; {changed})";
    }
}
