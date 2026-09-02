#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Baseline;
using AiNetLinter.Configuration;
using AiNetLinter.Mcp.Assemblies.Analysis.References;
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
internal sealed class McpCodeGraphServer : ISolutionStateProvider, IDisposable, IAsyncDisposable
{
    private readonly Lock _lock = new();
    private readonly ILintConsole _console;
    private readonly Dictionary<string, McpFileState> _fileState = new(StringComparer.OrdinalIgnoreCase);
    private readonly Task<SourceFileCatalog?>? _loadTask;
    private readonly Func<CancellationToken, Task<SourceFileCatalog?>>? _loadFunc;
    private readonly CancellationTokenSource _loadCancellation = new();
    private readonly DateTime _startedAtUtc = DateTime.UtcNow;
    private SourceFileCatalog? _catalog;
    private DateTime? _lastSolutionDirMtimeUtc;
    private int _refreshCount;
    private long _stalenessCheckCount;
    private double _stalenessCheckTotalMs;
    private int _lastStalenessWarningCount;
    private string? _lastStalenessWarning;
    private DateTime? _lastGoodStateUtc;
    private string? _lastRefreshError;
    private int _disposed;
    private readonly bool _isReadOnlySnapshot;
    private readonly AnalysisSymbolIdentity? _assemblySymbolIdentity;

    // Input-Record als Parameter-Object, damit MaxConstructorDependencies: 5 eingehalten wird
    // und kuenftige Config-Properties additiv wachsen koennen, ohne die Konstruktor-Signatur
    // zu aendern.
    public McpCodeGraphServer(McpCodeGraphServerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _console = options.Console;
        MaxLineCount = options.MaxLineCount;
        Config = options.Config;
        UsedDefaultConfig = options.UsedDefaultConfig;
        ResolvedConfigPath = options.ResolvedConfigPath;
        _assemblySymbolIdentity = options.AssemblySymbolIdentity;
        if (options.ReadOnlySolutionSnapshot is not null && (options.Catalog is not null || options.LoadFunc is not null))
        {
            throw new ArgumentException("ReadOnlySolutionSnapshot kann nicht mit Catalog oder LoadFunc kombiniert werden.");
        }

        if (options.ReadOnlySolutionSnapshot is { } snapshot)
        {
            _catalog = new SourceFileCatalog(snapshot, hasLoadingErrors: false);
            _isReadOnlySnapshot = true;
            _lastGoodStateUtc = DateTime.UtcNow;
        }
        else if (options.Catalog is { } catalog)
        {
            _loadFunc = options.LoadFunc;
            _catalog = catalog;
            InitializeFileState(catalog.Solution);
            _lastGoodStateUtc = DateTime.UtcNow;
        }
        else if (options.LoadFunc is { } loadFunc)
        {
            _loadFunc = loadFunc;
            // Hintergrund-Load: der Server startet sofort, der Tool-Dispatch sieht
            // solange LoadState == Loading und antwortet mit McpToolResults.Loading().
            _loadTask = Task.Run(() => loadFunc(_loadCancellation.Token));
        }
    }

    /// <summary>True, sobald eine Solution resident verfuegbar ist (synchroner Konstruktorpfad
    /// oder adoptierter Hintergrund-Load mit nicht-null-Resultat).</summary>
    public bool IsLoaded => _catalog is not null;

    /// <summary>Drei-Zustands-Lebenszyklus des Solution-Loads. Wird aus <c>_loadTask</c> und
    /// <c>_catalog</c> abgeleitet — kein eigener State, damit es keine Drift zwischen
    /// Load-Task und Katalog geben kann. Ein erfolgreicher Load mit null-Resultat zaehlt
    /// als <see cref="ServerLoadState.LoadFailed"/>, weil kein abfragbarer Solution-Zustand
    /// zur Verfuegung steht.</summary>
    public ServerLoadState LoadState => _loadTask switch
    {
        null => _catalog is null ? ServerLoadState.LoadFailed : ServerLoadState.Loaded,
        // ainetlinter-disable BanBlockingTaskAccess — IsCompletedSuccessfully: true garantiert, dass GetAwaiter().GetResult() nicht blockiert; ohne diesen Peek meldet LoadState fälschlich LoadFailed vor dem ersten GetCurrentSolution()-Aufruf.
        { IsCompletedSuccessfully: true } => (_catalog ?? _loadTask.GetAwaiter().GetResult()) is null ? ServerLoadState.LoadFailed : ServerLoadState.Loaded,
        { IsFaulted: true } => ServerLoadState.LoadFailed,
        { IsCanceled: true } => ServerLoadState.LoadFailed,
        _ => ServerLoadState.Loading,
    };

