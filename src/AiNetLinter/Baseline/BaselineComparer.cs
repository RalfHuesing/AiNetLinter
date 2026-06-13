namespace AiNetLinter.Baseline;

/// <summary>
/// Vergleicht gespeicherte Baseline-Checksummen mit dem aktuellen Dateistand.
/// Erkennt Umbenennungen anhand identischer SHA-256-Hashes, um falsch-positive "neue Datei"-Meldungen zu vermeiden.
/// </summary>
public static class BaselineComparer
{
    /// <summary>
    /// Ermittelt geänderte, neue und entfernte Dateien anhand der Checksummen.
    /// </summary>
    public static BaselineComparisonResult Compare(
        BaselineFile storedBaseline,
        IReadOnlyDictionary<string, string> currentChecksums)
    {
        var changed = FindChecksumChanges(storedBaseline, currentChecksums);
        var removed = FindRemovedFiles(storedBaseline, currentChecksums);
        AddNewFiles(storedBaseline, currentChecksums, changed, removed);

        var hasAnyChange = changed.Count > 0 || removed.Count > 0;
        return new BaselineComparisonResult
        {
            ChangedFiles = changed,
            RemovedFiles = removed,
            HasAnyChange = hasAnyChange,
        };
    }

    private static HashSet<string> FindChecksumChanges(
        BaselineFile storedBaseline,
        IReadOnlyDictionary<string, string> currentChecksums)
    {
        var changed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (relativePath, storedChecksum) in storedBaseline.Files)
        {
            if (!currentChecksums.TryGetValue(relativePath, out var currentChecksum))
            {
                continue;
            }

            if (!string.Equals(storedChecksum, currentChecksum, StringComparison.OrdinalIgnoreCase))
            {
                changed.Add(relativePath);
            }
        }

        return changed;
    }

    private static HashSet<string> FindRemovedFiles(
        BaselineFile storedBaseline,
        IReadOnlyDictionary<string, string> currentChecksums)
    {
        var removed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var relativePath in storedBaseline.Files.Keys)
        {
            if (!currentChecksums.ContainsKey(relativePath))
            {
                removed.Add(relativePath);
            }
        }

        return removed;
    }

    private static void AddNewFiles(
        BaselineFile storedBaseline,
        IReadOnlyDictionary<string, string> currentChecksums,
        HashSet<string> changed,
        HashSet<string> removed)
    {
        var storedHashToPath = BuildHashIndex(storedBaseline.Files);

        foreach (var (relativePath, currentHash) in currentChecksums)
        {
            if (storedBaseline.Files.ContainsKey(relativePath))
            {
                continue;
            }

            if (storedHashToPath.TryGetValue(currentHash, out var originalPath) && removed.Contains(originalPath))
            {
                // Gleicher Hash unter anderem Pfad → Umbenennung, keine Inhaltsänderung
                removed.Remove(originalPath);
            }
            else
            {
                changed.Add(relativePath);
            }
        }
    }

    private static Dictionary<string, string> BuildHashIndex(IReadOnlyDictionary<string, string> files)
    {
        var index = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (path, hash) in files)
        {
            index.TryAdd(hash, path);
        }
        return index;
    }
}
