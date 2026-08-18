---
status: open
type: step-plan
task: 01-namespace-tree
step: 003
corrects: null
title: "Umfassende FastTests, IntegrationTests & Dogfood-Tests für get_namespace_tree"
epic: EPIC-03
estimated_risk: low
step_type: single
items: []
created_by: planer
created_by_model: claude-3-7-sonnet
created_by_model_knowledge_cutoff: 2026-01
created_at: 2026-08-19T00:45:00+02:00
related_to: [step-001, step-002]
---

# Step 003: Umfassende FastTests, IntegrationTests & Dogfood-Tests für get_namespace_tree

## Bezug

- **Task:** `01-namespace-tree`
- **Epic:** `EPIC-03` aus `roadmap.md` — Testabdeckung in `AiNetLinter.FastTests` (>15 Tests für Zoom-Stufen, Filter, Truncation, Errors) und IntegrationTest-Verifikation
- **Konzept-Referenz:** `konzept.md` §Definition of Done #11-13

## Aktueller Projektzustand (JIT-Kontext)

- In `step-001` und `step-002` wurden 5 Scanner-Tests und 7 Tool-Tests angelegt (insgesamt 12 Tests).
- Es fehlen noch gezielte Tests für:
  - Globale / Root-Namespaces (`<global>`) und Top-Level-Statements.
  - Leere Parent-Namespaces (`(0 Typen)` Navigation).
  - Depth-Clamping (depth > 3 wird auf 3 begrenzt).
  - Case-Insensitivität für `kind` (`"CLASS"`, `"Interface"` etc.) und `project`.
  - IntegrationTests / E2E & Dogfood: `McpServerAllToolsE2ETests` (E2E-Aufruf gegen Fixture) und `McpLiveRepositoryTests` (Live-Dogfooding von `get_namespace_tree` gegen die echte Repo-Solution).

## Intention

Vervollständigung der Testsuite auf >15 FastTests in `AiNetLinter.FastTests` sowie Integration von `get_namespace_tree` in `McpServerAllToolsE2ETests` und `McpLiveRepositoryTests`.

## Konkrete Änderungen

### Datei 1: `src/AiNetLinter.FastTests/Mcp/Tools/GetNamespaceTreeScannerTests.cs`

- **Was:** Ergänzung von Tests für:
  - `ScanProjectNamespacesAsync_GlobalNamespace_ReturnsTypesInGlobalNamespace`
  - `ScanProjectNamespacesAsync_DepthExceedingCap_IsClampedTo3`
  - `ScanProjectNamespacesAsync_KindFilterCaseInsensitive_MatchesCorrectTypes`
  - `ScanProjectNamespacesAsync_EmptyParentWithSubNamespaceTypes_ShowsParent`

### Datei 2: `src/AiNetLinter.FastTests/Mcp/Tools/GetNamespaceTreeToolTests.cs`

- **Was:** Ergänzung von Tests für:
  - `ExecuteAsync_CaseInsensitiveProjectName_ResolvesCorrectly`
  - `ExecuteAsync_NegativeDepth_DefaultsTo1`

### Datei 3: `src/AiNetLinter.IntegrationTests/Mcp/McpServerAllToolsE2ETests.cs`

- **Was:** Neuer Test `GetNamespaceTree_NoArguments_ReturnsSolutionOverview` und `GetNamespaceTree_SpecificProject_ReturnsNamespaces`.

### Datei 4: `src/AiNetLinter.IntegrationTests/Mcp/McpLiveRepositoryTests.cs`

- **Was:** Neuer Dogfooding-Test `LiveDogfood_GetNamespaceTree_ReturnsProjectsAndNamespaces`.

## Tests

- [ ] Mindestens 17 FastTests in `AiNetLinter.FastTests` laufen grün.
- [ ] IntegrationTests und Live-Dogfooding laufen 100% grün.

## Definition of Done

- [ ] Alle neuen Tests umgesetzt
- [ ] `dotnet build` mit 0 Warnungen
- [ ] `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress` und `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress` 100% grün
- [ ] `step-003/step-result.md` geschrieben
- [ ] `status` in `step-plan.md` auf `done (pending audit)`

## Rules-Refs

- `.agents/rules/AiNetLinterRichtlinien.mdc#2. Entwicklungs- & Test-Workflow` — Volllauf beider Testprojekte Pflicht.
