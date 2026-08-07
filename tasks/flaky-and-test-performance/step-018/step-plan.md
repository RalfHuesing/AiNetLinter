---
status: in_progress
type: step-plan
task: flaky-and-test-performance
step: 018
corrects: null
title: "EPIC-07 Tote ConsoleTestCollection-Infrastruktur entfernen"
epic: EPIC-07
estimated_risk: low
step_type: single
items: []
created_by: planer
created_by_model: gemini-3.6-flash
created_at: 2026-08-07T18:16:00+02:00
---

# Step 018: EPIC-07 Tote ConsoleTestCollection-Infrastruktur entfernen

## Bezug

- **Task:** `flaky-and-test-performance`
- **Epic:** `EPIC-07` aus `roadmap.md` — Tote ConsoleTestCollection-Infrastruktur entfernen.
- **Konzept-Referenz:** `konzept.md` §"Wie" Schritt 7, §"Entdeckte Mängel".

## Intention

Entfernen der ungenutzten Zwangsserialisierungs-Klasse `ConsoleTestCollection.cs` sowie der `[Collection("ConsoleTestCollection")]`-Attribute von den 5 betroffenen Testklassen (`AuditCommandTests`, `DocsCommandTests`, `PlaybookCheckCommandTests`, `SyncAgentRulesCommandTests`, `ProgramTests`).

## Konkrete Änderungen

1. **Datei gelöscht:** `src/AiNetLinter.Tests/ConsoleTestCollection.cs`
2. **Attribute entfernt:** aus `AuditCommandTests.cs`, `DocsCommandTests.cs`, `PlaybookCheckCommandTests.cs`, `SyncAgentRulesCommandTests.cs`, `ProgramTests.cs`.

## Definition of Done

- [x] `ConsoleTestCollection.cs` gelöscht.
- [x] Attribute aus den 5 Test-Klassen entfernt.
- [x] `dotnet build` grün mit 0 Warnungen.