    internal Task<SourceFileCatalog?>? LoadTask => _loadTask;
    /// <summary>Zeilen-Grenzwert aus <c>rules.json</c> bzw. <see cref="MetricsConfig"/>-Default.</summary>
    public int MaxLineCount { get; }

    /// <summary>Vollstaendige Linter-Konfiguration (aus <c>rules.json</c> via <c>--config</c> oder Default).
    /// Privates Setter statt <see langword="init"/>, weil <see cref="ReloadConfig"/> diese zur
    /// Laufzeit austauscht (<c>reload_config</c>-Tool). Isolierter Zugriff auf NUR dieses
    /// Property ist unkritisch; zusammen mit <see cref="UsedDefaultConfig"/>/<see cref="ResolvedConfigPath"/>
    /// immer <see cref="GetConfigSnapshot"/> nutzen (Begruendung dort).</summary>
    public ILinterEngineConfig Config { get; private set; }

    /// <summary>True, wenn der Server mit der Config-Default-Konfiguration laeuft (kein <c>rules.json</c> gefunden).
    /// Kombinierte Lesezugriffe: siehe <see cref="GetConfigSnapshot"/>.</summary>
    public bool UsedDefaultConfig { get; private set; }

    /// <summary>Pfad der tatsaechlich geladenen <c>rules.json</c>, oder <see langword="null"/> wenn
    /// <see cref="UsedDefaultConfig"/> <see langword="true"/> ist. Kombinierte Lesezugriffe: siehe
    /// <see cref="GetConfigSnapshot"/>.</summary>
    public string? ResolvedConfigPath { get; private set; }

    /// <summary>Konsolen-Kanal, an den der MCP-Server selbst loggt.</summary>
    public ILintConsole Console => _console;

    internal AnalysisSymbolIdentity? AssemblySymbolIdentity => _assemblySymbolIdentity;
    AnalysisSymbolIdentity? ISolutionStateProvider.AssemblySymbolIdentity => AssemblySymbolIdentity;

    /// <summary>Zeit seit Konstruktion dieser Instanz — Proxy fuer die Server-Uptime, verwendet von
    /// <c>get_server_health</c>.</summary>
    public TimeSpan Uptime => DateTime.UtcNow - _startedAtUtc;

    /// <summary>Anzahl der <see cref="GetCurrentSolution"/>-Aufrufe seit Start, bei denen
    /// <see cref="RefreshStaleDocuments"/> tatsaechlich eine Aenderung (neue/geloeschte/modifizierte
    /// Datei) in die resident gehaltene <see cref="Solution"/> uebernommen hat. Verwendet von
    /// <c>get_server_health</c> als Signal, wie oft der Staleness-Check seit Start gegriffen hat.
    /// Unter <see cref="_lock"/> gelesen, konsistent mit dem uebrigen Zugriffsmuster auf
    /// <see cref="_catalog"/>/<see cref="_fileState"/> in dieser Klasse.</summary>
    public int RefreshCount { get { lock (_lock) { return _refreshCount; } } }

