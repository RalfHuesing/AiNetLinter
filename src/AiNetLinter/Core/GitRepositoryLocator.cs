#nullable enable

using System.IO;

namespace AiNetLinter.Core;

/// <summary>
/// Ermittelt das Wurzelverzeichnis eines Git-Repositories (Verzeichnis mit <c>.git</c>-Unterordner)
/// ausgehend von einem Datei- oder Verzeichnispfad — zentraler Helper statt separater Implementierungen
/// in <see cref="DiffImpactAnalyzer"/> und <see cref="AiNetLinter.Scope.GitChangedFilesResolver"/>.
/// </summary>
internal static class GitRepositoryLocator
{
    internal static string? FindRoot(string startPath)
    {
        var current = File.Exists(startPath) ? Path.GetDirectoryName(startPath) : startPath;
        while (!string.IsNullOrEmpty(current))
        {
            if (Directory.Exists(Path.Combine(current, ".git")))
            {
                return current;
            }
            current = Path.GetDirectoryName(current);
        }
        return null;
    }
}
