---
status: open
type: step-plan
task: safeguard
step: 001
title: "SafeguardScanner mit deterministischer Score-Berechnung"
epic: EPIC-01
estimated_risk: medium
step_type: single
items: []
created_by: planer
created_by_model: MiniMax-M3
created_by_model_knowledge_cutoff: 2026-01
created_at: 2026-08-06T14:10:00+02:00
last_updated: 2026-08-06T14:10:00+02:00
related_to:
  - konzept.md#muss-haven-punkte-4-6-8
---

# Step 001: SafeguardScanner mit deterministischer Score-Berechnung

## Bezug

- **Task:** `safeguard`
- **Epic:** `EPIC-01` aus `roadmap.md` — SafeguardScanner (deterministische
  Score-Berechnung), aktuell offen
- **Konzept-Referenz:** `konzept.md` §"Muss-Haven" Punkte 4-6+8
  (deterministische Score-Berechnung, Score-Komponenten,
  Remediation-Generator, 10+ Unit-Tests) und §"Wie" Schritt 1

## Aktueller Projektzustand (JIT-Kontext)

Beim Lesen des Live-Repo-Codes vorgefunden — beeinflusst den Plan direkt:

- **`LinterEngine.RunAsync(Solution, noCache: true, cacheTtlMinutes: 0, ct)`**
  (`src/AiNetLinter/Core/LinterEngine.cs:64`) ist die zentrale, wiederverwendbare
  Eintrittspunkt-API für Lint-Analysen. Liefert
  `IReadOnlyCollection<RuleViolation>` mit bereits aufgelöster
  `EffectiveSeverity` und `RuleName`. Wird exakt so von `GetViolationsScanner`
  aufgerufen — keine eigene Lint-Loop im Scanner.
- **`RuleViolation`-Modell** (`src/AiNetLinter/Models/RuleViolation.cs`):
  `record` mit `FilePath`, `LineNumber`, `RuleName`, `Details`, `Guidance`,
  `EffectiveSeverity?` — kann direkt als Quelle für `ViolationEntry` dienen
  (Mapping 1:1, keine Neuerfindung).
- **`Config`-Record** (`src/AiNetLinter/Configuration/Config.cs`, aus
  `ILinterEngineConfig`): trägt u. a. `Metrics.MaxCyclomaticComplexity` und
  `Global.EnforceSealedClasses` als Schwellwerte. Konstanter Default
  `MinScoreDefault = 8.0` im Scanner — keine `rules.json`-Änderung in
  EPIC-01 (Konzept §"Muss-Haven" nennt rules.json-Default als optional,
  §"Wo im Projekt" listet rules.json explizit als "optional … falls nicht
  schon strukturell vorhanden"; aktuell nicht vorhanden → EPIC-01 lässt
  es bei der Konstante, EPIC-02/EPIC-03 können es nachziehen).
- **`GetViolationsScanner`-Pattern** (`src/AiNetLinter/Mcp/Tools/GetViolationsScanner.cs:33`):
  statische Klasse, `internal`, `#nullable enable` am Dateianfang,
  Parameter-Record (`GetViolationsScannerParameters`) für
  `MaxMethodParameterCount`-Compliance, dedizierter Result-Record
  (`GetViolationsResult`) mit `IsMalfunction`-Flag und `Context`,
  defensiver `try/catch` (kein leerer Catch, `ex is not
  OperationCanceledException`), Downcast `ILinterEngineConfig` → `Config`
  dokumentiert als nicht-spekulativ, LinterEngine-Konstruktor-Signatur
  `(config, rulesJsonContent: null, profiler: null, console, args: null)`.
  Diesen Pattern 1:1 übernehmen — keine parallele Struktur.
- **Test-Pattern** (`src/AiNetLinter.Tests/Mcp/Tools/GetViolationsToolTests.cs`):
  xUnit v3, `IClassFixture<SymbolGraphCatalogFixture>` für gesharete
  Mini-Solution, `Assert.IsType<TextContentBlock>` für Inhaltsprüfung,
  `ThrowingTextLoader`-Fake für deterministische Malfunction-Simulation,
  `FormatReport`-Tests greifen `internal`-Member direkt (gleiche Assembly
  via `InternalsVisibleTo`). Für die Safeguard-Tests: paralleler
  `SafeguardScannerTests`-Aufbau, gezielte Synthese von
  `RuleViolation`-Listen für die Komponenten-Tests (kein vollständiger
  Lint-Lauf nötig → schnell, deterministisch, kein Disk-IO).
