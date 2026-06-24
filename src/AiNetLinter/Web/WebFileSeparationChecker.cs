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
/// Post-Analyse-Check fuer Web-Dateien (Phase 1: CSS, Phase 2: JS, Phase 3: Razor).
/// Arbeitet auf dem Dateisystem (Roslyn sieht keine .css/.js/.razor-Dateien).
/// Spiegel des UiFileSeparationChecker-Patterns aus Epic 22.
/// </summary>
internal static class WebFileSeparationChecker
{
    /// <summary>
    /// Startet die Web-Analyse fuer die gesamte Solution.
    /// Fruehzeitiger Return, wenn Web.IsEnabled false ist (default) oder keine Web-Konfiguration aktiv.
    /// </summary>
    public static void Run(AnalysisState state, LinterConfig config)
    {
        if (!config.Web.IsEnabled) return;

        var solutionDir = GetSolutionDir(state.Solution);
        if (string.IsNullOrEmpty(solutionDir)) return;

        var request = new WebFileDiscoveryRequest(
            FileFilters: config.FileFilters,
            CssExemptPaths: config.Web.Css.ExemptPaths,
            JsExemptPaths: config.Web.Js.ExemptPaths);

        var entries = WebFileCatalog.Collect(state.Solution, solutionDir, request);
        if (entries.Count == 0) return;

        AnalyzeCssEntries(entries, config, state.Violations);
        AnalyzeJsEntries(entries, config, state.Violations);
    }

    private static void AnalyzeCssEntries(
        IReadOnlyList<WebFileEntry> entries,
        LinterConfig config,
        ConcurrentBag<RuleViolation> violations)
    {
        foreach (var entry in entries.Where(e => e.Type == WebFileType.Css))
        {
            var effective = ResolveForFile(entry.AbsolutePath, config);
            if (!IsCssAnalysisActive(effective)) continue;

            AnalyzeSingleFile(
                entry,
                effective,
                content => CssAnalyzer.Analyze(content, entry.AbsolutePath, effective.Web.Css),
                violations);
        }
    }

    private static void AnalyzeJsEntries(
        IReadOnlyList<WebFileEntry> entries,
        LinterConfig config,
        ConcurrentBag<RuleViolation> violations)
    {
        foreach (var entry in entries.Where(e => e.Type == WebFileType.Js))
        {
            var effective = ResolveForFile(entry.AbsolutePath, config);
            if (!IsJsAnalysisActive(effective)) continue;

            AnalyzeSingleFile(
                entry,
                effective,
                content => JsAnalyzer.Analyze(content, entry.AbsolutePath, effective.Web.Js),
                violations);
        }
    }

    private static void AnalyzeSingleFile(
        WebFileEntry entry,
        LinterConfig effectiveConfig,
        Func<string, System.Collections.Generic.IReadOnlyList<RuleViolation>> analyze,
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

        var fileViolations = analyze(content);

        foreach (var v in fileViolations)
        {
            if (WebSuppressionHelper.IsSuppressed(content, v.RuleName)) continue;
            violations.Add(v);
        }
    }

    private static LinterConfig ResolveForFile(string absolutePath, LinterConfig globalConfig) =>
        ProjectConfigResolver.ResolveForFile(absolutePath, projectName: null, globalConfig: globalConfig);

    private static bool IsCssAnalysisActive(LinterConfig effective) =>
        effective.Web.IsEnabled && IsAnyCssRuleActive(effective.Web.Css);

    private static bool IsJsAnalysisActive(LinterConfig effective) =>
        effective.Web.IsEnabled && IsAnyJsRuleActive(effective.Web.Js);

    private static bool IsAnyCssRuleActive(CssConfig css) =>
        css.MaxCssLineCount > 0
        || css.PreferScopedCss
        || css.MaxCssSelectorComplexity > 0;

    private static bool IsAnyJsRuleActive(JsConfig js) =>
        js.MaxJsLineCount > 0
        || js.EnforceJsModules;

    private static string? GetSolutionDir(Microsoft.CodeAnalysis.Solution solution) =>
        string.IsNullOrEmpty(solution.FilePath)
            ? null
            : Path.GetDirectoryName(solution.FilePath);
}
