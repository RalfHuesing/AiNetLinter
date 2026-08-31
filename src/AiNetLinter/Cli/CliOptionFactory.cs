#nullable enable

using System.CommandLine;
using System.Globalization;
using System.Linq;

namespace AiNetLinter.Cli;

/// <summary>
/// Erzeugt einzelne CLI-Optionen für den AiNetLinter-Einstiegspunkt.
/// </summary>
internal static class CliOptionFactory
{
    // Optionsnamen als Konstanten fuer Stellen, die Flags ohne Option-Objekt uebergeben
    // (z. B. der detached Daemon-Spawn): Parsing und Spawn bleiben so konsistent.
    internal const string DaemonStart = "--daemon-start";
    internal const string McpServer = "--mcp-server";
    internal const string McpProjectTtlMinutes = "--mcp-project-ttl-minutes";
    internal const string McpMaxProjects = "--mcp-max-projects";
    internal const string McpExternalMaxDiskBytes = "--mcp-external-max-disk-bytes";
    internal const string McpExternalMaxMemoryBytes = "--mcp-external-max-memory-bytes";
    internal const string McpExternalMaxParallelOperations = "--mcp-external-max-parallel-operations";
    internal const string McpExternalMaxResidentResources = "--mcp-external-max-resident-resources";
    internal const string McpExternalIdleTtlMinutes = "--mcp-external-idle-ttl-minutes";
    internal const string McpDaemonIdleExitMinutes = "--mcp-daemon-idle-exit-minutes";
    internal const string DaemonInstance = "--daemon-instance";

    internal static Option<string?> CreateConfigOption() => new("--config", "-c")
    {
        Description = "Pfad zur JSON-Konfigurationsdatei (rules.json)",
    };

    internal static Option<string?> CreatePathOption() => new Option<string?>("--path", "-p")
    {
        Description = "Pfad zur Solution-Datei (.sln / .slnx) oder ein Verzeichnis (nicht erforderlich bei --docs)",
    };

    internal static Option<bool> CreateVerboseOption() => new("--verbose", "-v")
    {
        Description = "Detaillierte Protokollausgabe aktivieren",
    };

    internal static Option<string?> CreateBaselineCreateOption() => new("--create-baseline")
    {
        Description = "Erzeugt eine Baseline-JSON mit Datei-Checksummen am angegebenen Pfad",
    };

    internal static Option<string?> CreateBaselineOption() => new("--baseline")
    {
        Description = "Pfad zur Baseline-JSON fuer inkrementelle Migration",
    };

    internal static Option<bool> CreateAddDisableAllOption() => new("--add-disable-all")
    {
        Description = "Audit-Lauf und '// ainetlinter-disable all' nur in Dateien mit Verstoessen einfuegen",
    };

    internal static Option<bool> CreateRemoveDisableAllOption() => new("--remove-disable-all")
    {
        Description = "Entfernt exakte '// ainetlinter-disable all'-Zeilen aus allen .cs-Dateien unter --path",
    };

    internal static Option<bool> CreateWaveReadyOption() => new("--wave-ready")
    {
        Description = "Nur Verstoesse in Dateien ohne '// ainetlinter-disable all'",
    };

    internal static Option<bool> CreateOnlyChangedOption() => new("--only-changed")
    {
        Description = "Nur Verstoesse in geaenderten Dateien (erfordert --baseline)",
    };

    internal static Option<bool> CreateFixOption() => new("--fix")
    {
        Description = "Automatische Behebung einfacher Verstoesse (z. B. sealed, readonly, #nullable enable) direkt ueber die CLI",
    };

    internal static Option<bool> CreateSyncAgentRulesOption() => new("--sync-agent-rules", "-sar")
    {
        Description = "Synchronisiert die rules.json Konfiguration als .agents/rules/AiNetLinter.mdc Datei",
    };

    internal static Option<bool> CreateSyncAgentRulesOnlyOption() => new("--sync-agent-rules-only", "-saro")
    {
        Description = "Synchronisiert die rules.json Konfiguration als .agents/rules/AiNetLinter.mdc Datei und beendet das Programm (schneller Pfad ohne Lint-Lauf)",
    };

    internal static Option<string?> CreateAgentRulesPathOption() => new("--agent-rules-path", "-arp")
    {
        Description = "Benutzerdefinierter Pfad (Verzeichnis oder .mdc-Datei) fuer die Synchronisation der Agent-Regeln (Optional)",
    };

    internal static Option<string?> CreateDocsOption() => new("--docs", "-d")
    {
        Description = "Gibt eine integrierte Dokumentationsdatei aus (Optionen: integration, readme, agent-api, configuration, rationale, roadmap, rules-json, mcp-bootstrap, mcp-rule; case-insensitive). 'mcp-bootstrap' erklaert die einmalige MCP-Projektintegration.",
    };

    internal static Option<bool> CreateListRulesOption() => new("--list-rules")
    {
        Description = "Alle bekannten Regeln als Tabelle ausgeben",
    };

    internal static Option<string?> CreateDescribeRuleOption() => new("--describe-rule")
    {
        Description = "Vollstaendige Beschreibung einer Regel ausgeben (z. B. --describe-rule EnforceNullableEnable)",
    };