- **`GetViolationsScannerTests.cs` existiert NICHT** (verifiziert) — die
  Scanner-Logik wird aktuell nur über den Tool-Wrapper indirekt getestet.
  Der `SafeguardScannerTests.cs` ist daher die erste dedizierte
  Scanner-Test-Datei im Projekt; etabliert ein neues Test-Pattern, das
  auch der zukünftige `GetViolationsScannerTests` als Vorbild nehmen
  könnte (out of scope hier, Erwähnung als Tech-Debt-Kandidat wenn
  Coder/Kritiker entsprechend beobachtet).
- **JSON-Schema-Bausteine:** Der bestehende `McpToolResults`-Helper
  liefert ausschließlich `TextContentBlock` (kein structured content).
  EPIC-01 beschränkt sich auf die C#-Record-Definitionen; die
  JSON-Schema-2020-12-Serialisierung und der `CallToolResult`-Aufbau
  mit structured content sind **EPIC-02** (Tool-Wrapper). Records
  werden mit klaren PascalCase-Properties + `required`-Markern
  designed, sodass sie sowohl als JSON-deserialisierbare POCOs
  (MCP-Input/Output) als auch als deterministische Score-Container
  funktionieren.
- **Footprint:** `AnalysisToolRegistrations.cs` hat aktuell
  `MaxAIContextFootprint = 2870` (PathOverride in `rules.json` Zeile 408).
  EPIC-01 fasst diese Datei **nicht** an (Tool-Registrierung ist EPIC-02)
  — die Beobachtung wandert als "Auffälligkeit für den Orchestrator" in
  Schritt 6 dieses Plans. Der neue `SafeguardScanner.cs` selbst folgt
  dem `GetViolationsScanner`-Pattern und braucht **keinen** PathOverride
  (Standard-Limit 2500 reicht, da identische Pull-in-Klasse
  `LinterEngine`).
- **`McpSufficiencyHints`/`McpTruncation`/`McpToolResults`:** nicht
  relevant in EPIC-01 (Scanner ist Tool-frei, liefert nur Records). Der
  Coder darf sie nicht antippen — Konzept §"Wo im Projekt" +
  "Nicht angefasst (bewusst)" listet sie explizit als tabu.

## Intention

Nach diesem Step existiert `SafeguardScanner.ComputeScoreAsync` als reine,
testbare Funktion, die aus der resident geladenen Solution + Config +
optionalem Scope-Filter einen **deterministischen** Quality-Score
(0-10, gewichtet aus Violations/CC/Footprint/Sealed-Quote) berechnet
und diesen zusammen mit Top-Violations + kontextspezifischen
Remediation-Hints in einem `ScoreResult`-Record liefert. Kein
MCP-Wrapper, keine Registrierung, keine Doku-Änderung — das ist EPIC-02
und EPIC-03. Die 5+ Unit-Tests beweisen Determinismus, leere Solution,
einzelne Violation, hoher/niedriger Score, Threshold-Logik, Edge-Cases.

## Konkrete Änderungen

### Datei 1: `src/AiNetLinter/Mcp/Tools/SafeguardScanner.cs` (neu)

