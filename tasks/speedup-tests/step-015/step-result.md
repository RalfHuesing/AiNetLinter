---
status: done
type: step-result
task: speedup-tests
step: 015
epic: EPIC-4
step_type: single
coded_by: coder
coded_by_model: gpt-5.6-terra High
coded_by_model_knowledge_cutoff: nicht ausgewiesen
coded_at: 2026-08-12
code_commit_hash: 9abadf9
status_after: done (pending audit)
blocker_category: n/a
---

# Result Step 015: Duplicate-Detection-Scanner auf die In-Memory-Testplattform migrieren

## Zusammenfassung

Die gemeinsame Roslyn-Factory vergibt bei Bedarf normalisierte, rein virtuelle Solution- und
Dokumentpfade, ohne die bisherigen pfadlosen Aufrufe zu verändern. Die sieben Scannerverträge
laufen nun als Component-Tests in FastTests; Bucket-Filter und Trunkierung prüfen je einen
vorhandenen, anschließend ausgeschlossenen oder gekappten Clusterbestand.

## Geänderte Dateien

- `src/AiNetLinter.TestKit/RoslynTestSolutionFactory.cs` — optionaler Overload für virtuelle, normalisierte Pfade.
- `src/AiNetLinter.FastTests/Platform/RoslynTestSolutionFactoryTests.cs` — Pfad- und Nichtmaterialisierungsvertrag.
- `src/AiNetLinter.FastTests/Mcp/Tools/DuplicateDetectionScannerTests.cs` (neu) — sieben Component-Scannerverträge ohne lokalen Workspace oder Dateisystem.
- `src/AiNetLinter.Tests/Mcp/Tools/DuplicateDetectionScannerTests.cs` — nach vollständiger Übernahme gelöscht.
- `tasks/speedup-tests/test-migration-ledger.md` / `codemap.md` — Zielort und Factory-/Scanner-Pointer nachgeführt.

## Commit

- **Code-Commit-Hash:** `9abadf9`
- **Message:**
  ```
  test(duplicates): migriere Scanner-Kohorte [speedup-tests]
  ```
- **Branch:** main
- **Push:** nein (lokal)
- **Doku-Commit:** separater, zweiter Commit.

## Build-/Test-Output

```
dotnet test src/AiNetLinter.Tests --filter FullyQualifiedName~DuplicateDetectionScannerTests → grün (7 Tests, 0 Fehler)
dotnet build → grün (5 Projekte, 0 Warnungen/Fehler)
dotnet test src/AiNetLinter.FastTests --no-build --filter "FullyQualifiedName~DuplicateDetectionScannerTests|FullyQualifiedName~RoslynTestSolutionFactoryTests|FullyQualifiedName~FastTestsDependencyGuardTests|FullyQualifiedName~TestCategoryProfileGuardTests" → grün (17 Tests, 0 Fehler)
dotnet test src/AiNetLinter.IntegrationTests --no-build --filter "FullyQualifiedName~TestMigrationLedgerConsistencyTests|FullyQualifiedName~LegacyProjectBuildGateTests" → grün (5 Tests, 0 Fehler)
```

## Abweichungen vom Plan

- Der Plan stand beim Coder-Aufruf auf `open` statt `in_progress`; der Status wurde als zulässige
  Abschlussaktualisierung auf `done (pending audit)` gesetzt.
- Der erste Build meldete eine überzählige Klammer im neuen Factory-Vertragstest; die minimale
  Syntaxkorrektur war der erste und einzige Fixversuch, der Folge-Build und alle Plan-Filter waren grün.

## Beobachtungen

- Keine außerhalb des Step-Scopes.

## Bekannte Unschärfen

- Keine: Die drei zuvor schwachen Fälle haben ihre positiven Ausgangscluster im gezielten FastTests-Lauf nachweisbar erzeugt.
