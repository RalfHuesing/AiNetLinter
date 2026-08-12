---
status: done
type: step-result
task: speedup-tests
step: 009
epic: EPIC-2
step_type: single
coded_by: coder
coded_by_model: claude-sonnet-5
coded_by_model_knowledge_cutoff: 2026-01
coded_at: 2026-08-12
code_commit_hash: 1d64b47
status_after: done
blocker_category: n/a
---

# Result Step 009: Korrektur — FilterMiniFidelityTests deckt IsTestProject-Diskrepanz auf statt sie wegzuassertieren

## Zusammenfassung

Zeilen 86-92 in `AssertTestProjectDetectionMatches` gelöscht: Kommentarblock
plus die Assertion, die die bekannte In-Memory-Fehlklassifikation von
`FilterMini` als Testprojekt bestätigte (TD-005). Die drei verbleibenden
Assertions (disk `FilterMini` → false, disk `FilterMini.Tests` → true,
in-memory `FilterMini.Tests` → true) sind unverändert geblieben.

## Geänderte Dateien

- `src/AiNetLinter.IntegrationTests/Platform/FilterMiniFidelityTests.cs` — 8 Zeilen (Kommentar + Assertion) aus `AssertTestProjectDetectionMatches` entfernt, sonst keine Änderung.

## Commit

- **Code-Commit-Hash:** `1d64b47`
- **Message:**
  ```
  fix(tests): entferne Assertion die IsTestProject-Fehlklassifikation bestaetigt [speedup-tests]

  AssertTestProjectDetectionMatches pruefte bisher auch, dass das In-Memory-
  Produktionsprojekt FilterMini faelschlich als Testprojekt erkannt wird
  (TD-005-Root-Cause in RoslynTestSolutionFactory). Das versteckte statt
  aufzudecken. Loeschung ohne Ersatz, die drei verbleibenden Assertions
  bleiben unveraendert.

  Refs: tasks/speedup-tests/step-009
  ```
- **Branch:** main
- **Push:** nein (lokal)
- **Doku-Commit:** separater, zweiter Commit (Hash steht nicht hier drin — Selbstbezug, siehe `git log`).

## Build-/Test-Output

```
dotnet build src/AiNetLinter.IntegrationTests → grün (0 Warnung(en), 0 Fehler)
dotnet test src/AiNetLinter.IntegrationTests --filter FullyQualifiedName~FilterMiniFidelityTests → grün (1 Test, 0 Fehler)
```

## Abweichungen vom Plan

Keine — Plan 1:1 umgesetzt.

## Beobachtungen

Keine über den Plan hinaus — TD-005 (Root-Cause in `RoslynTestSolutionFactory.CoreReferences`) ist bereits dokumentiert und bewusst nicht Teil dieses Steps.

## Bekannte Unschärfen

Keine.
