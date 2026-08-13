---
status: done
type: step-review
task: speedup-tests
step: 026
epic: EPIC-6
step_type: batch
reviewed_by: kritiker
reviewed_by_model: gpt-5.6
reviewed_by_model_knowledge_cutoff: nicht ausgewiesen
reviewed_at: 2026-08-13T23:52:28+02:00
verdict: issues
tech_debt_ids: []
---

# Review Step 026: Runtime-sauberer MCP-Vertragsschnitt und vollständiger Hostabschluss

## Verdict

- [ ] **approved** — alle vier Prüfebenen ok
- [x] **issues** — Korrektur erforderlich; auf Nutzerwunsch keine Folgeplanung in diesem Durchgang
- [ ] **blocked** — Nutzer-Entscheidung nötig

## Geprüft

- [x] Plan-Erfüllung einschließlich der 21 historischen Klassen, der 66/55-Matrix und der ausgeschlossenen Familien
- [x] Rules-Refs einschließlich Kategorie-/Guard- und Kommentarvorgaben
- [x] Logische Korrektheit von Hostownership, Retry, Framing, Runner und TRX-Ausgängen
- [x] Konzept-Treue: Fast/Integration-Grenze und keine nachträgliche Pre-Move-Baselinebehauptung
- [x] Commit-Scope von `06fdc20` und `1e20391`, `git diff --check` der beiden Commits
- [x] Drift-Audit mit `find_duplicates(scopeDir="src", minTokens=20)`

## Befund

### Plan-Erfüllung

Die Fast/Integration-Aufteilung, die 121er-Matrix (66 Fast/55 Integration), das Ledger mit 53 pending und die ehrliche Notiz zur fehlenden Pre-Move-Laufbaseline sind dokumentiert; der geforderte 13er-Command-Vertragslauf ist jedoch in der vorhandenen TRX rot und verhindert die zugesagte vollständige Gate-Evidenz.

### Rules-Konformität

`McpServerCommandTests` enthält zusätzlich zum Klassen-Trait weiterhin zehn gleichwertige Methoden-Traits; dies widerspricht der Step-Vorgabe, falsche Methoden-Traits zu entfernen, statt den Klassen-Trait zu verdoppeln. Das Drift-Audit fand außerdem einen exact-Cluster der beiden `EveryTestClass_HasExactlyOneValidCategoryTrait`-Methoden; TD-006 darf daher nicht als vollständig geschlossen gelten, solange die gemeinsame Prüflogik nicht zentralisiert oder die Schuld nachvollziehbar offen dokumentiert ist.

### Logische Korrektheit

`TestResults/step026-command-contracts.trx` endet mit 11/13, nicht 13/13: beide Git-Impact-Command-Verträge scheitern im `GitImpactMiniFixtureWorkspace.Dispose()` mit `DirectoryNotFoundException` an `FixtureWorkspaces.cs:69`; die Host-/Workspace-Ownership ist damit für diesen Vertragsweg nicht deterministisch. Die späteren Einzel-TRX `step026-command-git-contracts.trx` (2/2) ersetzen den fehlgeschlagenen gemeinsamen Vertragslauf nicht.

### Konzept-Treue (Ebene 4)

Die physische Grenze ist ansonsten konzepttreu: der MSBuild-ladende `RunAsync`-Warnvertrag liegt in Integration, Fast bleibt durch die vorhandenen Guard-TRX sauber, und die Result-Dokumentation behauptet die fehlende Pre-Move-Baseline nicht als Lauf.

### Build-/Test-Status

```text
Dokumentiertes Pure-Command-Gate: step026-cause-fast.trx → grün (12/12)
Dokumentiertes Fast-Zielgate: Result 69/69; vorliegende Teil-TRX fast-contracts/guards → grün (14/14, 3/3)
Dokumentiertes Retry-Gate: kein zugehöriges TRX vorliegend; Result meldet 2/2
Dokumentiertes Integration-Gate: step026-command-contracts.trx → fehlgeschlagen (11/13)
Dokumentiertes Command-Git-Nachlaufgate: step026-command-git-contracts.trx → grün (2/2)
Dokumentiertes Integration-Teilgate: step026-integration-contracts/guards.trx → grün (18/18, 2/2)
Dokumentierte Framing-Wiederholung: Result meldet 3/3 dreifach; kein separates Framing-TRX vorliegend
Dokumentierter Build: Result meldet 0 Warnungen/0 Fehler; nicht selbst erneut ausgeführt (Vorgabe)
```

## Findings

1. `src/AiNetLinter.IntegrationTests/Fixtures/FixtureWorkspaces.cs:69`, aufgerufen durch `src/AiNetLinter.IntegrationTests/Mcp/McpServerCommandContractTests.cs:101` und `:111` — **[CRITICAL] [item-02/item-03, Plan-Erfüllung und Logik]** Der relevante gemeinsame Command-Vertragslauf ist nachweislich rot: beide wiederhergestellten Git-Impact-Hostverträge enden während der Workspace-Freigabe mit `DirectoryNotFoundException`; `step026-command-contracts.trx` enthält 11/13 und widerspricht damit dem als grün dokumentierten Abschluss. **Fix:** Workspace-/Host-Ownership und idempotentes Cleanup so korrigieren, dass beide Verträge im gemeinsamen 13er-Filter ohne nachgelagerten Dispose-Fehler laufen; anschließend genau diesen Filter erneut mit eigener TRX ausführen und die Gate-Angabe berichtigen.
2. `src/AiNetLinter.FastTests/Mcp/McpServerCommandTests.cs:17` bis `:212` — **[MAJOR] [item-04, Plan-Erfüllung/Rules]** Neben dem Klassen-`[Trait("Category", "Unit")]` stehen an allen zehn Methoden derselbe Kategorie-Trait. Der Plan fordert ausdrücklich, solche Methoden-Traits zu entfernen, statt den Klassen-Trait zu verdoppeln; der Klassen-basierte Inspector kann diese Doppelung nicht erkennen. **Fix:** Die redundanten Methoden-Traits entfernen und den engen Kategorie-/Fast-Guard erneut belegen.
3. `src/AiNetLinter.FastTests/Architecture/TestCategoryProfileGuardTests.cs:23` und `src/AiNetLinter.IntegrationTests/Architecture/TestCategoryProfileGuardTests.cs:22` — **[MAJOR] [item-04, Rules]** Das durchgeführte Drift-Audit liefert für beide Methoden einen exact-Cluster (Score 1,00, 133 Tokens). TD-006 ist als geschlossen markiert, obwohl die gemeinsame Validierungslogik weiterhin doppelt implementiert ist. **Fix:** Validierungslogik in TestKit zentralisieren und beide Guards darauf reduzieren; falls eine technische Grenze dies ausschließt, TD-006 wieder öffnen und die begründete Restduplikation dokumentieren.

## Sonstige Beobachtungen / MINOR / NITPICK

- Die beiden Kategorieguard-Dateien enthalten Kommentare mit Task-Artefaktverweisen (`konzept.md`, Leitplanke); diese sollten bei der Korrektur auf eine zeitstabile technische Begründung reduziert werden.
- Die exact-Cluster mit Legacy-/Strangler-Helpern und die geprüften near-Cluster betreffen entweder bewusst verbleibende pending-Familien oder fachlich unterschiedliche Testfälle; daraus folgt kein weiterer Step-026-Fund.
