#nullable enable

using AiNetLinter.Configuration;

namespace AiNetLinter.Mcp.Projects;

/// <summary>
/// Materialisiert die explizit benachbarte Regeldatei und baut daraus Server-Options.
/// Ein fehlender Nachbarpfad bleibt eine navigation-fähige Session; der Lint-Einstieg
/// erkennt den fehlenden <c>ResolvedConfigPath</c> vor jeder Analyse als <c>not_configured</c>.
/// </summary>
internal static class ProjectInstanceFactory
{
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
        // absent. This is an intentional, navigation-capable session state. The null
        // ResolvedConfigPath on the resulting server is the sole lint-capability marker.
        if (string.IsNullOrWhiteSpace(definition.RulesPath))
        {
            return createFromOptions(McpCodeGraphServerOptions.From(
                new McpCodeGraphServerOptionsFromParameters(
                    Catalog: null,
                    Config: null,
                    ResolvedConfigPath: null)));
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

        return createFromOptions(McpCodeGraphServerOptions.From(
            new McpCodeGraphServerOptionsFromParameters(
                Catalog: null,
                MaxLineCount: config.Metrics.MaxLineCount,
                Config: config,
                ResolvedConfigPath: definition.RulesPath)));
    }

    private static ProjectInstanceCreation RulesInvalid(string rulesPath, string? detail = null) =>
        ProjectInstanceCreation.Failed(
            ProjectErrorCodes.RulesInvalid,
            string.Join(
                Environment.NewLine,
                $"Regeldatei konnte nicht gelesen oder validiert werden: {rulesPath}.",
                detail is null ? null : $"Konfigurationsfehler: {detail}",
                "JSON-Syntax und Felder gegen das Schema pruefen; es wurden bewusst keine " +
                "technischen Ersatzregeln geladen.",
                "Minimale gueltige ainetlinter-rules.json zum Kopieren:",
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
