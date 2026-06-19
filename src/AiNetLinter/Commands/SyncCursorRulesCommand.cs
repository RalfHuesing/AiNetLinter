#nullable enable

using System.IO;
using System.Text;
using AiNetLinter.Cli;
using AiNetLinter.Configuration;
using AiNetLinter.Core;
using AiNetLinter.Output;

namespace AiNetLinter.Commands;

/// <summary>
/// Synchronisiert oder prüft die Cursor-Regeldateien (.mdc) aus der aktuellen Konfiguration.
/// </summary>
internal static class SyncCursorRulesCommand
{
    /// <summary>
    /// Führt die Cursor-Regeln-Synchronisation oder Drift-Prüfung aus.
    /// </summary>
    internal static int Run(LinterArgs args, ILintConsole? console = null)
    {
        var c = console ?? ConsoleLintConsole.Instance;
        var config = LinterConfigLoader.TryLoadConfig(args.ConfigPath, isRequired: true);
        if (config == null)
        {
            return 1;
        }

        string baseDir = ResolveBaseDirectory(args.TargetPath);
        var cursorRulesDir = Path.Combine(baseDir, ".cursor", "rules");
        var mdcPath = Path.Combine(cursorRulesDir, "AiNetLinter.mdc");

        var content = CursorRulesGenerator.GenerateContent(config, args.ConfigPath ?? "rules.json");

        if (args.Check)
        {
            return RunCheck(mdcPath, content, c);
        }

        return RunWrite(cursorRulesDir, mdcPath, content, c);
    }

    private static int RunCheck(string mdcPath, string content, ILintConsole c)
    {
        if (!File.Exists(mdcPath))
        {
            c.WriteError(LinterErrorFormatter.Format(LinterErrorCodes.ResourceNotFound,
                "Cursor-Regeldatei existiert nicht.",
                context: mdcPath,
                hint: "Cursor-Regeln mit --sync-cursor-rules (ohne --check) erzeugen."));
            return 1;
        }

        var existing = File.ReadAllText(mdcPath, Encoding.UTF8);
        if (existing != content)
        {
            c.WriteError(LinterErrorFormatter.Format(LinterErrorCodes.DriftDetected,
                "Cursor-Regeln stimmen nicht mit der gespeicherten Datei ueberein.",
                context: mdcPath,
                hint: "Cursor-Regeln mit --sync-cursor-rules (ohne --check) aktualisieren."));
            return 1;
        }

        c.WriteLine("[OK]: Cursor-Regeln sind aktuell.");
        return 0;
    }

    private static int RunWrite(string cursorRulesDir, string mdcPath, string content, ILintConsole c)
    {
        if (!Directory.Exists(cursorRulesDir))
        {
            Directory.CreateDirectory(cursorRulesDir);
        }

        if (File.Exists(mdcPath) && File.ReadAllText(mdcPath, Encoding.UTF8) == content)
        {
            c.WriteLine($"[INFO]: Cursor-Regeldatei ist bereits aktuell (kein Schreibzugriff): {mdcPath}");
            return 0;
        }

        File.WriteAllText(mdcPath, content, Encoding.UTF8);
        c.WriteLine($"[INFO]: Cursor-Regeldatei erfolgreich synchronisiert unter: {mdcPath}");
        return 0;
    }

    internal static string ResolveBaseDirectory(string targetPath)
    {
        if (Directory.Exists(targetPath))
        {
            return targetPath;
        }
        if (File.Exists(targetPath))
        {
            return Path.GetDirectoryName(targetPath) ?? targetPath;
        }
        return targetPath;
    }
}
