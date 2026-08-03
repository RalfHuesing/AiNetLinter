#nullable enable

using AiNetLinter.Baseline;
using AiNetLinter.Configuration;
using AiNetLinter.Output;

namespace AiNetLinter.Mcp;

/// <summary>
/// Input-Parametersatz fuer <see cref="McpCodeGraphServer"/>. Eingefuehrt als
/// Ersatz fuer den frueheren 5-Parameter-Konstruktor, der am projektweiten
/// <c>MaxConstructorDependencies: 5</c>-Limit (siehe <c>AiNetLinter.mdc</c>)
/// exakt angelangt war — jede weitere P0/P1-Erweiterung am Konstruktor
/// haette den Build gebrochen. Mit diesem Record wachsen kuenftige
/// Konfigurations-Properties additiv, ohne die Konstruktor-Signatur zu aendern.
/// </summary>
internal sealed record McpCodeGraphServerOptions
{
    /// <summary>Geladene Solution, kann <see langword="null"/> sein fuer nicht-ladbare Fixtures.</summary>
    public required SourceFileCatalog? Catalog { get; init; }

    /// <summary>Haupt-Konsolen-Kanal fuer Server-Logs und Lint-Warnungen.</summary>
    public required ILintConsole Console { get; init; }

    /// <summary>Zeilen-Grenzwert fuer <c>get_hotspots</c>-Klassifikation, Default 700
    /// (siehe <c>MetricsConfig.MaxLineCount</c>).</summary>
    public int MaxLineCount { get; init; } = 700;

    /// <summary>Vollstaendige Linter-Konfiguration aus <c>rules.json</c> via <c>--config</c>,
    /// sonst Default-<see cref="Config"/>. Exposed als schmale Lese-Sicht
    /// (<see cref="ILinterEngineConfig"/>), damit der vollstaendige <c>Configuration</c>-Namespace
    /// nicht in den Footprint der <c>McpCodeGraphServer</c>-Konsumenten gezogen wird.</summary>
    public required ILinterEngineConfig Config { get; init; }

    /// <summary>
    /// Factory-Methode mit identischer Parameter-Signatur wie der vorherige
    /// <c>McpCodeGraphServer</c>-Konstruktor. Erlaubt minimal-invasive Migration
    /// der Call-Sites (1:1-Uebersetzung) ohne neuen 5-Parameter-Record-Konstruktor.
    /// <c>consoleOverride</c> wurde bewusst entfernt: kein einziger Call-Site
    /// uebergibt ihn.
    /// </summary>
    public static McpCodeGraphServerOptions From(
        SourceFileCatalog? catalog,
        ILintConsole? console = null,
        int maxLineCount = 700,
        Config? config = null)
    {
        return new McpCodeGraphServerOptions
        {
            Catalog = catalog,
            Console = console ?? LinterConsole.Instance,
            MaxLineCount = maxLineCount,
            Config = config ?? new Config { Global = new GlobalConfig(), Metrics = new MetricsConfig() },
        };
    }
}
