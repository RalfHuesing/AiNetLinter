#nullable enable

using System.IO;
using System.Text;
using AiNetLinter.Cli;
using AiNetLinter.Configuration;
using AiNetLinter.Core;
using AiNetLinter.Generators;
using AiNetLinter.Output;

namespace AiNetLinter.Commands;

/// <summary>
/// Synchronisiert oder prüft die Agent-Regeldateien (.mdc) aus der aktuellen Konfiguration.
/// </summary>
internal static class SyncAgentRulesCommand
{
    /// <summary>
    /// Führt die Agent-Regeln-Synchronisation oder Drift-Prüfung aus.
    /// </summary>
    internal static int Run(LinterArgs args, ILintConsole? console = null)
    {
        var c = console ?? LinterConsole.Instance;
        var config = ConfigLoader.TryLoadConfig(args.ConfigPath, isRequired: true);
        if (config == null)
        {
            return 1;
        }

        string baseDir = ResolveBaseDirectory(args.TargetPath);
        var mdcPath = AgentRulesGenerator.ResolveAgentRulesPath(baseDir, args.AgentRulesPath);
        var agentRulesDir = Path.GetDirectoryName(mdcPath) ?? "";

        var content = AgentRulesGenerator.GenerateContent(config, args.ConfigPath ?? "rules.json");

        if (args.Check)
        {
            return RunCheck(mdcPath, content, c);
        }

        return RunWrite(agentRulesDir, mdcPath, content, c);
    }

    private static int RunCheck(string mdcPath, string content, ILintConsole c)
    {
        if (!File.Exists(mdcPath))
        {
            c.WriteError(LinterErrorFormatter.Format(LinterErrorCodes.ResourceNotFound,
                "Agent-Regeldatei existiert nicht.",
                context: mdcPath,
                hint: "Agent-Regeln mit --sync-agent-rules-only (ohne --check) erzeugen."));
            return 1;
        }

        var existing = File.ReadAllText(mdcPath, Encoding.UTF8);
        if (existing != content)
        {
            c.WriteError(LinterErrorFormatter.Format(LinterErrorCodes.DriftDetected,
                "Agent-Regeln stimmen nicht mit der gespeicherten Datei ueberein.",
                context: mdcPath,
                hint: "Agent-Regeln mit --sync-agent-rules-only (ohne --check) aktualisieren."));
            return 1;
        }

        c.WriteLine("[OK]: Agent-Regeln sind aktuell.");
        return 0;
    }

    private static int RunWrite(string agentRulesDir, string mdcPath, string content, ILintConsole c)
    {
        if (!Directory.Exists(agentRulesDir))
        {
            Directory.CreateDirectory(agentRulesDir);
        }

        if (File.Exists(mdcPath) && File.ReadAllText(mdcPath, Encoding.UTF8) == content)
        {
            c.WriteLine($"[INFO]: Agent-Regeldatei ist bereits aktuell (kein Schreibzugriff): {mdcPath}");
            return 0;
        }

        File.WriteAllText(mdcPath, content, Encoding.UTF8);
        c.WriteLine($"[INFO]: Agent-Regeldatei erfolgreich synchronisiert unter: {mdcPath}");
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