    /// <summary>Diagnose-Schnappschuss des Staleness-Subsystems: Check-Anzahl und kumulierte
    /// Dauer (Konzept 02, c — Evidenzbasis fuer Kosten/Frequenz) sowie Warnungszähler und
    /// letzte Warnmeldung fuer unzugängliche Teilbäume (Konzept 02, C — die Warnung kippt den
    /// Zustand bewusst NICHT dauerhaft auf "geändert"). Als ein Record statt vier einzelner
    /// Properties, damit die oeffentliche API-Oberflaeche unter MaxPublicMembersPerType bleibt.
    /// Konsumiert von <c>get_server_health</c>. Unter <see cref="_lock"/> gelesen, konsistent
    /// mit dem uebrigen Zugriffsmuster dieser Klasse.</summary>
    internal ServerStalenessStats LastStalenessStats
    {
        get { lock (_lock) { return new ServerStalenessStats(_stalenessCheckCount, _stalenessCheckTotalMs, _lastStalenessWarningCount, _lastStalenessWarning); } }
    }

    /// <summary>Zeitpunkt, an dem der zuletzt resident gute Solution-Zustand entstanden ist
    /// (erfolgreicher Kalt-Load, adoptierter Hintergrund-Load oder erfolgreicher Refresh).
    /// <see langword="null"/>, solange kein guter Zustand existiert. Zweistufiger Fehlervertrag:
    /// Analyse-Antworten basieren nach einem fehlgeschlagenen Refresh weiterhin auf diesem Stand.</summary>
    public DateTime? LastGoodStateUtc { get { lock (_lock) { return _lastGoodStateUtc; } } }

    /// <summary>Ursprungsmeldung des letzten Ladefehlers: Fault des Kalt-Loads (lazy aus
    /// <see cref="_loadTask"/> abgeleitet) oder fehlgeschlagener Reload/Sweep. Ein Erfolg heilt:
    /// erfolgreicher Load/Refresh setzt den Wert zurueck.</summary>
    public string? LastLoadError
    {
        get
        {
            lock (_lock)
            {
                if (_lastRefreshError is not null) return _lastRefreshError;
                return _loadTask switch
                {
                    { IsFaulted: true } task => task.Exception?.InnerException?.Message ?? task.Exception?.Message,
                    { IsCanceled: true } => "Hintergrund-Load wurde abgebrochen.",
                    _ => null,
                };
            }
        }
    }

    /// <summary>True, waehrend ein geladener Zustand durch einen fehlgeschlagenen Refresh
    /// ueberschattet wird — Antworten auf dieser Instanz tragen einen [WARN]-Kopf, bis ein
    /// erfolgreicher Refresh heilt.</summary>
    internal bool HasDegradedAnswerState
    {
        get { lock (_lock) { return _lastRefreshError is not null && _catalog is not null; } }
    }

    /// <summary>
    /// Ersetzt die resident gehaltene Config-Instanz zur Laufzeit (<c>reload_config</c>-Tool).
    /// <paramref name="newConfig"/> ist bereits erfolgreich geladen/validiert. Unter <see cref="_lock"/>
    /// wie <see cref="GetConfigSnapshot"/> — NICHT weil das <see cref="GetCurrentSolution"/> schuetzt
    /// (der liest Config gar nicht), sondern damit Snapshot-Leser nie eine halb ausgetauschte
    /// Kombination der drei Felder sehen.
    /// </summary>
    internal void ReloadConfig(ILinterEngineConfig newConfig, bool usedDefaultConfig, string? resolvedConfigPath)
    {
        lock (_lock)
        {
            Config = newConfig;
            UsedDefaultConfig = usedDefaultConfig;
            ResolvedConfigPath = resolvedConfigPath;
        }
    }

