#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Configuration;
using AiNetLinter.Output;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Tools.ServerMaintenance;

/// <summary>
/// MCP-Tool <c>reload_config</c>: liest die <c>rules.json</c> zur Laufzeit neu ein und ersetzt
/// die in <see cref="McpCodeGraphServer"/> resident gehaltene Config-Instanz, die
/// <c>get_violations</c> nutzt — und laedt die Solution/Workspace-Kompilierung (inkl.
/// wiederhergestellter NuGet-Metadatenreferenzen) neu ein, ohne Server-Neustart.
/// </summary>
internal static class ReloadConfigTool
{
    /// <summary>
    /// Ohne <paramref name="configPath"/> wird der zuletzt verwendete Pfad
    /// (<see cref="McpCodeGraphServer.ResolvedConfigPath"/>) erneut geladen; lief der Server mit
    /// Default-Regeln, wird — wie beim Server-Start
    /// (<see cref="AiNetLinter.Commands.McpServerCommand.TryResolveRulesJsonPath"/>) — erneut neben
    /// der Solution nach einer inzwischen angelegten <c>rules.json</c> gesucht. Datei fehlt oder ist
    /// ungueltiges JSON: <see cref="McpToolResults.Recoverable"/> (IsErrorPolicy.md) — die aktive
    /// Config bleibt unveraendert, kein Datenverlust, kein Absturz.
    /// </summary>
    internal static async Task<CallToolResult> ExecuteAsync(McpCodeGraphServer state, string? configPath, CancellationToken ct)
    {
        if (state.LoadState == ServerLoadState.Loading) return McpToolResults.Loading();
        var solution = state.GetCurrentSolution();
        if (solution is null) return McpToolResults.SolutionNotLoaded();

        var targetPath = ResolveTargetPath(configPath, state, solution.FilePath);
        if (targetPath is null)
        {
            await state.ReloadSolutionAsync(ct);
            return McpToolResults.Text(
                "Keine rules.json gefunden (weder expliziter configPath noch neben der Solution) — " +
                "Server laeuft weiterhin unveraendert mit den eingebauten Default-Regeln.");
        }

        if (!File.Exists(targetPath))
        {
            return McpToolResults.Recoverable(
                LinterErrorCodes.ConfigNotFound,
                $"Konfigurationsdatei nicht gefunden: {targetPath}",
                context: targetPath,
                hint: "Pfad pruefen (relativ zum Solution-Verzeichnis oder absolut) oder configPath " +
                      "weglassen, um erneut neben der Solution zu suchen. Bisherige Konfiguration bleibt aktiv.");
        }

        var newConfig = ConfigLoader.TryLoadConfig(targetPath, isRequired: false);
        if (newConfig is null)
        {
            return McpToolResults.Recoverable(
                LinterErrorCodes.ConfigInvalid,
                $"Konfigurationsdatei konnte nicht geladen werden (ungueltiges JSON?): {targetPath}",
                context: targetPath,
                hint: "JSON-Syntax der rules.json pruefen. Bisherige Konfiguration bleibt aktiv.");
        }

        var summary = BuildSummary(state, targetPath, newConfig);
        state.ReloadConfig(newConfig, usedDefaultConfig: false, resolvedConfigPath: targetPath);
        await state.ReloadSolutionAsync(ct);
        return McpToolResults.Text(summary);
    }

    /// <summary>
    /// Explizit &gt; zuletzt verwendeter Pfad &gt; frische Auto-Discovery neben der Solution
    /// (wiederverwendet <see cref="AiNetLinter.Commands.McpServerCommand.TryResolveRulesJsonPath"/>,
    /// damit die Discovery-Regel nicht dupliziert wird).
    /// </summary>
    private static string? ResolveTargetPath(string? configPath, McpCodeGraphServer state, string? solutionPath)
    {
        if (!string.IsNullOrWhiteSpace(configPath)) return configPath;
        if (state.ResolvedConfigPath is { } existing) return existing;
        return string.IsNullOrEmpty(solutionPath)
            ? null
            : Commands.McpServerCommand.TryResolveRulesJsonPath(null, solutionPath);
    }

    private static string BuildSummary(McpCodeGraphServer state, string newPath, Config newConfig)
    {
        // Atomarer Schnappschuss statt dreier getrennter Property-Zugriffe: sonst koennte ein
        // gleichzeitiger zweiter reload_config-Aufruf eine zerrissene "Vorher"-Kombination liefern
        // (siehe McpCodeGraphServer.GetConfigSnapshot).
        var (oldConfig, oldUsedDefaultConfig, oldResolvedConfigPath) = state.GetConfigSnapshot();
        var oldDescription = oldUsedDefaultConfig
            ? "Default-Regeln (keine rules.json)"
            : oldResolvedConfigPath ?? "unbekannt";
        var oldEnabledRules = CountEnabledRules(oldConfig.Global);
        var newEnabledRules = CountEnabledRules(newConfig.Global);
        var delta = newEnabledRules - oldEnabledRules;
        var deltaText = delta == 0 ? "unveraendert" : delta > 0 ? $"+{delta}" : delta.ToString();

        return "Config neu geladen.\n" +
               $"- Vorher: {oldDescription} ({oldEnabledRules} aktivierte Regeln)\n" +
               $"- Nachher: {newPath} ({newEnabledRules} aktivierte Regeln, {deltaText})";
    }

    /// <summary>
    /// Grobe, aber wartungsarme Kennzahl fuer "aktivierte Regeln": zaehlt <see langword="true"/>-
    /// Bool-Properties in <see cref="GlobalConfig"/>, deren Name mit einem der bekannten
    /// Regel-Aktivierungs-Praefixe beginnt (<c>Enforce</c>/<c>Ban</c>/<c>Detect</c>/<c>Avoid</c>/
    /// <c>Enable</c>/<c>Prevent</c>). Bewusst ohne die <c>Allow*</c>-Bools (die heben eine
    /// Regel-Ausnahme auf, sind also das Gegenteil von "aktiviert") — eine simple
    /// "alle true-Bools zaehlen"-Heuristik waere hier irrefuehrend.
    /// </summary>
    private static readonly string[] RuleEnablingPrefixes = ["Enforce", "Ban", "Detect", "Avoid", "Enable", "Prevent"];

    private static int CountEnabledRules(GlobalConfig global)
    {
        var count = 0;
        foreach (var prop in typeof(GlobalConfig).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (prop.PropertyType != typeof(bool)) continue;
            if (!RuleEnablingPrefixes.Any(p => prop.Name.StartsWith(p, StringComparison.Ordinal))) continue;
            if ((bool)prop.GetValue(global)!) count++;
        }
        return count;
    }
}