- **Was:** Neue statische Klasse `internal static class SafeguardScanner`,
  `#nullable enable` am Dateianfang (Richtlinien §1, AiNetLinter.mdc
  `EnforceNullableEnable`).
  - Eine öffentliche Methode:
    `internal static Task<SafeguardScoreResult> ComputeScoreAsync(SafeguardScannerParameters p)`
    — orchestriert: `LinterEngine.RunAsync(solution, noCache: true, ...)`
    → Violations, dann lokale Helfer für CC/Footprint/Sealed-Aggregation,
    dann `BuildScoreResult` → `ScoreResult`. Defensiver
    `try/catch (Exception ex) when (ex is not OperationCanceledException)`
    analog `GetViolationsScanner.BuildViolationsTextAsync` (Zeile 73),
    liefert `IsMalfunction: true` mit `Context: ex.Message`. **Kein**
    leerer Catch, **kein** swallow.
  - Eine `internal static` deterministische Berechnungsfunktion für die
    4 Score-Komponenten — Reihenfolge:
    1. **Violation-Komponente:** gewichtete Summe der
       `EffectiveSeverity` aus `RuleViolation`s (error=2, warning=1,
       info=0.25) — Konstante in einer `internal static class
       SafeguardWeights` o. ä. zentral, damit Tests sie importieren
       können.
    2. **CC-Komponente:** Mittelwert der `MaxCognitiveComplexity` aus
       allen Klassen-Symbols der Solution (gefiltert auf
       `scopeFilter` falls gesetzt, analog `GetViolationsScanner`).
       Implementiert via direkter Roslyn-Walk:
       `solution.Projects.SelectMany(p => p.GetCompilationAsync().GetSymbolsWithName(...))` —
       **kein** neuer Lint-Lauf. KISS: für die ersten 5+ Tests reicht
       ein simpler `INamedTypeSymbol`-Walk. Coder darf auf
       `LinterAnalyzer` zurückgreifen, falls die Walk-Logik dort
       wiederverwendbar ist (`LinterAnalyzer.Classes` liefert
       `List<ClassInfo>` mit `MaxCognitiveComplexity` +
       `AIContextFootprint` — siehe `src/AiNetLinter/Models/ClassInfo.cs:25,35`).
       **Empfehlung:** erst direkter Roslyn-Walk mit einfachem
       CC-Operator, Migration auf `ClassInfo`-Pfad wenn Reuse größer
       als Coupling-Kosten.
    3. **Footprint-Komponente:** Mittelwert der
       `AIContextFootprintCalculator.Calculate(symbol)` pro
       INamedTypeSymbol — `Calculate` ist `public static` und nimmt
       ein einzelnes `INamedTypeSymbol` plus optionale Ignore-Listen
       (siehe `src/AiNetLinter/Metrics/AIContextFootprintCalculator.cs:21`).
       Coder reicht `config`-Werte (z. B. `FootprintIgnoreNamespacePrefixes`,
       `FootprintIgnoreTypeNames` aus `Config.Metrics`) durch, um
       Selbstkonsistenz mit der LinterEngine zu wahren.
    4. **Sealed-Quote:** `sealedCount / totalConcreteClassCount` (nur
       Klassen, keine Interfaces/Records/Structs/Enums; Interfaces
       sind implizit "abgeschlossen" und verzerren die Quote).
       `symbol.IsSealed && symbol.TypeKind == TypeKind.Class`.
  - Aggregation in `score = 10.0 - violationsPenalty - ccPenalty -
    footprintPenalty + sealedBonus` mit anschließendem
    `Math.Clamp(score, 0.0, 10.0)`. **Gewichte und Bonus-Faktor
    benennen** als `internal const`s (z. B. `ViolationErrorWeight =
    0.1`, `CcPerUnitOverThreshold = 0.05`,
    `FootprintPerUnitOverLimit = 0.02`, `SealedBonusPerQuarter =
    0.5`) — exakt wie in Konzept §"Wie" skizziert, mit Begründung
    im XML-Doc-Kommentar an der Konstante (warum diese Faktoren).
    Coder darf die Gewichte anpassen, **wenn** die ersten
    Test-Runs auf dem `SymbolGraphMiniFixtureWorkspace` unplausible
    Scores liefern (z. B. immer < 0 oder immer 10.0); Anpassung
    im Commit-Body dokumentieren.
  - `ScoreResult` und `ViolationEntry` und `RemediationHint` Records
    in derselben Datei (analog `GetViolationsResult` +
    `GetViolationsScannerParameters` in derselben Datei wie
    `GetViolationsScanner`). Begründung: alle Records sind
    exklusiv für diesen Scanner; Konsument ist in EPIC-02 ein
    einzelner Aufrufer, keine Duplikation.
  - Remediation-Generierung: `static string
    BuildRemediation(IReadOnlyCollection<RuleViolation> topViolations, Config config)`
    — pro `RuleName` ein kontextspezifischer Hinweis. Mapping-Tabelle
    als `static IReadOnlyDictionary<string, string>` mit den ~10
    häufigsten RuleNames aus `LinterRuleIds.cs` (z. B.
    `MaxLineCount`, `MaxMethodLineCount`, `MaxCyclomaticComplexity`,
    `MaxCognitiveComplexity`, `AIContextFootprint`,
    `EnforceSealedClasses`, `MaxConstructorDependencies`,
    `BanAsyncVoid`, `BanBlockingTaskAccess`, `EnforceNoSilentCatch`).
    Default-Hint für unbekannte RuleNames:
    "Regel-Verstoß prüfen — Details in Docs/configuration.md".
    Coder darf die Tabelle um weitere Regeln aus
    `LinterRuleIds` erweitern, falls Tests das verlangen.
