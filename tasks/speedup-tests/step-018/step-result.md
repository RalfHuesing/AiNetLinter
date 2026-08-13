---
status: done
type: step-result
task: speedup-tests
step: 018
epic: EPIC-4
step_type: batch
coded_by: coder
coded_by_model: gpt-5.6-terra
coded_by_model_knowledge_cutoff: nicht ausgewiesen
coded_at: 2026-08-13
code_commit_hash: f0dbacc
status_after: done (pending audit)
blocker_category: n/a
---

# Result Step 018: Fuenf rohe FastTests-Helper auf virtuelle Snapshots schliessen

## Zusammenfassung

Die fuenf letzten FastTests-Klassen verwenden nun ausschliesslich besitzende virtuelle
`RoslynTestSolution`-Snapshots bzw. `McpInMemoryTestContext`. Lokale Temp-Verzeichnisse,
Dateischreiben, manuelle Workspace-/Catalog-Builder und das funktionslose Probeverzeichnis der
Malfunction-Faelle sind entfernt; alle 62 vorhandenen Testvertraege blieben erhalten.

## Geänderte Dateien

- **item-01:** `src/AiNetLinter.FastTests/Mcp/Tools/DependencyGraphScannerTests.cs` — 15 Scannervertraege auf virtuelle Factory-Snapshots umgestellt.
- **item-02:** `src/AiNetLinter.FastTests/Mcp/Tools/DuplicateDetectionToolTests.cs` / `DuplicateDetectionToolRefactoringDriftTests.cs` — Tool-Dispatch gegen besitzenden Snapshot-Kontext ausgefuehrt.
- **item-03:** `src/AiNetLinter.FastTests/Mcp/Tools/PatternDetectScannerTests.cs` / `SafeguardScannerTests.cs` — Scanner- und Faulting-Faelle ohne Dateisystem aufgebaut.
- **item-04:** `tasks/speedup-tests/step-018/step-plan.md`, `task-state.md`, `codemap.md`, `step-result.md` — Abschlussstatus und Pointer aktualisiert.

## Commit

- **Code-Commit-Hash:** `f0dbacc`
- **Message:**
  ```
  refactor(mcp): migriere Tooltests auf Snapshots [speedup-tests]
  ```
- **Branch:** main
- **Push:** nein (lokal)
- **Doku-Commit:** separater, zweiter Commit.

## Build-/Test-Output

```
statischer Fuenf-Dateien-Guard (Dateisystem/Catalog/Builder) → grün (0 Treffer; 62 Methoden)
dotnet test src\AiNetLinter.FastTests --filter "FullyQualifiedName~DependencyGraphScannerTests|FullyQualifiedName~DuplicateDetectionToolTests|FullyQualifiedName~DuplicateDetectionToolRefactoringDriftTests|FullyQualifiedName~PatternDetectScannerTests|FullyQualifiedName~SafeguardScannerTests" → grün (62 Tests, 0 Fehler)
dotnet test src\AiNetLinter.FastTests --filter "FullyQualifiedName~GetHotspotsToolTests|FullyQualifiedName~MetricsTreeToolTests|FullyQualifiedName~MetricsTreeRoslynScannerTests|FullyQualifiedName~GetCallTreeToolTests|FullyQualifiedName~FindReferencesToolTests|FullyQualifiedName~GetTypeHierarchyToolTests|FullyQualifiedName~GetViolationsToolTests|FullyQualifiedName~PatternDetectToolTests|FullyQualifiedName~SafeguardScannerTests|FullyQualifiedName~SafeguardToolTests|FullyQualifiedName~McpCodeGraphServerReadOnlySnapshotTests|FullyQualifiedName~RoslynTestSolutionFactoryTests" → grün (126 Tests, 0 Fehler)
dotnet build → grün (0 Warnungen, 0 Fehler)
dotnet test src\AiNetLinter.FastTests --no-build --filter "FullyQualifiedName~LinterAnalyzerArchitectureRuleTests|FullyQualifiedName~LinterAnalyzerTests|FullyQualifiedName~CallGraphTraversalTests|FullyQualifiedName~DependencyGraphScannerTests|FullyQualifiedName~DependencyGraphToolTests|FullyQualifiedName~DiRegistrationHeuristicsTests|FullyQualifiedName~DuplicateDetectionToolRefactoringDriftTests|FullyQualifiedName~DuplicateDetectionToolTests|FullyQualifiedName~FindReferencesToolTests|FullyQualifiedName~GetCallTreeToolTests|FullyQualifiedName~GetFileSkeletonToolTests|FullyQualifiedName~GetHotspotsToolTests|FullyQualifiedName~GetSymbolBodyToolTests|FullyQualifiedName~GetTypeHierarchyToolTests|FullyQualifiedName~GetViolationsToolTests|FullyQualifiedName~McpToolResultsTests|FullyQualifiedName~MetricsTreeRoslynScannerTests|FullyQualifiedName~MetricsTreeToolTests|FullyQualifiedName~PatternDetectScannerTests|FullyQualifiedName~PatternDetectToolTests|FullyQualifiedName~SafeguardScannerTests|FullyQualifiedName~SafeguardToolTests|FullyQualifiedName~SymbolIdentifierResolverTests|FullyQualifiedName~McpCodeGraphServerReadOnlySnapshotTests|FullyQualifiedName~RoslynTestSolutionFactoryTests" → grün (253 Tests, 0 Fehler)
dotnet test src\AiNetLinter.Tests --no-build --filter "FullyQualifiedName~McpCodeGraphServerConstructorTests|FullyQualifiedName~McpCodeGraphServerFileDiscoveryTests|FullyQualifiedName~McpCodeGraphServerStalenessMtimeCacheTests" → grün (8 Tests, 0 Fehler)
dotnet test src\AiNetLinter.Tests --no-build --filter "FullyQualifiedName~SuppressionScannerTests" → grün (1 Test, 0 Fehler)
dotnet test src\AiNetLinter.FastTests --no-build --filter "FullyQualifiedName~FastTestsDependencyGuardTests|FullyQualifiedName~TestCategoryProfileGuardTests" → grün (3 Tests, 0 Fehler)
dotnet test src\AiNetLinter.IntegrationTests --no-build --filter "FullyQualifiedName~TestMigrationLedgerConsistencyTests|FullyQualifiedName~LegacyProjectBuildGateTests" → grün (5 Tests, 0 Fehler)
statischer 23-Scope-Guard, Ledger-Check und git diff --check → grün (0 Treffer; 23 migrated; Suppression pending)
```

## Abweichungen vom Plan

Keine — Plan 1:1 umgesetzt. Der 126er-Filter war im aktuellen Recovery-6-Plan nur als
Erwartungswert angegeben und wurde aus dem committed Recovery-4-Plan als zehn Fehlerklassen plus
Snapshot-Seam- und Factory-Vertraege rekonstruiert; er ergab die erwarteten 126 Tests.

## Beobachtungen

Keine außerhalb des Scopes.

## Bekannte Unschärfen

Keine. Die Recovery verbrauchte keinen der beiden verbleibenden ursachengebundenen Fixversuche.
