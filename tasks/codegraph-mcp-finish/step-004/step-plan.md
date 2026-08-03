---
status: done (pending audit)
type: step-plan
task: codegraph-mcp-finish
step: 004
title: "Testsuite-Performance — Test-Data-Builder/Object-Mother konsolidieren, Kern-Testinfrastruktur (F.4, Teilscope)"
epic: EPIC-01
estimated_risk: medium
step_type: single
items: []
created_by: planer
created_by_model: claude-sonnet-5
created_by_model_knowledge_cutoff: 2026-01
created_at: 2026-08-03
related_to: ["step-003"]
---

# Step 004: Test-Data-Builder/Object-Mother konsolidieren — Kern-Testinfrastruktur (F.4, Teilscope)

## Bezug

- **Task:** `codegraph-mcp-finish`
- **Epic:** `EPIC-01` aus `roadmap.md` — Testsuite-Performance (Block F).
  F.1–F.3 sind approved. F.4 ist der nächste offene Teilpunkt, F.5–F.6
  bleiben für spätere Steps offen.
- **Konzept-Referenz:** `Konzept.md` Muss-Haben F, Punkt 4: „Test-Data-Builder/
  Object-Mother für `Config`/`GlobalConfig`/`CheckerContext` statt
  ad-hoc-Konstruktion pro Test — reduziert Boilerplate, kein Laufzeit-Hebel."
  Non-Goals: „Keine Änderung an Testinhalten/Assertions", „Keine neue
  Testabdeckung durch Block F" (reines Boilerplate-/Organisations-Refactoring).

## Aktueller Projektzustand (JIT-Kontext)

- **`src/AiNetLinter.Tests/TestHelper.cs` existiert bereits** und ist
  faktisch schon das Object-Mother für `CheckerContext`
  (`TestHelper.CreateContext(...)`) und für einen Default-`Config`
  (`TestHelper.CreateDefaultConfig()` → `new Config { Global = new
  GlobalConfig(), Metrics = new MetricsConfig() }`). `CreateContext` wird
  bereits in 15 Dateien / 96 Aufrufen genutzt (u. a. der komplette
  `Core/Checkers/`-Cluster aus step-003) — F.4 ist also **kein Neubau**,
  sondern eine **Lückenschließung**: einen bereits etablierten,
  funktionierenden Mechanismus konsequent weiterverwenden, statt ihn neu
  zu erfinden.
- **Tatsächliche Lücke, per Grep verifiziert (nicht nur laut `Konzept.md`
  vermutet):** 42 Testdateien konstruieren `Config` weiterhin roh
  (`new Config { Global = new GlobalConfig{...}, Metrics = new
  MetricsConfig{...} }` bzw. target-typed `Config X(...) => new() {...}`),
  statt auf `TestHelper.CreateDefaultConfig()` aufzusetzen — jede Datei mit
  einer eigenen lokalen `CreateConfig`/`CreateDefaultConfig`/`ConfigWith`-
  Methode oder direkt inline im Testkörper. Wichtig für die Scope-Wahl
  unten: **keine dieser 42 Methoden ist ein reiner 1:1-Duplikat ohne
  Mehrwert** — jede parametrisiert echte, testspezifische Regel-Werte
  (z. B. `CreateConfig(int maxDeps, ...)` in
  `MaxConstructorDependenciesTests.cs`). Die eigentliche Duplikation ist
  ausschließlich das immer gleiche 2-Zeilen-Skelett
  `Global = new GlobalConfig(), Metrics = new MetricsConfig()`
  (bzw. dessen Startpunkt vor der eigentlichen Individualisierung) — nicht
  die Parametrisierung selbst, die bleibt sinnvoll und wird **nicht**
  angetastet.