- **Warum:** Reine Funktion, vollständig unit-testbar, deterministisch
  (kein `DateTime.Now`, kein `Random`, keine externen I/O außer
  Roslyn-Walk; selbst der `LinterEngine.RunAsync`-Aufruf ist
  `noCache: true` und liest die resident gehaltene Solution).

### Datei 2: `src/AiNetLinter/Mcp/Tools/SafeguardScanner.cs` (Records, gleiche Datei)

- **Was:** Drei `internal sealed record`s + ein Parameter-Record:
  - `SafeguardScannerParameters(Solution Solution, ILinterEngineConfig
    Config, ILintConsole Console, string? ScopeFilter, CancellationToken
    CancellationToken, double MinScoreThreshold = 8.0, int
    MaxRemediationEntries = 20)` — 7 Felder, aber 3 sind "nur"
    Plumbing (Solution, Config, CancellationToken analog
    `GetViolationsScannerParameters`) und 3 sind
    safeguard-spezifisch. **Entscheidung:** Record mit 7 Feldern,
    keine Suppression nötig (`MaxMethodParameterCount: 4` gilt für
    Methoden-Parameter, nicht Record-Konstruktoren — bestehendes
    Pattern in `GetViolationsScannerParameters` mit 6 Feldern).
  - `ScoreResult(bool Passed, double Score, double Threshold,
    IReadOnlyList<ViolationEntry> Violations, RemediationHint
    Remediation, string Summary)` — top-level Output-Container.
  - `ViolationEntry(string FilePath, int LineNumber, string RuleName,
    string Details, string Severity, string Guidance)` — 1:1-Mapping
    aus `RuleViolation`, sortier- und vergleichbar nach
    `(FilePath, LineNumber)`.
  - `RemediationHint(string TopIssue, IReadOnlyList<string>
    ActionableSteps, string DocumentationHint)` — strukturierte
    Remediation statt freier Text, damit EPIC-02 das in einen
    strukturierten JSON-Schema-Output mappen kann.
  - `SafeguardScoreResult(ScoreResult? Score, bool IsMalfunction,
    string? Context = null)` — analog `GetViolationsResult` (Text →
    Score, IsMalfunktion, Context).
- **Warum:** Records sind die "JSON-Schema-Bausteine" aus dem Epic
  (siehe Konzept §"Muss-Haben" Punkt 5, §"Wie" Schritt 1).
  Strukturierte Felder erlauben MCP-Schema-2020-12-Output in EPIC-02
  ohne Schema-Builder-Code in EPIC-01.

### Datei 3: `src/AiNetLinter.Tests/Mcp/Tools/SafeguardScannerTests.cs` (neu)

