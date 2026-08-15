---
status: done
type: step-result
task: ainetlinter-feedback-r1
step: "001"
epic: EPIC-01
step_type: single
coded_by: coder
coded_by_model: gemini-3.7-flash
coded_by_model_knowledge_cutoff: 2026-01
coded_at: 2026-08-15T19:12:00+02:00
code_commit_hash: adadf99
status_after: done
blocker_category: n/a
---

# Result Step 001: FB-02: AvoidExcessiveMiddleMen fuer Testfiles ueberspringen

## Zusammenfassung

In `MiddleManChecker.ShouldSkipClass` wurde die Prüfung `if (ctx.IsTestFile) return true;` integriert, sodass Testdateien nicht mehr fälschlicherweise als Middle-Man-Klassen gemeldet werden. In `MiddleManCheckerTests` wurde ein entsprechender Unit-Test hinzugefügt.

## Geänderte Dateien

- `src/AiNetLinter/Core/Checkers/MiddleManChecker.cs` — `ctx.IsTestFile` Skip-Prüfung hinzugefügt.
- `src/AiNetLinter.FastTests/Core/Checkers/MiddleManCheckerTests.cs` — Unit-Test `MiddleManChecker_NoViolation_WhenTestFile` ergänzt.

## Commit

- **Code-Commit-Hash:** `adadf99`
- **Message:**
  ```
  feat(checker): AvoidExcessiveMiddleMen fuer Testfiles ueberspringen [ainetlinter-feedback-r1]

  Refs: tasks/ainetlinter-feedback-r1/step-001
  ```
- **Branch:** main
- **Push:** nein (lokal)
- **Doku-Commit:** separater Doku-Commit

## Build-/Test-Output

```
dotnet build                                                      → grün
dotnet test src/AiNetLinter.FastTests --filter Category!=Stress   → grün (1325 Tests, 0 Fehler)
```

## Abweichungen vom Plan

Keine — Plan 1:1 umgesetzt.

## Beobachtungen

Keine.

## Bekannte Unschärfen

Keine.
