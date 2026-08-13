---
status: done (pending audit)
type: step-result
task: speedup-tests
step: 019
epic: EPIC-4
step_type: batch
coded_by: coder
coded_by_model: gpt-5.6-terra
coded_by_model_knowledge_cutoff: nicht ausgewiesen
coded_at: 2026-08-13T19:50:02+02:00
code_commit_hash: 6413510
status_after: done (pending audit)
blocker_category: n/a
---

# Result Step 019: EPIC-4-Grenze fuer Find-Symbol

## Zusammenfassung

Die beiden Legacy-Klassen sind geloescht und ihre 20 historischen Methoden entlang der echten
Ausfuehrungsgrenze migriert: elf Snapshot-/Dispatchvertraege nach FastTests, neun
C#-Leermengen-/Miss-Hint-Vertraege als hermetischer Diskadapter nach IntegrationTests. Der
Integration-Adapter besitzt pro Testklasse genau eine isolierte `SymbolGraphMini`-Kopie und einen
`SourceFileCatalog.LoadAsync`-Load. Es wurde keine Produkt-Seam oder TestKit-Abstraktion ergaenzt.

## Geänderte Dateien

- item-01: `src/AiNetLinter.FastTests/Mcp/Tools/FindSymbolScannerTests.cs` — zwei Scanner-Snapshotvertraege und der isolierte Trunkierungs-Unitvertrag.
- item-01: `src/AiNetLinter.FastTests/Mcp/Tools/FindSymbolToolTests.cs` — acht Tool-Dispatch-, Structured-Content- und Compile-Error-Snapshotvertraege.
- item-02: `src/AiNetLinter.IntegrationTests/Mcp/Tools/FindSymbolFileAdapterTests.cs` — neun Miss-Hint- und Leermengenvertraege mit lokalem asynchronem Fixture.
- item-03: `src/AiNetLinter.Tests/Mcp/Tools/FindSymbolScannerTests.cs` / `FindSymbolToolTests.cs` — Legacy-Quellen entfernt.
- item-03: `tasks/speedup-tests/test-migration-ledger.md` — beide Klassen auf `migrated` gesetzt.
- item-03: `tasks/speedup-tests/codemap.md`, `roadmap.md`, `task-state.md` und `step-019/step-plan.md` — Zielorte, Epic-Status und Step-Abschluss aktualisiert.

## Commit

- **Code-Commit-Hash:** `6413510`
- **Message:**
  ```
  test(mcp): migriere Find-Symbol-Vertraege [speedup-tests]
  ```
- **Branch:** main
- **Push:** nein (lokal)
- **Doku-Commit:** separater, zweiter Commit folgt.

## Build-/Test-Output

```
dotnet test src/AiNetLinter.Tests --filter "FullyQualifiedName~FindSymbolScannerTests|FullyQualifiedName~FindSymbolToolTests" → grün (20 Tests, 0 Fehler)
dotnet build → grün (0 Warnungen, 0 Fehler)
dotnet test src/AiNetLinter.FastTests --no-build --filter "FullyQualifiedName~FindSymbol|FullyQualifiedName~McpCodeGraphServerReadOnlySnapshotTests|FullyQualifiedName~RoslynTestSolutionFactoryTests|FullyQualifiedName~FastTestsDependencyGuardTests|FullyQualifiedName~TestCategoryProfileGuardTests" → grün (25 Tests, 0 Fehler)
dotnet test src/AiNetLinter.IntegrationTests --no-build --filter "FullyQualifiedName~FindSymbolFileAdapterTests|FullyQualifiedName~TestMigrationLedgerConsistencyTests|FullyQualifiedName~LegacyProjectBuildGateTests|FullyQualifiedName~TestCategoryProfileGuardTests" → grün (15 Tests, 0 Fehler)
dotnet test src/AiNetLinter.FastTests --no-build --filter "Category=Component" → grün (289 Tests, 0 Fehler)
```

## Abweichungen vom Plan

Keine — Plan 1:1 umgesetzt. Die neun Dateivertraege werden in einer Zielklasse gehalten, damit
Kopie, Katalog und deterministisches Dispose pro Klasse einmal stattfinden.

## Beobachtungen

Der produktseitige Coverage-Abgleich ordnet die historischen Verträge `ValidKinds`,
Max-Result-Trunkierung, Compile-Fehler-Header, Location-Formatierung und Miss-Hint dem jeweiligen
Snapshot- oder Dateiadapterziel zu. Loading-Zweig und `OperationCanceledException`-Durchreichung
sind im vorliegenden historischen Find-Symbol-Schnitt nicht als eigenständige Verträge vorhanden;
es wurde dafür kein zusätzlicher Produktcode geändert.

Der abschließende DRY-Scan (`find_duplicates`, `src`, 20 Tokens) meldet zwei in dieser Migration
entstandene exakte Paare: die beiden Plain-No-Match-Tests und das lokale Fixture-Dispose-Muster.
Die Plain-No-Match-Methoden bleiben getrennt, weil sie zwei historische Klassenverträge belegen;
das lokale Dispose bleibt, weil eine gemeinsame Fixture-Abstraktion ohne zweiten Konsumenten den
Step-Scope erweitern würde. Alle übrigen exakten/nahen Cluster lagen bereits außerhalb dieses
Steps.

## Bekannte Unschärfen

Der Ledger-Guard unterstützt pro historische Klasse einen einzelnen maschinell pruefbaren
Abdeckungsdateipfad. Die zusaetzliche Integration-Abdeckung der gemischten Klassen ist deshalb in
der CodeMap dokumentiert und wird durch den gezielten Integrationfilter ausgefuehrt.