- **Was:** xUnit v3 Testklasse (Konzept §4 `xUnit v3 Tests: Pflicht`),
  `public sealed class SafeguardScannerTests` (`*.Tests` Override
  in `rules.json` Zeile 387: `EnforceSealedClasses` aus), nutzt
  `IClassFixture<SymbolGraphCatalogFixture>` für die Live-Mini-Solution.
  Tests (mindestens diese 5+, entsprechend Konzept §"Muss-Haven" 8 +
  Konzept §"Steps" Step 1 DoD):
  - **`ComputeScoreAsync_EmptySolution_ReturnsHighScore`**
    — synthetische leere `AdhocWorkspace`-Lösung (kein Document),
    `await scanner.ComputeScoreAsync(...)` → `Score >= 9.0`,
    `Passed == true`.
  - **`ComputeScoreAsync_SingleViolation_LowersScoreBelowThreshold`**
    — synthetische Solution mit einer Klasse, die
    `MaxLineCount`-Violation provoziert; Score < `MinScoreThreshold`
    (8.0), `Passed == false`, `Violations.Count >= 1`.
  - **`ComputeScoreAsync_KnownFixture_HasAtLeastOneViolation`**
    — `SymbolGraphCatalogFixture.Catalog` (enthält
    `ViolationTrigger`); Score ist endlich im [0, 10]-Bereich,
    `Violations` ist nicht leer.
  - **`ComputeScoreAsync_HighScoreAboveThreshold_Passes`**
    — Mini-Solution mit lauter `sealed` Klassen, kurzen Methoden,
    kleinen Footprints → Score deutlich über 8.0, `Passed == true`.
    Synthetische `AdhocWorkspace` mit 3-4 trivialen Klassen, damit
    der Test unabhängig von externer Fixture-Realität ist.
  - **`ComputeScoreAsync_LowScoreBelowThreshold_Fails`**
    — Mini-Solution mit einer Riesen-Klasse (viele Methoden, viele
    Dependencies, nicht sealed) → Score < 8.0, `Passed == false`,
    `Remediation.ActionableSteps` nicht leer.
  - **`ComputeScoreAsync_ThresholdLogic_ScoreEqualToThreshold_Passes`**
    — Score genau gleich `MinScoreThreshold` → `Passed == true`
    (Konzept-Formel: `passed = score >= threshold`).
  - **`ComputeScoreAsync_Determinismus_ZweiLaufeIdentischerScore`**
    — Zweimal `ComputeScoreAsync` mit identischem `Solution` +
    `Config` → `Score`, `Violations.Count`, `Summary`, `Remediation`
    Byte-für-Byte identisch (über `Assert.Equal` mit deep
    comparison; die deterministische Sortierung der Violations
    nach `(FilePath, LineNumber, RuleName)` macht das möglich).
  - **`ComputeScoreAsync_LinterEngineThrows_ReturnsMalfunctionWithContext`**
    — Pattern analog `GetViolationsToolTests.ExecuteAsync_LinterEngineThrows_ReturnsMalfunctionWithIsErrorTrueAndRetryHint`
    (Zeile 107-154) mit dem `ThrowingTextLoader`-Fake:
    `IsMalfunction == true`, `Context` enthält die Exception-Message.
  - **`BuildRemediation_UnknownRuleName_FallsBackToDefaultHint`**
    — Direkter Test der `BuildRemediation`-Methode mit einer
    `RuleViolation` mit unbekanntem `RuleName` →
    `ActionableSteps` enthält den Default-Hinweis.
  - **`BuildScoreResult_ClampsScoreToZeroAndTen`**
    — Direkter Test: Eingabe-Sub-Score erzeugt Roh-Wert < 0 → 0.0;
    Roh-Wert > 10 → 10.0.
  - **`SafeguardScannerParameters_DefaultThreshold_Is8`**
    — Property-Test: `new SafeguardScannerParameters(...)` ohne
    explizites `MinScoreThreshold` → `MinScoreThreshold == 8.0`.
- **Warum:** Diese Tests erfüllen Konzept §"Muss-Haben" 8 (10+
  Unit-Tests — EPIC-01 liefert 10+ für den Scanner, EPIC-02
  liefert weitere 5+ für den Tool-Wrapper → gemeinsam die
  Konzept-Vorgabe) und Konzept §"Steps" Step 1 DoD
  ("`dotnet test --filter FullyQualifiedName~SafeguardScanner`
  grün").

### PfadOverride- / `rules.json`-Änderungen

- **Keine** Änderung an `rules.json` in EPIC-01. `minScoreDefault:
  8.0` bleibt Konstante in `SafeguardScannerParameters.Default`.
  Begründung: Konzept §"Wo im Projekt" listet `rules.json` als
  "optional … falls nicht schon strukturell vorhanden" — eine
  Erweiterung des Schemas um ein `safeguard`-Section ist ein
  eigener Tech-Debt-Punkt (oder EPIC-02/EPIC-03, falls dort
  natürlich passend), nicht EPIC-01.
- **Keine** Änderung an `AnalysisToolRegistrations.cs` in EPIC-01
  (Tool-Registrierung ist EPIC-02). Falls EPIC-02 den Footprint
  über 2870 treibt, ist die Entscheidung "Konsolidierung oder
  weiterer PathOverride" dort ad-hoc zu treffen (Konzept
  §"Entdeckte Mängel" — Planer prüft in EPIC-02 erneut).

## Tests

- [ ] `dotnet test --filter FullyQualifiedName~SafeguardScanner` grün
- [ ] `dotnet test --filter FullyQualifiedName~Safeguard` grün
      (umfasst Scanner-Tests; Tool-Tests in EPIC-02)
