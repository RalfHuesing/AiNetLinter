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
    /// Holt oder setzt den Pfad, unter dem der Mermaid-Abhaengigkeitsgraph generiert werden soll.
    /// </summary>
    public string? GraphPath { get; init; }

    /// <summary>
    /// Holt oder setzt den Pfad, unter dem das AI-Playbook generiert werden soll.
    /// </summary>
    public string? PlaybookPath { get; init; }

    /// <summary>
    /// Holt oder setzt das Ausgabeformat (z. B. "text" oder "sarif").
    /// </summary>
    public required string Format { get; init; }

    /// <summary>
    /// Holt oder setzt einen Wert, der angibt, ob detaillierte Ausgaben (Verbose) protokolliert werden sollen.
    /// </summary>
    public required bool Verbose { get; init; }

    /// <summary>
    /// Holt oder setzt den Pfad, unter dem eine neue Baseline-Datei erstellt werden soll.
    /// </summary>
    public string? CreateBaselinePath { get; init; }

    /// <summary>
    /// Holt oder setzt den Pfad zur existierenden Baseline-Datei.
    /// </summary>
    public string? BaselinePath { get; init; }

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
    /// Holt oder setzt einen Wert, der angibt, ob nur geaenderte Dateien gemaess Baseline oder Git geprueft werden sollen.
    /// </summary>
    public bool OnlyChanged { get; init; }

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
    /// Holt oder setzt einen Wert, der angibt, ob eine Drift-Prüfung ohne Dateischreiben durchgeführt werden soll.
    /// </summary>
    public bool Check { get; init; }

    /// <summary>
    /// Holt oder setzt den Namen der Klasse für eine detaillierte Footprint-Analyse.
    /// </summary>
    public string? Footprint { get; init; }
}