    internal static Option<string?> CreateSearchRulesOption() => new("--search-rules")
    {
        Description = "Regeln nach Stichwort durchsuchen (RuleId, Bezeichnung, Warum, Intent)",
    };

    internal static Option<bool> CreateNoCacheOption() => new("--no-cache")
    {
        Description = "Cache deaktivieren — erzwingt vollständige Neu-Analyse aller Dateien.",
    };

    internal static Option<int> CreateCacheTtlOption() => new("--cache-ttl")
    {
        Description = "Cache-Lebensdauer in Minuten (0 = unbegrenzt). Standard: 60.",
        DefaultValueFactory = _ => 60,
    };

    internal static Option<bool> CreateMcpServerOption() => new(McpServer)
    {
        Description = "Startet einen stdio-basierten MCP-Server ohne eigenen Projektbezug: Jeder zielgebundene Tool-Aufruf adressiert per targetType und absolutem targetPath ein Projekt oder eine lokale Assembly.",
    };

    internal static Option<bool> CreateDaemonStartOption() => new(DaemonStart)
    {
        Description = "[internal] Startet den lokalen DaemonHost fuer Named-Pipe-Verbindungen.",
    };

    internal static Option<int?> CreateParentPidOption() => new("--parent-pid")
    {
        Description = "Optionale PID des Elternprozesses fuer den MCP-Lebenszyklus-Watchdog. Ohne diese Option wird die Parent-PID automatisch ermittelt.",
    };

    internal static Option<decimal?> CreateMcpProjectTtlOption() =>
        CreateInvariantDecimalOption(
            McpProjectTtlMinutes,
            "Optionale Idle-TTL der Projekt-Registry in Minuten (Dezimalwerte, InvariantCulture, z. B. 0.05 fuer ca. 3 Sekunden). Ohne Flag gilt der Default von 45 Minuten.");

    internal static Option<int?> CreateMcpMaxProjectsOption() => new(McpMaxProjects)
    {
        Description = "Optionale maximale Anzahl residenter Projekt-Keys in der Projekt-Registry (LRU-Rahmen). Ohne Flag gilt der Default von 4.",
    };

    internal static Option<long?> CreateMcpExternalMaxDiskBytesOption() => new(McpExternalMaxDiskBytes)
    {
        Description = "Optionale maximale externe Diskbelegung in Bytes (positiver Wert). Ohne Flag gilt ExternalSources:MaxDiskBytes oder der Default von 512 MiB.",
    };

    internal static Option<long?> CreateMcpExternalMaxMemoryBytesOption() => new(McpExternalMaxMemoryBytes)
    {
        Description = "Optionale maximale externe Speicherbelegung in Bytes (positiver Wert). Ohne Flag gilt ExternalSources:MaxMemoryBytes oder der Default von 512 MiB.",
    };

    internal static Option<int?> CreateMcpExternalMaxParallelOperationsOption() => new(McpExternalMaxParallelOperations)
    {
        Description = "Optionale maximale Zahl paralleler externer Creation-/Materialisierungsoperationen (positiver Wert).",
    };

    internal static Option<int?> CreateMcpExternalMaxResidentResourcesOption() => new(McpExternalMaxResidentResources)
    {
        Description = "Optionale maximale Zahl residenter externer Assembly-/Snapshot-Ressourcen (positiver Wert).",
    };

    internal static Option<decimal?> CreateMcpExternalIdleTtlOption() =>
        CreateInvariantDecimalOption(
            McpExternalIdleTtlMinutes,
            "Optionale Idle-TTL externer Assembly-/Snapshot-Ressourcen in Minuten (positive Dezimalwerte, InvariantCulture). Ohne Flag gilt ExternalSources:IdleTtlMinutes oder der Default von 45 Minuten.");

    internal static Option<decimal?> CreateMcpDaemonIdleExitOption() =>
        CreateInvariantDecimalOption(
            McpDaemonIdleExitMinutes,
            "Idle-Exit des internen DaemonHosts in Minuten (positive Dezimalwerte, InvariantCulture). Ohne Flag gilt der Default von 10 Minuten.");

    internal static Option<string?> CreateDaemonInstanceOption()
    {
        var option = new Option<string?>(DaemonInstance)
        {
            Description = "Optionale isolierte Daemon-Instanz (ASCII-ID, maximal 32 Zeichen; nur im MCP-/Daemon-Modus).",
        };
        option.CustomParser = result =>
        {
            var value = result.Tokens.SingleOrDefault()?.Value;
            var error = Mcp.Daemon.DaemonInstanceId.Validate(value);
            if (error is not null)
            {
                result.AddError($"{DaemonInstance} {error}.");
            }

            return error is null ? Mcp.Daemon.DaemonInstanceId.Normalize(value) : value;
        };
        return option;
    }

    private static Option<decimal?> CreateInvariantDecimalOption(string name, string description)
    {
        var option = new Option<decimal?>(name)
        {
            Description = description,
        };
        option.CustomParser = result =>
        {
            var raw = result.Tokens.SingleOrDefault()?.Value;
            if (decimal.TryParse(
                    raw,
                    NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                    CultureInfo.InvariantCulture,
                    out var value))
            {
                return value;
            }

            result.AddError("Wert muss eine Dezimalzahl im InvariantCulture-Format sein.");
            return null;
        };
        return option;
    }
}
