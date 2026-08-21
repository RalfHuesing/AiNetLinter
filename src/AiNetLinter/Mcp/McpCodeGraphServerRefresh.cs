#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using AiNetLinter.Baseline;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace AiNetLinter.Mcp;

/// <summary>
/// Lazy Solution-Refresh fuer den MCP-Server: pro Aufruf wird die resident gehaltene
/// <see cref="Solution"/> mit dem aktuellen Disk-Zustand abgeglichen. Drei Phasen laufen
/// nacheinander — gelöschte Dateien raus, neue Dateien rein, modifizierte Dateien
/// inkrementell aktualisiert. Bewusst als separate Klasse extrahiert, damit
/// <see cref="McpCodeGraphServer"/> selbst unter dem projektweiten
/// <c>MaxAIContextFootprint</c>-Limit bleibt und die einzelnen Phasen unter dem
/// <c>MaxCognitiveComplexity</c>-Limit gehalten werden koennen.
/// </summary>
internal static class McpCodeGraphServerRefresh
{

    /// <summary>
    /// Liefert die ggf. aktualisierte <see cref="Solution"/> und ein Flag, ob sich etwas
    /// geaendert hat. <paramref name="p.FileState"/> wird bei geloeschten Dateien
    /// bereinigt und bei neu einghaengten Dateien befuellt; der Aufrufer uebernimmt die
    /// Anwendung ueber <see cref="SourceFileCatalog.WithUpdatedSolution"/>.
    /// <paramref name="p.ShouldSweep"/> steuert Phase 2 (Verzeichnis-Sweep): liefert der
    /// Aufrufer <see langword="false"/>, wird der aufwendige <c>Directory.EnumerateFiles</c>-Walk
    /// uebersprungen — die uebrigen Phasen (geloeschte/modifizierte Dateien) laufen weiter,
    /// weil sie auf den bereits gecachten <see cref="McpFileState"/>-Eintraegen arbeiten.
    /// Parameter-Record, weil 5 Eingabewerte die projektweite <c>MaxMethodParameterCount: 4</c>-
    /// Grenze ueberschreiten wuerden.
    /// </summary>
    internal static (Solution solution, bool changed) Run(
        Solution current,
        string? solutionDir,
        McpCodeGraphServerRefreshParameters p)
    {
        var updated = current;
        var anyChanged = false;
        var removedIds = RemoveDeletedDocuments(ref updated, solutionDir, p.FileState, ref anyChanged);
        anyChanged |= SweepForNewFiles(ref updated, solutionDir, p.FileState, p.WriteWarn, p.ShouldSweep);
        RefreshModifiedDocuments(ref updated, solutionDir, removedIds, p.FileState, p.WriteWarn, ref anyChanged);
        return (updated, anyChanged);
    }

    private static HashSet<DocumentId> RemoveDeletedDocuments(
        ref Solution updated,
        string? solutionDir,
        Dictionary<string, McpFileState> fileState,
        ref bool anyChanged)
    {
        var removedIds = new HashSet<DocumentId>();
        foreach (var project in updated.Projects)
        {
            foreach (var document in project.Documents)
            {
                if (!SourceFileCatalog.IsValidDocument(document, solutionDir)) continue;
                var path = document.FilePath;
                if (string.IsNullOrEmpty(path) || File.Exists(path)) continue;

                updated = updated.RemoveDocument(document.Id);
                fileState.Remove(path);
                removedIds.Add(document.Id);
                anyChanged = true;
            }
        }
        return removedIds;
    }