- **Bewusste Scope-Grenze dieses Steps (Größenabschätzung nach
  Sichtung, nicht vorab angenommen):** Von den 42 betroffenen Dateien
  liegen **23 im bereits durch step-003 gerade erst neu geordneten
  `src/AiNetLinter.Tests/Core/Checkers/`-Cluster und in
  `src/AiNetLinter.Tests/FalsePositives/`** — dort ist die Konvention
  „eine lokale, parametrisierte `CreateConfig(...)`/`ConfigWith(...)`-
  Methode pro Testklasse" bereits absichtlich einheitlich (Auswahlkriterium
  1:1-Checker-Test, siehe step-003-Kontext) und die Migration aller 23 auf
  `TestHelper.CreateDefaultConfig() with {...}` in **derselben** Review-Runde
  wie die verbleibenden 19 wäre zu groß, um noch verlässlich in einer
  Kritiker-Runde geprüft zu werden (>90 Konstruktions-Stellen). Dieser Step
  deckt die **19 Dateien außerhalb dieser beiden Cluster** ab
  (Kern-Testinfrastruktur: `Core/` [Nicht-Checker], `Configuration/`,
  `Metrics/`-Infrastruktur, `Architecture/`, `Baseline/`) — das Epic bleibt
  für die restlichen 23 Dateien offen (siehe `roadmap.md`), kein
  eigenmächtiges Verkleinern des Konzept-Punkts, nur eine bewusste
  Stufung innerhalb von F.4.
- **Sicherheits-Beweis der Transformation (warum trotz 19 Dateien
  vertretbares Risiko):** `TestHelper.CreateDefaultConfig()` liefert exakt
  `new Config { Global = new GlobalConfig(), Metrics = new MetricsConfig()
  }`. Für einen beliebigen bestehenden Ausdruck
  `new Config { Global = X, Metrics = Y, ...Z }` ist
  `TestHelper.CreateDefaultConfig() with { Global = X, Metrics = Y, ...Z }`
  **immer** wertgleich (`record`-`with` setzt exakt die genannten
  Properties, alle anderen bleiben auf ihrem Default — identisch zum
  Ausgangszustand, da `Config` außer `Global`/`Metrics` ausschließlich
  optionale Properties mit Default-Werten hat, siehe
  `src/AiNetLinter/Configuration/Config.cs`). Die Transformation ist damit
  rein syntaktisch, nicht verhaltensändernd — das senkt das Risiko
  gegenüber einer inhaltlichen Änderung erheblich, ähnlich der
  Verschiebungs-Sicherheit in step-003.
- **Beiläufige Beobachtung (kein Fix in diesem Step, nur Hinweis):**
  Vier der 19 Dateien (`ArchitectureTests.cs`, `LinterAnalyzerTests.cs`,
  `LinterEngineCacheTests.cs`, `LinterEngineTests.cs`) definieren eine
  lokale Methode namens `CreateDefaultConfig()` — **identischer Name**,
  aber **andere Rückgabewerte** als `TestHelper.CreateDefaultConfig()`.
  Das ist verwirrend (Namenskollision unterschiedlicher Bedeutung), aber
  kein funktionaler Fehler (private Methode, kein Shadowing-Compilerfehler
  in C#). Nicht Teil dieses Steps (Umbenennung wäre eine über die reine
  Skelett-Konsolidierung hinausgehende Änderung) — falls die Kritiker-Review
  das als eigenständigen Fund sieht, ggf. als Tech-Debt vermerken.

## Intention

Nach diesem Step greifen die 19 Kern-Testinfrastruktur-Dateien für ihre
`Config`-Konstruktion auf das bestehende `TestHelper.CreateDefaultConfig()`
zurück (`TestHelper.CreateDefaultConfig() with { Global = ..., Metrics =
..., ... }` statt `new Config { Global = ..., Metrics = ..., ... }`), ohne
dass sich ein einziger Rückgabewert, Testname oder Assertion ändert —
reine Konsolidierung auf einen gemeinsamen Ausgangspunkt, kein neues
Builder-Framework (die vorhandene `record`+`with`-Syntax von C# **ist**
bereits der idiomatische, leichtgewichtige Objekt-Mother-Mechanismus für
diesen Fall — eine zusätzliche Fluent-Builder-Klasse wäre eine unnötige
zweite Abstraktion neben C#s eingebautem Sprachfeature und wird bewusst
nicht gebaut).

## Konkrete Änderungen

### Muster (gilt für jede der 19 Dateien unten)

Jede lokale Config-Konstruktion (egal ob als lokale Methode
`CreateConfig(...)`/`CreateDefaultConfig()`/`ConfigWith(...)`/`MakeConfig(...)`
oder inline im Testkörper) wird von