- [ ] `dotnet build` grün mit `TreatWarningsAsErrors=true`
      (Richtlinien §5 Zero-Warning-Direktive)
- [ ] Determinismus-Test grün: zwei aufeinanderfolgende
      `ComputeScoreAsync`-Aufrufe mit identischem Input liefern
      strukturell identischen `ScoreResult`
- [ ] Threshold-Logik: `score == threshold` → `Passed == true`,
      `score < threshold` → `Passed == false`
- [ ] Malfunction-Pfad: LinterEngine-Exception → `IsMalfunction ==
      true` mit `Context` non-null, kein leerer `catch`
- [ ] `MaxMethodLineCount` der `SafeguardScanner.ComputeScoreAsync`
      ≤ 60 (`*.Tests`-Override 100 greift nicht, da Scanner im
      Produktionsprojekt)
- [ ] `AIContextFootprint` von `SafeguardScanner.cs` ≤ 2500
      (Standard-Limit, kein PathOverride nötig wenn Pattern 1:1
      von `GetViolationsScanner` übernommen)

## Definition of Done

- [ ] Alle "Konkrete Änderungen" umgesetzt (Datei 1, Datei 2 in
      derselben Datei, Datei 3)
- [ ] `dotnet build` grün (Warnings-as-Errors)
- [ ] `dotnet test --filter FullyQualifiedName~SafeguardScanner` grün
      mit 10+ Tests
- [ ] `dotnet test --filter Category=Unit` grün (keine
      Regressionen in den ~200 bestehenden Unit-Tests)
- [ ] Commit auf aktuellem Branch (Conventional Commit auf Deutsch,
      imperativ, mit `[safeguard]`-Suffix, siehe
      `roadmap.md` Tech-Stack-Notiz)
- [ ] `tasks/safeguard/step-001/step-result.md` geschrieben mit
      Commit-Hash(es) und grünem Test-Output
- [ ] `status` in `step-plan.md` von `open` auf
      `done (pending audit)` gesetzt

## Rules-Refs

- `.agents/rules/AiNetLinterRichtlinien.mdc#1` — monolithisch &
  statisch: `static class SafeguardScanner`, kein DI, kein
  ALC, kein Plugin
- `.agents/rules/AiNetLinterRichtlinien.mdc#2` — Architektur-Verbote
  (ALC, Plugin, DI): keine dieser Maschinerien im Scanner
- `.agents/rules/AiNetLinterRichtlinien.mdc#3` — Windows /
  PowerShell / keine sed-Hacks; Commit-Commands bleiben für den
  Coder (out of scope hier)
- `.agents/rules/AiNetLinterRichtlinien.mdc#4` — xUnit v3 Tests
  Pflicht; **keine** neuen zwangsserialisierenden Collections
  (Tests laufen parallel, keine globalen Stdout-Streams)
- `.agents/rules/AiNetLinterRichtlinien.mdc#5` — Result-Pattern
  (hier als `SafeguardScoreResult` mit `IsMalfunction`/Context
  statt Exception-Wurf), Zero-Warning, sparsame Kommentare ohne
  Task-/Step-/EPIC-Referenzen im Produktionscode, **keine**
  TD-IDs im Code, `sealed` für Records (`internal sealed record`),
  `record` für unveränderliche Daten
- `.agents/rules/AiNetLinter.mdc` — Grenzwerte: `MaxLineCount=500`,
  `MaxMethodLineCount=60`, `MaxMethodParameterCount=4` (Records
  ausgenommen), `MaxCyclomaticComplexity=12`,
  `MaxCognitiveComplexity=15`, `AIContextFootprint=2500`,
  `MaxPublicMembersPerType=15`, `EnforceNullableEnable`,
  `EnforceSealedClasses`, `EnforceAsciiIdentifiers`,
  `EnforcePascalCase`, `BanAsyncVoid`, `BanBlockingTaskAccess`,
  `EnforceNoSilentCatch`, `AvoidExcessiveMiddleMen`
  (Scanner ist nicht reine Middleman-Klasse, da eigene
  Berechnungslogik), `DetectAndBanPhantomDependencies` (alle
  `using`s auflösbar)
- `rules.json` (`Metrics.MaxAIContextFootprint=2500`,
  `ProjectOverrides.*.Tests.MaxMethodLineCount=100`,
  `ProjectOverrides.*.Tests.EnforceSealedClasses=false`)

