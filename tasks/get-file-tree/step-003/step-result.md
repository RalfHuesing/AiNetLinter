---
status: 'done (pending audit)'
type: step-result
task: get-file-tree
step: 003
epic: EPIC-02
step_type: single
coded_by: coder
coded_by_model: GPT-5 (Codex)
coded_by_model_knowledge_cutoff: nicht im Systemkontext angegeben
coded_at: 2026-08-26T23:32:10+02:00
code_commit_hash: 5b8e4472814480b6973732bcd08c430d76d612be
test_infrastructure_fix_commit_hash: 0a45dc16
status_after: 'done (pending audit)'
blocker_category: none
---

# Result Step 003: Gemeinsame Walk-/Optionen-/Glob-Grundlage extrahieren

## Zusammenfassung

Die gemeinsame Walk-Grundlage unterstützt jetzt interne Optionen für Tiefe, Standardausschlüsse und Cancellation sowie unveränderliche Skip-/Partial-Statistiken; der bestehende Datei-Pattern-Walk und `SafeEnumerateFiles*` bleiben kompatibel. Die Glob-Übersetzung liegt zentral in `PathGlobMatcher`, auf den beide `FileFilterEvaluator`-Einstiege delegieren. Unit-, Component- und physische Integrationstests decken die neuen Entscheidungen und die Legacy-Abgrenzung ab.

## Nachtrag: Integrationstest-Blocker behoben

Die zuvor kollidierenden MCP-/Daemon-Vertragstests verwenden nun eine pro
Testprozess eindeutige `daemon-instance` (`tests-<TestRunner-PID>`). Dadurch
bleiben externe AiNetLinter-Installationen und bewusst laufende Benutzer-Daemons
unangetastet, während Janitor, Daemon-Host und ThinClient denselben isolierten
Test-Endpunkt verwenden. Das suiteweite `SubprocessLifetimeGate` wurde auf acht
Slots erhöht: Bei vier parallelen xUnit-Testfällen kann ein Daemon-Vertrag zwei
langlebige Prozesse gleichzeitig halten.

Der zusätzliche Testinfrastruktur-Fix ist in Folgecommit `0a45dc16` zu diesem
Step gesichert.

## Geänderte Dateien

- `src/AiNetLinter/Baseline/FileSystemWalkOptions.cs` (neu) — internes unveränderliches Options-Record mit Default- und File-Tree-Factories.
- `src/AiNetLinter/Baseline/FileSystemExclusionHelpers.cs` — Options-Walk, Tiefen-/Cancellation-Steuerung und sichtbare Skip-Statistiken bei erhaltener Legacy-Überladung.
- `src/AiNetLinter/Baseline/TreeWalkStats.cs` — Cancellation-, Standardausschluss- und Reparse-Skip-Metadaten ergänzt.
- `src/AiNetLinter/Configuration/PathGlobMatcher.cs` (neu) — zentrale separator-normalisierende `*`/`?`/`**`-Globübersetzung.
- `src/AiNetLinter/Configuration/FileFilterEvaluator.cs` — Datei- und Web-Glob-Einstiege auf den Matcher delegiert; Directory-Segmentprüfung unverändert.
- `src/AiNetLinter.FastTests/Baseline/StalenessTreeWalkerTests.cs` — Tiefen-, Cancellation-, Options- und Skip-Zähler-Tests ergänzt.
- `src/AiNetLinter.FastTests/Configuration/PathGlobMatcherTests.cs` (neu) — direkte Glob-Semantiktests ergänzt.
- `src/AiNetLinter.FastTests/Configuration/FileFilterEvaluatorTests.cs` — Wrapper-Regressionsfälle für `?`, `**` und Separatoren ergänzt.
- `src/AiNetLinter.IntegrationTests/Baseline/FileSystemExclusionHelpersTests.cs` — realen Options-Walk mit Standardausschluss und sichtbarer Datei geprüft.

## Commit

