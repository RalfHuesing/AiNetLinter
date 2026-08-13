---
status: done (pending audit)
type: step-result
task: speedup-tests
step: 021
epic: EPIC-5
coded_by: coder
coded_by_model: gpt-5.6-terra
coded_by_model_knowledge_cutoff: nicht ausgewiesen
coded_at: 2026-08-13
code_commit_hash: b2b8fde
status_after: done (pending audit)
blocker_category: n/a
---

# Result Step 021: MSBuild-/Baseline-/Datei-/Refresh-Super-Step

## Zusammenfassung

22 historische Dateien wurden entfernt und ihre 99 benannten Methoden in FastTests bzw. IntegrationTests überführt. Der Split von `SourceFileCatalogTests` legt Policyverträge in FastTests und den echten Loadadapter in IntegrationTests ab. `LoadedFixture` kapselt Lease, Katalog, exception-sicheres Loadbudget (2) und Disposal; Baseline- und SymbolGraph-Hosts verwenden es ohne Collection-Serialisierung.

## Build-/Test-Output

`dotnet test src/AiNetLinter.Tests --filter <22-Klassenfilter>` → grün (102 xUnit-Fälle, 0 Fehler; die Planzahl 99 zählt historische Methoden, Theories erzeugen zusätzliche Fälle).
`dotnet build --no-restore` → grün, 0 Warnungen/Fehler.
`dotnet test src/AiNetLinter.FastTests --no-build --filter <Zielklassen+Guards>` → grün (20 Tests).
`dotnet test src/AiNetLinter.IntegrationTests --no-build --filter <Platform>` → grün (11 Tests).
`dotnet test src/AiNetLinter.IntegrationTests --no-build --filter <migrierte Kohorte>` → grün (85 Tests).

## Abweichungen vom Plan

Die Legacy-Baseline meldete 102 statt der im Plan aufgeführten 99 Testfälle, weil die historische Methodenanzahl Theory-Instanzen nicht einzeln zählt. Assertions und Methodennamen blieben erhalten.

## Beobachtungen

Der erste Ledger-Guard erforderte für den Split einen ausschließlich maschinenlesbaren Primärpfad; der zusätzliche FastTests-Policyort ist daher in CodeMap dokumentiert.

## Bekannte Unschärfen

Kein Voll-, Dogfood-, Performance- oder Stresslauf durchgeführt. TD-010 bleibt bewusst offen.
