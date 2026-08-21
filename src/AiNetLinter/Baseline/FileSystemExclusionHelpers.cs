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
    /// Rekursive Tiefen-Traversierung der Wurzelverzeichnisse unter den projektweiten
    /// Suchregeln (eine Kern-Implementierung für Max-mtime-Walk und Datei-Sweep):
    /// - <see cref="FileAttributes.ReparsePoint"/>-Verzeichnisse werden nie betreten —
    ///   Junction-/Symlink-Zyklen können die Traversierung nicht aufblähen oder endlos laufen
    ///   lassen (Konzept 02, B).
    /// - Verzeichnisse mit ausgeschlossenem Namen (<see cref="SearchExcludedDirectories"/>,
    ///   z. B. <c>.git</c>, <c>obj</c>, <c>node_modules</c>) werden samt Teilbaum übersprungen
    ///   (Konzept 02, D) — es entsteht keine eigene vierte Ausschlussliste.
    /// - Verschachtelte Wurzeln werden dedupliziert (die umfassende Wurzel deckt die
    ///   enthaltene ab).
    /// - Ein unzugänglicher Teilbaum erzeugt genau eine Warnung und bricht den Gesamtwalk
    ///   NICHT ab (Konzept 02, C).
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
    {
        var warnings = new List<string>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pending = new Stack<string>();

        foreach (var root in GetDistinctTopLevelRoots(roots))
        {
            pending.Push(root);
        }

        while (pending.Count > 0)
        {
            var directory = pending.Pop();
            if (!visited.Add(NormalizeForDedup(directory))) continue;

            var subDirectories = TryEnumerateSubDirectories(directory, warnings);
            if (subDirectories == null) continue;

            if (visitDirectory != null)
            {
                VisitSafely(visitDirectory, directory, warnings);
            }

            VisitFilesIfRequested(directory, filePattern, visitFile, warnings);
            PushTraversableSubDirectories(subDirectories, pending, warnings);
        }

        return new TreeWalkStats(warnings);
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
        List<string> warnings)
    {
        if (visitFile == null || filePattern == null) return;

        IEnumerable<string> files;
        try
        {
            files = Directory.EnumerateFiles(directory, filePattern, SearchOption.TopDirectoryOnly);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            warnings.Add($"{directory}: {ex.Message}");
            return;
        }

        foreach (var file in files)
        {
            VisitSafely(visitFile, file, warnings);
        }
    }

    private static void PushTraversableSubDirectories(
        List<string> subDirectories,
        Stack<string> pending,
        List<string> warnings)
    {
        foreach (var subDirectory in subDirectories)
        {
            if (IsExcludedDirectoryName(Path.GetFileName(subDirectory))) continue;
            try
            {
                if (!IsTraversableSubDirectory(new DirectoryInfo(subDirectory).Attributes)) continue;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                warnings.Add($"{subDirectory}: {ex.Message}");
                continue;
            }

            pending.Push(subDirectory);
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
