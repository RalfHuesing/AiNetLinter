---
status: done (pending audit)
type: step-plan
task: find-dead-code
step: 003
corrects: null
title: "MCP-Tool-Wrapper, Registrierung & Server-Instructions"
epic: EPIC-03
estimated_risk: low
step_type: single
items: []
created_by: planer
created_by_model: gemini-2.5-pro
created_by_model_knowledge_cutoff: 2026-01
created_at: 2026-08-17T17:32:30+02:00
related_to: []
---

# Step 003: MCP-Tool-Wrapper, Registrierung & Server-Instructions

## Bezug

- **Task:** `find-dead-code`
- **Epic:** `EPIC-03` aus `roadmap.md` — MCP-Tool-Wrapper & Registrierung
- **Konzept-Referenz:** `konzept.md` §3.1, §3.6, §Wo im Projekt

## Aktueller Projektzustand (JIT-Kontext)

- `FindDeadCodeScanner`, `FindDeadCodeDiagnosticsScanner`, `DeadCodeFilters` und `DeadCodeModels` sind implementiert und grün getestet.
- `AnalysisToolRegistrations.cs` registriert analyse-orientierte Tools wie `get_violations`, `safeguard`, `find_magic_values`.
- `ServerInstructions.cs` hält die Übersicht aller MCP-Tools und Workflows vor.

## Intention

Implementierung des MCP-Tool-Wrappers `FindDeadCodeTool.cs` mit formatierter Text- und JSON-StructuredContent-Ausgabe, Registrierung in `AnalysisToolRegistrations.cs`, Ergänzung in `ServerInstructions.cs` und Absicherung durch FastTests.

## Konkrete Änderungen

### Datei 1: `src/AiNetLinter/Mcp/Tools/DeadCode/FindDeadCodeTool.cs` (neu)

- **Was:**
  - Wrapper-Klasse mit `ExecuteAsync(McpCodeGraphServer state, FindDeadCodeToolArgs args, CancellationToken ct)`.
  - Formatierung des Text-Outputs mit Header, Trust-Hinweis, Treffertabelle/Liste, Summary, Limits-Übersicht und `ask_user`-Aktionsempfehlung.
  - Generierung des JSON-`StructuredContent` mit `DeadSymbols`, `Summary`, `Limits`, `RecommendedNextAction`, `IsTruncated`.
  - Sufficiency-Hinweis via `McpSufficiencyHints.Append`.
- **Warum:** Erfüllt den MCP-Protokollvertrag und das Trust-Modell aus `konzept.md`.

### Datei 2: `src/AiNetLinter/Mcp/AnalysisToolRegistrations.cs` (Erweiterung)

- **Was:** Registriert `find_dead_code` mit allen optionalen Parametern (`accessibility`, `confidence`, `kind`, `scopeFilter`, `includeTests`, `mode`, `maxResults`) an der Tool-Collection.
- **Warum:** Macht das Tool über das MCP-Protokoll aufrufbar.

### Datei 3: `src/AiNetLinter/Mcp/ServerInstructions.cs` (Erweiterung)

- **Was:** Ergänzt `find_dead_code` in der Tool-Liste und in der C#-Only-Liste.
- **Warum:** Vollständige System-Instructions für verbundene Clients.

### Datei 4: `src/AiNetLinter.FastTests/Mcp/Tools/DeadCode/FindDeadCodeToolTests.cs` (neu)

- **Was:** FastTests für `FindDeadCodeTool`:
  - Aufruf liefert formatierte Text- und StructuredContent-Ausgabe.
  - Server-Zustände (Loading / SolutionNotLoaded) werden korrekt gehandhabt.
  - Parameter-Defaults und Filterung funktionieren.
- **Warum:** Verifikation der Protokoll- und Tool-Integration.

## Tests

- [ ] `FindDeadCodeToolTests.ExecuteAsync_ValidSolution_ReturnsFormattedTextAndStructuredContent`
- [ ] `FindDeadCodeToolTests.ExecuteAsync_SolutionNotLoaded_ReturnsSolutionNotLoadedError`
- [ ] `FindDeadCodeToolTests.ExecuteAsync_ServerLoading_ReturnsLoadingResult`

## Definition of Done

- [ ] Alle „Konkrete Änderungen" umgesetzt
- [ ] Build-Command aus Tech-Stack-Notiz (`roadmap.md`) grün (`dotnet build`)
- [ ] Test-Command aus Tech-Stack-Notiz grün (`dotnet test src/AiNetLinter.FastTests --filter Category!=Stress && dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress`)
- [ ] 0 Linter-Violations (`get_violations`)
- [ ] Commit auf aktuellem Branch (Conventional Commit `feat(deadcode): MCP-Tool find_dead_code registrieren [find-dead-code]`)
- [ ] `tasks/find-dead-code/step-003/step-result.md` geschrieben
- [ ] `status` in `step-plan.md` auf `done (pending audit)` gesetzt

## Rules-Refs

- `.agents/rules/AiNetLinter.mdc` — Sealed Classes, Methoden ≤60 Zeilen.
- `.agents/rules/AiNetLinterRichtlinien.mdc` — Monolithisch, kein DI/ALC, TreatWarningsAsErrors.
