#nullable enable

using System;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;

namespace AiNetLinter.Configuration;

public static class ProjectConfigResolver
{
    public static Config ResolveForDocument(Document document, Config globalConfig, string? solutionBasePath = null)
    {
        var config = ResolveForProject(document.Project.Name, globalConfig);

        if (document.FilePath != null && globalConfig.PathOverrides.Count > 0)
        {
            config = ResolveForPath(document.FilePath, solutionBasePath, config, globalConfig.PathOverrides);
        }

        return config;
    }

    private static Config ResolveForPath(
        string filePath,
        string? solutionBasePath,
        Config config,
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

    public static Config ResolveForFile(string? filePath, string? projectName, Config globalConfig)
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

    public static Config ResolveForProject(string projectName, Config globalConfig)
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

    private static Config MergeConfig(Config global, ProjectOverrideEntry overrides)
    {
        return global with
        {
            Global = global.Global.Apply(overrides.Global),
            Metrics = global.Metrics.Apply(overrides.Metrics),
            TestSentinel = global.TestSentinel.Apply(overrides.TestSentinel),
            UiSeparation = global.UiSeparation.Apply(overrides.UiSeparation),
            Web = global.Web.Apply(overrides.Web),
        };
    }
}
