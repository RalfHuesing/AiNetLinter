# Task-lokales Tech-Debt-Register

Dieses Register enthält nur actionable Minor-/P2-/P3-Befunde mit bewusster
Disposition. P0-/P1-Befunde bleiben im `roadmap.md`-Blocker und im
`execution-log.md`; kosmetische oder unbelegte Vorschläge werden dort nur im
Bericht festgehalten.

## TD-001 — Windows-Git-Prozess-Tests stabilisieren

- Schweregrad: P2
- Scope: `ExternalSourceGitProcessExecutorTests`
- Evidenz: breiterer Epic-2-Integrationslauf mit zwei Fehlern wegen
  Windows-Zugriffsrechten bzw. Prozess-Timeouts; Test-/Produktionsbereich lag
  außerhalb des Epic-2-Diffs.
- Disposition: `accepted-deferred`
- Nächster Schritt: Testumgebung und Prozessberechtigungen isoliert prüfen und
  den Git-Prozess-Test deterministisch machen, ohne den Assembly-Analyse-Scope
  auszuweiten.
- Log-Anker: `execution-log.md` — Epic-2-Review abgeschlossen

## TD-002 — Bestehenden ProjectRegistry-FastTest prüfen

- Schweregrad: P2
- Scope: `ProjectRegistryTests`, vollständiger FastTests-Non-Stress-Lauf
- Evidenz: Epic-3-Implementierer meldete 2216/2219 erfolgreiche Tests; der
  Fehler lag in einem unveränderten ProjectRegistry-Test außerhalb des Epic-
  3-Diffs, zwei bekannte Reparse-Tests wurden übersprungen.
- Disposition: `accepted-deferred`
- Nächster Schritt: beim Abschluss-Gate reproduzieren; bei erneutem Auftreten
  die Testisolierung bzw. Windows-Umgebungsabhängigkeit separat beheben.
- Log-Anker: `execution-log.md` — Epic-3-Implementierer abgeschlossen

## TD-003 — Diagnostische Magic-Value-Kandidaten bewerten

- Schweregrad: P3
- Scope: Assembly-Analysis-Scope
- Evidenz: `find_magic_values` meldete sieben diagnostische/Identifier-
  Kandidaten ohne sichere scope-nahe Refactoring-Korrektur.
- Disposition: `accepted-deferred`
- Nächster Schritt: erst im Abschluss-Audit prüfen, ob fachlich identische
  Werte tatsächlich eine gemeinsame Konstante benötigen; Diagnosecodes,
  Identifier und Wire-Verträge nicht pauschal zentralisieren.
- Log-Anker: `execution-log.md` — Epic-3-Implementierer abgeschlossen und
  Epic-3-Review abgeschlossen
