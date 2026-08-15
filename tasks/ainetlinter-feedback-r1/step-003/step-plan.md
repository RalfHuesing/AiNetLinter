---
status: in_progress
type: step-plan
task: ainetlinter-feedback-r1
step: "003"
corrects: null
title: "FB-04: find_duplicates UX (scopeType und Top-Cluster Summary)"
epic: EPIC-03
estimated_risk: medium
step_type: single
items: []
created_by: planer
created_by_model: gemini-3.7-flash
created_by_model_knowledge_cutoff: 2026-01
created_at: 2026-08-15T19:17:00+02:00
related_to: []
---

# Step 003: FB-04: find_duplicates UX (scopeType und Top-Cluster Summary)

## Bezug

- **Task:** `ainetlinter-feedback-r1`
- **Epic:** `EPIC-03` aus `roadmap.md` — FB-04 find_duplicates UX
- **Konzept-Referenz:** `konzept.md` §FB-04

## Aktueller Projektzustand (JIT-Kontext)

`find_duplicates` unterstützt bisher nur `scopeDir`, aber keine Trennung nach Produktions- und Testcode (`scopeType`). Bei großen Ergebnissätzen (> 20) gibt `DuplicateDetectionTool.RenderText` bisher alle Cluster ungefiltert ohne verdichteten Header aus.

## Intention

1. Ergänzung des Parameters `scopeType: "all" | "production" | "tests"` (Default `"all"`) in `DuplicateDetectionInput`, `DuplicateDetectionOptions`, `DuplicateDetectionToolRegistrations` und Filterung in `DuplicateDetectionEngine.IsEligibleDocument`.
2. Ergänzung eines Top-Cluster Summary-Headers in `DuplicateDetectionTool.RenderText` bei > 20 Clustern.
3. Komponenten- und Unit-Tests in `DuplicateDetectionToolTests.cs`.

## Konkrete Änderungen

### Datei 1: `src/AiNetLinter/Mcp/Tools/DuplicateDetection/DuplicateDetectionModels.cs`
- **Was:** `DuplicateDetectionInput` um `string? ScopeType = null` erweitern.

### Datei 2: `src/AiNetLinter/Core/DuplicateDetection/DuplicateDetectionModels.cs`
- **Was:** `DuplicateDetectionOptions` um `string? ScopeType = null` erweitern.

### Datei 3: `src/AiNetLinter/Mcp/Tools/DuplicateDetection/DuplicateDetectionScanner.cs`
- **Was:** `BuildOptions` erweitert um Übergabe von `ScopeType`.

### Datei 4: `src/AiNetLinter/Core/DuplicateDetection/DuplicateDetectionEngine.cs`
- **Was:** In `IsEligibleDocument` Filterung nach `scopeType` ("production" schließt Testdateien aus, "tests" lässt nur Testdateien zu).

### Datei 5: `src/AiNetLinter/Mcp/Tools/DuplicateDetection/DuplicateDetectionTool.cs`
- **Was:** Validierung für `scopeType` einfügen; in `RenderText` bei `result.TotalClusters > 20 || result.ShownClusters.Count > 20` Kurzübersicht mit Top-Clustern voranstellen.

### Datei 6: `src/AiNetLinter/Mcp/DuplicateDetectionToolRegistrations.cs`
- **Was:** `string? scopeType = null` im Tool-Delegaten und in der Tool-Beschreibung ergänzen.

### Datei 7: `src/AiNetLinter.FastTests/Mcp/Tools/DuplicateDetectionToolTests.cs`
- **Was:** Tests für `scopeType`-Validierung, Test/Production-Filterung und Top-Cluster-Summary-Header ergänzen.

## Tests

- [ ] `ExecuteAsync_InvalidScopeType_ReturnsRecoverableInvalidArgument` in `DuplicateDetectionToolTests.cs`
- [ ] `ExecuteAsync_ScopeTypeProduction_FiltersOutTestFiles` in `DuplicateDetectionToolTests.cs`
- [ ] `ExecuteAsync_ScopeTypeTests_FiltersOutProductionFiles` in `DuplicateDetectionToolTests.cs`
- [ ] `RenderText_WhenMoreThanTwentyClusters_IncludesTopClusterSummary` in `DuplicateDetectionToolTests.cs`

## Definition of Done

- [ ] Alle „Konkrete Änderungen" umgesetzt
- [ ] `dotnet build` fehler- und warnungsfrei
- [ ] `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress` grün
- [ ] Code-Commit & Doku-Commit auf aktuellem Branch
- [ ] `step-003/step-result.md` geschrieben

## Rules-Refs

- `.agents/rules/AiNetLinterRichtlinien.mdc#1-grundprinzipien` — MCP-UX Konsistenz
- `.agents/rules/AiNetLinter.mdc#general` — DuplicateCode Regel
