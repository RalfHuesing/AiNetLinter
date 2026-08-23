#nullable enable

using AiNetLinter.Configuration;

namespace AiNetLinter.Mcp.Projects;

/// <summary>
/// Gemeinsame Materialisierung der Regeldatei zu Config und MaxLineCount für den Batch- und
/// den Registry-Pfad (identische Semantik, keine Duplizierung) sowie Aufbau der Server-Options
/// aus einer Projektdefinition. Der Batch-Pfad materialisiert mit Defaults als Rückfallebene;
/// der Registry-Pfad scheitert über <see cref="TryCreate"/> deterministisch mit Fehlercode,
/// statt eine Instanz mit Default-Regeln zu starten.
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

    /// <summary>
    /// Erzeugt die Server-Options fuer eine Projektdefinition oder meldet einen deterministischen
    /// Fehlercode. Eine lesbare, aber ungueltige Regeldatei wird nie durch Defaults ersetzt —
    /// der Aufrufer entscheidet per Rueckgabe, ob eine residente Instanz entsteht.
    /// </summary>
    internal static ProjectInstanceCreation TryCreate(
        ProjectDefinition definition,
        Func<McpCodeGraphServerOptions, ProjectInstanceCreation> createFromOptions)
    {
        if (!File.Exists(definition.RulesPath))
        {
            return ProjectInstanceCreation.Failed(
                ProjectErrorCodes.RulesNotFound,
                $"Regeldatei nicht gefunden: {definition.RulesPath}.");
        }

        var config = ConfigLoader.TryLoadConfig(definition.RulesPath, isRequired: false);
        if (config is null)
        {
            return ProjectInstanceCreation.Failed(
                ProjectErrorCodes.RulesInvalid,
                $"Regeldatei ist lesbar, aber ungueltig (JSON-Syntax/Felder pruefen): {definition.RulesPath}. " +
                "Es wurden bewusst keine Default-Regeln geladen.");
        }

        var options = McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(
            Catalog: null,
            MaxLineCount: config.Metrics.MaxLineCount,
            Config: config,
            UsedDefaultConfig: false,
            ResolvedConfigPath: definition.RulesPath));
        return createFromOptions(options);
    }
}

/// <summary>
/// Ergebnis der Instanz-Erzeugung im Registry-Pfad: entweder eine konfigurierte Server-Instanz
/// (Solution-Load startet erst im Konstruktor als Hintergrund-Task) oder Fehlercode plus
/// Ursprungsmeldung ohne Eintrag in der Projektregistry.
/// </summary>
internal sealed record ProjectInstanceCreation(
    McpCodeGraphServer? Server,
    string? ErrorCode = null,
    string? ErrorMessage = null)
{
    internal bool Succeeded => Server is not null;

    internal static ProjectInstanceCreation Resident(McpCodeGraphServer server) => new(server);

    internal static ProjectInstanceCreation Failed(string errorCode, string errorMessage) =>
        new(null, errorCode, errorMessage);
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
