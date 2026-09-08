#nullable enable

using AiNetLinter.Configuration;

namespace AiNetLinter.Mcp.Projects;

/// <summary>
/// Gemeinsame Materialisierung der Regeldatei zu Config und MaxLineCount für den Batch- und
/// den Registry-Pfad (identische Semantik, keine Duplizierung) sowie Aufbau der Server-Options
/// aus einer Projektdefinition. Der Batch-Pfad materialisiert mit Defaults als Rückfallebene;
/// der Registry-Pfad akzeptiert den ausdrücklich nicht konfigurierten Nachbarpfad als
/// navigation-fähige Session, scheitert bei vorhandenen ungültigen Regeln jedoch deterministisch
/// mit Fehlercode statt diese durch Defaults zu ersetzen.
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
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(createFromOptions);

        // ProjectDefinitionLoader leaves RulesPath empty when the optional neighbor file is
        // absent. This is an intentional, navigation-capable session state: materialize the
        // engine defaults for non-lint consumers, but mark the source as unconfigured so callers
        // cannot mistake it for an explicitly configured lint session.
        if (string.IsNullOrWhiteSpace(definition.RulesPath))
        {
            var defaults = MaterializedRules.Defaults();
            return createFromOptions(CreateOptions(
                defaults,
                usedDefaultConfig: true,
                resolvedConfigPath: null));
        }

        Config? config;
        try
        {
            config = File.Exists(definition.RulesPath)
                ? ConfigLoader.TryLoadConfig(definition.RulesPath, isRequired: false)
                : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           NotSupportedException or InvalidOperationException)
        {
            return RulesInvalid(definition.RulesPath, exception.Message);
        }

        if (config is null)
        {
            return RulesInvalid(definition.RulesPath);
        }

        return createFromOptions(CreateOptions(
            new MaterializedRules(config, config.Metrics.MaxLineCount),
            usedDefaultConfig: false,
            resolvedConfigPath: definition.RulesPath));
    }

    private static McpCodeGraphServerOptions CreateOptions(
        MaterializedRules rules,
        bool usedDefaultConfig,
        string? resolvedConfigPath) =>
        McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(
            Catalog: null,
            MaxLineCount: rules.MaxLineCount,
            Config: rules.Config,
            UsedDefaultConfig: usedDefaultConfig,
            ResolvedConfigPath: resolvedConfigPath));

    private static ProjectInstanceCreation RulesInvalid(string rulesPath, string? detail = null) =>
        ProjectInstanceCreation.Failed(
            ProjectErrorCodes.RulesInvalid,
            string.Join(
                Environment.NewLine,
                $"Regeldatei konnte nicht gelesen oder validiert werden: {rulesPath}.",
                detail is null ? null : $"Konfigurationsfehler: {detail}",
                "JSON-Syntax und Felder gegen das Schema pruefen; es wurden bewusst keine " +
                "Default-Regeln geladen.",
                "Minimale gueltige rules.json zum Kopieren:",
                "{",
                "  \"Global\": {},",
                "  \"Metrics\": { \"MaxLineCount\": 700 }",
                "}",
                "Danach den Aufruf mit demselben targetPath wiederholen."));
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
