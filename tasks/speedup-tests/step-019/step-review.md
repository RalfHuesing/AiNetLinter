---
status: done
type: step-review
task: speedup-tests
step: 019
epic: EPIC-4
step_type: batch
reviewed_by: kritiker
reviewed_by_model: gpt-5.6-terra
reviewed_by_model_knowledge_cutoff: nicht ausgewiesen
reviewed_at: 2026-08-13T20:20:00+02:00
verdict: issues
tech_debt_ids: [TD-006, TD-007, TD-008, TD-009, TD-010]
---

# Review Step 019: EPIC-4-Grenze fuer Find-Symbol

## Verdict

- [ ] **approved**
- [x] **issues** — Korrektur-Step erforderlich
- [ ] **blocked**

## Geprüft

- [x] Plan-Erfüllung
- [x] Rules-Konformität
- [x] Logische Korrektheit
- [x] Konzept-Treue
- [x] Dokumentierte Build-/Test-Evidenz

## Befund

### Plan-Erfüllung

Die 20 historischen Methoden wurden vollständig abgeglichen: 11 liegen passend als Unit-/Component-Snapshot- oder Dispatch-Verträge in FastTests, 9 als diskbasierte C#-Leermengen-/Miss-Hint-Verträge in IntegrationTests; beide Legacy-Dateien sind entfernt, Ledger, CodeMap und Roadmap weisen die Zielorte und die offene Audit-Grenze konsistent aus.

### Rules-Konformität

Die drei FastTests sind als Unit bzw. Component kategorisiert und verwenden weder Katalog-/MSBuild-/Prozess-/Repo-Infrastruktur noch eine serialisierende Collection; der Integration-Adapter besitzt genau einen lokalen Catalog-Load und räumt Catalog und Lease deterministisch ab.

### Logische Korrektheit

Die Assertions erhalten Fundformat, Trunkierung, Kindnormalisierung, Invalid-Argument-, Structured-Content-, Compile-Header- und Datei-Fallback-Verträge; lediglich das unten genannte No-Match-Paar ist nach der Migration weiterhin verhaltensidentisch.

### Konzept-Treue (Ebene 4)

Die Teilung entlang In-Memory- und echtem Nicht-C#-Dateifallback respektiert die ausgeschlossenen EPIC-5/6-Kohorten und führt weder eine Produkt-Seam noch einen TestKit-Allzweckhelper ein.

### Build-/Test-Status

Die in `step-result.md` dokumentierte Evidenz ist konsistent: Legacy-Baseline 20, Build 0 Warnungen/Fehler, Fast-Filter 25, Integration-/Ledger-Filter 15 und Component-Grenzgate 289 Tests jeweils grün; wegen keiner Evidenzlücke wurde kein zusätzlicher Audit-Gate-Lauf gestartet.

## Findings

1. item-02 — `src/AiNetLinter.IntegrationTests/Mcp/Tools/FindSymbolFileAdapterTests.cs:37-44` und `:97-104` — [MAJOR] [Plan-Erfüllung/Logik] Beide Methoden materialisieren dieselbe `SymbolGraphMini`-Solution, rufen dieselbe Scanner-Methode mit demselben Pattern auf und enthalten dieselben beiden Assertions. Der frühere Methodenname mit `Tool` bildet keinen Tool-Dispatch ab; auch die historische Quelle rief direkt `FindSymbolScanner.FindMatchesAndFormat` auf. Damit bleibt kein eigenständiger Fehler-, Negativ- oder Formatvertrag erhalten, obwohl der Plan semantische Duplikate zwischen den Legacy-Klassen ausdrücklich zur Konsolidierung zulässt. **Fix:** Eine der beiden Methoden entfernen und im Result/Ledger-Coverage-Audit die semantische Konsolidierung der beiden historischen No-Match-Verträge benennen.

## Tech-Debt-Einträge aus diesem Review

- `TD-006` (siehe `tech-debt.md`) — Cross-Assembly-Kategorieauslesung ist doppelt.
- `TD-007` (siehe `tech-debt.md`) — Zwei lokale Skeleton-Standardkonfigurationen sind identisch.
- `TD-008` (siehe `tech-debt.md`) — Sieben exakte Fast-/Legacy-Helfer bleiben bis zum Strangler-Ende parallel.
- `TD-009` (siehe `tech-debt.md`) — Zwei Integration-Fixtures besitzen denselben Katalog-/Lease-Teardown.
- `TD-010` (siehe `tech-debt.md`) — TestKit- und Legacy-Workspacekopie bleiben bis EPIC-7 doppelt.
