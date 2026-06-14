#nullable enable

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
    /// </summary>
    /// <param name="document">Das zu analysierende Roslyn-Dokument.</param>
    /// <param name="globalConfig">Die globale Linter-Konfiguration.</param>
    /// <returns>Die für das Dokument effektive Linter-Konfiguration.</returns>
    public static LinterConfig ResolveForDocument(Document document, LinterConfig globalConfig)
    {
        return ResolveForProject(document.Project.Name, globalConfig);
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
        };
    }
}
