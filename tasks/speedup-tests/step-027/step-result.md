---
status: done
type: step-result
task: speedup-tests
step: 027
epic: EPIC-6
step_type: batch
coded_by: coder
coded_by_model: gemini-3.7-flash
coded_by_model_knowledge_cutoff: 2026-03
coded_at: 2026-08-14T12:20:00+02:00
code_commit_hash: 399a463
status_after: done
blocker_category: n/a
---

# Result Step 027: Korrektur: Git-Workspace-Cleanup und Kategorieguard abschliessen

## Zusammenfassung

Die drei Findings aus dem Step-026-Review wurden vollständig behoben:
1. `FixtureWorkspace.Dispose()` wurde als einmalige Schablone mit geschütztem `PrepareForDelete()`-Hook realisiert; `GitImpactMiniFixtureWorkspace` enumeriert Attribute nur bei existentem `RootPath`.
2. Alle zehn redundanten Methoden-Kategorietraits in `McpServerCommandTests` wurden entfernt.
3. Die Kategorieguard-Prüflogik wurde vollständig in `TestCategoryTraitInspector.EnsureEveryTestClassHasExactlyOneValidCategoryTrait` im TestKit konsolidiert; beide TestCategoryProfileGuardTests sind schlanke Ein-Zeilen-Konsumenten, und der exact-Duplikatcluster ist vollständig beseitigt.

## Geänderte Dateien

- `item-01`: `src/AiNetLinter.IntegrationTests/Fixtures/FixtureWorkspaces.cs` — `FixtureWorkspace.Dispose()` als einmalige Template-Methode mit `Interlocked.Exchange` und `PrepareForDelete()`-Hook; idempotentes Git-Cleanup.
- `item-01`: `src/AiNetLinter.IntegrationTests/Mcp/Tools/SymbolGraph/GetImpactToolIntegrationTests.cs` — Ursachetest `GitImpactMiniFixtureWorkspace_DisposeTwice_DeletesRootWithoutThrowing` ergänzt.
- `item-02`: `src/AiNetLinter.FastTests/Mcp/McpServerCommandTests.cs` — zehn redundante Methoden-Traits `[Trait("Category", "Unit")]` entfernt.
- `item-03`: `src/AiNetLinter.TestKit/TestCategoryTraitInspector.cs` — `EnsureEveryTestClassHasExactlyOneValidCategoryTrait` als zentrale Validierungs-API; Hilfsmethoden privatisiert.
- `item-03`: `src/AiNetLinter.FastTests/Architecture/TestCategoryProfileGuardTests.cs` — auf Ein-Zeilen-Aufruf umgestellt, XML-Doc zeitstabil gekürzt.
- `item-03`: `src/AiNetLinter.IntegrationTests/Architecture/TestCategoryProfileGuardTests.cs` — auf Ein-Zeilen-Aufruf umgestellt, XML-Doc zeitstabil gekürzt.

## Commit

- **Code-Commit-Hash:** `399a463`
- **Message:**
  ```
  fix(test): Git-Cleanup und Kategorieguard abschliessen [speedup-tests]

  Refs: tasks/speedup-tests/step-027
  ```
- **Branch:** `main`
- **Push:** nein (lokal)

## Build-/Test-Output

```
dotnet build -> grün (0 Warnungen, 0 Fehler)
step027-cleanup-cause.trx -> grün (1/1)
step027-command-contracts.trx -> grün (13/13)
step027-fast-guards.trx -> grün (13/13)
step027-integration-guards.trx -> grün (1/1)
step027-fast-matrix.trx -> grün (318/318, historischer Breitlauf; korrigiert via step028-fast-matrix.trx auf 69/69)
step027-integration-matrix.trx -> grün (112/112, historischer Breitlauf; korrigiert via step028-integration-matrix.trx auf 64/64)
step027-ledger-guards.trx -> grün (5/5)
git --no-pager diff --check -> grün
find_duplicates(scopeDir="src", minTokens=20) -> 0 exact-Cluster fuer die Kategorieguards
```

## Abweichungen vom Plan

Die in Step 027 protokollierten Läufe `step027-fast-matrix.trx` (318 Tests) und `step027-integration-matrix.trx` (112 Tests) waren zu breit angesetzt, da Namespace-Filter auch fremde MCP-Kohorten erfasst hatten. Diese wurden in Step 028 durch exakte klassenscharfe Manifeste und FQN-genaue Matrixläufe korrigiert: `step028-fast-matrix.trx` (69/69) und `step028-integration-matrix.trx` (64/64). Die Discovery- und TRX-FQN-Diffs (`step028-*-discovery.diff.txt`, `step028-*-trx.diff.txt`) sind jeweils 0 Byte (100 % manifestscharf).

## Beobachtungen

`find_duplicates(scopeDir="src", minTokens=20)` bestätigt, dass nach der Konsolidierung der Kategorie-Validierung im TestKit keinerlei Duplikatcluster zwischen FastTests und IntegrationTests bezüglich des Profilguards mehr existiert. TD-006 ist in `tech-debt.md` als geschlossen dokumentiert.

## Bekannte Unschärfen

Keine.
