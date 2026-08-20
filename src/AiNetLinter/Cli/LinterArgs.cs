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

    public string? PlaybookPath { get; init; }

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
    /// Holt oder setzt einen Wert, der angibt, ob ein Bericht ueber die technische Schuld ausgegeben werden soll.
    /// </summary>
    public bool DebtReport { get; init; }

    /// <summary>
    /// Holt oder setzt einen Wert, der angibt, ob die Analyse im Wave-Ready-Modus ausgefuehrt werden soll.
    /// </summary>
    public bool WaveReady { get; init; }

    public string? GitSince { get; init; }

    /// <summary>
    /// Holt oder setzt einen Wert, der angibt, ob gefundene einfache Verstoesse automatisch behoben werden sollen.
    /// </summary>
    public bool Fix { get; init; }

    /// <summary>
    /// Holt oder setzt einen Wert, der angibt, ob eine semantische Diff-Impact-Analyse ausgefuehrt werden soll.
    /// </summary>
    public bool HasImpact { get; init; }

    /// <summary>
    /// Holt oder setzt die optionale Git-Referenz, die fuer die Diff-Impact-Analyse genutzt wird.
    /// </summary>
    public string? ImpactRef { get; init; }

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
    /// Gibt an, ob nur auf Drift geprueft werden soll, ohne Dateien zu schreiben (gilt fuer --fix, --sync-agent-rules und --playbook).
    /// </summary>
    public bool Check { get; init; }

    /// <summary>
    /// Deaktiviert den Analyse-Cache (erzwingt vollständige Neu-Analyse aller Dateien).
    /// </summary>
    public bool NoCache { get; init; }

    /// <summary>
    /// Cache-Lebensdauer in Minuten. 0 = unbegrenzt. Standard: 60.
    /// </summary>
    public int CacheTtlMinutes { get; init; } = 60;

    public string? Footprint { get; init; }

    public string? Docs { get; init; }

    /// <summary>
    /// Gibt an, ob alle bekannten Regeln als Tabelle ausgegeben werden sollen.
    /// </summary>
    public bool ListRules { get; init; }

    public string? DescribeRule { get; init; }

    public string? SearchRules { get; init; }

    /// <summary>
    /// Filtert die Analyse auf bestimmte Projektnamen (kommagetrennt, Glob-Muster erlaubt).
    /// </summary>
    public System.Collections.Generic.IReadOnlyList<string> IncludeProjects { get; init; } = [];

    /// <summary>
    /// Schließt bestimmte Projekte von der Analyse aus (kommagetrennt, Glob-Muster erlaubt).
    /// </summary>
    public System.Collections.Generic.IReadOnlyList<string> ExcludeProjects { get; init; } = [];

    /// <summary>
    /// Filtert die Analyse auf bestimmte C#-Namespaces (kommagetrennt, Glob-Muster erlaubt).
    /// </summary>
    public System.Collections.Generic.IReadOnlyList<string> IncludeNamespaces { get; init; } = [];

    /// <summary>
    /// Schließt bestimmte Namespaces aus (kommagetrennt, Glob-Muster erlaubt).
    /// </summary>
    public System.Collections.Generic.IReadOnlyList<string> ExcludeNamespaces { get; init; } = [];

    /// <summary>
    /// Shortcut, um alle automatisch erkannten Testprojekte auszublenden.
    /// </summary>
    public bool ExcludeTests { get; init; }

    /// <summary>
    /// Shortcut, um ausschließlich Testprojekte zu analysieren.
    /// </summary>
    public bool TestsOnly { get; init; }

    /// <summary>
    /// Blendet private und protected Member in Maps (wie skeleton) aus, um Token zu sparen.
    /// </summary>
    public bool PublicOnly { get; init; }

    /// <summary>
    /// Holt oder setzt die Sprachen, für die Suppressions während der Analyse ignoriert werden sollen (null = nicht aktiv).
    /// </summary>
    public System.Collections.Generic.IReadOnlyList<string>? IgnoreSuppressions { get; init; }

    /// <summary>
    /// Gibt an, ob statt eines Batch-Laufs ein stdio-basierter MCP-Server (Model Context Protocol) gestartet werden soll.
    /// </summary>
    public bool McpServer { get; init; }

    /// <summary>
    /// Optionaler Pfad fuer das MCP-Call-Log (JSONL-Format, ein Eintrag pro Tool-Call). <c>null</c> = Log deaktiviert (Default). Wert = expliziter
    /// Pfad (absolut -> wie angegeben; relativ -> relativ zum Solution-Verzeichnis). Leerer/Whitespace-Wert = Default-Pfad unter
    /// <c>&lt;exeDir&gt;/logs/&lt;solutionName&gt;/&lt;yyyy-MM-dd&gt;/calls.jsonl</c>; erfordert eine aufloesbare Solution, sonst Abbruch mit Exit ungleich 0.
    /// </summary>
    public string? McpLogPath { get; init; }

    /// <summary>
    /// Optionale PID des Elternprozesses, dessen Ende den MCP-Server beendet.
    /// Bei <see langword="null"/> wird die PID automatisch ermittelt.
    /// </summary>
    public int? ParentPid { get; init; }

    /// <summary>
    /// Liefert die normalisierten und kanonischen Sprach-Identifier für --ignore-suppressions (z. B. 'c#' -> 'cs').
    /// </summary>
    public System.Collections.Generic.IReadOnlyList<string> GetNormalizedIgnoreSuppressions()
    {
        if (IgnoreSuppressions == null || IgnoreSuppressions.Count == 0) return System.Array.Empty<string>();

        var set = new System.Collections.Generic.HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        foreach (var item in IgnoreSuppressions)
        {
            if (string.IsNullOrWhiteSpace(item)) continue;
            var token = item.Trim().ToLowerInvariant();
            if (token == "c#") token = "cs";
            set.Add(token);
        }

        if (set.Contains("all"))
        {
            return new[] { "all" };
        }

        var result = new System.Collections.Generic.List<string>();
        foreach (var lang in new[] { "cs", "razor", "js", "css" })
        {
            if (set.Contains(lang)) result.Add(lang);
        }
        return result;
    }

    /// <summary>
    /// Validiert Pflicht-Beziehungen zwischen Optionen. Gibt einen Fehlertext zurueck, falls eine Constraint verletzt ist.
    /// </summary>
    public string? Validate()
    {
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

        return ValidateIgnoreSuppressions();
    }

    private bool IsPathMissing()
    {
        return !HasStandaloneCommand() && string.IsNullOrEmpty(TargetPath);
    }

    private bool HasStandaloneCommand() =>
        Docs != null || ListRules || DescribeRule != null || SearchRules != null || McpServer || SyncAgentRulesOnly;

    private string? ValidateIgnoreSuppressions()
    {
        if (IgnoreSuppressions == null) return null;

        var allowed = new System.Collections.Generic.HashSet<string>(System.StringComparer.OrdinalIgnoreCase) { "all", "cs", "c#", "razor", "js", "css" };
        foreach (var lang in IgnoreSuppressions)
        {
            if (string.IsNullOrWhiteSpace(lang) || !allowed.Contains(lang.Trim()))
            {
                return $"[ERROR]: Ungueltige Sprache fuer --ignore-suppressions: '{lang}'. Erlaubte Werte: all, cs, c#, razor, js, css.";
            }
        }
        return null;
    }

    private bool HasConflictingModeOptions()
    {
        int count = 0;
        if (CreateBaselinePath != null) count++;
        if (AddDisableAll) count++;
        if (RemoveDisableAll) count++;
        return count > 1 || (BaselinePath != null && count > 0);
    }
}
