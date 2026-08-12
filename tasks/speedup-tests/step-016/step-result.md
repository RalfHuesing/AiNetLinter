---
status: done
type: step-result
task: speedup-tests
step: 016
epic: EPIC-4
step_type: single
coded_by: coder
coded_by_model: gpt-5.6-terra
coded_by_model_knowledge_cutoff: nicht ausgewiesen
coded_at: 2026-08-12
code_commit_hash: 14ea50c
status_after: done (pending audit)
blocker_category: n/a
---

# Result Step 016: Refactoring-Drift-Scanner auf die In-Memory-Testplattform migrieren

## Zusammenfassung

Die sieben Scannerverträge wurden als acht `Component`-Tests nach FastTests verschoben und verwenden
die vorhandene `RoslynTestSolutionFactory` mit rein virtuellen Pfaden. Der zusätzliche Lambda-Fall
prüft, dass ein Helper-Aufruf aus einer Lambda den enthaltenden Caller ausschließt, während ein
separater Inline-Kandidat sichtbar bleibt. Die lokale Workspace-, Referenz-, Temp-Datei- und
Cleanup-Infrastruktur der Legacy-Klasse ist entfernt.

## Geänderte Dateien

- `src/AiNetLinter.FastTests/Mcp/Tools/RefactoringDriftScannerTests.cs` (neu) — acht In-Memory-Component-Verträge mit virtuellen Solution- und Dokumentpfaden.
- `src/AiNetLinter.Tests/Mcp/Tools/RefactoringDriftScannerTests.cs` — nach erfolgreichem Alt-/Neu-Abgleich gelöscht.
- `tasks/speedup-tests/test-migration-ledger.md` / `codemap.md` — realen Zielort und Factory-/Legacy-Pointer nachgeführt.

## Commit

- **Code-Commit-Hash:** `14ea50c`
- **Message:**
  ```
  test(duplicates): migriere Drift-Scanner-Kohorte [speedup-tests]

  Refs: tasks/speedup-tests/step-016
  ```
- **Branch:** main
- **Push:** nein (lokal)
- **Doku-Commit:** separater, zweiter Commit.

## Build-/Test-Output

```
dotnet test src/AiNetLinter.Tests --filter FullyQualifiedName~RefactoringDriftScannerTests → grün (7 Tests, 0 Fehler)
dotnet build → grün (5 Projekte, 0 Warnungen/Fehler)
dotnet test src/AiNetLinter.FastTests --no-build --filter "FullyQualifiedName~RefactoringDriftScannerTests|FullyQualifiedName~RoslynTestSolutionFactoryTests|FullyQualifiedName~FastTestsDependencyGuardTests|FullyQualifiedName~TestCategoryProfileGuardTests" → grün (18 Tests, 0 Fehler)
dotnet test src/AiNetLinter.IntegrationTests --no-build --filter "FullyQualifiedName~TestMigrationLedgerConsistencyTests|FullyQualifiedName~LegacyProjectBuildGateTests" → grün (5 Tests, 0 Fehler)
```

## Hilfreiche MCP-Abfragen

- `find_symbol` und `get_symbol_body` zeigten Scanner und Legacy-Matrix sowie die dokumentierte Lambda-Normalisierung.
- `find_references`, `get_call_tree` und `dependency_graph` grenzten die sieben direkten Scannerverträge von den nicht migrierten Tool-Konsumenten ab.
- `get_impact` meldete keine Signatur-Konsumenten; der MCP-Index nannte dabei einen veralteten Compile-Fehlerstatus, während der anschließende `dotnet build` grün war. `get_violations` meldete im neuen Test-Scope 0 Verstöße.

## Abweichungen vom Plan

- Der Plan stand beim Coder-Aufruf auf `open` statt `in_progress`; der Status wurde als zulässige Abschlussaktualisierung auf `done (pending audit)` gesetzt.

## Beobachtungen

- Keine außerhalb des Step-Scopes.

## Bekannte Unschärfen

- Keine: Der Lambda-Caller wird im gezielten FastTests-Lauf ausgeschlossen und der separate Inline-Kandidat gefunden.
