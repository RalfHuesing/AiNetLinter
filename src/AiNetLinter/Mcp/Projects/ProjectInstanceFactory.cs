#nullable enable

using AiNetLinter.Configuration;

namespace AiNetLinter.Mcp.Projects;

/// <summary>
/// Gemeinsame Materialisierung der Regeldatei zu Config und MaxLineCount für den Batch- und
/// den Registry-Pfad (identische Semantik, keine Duplizierung) sowie Aufbau der Server-Options
/// aus einer Projektdefinition. Die Existenzprüfung der Regeldatei bleibt im Loader — hier
/// wird nur geladen, mit Defaults als Rückfallebene.
/// </summary>
internal static class ProjectInstanceFactory
{
    internal static MaterializedRules MaterializeRules(string? rulesPath, bool isRequired)
    {
        if (string.IsNullOrWhiteSpace(rulesPath))
            return MaterializedRules.Defaults();

        var config = ConfigLoader.TryLoadConfig(rulesPath, isRequired) ?? MaterializedRules.Defaults().Config;
        return new MaterializedRules(config, config.Metrics.MaxLineCount);
    }

    internal static McpCodeGraphServerOptions Create(ProjectDefinition definition)
    {
        var rules = MaterializeRules(definition.RulesPath, isRequired: true);
        return McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(
            Catalog: null,
            MaxLineCount: rules.MaxLineCount,
            Config: rules.Config,
            UsedDefaultConfig: false,
            ResolvedConfigPath: definition.RulesPath));
    }
}

/// <summary>
/// Ergebnis der Regel-Materialisierung: volle Konfiguration plus daraus abgeleiteter
/// Zeilen-Grenzwert (bei Ladefehlern die jeweiligen Defaults).
/// </summary>
internal sealed record MaterializedRules(Config Config, int MaxLineCount)
{
    internal static MaterializedRules Defaults() => new(
        new Config { Global = new GlobalConfig(), Metrics = new MetricsConfig() },
        new MetricsConfig().MaxLineCount);
}
