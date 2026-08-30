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

## TD-004 — Snapshot-Eviction unter konkurrierendem Acquire serialisieren

- Schweregrad: P2
- Scope: `SourceSnapshotRegistry.EvictIdle`
- Evidenz: Der unabhängige Epic-3-Review stellte fest, dass Ressourcen vor
  der Snapshot-Sperre entfernt werden; ein paralleles `Acquire` kann dadurch
  einen Lease erwerben, bevor der Snapshot trotzdem entfernt und disposed
  wird. Aktuell wurde kein produktiver Aufrufer gefunden.
- Disposition: `accepted-deferred`
- Nächster Schritt: bei einer späteren Lifecycle-Härtung Eviction und Acquire
  unter einer gemeinsamen Ownership-/Lease-Entscheidung serialisieren und
  einen Race-Test ergänzen.
- Log-Anker: `execution-log.md` — Epic-3-Korrekturrunde-1-Review

## TD-005 — Source-Ressourcen vor Materialisierung budgetieren

- Schweregrad: P2
- Scope: `ExternalSourceSnapshotMaterializer`, `SourceSnapshotModels`
- Evidenz: Der unabhängige Epic-3-Review stellte fest, dass Source-Ressourcen
  erst nach vollständiger Materialisierung budgetiert werden und ein
  Schätzfehler auf `1,1` zurückfällt; transiente Disk-/Memory-Spitzen sind
  dadurch nicht geschützt.
- Disposition: `accepted-deferred`
- Nächster Schritt: eine belastbare Vorab-Schätzung oder reservierbare
  Streaming-/Rollback-Budgets definieren, ohne die Snapshot-Semantik zu
  verändern.
- Log-Anker: `execution-log.md` — Epic-3-Korrekturrunde-1-Review

## TD-006 — Creation-Barrier-Cancellation mit Consumer-Semantik absichern

- Schweregrad: P2
- Scope: `AssemblySourceSelectionOrchestrator` Creation Barrier
- Evidenz: Der unabhängige Epic-3-Review stellte fest, dass die Barrier das
  Token des ersten Aufrufers verwendet; dessen Cancellation beendet nur den
  Completion-Task. Der vorhandene Test deckt ausschließlich den erfolgreichen
  Join ab.
- Disposition: `accepted-deferred`
- Nächster Schritt: Cancellation-/Abbruchsemantik für den Produzenten und
  wartende Consumer explizit festlegen und mit einem gezielten Test absichern.
- Log-Anker: `execution-log.md` — Epic-3-Korrekturrunde-1-Review
