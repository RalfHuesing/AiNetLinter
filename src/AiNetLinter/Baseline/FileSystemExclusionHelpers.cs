#nullable enable

using System;
using System.Collections.Generic;
using System.IO;

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
    {
        try
        {
            return Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories);
        }
        catch (UnauthorizedAccessException) { return Array.Empty<string>(); }
        catch (IOException) { return Array.Empty<string>(); }
    }

    /// <summary>
    /// Prueft, ob <paramref name="path"/> in einem generierten Verzeichnis liegt
    /// (<c>obj/</c>, <c>bin/</c>, <c>node_modules/</c>). Vergleich ist case-insensitive und
    /// verwendet <see cref="Path.DirectorySeparatorChar"/>, damit sowohl Windows- als auch
    /// forward-slash-Pfade korrekt erkannt werden.
    /// </summary>
    internal static bool IsGeneratedPath(string path)
    {
        var sep = Path.DirectorySeparatorChar;
        return path.Contains($"{sep}obj{sep}", StringComparison.OrdinalIgnoreCase)
            || path.Contains($"{sep}bin{sep}", StringComparison.OrdinalIgnoreCase)
            || path.Contains($"{sep}node_modules{sep}", StringComparison.OrdinalIgnoreCase);
    }
}