    /// <summary>
    /// Laedt die resident gehaltene Solution und ihren <see cref="SourceFileCatalog"/> asynchron neu
    /// (z. B. nach <c>dotnet restore</c> bei neu hinzugefuegten NuGet-Abhaengigkeiten im Zuge von
    /// <c>reload_config</c>). Aktualisiert alle Projekt- und Metadaten-Referenzen der Solution.
    /// </summary>
    internal async Task<bool> ReloadSolutionAsync(CancellationToken ct = default)
    {
        string? solutionPath;
        lock (_lock)
        {
            if (_disposed != 0 || _isReadOnlySnapshot || _catalog is null) return false;
            solutionPath = _catalog.Solution.FilePath;
        }

        if (_loadFunc is null && (string.IsNullOrEmpty(solutionPath) || !File.Exists(solutionPath)))
        {
            return false;
        }

        try
        {
            var newCatalog = _loadFunc is not null
                ? await _loadFunc(ct).ConfigureAwait(false)
                : await SourceFileCatalog.LoadAsync(solutionPath!, ct).ConfigureAwait(false);
            if (newCatalog is null)
            {
                throw new InvalidOperationException("Solution-Reload lieferte keinen Katalog.");
            }

            lock (_lock)
            {
                var oldCatalog = _catalog;
                _catalog = newCatalog;
                _fileState.Clear();
                InitializeFileState(newCatalog.Solution);
                oldCatalog?.Dispose();
                _refreshCount++;
                _lastGoodStateUtc = DateTime.UtcNow;
                _lastRefreshError = null;
            }
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            lock (_lock)
            {
                _lastRefreshError = ex.Message;
            }

            _console.WriteError($"[WARN]: Solution konnte beim Reload nicht neu geladen werden ({solutionPath ?? "unbekannter Pfad"}): {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Atomarer Schnappschuss von <see cref="Config"/>/<see cref="UsedDefaultConfig"/>/
    /// <see cref="ResolvedConfigPath"/> unter <see cref="_lock"/>. Pflicht fuer jeden Aufrufer, der
    /// mehr als eines der drei Felder zusammen braucht — sonst kann ein gleichzeitiger
    /// <see cref="ReloadConfig"/>-Aufruf eine zerrissene Kombination liefern.
    /// </summary>
    internal (ILinterEngineConfig Config, bool UsedDefaultConfig, string? ResolvedConfigPath) GetConfigSnapshot()
    {
        lock (_lock)
        {
            return (Config, UsedDefaultConfig, ResolvedConfigPath);
        }
    }

    (ILinterEngineConfig Config, bool UsedDefaultConfig, string? ResolvedConfigPath)
        ISolutionStateProvider.GetConfigSnapshot() => GetConfigSnapshot();

    /// <summary>Liefert die aktuelle <see cref="Solution"/> oder <see langword="null"/>, wenn der
    /// Server noch laedt (<see cref="LoadState"/> == <see cref="ServerLoadState.Loading"/>) oder
    /// der Load fehlgeschlagen ist. Aufrufer pruefen idealerweise <see cref="LoadState"/> zuerst,
    /// um den Loading-Zustand sauber von "kein Catalog" zu unterscheiden.</summary>
    public Solution? GetCurrentSolution()
    {
        lock (_lock)
        {
            if (_disposed != 0) return null;

            if (_catalog is null && _loadTask is not null)
            {
                // ainetlinter-disable BanBlockingTaskAccess — die sync-Methode darf im
                // Tool-Dispatch-Pfad nicht async werden; einmaliges Adoptieren des
                // Load-Resultats ist der definierte Punkt, an dem die Blockierung
                // hingehört.
                if (_loadTask.IsCompletedSuccessfully
                    && _loadTask.GetAwaiter().GetResult() is { } loaded)
                {
                    _catalog = loaded;
                    InitializeFileState(loaded.Solution);
                    _lastGoodStateUtc = DateTime.UtcNow;
                }
                else
                {
                    return null;
                }
            }

            if (_catalog is null) return null;
            if (_isReadOnlySnapshot) return _catalog.Solution;
            RefreshStaleDocuments();
            return _catalog.Solution;
        }
    }

    public void Dispose()
    {
        DisposeAsync().AsTask().GetAwaiter().GetResult();
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

        _loadCancellation.Cancel();
        SourceFileCatalog? loadedCatalog = null;
        try
        {
            if (_loadTask is not null)
            {
                try
                {
                    loadedCatalog = await _loadTask.ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (_loadCancellation.IsCancellationRequested)
                {
                }
                catch (Exception exception)
                {
                    _console.WriteError($"[WARN]: Hintergrund-Load konnte beim MCP-Shutdown nicht abgeschlossen werden: {exception.Message}");
                }
            }

            SourceFileCatalog? catalog;
            lock (_lock)
            {
                catalog = _catalog;
                _catalog = null;
            }

            catalog?.Dispose();
            if (loadedCatalog is not null && !ReferenceEquals(loadedCatalog, catalog))
            {
                loadedCatalog.Dispose();
            }
        }
        finally
        {
            _loadCancellation.Dispose();
        }
    }

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
        // Baseline ueber dieselbe Walk-Grenze wie der spaetere Vergleich
        // (Projektverzeichnis-Vereinigung, siehe HasSolutionDirChanged) — abweichende
        // Grenzen wuerden eine permanente Schein-Aenderung erzeugen.
        if (!string.IsNullOrEmpty(solutionDir) && Directory.Exists(solutionDir))
        {
            _lastSolutionDirMtimeUtc = ComputeStalenessMtime(solution);
        }
    }

    private void RefreshStaleDocuments()
    {
        _stalenessCheckCount++;
        var startTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
        try
        {
            var solution = _catalog!.Solution;
            var (updated, anyChanged) = McpCodeGraphServerRefresh.Run(
                solution,
                Path.GetDirectoryName(solution.FilePath),
                new McpCodeGraphServerRefreshParameters(
                    FileState: _fileState,
                    WriteWarn: _console.WriteError,
                    ShouldSweep: () => HasSolutionDirChanged(solution)));
            if (anyChanged)
            {
                _catalog = _catalog.WithUpdatedSolution(updated);
                _refreshCount++;
                _lastGoodStateUtc = DateTime.UtcNow;
                _lastRefreshError = null;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Zweistufiger Fehlervertrag: der letzte gute Stand bleibt resident, die Analyse
            // laeuft weiter; Antworten auf dieser Instanz tragen bis zur Heilung einen
            // [WARN]-Kopf (siehe HasDegradedAnswerState).
            _lastRefreshError = ex.Message;
            _console.WriteError($"[WARN]: Inkrementeller Refresh fehlgeschlagen, letzter guter Stand bleibt aktiv: {ex.Message}");
        }
        finally
        {
            _stalenessCheckTotalMs += System.Diagnostics.Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;
        }
    }

    /// <summary>Vergleicht die maximale Verzeichnis-mtime ueber die Sweep-Wurzeln
    /// (Projektverzeichnis-Vereinigung, siehe <see cref="McpCodeGraphServerRefresh.GetSweepRoots"/>)
    /// mit dem letzten Stand. Eine reine Root-mtime-Pruefung reicht auf Windows nicht, weil das
    /// Root-mtime nur bei Aenderungen an der Root-Ebene selbst aktualisiert wird. Baseline
    /// (siehe <see cref="InitializeFileState"/>) und Vergleich nutzen dieselbe Walk-Grenze —
    /// sonst wuerde jede Grenz-Aenderung eine permanente Schein-Aenderung erzeugen.</summary>
    private bool HasSolutionDirChanged(Solution solution)
    {
        var current = ComputeStalenessMtime(solution);
        if (_lastSolutionDirMtimeUtc == current) return false;
        _lastSolutionDirMtimeUtc = current;
        return true;
    }

    /// <summary>Berechnet die maximale Verzeichnis-mtime ueber die Sweep-Wurzeln via
    /// <see cref="FileSystemExclusionHelpers.WalkFilteredTree"/> — mit Reparse-Point-Schutz,
    /// Namens-Ausschluessen und Fehlerzaehler statt Abbruch. Unzugängliche Teilbäume werden
    /// als Warnung vermerkt (Health-Metadaten), kippen aber nicht dauerhaft auf "geändert".</summary>
    private DateTime ComputeStalenessMtime(Solution solution)
    {
        var max = DateTime.MinValue;
        var stats = FileSystemExclusionHelpers.WalkFilteredTree(
            McpCodeGraphServerRefresh.GetSweepRoots(solution, Path.GetDirectoryName(solution.FilePath)),
            filePattern: null,
            visitDirectory: directory =>
            {
                var mtime = Directory.GetLastWriteTimeUtc(directory);
                if (mtime > max) max = mtime;
            },
            visitFile: null);
        _lastStalenessWarningCount = stats.InaccessibleSubtreeCount;
        _lastStalenessWarning = stats.Warnings.Count > 0 ? stats.Warnings[^1] : null;
        return max;
    }
}
