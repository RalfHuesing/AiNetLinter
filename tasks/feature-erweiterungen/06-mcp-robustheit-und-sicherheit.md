# 06 – MCP-Robustheit und Sicherheit

**Priorität:** P1  
**Zielstatus:** umsetzen/gegenprüfen  
**Abhängigkeiten:** alle anderen Teilkonzepte

## Ziel und Nutzen

Die neuen Kontext- und Inferenzfähigkeiten dürfen den lokalen MCP-Dienst weder durch große Eingaben, veraltete Zustände, fehlerhafte Pfade noch konkurrierende Sessions unzuverlässig machen.

## Pfadsicherheit

- Jeder Nutzerpfad wird gegen den freigegebenen Zielroot geprüft.
- Relative Pfade, .., Symlinks und Junctions dürfen keine Root-Umgehung ermöglichen.
- Sensible System- und Benutzerverzeichnisse bleiben geschützt.
- Pfadprüfungsfehler sind echte Toolfehler und werden nicht als leere Analyse maskiert.
- Pfade werden kanonisch und konsistent serialisiert.

## Eingabesicherheit

- Query-, Symbol-, Datei-, Tiefen-, Limit- und Budgetparameter besitzen harte Obergrenzen.
- Ungültige Kombinationen werden früh und erklärbar abgewiesen.
- Nutzeroptionen dürfen interne Sicherheitslimits nicht überschreiben.
- Fortsetzungstoken werden validiert und an Ziel und Snapshot gebunden.

## Session- und Daemon-Semantik

- Mehrere Sessions teilen nur bewusst freigegebene, snapshotkonsistente Projektzustände.
- Ein Refresh bringt fremde Sessions nicht in einen inkonsistenten Mischzustand.
- Locks, Idle-Lebensdauer, Worker und Cleanup folgen dem bestehenden Daemonmodell.
- Ein beendeter Client hinterlässt keine dauerhaft blockierende Ressource.
- Ein Fehler in einer Anfrage macht den Projektkontext nicht unbrauchbar.

## Fehlerklassen

Mindestens zu unterscheiden:

- ungültige oder nicht erlaubte Eingabe;
- Ziel nicht geladen oder Snapshot nicht bereit;
- verwertbares, aber unvollständiges Analyseergebnis;
- veralteter oder degradierter Analysezustand;
- interne Tool- oder Infrastrukturfehlfunktion.

Diese Klassen bleiben mit dem vorhandenen isError-/Recoverable-Modell kompatibel.

## Akzeptanzkriterien

- Traversal- und Symlink-Angriffe werden für alle relevanten Tools abgewehrt.
- Große Eingaben führen zu kontrollierten Antworten und nicht zu unbounded Speicher- oder CPU-Verbrauch.
- Parallel laufende Sessions liefern keine vermischten Snapshotdaten.
- Refresh- und Worker-Fehler sind recoverable, sofern ein sicherer Fallback existiert.
- Echte Sicherheits- und Infrastrukturfehler werden nicht als erfolgreiche leere Antwort verschleiert.
- Security- und Lifecycle-Tests laufen im normalen Nicht-Stress-Abschlusslauf.

## Verifikation

- Pfadtraversal-, Symlink-, Junction- und sensible-Root-Fixtures.
- Parametergrenzwert- und Missbrauchstests.
- Mehrsession- und Refresh-Konflikttests.
- Worker-/Daemon-Abbruch und Wiederanlauf.
- Prüfung, dass keine Quelltextinhalte, Konfigurationswerte oder Geheimnisse in Status- und Fehlerdaten geraten.