- **Code-Commit-Hash:** `5b8e4472814480b6973732bcd08c430d76d612be`
- **Message:**
  ```
  feat: Extrahiere Walk- und Glob-Grundlage [get-file-tree]

  Bündele Optionen, Statistiken und die Glob-Übersetzung für spätere Scanner.
  Bewahre Legacy-Enumeration und bestehende Aufrufer.
  Refs: tasks/get-file-tree/step-003
  ```
- **Branch:** `main`
- **Push:** nein (lokal)
- **Doku-Commit:** separater, zweiter Commit.

## Build-/Test-Output

```
dotnet test src/AiNetLinter.FastTests --filter "FullyQualifiedName~StalenessTreeWalkerTests|FullyQualifiedName~PathGlobMatcherTests|FullyQualifiedName~FileFilterEvaluatorTests" --no-restore → grün (36 Tests, 0 Fehler)
dotnet test src/AiNetLinter.FastTests --filter "FullyQualifiedName~SearchPatternScannerTests|FullyQualifiedName~SearchPatternScannerEvaluationTests" --no-restore → grün (19 Tests, 0 Fehler)
dotnet test src/AiNetLinter.IntegrationTests --filter "FullyQualifiedName~FileSystemExclusionHelpersTests" --no-restore → grün (9 Tests, 0 Fehler)
dotnet build → grün (0 Warnungen, 0 Fehler) (vor dem Testinfrastruktur-Fix)
dotnet test src/AiNetLinter.FastTests --filter Category!=Stress → grün (1.826 Tests, 0 Fehler)
dotnet test src/AiNetLinter.IntegrationTests --filter "FullyQualifiedName~Mcp.Daemon" → grün (8 Tests, 0 Fehler, 0 übersprungen)
dotnet test src/AiNetLinter.IntegrationTests --filter "FullyQualifiedName~CliRepositoryDogfoodTests.RunLinterCli_OnWholeSolution_ReturnsSuccess" → grün (1 Test, 0 Fehler)
dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress → grün (358 Tests, 0 Fehler, 0 übersprungen)
dotnet build --no-restore → final grün (0 Warnungen, 0 Fehler)
dotnet test src/AiNetLinter.FastTests --filter Category!=Stress --no-restore → final grün (1.826 Tests, 0 Fehler, 0 übersprungen)
```

## Abweichungen vom Plan

Die Code- und Teständerungen entsprechen dem Plan. Die Testinfrastruktur musste
im Step-Scope nachgebessert werden, weil externe Daemons den Default-Endpunkt
belegten und das ursprüngliche Prozessbudget bei parallelen Zwei-Prozess-Verträgen
eine Warteschleife zuließ. Nach der Reparatur ist der vorgeschriebene vollständige
Integration-Gate-Lauf grün.

## Beobachtungen

Der Drift-Audit mit `find_duplicates(scopeDir="src", minTokens=20, similarityThreshold="near")` fand 25 Near-Cluster, aber keinen Exact-Cluster und keinen relevanten Cluster in den geänderten Baseline-/Configuration-Bereichen; es wurde kein Tech-Debt-Eintrag angelegt. Der MCP-Violations-Check meldete nach der Komplexitätsaufteilung 0 Verstöße in den geänderten Produktionsbereichen.

## Bekannte Unschärfen

Der Codepfad und alle gezielten Tests sind grün. Der vollständige Fast-Gate ist
mit 1.826/1.826 Tests grün; der vollständige Integration-Gate ohne Stress ist
mit 358/358 Tests, 0 Fehlern und 0 Überspringungen grün. Der einzelne
Dogfood-Vertrag ist zusätzlich isoliert mit 1/1 grün gelaufen.

## Verifikation des Blocker-Fixes

- Daemon-Vertragsslice: 8/8 bestanden, 0 übersprungen.
- Dogfood-Single-Test: 1/1 bestanden.
- Vollständiger Fast-Gate ohne Stress: 1.826/1.826 bestanden.
- Vollständiger Integration-Gate ohne Stress: 358/358 bestanden, 0 übersprungen.
- Externe Installations-Daemons wurden nicht beendet.
