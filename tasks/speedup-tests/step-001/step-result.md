---
status: done
type: step-result
task: speedup-tests
step: 001
epic: EPIC-1
step_type: single
coded_by: coder
coded_by_model: claude-sonnet-5
coded_by_model_knowledge_cutoff: 2026-01
coded_at: 2026-08-12
code_commit_hash: b1fe9eb
status_after: done
blocker_category: n/a
---

# Result Step 001: Drei neue Testzielprojekte + gemeinsame Props + Config-Verträge auf neue Namen

## Zusammenfassung

Die drei Zielprojekte `AiNetLinter.FastTests`, `AiNetLinter.IntegrationTests` und
`AiNetLinter.TestKit` existieren jetzt als arbeitsfähige, in `AiNetLinter.slnx` eingebundene
Hüllen mit gemeinsamer `tests/AiNetLinter.TestProject.props`. `rules.json` löst `ProjectOverrides`
und `TestSentinel.TestProjectNameSuffixes` jetzt auch für die drei neuen Namen auf. Je ein
Unit-, Component- und Integrationstest belegt die korrekte Auflösung.

## Geänderte Dateien

- `tests/AiNetLinter.TestProject.props` (neu) — gemeinsame `TargetFramework`/`Nullable`/
  `TreatWarningsAsErrors`-Einstellungen und das `Microsoft.Build.Framework`/`Microsoft.NET.StringTools`-
  Paketpinning (18.8.2) für alle drei neuen Projekte, ohne festes `RunSettingsFilePath`.
- `src/AiNetLinter.FastTests/AiNetLinter.FastTests.csproj` (neu) — importiert die Props, xUnit-v3-
  Pakete, `ProjectReference` auf `AiNetLinter` und `AiNetLinter.TestKit`.
- `src/AiNetLinter.FastTests/Configuration/ProjectOverrideResolutionTests.cs` (neu) — Unit-Test
  (`Category=Unit`), belegt `ProjectConfigResolver.ResolveForProject` für die drei neuen Namen
  gegen die echte `rules.json`.
- `src/AiNetLinter.FastTests/Core/TestProjectDetectorSuffixTests.cs` (neu) — Component-Test
  (`Category=Component`, `AdhocWorkspace`), belegt `TestProjectDetector.IsTestProject` über den
  Namens-Suffix-Fallback für die drei neuen Namen.
- `src/AiNetLinter.IntegrationTests/AiNetLinter.IntegrationTests.csproj` (neu) — analoges
  SDK-Testprojekt, referenziert `AiNetLinter.TestKit` zusätzlich, eigenes `xunit.runner.json`.
- `src/AiNetLinter.IntegrationTests/xunit.runner.json` (neu) — `parallelizeAssembly: false`,
  identisch zum heutigen Wert in `AiNetLinter.Tests`.
- `src/AiNetLinter.IntegrationTests/Configuration/ProjectOverrideRealSolutionTests.cs` (neu) —
  Integrationstest (`Category=Integration`), lädt die echte `AiNetLinter.slnx` via
  `SourceFileCatalog.LoadAsync` und prüft `ResolveForProject` + `IsTestProject` mit echten
  Metadatenreferenzen für die drei neuen Namen.
- `src/AiNetLinter.TestKit/AiNetLinter.TestKit.csproj` (neu) — leere SDK-Class-Library, importiert
  die Props, `ProjectReference` auf `AiNetLinter`, keine xUnit-Abhängigkeit.
- `AiNetLinter.slnx` — die drei neuen `.csproj` unter `/src/` ergänzt.
- `rules.json` — `ProjectOverrides`-Schlüssel `"*.Tests"` zu `"*Tests"` geändert (matcht jetzt
  `AiNetLinter.Tests`, `AiNetLinter.FastTests`, `AiNetLinter.IntegrationTests`), zusätzlicher
  Schlüssel `"AiNetLinter.TestKit"` mit identischem Override-Inhalt ergänzt;
  `TestSentinel.TestProjectNameSuffixes` um `"TestKit"` erweitert.

## Commit

- **Code-Commit-Hash:** `b1fe9eb`
- **Message:**
  ```
  feat(tests): drei neue Testzielprojekte und Konfigurationsvertraege auf neue Namen [speedup-tests]

  Legt AiNetLinter.FastTests, AiNetLinter.IntegrationTests und AiNetLinter.TestKit als
  arbeitsfaehige, in AiNetLinter.slnx eingebundene Huellen mit gemeinsamer
  tests/AiNetLinter.TestProject.props an. Erweitert rules.json ProjectOverrides ("*Tests" statt
  "*.Tests" plus AiNetLinter.TestKit) und TestSentinel.TestProjectNameSuffixes (TestKit), damit die
  drei neuen Projektnamen denselben Test-Override wie das Legacy-Projekt erhalten. Je ein Unit-,
  Component- und Integrationstest belegt die Aufloesung fuer alle drei neuen Namen.

  Refs: tasks/speedup-tests/step-001
  ```
- **Branch:** main
- **Push:** nein (lokal)
- **Doku-Commit:** separater, zweiter Commit (Hash steht nicht hier drin).

## Build-/Test-Output

```
dotnet build → grün (0 Warnungen, 0 Fehler, 5 Projekte)
dotnet test src/AiNetLinter.FastTests --filter FullyQualifiedName~ProjectOverrideResolutionTests|FullyQualifiedName~TestProjectDetectorSuffixTests → grün (6 Tests)
dotnet test src/AiNetLinter.IntegrationTests --filter FullyQualifiedName~ProjectOverrideRealSolutionTests → grün (3 Tests)
dotnet test --filter FullyQualifiedName~ArchitectureTests → grün (13 Tests, Legacy-`*.Tests`-Verhalten unverändert)
```

## Abweichungen vom Plan

- Keine inhaltliche Abweichung. Einzige technische Korrektur gegenüber dem ersten Entwurf: der
  Erklärkommentar in `tests/AiNetLinter.TestProject.props` enthielt ursprünglich eine
  Doppel-Bindestrich-Sequenz (`--`) innerhalb eines XML-Kommentars, was laut XML-Spezifikation
  ungültig ist und den Import mit `MSB4024` scheitern ließ (Fehlermeldung wirkte zunächst wie ein
  generisches „TargetFramework leer"-Problem). Kommentar auf Semikolon umformuliert, danach baute
  die Solution grün.

## Beobachtungen

- Keine über den Scope hinausgehenden Auffälligkeiten.

## Bekannte Unschärfen

- `ProjectOverrides`-Schlüssel `"*Tests"` matcht laut `ProjectConfigResolver.IsMatch`
  (`^.*Tests$`) potenziell auch künftige Projekte, deren Name zufällig auf „Tests" endet, auch
  wenn sie inhaltlich keine der drei geplanten Zielprojekte sind — das ist dieselbe Wildcard-Logik
  wie vorher bei `"*.Tests"`, nur mit einem Zeichen weniger Einschränkung; im aktuellen Bestand
  (5 Solution-Projekte) gibt es keine Fehlmatches, aber der Kritiker sollte das bei künftigen
  Projekt-Neuanlagen im Hinterkopf behalten.
- Der neue Integrationstest lädt die komplette `AiNetLinter.slnx` per `SourceFileCatalog.LoadAsync`
  (~10-13s Laufzeit für 3 Theory-Fälle) — bewusst in Kauf genommen laut Plan, da nur ein einziger
  Integrationstest in diesem Step entsteht; relevant für die künftige Baseline-Messung.