    private static bool SweepForNewFiles(
        ref Solution updated,
        string? solutionDir,
        Dictionary<string, McpFileState> fileState,
        Action<string> writeWarn,
        Func<bool> shouldSweep)
    {
        if (!shouldSweep()) return false;
        if (string.IsNullOrEmpty(solutionDir) || !Directory.Exists(solutionDir)) return false;

        var knownPaths = BuildKnownPathSet(updated);
        var changed = false;

        // Sweep-Grenze = Vereinigung der Projektverzeichnisse (Konzept 02, Option b'):
        // PickProjectForNewFile haengt neue Dateien ausschliesslich unterhalb bekannter
        // Projektverzeichnisse ein — alles andere kann keine gueltigen neuen Quelldokumente
        // enthalten und wird deshalb gar nicht erst enumeriert (statt enumeriert und
        // verworfen zu werden).
        var sweepRoots = GetSweepRoots(updated, solutionDir);
        if (sweepRoots.Count == 0) return false;

        var candidates = new List<string>();
        var walkStats = FileSystemExclusionHelpers.WalkFilteredTree(
            sweepRoots,
            filePattern: "*.cs",
            visitDirectory: null,
            visitFile: path => candidates.Add(path));
        foreach (var warning in walkStats.Warnings)
        {
            writeWarn($"[WARN]: Verzeichnis-Sweep teilweise unzugänglich ({warning})");
        }

        foreach (var path in candidates)
        {
            if (SourceFileCatalog.IsGeneratedPath(path)) continue;
            if (knownPaths.Contains(path)) continue;

            // Kein Directory-Praefix-Treffer auf ein bekanntes Projekt bedeutet: die Datei
            // gehoert erkennbar zu keinem der geladenen Projekte (z. B. ein unabhaengiges
            // Test-Fixture-Projekt an anderer Stelle im selben Solution-Verzeichnis-Baum,
            // ausserhalb jeder Projekt-Ordnerstruktur). Frueher fiel dieser Fall auf "erstes
            // Projekt der Solution" zurueck — das haengte projektfremde Dateien (inkl. bewusst
            // regelverletzender Test-Fixtures) lautlos an ein beliebiges Projekt und machte
            // Lint-/Safeguard-Ergebnisse nicht-deterministisch, sobald der Sweep unter Last
            // (Directory-mtime-Aenderungen irgendwo im Repo) auslöste. Ohne Praefix-Treffer wird
            // die Datei jetzt uebersprungen statt willkuerlich zugeordnet.
            var projectId = PickProjectForNewFile(updated, path);
            if (projectId is null) continue;

            if (TryAddDocument(ref updated, projectId, path, fileState, writeWarn))
            {
                knownPaths.Add(path);
                changed = true;
            }
        }

        return changed;
    }

    private static void RefreshModifiedDocuments(
        ref Solution updated,
        string? solutionDir,
        HashSet<DocumentId> removedIds,
        Dictionary<string, McpFileState> fileState,
        Action<string> writeWarn,
        ref bool anyChanged)
    {
        foreach (var project in updated.Projects)
        {
            foreach (var document in project.Documents)
            {
                if (removedIds.Contains(document.Id)) continue;
                if (!SourceFileCatalog.IsValidDocument(document, solutionDir)) continue;
                if (TryRefreshDocument(document, ref updated, fileState, writeWarn)) anyChanged = true;
            }
        }
    }

    private static HashSet<string> BuildKnownPathSet(Solution solution)
    {
        return new HashSet<string>(
            solution.Projects.SelectMany(p => p.Documents)
                  .Where(d => d.FilePath != null)
                  .Select(d => d.FilePath!),
            StringComparer.OrdinalIgnoreCase);
    }

    private static bool TryAddDocument(
        ref Solution updated,
        ProjectId projectId,
        string path,
        Dictionary<string, McpFileState> fileState,
        Action<string> writeWarn)
    {
        try
        {
            // FileTextLoader liest den Inhalt on-demand von der Platte — kein eager
            // In-Memory-Kopieren noetig; Roslyn fragt erst beim Compile/SyntaxTree ab.
            var docInfo = DocumentInfo.Create(
                DocumentId.CreateNewId(projectId),
                Path.GetFileName(path),
                loader: new FileTextLoader(path, Encoding.UTF8),
                filePath: path);

            updated = updated.AddDocument(docInfo);
            CacheInitialFileState(path, fileState, writeWarn);
            return true;
        }
        catch (IOException ex)
        {
            writeWarn($"[WARN]: Neue Datei konnte nicht einghaengt werden ({path}): {ex.Message}");
            return false;
        }
    }

    internal static void CacheInitialFileState(
        string path,
        Dictionary<string, McpFileState> fileState,
        Action<string> writeWarn)
    {
        if (!File.Exists(path)) return;
        try
        {
            var mtime = File.GetLastWriteTimeUtc(path);
            var hash = FileChecksumCalculator.ComputeSha256Hex(path);
            fileState[path] = new McpFileState(mtime, hash);
        }
        catch (IOException ex)
        {
            writeWarn($"[WARN]: Datei konnte beim MCP-Server-Start nicht gehasht werden ({path}): {ex.Message}");
        }
    }

