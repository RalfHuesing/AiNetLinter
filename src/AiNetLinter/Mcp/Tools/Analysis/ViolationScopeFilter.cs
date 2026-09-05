#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AiNetLinter.Baseline;
using AiNetLinter.Core;
using AiNetLinter.Models;
using AiNetLinter.Output;
using Microsoft.CodeAnalysis;

namespace AiNetLinter.Mcp.Tools.Analysis;

/// <summary>
/// Gemeinsame Scope-Filter-/Sortierlogik fuer MCP-Tools, die auf bereits von der
/// <see cref="AiNetLinter.Core.LinterEngine"/> erzeugten <see cref="RuleViolation"/>-Objekten
/// aufsetzen (<c>get_violations</c>, <c>pattern_detect</c>). Frueher 1:1 dupliziert in
/// <see cref="GetViolationsScanner"/> und <see cref="PatternDetect.PatternDetectScanner"/> — als
/// zweite Konsumentenstelle die Duplikation zur Wartungslast wurde, hier extrahiert, damit
/// Aenderungen an Filter-/Sortierlogik nur an einer Stelle noetig sind.
/// </summary>
internal static class ViolationScopeFilter
{
    /// <summary>Baut eine Datei→Projekt-Zuordnung ueber alle gueltigen Dokumente der Solution.</summary>
    internal static Dictionary<string, string> BuildFileToProjectMap(Solution solution, string solutionDir)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var project in solution.Projects)
        {
            foreach (var document in project.Documents)
            {
                if (!SourceFileCatalog.IsValidDocument(document, solutionDir)) continue;
                if (document.FilePath is null) continue;
                map[document.FilePath] = project.Name;
            }
        }
        return map;
    }

    /// <summary>
    /// Kein Filter gesetzt = alles im Scope. Sonst case-insensitiver <c>Contains</c>-Abgleich auf
    /// Projekt-Name oder solution-relativem Pfad mit einheitlichen Trennzeichen.
    /// </summary>
    internal static bool MatchesScope(string filePath, string projectName, string solutionDir, string? scopeFilter)
    {
        if (string.IsNullOrEmpty(scopeFilter)) return true;
        var cleaned = Common.McpInputNormalizer.StripEnclosingQuotesAndBackticks(scopeFilter);
        if (projectName.Contains(cleaned, StringComparison.OrdinalIgnoreCase)) return true;

        var relativePath = PathNormalizer.ToRelative(solutionDir, filePath);
        var effectiveFilter = Path.IsPathRooted(cleaned)
            ? Common.McpInputNormalizer.NormalizePathOrScope(cleaned, solutionDir)
            : cleaned;

        return PathNormalizer.MatchesScope(relativePath, effectiveFilter);
    }

    /// <summary>Anzahl der Dateien aus <paramref name="fileToProject"/>, die <paramref name="scopeFilter"/> matchen.</summary>
    internal static int CountMatchingFiles(
        Dictionary<string, string> fileToProject, string solutionDir, string? scopeFilter)
    {
        return fileToProject.Count(kvp => MatchesScope(kvp.Key, kvp.Value, solutionDir, scopeFilter));
    }

    /// <summary>
    /// Filtert <paramref name="violations"/> auf den Scope und sortiert stabil nach
    /// Datei→Zeile→Regel — eine Quelle der Wahrheit fuer Text- und StructuredContent-Ausgabe,
    /// damit beide nie auseinanderdriften.
    /// </summary>
    internal static IReadOnlyList<RuleViolation> FilterAndSortViolations(
        string solutionDir, Dictionary<string, string> fileToProject,
        IReadOnlyCollection<RuleViolation> violations, string? scopeFilter) =>
        FilterAndSortViolations(solutionDir, fileToProject, violations, new ViolationFilterOptions(scopeFilter));

    internal static IReadOnlyList<RuleViolation> FilterAndSortViolations(
        string solutionDir, Dictionary<string, string> fileToProject,
        IReadOnlyCollection<RuleViolation> violations, ViolationFilterOptions filterOptions)
    {
        return violations
            .Where(v =>
            {
                var projectName = LookupProjectName(fileToProject, v.FilePath) ?? string.Empty;
                if (!MatchesScope(v.FilePath, projectName, solutionDir, filterOptions.ScopeFilter)) return false;
                if (!string.IsNullOrWhiteSpace(filterOptions.RuleId)
                    && !v.RuleName.Equals(filterOptions.RuleId.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
                if (!MatchesMinSeverity(v, filterOptions.MinSeverity)) return false;
                return true;
            })
            .OrderBy(v => v.FilePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(v => v.LineNumber)
            .ThenBy(v => v.RuleName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool MatchesMinSeverity(RuleViolation violation, string? minSeverity)
    {
        if (string.IsNullOrWhiteSpace(minSeverity)) return true;
        var requiredRank = SeverityRank(minSeverity);
        if (requiredRank == 0) return true;
        var actualSeverity = RuleRegistry.ResolveSeverity(violation);
        var actualRank = SeverityRank(actualSeverity);
        return actualRank >= requiredRank;
    }

    private static int SeverityRank(string severity) =>
        severity.Trim().ToLowerInvariant() switch
        {
            "error" => 3,
            "warning" => 2,
            "info" => 1,
            _ => 0,
        };

    private static string? LookupProjectName(Dictionary<string, string> fileToProject, string filePath)
    {
        return fileToProject.TryGetValue(filePath, out var name) ? name : null;
    }
}

internal sealed record ViolationFilterOptions(
    string? ScopeFilter = null,
    string? RuleId = null,
    string? MinSeverity = null);
