---
status: done
type: step-review
task: codegraph-mcp-finish
step: 004
epic: EPIC-01
step_type: single
reviewed_by: kritiker
reviewed_by_model: claude-sonnet-5
reviewed_by_model_knowledge_cutoff: 2026-01
reviewed_at: 2026-08-03
verdict: approved
tech_debt_ids: []
---

# Review Step 004: Test-Data-Builder/Object-Mother konsolidieren — Kern-Testinfrastruktur (F.4, Teilscope)

## Verdict

- [x] **approved** — alle vier Prüfebenen ok
- [ ] **issues**
- [ ] **blocked**

## Geprüft

- [x] Plan-Erfüllung: alle im `step-plan.md` genannten Änderungen erfolgt
- [x] Rules-Konformität: `<rules_dir>/**` (referenzierte Dateien) eingehalten
- [x] Logische Korrektheit: Code macht was er soll, nicht nur „grün"
- [x] Konzept-Treue: passt die Umsetzung zu `konzept.md` (Scope, Non-Goals, Muss-Haben)
- [x] Build: selbst nachgeprüft, grün
- [x] Tests: selbst nachgeprüft, grün

## Befund

Alle vier Ebenen unauffällig: alle 19 geplanten Dateien wurden korrekt auf `TestHelper.CreateDefaultConfig() with {...}` umgestellt (Stichproben-Diff-Prüfung bestätigt reine `record`-`with`-Semantik, keine Assertion/kein Testname berührt), der bewusste Teilscope (23 Dateien in `Core/Checkers/`+`FalsePositives/` unangetastet) ist per Diff verifiziert und im Plan/Result klar für den nächsten Aufruf dokumentiert, Rules §4/§5 sind eingehalten, und Build/Test (0 Warnungen, 1186/1186 grün) wurden selbst reproduziert.

### Plan-Erfüllung

Alle 19 im Plan gelisteten Dateien wurden umgestellt — per Commit-Diff (`git show 26fd08fa2...`) verifiziert: genau die 19 Dateien sind im Diff enthalten (Dateiliste im Diff-Stat deckt sich 1:1 mit der Plan-Tabelle). Grep-Sweep nach dem Step (selbst wiederholt) bestätigt für alle 19 Dateien 0 Treffer für `new Config\b` und das Muster `Config \w+\([^)]*\)\s*=>\s*new\(\)`. Methodensignaturen unverändert (Stichprobe: `CreateBaseConfig(int maxMethodLines = 42)`, `CreateConfig(int maxOverloads = 2)`, `CreateImmutabilityTestConfig(...)` — Name/Parameter/Modifikator jeweils identisch zu vorher, nur Körper umgeschrieben). Der explizit ausgenommene 23-Dateien-Cluster (`Core/Checkers/` + `FalsePositives/`) hat laut Diff keine einzige Änderung erfahren (`git diff --stat` für den Commit gegen diese Pfade liefert leer) — Teilscope sauber eingehalten.

**Stichproben-Verifikation der `with`-Transformation (behavior-preserving):**
- `ConfigSyncerTests.cs`: `DefaultConfig() => new() { Global = new GlobalConfig(), Metrics = new MetricsConfig() }` → `DefaultConfig() => TestHelper.CreateDefaultConfig()`. Da `TestHelper.CreateDefaultConfig()` laut Plan exakt diesen Wert liefert, ist das wertgleich. Bei `SyncerC_MetricsConfigOverride_...` wurde korrekt `Global = new GlobalConfig()` weggelassen (reiner Default) und nur `Metrics` im `with`-Block gesetzt — exakt nach der im Plan vorgegebenen Regel.
- `RuleRegistryTests.cs`: `fakeConfig` (beide Member Default) → `TestHelper.CreateDefaultConfig()` ohne `with`-Block — korrekt nach Plan-Regel „beide Default → kein with".
- `PathOverridesTests.cs`, `ScopeImmutabilityTests.cs`: individualisierte `Global`/`Metrics`-Werte bleiben 1:1 im `with`-Block erhalten, nur der Konstruktionsanker wechselt von `new Config` zu `TestHelper.CreateDefaultConfig() with`. Semantisch identisch, da `Config` (`src/AiNetLinter/Configuration/Config.cs`) außer `Global`/`Metrics` (beide `required`, von `TestHelper.CreateDefaultConfig()` gesetzt) ausschließlich optionale Properties mit Default-Initialisierern hat (`TestSentinel`, `UiSeparation`, `FileFilters`, `Web`, `RuleMetadata`, `ForbiddenNamespaceDependencies`, `ProjectOverrides`, `PathOverrides`, `SolutionBasePath`) — `with` überschreibt nur explizit genannte Properties, der Rest bleibt beim (identischen) Default. Die im Plan behauptete Wertgleichheit ist damit am Code bestätigt, nicht nur behauptet.

