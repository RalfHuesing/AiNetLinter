---
status: done
type: step-plan
task: ainetlinter-feedback-r1
step: "001"
corrects: null
title: "FB-02: AvoidExcessiveMiddleMen fuer Testfiles ueberspringen"
epic: EPIC-01
estimated_risk: low
step_type: single
items: []
created_by: planer
created_by_model: gemini-3.7-flash
created_by_model_knowledge_cutoff: 2026-01
created_at: 2026-08-15T19:11:00+02:00
related_to: []
---

# Step 001: FB-02: AvoidExcessiveMiddleMen fuer Testfiles ueberspringen

## Bezug

- **Task:** `ainetlinter-feedback-r1`
- **Epic:** `EPIC-01` aus `roadmap.md` — FB-02 Testfile-Skip fuer MiddleManChecker
- **Konzept-Referenz:** `konzept.md` §FB-02

## Aktueller Projektzustand (JIT-Kontext)

`src/AiNetLinter/Core/Checkers/MiddleManChecker.cs` prüft in `ShouldSkipClass` derzeit nur `ctx.Config.Global.AvoidExcessiveMiddleMen`, statische/abstrakte Modifiers, Suffixe und Basisklassen. `ctx.IsTestFile` wird bisher nicht geprüft, im Gegensatz zu 9 anderen Checkern.

## Intention

In `MiddleManChecker.ShouldSkipClass` wird `if (ctx.IsTestFile) return true;` direkt nach dem Config-Check eingefügt. In `MiddleManCheckerTests.cs` wird ein Test hinzugefügt, der bestätigt, dass bei `isTestFile: true` keine Violation erzeugt wird.

## Konkrete Änderungen

### Datei 1: `src/AiNetLinter/Core/Checkers/MiddleManChecker.cs` (Zeile ~51-53)

- **Was:** Ergänzung von `if (ctx.IsTestFile) return true;` direkt nach der Prüfung von `!ctx.Config.Global.AvoidExcessiveMiddleMen`.
- **Warum:** Test-Klassen sind per Definition keine Middle-Man-Klassen und dürfen bei Forwardern wie `Assert.True(Helper(...))` keine Violation auslösen.

### Datei 2: `src/AiNetLinter.FastTests/Core/Checkers/MiddleManCheckerTests.cs`

- **Was:** Neuer Unit-Test `MiddleManChecker_NoViolation_WhenTestFile()`.
- **Warum:** Absicherung, dass bei `isTestFile: true` Forwarder-Klassen ignoriert werden.

## Tests

- [ ] `MiddleManChecker_NoViolation_WhenTestFile` in `MiddleManCheckerTests.cs`

## Definition of Done

- [ ] Alle „Konkrete Änderungen" umgesetzt
- [ ] `dotnet build` fehler- und warnungsfrei
- [ ] `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress` grün
- [ ] Code-Commit & Doku-Commit auf aktuellem Branch
- [ ] `step-001/step-result.md` geschrieben

## Rules-Refs

- `.agents/rules/AiNetLinterRichtlinien.mdc#1-grundprinzipien` — Test-Skip Pattern Konsistenz
- `.agents/rules/AiNetLinter.mdc#general` — AvoidExcessiveMiddleMen Regel

## Bekannte Ausnahmen

- Keine
