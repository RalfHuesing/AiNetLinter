---
status: done
type: step-review
task: speedup-tests
step: 027
epic: EPIC-6
step_type: batch
reviewed_by: kritiker
reviewed_by_model: gpt-5.6-sol
reviewed_at: 2026-08-14T12:35:00+02:00
verdict: issues
tech_debt_ids: []
---

# Review Step 027: Git-Workspace-Cleanup und Kategorieguard abschliessen

## Verdict

- [ ] **approved** — alle vier Prüfebenen ok
- [x] **issues** — enge Matrix-Gates fehlen
- [ ] **blocked** — Nutzer-Entscheidung nötig

## Geprüft

- [x] Code- und Doku-Commits `399a463` und `479a7a7`, Scope und `git diff --check`
- [x] Alle drei Step-026-Findings, Rules-Refs und Konzeptgrenzen
- [x] Vorhandene TRX-Evidenz sowie eigene Wiederholung des 13er-Command-Gates
- [x] Kategorieguard-Aufrufer, TD-006-Status und unveränderte Git-Impact-Assertions

## Befund

### Plan-Erfüllung

item-01 bis item-03 sind im Code planmäßig umgesetzt: `FixtureWorkspace` besitzt die einmalige Interlocked-Schablone mit `PrepareForDelete()` und Lease-Dispose im `finally`; die zehn Methoden-Traits fehlen bei erhaltenem Klassen-Trait; beide Kategorieguards delegieren vollständig an die xUnit-freie TestKit-API. Der verpflichtende enge Matrix-Nachweis fehlt aber: `step027-fast-matrix.trx` enthält 318 statt 69 und `step027-integration-matrix.trx` 112 statt erwarteter 64 Tests; entgegen Plan und Result fehlt ein FQN-Diff samt Begründung.

### Rules-Konformität

Die gezielte Ownership bleibt erhalten, es gibt keinen zweiten Löschpfad, keine globale Serialisierung und keine Assertion- oder Allowlist-Abschwächung. Die beiden Git-Impact-Verträge enthalten weiter jeweils `Assert.Contains("CalculatorCaller.cs", ...)`.

### Logische Korrektheit

Der neue doppelte-Dispose-Ursachetest ist in `step027-cleanup-cause.trx` 1/1 grün, und der eigene Lauf `step027-review-command-contracts.trx` bestätigt die gemeinsamen Command-Verträge 13/13 grün. `step027-fast-guards.trx` belegt die zehn Command-Tests plus Kategorie- und beide Dependency-Guards mit 13/13; der eigene Fast- und Integration-Kategorieguard sind je 1/1 grün. Die überbreiten Matrix-TRX sind kein Ersatz für die angeforderten 69/64-Gates, weil die Kohortengleichheit nicht prüfbar ist.

### Konzept-Treue

Der Änderungsscope bleibt auf die Step-026-Korrekturen beschränkt und behauptet die historische Pre-Move-Laufbaseline nicht nachträglich; diese Evidenzlücke bleibt korrekt offen.

## Findings

1. `tasks/speedup-tests/step-027/step-result.md:48-49` — **[MAJOR] [Plan-Erfüllung, items 01–03]** Die verpflichtenden schmalen Matrix-Gates sind nicht belegt: Fast `318/318` statt `69/69`, Integration `112/112` statt `64/64`; der Plan verlangt bei abweichender Discovery einen FQN-Diff gegen Step-026 und eine Begründung, während das Result fälschlich „Keine — Plan 1:1 umgesetzt“ meldet. **Fix:** exakt die Step-026-FQN-Kohorten erneut ausführen (Fast 69; Integration-Kohorte plus Ursachetest und Process-Callsiteguard 64), je eigene TRX schreiben und bei jeder Abweichung FQN-Diff und sachliche Begründung im Result dokumentieren. Keine breiten Ersatzprofile verwenden.

## Build-/Test-Status

```text
Dokumentierter Build: 0 Warnungen / 0 Fehler (nicht wiederholt; seit dem Code-Commit unverändert)
Cleanup-Ursachetest: step027-cleanup-cause.trx → 1/1 grün
Command-Gate: step027-command-contracts.trx → 13/13 grün
Eigene Wiederholung: step027-review-command-contracts.trx → 13/13 grün
Fast Trait-/Guard-Gate: step027-fast-guards.trx → 13/13 grün
Eigene Kategorieguards: Fast 1/1, Integration 1/1 grün
Ledger-/Legacyguards: step027-ledger-guards.trx → 5/5 grün; Result meldet weiterhin 53 pending
Drift-Audit: dokumentiert ohne Kategorieguard-exact-Cluster; beide schlanken Aufrufer delegieren an TestKit
```
