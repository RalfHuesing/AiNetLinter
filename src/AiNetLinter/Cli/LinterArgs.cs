#nullable enable

namespace AiNetLinter.Cli;

/// <summary>
/// Argumente fuer die Ausfuehrung des Linters, die aus den CLI-Optionen geparst werden.
/// </summary>
public sealed class LinterArgs
{
    public string? ConfigPath { get; init; }

    public required string TargetPath { get; init; }

    /// <summary>
    /// Holt oder setzt einen Wert, der angibt, ob detaillierte Ausgaben (Verbose) protokolliert werden sollen.
    /// </summary>
    public required bool Verbose { get; init; }

    public string? CreateBaselinePath { get; init; }

    public string? BaselinePath { get; init; }

    public bool OnlyChanged { get; init; }

    /// <summary>
    /// Holt oder setzt einen Wert, der angibt, ob Deaktivierungskommentare in alle betroffenen Dateien eingefuegt werden sollen.
    /// </summary>
    public bool AddDisableAll { get; init; }

    /// <summary>
    /// Holt oder setzt einen Wert, der angibt, ob alle Deaktivierungskommentare aus den Quelldateien entfernt werden sollen.
    /// </summary>
    public bool RemoveDisableAll { get; init; }

    /// <summary>
    /// Holt oder setzt einen Wert, der angibt, ob die Analyse im Wave-Ready-Modus ausgefuehrt werden soll.
    /// </summary>
    public bool WaveReady { get; init; }

    /// <summary>
    /// Holt oder setzt einen Wert, der angibt, ob gefundene einfache Verstoesse automatisch behoben werden sollen.
    /// </summary>
    public bool Fix { get; init; }

    /// <summary>
    /// Holt oder setzt einen Wert, der angibt, ob Agent-Regeldateien (.mdc) automatisch synchronisiert werden sollen.
    /// </summary>
    public bool SyncAgentRules { get; init; }

    /// <summary>
    /// Holt oder setzt einen Wert, der angibt, ob nur Agent-Regeldateien (.mdc) synchronisiert werden sollen (Fast-Path ohne Audit).
    /// </summary>
    public bool SyncAgentRulesOnly { get; init; }

    /// <summary>
    /// Holt oder setzt den benutzerdefinierten Pfad fuer die Agent-Regeln (.mdc-Datei oder Verzeichnis).
    /// </summary>
    public string? AgentRulesPath { get; init; }

    /// <summary>
    /// Deaktiviert den Analyse-Cache (erzwingt vollständige Neu-Analyse aller Dateien).
    /// </summary>
    public bool NoCache { get; init; }

    /// <summary>
    /// Cache-Lebensdauer in Minuten. 0 = unbegrenzt. Standard: 60.
    /// </summary>
    public int CacheTtlMinutes { get; init; } = 60;

    public string? Docs { get; init; }

    /// <summary>
    /// Gibt an, ob alle bekannten Regeln als Tabelle ausgegeben werden sollen.
    /// </summary>
    public bool ListRules { get; init; }

    public string? DescribeRule { get; init; }

    public string? SearchRules { get; init; }

    /// <summary>
    /// Gibt an, ob statt eines Batch-Laufs ein stdio-basierter MCP-Server (Model Context Protocol) gestartet werden soll.
    /// </summary>
    public bool McpServer { get; init; }

    /// <summary>
    /// Optionale Idle-TTL der Projektregistry in Minuten (Dezimalwerte erlaubt, InvariantCulture,
    /// z. B. 0.05 fuer ca. 3 Sekunden). Ohne Flag gilt der Registry-Default (45 Minuten).
    /// </summary>
    public decimal? McpProjectTtlMinutes { get; init; }

    /// <summary>
    /// Optionale maximale Anzahl residenter Projekt-Keys in der Projektregistry (LRU-Rahmen).
    /// Ohne Flag gilt der Registry-Default (4).
    /// </summary>
    public int? McpMaxProjects { get; init; }

