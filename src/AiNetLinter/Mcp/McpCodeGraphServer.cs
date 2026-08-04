#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using AiNetLinter.Baseline;
using AiNetLinter.Configuration;
using AiNetLinter.Output;
using Microsoft.CodeAnalysis;

namespace AiNetLinter.Mcp;

/// <summary>
/// Haelt die geladene Solution ueber die Laufzeit des MCP-Servers resident und prueft lazy
/// (bei jedem <see cref="GetCurrentSolution"/>-Aufruf) per Hash/mtime, ob bekannte Quelldateien
/// seit dem letzten Zugriff auf der Platte geaendert wurden. Betroffene Dokumente werden
/// inkrementell ueber <see cref="SourceFileCatalog.WithUpdatedSolution"/> aktualisiert, kein
/// Komplett-Reload der <see cref="Microsoft.CodeAnalysis.MSBuild.MSBuildWorkspace"/>. Die
/// eigentliche Refresh-Logik (geloeschte Dateien raus, neue Dateien rein, modifizierte Dateien
/// aktualisieren) liegt in <see cref="McpCodeGraphServerRefresh"/>, damit diese Klasse unter
/// dem projektweiten <c>MaxAIContextFootprint</c>-Limit bleibt.
/// </summary>
internal sealed class McpCodeGraphServer : IDisposable
{
    private readonly Lock _lock = new();
    private readonly ILintConsole _console;
    private readonly Dictionary<string, McpFileState> _fileState = new(StringComparer.OrdinalIgnoreCase);
    private SourceFileCatalog? _catalog;

    // Input-Record als Parameter-Object, damit MaxConstructorDependencies: 5 eingehalten wird
    // und kuenftige Config-Properties additiv wachsen koennen, ohne die Konstruktor-Signatur
    // zu aendern.
    public McpCodeGraphServer(McpCodeGraphServerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _catalog = options.Catalog;
        _console = options.Console;
        MaxLineCount = options.MaxLineCount;
        Config = options.Config;
        UsedDefaultConfig = options.UsedDefaultConfig;

        if (_catalog is not null)
        {
            InitializeFileState(_catalog.Solution);
        }
    }

    public bool IsLoaded => _catalog is not null;

    /// <summary>
    /// Zeilen-Grenzwert (aus <c>rules.json</c>/<see cref="AiNetLinter.Configuration.MetricsConfig"/>-Default),
    /// gegen den <c>get_hotspots</c> Dateien der residenten Solution klassifiziert. Pro Server-Session
    /// fest, nicht pro Tool-Call — die Config aendert sich zur Laufzeit nicht.
    /// </summary>
    public int MaxLineCount { get; }

    /// <summary>
    /// Vollstaendige Linter-Konfiguration (aus <c>rules.json</c> via <c>--config</c> geladen, sonst
    /// <see cref="Config"/>-Default). Benoetigt von Tools, die regelbasiert arbeiten (z. B.
    /// <c>get_violations</c> fuer <see cref="Core.LinterEngine"/>-Konstruktion und
    /// PathOverrides). Nie <see langword="null"/> — der Konstruktor normalisiert mit <c>?? new Config()</c>.
    /// Exposed als schmale Lese-Sicht (<see cref="ILinterEngineConfig"/>), damit Tool-Klassen
    /// nur die Properties ins Footprint ziehen, die sie auch tatsaechlich konsumieren.
    /// </summary>
    public ILinterEngineConfig Config { get; }

    /// <summary>
    /// True, wenn der Server ohne <c>--config</c> gestartet wurde und auch keine
    /// <c>rules.json</c> neben der aufgeloesten Solution-Datei gefunden wurde — der Server
    /// laeuft in diesem Fall mit der <see cref="Config"/>-Default-Konfiguration.
    /// <c>get_violations</c> zeigt in diesem Fall eine sichtbare Header-Zeile an, damit der
    /// Agent-LLM erkennt, dass die Lint-Ergebnisse nicht aus der projekteigenen
    /// <c>rules.json</c> stammen.
    /// </summary>
    public bool UsedDefaultConfig { get; }

    /// <summary>
    /// Konsolen-Kanal, an den der MCP-Server selbst loggt. Wird von <c>get_violations</c> an
    /// <see cref="Core.LinterEngine"/> weitergereicht, damit Lint-Warnungen auf demselben Kanal
    /// landen wie die uebrigen MCP-Server-Logs (nicht auf stdout, wo sie mit dem stdio-MCP-Verkehr
    /// kollidieren wuerden).
    /// </summary>
    public ILintConsole Console => _console;

    /// <summary>
    /// Liefert die aktuelle, ggf. lazy aktualisierte <see cref="Solution"/> — <see langword="null"/>,
    /// wenn beim Start keine Solution geladen werden konnte.
    /// </summary>
    public Solution? GetCurrentSolution()
    {
        lock (_lock)
        {
            if (_catalog is null) return null;

            RefreshStaleDocuments();
            return _catalog.Solution;
        }
    }

    public void Dispose() => _catalog?.Dispose();

    private void InitializeFileState(Solution solution)
    {
        var solutionDir = Path.GetDirectoryName(solution.FilePath);

        foreach (var project in solution.Projects)
        {
            foreach (var document in project.Documents)
            {
                if (!SourceFileCatalog.IsValidDocument(document, solutionDir)) continue;
                McpCodeGraphServerRefresh.CacheInitialFileState(document.FilePath!, _fileState, _console.WriteError);
            }
        }
    }

    private void RefreshStaleDocuments()
    {
        var (updated, anyChanged) = McpCodeGraphServerRefresh.Run(
            _catalog!.Solution,
            Path.GetDirectoryName(_catalog.Solution.FilePath),
            _fileState,
            _console.WriteError);

        if (anyChanged)
        {
            _catalog = _catalog.WithUpdatedSolution(updated);
        }
    }
}
