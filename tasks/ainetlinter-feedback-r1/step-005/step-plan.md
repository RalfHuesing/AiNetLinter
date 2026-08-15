---
status: in_progress
type: step-plan
task: ainetlinter-feedback-r1
step: "005"
corrects: null
title: "Teil A: Neues MCP-Tool get_class_structure"
epic: EPIC-05
estimated_risk: medium
step_type: single
items: []
created_by: planer
created_by_model: gemini-3.7-flash
created_by_model_knowledge_cutoff: 2026-01
created_at: 2026-08-15T19:28:00+02:00
related_to: []
---

# Step 005: Teil A — Neues MCP-Tool get_class_structure

## Bezug

- **Task:** `ainetlinter-feedback-r1`
- **Epic:** `EPIC-05` aus `roadmap.md` — Teil A: Neues MCP-Tool `get_class_structure`
- **Konzept-Referenz:** `konzept.md` §A

## Aktueller Projektzustand (JIT-Kontext)

Für einen schnellen Überblick über die interne Struktur eines Typs (welche Member gibt es, welche Sichtbarkeit, welche Zeilenbereiche und Zeilenlängen) gab es bisher nur `get_file_skeleton` (datei-basiert, ohne Zeilenspannen der Member) und `find_symbol` (flache Trefferliste).

## Intention

1. Neues MCP-Tool `get_class_structure` in `src/AiNetLinter/Mcp/Tools/FileStructure/GetClassStructureTool.cs` und Models in `GetClassStructureModels.cs` implementieren.
2. Registrierung in `FileStructureToolRegistrations.cs` und Aufnahme in `OverviewResourceRegistration.ToolSummaries`.
3. Parameter: `symbol` (Pflicht — Typname, File:Line:Col oder DocCommentId), `sortBy` (optional: `"lines"` [Default], `"kind"`, `"name"`).
4. Ausgabe als tabellarisches Markdown (Header mit Typ, Kind, Files, Total Lines, Member Count + Markdown-Tabelle) und Structured JSON Payload (`ClassStructurePayload`).
5. FastTests & Component-Tests in `GetClassStructureToolTests.cs`.

## Konkrete Änderungen

### Datei 1: `src/AiNetLinter/Mcp/Tools/FileStructure/GetClassStructureModels.cs`
- **Was:** Models `ClassStructureMemberEntry` und `ClassStructurePayload`.

### Datei 2: `src/AiNetLinter/Mcp/Tools/FileStructure/GetClassStructureTool.cs`
- **Was:** Tool-Implementierung mit Symbol-Auflösung über `FindReferencesTool.ResolveSymbolAsync`, Member-Extraktion, Zeilenberechnung und Markdown/Structured-Output Formatierung.

### Datei 3: `src/AiNetLinter/Mcp/FileStructureToolRegistrations.cs`
- **Was:** Registrierung von `get_class_structure` an der Tool-Collection.

### Datei 4: `src/AiNetLinter/Mcp/OverviewResourceRegistration.cs`
- **Was:** `get_class_structure` in `ToolSummaries` aufnehmen.

### Datei 5: `src/AiNetLinter.FastTests/Mcp/Tools/GetClassStructureToolTests.cs`
- **Was:** Neue Test-Klasse für `get_class_structure` (Argument-Validierung, Symbol-Auflösung, Member-Details, Partial Classes, Structured JSON).

## Tests

- [ ] `ExecuteAsync_NoSolutionLoaded_ReturnsErrorWithSolutionNotLoadedCode` in `GetClassStructureToolTests.cs`
- [ ] `ExecuteAsync_MissingSymbol_ReturnsRecoverableInvalidArgument` in `GetClassStructureToolTests.cs`
- [ ] `ExecuteAsync_SymbolNotFound_ReturnsSymbolNotFound` in `GetClassStructureToolTests.cs`
- [ ] `ExecuteAsync_ValidClass_ReturnsHeaderAndMemberTable` in `GetClassStructureToolTests.cs`
- [ ] `ExecuteAsync_ValidClass_ReturnsStructuredContent` in `GetClassStructureToolTests.cs`

## Definition of Done

- [ ] Alle „Konkrete Änderungen" umgesetzt
- [ ] `dotnet build` fehler- und warnungsfrei
- [ ] `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress` grün
- [ ] Code-Commit & Doku-Commit auf aktuellem Branch
- [ ] `step-005/step-result.md` geschrieben

## Rules-Refs

- `.agents/rules/AiNetLinterRichtlinien.mdc#1-grundprinzipien` — MCP-UX Konsistenz
