#nullable enable

namespace AiNetLinter.Mcp;

/// <summary>
/// Drei-Zustands-Lebenszyklus des Solution-Loads im MCP-Server. Ergaenzt den bisherigen
/// binaeren "Solution ist geladen oder nicht"-Indikator um den transienten Wartezustand
/// waehrend des Hintergrund-Loads, damit Tool-Aufrufe waehrend dieser Zeitspanne eine
/// sprechende Antwort liefern koennen (kein leerer Fehler, kein "Found 0 results").
/// Reihenfolge der Enum-Werte entspricht der zeitlichen Abfolge.
/// </summary>
public enum ServerLoadState
{
    /// <summary>Hintergrund-Load laeuft, Tool-Aufrufe muessen warten.</summary>
    Loading,

    /// <summary>Load erfolgreich abgeschlossen, <see cref="McpCodeGraphServer.GetCurrentSolution"/> liefert die Loesung.</summary>
    Loaded,

    /// <summary>Load fehlgeschlagen oder abgebrochen, Server laeuft ohne Solution.</summary>
    LoadFailed,
}