## Bekannte Ausnahmen

- **Konzept-Formel-Gewichte:** Konzept §"Wie" listet 0.1, 0.05, 0.02,
  0.5 als Skizze. Diese Werte werden in `SafeguardScanner` als
  benannte Konstanten übernommen, dürfen aber vom Coder an die
  ersten Test-Ergebnisse angepasst werden, wenn die Scores
  offensichtlich unplausibel verteilt sind (z. B. immer < 0 oder
  immer 10.0). Anpassung im Commit-Body dokumentieren.
- **Test-Edge-Cases ohne Fixture:** Die Tests
  `ComputeScoreAsync_HighScoreAboveThreshold_Passes`,
  `ComputeScoreAsync_LowScoreBelowThreshold_Fails`,
  `ComputeScoreAsync_EmptySolution_ReturnsHighScore` und
  `BuildScoreResult_ClampsScoreToZeroAndTen` brauchen entweder
  synthetische `AdhocWorkspace`s oder direkten Zugriff auf
  `BuildScoreResult` (internal). Coder wählt die einfachere
  Variante; bei Synthese-`AdhocWorkspace` ist auf
  `LinterEngine.RunAsync`-Kompatibilität zu achten
  (siehe `GetViolationsToolTests.ExecuteAsync_LinterEngineThrows_…`
  für ein bewährtes Muster mit AdhocWorkspace + DocumentInfo).
- **`LinterAnalyzer`-Reuse:** Falls die direkte Roslyn-Walk
  dupliciert, was `LinterAnalyzer` schon macht, Coder darf auf
  `LinterAnalyzer` umstellen. Tradeoff: stärkere Bindung an
  LinterEngine-Coupling → größerer AIContextFootprint auf der
  Scanner-Klasse. **Empfehlung:** erst direkter Walk, nur
  umstellen wenn die Direkt-Implementierung > 30 Zeilen wird
  oder einen Bug dupliziert.

## Code-Skizze (optional)

```
internal static class SafeguardScanner
{
    internal const double DefaultMinScoreThreshold = 8.0;
    internal const int DefaultMaxRemediationEntries = 20;

    // Konzept §"Wie" — bewusst benannte Konstanten, damit Tests
    // und Dokumentation dieselben Werte sehen.
    internal const double ViolationWeightPerError = 0.1;
    internal const double CcPenaltyPerUnitOverThreshold = 0.05;
    internal const double FootprintPenaltyPerUnitOverLimit = 0.02;
    internal const double SealedBonusPerQuarterOverHalf = 0.5;

    internal static async Task<SafeguardScoreResult> ComputeScoreAsync(SafeguardScannerParameters p)
    {
        var concreteConfig = (Config)p.Config; // siehe GetViolationsScanner §Begründung
        IReadOnlyCollection<RuleViolation> violations;
        try
        {
            var engine = new LinterEngine(
                config: concreteConfig, rulesJsonContent: null,
                profiler: null, console: p.Console, args: null);
            violations = await engine.RunAsync(p.Solution, noCache: true, cacheTtlMinutes: 0, p.CancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new SafeguardScoreResult(
                Score: null, IsMalfunction: true, Context: ex.Message);
        }

        var classes = EnumerateConcreteClasses(p.Solution, p.ScopeFilter, p.CancellationToken);
        var score = BuildScoreResult(
            violations, classes, concreteConfig, p.MinScoreThreshold, p.MaxRemediationEntries);
        return new SafeguardScoreResult(Score: score, IsMalfunction: false);
    }

    private static IEnumerable<INamedTypeSymbol> EnumerateConcreteClasses(
        Solution solution, string? scopeFilter, CancellationToken ct)
    {
        foreach (var project in solution.Projects)
        {
            var compilation = project.GetCompilationAsync(ct).GetAwaiter().GetResult();
            // ... iteriere TypeSymbols, filtere TypeKind.Class, wende scopeFilter an
        }
    }

    internal static ScoreResult BuildScoreResult(
        IReadOnlyCollection<RuleViolation> violations,
        IReadOnlyCollection<INamedTypeSymbol> classes,
        Config config,
        double threshold,
        int maxRemediationEntries)
    {
        // ... Violations sortiert nach (Severity, FilePath, LineNumber)
        // ... CC-Mittel, Footprint-Mittel, Sealed-Quote
        // ... Score-Formel anwenden, clampen
        // ... Summary-String, Remediation-Hint generieren
        return new ScoreResult(passed, clamped, threshold, topViolations, remediation, summary);
    }

    internal static string BuildRemediation(
        IReadOnlyCollection<RuleViolation> topViolations, Config config)
    {
        // Lookup-Tabelle + Default-Fallback
    }
}

internal sealed record SafeguardScannerParameters(
    Solution Solution, ILinterEngineConfig Config, ILintConsole Console,
    string? ScopeFilter, CancellationToken CancellationToken,
    double MinScoreThreshold = SafeguardScanner.DefaultMinScoreThreshold,
    int MaxRemediationEntries = SafeguardScanner.DefaultMaxRemediationEntries);

internal sealed record ScoreResult(
    bool Passed, double Score, double Threshold,
    IReadOnlyList<ViolationEntry> Violations,
    RemediationHint Remediation, string Summary);

internal sealed record ViolationEntry(
    string FilePath, int LineNumber, string RuleName,
    string Details, string Severity, string Guidance);

internal sealed record RemediationHint(
    string TopIssue, IReadOnlyList<string> ActionableSteps, string DocumentationHint);

internal sealed record SafeguardScoreResult(
    ScoreResult? Score, bool IsMalfunction, string? Context = null);
```

