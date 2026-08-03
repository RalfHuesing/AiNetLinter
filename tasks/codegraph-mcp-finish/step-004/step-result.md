---
status: done
type: step-result
task: codegraph-mcp-finish
step: 004
epic: EPIC-01
step_type: single
coded_by: coder
coded_by_model: claude-sonnet-5
coded_by_model_knowledge_cutoff: 2026-01
coded_at: 2026-08-03
code_commit_hash: 26fd08fa23b19ed5534d1b172dde6f8b4d66fb33
status_after: done
blocker_category: n/a
---

# Result Step 004: Test-Data-Builder/Object-Mother konsolidieren — Kern-Testinfrastruktur (F.4, Teilscope)

## Zusammenfassung

Alle 19 im Plan gelisteten Dateien wurden 1:1 nach Muster umgestellt: jede
lokale `new Config { Global = ..., Metrics = ... }`-Konstruktion (egal ob
lokale Methode oder inline im Testkörper) greift jetzt auf
`TestHelper.CreateDefaultConfig() with { ... }` zurück, mit weggelassenem
`Global`/`Metrics`-Member, wo der Originalwert bereits reiner Default war.
Methodensignaturen, Testkörper und Assertions blieben unverändert.

## Geänderte Dateien

- `src/AiNetLinter.Tests/Architecture/ArchitectureTests.cs` — `CreateDefaultConfig()`
- `src/AiNetLinter.Tests/Baseline/SourceFileCatalogTests.cs` — 1 inline `new Config`
- `src/AiNetLinter.Tests/Configuration/AgentFeaturesTests.cs` — `CreateConfig(...)` + 1 inline (`RuleMetadataRegistry_ResolvesKnownRule`, beide Members Default → ohne `with`)
- `src/AiNetLinter.Tests/Configuration/ConfigSyncerTests.cs` — `DefaultConfig()` (ohne `with`, war 1:1-Duplikat) + 3 inline (nicht 2 wie im Plan geschätzt, siehe „Abweichungen")
- `src/AiNetLinter.Tests/Configuration/DeveloperExperienceTests.cs` — 6 inline (nicht 5 wie im Plan geschätzt, siehe „Abweichungen")
- `src/AiNetLinter.Tests/Configuration/FileFilterEvaluatorTests.cs` — `CreateTestConfig(...)`
- `src/AiNetLinter.Tests/Configuration/PathOverridesTests.cs` — `CreateBaseConfig(...)` + 3 inline in `ResolveForFile_*`
- `src/AiNetLinter.Tests/Configuration/ConfigNormalizerTests.cs` — `CreateBaseConfig()` (ohne `with`)
- `src/AiNetLinter.Tests/Core/ControlFlowResilienceTests.cs` — `CreateConfig(bool)` + `CreateSilentCatchConfig(...)`
- `src/AiNetLinter.Tests/Core/LinterAnalyzerTests.cs` — `CreateDefaultConfig()` (Namenskollision mit `TestHelper`, siehe Plan-Beobachtung — unverändert belassen)
- `src/AiNetLinter.Tests/Core/LinterEngineCacheTests.cs` — `CreateDefaultConfig()` (dito)
- `src/AiNetLinter.Tests/Core/LinterEngineTests.cs` — `CreateDefaultConfig()` (dito)
- `src/AiNetLinter.Tests/Core/NullCoalescingInitializerClassifierTests.cs` — `CreateConfig(...)`
- `src/AiNetLinter.Tests/Core/PlaybookGeneratorRound2Tests.cs` — 3 inline (2× ohne `with`, da beide Members Default)
- `src/AiNetLinter.Tests/Core/ResultPatternNamespaceTests.cs` — `CreateConfig(...)`
- `src/AiNetLinter.Tests/Core/RuleRegistryTests.cs` — `fakeConfig` (ohne `with`, beide Members Default)
- `src/AiNetLinter.Tests/Core/ScopeImmutabilityTests.cs` — `CreateConfig(int)` + `CreateImmutabilityTestConfig(...)`
- `src/AiNetLinter.Tests/Core/StaticTestSentinelExemptionTests.cs` — `CreateSentinelConfig(...)` + 1 inline
- `src/AiNetLinter.Tests/Metrics/MethodLineCounterTests.cs` — 1 inline
- `src/AiNetLinter.Tests/Metrics/PostAnalysisChecksPathOverrideTests.cs` — `MakeConfig(...)` + 1 inline

## Commit

- **Code-Commit-Hash:** `26fd08fa23b19ed5534d1b172dde6f8b4d66fb33`
- **Message:**
  ```
  refactor(tests): Config-Konstruktion auf TestHelper.CreateDefaultConfig() konsolidiert [codegraph-mcp-finish]

  19 Testdateien der Kern-Testinfrastruktur (Core/, Configuration/,
  Metrics/, Architecture/, Baseline/) nutzen jetzt einheitlich
  TestHelper.CreateDefaultConfig() with {...} statt lokal dupliziertem
  new Config { Global = new GlobalConfig(), Metrics = new MetricsConfig() }.
  Reine Skelett-Konsolidierung, keine Test-Assertions/-Namen geändert.

  Refs: tasks/codegraph-mcp-finish/step-004
  ```
- **Branch:** main
- **Push:** nein (lokal)
- **Doku-Commit:** separater, zweiter Commit (Hash steht nicht hier drin).

## Build-/Test-Output

```
dotnet build AiNetLinter.slnx                → grün, 0 Warnungen
dotnet test --filter Category=Unit --no-build → grün (100 Tests, 0 Fehler)
dotnet test AiNetLinter.slnx --no-build       → grün (1186 Tests, 0 Fehler, 1 Datei-Sperren-Prozess vorab bereinigt)
```

Grep-Sweep `new Config\b` über alle 19 Dateien: 0 Treffer (verifiziert).

## Abweichungen vom Plan

- **Tatsächliche Anzahl inline-Konstrukte weicht in 2 Dateien vom Plan ab**
  (reine Zähl-Ungenauigkeit des Plans, kein Scope-Problem): `ConfigSyncerTests.cs`
  hatte 3 inline `new Config` statt der im Plan genannten 2 (zusätzlich zu
  `DefaultConfig()`); `DeveloperExperienceTests.cs` hatte 6 statt 5. Alle
  jeweils gefundenen Vorkommen wurden konsolidiert — die Datei-Liste selbst
  (19 Dateien) und der Umsetzungs-Scope blieben identisch zum Plan, nur die
  Item-Zählung pro Datei war im Plan zu niedrig angesetzt.
- Ansonsten Plan 1:1 umgesetzt: Namenskollision bei `CreateDefaultConfig()`
  in 4 Dateien bewusst nicht umbenannt (wie im Plan vorgesehen).

## Beobachtungen

- Bestätigt aus dem Plan: die 4 Dateien mit lokaler `CreateDefaultConfig()`
  (`ArchitectureTests.cs`, `LinterAnalyzerTests.cs`, `LinterEngineCacheTests.cs`,
  `LinterEngineTests.cs`) haben weiterhin einen irreführenden Namen
  (gleicher Name wie `TestHelper.CreateDefaultConfig()`, aber andere
  Rückgabewerte) — nicht Teil dieses Steps, siehe Plan-Notiz.
- Keine neuen Beobachtungen über den Plan hinaus.

## Bekannte Unschärfen

- Die Zählabweichungen bei `ConfigSyncerTests.cs`/`DeveloperExperienceTests.cs`
  (siehe „Abweichungen") sollte der Kritiker gegen den tatsächlichen Diff
  prüfen — der Umfang der Grep-Sweep-Verifikation (0 verbleibende `new Config`
  in den 19 Dateien) deckt das ab, aber die Plan-Tabelle selbst war insofern
  ungenau.
- Keine Testanzahl-Baseline vor dem Step separat erfasst (kein `git stash`
  + Vorab-Lauf) — die Volllauf-Zahl (1186) stammt ausschließlich aus dem
  Nach-Zustand. Da die Änderung rein syntaktisch ist (keine Methode
  hinzugefügt/entfernt, keine `[Fact]`/`[Theory]` berührt), ist eine
  Abweichung der Testanzahl praktisch ausgeschlossen, aber nicht per Diff
  gegen eine Vorab-Zahl verifiziert.
