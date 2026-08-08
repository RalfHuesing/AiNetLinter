#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using AiNetLinter.Baseline;
using Microsoft.CodeAnalysis;

namespace AiNetLinter.Mcp.Tools;

/// <summary>
/// Gemeinsamer Datei-Walk-Kern gegen eine resident gehaltene <see cref="Solution"/> — extrahiert aus
/// <see cref="GetHotspotsScanner"/> (<c>CollectFiles</c>/<c>MatchesScope</c>/<c>TryCountLines</c>), um
/// eine zweite, unabhaengige Walk-Implementierung fuer <c>metrics_tree</c>s Datei-Walk-Modi zu
/// vermeiden. Generalisiert um einen zusaetzlichen, optionalen Regex-<c>fileFilter</c> auf den
/// relativen Pfad und liefert Datei-Inhalte statt nur der Zeilenzahl (<c>comment_density</c> braucht
/// den Inhalt, <c>code_size</c> nutzt nur <c>.Length</c> auf dem Ergebnis).
/// </summary>
internal static class SolutionFileWalker
{
    /// <summary>Relativer und absoluter Pfad einer per Walk gefundenen Datei.</summary>
    internal readonly record struct WalkedFile(string RelativePath, string AbsolutePath);

    /// <summary>
    /// Sammelt alle gueltigen Quelldateien der <paramref name="solution"/>, gefiltert ueber
    /// <see cref="MatchesScope"/> (Projekt-Name/Pfad-Substring) und optional zusaetzlich ueber
    /// <paramref name="fileFilter"/> (Regex auf den relativen Pfad).
    /// </summary>
    internal static List<WalkedFile> CollectFiles(
        Solution solution, string solutionDir, string? scopeFilter, Regex? fileFilter = null)
    {
        var result = new List<WalkedFile>();

        foreach (var project in solution.Projects)
        {
            foreach (var document in project.Documents)
            {
                if (!SourceFileCatalog.IsValidDocument(document, solutionDir)) continue;
                if (!MatchesScope(document, solutionDir, scopeFilter)) continue;

                var relativePath = Path.GetRelativePath(solutionDir, document.FilePath!).Replace('\\', '/');
                if (fileFilter != null && !fileFilter.IsMatch(relativePath)) continue;

                result.Add(new WalkedFile(relativePath, document.FilePath!));
            }
        }

        return result;
    }

    /// <summary>
    /// Prueft, ob ein Dokument in den optionalen <paramref name="scopeFilter"/> faellt — entweder als
    /// Substring im Projekt-Namen oder im relativen Pfad (beide <see cref="StringComparison.OrdinalIgnoreCase"/>).
    /// </summary>
    internal static bool MatchesScope(Document document, string solutionDir, string? scopeFilter)
    {
        if (string.IsNullOrEmpty(scopeFilter)) return true;

        if (document.Project.Name.Contains(scopeFilter, StringComparison.OrdinalIgnoreCase)) return true;

        var relativePath = Path.GetRelativePath(solutionDir, document.FilePath!);
        return relativePath.Contains(scopeFilter, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Liest alle Zeilen von <paramref name="path"/>, oder <see langword="null"/> bei einem
    /// <see cref="IOException"/> (z. B. Datei zwischenzeitlich geloescht/gesperrt).
    /// </summary>
    internal static string[]? TryReadAllLines(string path)
    {
        try
        {
            return File.ReadAllLines(path);
        }
        catch (IOException)
        {
            return null;
        }
    }
}
