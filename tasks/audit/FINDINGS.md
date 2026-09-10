# Offene MCP-UX-Befunde

Stand: Live-360-Pruefung des laufenden Servers gegen ein zweites, unkonfiguriertes Source-Target sowie das konfigurierte Referenz-Target.

Diese Datei enthaelt **ausschliesslich noch offene Befunde**. Historisch behobene Befunde wurden bewusst entfernt. Details und Reproduktionen stehen in [LIVE-360-FINDINGS.md](LIVE-360-FINDINGS.md).

| ID | Schweregrad | Bereich | Kurzfassung |
| --- | --- | --- | --- |
| L360-001 | Major | Health | Globaler Health-Call kann mit Detailflag Sessiondetails statt nur Aggregate liefern. |
| L360-002 | Major | Health | Nichtpositive Diagnose-Limits werden im Source-Pfad weiter still ersetzt. |
| L360-003 | Major | Discovery | Namespace-Praefix benoetigt unerwartet einen abschliessenden Punkt. |
| L360-004 | Major | Response-Budget | Mehrere Discovery-/Kontexttools ignorieren `maxResponseBytes`. |
| L360-005 | Major | Metriken | `metrics_tree` begrenzt Text, nicht aber die strukturierte Nutzlast. |
| L360-006 | Major | Symbolsuche | `find_symbol.kind` filtert nicht strikt nach Symbolart. |
| L360-007 | Major | Limits | Mehrere Tools ersetzen oder ignorieren Null-Limits stillschweigend. |
| L360-008 | Major | Chaining | Skeleton- und Feature-Context-Daten sind teilweise nicht strukturiert handoff-faehig. |
| L360-009 | Major | Fehler-UX | Fehlendes `targetPath` und falsche Array-Elementtypen umgehen die feldgenaue Fehlerbehandlung. |
| L360-010 | Minor | Navigation | Health-Snapshots sind nicht mit nachfolgenden Analyse-Snapshots korrelierbar. |
| L360-011 | Minor | Filter | Ungueltige Severity-/Formatwerte werden teilweise still durch Defaults ersetzt. |
| L360-012 | Minor | Clamping | Einige Tools weisen effektive Tiefe und Clamp nicht maschinenlesbar aus. |
| E-003 | Deferred | Vertrag | `navigation.completeness` vermischt bewusst Ergebnis- und Verfuegbarkeitszustand; ein Fix waere breaking. |
