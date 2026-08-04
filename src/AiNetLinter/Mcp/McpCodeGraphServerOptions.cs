#nullable enable

using AiNetLinter.Baseline;
using AiNetLinter.Configuration;
using AiNetLinter.Output;

namespace AiNetLinter.Mcp;

/// <summary>
/// Input-Parametersatz fuer <see cref="McpCodeGraphServer"/>. Kapselt die Optionen in einem
/// Record, damit <c>MaxConstructorDependencies: 5</c> eingehalten wird und kuenftige
/// Konfigurations-Properties additiv wachsen koennen, ohne die Konstruktor-Signatur zu
/// aendern.
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
    /// True, wenn <c>McpServerCommand</c> keine <c>rules.json</c> neben der aufgeloesten
    /// Solution-Datei finden konnte und der Server mit der <see cref="Config"/>-Default-
    /// Konfiguration laeuft. <c>get_violations</c> zeigt in diesem Fall eine sichtbare
    /// Header-Zeile an. Siehe <see cref="McpCodeGraphServer.UsedDefaultConfig"/>.
    /// </summary>
    public bool UsedDefaultConfig { get; init; }

    /// <summary>
    /// Factory-Methode, kapselt die ehemaligen 5 Parameter in einem Record, damit
    /// <c>MaxMethodParameterCount: 4</c> (siehe <c>AiNetLinter.mdc</c>) nicht verletzt wird.
    /// Existierende Call-Sites koennen <see cref="McpCodeGraphServerOptions"/> auch direkt
    /// via <c>new McpCodeGraphServerOptions { ... }</c> konstruieren, sobald der
    /// <c>From(...)</c>-Einstiegspunkt nicht mehr gebraucht wird.
    /// </summary>
    public static McpCodeGraphServerOptions From(McpCodeGraphServerOptionsFromParameters p)
    {
        return new McpCodeGraphServerOptions
        {
            Catalog = p.Catalog,
            Console = p.Console ?? LinterConsole.Instance,
            MaxLineCount = p.MaxLineCount,
            Config = p.Config ?? new Config { Global = new GlobalConfig(), Metrics = new MetricsConfig() },
            UsedDefaultConfig = p.UsedDefaultConfig,
        };
    }
}

/// <summary>
/// Parameter-Record fuer <see cref="McpCodeGraphServerOptions.From"/>. Fasst die
/// ehemalige 5-Parameter-Signatur zusammen, damit <c>MaxMethodParameterCount: 4</c>
/// eingehalten wird.
/// </summary>
internal sealed record McpCodeGraphServerOptionsFromParameters(
    SourceFileCatalog? Catalog,
    ILintConsole? Console = null,
    int MaxLineCount = 700,
    Config? Config = null,
    bool UsedDefaultConfig = false);