```csharp
new Config
{
    Global = new GlobalConfig { /* ... */ },
    Metrics = new MetricsConfig { /* ... */ },
    // ggf. weitere Properties (SolutionBasePath, PathOverrides, TestSentinel, ...)
}
```

zu

```csharp
TestHelper.CreateDefaultConfig() with
{
    Global = new GlobalConfig { /* ... unverändert übernommen ... */ },
    Metrics = new MetricsConfig { /* ... unverändert übernommen ... */ },
    // ggf. weitere Properties unverändert übernommen
}
```

umgeschrieben. **Regeln dabei:**

- Methodensignaturen (Name, Parameter, Zugriffsmodifikator) bleiben
  **exakt** unverändert — nur der Methodenkörper/die inline-Konstruktion
  wird umgeschrieben. Kein Call-Site außerhalb der jeweiligen Datei ist
  betroffen.
- Ist `Global`/`Metrics` im Original bereits der reine Default
  (`new GlobalConfig()`/`new MetricsConfig()`, keine Property gesetzt),
  wird das jeweilige `with`-Member weggelassen (nicht `Global = new
  GlobalConfig()` redundant wiederholen) — z. B. wird aus
  `new Config { Global = new GlobalConfig(), Metrics = new MetricsConfig
  { X = 1 } }` schlicht `TestHelper.CreateDefaultConfig() with { Metrics
  = new MetricsConfig { X = 1 } }`.
- Sind **beide** (`Global` und `Metrics`) reiner Default und keine
  weiteren Properties gesetzt: direkt `TestHelper.CreateDefaultConfig()`
  ohne `with`-Block verwenden.
- Alle sonstigen Config-Properties (`SolutionBasePath`, `PathOverrides`,
  `ProjectOverrides`, `TestSentinel`, `FileFilters`, ...) werden 1:1 mit
  übernommen, nur an den `with`-Block gehängt statt an die
  Objekt-Initialisierer-Syntax.

### Betroffene Dateien (19, Kern-Testinfrastruktur)

| Datei | Lokale(r) Konstruktions-Punkt(e) |
|---|---|
| `src/AiNetLinter.Tests/Architecture/ArchitectureTests.cs` | `CreateDefaultConfig()` |
| `src/AiNetLinter.Tests/Baseline/SourceFileCatalogTests.cs` | 1 inline `new Config {...}` (inkl. `TestSentinel`-Override) |
| `src/AiNetLinter.Tests/Configuration/AgentFeaturesTests.cs` | `CreateConfig(Func<GlobalConfig,GlobalConfig>? configureGlobal = null)` + 1 inline |
| `src/AiNetLinter.Tests/Configuration/ConfigSyncerTests.cs` | `DefaultConfig()` (1:1-Duplikat von `TestHelper.CreateDefaultConfig()`) + 2 weitere inline `new Config {...}` |
| `src/AiNetLinter.Tests/Configuration/DeveloperExperienceTests.cs` | 5 inline `new Config {...}` (kein lokaler Wrapper) |
| `src/AiNetLinter.Tests/Configuration/FileFilterEvaluatorTests.cs` | `CreateTestConfig(FileFiltersConfig filters)` |
| `src/AiNetLinter.Tests/Configuration/PathOverridesTests.cs` | `CreateBaseConfig(int maxMethodLines = 42)` (target-typed `=> new()`) + 3 inline `new Config {...}` in den `ResolveForFile_*`-Tests |
| `src/AiNetLinter.Tests/Configuration/ConfigNormalizerTests.cs` | `CreateBaseConfig()` |
| `src/AiNetLinter.Tests/Core/ControlFlowResilienceTests.cs` | `CreateConfig(bool enabled)` + `CreateSilentCatchConfig(bool enabled, bool allowCancellationCatch)` |
| `src/AiNetLinter.Tests/Core/LinterAnalyzerTests.cs` | `CreateDefaultConfig()` (Namenskollision mit `TestHelper`, siehe Beobachtung oben) |
| `src/AiNetLinter.Tests/Core/LinterEngineCacheTests.cs` | `CreateDefaultConfig()` (dito) |
| `src/AiNetLinter.Tests/Core/LinterEngineTests.cs` | `CreateDefaultConfig()` (dito) |
| `src/AiNetLinter.Tests/Core/NullCoalescingInitializerClassifierTests.cs` | `CreateConfig(...)` |
| `src/AiNetLinter.Tests/Core/PlaybookGeneratorRound2Tests.cs` | 3 inline `new Config {...}` (kein lokaler Wrapper; 2 Aufrufe von `TestHelper.CreateContext` an anderer Stelle bleiben unverändert) |
| `src/AiNetLinter.Tests/Core/ResultPatternNamespaceTests.cs` | `CreateConfig(...)` |
| `src/AiNetLinter.Tests/Core/RuleRegistryTests.cs` | 1 inline `new Config {...}` (`fakeConfig`) |
| `src/AiNetLinter.Tests/Core/ScopeImmutabilityTests.cs` | `CreateConfig(int maxOverloads = 2)` + `CreateImmutabilityTestConfig(bool allowPrivateBackingFields = false, string[]? exemptBaseTypes = null)` |
| `src/AiNetLinter.Tests/Core/StaticTestSentinelExemptionTests.cs` | `CreateSentinelConfig(TestSentinelConfig? sentinel = null)` + 1 weiteres inline `new Config {...}` |
| `src/AiNetLinter.Tests/Metrics/MethodLineCounterTests.cs` | 1 inline `new Config {...}` |
| `src/AiNetLinter.Tests/Metrics/PostAnalysisChecksPathOverrideTests.cs` | `MakeConfig(int globalLimit, int pathOverrideLimit)` (target-typed `=> new()`) |

