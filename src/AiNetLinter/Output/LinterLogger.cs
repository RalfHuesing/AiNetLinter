#nullable enable

using AiNetLinter.Baseline;

namespace AiNetLinter.Output;

/// <summary>
/// Kapselt Logging-Operationen fuer die Linter-Ausfuehrung.
/// </summary>
internal static class LinterLogger
{
    public static void LogStart(bool verbose, string configPath, string targetPath, ILintConsole? console = null)
    {
        if (verbose)
        {
            var c = console ?? LinterConsole.Instance;
            c.WriteLine($"[INFO]: Lade Konfiguration von: {configPath}");
            c.WriteLine($"[INFO]: Analysiere Ziel-Pfad: {targetPath}");
        }
    }

    public static void LogBaselineCreate(bool verbose, string targetPath, string baselinePath, ILintConsole? console = null)
    {
        if (verbose)
        {
            var c = console ?? LinterConsole.Instance;
            c.WriteLine($"[INFO]: Erzeuge Baseline fuer: {targetPath}");
            c.WriteLine($"[INFO]: Ausgabedatei: {baselinePath}");
        }
    }

    public static void LogDisableAllInject(bool verbose, string targetPath, ILintConsole? console = null)
    {
        if (verbose)
        {
            var c = console ?? LinterConsole.Instance;
            c.WriteLine($"[INFO]: Audit und Disable-all-Injection unter: {targetPath}");
        }
    }

    public static void LogDisableAllRemove(bool verbose, string targetPath, ILintConsole? console = null)
    {
        if (verbose)
        {
            var c = console ?? LinterConsole.Instance;
            c.WriteLine($"[INFO]: Entferne Disable-all-Kommentare unter: {targetPath}");
        }
    }

    public static void LogBaselineUpdate(bool verbose, BaselineComparisonResult comparison, ILintConsole? console = null)
    {
        if (!verbose) return;

        var c = console ?? LinterConsole.Instance;
        var changedCount = comparison.ChangedFiles.Count;
        var removedCount = comparison.RemovedFiles.Count;
        c.WriteLine($"[INFO]: Baseline aktualisiert: {changedCount} geaendert, {removedCount} entfernt.");
    }
}
