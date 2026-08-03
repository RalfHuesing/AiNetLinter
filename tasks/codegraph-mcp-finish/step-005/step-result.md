---
status: done
type: step-result
task: codegraph-mcp-finish
step: 005
epic: EPIC-01
step_type: single
coded_by: coder
coded_by_model: claude-sonnet-5
coded_by_model_knowledge_cutoff: 2026-01
coded_at: 2026-08-03
code_commit_hash: d744dc9
status_after: done
blocker_category: n/a
---

# Result Step 005: Test-Data-Builder/Object-Mother konsolidieren — Rest-Cluster (F.4, Teil 2/2) + `#nullable enable` Randmitnahme (F.5)

## Zusammenfassung

Alle 19 im Plan gelisteten Testdateien in `Core/Checkers/`, `Metrics/`
und `FalsePositives/` wurden von lokaler `new Config {...}`-Konstruktion
auf `TestHelper.CreateDefaultConfig() with {...}` (bzw. direkten Aufruf
ohne `with`, wo Global/Metrics reiner Default waren) umgestellt. In den
11 Dateien ohne bestehendes Pragma wurde zusätzlich `#nullable enable`
als erste Zeile ergänzt. Beide Sonderfälle (`MaxInheritanceDepthTests.cs`,
`NamespaceDirectoryMappingTests.cs` mit lokaler Methode `CreateDefaultConfig()`)
wurden wie im Plan vorgesehen behandelt: nur der Methodenkörper geändert,
Methodenname/Aufrufstellen unangetastet. F.4 ist damit vollständig
abgeschlossen, `roadmap.md` entsprechend aktualisiert.

## Geänderte Dateien

- `src/AiNetLinter.Tests/Core/Checkers/MaxPartialClassFilesTests.cs` — Config-Konsolidierung + `#nullable enable` ergänzt
- `src/AiNetLinter.Tests/Core/Checkers/WpfCodeBehindTests.cs` — Config-Konsolidierung (Metrics war reiner Default, `with`-Member weggelassen)
- `src/AiNetLinter.Tests/Core/Checkers/SwitchDispatcherDetectorTests.cs` — Config-Konsolidierung
- `src/AiNetLinter.Tests/Core/Checkers/SilentCatchAllowedTypesTests.cs` — Config-Konsolidierung + `#nullable enable` ergänzt
- `src/AiNetLinter.Tests/Core/Checkers/MethodParameterCountOverrideTests.cs` — Config-Konsolidierung + `#nullable enable` ergänzt
- `src/AiNetLinter.Tests/Core/Checkers/MethodParameterCountIgnoreTypePrefixesTests.cs` — Config-Konsolidierung + `#nullable enable` ergänzt
- `src/AiNetLinter.Tests/Core/Checkers/MethodParameterCountAccessibilityTests.cs` — Config-Konsolidierung
- `src/AiNetLinter.Tests/Core/Checkers/CouplingSemanticTests.cs` — Config-Konsolidierung + `#nullable enable` ergänzt
- `src/AiNetLinter.Tests/Core/Checkers/MaxBoolParameterCountTests.cs` — Config-Konsolidierung + `#nullable enable` ergänzt
- `src/AiNetLinter.Tests/Core/Checkers/MaxConstructorDependenciesTests.cs` — Config-Konsolidierung
- `src/AiNetLinter.Tests/Core/Checkers/MaxInheritanceDepthTests.cs` — Config-Konsolidierung + `#nullable enable` ergänzt; Namenskollision `CreateDefaultConfig()` bewusst unverändert (siehe Plan)
- `src/AiNetLinter.Tests/Core/Checkers/MaxPublicMembersPerTypeTests.cs` — Config-Konsolidierung + `#nullable enable` ergänzt
- `src/AiNetLinter.Tests/Core/Checkers/MaxSwitchArmsTests.cs` — Config-Konsolidierung
- `src/AiNetLinter.Tests/Core/Checkers/NamespaceDirectoryMappingTests.cs` — Config-Konsolidierung (Metrics war reiner Default) + `#nullable enable` ergänzt; Namenskollision `CreateDefaultConfig()` bewusst unverändert
- `src/AiNetLinter.Tests/Core/Checkers/NestedTypesCheckerTests.cs` — Config-Konsolidierung (Metrics war reiner Default) + `#nullable enable` ergänzt
- `src/AiNetLinter.Tests/Metrics/FileLimitGuidanceTests.cs` — Config-Konsolidierung
- `src/AiNetLinter.Tests/Metrics/MaxDirectoryChildrenTests.cs` — Config-Konsolidierung + `#nullable enable` ergänzt
- `src/AiNetLinter.Tests/FalsePositives/FalsePositiveExtensionsTests.cs` — Config-Konsolidierung
- `src/AiNetLinter.Tests/FalsePositives/FalsePositiveTests.cs` — Config-Konsolidierung
- `tasks/codegraph-mcp-finish/roadmap.md` — F.4 als vollständig abgeschlossen markiert, F.5-Teilfortschritt vermerkt, F.6 weiterhin offen