**Explizit NICHT Teil dieses Steps** (siehe „Aktueller Projektzustand" —
bewusste Scope-Grenze, bleibt in `roadmap.md`/EPIC-01 offen für einen
möglichen Folge-Step):

- Die 20 Dateien in `src/AiNetLinter.Tests/Core/Checkers/`, die eine
  eigene parametrisierte `CreateConfig(...)`/`ConfigWith(...)`-Methode
  besitzen (u. a. `MaxConstructorDependenciesTests.cs`,
  `MaxInheritanceDepthTests.cs`, `MaxSwitchArmsTests.cs`,
  `NamespaceDirectoryMappingTests.cs`, `WpfCodeBehindTests.cs`,
  `SwitchDispatcherDetectorTests.cs`, `CouplingSemanticTests.cs`,
  `BlockingTaskCheckerTests.cs`, `AsyncVoidCheckerTests.cs`,
  `LinqChainLengthCheckerTests.cs`, `MaxBoolParameterCountTests.cs`,
  `MaxPartialClassFilesTests.cs`, `MethodParameterCountOverrideTests.cs`,
  `MaxPublicMembersPerTypeTests.cs`,
  `MethodParameterCountIgnoreTypePrefixesTests.cs`,
  `MethodParameterCountAccessibilityTests.cs`, `NestedTypesCheckerTests.cs`,
  `SilentCatchAllowedTypesTests.cs`, sowie `MaxDirectoryChildrenTests.cs`
  und `FileLimitGuidanceTests.cs` in `Metrics/`).
- Die 2 Dateien in `src/AiNetLinter.Tests/FalsePositives/`
  (`FalsePositiveExtensionsTests.cs`, `FalsePositiveTests.cs`).

## Tests

- [ ] `dotnet build AiNetLinter.slnx` — grün, 0 Warnungen.
- [ ] `dotnet test --filter Category=Unit` — grün, **exakt gleiche
      Testanzahl** wie vor dem Step (reine Konstruktions-Umstellung, keine
      Assertion/kein Testname geändert).
- [ ] `dotnet test AiNetLinter.slnx --no-build` (Volllauf) — grün, gleiche
      Testanzahl wie vor dem Step.
- [ ] Grep-Sweep nach dem Step: `new Config\b` und das Muster
      `Config \w+\([^)]*\)\s*=>\s*new\(\)` dürfen in den 19 oben gelisteten
      Dateien **keine** Treffer mehr liefern (außer innerhalb von
      `TestHelper.cs` selbst, das unverändert bleibt) — Nachweis der
      Vollständigkeit dieses Steps innerhalb seines eigenen Scopes.
