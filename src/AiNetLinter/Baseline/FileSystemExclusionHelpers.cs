#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace AiNetLinter.Baseline;

/// <summary>
/// Projekteinheitliche Dateisystem-Ausschlussmuster fuer freie Walk-Scans (ohne Roslyn).
/// Wird von <see cref="WebFileCatalog"/> und <see cref="AiNetLinter.Mcp.Tools.GetIndexScopeScanner"/>
/// konsumiert, damit die 1:1-Duplikation der Methoden aufgelöst wird und kuenftige
/// Dateisystem-Scans die gleichen Exclusions ohne erneute Implementierung erhalten.
/// Bewusst nicht fuer Roslyn-Walks gedacht (dort filtert <see cref="SourceFileCatalog.IsGeneratedDocument"/>);
/// nur fuer Loesungen, in denen Roslyn den Dateityp nicht sieht (.css/.js/.razor/.xaml/.html).
/// </summary>
internal static class FileSystemExclusionHelpers
{
    /// <summary>
    /// Enumeriert alle Dateien unterhalb <paramref name="directory"/> rekursiv. Schluckt
    /// <see cref="UnauthorizedAccessException"/> und <see cref="IOException"/> (z. B. gesperrte
    /// oder geloeschte Subdirectories), damit ein einzelner unzugaenglicher Ast den gesamten
    /// Walk nicht abbricht — Aufrufer bekommen stattdessen die erreichbaren Dateien.
    /// </summary>
    internal static IEnumerable<string> SafeEnumerateFiles(string directory)
        => SafeEnumerateFilesWithErrors(directory).Files;

