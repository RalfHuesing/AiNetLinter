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
/// MCP-Tool <c>reload_config</c>: liest die <c>ainetlinter-rules.json</c> zur Laufzeit neu ein und ersetzt
/// die in <see cref="McpCodeGraphServer"/> resident gehaltene Config-Instanz, die
/// <c>get_violations</c> nutzt — und laedt die Solution/Workspace-Kompilierung (inkl.
/// wiederhergestellter NuGet-Metadatenreferenzen) neu ein, ohne Server-Neustart.
/// </summary>
internal static class ReloadConfigTool
{
    /// <summary>
    /// Lädt ausschließlich die optionale, zur adressierten Solution benachbarte Regeldatei.
    /// Datei fehlt oder ist ungueltiges JSON:
    /// <see cref="McpToolResults.Recoverable"/> (IsErrorPolicy.md) — die aktive Config bleibt
    /// unveraendert, kein Datenverlust, kein Absturz.
    /// </summary>
    internal static async Task<CallToolResult> ExecuteAsync(
        McpCodeGraphServer state,
        string rulesPath,
        CancellationToken ct)
    {
        if (state.LoadState == ServerLoadState.Loading) return McpToolResults.Loading();
        var solution = state.GetCurrentSolution();
        if (solution is null) return McpToolResults.SolutionNotLoaded();

        if (!File.Exists(rulesPath))
        {
            return McpToolResults.Recoverable(
                LinterErrorCodes.ConfigNotFound,
                $"Optionale Regeldatei nicht gefunden: {rulesPath}",
                context: rulesPath,
                hint: "ainetlinter-rules.json neben der adressierten Solution anlegen. Bisherige Konfiguration bleibt aktiv.");
        }

        var newConfig = ConfigLoader.TryLoadConfig(rulesPath, isRequired: false);
        if (newConfig is null)
        {
            return McpToolResults.Recoverable(
                LinterErrorCodes.ConfigInvalid,
                $"Regeldatei konnte nicht geladen werden (ungueltiges JSON?): {rulesPath}",
                context: rulesPath,
                hint: "JSON-Syntax von ainetlinter-rules.json pruefen. Bisherige Konfiguration bleibt aktiv.");
        }

        var payload = BuildPayload(state, rulesPath, newConfig);
        state.ReloadConfig(newConfig, resolvedConfigPath: rulesPath);
        await state.ReloadSolutionAsync(ct);
        return McpToolResults.Text(BuildSummary(payload), payload);
    }

    private static ReloadConfigPayload BuildPayload(McpCodeGraphServer state, string newPath, Config newConfig)
    {
        // Atomarer Schnappschuss statt dreier getrennter Property-Zugriffe: sonst koennte ein
        // gleichzeitiger zweiter reload_config-Aufruf eine zerrissene "Vorher"-Kombination liefern
        // (siehe McpCodeGraphServer.GetConfigSnapshot).
        var (oldConfig, oldResolvedConfigPath) = state.GetConfigSnapshot();
        var oldDescription = oldResolvedConfigPath ?? "not_configured";
        var oldEnabledRules = oldConfig is null ? 0 : CountEnabledRules(oldConfig.Global);
        var newEnabledRules = CountEnabledRules(newConfig.Global);
        return new ReloadConfigPayload(
            oldDescription,
            newPath,
            oldEnabledRules,
            newEnabledRules,
            newEnabledRules - oldEnabledRules);
    }

    private static string BuildSummary(ReloadConfigPayload payload)
    {
        var deltaText = payload.EnabledRuleDelta == 0
            ? "unveraendert"
            : payload.EnabledRuleDelta > 0
                ? $"+{payload.EnabledRuleDelta}"
                : payload.EnabledRuleDelta.ToString();
        return "Config neu geladen.\n" +
               $"- Vorher: {payload.PreviousConfig} ({payload.PreviousEnabledRuleCount} aktivierte Regeln)\n" +
               $"- Nachher: {payload.ConfigPath} ({payload.EnabledRuleCount} aktivierte Regeln, {deltaText})";
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
