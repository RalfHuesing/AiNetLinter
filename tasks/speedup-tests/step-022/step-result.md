---
status: done (pending audit)
type: step-result
task: speedup-tests
step: 022
epic: EPIC-5
step_type: single
coded_by: coder
coded_by_model: gpt-5.6-terra
coded_by_model_knowledge_cutoff: nicht ausgewiesen
coded_at: 2026-08-13
code_commit_hash: 5aa397f, 1c5090d
status_after: done (pending audit)
blocker_category: n/a
---

# Result Step 022: Korrektur: globales MSBuild-Loadgate und read-only Server-Ownership

## Zusammenfassung

`LoadedFixture` fuehrt reale Katalogloads nun ausschliesslich durch einen internen, exception- und cancellation-sicheren Max-2-Gate-Kern. `FilterMiniFidelityTests` und `ProjectOverrideRealSolutionTests` verwenden diesen Pfad; ein statischer Vertrag begrenzt direkte `SourceFileCatalog.LoadAsync`-Callsites auf `LoadedFixture.cs`. `SymbolGraphCatalogFixture` exponiert nur noch `RootPath` und `Snapshot`; die drei read-only Toolklassen erhalten je einen eigenen Snapshot-Server und koennen den Fixture-Owner nicht mehr schliessen.

## Geänderte Dateien

- `src/AiNetLinter.IntegrationTests/Platform/LoadedFixture.cs` — zentraler Max-2-Gate-Kern fuer alle realen Integrationstest-Katalogloads.
- `src/AiNetLinter.IntegrationTests/Platform/LoadedFixtureTests.cs` (neu) — deterministische Max-2-, Exception-, Cancellation- und Callsite-Guard-Vertraege.
- `src/AiNetLinter.IntegrationTests/Platform/FilterMiniFidelityTests.cs` — verwendet den besitzenden `LoadedFixture`.
- `src/AiNetLinter.IntegrationTests/Configuration/ProjectOverrideRealSolutionTests.cs` — verwendet den budgetierten Katalogload.
- `src/AiNetLinter.IntegrationTests/Mcp/Tools/SymbolGraphCatalogFixture.cs` — kapselt den besessenen Katalog und erzeugt nur Snapshot-Server.
- `src/AiNetLinter.IntegrationTests/Mcp/Tools/SymbolGraphCatalogFixtureTests.cs` (neu) — belegt Dispose-Sicherheit fuer parallele und spaetere Snapshot-Leser.
- `src/AiNetLinter.IntegrationTests/Mcp/Tools/GetServerHealthToolTests.cs`, `GetIndexScopeToolTests.cs`, `SearchPatternToolTests.cs` — verwenden lokal entsorgte Snapshot-Server.

## Commit

- **Code-Commit-Hashes:** `5aa397f`, `1c5090d`
- **Messages:**
  ```
  fix(tests): zentralisiere MSBuild-Loadgate [speedup-tests]

  Refs: tasks/speedup-tests/step-022
  ```
  ```
  fix(tests): kapsle SymbolGraph-Owner [speedup-tests]

  Refs: tasks/speedup-tests/step-022
  ```
- **Branch:** `main`
- **Push:** nein (lokal)
- **Doku-Commit:** separater, zweiter Commit.

## Build-/Test-Output

`dotnet build` → gruen (0 Warnungen, 0 Fehler).
`dotnet test src/AiNetLinter.IntegrationTests --no-build --filter "FullyQualifiedName~LoadedFixtureTests|FullyQualifiedName~MsBuildFixtureHostTests|FullyQualifiedName~MsBuildFixtureHostSharedInstanceTests|FullyQualifiedName~FilterMiniFidelityTests|FullyQualifiedName~ProjectOverrideRealSolutionTests"` → gruen (11 Tests, 0 Fehler).
`dotnet test src/AiNetLinter.IntegrationTests --no-build --filter "FullyQualifiedName~SymbolGraphCatalogFixtureTests|FullyQualifiedName~GetServerHealthToolTests|FullyQualifiedName~GetIndexScopeToolTests|FullyQualifiedName~SearchPatternToolTests"` → gruen (25 Tests, 0 Fehler).
`dotnet test src/AiNetLinter.IntegrationTests --no-build --filter "FullyQualifiedName~TestMigrationLedgerConsistencyTests|FullyQualifiedName~LegacyProjectBuildGateTests|FullyQualifiedName~TestCategoryProfileGuardTests"` → gruen (6 Tests, 0 Fehler).
Statische Guards → nur `Platform/LoadedFixture.cs` enthaelt `SourceFileCatalog.LoadAsync(`; keine `_fixture.Catalog`-Verwendung in den drei gemeinsamen Konsumenten; keine `Catalog`-/`Workspace`-Owner-Property im SymbolGraph-Fixture; TD-004-Kommentarcheck leer; `git --no-pager diff --check` gruen.

## Abweichungen vom Plan

Keine fachliche Abweichung. Nach dem ersten Code-Commit wurde die noch indirekt exponierte `Workspace`-Property als Owner-Leak erkannt und in einem zweiten, getrennten lokalen Code-Commit auf `RootPath` plus `Snapshot` reduziert.

## Beobachtungen

Der erste Build schlug ausschliesslich wegen der Sichtbarkeitsregel fehl: Ein oeffentlicher Fixture-Helper darf keinen internen `McpCodeGraphServer` zurueckgeben. Der Helper ist deshalb `internal`; ein Fixversuch von sechs wurde verbraucht.

## Bekannte Unschärfen

Kein Voll-, Dogfood-, Performance- oder Stresslauf. TD-004 bleibt mit dem leeren Kommentarcheck und dem 11er-Plattformfilter geschlossen; TD-009 bleibt mit Max-2-Vertrag, Callsite-Guard, fehlender Fixture-Catalog-Exposition und 25er-Ownershipfilter geschlossen. TD-010 blieb unveraendert offen.
