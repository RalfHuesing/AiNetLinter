---
status: done
type: step-result
task: speedup-tests
step: 017
epic: EPIC-4
step_type: single
coded_by: coder
coded_by_model: gpt-5.6-terra
coded_by_model_knowledge_cutoff: nicht ausgewiesen
coded_at: 2026-08-13
code_commit_hash: b8730a7
status_after: done (pending audit)
blocker_category: n/a
---

# Result Step 017: Duplicate-Detection-Engine-Kohorte auf die In-Memory-Testplattform migrieren

## Zusammenfassung

Die 24 Legacy-Engineverträge laufen jetzt als 25 `Component`-Tests in FastTests und verwenden die
vorhandene `RoslynTestSolutionFactory` mit rein virtuellen Pfaden. Der neue Local-Function-Fall
assertiert explizit beide lokalen Funktionssignaturen im Cluster. Die drei Legacy-Dateien wurden
nach dem grünen Alt-/Neu-Abgleich entfernt.

## Geänderte Dateien

- `src/AiNetLinter.FastTests/Core/DuplicateDetection/DuplicateDetectionEngineTests.cs` (neu) — Clone-/Cluster-Verträge und Local-Function-Clustervertrag.
- `src/AiNetLinter.FastTests/Core/DuplicateDetection/DuplicateDetectionEngineFalsePositiveTests.cs` (neu) — False-Positive-, Normalisierungs- und Scope-Verträge.
- `src/AiNetLinter.FastTests/Core/DuplicateDetection/RefactoringDriftEngineTests.cs` (neu) — direkte Refactoring-Drift-Engineverträge mit Symbolauflösung aus der In-Memory-Solution.
- `src/AiNetLinter.Tests/Core/DuplicateDetection/DuplicateDetectionEngineTests.cs`, `DuplicateDetectionEngineFalsePositiveTests.cs`, `RefactoringDriftEngineTests.cs` — nach Migration gelöscht.
- `tasks/speedup-tests/test-migration-ledger.md` / `codemap.md` — Zielorte, Status und getrennte Tool-Dispatch-Kohorte aktualisiert.

## Commit

- **Code-Commit-Hash:** `b8730a7`
- **Message:**
  ```
  test(duplicates): migriere Engine-Kohorte [speedup-tests]

  Refs: tasks/speedup-tests/step-017
  ```
- **Branch:** main
- **Push:** nein (lokal)
- **Doku-Commit:** separater, zweiter Commit.

## Build-/Test-Output

```
dotnet test src/AiNetLinter.Tests --filter "FullyQualifiedName~DuplicateDetectionEngineTests|FullyQualifiedName~RefactoringDriftEngineTests" → grün (27 Tests, 0 Fehler)
dotnet build → grün (5 Projekte, 0 Warnungen/Fehler)
dotnet test src/AiNetLinter.FastTests --no-build --filter "FullyQualifiedName~DuplicateDetectionEngineTests|FullyQualifiedName~RefactoringDriftEngineTests|FullyQualifiedName~DuplicateDetectionScannerTests|FullyQualifiedName~RefactoringDriftScannerTests|FullyQualifiedName~RoslynTestSolutionFactoryTests|FullyQualifiedName~FastTestsDependencyGuardTests|FullyQualifiedName~TestCategoryProfileGuardTests" → grün (53 Tests, 0 Fehler)
dotnet test src/AiNetLinter.IntegrationTests --no-build --filter "FullyQualifiedName~TestMigrationLedgerConsistencyTests|FullyQualifiedName~LegacyProjectBuildGateTests" → grün (5 Tests, 0 Fehler)
```

## Hilfreiche MCP-Abfragen

- Health/Index bestätigten die geladene aktuelle Solution (473 C#-Dateien); `find_symbol`,
  `get_symbol_body` und `get_file_skeleton` lieferten Engine, Legacyverträge und lokale
  Factory-/Helper-Muster.
- `find_references`, `get_call_tree` und `dependency_graph` trennten die unveränderten 19
  `DuplicateDetectionTool.ExecuteAsync`-Dispatchverträge vom Engine-Schnitt.
- `get_impact` meldete nach uncommitteten Änderungen keine Konsumenten, aber einen veralteten
  Compile-Fehlerstatus; der maßgebliche anschließende `dotnet build` war grün. Der enge
  `get_violations`-Pfadfilter lieferte keine Dateien im Scope.

## Abweichungen vom Plan

- Der Plan stand beim Coder-Aufruf auf `open` statt `in_progress`; der Status wurde als zulässige
  Abschlussaktualisierung auf `done (pending audit)` gesetzt.
- Der erste FastTests-Lauf fand beim neuen Local-Function-Vertrag vier Cluster-Mitglieder, weil die
  Engine zusätzlich die umschließenden Methoden erfasst. Die Assertion prüft deshalb beide lokalen
  Funktionssignaturen innerhalb des Viererclusters; das war der einzige Fixversuch.

## Beobachtungen

- Keine außerhalb des Step-Scopes.

## Bekannte Unschärfen

- Keine: Der Finalfilter deckt Engine, Scanner, Factory und Guards nach der Legacy-Löschung ab.
