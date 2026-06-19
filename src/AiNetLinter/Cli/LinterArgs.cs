#nullable enable

namespace AiNetLinter.Cli;

/// <summary>
/// Argumente fuer die Ausfuehrung des Linters, die aus den CLI-Optionen geparst werden.
/// </summary>
public sealed class LinterArgs
{
    /// <summary>
    /// Holt oder setzt den Pfad zur optionalen Konfigurationsdatei.
    /// </summary>
    public string? ConfigPath { get; init; }

    /// <summary>
    /// Holt oder setzt den Zielpfad (Datei oder Verzeichnis), der analysiert werden soll.
    /// </summary>
    public required string TargetPath { get; init; }

    /// <summary>
    /// Holt oder setzt einen Wert, der angibt, ob detaillierte Ausgaben (Verbose) protokolliert werden sollen.
    /// </summary>
    public required bool Verbose { get; init; }

    /// <summary>
    /// Holt oder setzt den Pfad, unter dem der Mermaid-Abhaengigkeitsgraph generiert werden soll.
    /// </summary>
    public string? GraphPath { get; init; }

    /// <summary>
    /// Holt oder setzt den Pfad, unter dem das AI-Playbook generiert werden soll.
    /// </summary>
    public string? PlaybookPath { get; init; }

    /// <summary>
    /// Holt oder setzt den Pfad, unter dem eine neue Baseline-Datei erstellt werden soll.
    /// </summary>
    public string? CreateBaselinePath { get; init; }

    /// <summary>
    /// Holt oder setzt den Pfad zur existierenden Baseline-Datei.
    /// </summary>
    public string? BaselinePath { get; init; }

    /// <summary>
    /// Holt oder setzt einen Wert, der angibt, ob nur geaenderte Dateien geprueft werden sollen.
    /// </summary>
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

    /// <summary>
    /// Holt oder setzt die Git-Referenz (z. B. "HEAD~1"), ab der Aenderungen analysiert werden.
    /// </summary>
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
    /// Holt oder setzt einen Wert, der angibt, ob Cursor-Regeldateien (.mdc) automatisch synchronisiert werden sollen.
    /// </summary>
    public bool SyncCursorRules { get; init; }

    /// <summary>
    /// Gibt an, ob nur auf Drift geprueft werden soll, ohne Dateien zu schreiben (gilt fuer --fix, --sync-cursor-rules und --playbook).
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

    /// <summary>
    /// Holt oder setzt den Namen der Klasse fuer eine detaillierte Footprint-Analyse.
    /// </summary>
    public string? Footprint { get; init; }

    /// <summary>
    /// Holt oder setzt einen Wert, der angibt, ob die eingebettete README ausgegeben werden soll.
    /// </summary>
    public bool Readme { get; init; }

    /// <summary>
    /// Gibt an, ob alle bekannten Regeln als Tabelle ausgegeben werden sollen.
    /// </summary>
    public bool ListRules { get; init; }

    /// <summary>
    /// Holt oder setzt die Regel-ID, fuer die eine Beschreibung ausgegeben werden soll.
    /// </summary>
    public string? DescribeRule { get; init; }

    /// <summary>
    /// Holt oder setzt den Suchbegriff fuer die Regelsuche.
    /// </summary>
    public string? SearchRules { get; init; }

    /// <summary>
    /// Validiert Pflicht-Beziehungen zwischen Optionen. Gibt einen Fehlertext zurueck, falls eine Constraint verletzt ist.
    /// </summary>
    public string? Validate()
    {
        if (!Readme && !ListRules && DescribeRule == null && SearchRules == null && string.IsNullOrEmpty(TargetPath))
        {
            return "[ERROR]: --path ist erforderlich (außer bei --readme, --list-rules, --describe-rule, --search-rules).";
        }

        if (HasConflictingModeOptions())
        {
            return "[ERROR]: Wartungsmodi (--create-baseline, --add-disable-all, --remove-disable-all) sind untereinander und mit --baseline nicht kombinierbar.";
        }

        if (OnlyChanged && BaselinePath == null)
        {
            return "[ERROR]: --only-changed erfordert --baseline.";
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
