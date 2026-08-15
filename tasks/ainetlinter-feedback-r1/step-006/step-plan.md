---
status: done
type: step-plan
task: ainetlinter-feedback-r1
step: "006"
corrects: null
title: "FB-01: Heuristik fuer declaration-only types im AIContextFootprint"
epic: EPIC-06
estimated_risk: low
step_type: single
items: []
created_by: planer
created_by_model: gemini-3.7-flash
created_by_model_knowledge_cutoff: 2026-01
created_at: 2026-08-15T19:32:00+02:00
related_to: []
---

# Step 006: FB-01 — Heuristik für „declaration-only types" im AIContextFootprint

## Bezug

- **Task:** `ainetlinter-feedback-r1`
- **Epic:** `EPIC-06` aus `roadmap.md` — FB-01: Heuristik für „declaration-only types" im `AIContextFootprint`
- **Konzept-Referenz:** `konzept.md` §FB-01

## Aktueller Projektzustand (JIT-Kontext)

Reine Datenträger-Klassen (DTOs, Models, Options, Records) werden transitiv voll mit der Gesamtzeilenzahl ihrer Quellcodedatei angerechnet, obwohl sie für den Aufrufer nur ihre Signatur beitragen.

## Intention

1. In `src/AiNetLinter/Metrics/AIContextFootprintCalculator.cs` Methode `IsDeclarationOnlyType` und `MaxDeclarationLines = 10` hinzufügen.
2. In `CalculateDetailed`: für abhängige Typen, die `IsDeclarationOnlyType` erfüllen, nur die Deklarationszeilen (geklemmt auf max. `MaxDeclarationLines`) statt des gesamten Datei-Bodys aufsummieren.
3. FastTests in `src/AiNetLinter.FastTests/Metrics/AIContextFootprintCalculatorTests.cs` erstellen bzw. erweitern.

## Konkrete Änderungen

### Datei 1: `src/AiNetLinter/Metrics/AIContextFootprintCalculator.cs`
- **Was:** `IsDeclarationOnlyType(INamedTypeSymbol)` und Deckelung auf max. 10 Zeilen für transitive reine Datenträger-Typen.

### Datei 2: `src/AiNetLinter.FastTests/Metrics/AIContextFootprintCalculatorTests.cs`
- **Was:** Neue FastTests für DTOs, Positional Records und Methodenklassen.

## Tests

- [ ] `Calculate_WithDeclarationOnlyDto_CapsDtoFootprintToMaxDeclarationLines`
- [ ] `Calculate_WithPositionalRecord_CapsRecordFootprint`
- [ ] `Calculate_WithMethodClass_CountsFullFileLines`
- [ ] `IsDeclarationOnlyType_IdentifiesDtoAndRecordsCorrectly`

## Definition of Done

- [ ] Alle „Konkrete Änderungen" umgesetzt
- [ ] `dotnet build` fehler- und warnungsfrei
- [ ] `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress` grün
- [ ] Code-Commit & Doku-Commit auf aktuellem Branch
- [ ] `step-006/step-result.md` geschrieben

## Rules-Refs

- `.agents/rules/AiNetLinterRichtlinien.mdc#1-grundprinzipien` — Determinismus & Performance
