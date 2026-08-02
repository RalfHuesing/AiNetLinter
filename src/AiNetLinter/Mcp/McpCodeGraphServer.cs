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

internal sealed class McpCodeGraphServer : IDisposable
{
    private readonly Lock _lock = new();
    private readonly ILintConsole _console;
    private readonly Dictionary<string, FileState> _fileState = new(StringComparer.OrdinalIgnoreCase);
    private SourceFileCatalog? _catalog;

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