    internal static FileSystemEnumerationResult SafeEnumerateFilesWithErrors(
        string directory,
        CancellationToken cancellationToken = default)
    {
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = false,
            AttributesToSkip = FileAttributes.ReparsePoint,
        };
        var errorCount = 0;
        return new FileSystemEnumerationResult(
            EnumerateFiles(directory, options, cancellationToken, () => errorCount++),
            () => errorCount);
    }

    internal static readonly string[] SearchExcludedDirectories =
    [
        ".git", ".hg", ".svn", ".vs", ".idea", "obj", "bin", "node_modules",
        "worktrees", ".worktrees", "TestResults", "artifacts", "coverage", "temp", "packages",
    ];

    internal static bool IsSearchExcludedRelativePath(string relativePath)
    {
        var normalized = relativePath.Replace('\\', '/').Trim('/');
        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);

        return segments.Any(segment => SearchExcludedDirectories.Contains(segment, StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>Prueft einen einzelnen Verzeichnis-Segmentnamen gegen die projektweite
    /// Ausschlussliste — vom Staleness-Walk genutzt, um ausgeschlossene Teilbaeume gar nicht
    /// erst zu betreten (statt erst rekursiv zu enumerieren und nachtraeglich zu filtern).</summary>
    internal static bool IsExcludedDirectoryName(string? name) =>
        !string.IsNullOrEmpty(name) && SearchExcludedDirectories.Contains(name, StringComparer.OrdinalIgnoreCase);

    /// <summary>Entscheidet, ob ein Unterverzeichnis betreten werden darf: Reparse Points
    /// (Junctions/Symlinks) werden nie traversiert — ohne diesen Schutz koennen
    /// Junction-/Symlink-Zyklen den Walk endlos laufen lassen oder massiv aufblaehten
    /// (Konzept 02, B). Als Pure Function, damit die Zyklus-Schutz-Entscheidung
    /// deterministisch testbar ist.</summary>
    internal static bool IsTraversableSubDirectory(FileAttributes attributes) =>
        !attributes.HasFlag(FileAttributes.ReparsePoint);


    /// <summary>
    /// Prueft, ob <paramref name="path"/> in einem generierten Verzeichnis oder einem
    /// verschachtelten Git-Worktree liegt (<c>obj/</c>, <c>bin/</c>, <c>node_modules/</c>,
    /// <c>worktrees/</c>, <c>.worktrees/</c> — Worktrees enthalten volle Repo-Kopien und
    /// wuerden sonst Treffer vervielfachen, siehe <see cref="SourceFileCatalog.IsGeneratedPath"/>
    /// fuer denselben Ausschluss auf dem Roslyn-Walk-Pfad). Vergleich ist case-insensitive und
    /// verwendet <see cref="Path.DirectorySeparatorChar"/>, damit sowohl Windows- als auch
    /// forward-slash-Pfade korrekt erkannt werden.
    /// </summary>
    internal static bool IsGeneratedPath(string path)
    {
        var sep = Path.DirectorySeparatorChar;
        return path.Contains($"{sep}obj{sep}", StringComparison.OrdinalIgnoreCase)
            || path.Contains($"{sep}bin{sep}", StringComparison.OrdinalIgnoreCase)
            || path.Contains($"{sep}node_modules{sep}", StringComparison.OrdinalIgnoreCase)
            || path.Contains($"{sep}worktrees{sep}", StringComparison.OrdinalIgnoreCase)
            || path.Contains($"{sep}.worktrees{sep}", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".AssemblyAttributes.cs", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Traversiert die Wurzelverzeichnisse mit konfigurierbarer Tiefe und Cancellation.
    /// Der Options-Einstieg besucht alle Dateien und verwendet die zentralen
    /// Ausschluss- und Reparse-Point-Regeln.
    /// </summary>
    /// <param name="roots">Wurzelverzeichnisse (z. B. Projektverzeichnis-Vereinigung).</param>
    /// <param name="options">Tiefe, Ausschlüsse und Cancellation des Walks.</param>
    /// <param name="visitDirectory">Callback je besuchtem Verzeichnis.</param>
    /// <param name="visitFile">Optional: Callback je gefundenem Dateipfad.</param>
    internal static TreeWalkStats WalkFilteredTree(
        IEnumerable<string> roots,
        FileSystemWalkOptions options,
        Action<string>? visitDirectory,
        Action<string>? visitFile)
        => WalkFilteredTreeCore(roots, options, "*", (visitDirectory, visitFile));

    /// <summary>
    /// Rekursive Tiefen-Traversierung der Wurzelverzeichnisse unter den projektweiten
    /// Suchregeln mit dem bestehenden Datei-Pattern-Vertrag.
    /// </summary>
    /// <param name="roots">Wurzelverzeichnisse (z. B. Projektverzeichnis-Vereinigung).</param>
    /// <param name="filePattern">Optional: Suchpattern für Datei-Besuche (z. B. <c>*.cs</c>);
    /// <see langword="null"/> besucht nur Verzeichnisse.</param>
    /// <param name="visitDirectory">Callback je besuchtem Verzeichnis (inkl. Wurzeln). Eine
    /// IOException/UnauthorizedAccessException im Callback zählt als Warnung für den Knoten.</param>
    /// <param name="visitFile">Optional: Callback je gefundenem Dateipfad.</param>
    internal static TreeWalkStats WalkFilteredTree(
        IEnumerable<string> roots,
        string? filePattern,
        Action<string>? visitDirectory,
        Action<string>? visitFile)
        => WalkFilteredTreeCore(
            roots,
            FileSystemWalkOptions.Default(CancellationToken.None),
            filePattern,
            (visitDirectory, visitFile));

    private static TreeWalkStats WalkFilteredTreeCore(
        IEnumerable<string> roots,
        FileSystemWalkOptions options,
        string? filePattern,
        (Action<string>? VisitDirectory, Action<string>? VisitFile) callbacks)
    {
        var context = new WalkContext(options);
        EnqueueRoots(roots, context);

        while (context.PendingDirectories.Count > 0)
        {
            if (context.IsCancellationRequested) break;

            var entry = context.PendingDirectories.Pop();
            if (!context.VisitedDirectories.Add(NormalizeForDedup(entry.Directory))) continue;
            ProcessDirectory(entry, filePattern, callbacks, context);
        }

        return context.CreateStats();
    }

    private static void EnqueueRoots(IEnumerable<string> roots, WalkContext context)
    {
        foreach (var root in GetDistinctTopLevelRoots(roots))
        {
            if (context.IsCancellationRequested) break;
            context.PendingDirectories.Push((root, 0));
        }
    }

    private static void ProcessDirectory(
        (string Directory, int Depth) entry,
        string? filePattern,
        (Action<string>? VisitDirectory, Action<string>? VisitFile) callbacks,
        WalkContext context)
    {
        List<string>? subDirectories;
        if (entry.Depth >= context.Options.MaxDepth)
        {
            subDirectories = [];
            if (WalkContext.HasAnySubDirectories(entry.Directory, context.Warnings))
            {
                context.HasDepthTruncation = true;
            }
        }
        else
        {
            subDirectories = TryEnumerateSubDirectories(entry.Directory, context.Warnings);
        }

        if (subDirectories == null || context.IsCancellationRequested) return;

        var visitDirectory = callbacks.VisitDirectory;
        if (visitDirectory != null)
        {
            VisitSafely(visitDirectory, entry.Directory, context.Warnings);
        }

        if (context.IsCancellationRequested) return;
        VisitFilesIfRequested(entry.Directory, filePattern, callbacks.VisitFile, context);
        if (context.IsCancellationRequested || entry.Depth >= context.Options.MaxDepth) return;

        PushTraversableSubDirectories(subDirectories, entry.Depth, context);
    }

    /// <summary>Listet die direkten Unter Verzeichnisse auf; liefert <see langword="null"/>, wenn
    /// das Verzeichnis unzugänglich ist (Warnung, Teilbaum wird ausgelassen — Konzept 02, C).</summary>
    private static List<string>? TryEnumerateSubDirectories(string directory, List<string> warnings)
    {
        try
        {
            return Directory.EnumerateDirectories(directory).ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            warnings.Add($"{directory}: {ex.Message}");
            return null;
        }
    }

    private static void VisitFilesIfRequested(
        string directory,
        string? filePattern,
        Action<string>? visitFile,
        WalkContext context)
    {
        if (visitFile == null || filePattern == null || context.IsCancellationRequested) return;

        try
        {
            foreach (var file in Directory.EnumerateFiles(directory, filePattern, SearchOption.TopDirectoryOnly))
            {
                if (context.IsCancellationRequested) return;
                VisitSafely(visitFile, file, context.Warnings);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            context.Warnings.Add($"{directory}: {ex.Message}");
        }
    }

    private static void PushTraversableSubDirectories(
        List<string> subDirectories,
        int depth,
        WalkContext context)
    {
        foreach (var subDirectory in subDirectories)
        {
            if (context.IsCancellationRequested) return;
            if (context.Options.SkipExcludedDirectories && IsExcludedDirectoryName(Path.GetFileName(subDirectory)))
            {
                context.SkippedExcludedDirectoryCount++;
                continue;
            }

            try
            {
                if (!IsTraversableSubDirectory(new DirectoryInfo(subDirectory).Attributes))
                {
                    context.SkippedReparsePointCount++;
                    continue;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                context.Warnings.Add($"{subDirectory}: {ex.Message}");
                continue;
            }

            if (context.IsCancellationRequested) return;
            context.PendingDirectories.Push((subDirectory, depth + 1));
        }
    }

    private static void VisitSafely(Action<string> visitor, string path, List<string> warnings)
    {
        try
        {
            visitor(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            warnings.Add($"{path}: {ex.Message}");
        }
    }

    private static string NormalizeForDedup(string directory) =>
        directory.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)
                 .TrimEnd(Path.DirectorySeparatorChar);

    /// <summary>Entfernt Wurzeln, die in einer anderen Wurzel enthalten sind (die umfassende
    /// Wurzel traversiert sie mit) — verhindert Doppelbesuche bei verschachtelten
    /// Projektverzeichnissen.</summary>
    private static List<string> GetDistinctTopLevelRoots(IEnumerable<string> roots)
    {
        var normalized = roots
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Select(NormalizeForDedup)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(r => r.Length)
            .ToList();

        var accepted = new List<string>();
        foreach (var candidate in normalized)
        {
            if (accepted.Any(a => candidate.StartsWith(a + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }
            accepted.Add(candidate);
        }
        return accepted;
    }


    private static IEnumerable<string> EnumerateFiles(
        string directory,
        EnumerationOptions options,
        CancellationToken cancellationToken,
        Action recordError)
    {
        IEnumerator<string>? enumerator;
        try
        {
            enumerator = Directory.EnumerateFiles(directory, "*", options).GetEnumerator();
        }
        catch (UnauthorizedAccessException)
        {
            recordError();
            yield break;
        }
        catch (IOException)
        {
            recordError();
            yield break;
        }

        using (enumerator)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                bool hasNext;
                try
                {
                    hasNext = enumerator.MoveNext();
                }
                catch (UnauthorizedAccessException)
                {
                    recordError();
                    yield break;
                }
                catch (IOException)
                {
                    recordError();
                    yield break;
                }

                if (!hasNext) yield break;
                yield return enumerator.Current;
            }
        }
    }

    private sealed class WalkContext
    {
        internal WalkContext(FileSystemWalkOptions options)
        {
            Options = options;
            Warnings = [];
            VisitedDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            PendingDirectories = [];
            CancellationRequested = options.CancellationToken.IsCancellationRequested;
        }

        internal FileSystemWalkOptions Options { get; }
        internal List<string> Warnings { get; }
        internal HashSet<string> VisitedDirectories { get; }
        internal Stack<(string Directory, int Depth)> PendingDirectories { get; }
        internal bool CancellationRequested { get; private set; }
        internal int SkippedExcludedDirectoryCount { get; set; }
        internal int SkippedReparsePointCount { get; set; }
        internal bool HasDepthTruncation { get; set; }

        internal bool IsCancellationRequested
        {
            get
            {
                if (Options.CancellationToken.IsCancellationRequested)
                {
                    CancellationRequested = true;
                }

                return CancellationRequested;
            }
        }

        internal TreeWalkStats CreateStats() => new(Warnings)
        {
            CancellationRequested = CancellationRequested,
            SkippedExcludedDirectoryCount = SkippedExcludedDirectoryCount,
            SkippedReparsePointCount = SkippedReparsePointCount,
            HasDepthTruncation = HasDepthTruncation,
        };

        internal static bool HasAnySubDirectories(string directory, List<string> warnings)
        {
            try
            {
                using var enumerator = Directory.EnumerateDirectories(directory).GetEnumerator();
                return enumerator.MoveNext();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                warnings.Add($"{directory}: {ex.Message}");
                return false;
            }
        }
    }
}

internal sealed class FileSystemEnumerationResult
{
    private readonly Func<int> _errorCountProvider;

    internal FileSystemEnumerationResult(IEnumerable<string> files, Func<int> errorCountProvider)
    {
        Files = files;
        _errorCountProvider = errorCountProvider;
    }

    internal IEnumerable<string> Files { get; }
    internal int ErrorCount => _errorCountProvider();
}
