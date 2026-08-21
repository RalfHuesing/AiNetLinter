#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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

    internal static FileSystemEnumerationResult SafeEnumerateFilesWithErrors(string directory)
    {
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = false,
            AttributesToSkip = FileAttributes.ReparsePoint,
        };
        var files = new List<string>();
        var errorCount = 0;

        try
        {
            files.AddRange(Directory.EnumerateFiles(directory, "*", options));
        }
        catch (UnauthorizedAccessException ignored) { _ = ignored; errorCount++; }
        catch (IOException ignored) { _ = ignored; errorCount++; }

        return new FileSystemEnumerationResult(files, errorCount);
    }

    internal static bool IsSearchExcludedRelativePath(string relativePath)
    {
        var normalized = relativePath.Replace('\\', '/').Trim('/');
        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var excludedDirectories = new[]
        {
            ".git", ".hg", ".svn", ".vs", ".idea", "obj", "bin", "node_modules",
            "worktrees", ".worktrees", "testresults", "artifacts", "coverage", "temp", "packages",
        };

        return segments.Any(segment => excludedDirectories.Contains(segment, StringComparer.OrdinalIgnoreCase));
    }

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
            || path.Contains($"{sep}.worktrees{sep}", StringComparison.OrdinalIgnoreCase);
    }
}

internal sealed record FileSystemEnumerationResult(IReadOnlyList<string> Files, int ErrorCount);