## Notes

- **EPIC-02-Anschluss:** `ScoreResult` / `ViolationEntry` /
  `RemediationHint` sind 1:1 das Output-Format, das EPIC-02 in
  `CallToolResult.Content` als structured content (JSON Schema
  2020-12) serialisiert. Der dortige `SafeguardTool` braucht
  diese Records nur noch per `JsonSerializer.Serialize` zu
  konvertieren — keine zusätzliche Mapping-Logik.
- **Auffälligkeit für Orchestrator / EPIC-02:** Das Hinzufügen
  von `AddSafeguard(...)` in `AnalysisToolRegistrations.Register(...)`
  wird die `AIContextFootprint` dieser Klasse weiter erhöhen
  (aktueller PathOverride 2870). Pull-in ist identisch zu
  `AddGetViolations` (beide nutzen `LinterEngine` + Lint-Checker),
  also voraussichtlich moderat. **Entscheidung ad-hoc in EPIC-02:**
  entweder Konsolidierung der Add-Methoden in Helper-Klasse
  (z. B. `AnalysisToolHelpers.BuildLinterEngine(state, console)`)
  oder moderater PathOverride-Anstieg (z. B. 2870 → 2950).
- **Wiederverwendete Strukturen (keine Duplikation):**
  - `LinterEngine.RunAsync` (gleiche Konstruktor-Signatur wie
    `GetViolationsScanner`)
  - `RuleViolation` als Datenquelle für `ViolationEntry`
  - `Config.Metrics.MaxCyclomaticComplexity` als CC-Threshold
  - `Config.Global.EnforceSealedClasses` als Schalter für
    Sealed-Quote-Berechnung (falls false → Sealed-Komponente
    auf 0.0 setzen statt fehlende Quote zu simulieren)
  - `Config.Metrics.FootprintIgnoreNamespacePrefixes` +
    `FootprintIgnoreTypeNames` werden an
    `AIContextFootprintCalculator.Calculate` durchgereicht
    (Selbstkonsistenz)
  - `LinterRuleIds` (siehe `src/AiNetLinter/Core/LinterRuleIds.cs`)
    als Quelle für die Remediation-Mapping-Tabelle
- **Nicht wiederverwendet (explizit):** `McpToolResults` /
  `McpSufficiencyHints` / `McpTruncation` / `McpCallLog` — alle
  Tool-Schicht, in EPIC-01 nicht relevant. Konzept
  §"Wo im Projekt" / "Nicht angefasst (bewusst)" bestätigt das.
- **Tech-Debt-Beobachtung (für den Kritiker, nicht Scope):** Die
  fehlende `GetViolationsScannerTests.cs`-Datei ist ein
  bemerkenswertes Loch — die Scanner-Logik wird aktuell nur
  indirekt über den Tool-Wrapper getestet. EPIC-01 repariert
  das für den SafeguardScanner, könnte aber als TD-Eintrag
  „fehlende Scanner-Tests für Bestandsscanner" festgehalten
  werden, falls der Kritiker das im Review beobachtet.
