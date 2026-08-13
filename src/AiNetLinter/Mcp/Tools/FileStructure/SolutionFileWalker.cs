#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using AiNetLinter.Baseline;
using Microsoft.CodeAnalysis;

namespace AiNetLinter.Mcp.Tools.FileStructure;

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

                result.Add(new WalkedFile(relativePath, document.FilePath!, document));
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
    /// Liest die Zeilen aus einem bereits residenten Roslyn-Dokument. Falls der Workspace den
    /// Text noch nicht materialisiert hat, wird fuer den Live-Pfad auf die Datei zurueckgefallen.
    /// </summary>
    internal static string[]? TryReadAllLines(WalkedFile file)
    {
        if (file.Document.TryGetText(out var sourceText))
        {
            return ToLines(sourceText);
        }

        if (!File.Exists(file.AbsolutePath))
        {
            // ainetlinter-disable BanBlockingTaskAccess — der synchrone Toolpfad kann bei rein
            // virtuellen Dokumenten nicht auf die Platte ausweichen; Roslyn liefert deren Text
            // aus dem bereits an den Snapshot gebundenen Workspace.
            return ToLines(file.Document.GetTextAsync().GetAwaiter().GetResult());
        }

        try
        {
            return File.ReadAllLines(file.AbsolutePath);
        }
        catch (IOException)
        {
            return null;
        }
    }

    private static string[] ToLines(Microsoft.CodeAnalysis.Text.SourceText sourceText)
    {
        var lines = new string[sourceText.Lines.Count];
        for (var index = 0; index < lines.Length; index++)
        {
            lines[index] = sourceText.Lines[index].ToString();
        }
        return lines;
    }
}
