#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Baseline;
using AiNetLinter.Configuration;
using AiNetLinter.Output;
using Microsoft.CodeAnalysis;

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
    /// Absoluter oder relativer Pfad der tatsaechlich geladenen <c>rules.json</c> — entweder
    /// explizit per <c>--config</c> angegeben oder per Auto-Discovery neben der Solution
    /// gefunden. <see langword="null"/>, wenn <see cref="UsedDefaultConfig"/> <see langword="true"/>
    /// ist. Rein informativ (z. B. fuer die <c>ainetlinter://overview</c>-Resource) — die
    /// eigentliche Config-Aufloesung ist bereits in <see cref="Config"/> abgeschlossen.
    /// </summary>
    public string? ResolvedConfigPath { get; init; }

    /// <summary>
    /// Optionaler Hintergrund-Loader: liefert er eine Solution, startet der Server den
    /// Load in einem <see cref="Task"/> und beantwortet Tool-Aufrufe waehrend dieser Zeit
    /// mit <see cref="McpToolResults.Loading"/>. <see langword="null"/> (Default)
    /// aktiviert den klassischen synchronen Pfad fuer Tests und Backward-Compat —
    /// <see cref="McpCodeGraphServer"/> uebernimmt dann den uebergebenen
    /// <see cref="Catalog"/> sofort. Setzt <c>McpServerCommand</c> den Hintergrund-Pfad
    /// aktiv, behaelt der Konstruktor die uebrigen Optionen (Config, Console, MaxLineCount)
    /// unveraendert.
    /// </summary>
    public Func<CancellationToken, Task<SourceFileCatalog?>>? LoadFunc { get; init; }

    internal Solution? ReadOnlySolutionSnapshot { get; init; }

    internal AnalysisSymbolIdentity? AssemblySymbolIdentity { get; init; }

    /// <summary>
    /// Factory-Methode, die die Konfigurations-Eingaenge in einem Parameter-Record bündelt,
    /// damit <c>MaxMethodParameterCount: 4</c> (siehe <c>AiNetLinter.mdc</c>) eingehalten wird
    /// und kuenftige Properties additiv am Options-Record wachsen koennen, ohne die
    /// Factory-Signatur zu aendern. Existierende Call-Sites koennen
    /// <see cref="McpCodeGraphServerOptions"/> auch direkt via
    /// <c>new McpCodeGraphServerOptions { ... }</c> konstruieren, sobald der
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
            ResolvedConfigPath = p.ResolvedConfigPath,
            ReadOnlySolutionSnapshot = p.ReadOnlySolutionSnapshot,
            AssemblySymbolIdentity = p.AssemblySymbolIdentity,
        };
    }
}

/// <summary>
/// Parameter-Record fuer <see cref="McpCodeGraphServerOptions.From"/>. Bündelt die
/// Konfigurations-Eingaenge in einem Record, damit <c>MaxMethodParameterCount: 4</c>
/// (siehe <c>AiNetLinter.mdc</c>) fuer die Factory eingehalten wird und kuenftige
/// Properties additiv wachsen koennen.
/// </summary>
internal sealed record McpCodeGraphServerOptionsFromParameters(
    SourceFileCatalog? Catalog,
    ILintConsole? Console = null,
    int MaxLineCount = 700,
    Config? Config = null,
    bool UsedDefaultConfig = false,
    string? ResolvedConfigPath = null,
    Solution? ReadOnlySolutionSnapshot = null,
    AnalysisSymbolIdentity? AssemblySymbolIdentity = null);
