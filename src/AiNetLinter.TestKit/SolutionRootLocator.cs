#nullable enable

using System;
using System.IO;

namespace AiNetLinter.TestKit;

/// <summary>
/// Sucht das Root-Verzeichnis der Projektmappe ausgehend vom aktuellen Anwendungsordner.
/// </summary>
public static class SolutionRootLocator
{
    /// <summary>
    /// Ermittelt das Verzeichnis mit <c>AiNetLinter.slnx</c>.
    /// </summary>
    public static string Find()
    {
        var currentDirectory = new DirectoryInfo(AppContext.BaseDirectory);
        while (currentDirectory is not null)
        {
            if (File.Exists(Path.Combine(currentDirectory.FullName, "AiNetLinter.slnx")))
            {
                return currentDirectory.FullName;
            }

            currentDirectory = currentDirectory.Parent;
        }

        throw new DirectoryNotFoundException("Das Root-Verzeichnis mit der Projektmappe 'AiNetLinter.slnx' wurde nicht gefunden.");
    }
}
