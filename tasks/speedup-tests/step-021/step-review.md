---
status: done
type: step-review
task: speedup-tests
step: 021
epic: EPIC-5
step_type: batch
reviewed_by: kritiker
reviewed_by_model: gpt-5.6-terra
reviewed_by_model_knowledge_cutoff: 2026-01
reviewed_at: 2026-08-13
verdict: issues
tech_debt_ids: []
---

# Review Step 021: MSBuild-/Baseline-/Datei-/Refresh-Super-Step

## Verdict

- [ ] **approved** — alle vier Prüfebenen ok
- [x] **issues** — Korrektur-Step erforderlich
- [ ] **blocked** — Nutzer-Entscheidung nötig

## Geprüft

- [x] Plan-Erfüllung
- [x] Rules-Konformität
- [x] Logische Korrektheit
- [x] Konzept-Treue
- [x] Build/Test-Evidenz und enge Auditläufe

## Befund

### Plan-Erfüllung

Die 21 vollständigen Renames plus die Löschung von `SourceFileCatalogTests` ergeben die verlangten 22 Legacy-Dateien; Ledger und Zielaufteilung sind maschinenlesbar, aber item-01 setzt das für alle realen Loads verlangte Budget nicht vollständig durch.

### Rules-Konformität

Die Zielklassen tragen die vorgesehenen Kategorien und führen keine neue globale Test-Collection ein; die fehlende Begrenzung direkter MSBuild-Loads verletzt jedoch die im Plan referenzierte Parallelitätsvorgabe.

### Logische Korrektheit

`LoadedFixture` gibt den Permit bei Abbruch und Ausnahme korrekt frei, aber der geteilte SymbolGraph-Katalog wird an Besitzer übergeben, die ihn beim Dispose wieder schließen; damit ist die Owner-Grenze nicht korrekt erhalten.

### Konzept-Treue (Ebene 4)

Die Migration bleibt in der vorgesehenen EPIC-5-Kohorte und lässt die ausgeschlossenen EPIC-6/7-Klassen sowie TD-010 unangetastet, verfehlt aber die zugesagte exception-safe Begrenzung realer MSBuild-Loads auf zwei.

### Build-/Test-Status

```text
Dokumentierte Legacy-Baseline (vor Löschung) → grün (102 xUnit-Fälle, 0 Fehler)
Dokumentierter Build → grün (0 Warnungen, 0 Fehler)
Dokumentierter Fast-Filter → grün (20 Tests, 0 Fehler)
Audit: dotnet test src/AiNetLinter.IntegrationTests --no-build --filter "FullyQualifiedName~LoadedFixture|FullyQualifiedName~MsBuildFixtureHost|FullyQualifiedName~FindSymbolFileAdapterTests" → grün (11 Tests, 0 Fehler)
Audit: dotnet test src/AiNetLinter.IntegrationTests --no-build --filter "FullyQualifiedName~GetServerHealthToolTests|FullyQualifiedName~GetIndexScopeToolTests|FullyQualifiedName~SearchPatternToolTests" → grün (24 Tests, 0 Fehler)
Dokumentierter migrierter Integration-Filter → grün (85 Tests, 0 Fehler)
Dokumentierte Guards → grün (6 Tests, 0 Fehler)
```

## Findings

1. `src/AiNetLinter.IntegrationTests/Platform/FilterMiniFidelityTests.cs:36` und `src/AiNetLinter.IntegrationTests/Configuration/ProjectOverrideRealSolutionTests.cs:35` — [MAJOR] [Plan/Rules/Logik] Beide echten `SourceFileCatalog.LoadAsync`-Aufrufe umgehen `LoadedFixture.LoadBudget`. Item-01 verlangt ausdrücklich, alle heutigen direkten Integration-Loads mitzuziehen, wenn das für die tatsächliche Durchsetzung des Budgets nötig ist; der statische Scan zeigt diese zwei Ausnahmen. **Fix:** Beide Aufrufe über den budgetierten Owner bzw. `LoadedFixture.LoadCatalogAsync` führen und ihre Katalog-/Lease-Lebensdauer eindeutig entsorgen; anschließend einen kleinen Max-2-Vertrag ergänzen, der auch diese Pfade einschließt.
2. `src/AiNetLinter.IntegrationTests/Mcp/Tools/GetServerHealthToolTests.cs:33`, `src/AiNetLinter.IntegrationTests/Mcp/Tools/GetIndexScopeToolTests.cs:36` und `src/AiNetLinter/Mcp/McpCodeGraphServer.cs:202` — [MAJOR] [Plan/Logik] Die Tests übergeben den assembly-weit geteilten `SymbolGraphCatalogFixture.Catalog` direkt an `McpCodeGraphServer`; dessen `Dispose()` ruft auf genau diesem Katalog `Dispose()` auf. Damit können `using var state`-Tests den vom Fixture-Owner verwalteten MSBuildWorkspace schließen, während parallele Leser weiterhin dieselbe Solution verwenden. Der grüne 24er-Auditlauf ist kein Gegenbeweis, weil die konkrete `MSBuildWorkspace.Dispose()`-Auswirkung reihenfolgeabhängig bleibt. **Fix:** Read-only-Konsumenten über `ReadOnlySolutionSnapshot` oder einen gleichwertigen nichtbesitzenden Snapshot-Adapter instanziieren, statt den Fixture-Katalog zu übertragen; nur `LoadedFixture` darf dessen Katalog entsorgen. Ergänze einen expliziten Vertrag, dass nach einem disposeden Tool-Server ein weiterer Leser des Fixture-Snapshots weiterhin funktioniert.
