---
status: done
type: step-plan
task: find-dead-code
step: 004
corrects: null
title: "Erweiterte Testsuite & Live-Dogfooding-Verifikation"
epic: EPIC-04
estimated_risk: low
step_type: single
items: []
created_by: planer
created_by_model: gemini-2.5-pro
created_by_model_knowledge_cutoff: 2026-01
created_at: 2026-08-17T17:40:30+02:00
related_to: []
---

# Step 004: Erweiterte Testsuite & Live-Dogfooding-Verifikation

## Bezug

- **Task:** `find-dead-code`
- **Epic:** `EPIC-04` aus `roadmap.md` — Testsuite & Integration-Verifikation
- **Konzept-Referenz:** `konzept.md` §3.7 Test-Konzept, §Definition of Done

## Aktueller Projektzustand (JIT-Kontext)

- `FindDeadCodeScanner`, `FindDeadCodeDiagnosticsScanner`, `DeadCodeFilters`, `DeadCodeModels`, `DeadCodeWhitelist` und `FindDeadCodeTool` sind vollständig implementiert.
- FastTests und IntegrationTests sind alle grün.

## Intention

Vollständige Absicherung aller Sonder- und Randfälle (Pagination/MaxResults, Scope-Filter, Attribute-Whitelisting wie `[JsonConstructor]`, Events & Delegates, Filter-Kombinationen) in `AiNetLinter.FastTests` sowie Live-Dogfooding-Integrationstest in `AiNetLinter.IntegrationTests/Mcp/McpLiveRepositoryTests.cs`. Durchführung des Drift-Audits (`find_duplicates`).

## Konkrete Änderungen

### Datei 1: `src/AiNetLinter.FastTests/Mcp/Tools/DeadCode/FindDeadCodeScannerTests.cs` (Erweiterung)

- **Was:**
  - Tests für Pagination / MaxResults Truncation (`isTruncated`).
  - Tests für ScopeFilter (relative Pfade / Projektfilter).
  - Tests für Whitelist-Attribute (`[JsonConstructor]`, `[Benchmark]`).
  - Tests für `event` und `delegate` Dead-Code-Erkennung.
- **Warum:** Lückenlose Abdeckung der Akzeptanzkriterien aus `konzept.md` §3.7.

### Datei 2: `src/AiNetLinter.IntegrationTests/Mcp/McpLiveRepositoryTests.cs` (Erweiterung)

- **Was:** Neuer Dogfooding-Test `LiveDogfood_FindDeadCode_WithScopeFilter_ReturnsResults` gegen das reale Repository.
- **Warum:** End-to-End-Verifikation des MCP-Tools `find_dead_code` im Live-Serverbetrieb.

## Tests

- [ ] `FindDeadCodeScannerTests.ScanAsync_WithMaxResults_TruncatesAndSetsFlag`
- [ ] `FindDeadCodeScannerTests.ScanAsync_WithScopeFilter_LimitsToMatchingFiles`
- [ ] `FindDeadCodeScannerTests.ScanAsync_JsonConstructorAttribute_IsWhitelisted`
- [ ] `FindDeadCodeScannerTests.ScanAsync_UnusedEventAndDelegate_DetectedAsDeadCode`
- [ ] `McpLiveRepositoryTests.LiveDogfood_FindDeadCode_WithScopeFilter_ReturnsResults`

## Definition of Done

- [ ] Alle „Konkrete Änderungen" umgesetzt
- [ ] Drift-Audit über `find_duplicates` ausgeführt
- [ ] Build-Command aus Tech-Stack-Notiz (`roadmap.md`) grün (`dotnet build`)
- [ ] Test-Command aus Tech-Stack-Notiz grün (`dotnet test src/AiNetLinter.FastTests --filter Category!=Stress && dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress`)
- [ ] 0 Linter-Violations (`get_violations`)
- [ ] Commit auf aktuellem Branch (Conventional Commit `test(deadcode): Erweiterte Testsuite und Live-Dogfooding ergaenzen [find-dead-code]`)
- [ ] `tasks/find-dead-code/step-004/step-result.md` geschrieben
- [ ] `status` in `step-plan.md` auf `done (pending audit)` gesetzt

## Rules-Refs

- `.agents/rules/AiNetLinter.mdc` — FastTests <10s, IntegrationTests getrennt.
- `.agents/rules/AiNetLinterRichtlinien.mdc` — Alle Tests grün vor Task-Beendigung.
