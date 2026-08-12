---
status: done
type: step-result
task: speedup-tests
step: 010
epic: EPIC-3
step_type: single
coded_by: coder
coded_by_model: "gpt-5.6-sol High (Ersatz fuer nicht auswaehlbares gpt-5.6-luna High)"
coded_by_model_knowledge_cutoff: "nicht ausgewiesen"
coded_at: 2026-08-12T21:18:15.9913352+02:00
code_commit_hash: 8c1552f
status_after: done
blocker_category: n/a
---

# Result Step 010: EPIC-3 Teil 1 — Core/Checkers-Kohorte nach AiNetLinter.FastTests migrieren

## Zusammenfassung

Die 28 Checker-Testklassen wurden ohne Testlogik- oder Assertion-Aenderung nach
`AiNetLinter.FastTests` verschoben; nur der Namespace wechselte. Eine FastTests-lokale,
auf die benoetigten Methoden begrenzte `TestHelper`-Teilmenge traegt ihre bisherigen
Abhaengigkeiten. Das Ledger weist alle 28 Klassen mit existierendem neuen Abdeckungsort als
`migrated` aus; die Testfallzahl blieb mit 236 vor und nach dem Move identisch.

## Geänderte Dateien

- `src/AiNetLinter.FastTests/Core/Checkers/*.cs` — 28 Checker-Testklassen aus dem Legacy-Projekt verschoben und auf den FastTests-Namespace umgestellt.
- `src/AiNetLinter.FastTests/TestHelper.cs` (neu) — nur die von der migrierten Kohorte benoetigte Helper-Teilmenge aus dem Legacy-Testprojekt uebernommen.
- `tasks/speedup-tests/test-migration-ledger.md` — 28 Checker-Eintraege auf `migrated` gesetzt und mit ihren neuen Dateipfaden verknuepft.

## Commit

- **Code-Commit-Hash:** `8c1552f`
- **Message:**
  ```
  refactor(tests): migriere Checker-Kohorte [speedup-tests]

  Refs: tasks/speedup-tests/step-010
  ```
- **Branch:** main
- **Push:** nein (lokal)
- **Doku-Commit:** separater, zweiter Commit (Hash steht nicht hier drin — Selbstbezug, siehe `git log`).

## Build-/Test-Output

```
dotnet test src/AiNetLinter.Tests --filter FullyQualifiedName~Core.Checkers (detached HEAD vor Move) → gruen (236 Tests, 0 Fehler)
dotnet build → gruen (0 Warnungen, 0 Fehler)
dotnet build src/AiNetLinter.FastTests → gruen (0 Warnungen, 0 Fehler)
dotnet build src/AiNetLinter.Tests → gruen (0 Warnungen, 0 Fehler)
dotnet test src/AiNetLinter.FastTests --no-build --filter FullyQualifiedName~Core.Checkers → gruen (236 Tests, 0 Fehler)
dotnet test src/AiNetLinter.IntegrationTests --no-build --filter FullyQualifiedName~TestMigrationLedgerConsistencyTests → gruen (4 Tests, 0 Fehler)
dotnet test src/AiNetLinter.IntegrationTests --no-build --filter FullyQualifiedName~LegacyProjectBuildGateTests → gruen (1 Test, 0 Fehler)
dotnet test src/AiNetLinter.FastTests --no-build --filter FullyQualifiedName~FastTestsDependencyGuardTests → gruen (2 Tests, 0 Fehler)
dotnet test src/AiNetLinter.FastTests --no-build --filter FullyQualifiedName~TestCategoryProfileGuardTests → gruen (1 Test, 0 Fehler)
```

## Abweichungen vom Plan

Der uebernommene Working Tree enthielt den Move bereits, bevor die verlangte Legacy-Vergleichsbasis
gemessen war. Deshalb wurde der Vorher-Lauf in einem temporaeren detached Worktree auf dem
unveraenderten Step-Start-Commit `19d1c82` rekonstruiert; der Worktree wurde danach entfernt. Der
Step-Plan stand trotz des bereits vorhandenen In-Progress-Orchestrierungscommits noch auf `open` und
wurde direkt auf `done (pending audit)` gesetzt. Reine Whitespace-Abweichungen in sechs uebernommenen
Dateien wurden vor dem Commit entfernt, sodass alle 28 Moves nur den Namespace aendern.

## Beobachtungen

Die bereits vorhandene, uncommittete Aenderung an `tasks/speedup-tests/task-state.md` betrifft die
vom Nutzer vorgegebene Modellwahl und wurde bewusst nicht in einen der Step-010-Commits aufgenommen.
Das angeforderte Modell GPT-5.6 Luna High war in der Agentenauswahl nicht verfuegbar; umgesetzt wurde
der Step mit GPT-5.6 Sol High.

## Bekannte Unschärfen

Keine.
