---
status: open
type: step-plan
task: speedup-tests
step: 003
corrects: step-002
title: "Korrektur: Nachweis fuer Ledger-Konsistenzguard nachtragen"
epic: EPIC-1
estimated_risk: low
step_type: single
items: []
created_by: orchestrator
created_by_model: claude-sonnet-5
created_by_model_knowledge_cutoff: 2026-01
created_at: 2026-08-12
related_to: ["tasks/speedup-tests/step-002/step-review.md"]
---

# Step 003: Korrektur — Nachweis fuer Ledger-Konsistenzguard nachtragen

## Bezug

- **Task:** `speedup-tests`
- **Epic:** `EPIC-1` aus `roadmap.md` — dieser Step korrigiert ausschliesslich
  eine Dokumentationslücke aus step-002, ändert nichts am Epic-Fortschritt.
- **Konzept-Referenz:** DoD-Anforderung "Guards nachweislich rot bei
  simulierter Lücke" aus `tasks/speedup-tests/step-002/step-plan.md`.

## Aktueller Projektzustand (JIT-Kontext)

Mechanisches Korrektur-Transkript aus `tasks/speedup-tests/step-002/step-review.md`
(Finding, MAJOR, Ebene Plan-Erfüllung/DoD) — keine eigene Codeanalyse, keine
Interpretation. Der Guard selbst (`TestMigrationLedgerConsistencyTests`) ist laut
Kritiker durch Codelesen als korrekt bestätigt; es fehlt ausschliesslich der
dokumentierte Nachweis eines tatsächlichen roten Laufs.

## Intention

`step-002/step-result.md` soll um den fehlenden Nachweis ergänzt werden, dass
`TestMigrationLedgerConsistencyTests` bei einer simulierten Lücke im Ledger
tatsächlich rot wird. Kein Produktionscode wird geändert.

## Konkrete Änderungen

### Datei 1: `tasks/speedup-tests/test-migration-ledger.md`

- **Was:** Temporär eine beliebige Zeile aus dem Ledger entfernen (Test-Klasse
  aus dem Legacy-Inventar), Test laufen lassen (siehe Tests unten), danach die
  Zeile exakt wiederherstellen (`git diff` muss danach leer sein für diese Datei).
- **Warum:** Nachweis, dass der Konsistenzguard tatsächlich auf eine Lücke reagiert.

### Datei 2: `tasks/speedup-tests/step-002/step-result.md`

- **Was:** Unter „Beobachtungen" oder einem passenden bestehenden Abschnitt 2-3
  Sätze ergänzen: welche Zeile testweise entfernt wurde, welcher Testbefehl
  ausgeführt wurde, dass der Test wie erwartet rot wurde (mit knapper
  Fehlermeldung/Assertion-Text), und dass die Ledger-Datei danach unverändert
  zum committeten Stand zurückgesetzt wurde.
- **Warum:** Schliesst die MAJOR-Finding-Lücke aus `step-002/step-review.md`
  (fehlender DoD-Nachweis).

## Tests

- [ ] `dotnet test src/AiNetLinter.IntegrationTests --filter FullyQualifiedName~TestMigrationLedgerConsistencyTests` — einmal mit testweise entfernter Ledger-Zeile (muss ROT sein), einmal danach mit wiederhergestellter Zeile (muss GRÜN sein)

## Definition of Done

- [ ] Ledger-Guard nachweislich rot bei simulierter Lücke, Nachweis in `step-002/step-result.md` nachgetragen
- [ ] `test-migration-ledger.md` nach dem Test wieder exakt im ursprünglichen (committeten) Zustand — `git diff` für diese Datei leer
- [ ] Guard-Testlauf nach Wiederherstellung wieder grün
- [ ] Commit auf aktuellem Branch (Conventional Commit), Subject-Suffix `[speedup-tests]`
- [ ] `step-003/step-result.md` geschrieben
- [ ] `status` in `step-plan.md` von `in_progress` auf `done (pending audit)` gesetzt

## Rules-Refs

- (keine zusätzlichen — reine Nachweis-/Dokumentationskorrektur, keine neue Regelberührung gegenüber step-002)

## Bekannte Ausnahmen

- Keine.

## Notes

Scope-Disziplin (siehe `../../spec.md` §6.2.1): ausschliesslich das eine
MAJOR-Finding aus `step-002/step-review.md` beheben. Die drei in diesem Review
neu angelegten Tech-Debt-Einträge (TD-001..TD-003) sind explizit **nicht**
Scope dieser Korrektur — sie bleiben in `tech-debt.md` liegen, bis ein
späterer Step in ihrer Nähe vorbeikommt (TD-003 ist `auto_fixable: ja` und
damit für opportunistisches Anhängen an einen künftigen Step vorgesehen,
nicht für diese Korrektur).
