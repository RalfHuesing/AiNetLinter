---
status: in_progress
type: step-plan
task: ainetlinter-feedback-r1
step: "004"
corrects: null
title: "Teil B: Code-Snippet in get_violations direkt mitgeben"
epic: EPIC-04
estimated_risk: medium
step_type: single
items: []
created_by: planer
created_by_model: gemini-3.7-flash
created_by_model_knowledge_cutoff: 2026-01
created_at: 2026-08-15T19:24:00+02:00
related_to: []
---

# Step 004: Teil B — Code-Snippet in get_violations direkt mitgeben

## Bezug

- **Task:** `ainetlinter-feedback-r1`
- **Epic:** `EPIC-04` aus `roadmap.md` — Teil B: Code-Snippet in `get_violations`
- **Konzept-Referenz:** `konzept.md` §B

## Aktueller Projektzustand (JIT-Kontext)

`get_violations` meldet bisher nur Dateipfad, Zeile, Regel und Detailtext. Ein Agent muss separate Tool-Aufrufe (`get_symbol_body`, `get_file_skeleton`, etc.) ausführen, um den betroffenen Code-Kontext zu sehen.

## Intention

1. `RuleViolation` um `string? Snippet { get; init; }` erweitern.
2. `GetViolationsScannerParameters`, `GetViolationsScanner.BuildViolationsTextAsync`, `GetViolationsTool` und `AnalysisToolRegistrations` um `int contextLines = 0` (geklemmt auf `0..5`) und `bool includeSnippet = false` erweitern.
3. Wenn `includeSnippet: true`, Snippet für gemeldete Violations über Roslyn-Dokumente / SourceText extrahieren (max 15 Zeilen) und sowohl im Text-Report als auch im `StructuredContent` (`RuleViolation.Snippet`) bereitstellen.
4. `ViolationMarkdownFormatter` um Snippet-Rendering erweitern.
5. Unit- und Komponenten-Tests in `GetViolationsToolTests.cs` und `ViolationMarkdownFormatterTests.cs` ergänzen.

## Konkrete Änderungen

### Datei 1: `src/AiNetLinter/Models/RuleViolation.cs`
- **Was:** `public string? Snippet { get; init; }` hinzufügen.

### Datei 2: `src/AiNetLinter/Mcp/Tools/Analysis/GetViolationsScanner.cs`
- **Was:** `GetViolationsScannerParameters` um `ContextLines` und `IncludeSnippet` erweitern. In `BuildViolationsTextAsync` Snippets extrahieren und an `FormatReport` / `RuleViolation` anhängen (max 15 Zeilen). In `AppendSection` Code-Blöcke rendern wenn `Snippet` vorhanden ist.

### Datei 3: `src/AiNetLinter/Output/ViolationMarkdownFormatter.cs`
- **Was:** In `AppendFileGroup` Snippet als C#-Codeblock rendern wenn vorhanden.

### Datei 4: `src/AiNetLinter/Mcp/Tools/Analysis/GetViolationsTool.cs`
- **Was:** Parameter `contextLines` und `includeSnippet` in `ExecuteAsync` entgegennehmen und an `GetViolationsScanner` weiterreichen.

### Datei 5: `src/AiNetLinter/Mcp/AnalysisToolRegistrations.cs`
- **Was:** `contextLines` und `includeSnippet` in `AddGetViolations` registrieren und Tool-Beschreibung aktualisieren.

### Datei 6: `src/AiNetLinter.FastTests/Mcp/Tools/GetViolationsToolTests.cs`
- **Was:** Tests für `includeSnippet: true`, `contextLines`, Truncation und `StructuredContent` ergänzen.

## Tests

- [ ] `ExecuteAsync_IncludeSnippetTrue_AppendsCodeSnippetToTextAndStructuredContent` in `GetViolationsToolTests.cs`
- [ ] `ExecuteAsync_IncludeSnippetWithContextLines_IncludesSurroundingLines` in `GetViolationsToolTests.cs`
- [ ] `ExecuteAsync_IncludeSnippetFalse_SnippetPropertyIsNull` in `GetViolationsToolTests.cs`

## Definition of Done

- [ ] Alle „Konkrete Änderungen" umgesetzt
- [ ] `dotnet build` fehler- und warnungsfrei
- [ ] `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress` grün
- [ ] Code-Commit & Doku-Commit auf aktuellem Branch
- [ ] `step-004/step-result.md` geschrieben

## Rules-Refs

- `.agents/rules/AiNetLinterRichtlinien.mdc#1-grundprinzipien` — MCP Token-Effizienz
