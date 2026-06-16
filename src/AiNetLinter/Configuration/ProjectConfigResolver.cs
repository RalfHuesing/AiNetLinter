#nullable enable

using System;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;

namespace AiNetLinter.Configuration;

/// <summary>
/// Löst die effektive Konfiguration für ein Dokument oder ein Projekt auf Basis von Projekt-Overrides auf.
/// </summary>
public static class ProjectConfigResolver
{
    /// <summary>
    /// Löst die effektive Linter-Konfiguration für ein bestimmtes Roslyn-Dokument auf.
    /// Erst ProjectOverrides, dann PathOverrides (höhere Priorität).
    /// </summary>
    /// <param name="document">Das zu analysierende Roslyn-Dokument.</param>
    /// <param name="globalConfig">Die globale Linter-Konfiguration.</param>
    /// <param name="solutionBasePath">Basis-Pfad der Solution für relative Pfadberechnung.</param>
    /// <returns>Die für das Dokument effektive Linter-Konfiguration.</returns>
    public static LinterConfig ResolveForDocument(Document document, LinterConfig globalConfig, string? solutionBasePath = null)
    {
        var config = ResolveForProject(document.Project.Name, globalConfig);

        if (document.FilePath != null && globalConfig.PathOverrides.Count > 0)
        {
            config = ResolveForPath(document.FilePath, solutionBasePath, config, globalConfig.PathOverrides);
        }

        return config;
    }

    private static LinterConfig ResolveForPath(
        string filePath,
        string? solutionBasePath,
        LinterConfig config,
        IReadOnlyDictionary<string, ProjectOverrideEntry> pathOverrides)
    {
        var relativePath = ResolveRelativePath(filePath, solutionBasePath);

        foreach (var pair in pathOverrides)
        {
            if (MatchesGlobPath(relativePath, pair.Key))
            {
                return MergeConfig(config, pair.Value);
            }
        }

        return config;
    }

    internal static string ResolveRelativePath(string filePath, string? solutionBasePath)
    {
        if (string.IsNullOrEmpty(solutionBasePath))
            return filePath.Replace('\\', '/');

        if (filePath.StartsWith(solutionBasePath, StringComparison.OrdinalIgnoreCase))
            return filePath[solutionBasePath.Length..].TrimStart('/', '\\').Replace('\\', '/');

        return filePath.Replace('\\', '/');
    }

    internal static bool MatchesGlobPath(string relativePath, string pattern)
    {
        var normalizedPattern = pattern.Replace('\\', '/');
        var regexPattern = "^" +
            Regex.Escape(normalizedPattern)
                 .Replace("\\*\\*", ".*")
                 .Replace("\\*", "[^/]*")
            + "$";
        return Regex.IsMatch(relativePath, regexPattern, RegexOptions.IgnoreCase);
    }

    /// <summary>
    /// Löst die effektive Linter-Konfiguration anhand von Projektname und absolutem Dateipfad auf.
    /// Wird für post-analytische Checks verwendet, bei denen kein Roslyn-Document verfügbar ist.
    /// Erst ProjectOverrides, dann PathOverrides (höhere Priorität).
    /// </summary>
    public static LinterConfig ResolveForFile(string? filePath, string? projectName, LinterConfig globalConfig)
    {
        var config = projectName != null
            ? ResolveForProject(projectName, globalConfig)
            : globalConfig;

        if (filePath != null && globalConfig.PathOverrides.Count > 0)
        {
            config = ResolveForPath(filePath, globalConfig.SolutionBasePath, config, globalConfig.PathOverrides);
        }

        return config;
    }

    /// <summary>
    /// Löst die effektive Linter-Konfiguration für einen Projektnamen auf.
    /// </summary>
    /// <param name="projectName">Der Name des Roslyn-Projekts.</param>
    /// <param name="globalConfig">Die globale Linter-Konfiguration.</param>
    /// <returns>Die für das Projekt effektive Linter-Konfiguration.</returns>
    public static LinterConfig ResolveForProject(string projectName, LinterConfig globalConfig)
    {
        if (globalConfig.ProjectOverrides == null || globalConfig.ProjectOverrides.Count == 0)
        {
            return globalConfig;
        }

        foreach (var pair in globalConfig.ProjectOverrides)
        {
            if (IsMatch(projectName, pair.Key))
            {
                return MergeConfig(globalConfig, pair.Value);
            }
        }

        return globalConfig;
    }

    private static bool IsMatch(string name, string pattern)
    {
        var regexPattern = "^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$";
        return Regex.IsMatch(name, regexPattern, RegexOptions.IgnoreCase);
    }

    private static LinterConfig MergeConfig(LinterConfig global, ProjectOverrideEntry overrides)
    {
        return global with
        {
            Global = global.Global.Apply(overrides.Global),
            Metrics = global.Metrics.Apply(overrides.Metrics),
            MagicValues = global.MagicValues.Apply(overrides.MagicValues),
            TestSentinel = global.TestSentinel.Apply(overrides.TestSentinel),
            UiSeparation = global.UiSeparation.Apply(overrides.UiSeparation),
        };
    }
}
