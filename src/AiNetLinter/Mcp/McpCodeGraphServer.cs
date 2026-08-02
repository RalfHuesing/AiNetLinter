#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using AiNetLinter.Baseline;
using AiNetLinter.Configuration;
using AiNetLinter.Output;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace AiNetLinter.Mcp;

/// <summary>
/// Haelt die geladene Solution ueber die Laufzeit des MCP-Servers resident und prueft lazy
/// (bei jedem <see cref="GetCurrentSolution"/>-Aufruf) per Hash/mtime, ob bekannte Quelldateien
/// seit dem letzten Zugriff auf der Platte geaendert wurden. Betroffene Dokumente werden
/// inkrementell ueber <see cref="SourceFileCatalog.WithUpdatedSolution"/> aktualisiert, kein
/// Komplett-Reload der <see cref="Microsoft.CodeAnalysis.MSBuild.MSBuildWorkspace"/>.
/// </summary>
internal sealed class McpCodeGraphServer : IDisposable
{
    private readonly Lock _lock = new();
    private readonly ILintConsole _console;
    private readonly Dictionary<string, FileState> _fileState = new(StringComparer.OrdinalIgnoreCase);
    private SourceFileCatalog? _catalog;

    // Eingefuehrt mit
    // der am projektweiten MaxConstructorDependencies: 5-Limit lag 
    // und McpCodeGraphServerOptions.cs). Erlaubt additive P0/P1-Erweiterungen an der Config,
    // ohne die Konstruktor-Signatur zu aendern.
    public McpCodeGraphServer(McpCodeGraphServerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _catalog = options.Catalog;
        _console = options.Console;
        MaxLineCount = options.MaxLineCount;
        Config = options.Config;

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
    /// </summary>
    public Config Config { get; }

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
                TryCacheInitialFileState(document.FilePath!);
            }
        }
    }

    private void TryCacheInitialFileState(string path)
    {
        if (!File.Exists(path)) return;

        try
        {
            var mtime = File.GetLastWriteTimeUtc(path);
            var hash = FileChecksumCalculator.ComputeSha256Hex(path);
            _fileState[path] = new FileState(mtime, hash);
        }
        catch (IOException ex)
        {
            _console.WriteError($"[WARN]: Datei konnte beim MCP-Server-Start nicht gehasht werden ({path}): {ex.Message}");
        }
    }

    private void RefreshStaleDocuments()
    {
        var solutionDir = Path.GetDirectoryName(_catalog!.Solution.FilePath);
        var updated = _catalog.Solution;
        var anyChanged = false;

        foreach (var project in _catalog.Solution.Projects)
        {
            foreach (var document in project.Documents)
            {
                if (!SourceFileCatalog.IsValidDocument(document, solutionDir)) continue;
                if (TryRefreshDocument(document, ref updated)) anyChanged = true;
            }
        }

        if (anyChanged)
        {
            _catalog = _catalog.WithUpdatedSolution(updated);
        }
    }

    private bool TryRefreshDocument(Document document, ref Solution updated)
    {
        var path = document.FilePath!;
        if (!File.Exists(path)) return false;

        var currentMtime = File.GetLastWriteTimeUtc(path);
        if (_fileState.TryGetValue(path, out var known) && known.MtimeUtc == currentMtime)
        {
            return false;
        }

        return TryApplyContentChange(document, path, currentMtime, known, ref updated);
    }

    private bool TryApplyContentChange(
        Document document,
        string path,
        DateTime currentMtime,
        FileState known,
        ref Solution updated)
    {
        try
        {
            var currentHash = FileChecksumCalculator.ComputeSha256Hex(path);
            if (currentHash == known.Hash)
            {
                _fileState[path] = known with { MtimeUtc = currentMtime };
                return false;
            }

            var text = File.ReadAllText(path);
            updated = updated.WithDocumentText(document.Id, SourceText.From(text));
            _fileState[path] = new FileState(currentMtime, currentHash);
            return true;
        }
        catch (IOException ex)
        {
            _console.WriteError($"[WARN]: Datei konnte beim Staleness-Check nicht gelesen werden ({path}): {ex.Message}");
            return false;
        }
    }

    private readonly record struct FileState(DateTime MtimeUtc, string Hash);
}
