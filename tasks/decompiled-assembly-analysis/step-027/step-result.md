---
status: done (pending audit)
type: step-result
task: decompiled-assembly-analysis
step: 027
epic: EPIC-05
step_type: correction
coded_by: coder
coded_by_model: gpt-5
coded_at: 2026-08-29
code_commit_hash: siehe Abschluss-Commit
status_after: done
blocker_category: n/a
---

# Result Step 027: Step-026-Cache-Publish-Vertrag korrigieren

## Zusammenfassung

Der Cache-Publish-Vertrag hält den Same-Key-Lease jetzt über vollständige
Generation, generation-aware Current-Prüfung, Pointer-Commit, Read-back,
Rollback und Staging-Cleanup. Ein Cleanup löscht keine aktuell referenzierte
Generation. Manifest und unabhängiges Inventar werden getrennt geprüft; beide
müssen das erwartete SolutionPath und dasselbe vollständige Inventar belegen.
Metadaten und Content werden bounded als striktes UTF-8 bzw. bounded Hash-Stream
gelesen. Acquirer- und Writer-Tests verwenden ausschließlich injizierte
Test-Roots bzw. Recording-Writer.

## Geänderte Dateien

- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCacheContract.cs`
- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCacheModels.cs`
- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCacheReader.cs`
- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCacheReadSupport.cs`
- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCacheInventoryValidationParameters.cs`
- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCacheStorage.cs`
- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCacheMetadataStorage.cs`
- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCacheCleanup.cs`
- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCacheWriter.cs`
- Cache-/Acquirer-Testdateien in `src/AiNetLinter.FastTests/Mcp/Assemblies/`
- `tasks/decompiled-assembly-analysis/step-027/step-result.md`

## Kriterienabdeckung

- **A Same-Key-Synchronisation:** Ein pro Cache-Key gehaltener Lease umfasst
  auch generation-aware Pointer-Ersetzung, den erwarteten vorherigen
  Generationstand, Rollback und Cleanup. Rollback ersetzt nur den noch auf die
  fehlgeschlagene Generation zeigenden Pointer; ein neuerer Current bleibt
  erhalten. Es gibt keine globale oder Cross-Process-Lock-Infrastruktur,
  Sleeps oder Retries.
- **B Read-back:** Manifest und Inventory werden unabhängig validiert. Ein
  verkürztes Paar aus Manifest und Content wird abgewiesen; fehlende,
  zusätzliche, doppelte oder abweichende Einträge, fehlender Solution-Anker,
  Hash-/Längenabweichungen, Truncation, Wachstum, Limits und Reparse-Punkte
  führen fail-closed zum Verwerfen. Alle Dateiwalks, JSON-Lesen und Hashes sind
  bounded.
- **C Testisolation:** Der Runtime-Default bleibt unverändert. Acquirer-Tests
  injizieren TestTempDirectory plus Local-Writer oder Recording-Writer;
  Writer-Tests schreiben ebenfalls nur in TestTempDirectory. Die alten vier
  Generationen unter dem FastTests-AppContext-Cache wurden nicht gelöscht und
  blieben unverändert.
- Bestehende Key-/Schema-/Manifest-/Current-, Ownership-, Cancellation-,
  PathGuard-, Reparse-, Cleanup-, Credential-, HTTP-, Git-, Process- und
  Native-Invarianten sowie der fail-open Acquirer-Erfolg bleiben erhalten.

## Build-/Test-Output

- `dotnet build --no-restore` — grün, 0 Warnungen, 0 Fehler.
- Fokussierter Cache-/Acquirer-Lauf — 57 bestanden, 2 übersprungen, 0 Fehler.
- `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress --no-restore`
  — 2019 bestanden, 2 übersprungen, 0 Fehler, 2021 gesamt.
- `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress --no-restore`
  — 370 bestanden, 0 übersprungen, 0 Fehler.
- Die beiden Skips sind ausschließlich transparente echte Reparse-Tests mit
  Win32-Fehler 1314. Die Stress-Kategorie wurde nicht ausgeführt.

## MCP-/DRY-/MagicValues-/DeadCode-Nachweis

- Semantisch geprüft wurden die geänderten Writer-, Reader-, Storage- und
  Acquirer-Symbole mit `get_feature_context`, `get_symbol_body`,
  `find_references` und `get_impact`; Violations wurden mit absolutem
  `projectRoot` nachgeprüft.
- `get_violations` meldet für Cache-, Acquirer- und Provider-Scope jeweils
  keine Code-Violations. Der scoped `safeguard`-Score bleibt wegen bestehender
  Directory-/AIContext-Footprint-Befunde bei 5,90; diese liegen außerhalb des
  Step-027-Codes und wurden nicht verändert.
- Der `drift-audit`-Skill wurde ausgeführt. Der solutionweite Token-Scan fand
  bestehende Cluster; im relevanten Produktions-Assemblies-Scope gab es 0
  Exact- und 0 Near-Cluster, im relevanten Test-Scope ebenfalls 0/0. Der
  strukturelle Scan lieferte 23 bestehende Kandidaten, keinen Cache-Publish-
  Kandidaten. Es gab daher keine neue Konsolidierung und keinen Tech-Debt-Fund.
- Der scoped `find_magic_values`-Audit meldete 33 Treffer in 32 Einträgen,
  überwiegend absichtliche Test-Identifikatoren, Fixture-URLs und bestehende
  Vertragskonstanten. Kein globaler Sweep und keine künstliche
  AssemblyCache-Vereinheitlichung wurden durchgeführt.
- `find_dead_code` meldete 0 unreferenzierte Symbole bei 29 geprüften Symbolen.

## Testisolation und Aufräumnachweis

Nach dem Lauf existieren keine aktiven `.ainet-test-owner-*`-Marker und keine
`testhost`-/`vstest.console`-Prozesse. Der bestehende
`src/AiNetLinter.FastTests/bin/Debug/net10.0/cache/source`-Bestand blieb bei 9
Dateien und 4 Generationen; alte fremde Temp-Reste wurden nicht gelöscht.

## Abweichungen und offene Risiken

Keine Abweichung vom Korrekturscope. `task-state.md`, Roadmap und `tech-debt.md`
wurden nicht geändert. Die Same-Key-Synchronisation ist bewusst die bestehende
prozessinterne Lease-Infrastruktur; eine Cross-Process-Sperre bleibt außerhalb
des Vertrags. Reparse-Tests können auf Windows weiterhin mit Win32 1314
übersprungen werden.
