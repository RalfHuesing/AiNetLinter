---
status: done (pending audit)
type: step-result
task: speedup-tests
step: 023
epic: EPIC-5
step_type: batch
coded_by: coder
coded_by_model: gpt-5.6-terra
coded_by_model_knowledge_cutoff: nicht ausgewiesen
coded_at: 2026-08-13
code_commit_hash: 312b652
status_after: done (pending audit)
blocker_category: n/a
---

# Result Step 023: Config-/Suppression-Dateikohorte und EPIC-5-Grenzgate

## Zusammenfassung

Die 21 Legacy-Klassen liegen nun in FastTests (zwölf Unit-/Component-Klassen) beziehungsweise
IntegrationTests (neun Datei-/Commandadapter). Der Checkout-mutierende Self-Repository-Sync wurde
gegen den stärkeren Temp-Root-Vertrag konsolidiert; dessen Generatorinhalt belegt jetzt beide
Projekt-Overrides. TD-003 ist durch die mechanische Regenerierung von `AiNetLinter.mdc` geschlossen.

## Geänderte Dateien

- `src/AiNetLinter.FastTests/Cli/`, `Configuration/`, `Core/`, `Suppression/` — Item 01-03: zwölf reine Policy-, Config- und Compound-Suppression-Verträge nach FastTests verschoben.
- `src/AiNetLinter.IntegrationTests/Configuration/`, `Suppression/` — Item 04-06: neun Datei-/Commandadapter auf private Temp-Wurzeln oder `BaselineMiniFixtureWorkspace` migriert.
- `.agents/rules/AiNetLinter.mdc` — Item 07: vom Generator aus `rules.json` synchronisiert.
- `tasks/speedup-tests/test-migration-ledger.md` — Item 08: 21 Zeilen auf Zielorte aktualisiert; ein Self-Repo-Sync-Vertrag als konsolidiert markiert.

## Commit

- **Code-Commit-Hash:** `312b652`
- **Message:**
  ```
  test: migriere Config- und Suppressionsvertraege [speedup-tests]

  Refs: tasks/speedup-tests/step-023
  ```
- **Branch:** `main`
- **Push:** nein (lokal)
- **Doku-Commit:** separater, zweiter Commit.

## Build-/Test-Output

`dotnet test src/AiNetLinter.Tests --filter <21 Klassen>` → grün (139 Runner-Fälle, 0 Fehler; historisch 126 Methoden/140 statisch sichtbare Fälle).
`dotnet build` → grün (0 Warnungen, 0 Fehler).
`dotnet test src/AiNetLinter.FastTests --no-build --filter <12 Klassen>` → grün (98 Tests, 0 Fehler).
`dotnet test src/AiNetLinter.IntegrationTests --no-build --filter <9 Klassen>` → grün (41 Tests, 0 Fehler).
`dotnet test src/AiNetLinter.IntegrationTests --no-build --filter "FullyQualifiedName~TestMigrationLedgerConsistencyTests|FullyQualifiedName~LegacyProjectBuildGateTests|FullyQualifiedName~TestCategoryProfileGuardTests"` → grün (6 Tests, 0 Fehler).
`dotnet test src/AiNetLinter.FastTests --no-build --filter "FullyQualifiedName~FastTestsDependencyGuardTests"` → grün (2 Tests, 0 Fehler).
`dotnet test src/AiNetLinter.FastTests --no-build --filter <Roslyn-Teilmenge Step 023>` → grün (55 Tests, 0 Fehler).

## Abweichungen vom Plan

Die Legacy-Baseline führte 139 Runner-Fälle statt der statisch gezählten 140 aus; die Theory-Aufteilung ist der Unterschied. Der Self-Repository-Sync wurde wie geplant transparent konsolidiert, sodass die Zielsumme ebenfalls 139 Runner-Fälle ergibt. Der Generatorlauf lieferte zusätzlich den erwarteten CLI-Audit-Hinweis ohne `--config`; die Regenerierung selbst war erfolgreich und der Diff beschränkt sich auf die zwei Projekt-Overrides.

## Beobachtungen

Der vollständige Fast-Unit/Component-Filter führt reproduzierbar alle 777 Testfälle erfolgreich aus, endet aber in der bestehenden Runtime-Dependency-Guard-Collection mit `Microsoft.CodeAnalysis.Workspaces.MSBuild` als dynamisch geladener Assembly. Der statische Guard und die Step-023-Roslyn-Konsumenten sind einzeln grün; auch der einzige FastTests-Pfad mit vollständigem AppDomain-Assemblyscan ist zusammen mit dem Guard grün. Der Kritiker soll die verbleibende Auslöserklasse vor einer möglichen Guard-Änderung separat bestimmen.

Der vollständige Integration-Filter hängt in vorbestehenden realen MSBuild-/MCP-Klassen und ließ die Loadbudget-Tests nach parallelen Gate-Starts fehlschlagen. Die gezielte Step-023-Integrationkohorte und die Ledger-/Kategorieguards sind unabhängig davon grün; die vom Coder gestarteten hängenden Testprozessketten wurden nach PID-/Commandline-Prüfung gezielt beendet.

Der geforderte MCP-basierte `find_duplicates(scopeDir="src", minTokens=20)`-Audit konnte in dieser Agentensitzung nicht ausgeführt werden, weil das Projekt-MCP-Tool nicht als aufrufbare Capability bereitstand. Kein Ersatz-Scan wurde verwendet.

## Bekannte Unschärfen

Kein Dogfood-, Performance- oder Stresslauf. Die beiden vollständigen EPIC-Profilgrenzen benötigen im Kritiker-Audit eine saubere Einzelprozessausführung: Fast wegen der Runtime-Guard-Drift, Integration wegen langlaufender echter MSBuild-/MCP-Kohorten. TD-010 bleibt offen; nach dieser Migration verbleiben 20 Legacy-Konsumenten der alten Workspace-Familie.
