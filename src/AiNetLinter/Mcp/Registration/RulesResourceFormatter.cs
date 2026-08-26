#nullable enable

using System;
using System.Linq;
using System.Text;
using AiNetLinter.Configuration;
using AiNetLinter.Core;
using AiNetLinter.Mcp.Projects;
using AiNetLinter.Output;

namespace AiNetLinter.Mcp.Registration;

internal static class RulesResourceFormatter
{
    internal static string BuildMarkdown(ProjectSnapshot snapshot)
    {
        var configSnapshot = snapshot.Server.GetConfigSnapshot();
        if (configSnapshot.Config is not Config config)
        {
            throw new InvalidOperationException("Die resident gehaltene MCP-Konfiguration ist keine Config-Instanz.");
        }

        return BuildMarkdown(
            snapshot.RootPath,
            config,
            configSnapshot.UsedDefaultConfig,
            configSnapshot.ResolvedConfigPath);
    }

    internal static string BuildMarkdown(
        string projectRoot,
        Config config,
        bool usedDefaultConfig,
        string? resolvedConfigPath)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# AiNetLinter — effektive Regelkonfiguration");
        builder.AppendLine();
        builder.AppendLine($"- Projektroot: `{projectRoot}`");
        builder.AppendLine($"- Konfigurationsquelle: {DescribeConfigOrigin(usedDefaultConfig, resolvedConfigPath)}");
        builder.AppendLine("- Grundlage: aktueller atomarer Config-Snapshot des adressierten Projekt-Keys.");
        builder.AppendLine();

        AppendActiveRules(builder, config);
        AppendThresholds(builder, config);
        AppendDisabledRules(builder, config);
        AppendProjectOverrides(builder, config);
        return builder.ToString().TrimEnd();
    }

    private static string DescribeConfigOrigin(bool usedDefaultConfig, string? resolvedConfigPath) =>
        usedDefaultConfig
            ? "`eingebaute Default-Konfiguration`"
            : string.IsNullOrWhiteSpace(resolvedConfigPath)
                ? "`unbekannt`"
                : $"`{resolvedConfigPath}`";

    private static void AppendActiveRules(StringBuilder builder, Config config)
    {
        builder.AppendLine("## Aktive Regeln");
        builder.AppendLine();
        var table = new MarkdownTableBuilder()
            .AddColumn("Regel")
            .AddColumn("Intent")
            .AddColumn("Severity")
            .AddColumn("Beschreibung")
            .AddColumn("Konfiguration");

        foreach (var rule in RuleRegistry.All
                     .Where(rule => !rule.IsMetric && rule.IsEnabled(config))
                     .OrderBy(rule => rule.RuleId, StringComparer.Ordinal))
        {
            var metadata = RuleMetadataRegistry.Resolve(rule.RuleId, config);
            table.AddRow(
                $"`{rule.RuleId}`",
                metadata.Intent,
                metadata.Severity,
                rule.GetShortDescription(config),
                rule.ConfigKeyHint ?? $"rules.json → Global.{rule.RuleId}");
        }

        var markdown = new MarkdownBuilder();
        markdown.Table(table);
        markdown.AppendTo(builder);
        builder.AppendLine();
    }

    private static void AppendThresholds(StringBuilder builder, Config config)
    {
        builder.AppendLine("## Effektive Schwellwerte");
        builder.AppendLine();
        var table = new MarkdownTableBuilder()
            .AddColumn("Regel")
            .AddColumn("Limit", ColumnAlign.Right)
            .AddColumn("Status")
            .AddColumn("Konfiguration");

        foreach (var rule in RuleRegistry.All
                     .Where(rule => rule.IsMetric)
                     .OrderBy(rule => rule.RuleId, StringComparer.Ordinal))
        {
            var limit = rule.GetMetricLimit?.Invoke(config) ?? 0;
            table.AddRow(
                $"`{rule.RuleId}`",
                limit,
                rule.IsEnabled(config) ? "aktiv" : "deaktiviert",
                rule.ConfigKeyHint ?? $"rules.json → Metrics.{rule.RuleId}");
        }

        var markdown = new MarkdownBuilder();
        markdown.Table(table);
        markdown.AppendTo(builder);
        builder.AppendLine();
    }

    private static void AppendDisabledRules(StringBuilder builder, Config config)
    {
        var disabled = RuleRegistry.All
            .Where(rule => !rule.IsMetric && !rule.IsEnabled(config))
            .OrderBy(rule => rule.RuleId, StringComparer.Ordinal)
            .ToList();
        if (disabled.Count == 0)
        {
            return;
        }

        builder.AppendLine("## Deaktivierte Regeln");
        builder.AppendLine();
        builder.AppendLine(string.Join(", ", disabled.Select(rule => $"`{rule.RuleId}`")));
        builder.AppendLine();
    }

    private static void AppendProjectOverrides(StringBuilder builder, Config config)
    {
        if (config.ProjectOverrides.Count == 0 && config.PathOverrides.Count == 0)
        {
            return;
        }

        builder.AppendLine("## Weitere effektive Overrides");
        builder.AppendLine();
        if (config.ProjectOverrides.Count > 0)
        {
            builder.AppendLine($"- Projekt-Overrides: {config.ProjectOverrides.Count} Muster");
        }
        if (config.PathOverrides.Count > 0)
        {
            builder.AppendLine($"- Pfad-Overrides: {config.PathOverrides.Count} Muster");
        }
        builder.AppendLine("Die konkrete Anwendung erfolgt pro Roslyn-Projekt bzw. Datei; Details stehen in der referenzierten rules.json.");
        builder.AppendLine();
    }
}