- [ ] Vor jedem Build/Test: offene `AiNetLinter.exe`/`testhost.exe`-Prozesse
      prüfen und ggf. beenden (Tech-Stack-Notiz, bekannte
      Datei-Sperren-Falle).

## Definition of Done

- [ ] Alle 19 gelisteten Dateien auf `TestHelper.CreateDefaultConfig()
      with {...}` (bzw. direkten Aufruf ohne `with`, falls kein
      Custom-Wert) umgestellt, Methodensignaturen/Testkörper/Assertions
      unverändert.
- [ ] Grep-Sweep (siehe Tests) liefert für diese 19 Dateien keine
      verbleibenden `new Config`/target-typed-`Config`-Treffer.
- [ ] Build-Command aus Tech-Stack-Notiz grün, 0 Warnungen.
- [ ] Test-Command aus Tech-Stack-Notiz grün (Unit + Volllauf), Testanzahl
      identisch zu vorher.
- [ ] Commit auf aktuellem Branch (Conventional Commit, Suffix
      `[codegraph-mcp-finish]`).
- [ ] `step-004/step-result.md` geschrieben.
- [ ] `status` in `step-plan.md` von `in_progress` auf
      `done (pending audit)` gesetzt.

## Rules-Refs

- `.agents/rules/AiNetLinterRichtlinien.mdc` §4 „Testsuite-Parallelität
  bewahren"/Build-Test-Pflichten (Zero-Warning) — reine
  Refactoring-Konsolidierung darf keine neuen Warnungen/keine
  Parallelitäts-Regressionen einführen; §5 Kommentar-Konventionen (keine
  Task-/Planungsartefakt-Referenzen wie `step-004`/`F.4` im Code selbst,
  falls beim Umschreiben ein Kommentar berührt wird).
- `.agents/rules/AiNetLinter.mdc` — keine der 19 Dateien überschreitet
  durch die Umstellung `AIContextFootprint`/`MaxLineCount` (die Änderung
  ist pro Datei nur wenige Zeilen kürzer, nie länger — `with` ist knapper
  als ein vollständiger Objekt-Initialisierer).

## Bekannte Ausnahmen

- Keine bekannten flaky Tests in diesem Step-Scope.

## Notes

- **Bestehende Struktur wiederverwenden, kein neues Framework** (Kern des
  JIT-Ansatzes, siehe „Aktueller Projektzustand"): `TestHelper` existiert
  bereits und wird von 15 Dateien für `CreateContext` genutzt — dieser
  Step erweitert seine Nutzung auf `CreateDefaultConfig()` in den 19
  Dateien, die es bisher nicht taten. Es entsteht **keine** neue
  Builder-Klasse, kein zweiter, konkurrierender Objekt-Mother-Mechanismus.
- **F.4 bleibt nach diesem Step teilweise offen** (23 Dateien in
  `Core/Checkers/`+`FalsePositives/`, siehe „Explizit NICHT Teil dieses
  Steps") — das Epic `EPIC-01` bleibt entsprechend in `roadmap.md` offen,
  kein vollständiges Abhaken von F.4. Ein Folge-Step-Modus-Aufruf
  entscheidet, ob der Rest als eigener Step folgt oder (falls der
  Nutzer/Kritiker den Rest-Nutzen als zu gering gegenüber dem
  Review-Aufwand einschätzt) bewusst als Tech-Debt-Eintrag zurückgestellt
  wird — das ist zu diesem Zeitpunkt noch nicht entschieden.
- **Namenskollision `CreateDefaultConfig()`** (siehe „Aktueller
  Projektzustand", 4 Dateien) bewusst nicht umbenannt — Umbenennung wäre
  eine über reine Konsolidierung hinausgehende Änderung und nicht nötig,
  damit dieser Step funktional korrekt ist (private Methoden, kein
  Namenskonflikt zur Compile-Zeit).
- `PathOverridesTests.cs`, `PostAnalysisChecksPathOverrideTests.cs`,
  `SourceFileCatalogTests.cs` setzen zusätzlich `SolutionBasePath`/
  `PathOverrides`/`TestSentinel` — diese Properties existieren direkt auf
  `Config` (nicht auf `Global`/`Metrics`) und werden genauso unverändert
  in den `with`-Block übernommen wie `Global`/`Metrics`.