Zählabweichung (`ConfigSyncerTests.cs`: 3 statt 2 inline-Konstrukte; `DeveloperExperienceTests.cs`: 6 statt 5) bleibt innerhalb der 19-Dateien-Liste, keine Scope-Änderung — per Diff nachvollzogen (`DeveloperExperienceTests.cs`-Diff zeigt 6 Hunks mit `new Config` → `TestHelper.CreateDefaultConfig() with`). Vertretbare Plan-Ungenauigkeit, kein Finding.

### Rules-Konformität

- `AiNetLinterRichtlinien.mdc` §4 (Testsuite-Parallelität): keine der 19 Dateien führt eine neue Collection-Serialisierung ein — reine Konstruktionsumstellung, keine Test-Attribute berührt. Eingehalten.
- §4/§5 (Zero-Warning, Kommentar-Konventionen): Build lokal reproduziert, 0 Warnungen. Grep nach `step-004`/`F.4` in den 19 Dateien liefert 0 Treffer — keine Planungsartefakt-Referenzen im Code. Eingehalten.
- `AiNetLinter.mdc` (AIContextFootprint/MaxLineCount): Diff-Stat zeigt für jede Datei eine Nettoverkürzung oder Gleichstand (z. B. `ConfigSyncerTests.cs`: 15 Zeilen -, `DeveloperExperienceTests.cs`: 15 Zeilen -), nie eine Verlängerung. Eingehalten.

### Logische Korrektheit

`TestHelper.CreateDefaultConfig()` selbst (`src/AiNetLinter.Tests/TestHelper.cs`) blieb im Commit unverändert (nicht im Diff enthalten) und liefert weiterhin `new Config { Global = new GlobalConfig(), Metrics = new MetricsConfig() }` — der Umbau der 19 Dateien baut also korrekt auf einem stabilen, unveränderten Fundament auf. Die Transformation ist rein syntaktisch (siehe Stichproben oben); kein Fall gefunden, in dem eine individualisierte Property versehentlich verloren ging oder ein `with`-Block fälschlich weggelassen wurde, wo er nötig gewesen wäre.

### Konzept-Treue (Ebene 4)

Deckt sich mit Konzept.md Muss-Haben F.4 (Test-Data-Builder/Object-Mother für `Config`/`GlobalConfig`/`CheckerContext`) — hier wird der bereits bestehende `TestHelper`-Mechanismus konsequent weiterverwendet statt neu erfunden, wie im Plan begründet. Non-Goals eingehalten: keine Testinhalte/Assertions geändert (verifiziert per Diff-Stichproben), keine neue Testabdeckung (reine Konstruktions-Umstellung). Der Teilscope (19 von 42 Dateien) ist im Step-Plan („Aktueller Projektzustand", „Explizit NICHT Teil dieses Steps") und im Step-Result klar benannt, inkl. expliziter Liste der 23 verbleibenden Dateien und der Begründung (Review-Aufwand pro Runde). `roadmap.md`/EPIC-01 bleibt laut Step-Plan/Notes bewusst offen für F.4 — der nächste Planer-Aufruf hat damit alles Nötige, um zu entscheiden, ob ein Folge-Step für die restlichen 23 Dateien folgt. Kein Scope-Creep, kein Non-Goal umgesetzt, kein Muss-Haben-Punkt fehlt für den beanspruchten Teilscope.

### Build-/Test-Status

```
dotnet build AiNetLinter.slnx           → grün, 0 Warnungen (selbst reproduziert)
dotnet test AiNetLinter.slnx --no-build → grün (1186 Tests, 0 Fehler, selbst reproduziert)
```

## Sonstige Beobachtungen / MINOR / NITPICK

Die Namenskollision `CreateDefaultConfig()` in 4 Dateien (`ArchitectureTests.cs`, `LinterAnalyzerTests.cs`, `LinterEngineCacheTests.cs`, `LinterEngineTests.cs` — gleicher Name, andere Rückgabewerte als `TestHelper.CreateDefaultConfig()`) wurde bereits im Step-Plan explizit als bewusst nicht behoben vermerkt und die Frage an den Kritiker delegiert, ob ein eigenständiger Tech-Debt-Eintrag sinnvoll ist. Bewertung: rein kosmetisch (private Methoden, kein Compile-Konflikt, kein Verhaltensrisiko) — Priorität wäre `niedrig` und der Mehrwert eines eigenen `tech-debt.md`-Eintrags gegenüber dem bereits vorhandenen, expliziten Vermerk im Step-Plan/-Result ist gering. Kein neuer Tech-Debt-Eintrag angelegt, da die Beobachtung inhaltlich bereits vollständig im Step-Plan (Abschnitt „Aktueller Projektzustand") dokumentiert ist und bei nächster Berührung dieser 4 Dateien ohnehin sichtbar wird.