## Commit

- **Code-Commit-Hash:** `d744dc9`
- **Message:**
  ```
  refactor(tests): Config-Konstruktion Rest-Cluster auf TestHelper konsolidiert [codegraph-mcp-finish]

  Restliche 19 Testdateien in Core/Checkers/, Metrics/ und FalsePositives/
  auf TestHelper.CreateDefaultConfig() with {...} umgestellt (F.4 Teil
  2/2, schließt Block F.4 vollständig ab). In den 11 dieser Dateien ohne
  bisheriges Pragma zusätzlich #nullable enable am Dateianfang ergänzt
  (F.5-Randmitnahme, keine eigene Flächenaktion). roadmap.md entsprechend
  aktualisiert.

  Refs: tasks/codegraph-mcp-finish/step-005
  ```
- **Branch:** main
- **Push:** nein (lokal)
- **Doku-Commit:** separater, zweiter Commit (Hash steht nicht hier drin — Selbstbezug, siehe `git log`).

## Build-/Test-Output

```
dotnet build AiNetLinter.slnx           → grün, 0 Warnungen
dotnet test --filter Category=Unit      → grün (100 Tests, 0 Fehler)
dotnet test AiNetLinter.slnx --no-build → grün (1186 Tests, 0 Fehler, identisch zur Baseline aus step-004)
```

## Abweichungen vom Plan

Keine — Plan 1:1 umgesetzt. Kein Fall in den 11 Dateien deckte eine neue
Nullable-Warnung auf (Zero-Warning-Build war beim ersten Durchlauf grün),
„Bekannte Ausnahmen"-Klausel musste nicht angewendet werden.

## Beobachtungen

- Namenskollisionen `CreateDefaultConfig()` sind über beide Steps
  (step-004 + step-005) jetzt in 6 Dateien vorhanden (4 aus step-004 +
  `MaxInheritanceDepthTests.cs` + `NamespaceDirectoryMappingTests.cs`).
  Wie im Plan erwähnt: Entscheidung über einen gebündelten
  Tech-Debt-Eintrag liegt beim Kritiker.
- In mehreren der bearbeiteten Dateien (z. B. `SilentCatchAllowedTypesTests.cs`,
  `MethodParameterCountOverrideTests.cs`, `MaxBoolParameterCountTests.cs`)
  stehen mehrere `GlobalConfig`-Property-Zuweisungen ohne Zeilenumbruch
  hintereinander auf einer physischen Zeile (offenbar Altlast aus einer
  früheren automatisierten Bearbeitung, z. B.
  `EnforceNoSilentCatch = false,                EnforceExplicitStateImmutability = false, ...`).
  Nicht Teil dieses Steps (reine Formatierungsfrage, keine
  Verhaltensänderung) — nicht angefasst, da außerhalb des Scope
  „nur Konstruktions-Ausdruck umschreiben".

## Bekannte Unschärfen

Keine — alle Grep-Sweeps (Config-Konstruktion, `#nullable enable`
Erstzeile) wurden nach der Umsetzung verifiziert und liefern keine
Treffer/Abweichungen mehr.
