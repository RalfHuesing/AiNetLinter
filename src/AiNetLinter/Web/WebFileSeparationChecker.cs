#nullable enable

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using AiNetLinter.Configuration;
using AiNetLinter.Core;
using AiNetLinter.Models;

namespace AiNetLinter.Web;

/// <summary>
/// Post-Analyse-Check fuer Web-Dateien (Phase 1: CSS, spaeter JS/Razor).
/// Arbeitet auf dem Dateisystem (Roslyn sieht keine .css/.js/.razor-Dateien).
/// Spiegel des UiFileSeparationChecker-Patterns aus Epic 22.
/// </summary>
internal static class WebFileSeparationChecker
{
    /// <summary>
    /// Startet die Web-Analyse fuer die gesamte Solution.
    /// Fruehzeitiger Return, wenn Web.IsEnabled false ist (default) oder keine CSS-Konfiguration aktiv.
    /// </summary>
    public static void Run(AnalysisState state, LinterConfig config)
    {
        if (!config.Web.IsEnabled) return;

        // Phase 1 unterstuetzt nur CSS. JS/Razor werden in spaeteren Phasen ergaenzt
        // und hier als No-Op behandelt.
        var solutionDir = GetSolutionDir(state.Solution);
        if (string.IsNullOrEmpty(solutionDir)) return;

        var cssEntries = WebFileCatalog.Collect(
            state.Solution,
            solutionDir,
            config.FileFilters,
            config.Web.Css.ExemptPaths)
            .Where(e => e.Type == WebFileType.Css)
            .ToArray();

        if (cssEntries.Length == 0) return;

        foreach (var entry in cssEntries)
        {
            // Per-File ProjectOverride aufloesen — relevante Eigenschaften sind nur
            // die CSS-Schwellenwerte (IsEnabled ist global gesteuert).
            var effective = ProjectConfigResolver.ResolveForFile(
                entry.AbsolutePath,
                projectName: null,
                globalConfig: config);

            if (!effective.Web.IsEnabled) continue;
            if (!IsAnyCssRuleActive(effective.Web.Css)) continue;

            AnalyzeCssFile(entry, effective, state.Violations);
        }
    }

    private static void AnalyzeCssFile(
        WebFileEntry entry,
        LinterConfig effectiveConfig,
        ConcurrentBag<RuleViolation> violations)
    {
        string content;
        try
        {
            content = File.ReadAllText(entry.AbsolutePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return;
        }

        var fileViolations = CssAnalyzer.Analyze(content, entry.AbsolutePath, effectiveConfig.Web.Css);

        foreach (var v in fileViolations)
        {
            if (WebSuppressionHelper.IsSuppressed(content, v.RuleName)) continue;
            violations.Add(v);
        }
    }

    private static bool IsAnyCssRuleActive(CssConfig css)
    {
        return css.MaxCssLineCount > 0
            || css.PreferScopedCss
            || css.MaxCssSelectorComplexity > 0;
    }

    private static string? GetSolutionDir(Microsoft.CodeAnalysis.Solution solution) =>
        string.IsNullOrEmpty(solution.FilePath)
            ? null
            : Path.GetDirectoryName(solution.FilePath);
}
