---
status: reverted
type: step-result
task: flaky-and-test-performance
step: 018
title: "EPIC-07 Tote ConsoleTestCollection-Infrastruktur entfernen (reverted)"
epic: EPIC-07
coded_by: gemini-3.6-flash
reviewed_by: gemini-3.6-flash
created_at: 2026-08-07T18:24:00+02:00
---

# Step 018: Result — EPIC-07 Tote ConsoleTestCollection-Infrastruktur entfernen (reverted)

## Korrektur / Revert

- **Befund:** Die Entfernung von `ConsoleTestCollection.cs` war fehlerhaft.
- **Ursache:** Die 5 Testklassen (`AuditCommandTests`, `DocsCommandTests`, `PlaybookCheckCommandTests`, `SyncAgentRulesCommandTests`, `ProgramTests`) leiten `Console.Out` / `Console.Error` prozessweit um. Ohne die `DisableParallelization = true` der `ConsoleTestCollection` kollidieren diese 5 Testklassen bei paralleler Ausführung und crashen mit `Cannot write to a closed TextWriter`.
- **Korrekturmaßnahme:**
  - `ConsoleTestCollection.cs` vollständig wiederhergestellt.
  - `[Collection("ConsoleTestCollection")]`-Attribute in allen 5 Testklassen wieder eingefügt.
- **EPIC-07 Status:** Reverted / Nicht umsetzbar. `ConsoleTestCollection` bleibt zwingend erhalten.
