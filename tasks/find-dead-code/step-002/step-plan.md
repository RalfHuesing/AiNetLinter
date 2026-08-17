---
status: done
type: step-plan
task: find-dead-code
step: 002
corrects: null
title: "Diagnosen & Locals-Erkennung (Mode: locals & both)"
epic: EPIC-02
estimated_risk: low
step_type: single
items: []
created_by: planer
created_by_model: gemini-2.5-pro
created_by_model_knowledge_cutoff: 2026-01
created_at: 2026-08-17T17:26:00+02:00
related_to: []
---

# Step 002: Diagnosen & Locals-Erkennung (Mode: locals & both)

## Bezug

- **Task:** `find-dead-code`
- **Epic:** `EPIC-02` aus `roadmap.md` — Diagnosen & Locals-Erkennung (Mode-Support)
- **Konzept-Referenz:** `konzept.md` §3.3 Mode-Filter, §Wie Schritt 4

## Aktueller Projektzustand (JIT-Kontext)

- `FindDeadCodeScanner` führt aktuell in `step-001` den Symbol-Graph-Check (`mode: members`) durch.
- `McpCompileDiagnostics` zeigt, wie Roslyn `Diagnostic`-Objekte effizient pro Projekt/Dokument verarbeitet werden.
- `DeadCodeMode` (`Members`, `Locals`, `Both`) ist in `DeadCodeModels.cs` bereits vordefiniert.

## Intention

Erweiterung von `FindDeadCodeScanner.cs` um die Auswertung von Compiler- und Analyzer-Diagnosen (`CS0169`, `CS0414`, `IDE0051`, `IDE0052`) für `mode: locals` und `mode: both`. Doppelte Erfassungen zwischen Symbol-Graph und Diagnosen werden de-dupliziert.

## Konkrete Änderungen

### Datei 1: `src/AiNetLinter/Mcp/Tools/DeadCode/FindDeadCodeScanner.cs` (Erweiterung)

- **Was:**
  - Neue Methode `ScanDiagnosticsAsync` zur Extraktion von `CS0169` (unused field), `CS0414` (assigned but unused field), `IDE0051` (unused private member) und `IDE0052` (unread private member).
  - Steuerung über `args.Mode`:
    - `Members`: nur Symbol-Graph.
    - `Locals`: nur Compiler-Diagnosen.
    - `Both`: beides kombiniert mit stabiler De-Duplikation nach Datei/Zeile/SymbolName.
- **Warum:** Erfüllt die `mode`-Filteranforderung aus `konzept.md`.

### Datei 2: `src/AiNetLinter.FastTests/Mcp/Tools/DeadCode/FindDeadCodeScannerTests.cs` (Erweiterung)

- **Was:** Ergänzung von Unit-Tests für `mode: locals`, `mode: both` und De-Duplikation.
- **Warum:** Sicherstellung der semantischen Korrektheit von `mode`.

## Tests

- [ ] `FindDeadCodeScannerTests.ScanAsync_ModeLocals_CollectsUnusedFieldDiagnostics`
- [ ] `FindDeadCodeScannerTests.ScanAsync_ModeBoth_CombinesAndDeduplicates`
- [ ] `FindDeadCodeScannerTests.ScanAsync_ModeMembers_IgnoresLocalsOnly`

## Definition of Done

- [ ] Alle „Konkrete Änderungen" umgesetzt
- [ ] Build-Command aus Tech-Stack-Notiz (`roadmap.md`) grün (`dotnet build`)
- [ ] Test-Command aus Tech-Stack-Notiz grün (`dotnet test src/AiNetLinter.FastTests --filter Category!=Stress && dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress`)
- [ ] 0 Linter-Violations (`get_violations`)
- [ ] Commit auf aktuellem Branch (Conventional Commit `feat(deadcode): Diagnosen und Locals-Erkennung integrieren [find-dead-code]`)
- [ ] `tasks/find-dead-code/step-002/step-result.md` geschrieben
- [ ] `status` in `step-plan.md` auf `done (pending audit)` gesetzt

## Rules-Refs

- `.agents/rules/AiNetLinter.mdc` — Methoden ≤60 Zeilen, Cognitive Complexity ≤15, McCabe ≤12.
- `.agents/rules/AiNetLinterRichtlinien.mdc` — Monolithisch, Result-Pattern.