    /// <summary>
    /// Gibt an, ob der interne DaemonHost statt des stdio-MCP-Servers gestartet werden soll.
    /// </summary>
    public bool DaemonStart { get; init; }

    /// <summary>
    /// Positive Idle-Exit-Zeit des internen DaemonHosts in Minuten.
    /// </summary>
    public decimal? McpDaemonIdleExitMinutes { get; init; }

    /// <summary>
    /// Optionale PID des Elternprozesses, dessen Ende den MCP-Server beendet.
    /// Bei <see langword="null"/> wird die PID automatisch ermittelt.
    /// </summary>
    public int? ParentPid { get; init; }

    /// <summary>
    /// Validiert Pflicht-Beziehungen zwischen Optionen. Gibt einen Fehlertext zurueck, falls eine Constraint verletzt ist.
    /// </summary>
    public string? Validate()
    {
        if (McpServer || DaemonStart)
        {
            var mcpError = ValidateMcpMode();
            if (mcpError != null) return mcpError;
        }

        if (IsPathMissing())
        {
            return "[ERROR]: --path ist erforderlich (außer bei --docs, --list-rules, --describe-rule, --search-rules).";
        }

        if (HasConflictingModeOptions())
        {
            return "[ERROR]: Wartungsmodi (--create-baseline, --add-disable-all, --remove-disable-all) sind untereinander und mit --baseline nicht kombinierbar.";
        }

        if (OnlyChanged && BaselinePath == null)
        {
            return "[ERROR]: --only-changed erfordert --baseline.";
        }

        if (ParentPid is <= 0)
        {
            return "[ERROR]: --parent-pid muss eine positive Prozess-ID sein.";
        }

        return null;
    }

    private string? ValidateMcpMode()
    {
        if (McpServer && DaemonStart)
        {
            return "[ERROR]: --mcp-server und --daemon-start koennen nicht gemeinsam verwendet werden.";
        }

        // Harter Cut: im MCP-Modus traegt jeder Aufruf seinen Projektbezug selbst (projectRoot +
        // Definitionsdatei ainetlinter.project.json); --path/--config haben keinen Sinn mehr.
        if (!string.IsNullOrWhiteSpace(TargetPath))
        {
            return "[ERROR]: --path ist im MCP-Modus (--mcp-server) nicht zulaessig. Der Projektbezug " +
                   "kommt je Tool-Aufruf ueber projectRoot aus der Definitionsdatei ainetlinter.project.json im Projektroot.";
        }

        if (!string.IsNullOrWhiteSpace(ConfigPath))
        {
            return "[ERROR]: --config ist im MCP-Modus (--mcp-server) nicht zulaessig. Regeldateien " +
                   "werden je Key aus der Definitionsdatei gelesen; ein Override je Aufruf ist via reload_config moeglich.";
        }

        if (McpProjectTtlMinutes is <= 0)
        {
            return "[ERROR]: --mcp-project-ttl-minutes muss groesser als 0 sein.";
        }

        if (McpMaxProjects is <= 0)
        {
            return "[ERROR]: --mcp-max-projects muss groesser als 0 sein.";
        }

        if (McpDaemonIdleExitMinutes is <= 0)
        {
            return "[ERROR]: --mcp-daemon-idle-exit-minutes muss groesser als 0 sein.";
        }

        return null;
    }

    private bool IsPathMissing()
    {
        return !HasStandaloneCommand() && string.IsNullOrEmpty(TargetPath);
    }

    private bool HasStandaloneCommand() =>
        Docs != null || ListRules || DescribeRule != null || SearchRules != null || McpServer || DaemonStart || SyncAgentRulesOnly;

    private bool HasConflictingModeOptions()
    {
        int count = 0;
        if (CreateBaselinePath != null) count++;
        if (AddDisableAll) count++;
        if (RemoveDisableAll) count++;
        return count > 1 || (BaselinePath != null && count > 0);
    }
}
