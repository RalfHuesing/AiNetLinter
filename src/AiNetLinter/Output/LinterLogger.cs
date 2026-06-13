#nullable enable

using System;
using AiNetLinter.Baseline;

namespace AiNetLinter.Output;

/// <summary>
/// Kapselt Logging-Operationen fuer die Linter-Ausfuehrung.
/// </summary>
public static class LinterLogger
{
    /// <summary>
    /// Protokolliert den Start des Linters.
    /// </summary>
    /// <param name="verbose">Gibt an, ob Logging aktiviert ist.</param>
    /// <param name="configPath">Der Pfad zur geladenen Konfiguration.</param>
    /// <param name="targetPath">Der analysierte Ziel-Pfad.</param>
    public static void LogStart(bool verbose, string configPath, string targetPath)
    {
        if (verbose)
        {
            Console.WriteLine($"[INFO]: Lade Konfiguration von: {configPath}");
            Console.WriteLine($"[INFO]: Analysiere Ziel-Pfad: {targetPath}");
        }
    }

    /// <summary>
    /// Protokolliert das Erstellen einer Baseline.
    /// </summary>
    /// <param name="verbose">Gibt an, ob Logging aktiviert ist.</param>
    /// <param name="targetPath">Der Zielpfad.</param>
    /// <param name="baselinePath">Der Ausgabepfad der Baseline.</param>
    public static void LogBaselineCreate(bool verbose, string targetPath, string baselinePath)
    {
        if (verbose)
        {
            Console.WriteLine($"[INFO]: Erzeuge Baseline fuer: {targetPath}");
            Console.WriteLine($"[INFO]: Ausgabedatei: {baselinePath}");
        }
    }

    /// <summary>
    /// Protokolliert den Start des Add-Disable-All-Modus.
    /// </summary>
    /// <param name="verbose">Gibt an, ob Logging aktiviert ist.</param>
    /// <param name="targetPath">Der Zielpfad.</param>
    public static void LogDisableAllInject(bool verbose, string targetPath)
    {
        if (verbose)
        {
            Console.WriteLine($"[INFO]: Audit und Disable-all-Injection unter: {targetPath}");
        }
    }

    /// <summary>
    /// Protokolliert den Start des Remove-Disable-All-Modus.
    /// </summary>
    /// <param name="verbose">Gibt an, ob Logging aktiviert ist.</param>
    /// <param name="targetPath">Der Zielpfad.</param>
    public static void LogDisableAllRemove(bool verbose, string targetPath)
    {
        if (verbose)
        {
            Console.WriteLine($"[INFO]: Entferne Disable-all-Kommentare unter: {targetPath}");
        }
    }

    /// <summary>
    /// Protokolliert das Ergebnis eines Baseline-Updates.
    /// </summary>
    /// <param name="verbose">Gibt an, ob Logging aktiviert ist.</param>
    /// <param name="comparison">Das Vergleichsergebnis der Baseline.</param>
    public static void LogBaselineUpdate(bool verbose, BaselineComparisonResult comparison)
    {
        if (!verbose)
        {
            return;
        }

        var changedCount = comparison.ChangedFiles.Count;
        var removedCount = comparison.RemovedFiles.Count;
        Console.WriteLine($"[INFO]: Baseline aktualisiert: {changedCount} geaendert, {removedCount} entfernt.");
    }
}
