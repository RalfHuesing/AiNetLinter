---
status: completed
type: step-result
task: ainetlinter-feedback-r1
step: "006"
title: "FB-01: Heuristik fuer declaration-only types im AIContextFootprint"
epic: EPIC-06
coded_by: coder
coded_by_model: gemini-3.7-flash
coded_by_model_knowledge_cutoff: 2026-01
coded_at: 2026-08-15T19:33:00+02:00
related_to:
  - tasks/ainetlinter-feedback-r1/step-006/step-plan.md
---

# Step 006: FB-01 — Heuristik für „declaration-only types" im AIContextFootprint — Ergebnis

## Was wurde geändert

1. **`AIContextFootprintCalculator` (`src/AiNetLinter/Metrics/AIContextFootprintCalculator.cs`):**
   - Konstante `MaxDeclarationLines = 10` definiert.
   - Methode `IsDeclarationOnlyType(INamedTypeSymbol symbol)` implementiert: identifiziert reine Datenträger-Typen (Enums, DTOs/Models mit Properties/Fields ohne Methoden, Records ohne benutzerdefinierte Methoden).
   - `CalculateDetailed` angepasst: für abhängige Typen, die `IsDeclarationOnlyType` erfüllen, werden nur die Deklarationszeilen (geklemmt auf max. `MaxDeclarationLines`) zum Footprint und zu den `TopDependencies` hinzugerechnet statt der gesamten Datei.
2. **Tests (`AIContextFootprintCalculatorTests.cs`):**
   - `IsDeclarationOnlyType_IdentifiesDtoAndRecordsCorrectly`: Typ-Erkennung für DTOs, Positional Records, Enums, Service-Klassen und Records mit Methoden.
   - `Calculate_WithDeclarationOnlyDto_CapsDtoFootprintToMaxDeclarationLines`: Verifiziert, dass ein DTO in einer großen Datei nur mit 5 Deklarationszeilen angerechnet wird.
   - `Calculate_WithMethodClass_CountsFullFileLines`: Verifiziert, dass Methodenklassen weiterhin voll angerechnet werden.

## Verifikation

- `dotnet test src/AiNetLinter.FastTests --filter FullyQualifiedName~AIContextFootprintCalculatorTests`: 3/3 bestanden.
- `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress`: 1345/1345 bestanden.
