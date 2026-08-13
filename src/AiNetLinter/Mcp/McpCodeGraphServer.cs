#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
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
    private readonly Task<SourceFileCatalog?>? _loadTask;
    private readonly DateTime _startedAtUtc = DateTime.UtcNow;
    private SourceFileCatalog? _catalog;
    private DateTime? _lastSolutionDirMtimeUtc;
    private int _refreshCount;
    private readonly bool _isReadOnlySnapshot;

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
        if (options.ReadOnlySolutionSnapshot is not null && (options.Catalog is not null || options.LoadFunc is not null))
        {
            throw new ArgumentException("ReadOnlySolutionSnapshot kann nicht mit Catalog oder LoadFunc kombiniert werden.");
        }

        if (options.ReadOnlySolutionSnapshot is { } snapshot)
        {
            _catalog = new SourceFileCatalog(snapshot, hasLoadingErrors: false);
            _isReadOnlySnapshot = true;
        }
        else if (options.LoadFunc is { } loadFunc)
        {
            // Hintergrund-Load: der Server startet sofort, der Tool-Dispatch sieht
            // solange LoadState == Loading und antwortet mit McpToolResults.Loading().
            _loadTask = Task.Run(() => loadFunc(CancellationToken.None));
        }
        else if (options.Catalog is { } catalog)
        {
            _catalog = catalog;
            InitializeFileState(catalog.Solution);
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

    /// <summary>Liefert die aktuelle <see cref="Solution"/> oder <see langword="null"/>, wenn der
    /// Server noch laedt (<see cref="LoadState"/> == <see cref="ServerLoadState.Loading"/>) oder
    /// der Load fehlgeschlagen ist. Aufrufer pruefen idealerweise <see cref="LoadState"/> zuerst,
    /// um den Loading-Zustand sauber von "kein Catalog" zu unterscheiden.</summary>
    public Solution? GetCurrentSolution()
    {
        lock (_lock)
        {
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
        if (_loadTask is { IsCompleted: false })
        {
            // ainetlinter-disable BanBlockingTaskAccess — Dispose darf den Server-Thread nicht
            // blockieren, aber ein laufender Hintergrund-Load muss vor Catalog-Dispose
            // abgeschlossen sein, sonst laeuft der Load-Thread in einen disposed Workspace.
            try { _loadTask.Wait(TimeSpan.FromSeconds(2)); }
            // ainetlinter-disable EnforceNoSilentCatch — Server faehrt herunter, das
            // Load-Ergebnis koennen wir nicht mehr verwenden, eine separate Fehlerausgabe
            // ist nicht noetig, weil der Server sowieso terminiert.
            catch (AggregateException) { }
        }
        _catalog?.Dispose();
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
        if (!string.IsNullOrEmpty(solutionDir) && Directory.Exists(solutionDir))
        {
            try { _lastSolutionDirMtimeUtc = ComputeMaxDirMtimeUtc(solutionDir); }
            catch (IOException) { _lastSolutionDirMtimeUtc = null; }
            catch (UnauthorizedAccessException) { _lastSolutionDirMtimeUtc = null; }
        }
    }

    private void RefreshStaleDocuments()
    {
        var (updated, anyChanged) = McpCodeGraphServerRefresh.Run(
            _catalog!.Solution,
            Path.GetDirectoryName(_catalog.Solution.FilePath),
            new McpCodeGraphServerRefreshParameters(
                FileState: _fileState,
                WriteWarn: _console.WriteError,
                ShouldSweep: () => HasSolutionDirChanged(Path.GetDirectoryName(_catalog!.Solution.FilePath))));
        if (anyChanged)
        {
            _catalog = _catalog.WithUpdatedSolution(updated);
            _refreshCount++;
        }
    }

    /// <summary>Vergleicht die maximale mtime ueber alle Verzeichnisse unterhalb <paramref name="solutionDir"/>
    /// mit dem letzten Stand. Eine reine Root-mtime-Pruefung reicht auf Windows nicht, weil das
    /// Root-mtime nur bei Aenderungen an der Root-Ebene selbst aktualisiert wird. Max-Aggregation
    /// bleibt O(n_dirs) und damit deutlich guenstiger als der vollstaendige Datei-Walk in Phase 2.</summary>
    private bool HasSolutionDirChanged(string? solutionDir)
    {
        if (string.IsNullOrEmpty(solutionDir)) return false;
        DateTime current;
        try { current = ComputeMaxDirMtimeUtc(solutionDir); }
        catch (IOException) { return true; }
        catch (UnauthorizedAccessException) { return true; }
        if (_lastSolutionDirMtimeUtc == current) return false;
        _lastSolutionDirMtimeUtc = current;
        return true;
    }

    private static DateTime ComputeMaxDirMtimeUtc(string solutionDir)
    {
        var max = Directory.GetLastWriteTimeUtc(solutionDir);
        foreach (var dir in Directory.EnumerateDirectories(solutionDir, "*", SearchOption.AllDirectories))
        {
            try
            {
                var m = Directory.GetLastWriteTimeUtc(dir);
                if (m > max) max = m;
            }
            // ainetlinter-disable EnforceNoSilentCatch — einzelne Subdirectories koennen
            // unzugaenglich sein (gelockt, geloescht), die uebrigen mtimes sind trotzdem
            // ein brauchbarer Cache-Hinweis.
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        return max;
    }
}