    private static bool TryRefreshDocument(
        Document document,
        ref Solution updated,
        Dictionary<string, McpFileState> fileState,
        Action<string> writeWarn)
    {
        var path = document.FilePath!;
        if (!File.Exists(path)) return false;

        var currentMtime = File.GetLastWriteTimeUtc(path);
        if (fileState.TryGetValue(path, out var known) && known.MtimeUtc == currentMtime)
        {
            return false;
        }

        try
        {
            return TryApplyContentChange(document, path, currentMtime, known, ref updated, fileState);
        }
        catch (IOException ex)
        {
            writeWarn($"[WARN]: Datei konnte beim Staleness-Check nicht gelesen werden ({path}): {ex.Message}");
            return false;
        }
    }

    private static bool TryApplyContentChange(
        Document document,
        string path,
        DateTime currentMtime,
        McpFileState known,
        ref Solution updated,
        Dictionary<string, McpFileState> fileState)
    {
        var currentHash = FileChecksumCalculator.ComputeSha256Hex(path);
        if (known.Hash == currentHash)
        {
            fileState[path] = known with { MtimeUtc = currentMtime };
            return false;
        }

        var text = File.ReadAllText(path);
        updated = updated.WithDocumentText(document.Id, SourceText.From(text));
        fileState[path] = new McpFileState(currentMtime, currentHash);
        return true;
    }

    /// <summary>
    /// Ermittelt das passendste Projekt fuer eine neu entdeckte Datei anhand des
    /// Projektverzeichnisses. Waehlt das Projekt mit dem laengsten uebereinstimmenden
    /// Pfadpraefix (inkl. Verzeichnistrenner-Abgleich), sodass Unter- und Testprojekte
    /// (z. B. src/AiNetLinter.FastTests) Vorrang vor uebergeordneten oder praefixaehnlichen
    /// Projekten (z. B. src/AiNetLinter) erhalten. Liefert null, wenn die Datei in keinem
    /// bekannten Projektverzeichnis liegt.
    /// </summary>
    internal static ProjectId? PickProjectForNewFile(Solution solution, string newFilePath)
    {
        var fileDir = Path.GetDirectoryName(newFilePath);
        if (string.IsNullOrEmpty(fileDir)) return null;

        return solution.Projects
            .Where(p => p.FilePath != null && IsDirectoryInside(fileDir, Path.GetDirectoryName(p.FilePath)!))
            .OrderByDescending(p => Path.GetDirectoryName(p.FilePath)!.Length)
            .FirstOrDefault()
            ?.Id;
    }

    /// <summary>
    /// Vereinigung der Projektverzeichnisse als Walk-/Sweep-Grenze (Konzept 02, Option b'):
    /// Neue gueltige Quelldokumente koennen ausschliesslich unterhalb bekannter
    /// Projektordner entstehen (<see cref="PickProjectForNewFile"/> liefert ausserhalb
    /// <see langword="null"/>) — der Staleness-Walk darf sich daher auf genau diese
    /// Verzeichnisse beschraenken, ohne eine neue Datei zu uebersehen. Fallback auf das
    /// Solution-Verzeichnis, wenn kein Projekt einen Dateipfad besitzt.
    /// </summary>
    internal static IReadOnlyList<string> GetSweepRoots(Solution solution, string? solutionDir)
    {
        var projectDirectories = solution.Projects
            .Where(p => p.FilePath != null)
            .Select(p => Path.GetDirectoryName(p.FilePath)!)
            .Where(d => !string.IsNullOrEmpty(d) && Directory.Exists(d))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (projectDirectories.Count > 0) return projectDirectories;

        return !string.IsNullOrEmpty(solutionDir) && Directory.Exists(solutionDir)
            ? [solutionDir]
            : [];
    }

    private static bool IsDirectoryInside(string dir, string parentDir)
    {
        var normalizedDir = dir.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)
                               .TrimEnd(Path.DirectorySeparatorChar);
        var normalizedParent = parentDir.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)
                                        .TrimEnd(Path.DirectorySeparatorChar);

        if (string.Equals(normalizedDir, normalizedParent, StringComparison.OrdinalIgnoreCase)) return true;

        return normalizedDir.StartsWith(normalizedParent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }
}
