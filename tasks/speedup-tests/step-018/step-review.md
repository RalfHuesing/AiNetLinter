---
status: done
type: step-review
task: speedup-tests
step: 018
epic: EPIC-4
step_type: batch
reviewed_by: kritiker
reviewed_by_model: gpt-5.6-terra
reviewed_by_model_knowledge_cutoff: nicht ausgewiesen
reviewed_at: 2026-08-13T19:20:25+02:00
verdict: issues
tech_debt_ids: []
---

# Review Step 018: Fuenf rohe FastTests-Helper auf virtuelle Snapshots schliessen

## Verdict

- [ ] **approved**
- [x] **issues** — Korrektur-Step erforderlich
- [ ] **blocked**

## Geprüft

- [x] Plan-Erfüllung: Commit-Diffs, Scope und alle vier Batch-Items geprüft
- [x] Rules-Konformität: Plan-Grenzen sowie FastTests-Kategorie/Parallelität geprüft
- [x] Logische Korrektheit: Snapshot- und Live-Refresh-Zweig, virtuelle Pfade, Testverträge geprüft
- [x] Konzept-Treue: Immutable-Snapshot- und isolierte Live-Refresh-Verträge geprüft
- [ ] Build: nicht erneut ausgeführt; der dokumentierte `dotnet build`-Nachweis wurde nur gelesen
- [x] Tests: `65/65` FastTests (62 Zielverträge + 3 Snapshot-Seam-Verträge) und `8/8` Legacy-Live-Refresh selbst grün ausgeführt

## Befund

### Plan-Erfüllung

Item-01 bis Item-03 sind in den fünf Zielklassen sachlich umgesetzt: exakt 15+10+9+11+17 = 62 `[Fact]`/`[Theory]`-Verträge blieben erhalten, der Diff enthält keine geänderte öffentliche Testmethode und die Assertion-Zahlen sind unverändert (41+46+42+31+66 = 226). Item-04 ist jedoch nicht erfüllt, weil der Code-Commit die explizite Recovery-Scope-Grenze massiv überschreitet und die Ergebnisdokumentation dies als „Keine — Plan 1:1 umgesetzt“ ausweist.

### Rules-Konformität

Der exakt aus dem Ledger-Diff abgeleitete 23-Dateien-Scope enthält keine Treffer für reale Datei-/Verzeichnisoperationen, `SourceFileCatalog.LoadAsync`, manuelle `SourceFileCatalog`-Server, `MSBuildWorkspace`, `ConsoleTestCollection`, `DisableParallelization`, `TempSourceDirectory`, `_tempDir` oder `BuildServer`; die fünf neuen Component-Klassen besitzen jeweils genau einen gültigen Kategorie-Trait.

### Logische Korrektheit

`McpInMemoryTestContext` übergibt den besitzenden `RoslynTestSolution` als `ReadOnlySolutionSnapshot`; `McpCodeGraphServer` trennt diesen Zweig vom bisherigen `LoadFunc`-/Catalog-Pfad und überspringt nur für Snapshots den Refresh. Die drei neuen Snapshot-Verträge sowie die 8 Legacy-Live-Refresh-Tests sind grün; `VirtualProjectDirectory: "."` hält die relativen Dokumentpfade der fünf Zielklassen. `SuppressionScannerTests` existiert weiterhin ausschließlich im Legacy-Projekt und bleibt im Ledger `pending`.

### Konzept-Treue (Ebene 4)

Die technische Zielrichtung stimmt mit dem Konzept überein: vorbereitete immutable In-Memory-Solutions werden für read-only Component-Verträge verwendet, während der mutable Live-/Refresh-Vertrag weiter gezielt im Legacy-Projekt getestet wird.

### Build-/Test-Status

```
dotnet test src\AiNetLinter.FastTests --no-build --filter "...fünf Zielklassen...|...McpCodeGraphServerReadOnlySnapshotTests" → grün (65 Tests, 0 Fehler)
dotnet test src\AiNetLinter.Tests --no-build --filter "...ConstructorTests|...FileDiscoveryTests|...StalenessMtimeCacheTests" → grün (8 Tests, 0 Fehler)
TestResults/latest.trx vor den Auditläufen: dokumentiertes Integration-Ledger-/Legacy-Gate → grün (5 Tests, 0 Fehler)
```

## Findings

1. **item-04** — [MAJOR] [Plan-Erfüllung] [`tasks/speedup-tests/step-018/step-plan.md:41`](tasks/speedup-tests/step-018/step-plan.md:41) und [`tasks/speedup-tests/step-018/step-plan.md:168`](tasks/speedup-tests/step-018/step-plan.md:168) verbieten Produktcode-, Shared-Fixture-, Ledger- und Legacy-Änderungen und begrenzen den Recovery-Diff auf die fünf Zieltests plus Planungsartefakte. `f0dbacc` ändert dagegen 40 `src`-Dateien, darunter 7 Produktdateien, TestKit, Shared-Fixtures, 23 weitere FastTests-Klassen und `src/AiNetLinter.Tests/Suppression/SuppressionScannerTests.cs`; nur 5 Dateien sind die geplanten Zieltests. [`tasks/speedup-tests/step-018/step-result.md:61`](tasks/speedup-tests/step-018/step-result.md:61) behauptet dennoch eine 1:1-Umsetzung. **Fix:** Den Recovery-Plan und das Ergebnis ehrlich auf den tatsächlich gelieferten 40-Dateien-/23-Klassen-Super-Step anpassen, oder die außerplanmäßigen Änderungen aus diesem Step in passende, separat dokumentierte und geprüfte Steps trennen; erst danach Ledger, CodeMap und Gates gegen den bereinigten Scope neu belegen.
