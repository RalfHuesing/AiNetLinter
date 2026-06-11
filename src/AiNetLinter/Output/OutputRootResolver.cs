namespace AiNetLinter.Output;

/// <summary>
/// Ermittelt die Pfadbasis für relative Ausgabepfade aus dem CLI-Argument --path.
/// </summary>
public static class OutputRootResolver
{
    /// <summary>
    /// Löst das --path-Argument in ein absolutes Verzeichnis auf.
    /// Verzeichnis → FullPath; Solution-Datei → übergeordnetes Verzeichnis.
    /// </summary>
    public static string Resolve(string targetPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);

        if (Directory.Exists(targetPath))
        {
            return Path.GetFullPath(targetPath);
        }

        if (File.Exists(targetPath))
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(targetPath));
            if (string.IsNullOrEmpty(directory))
            {
                throw new InvalidOperationException($"Kein Verzeichnis für Pfad ermittelbar: {targetPath}");
            }

            return directory;
        }

        throw new DirectoryNotFoundException($"Pfad nicht gefunden: {targetPath}");
    }
}
