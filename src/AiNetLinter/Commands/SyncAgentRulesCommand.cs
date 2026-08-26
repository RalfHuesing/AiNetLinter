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
/// Synchronisiert die Agent-Regeldateien (.mdc) aus der aktuellen Konfiguration.
/// </summary>
internal static class SyncAgentRulesCommand
{
    /// <summary>
    /// Führt die Agent-Regeln-Synchronisation aus.
    /// </summary>
    internal static int Run(LinterArgs args, ILintConsole? console = null)
    {
        var c = console ?? LinterConsole.Instance;
        var baseDir = AgentRulesGenerator.ResolveBaseDirectory(
            string.IsNullOrWhiteSpace(args.TargetPath) ? Directory.GetCurrentDirectory() : args.TargetPath);
        var config = LoadConfigForSync(args, baseDir, c);
        if (config == null)
        {
            return 1;
        }

        var mdcPath = AgentRulesGenerator.ResolveAgentRulesPath(baseDir, args.AgentRulesPath);
        var agentRulesDir = Path.GetDirectoryName(mdcPath) ?? "";

        bool hasBaseline = AgentRulesGenerator.DetectBaselineUsage(baseDir, args.BaselinePath);
        var content = AgentRulesGenerator.GenerateContent(config, args.ConfigPath ?? "rules.json", hasBaseline: hasBaseline);

        return RunWrite(agentRulesDir, mdcPath, content, c);
    }

    /// <summary>
    /// Lädt die Konfiguration für den Sync. Ohne <c>--config</c> wird <c>rules.json</c> im
    /// Zielverzeichnis per Auto-Discovery gesucht — damit funktioniert der dokumentierte
    /// Aufruf <c>--sync-agent-rules-only</c> im Repo-Root ohne weitere Argumente, statt mit
    /// der Audit-Fehlermeldung <c>CONFIG_REQUIRED</c> zu scheitern.
    /// </summary>
    private static Config? LoadConfigForSync(LinterArgs args, string baseDir, ILintConsole c)
    {
        if (!string.IsNullOrWhiteSpace(args.ConfigPath))
        {
            return ConfigLoader.TryLoadConfig(args.ConfigPath, isRequired: true);
        }

        var discovered = Path.Combine(baseDir, "rules.json");
        if (!File.Exists(discovered))
        {
            c.WriteError(LinterErrorFormatter.Format(LinterErrorCodes.ConfigNotFound,
                "Keine rules.json gefunden (weder --config noch Auto-Discovery im Zielverzeichnis).",
                context: discovered,
                hint: "--config <pfad> angeben oder im Verzeichnis mit rules.json ausfuehren."));
            return null;
        }

        return ConfigLoader.TryLoadConfig(discovered, isRequired: true);
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
}
