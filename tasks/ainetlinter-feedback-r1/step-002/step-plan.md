---
status: in_progress
type: step-plan
task: ainetlinter-feedback-r1
step: "002"
corrects: null
title: "FB-03: MaxPublicMembersPerType fuer Testfiles standardmaessig ueberspringen mit Opt-in"
epic: EPIC-02
estimated_risk: low
step_type: single
items: []
created_by: planer
created_by_model: gemini-3.7-flash
created_by_model_knowledge_cutoff: 2026-01
created_at: 2026-08-15T19:13:00+02:00
related_to: []
---

# Step 002: FB-03: MaxPublicMembersPerType fuer Testfiles standardmaessig ueberspringen mit Opt-in

## Bezug

- **Task:** `ainetlinter-feedback-r1`
- **Epic:** `EPIC-02` aus `roadmap.md` — FB-03 Testfile-Skip & Opt-in fuer PublicMembersChecker
- **Konzept-Referenz:** `konzept.md` §FB-03

## Aktueller Projektzustand (JIT-Kontext)

`PublicMembersChecker.cs` prüft derzeit `ctx.Config.Metrics.MaxPublicMembersPerType` ohne `IsTestFile`-Prüfung. Testklassen mit vielen `[Fact]`-Methoden überschreiten das Limit zwangsläufig. `MetricsConfig.cs` hat noch keine Property `MaxPublicMembersPerTypeApplyToTestFiles`.

## Intention

1. Ergänzung von `MaxPublicMembersPerTypeApplyToTestFiles: bool = false` in `MetricsConfig.cs`, `ConfigOverrides.cs` und `MetricsConfigApplier.cs`.
2. In `PublicMembersChecker.Check` die Skip-Bedingung `if (ctx.IsTestFile && !ctx.Config.Metrics.MaxPublicMembersPerTypeApplyToTestFiles) return;` einbauen.
3. Ergänzung in `rules.json` und `tests/Fixtures/BaselineMini/rules.json`.
4. FastTests in `MaxPublicMembersPerTypeTests.cs` für Standard-Skip und Opt-in.

## Konkrete Änderungen

### Datei 1: `src/AiNetLinter/Configuration/MetricsConfig.cs`
- **Was:** Property `public bool MaxPublicMembersPerTypeApplyToTestFiles { get; init; } = false;` hinzufügen.
- **Warum:** Konfigurierbarkeit des Test-Skip-Verhaltens.

### Datei 2: `src/AiNetLinter/Configuration/ConfigOverrides.cs`
- **Was:** Property `public bool? MaxPublicMembersPerTypeApplyToTestFiles { get; init; }` in `MetricsConfigOverride` hinzufügen.
- **Warum:** Unterstützung für projektbezogene Overrides.

### Datei 3: `src/AiNetLinter/Configuration/MetricsConfigApplier.cs`
- **Was:** Zuweisung `MaxPublicMembersPerTypeApplyToTestFiles = o.MaxPublicMembersPerTypeApplyToTestFiles ?? config.MaxPublicMembersPerTypeApplyToTestFiles` hinzufügen.
- **Warum:** Applier-Merge.

### Datei 4: `src/AiNetLinter/Core/Checkers/PublicMembersChecker.cs`
- **Was:** Am Anfang von `Check()`: `if (ctx.IsTestFile && !ctx.Config.Metrics.MaxPublicMembersPerTypeApplyToTestFiles) return;`
- **Warum:** Testfiles standardmäßig ausnehmen.

### Datei 5: `rules.json` & `tests/Fixtures/BaselineMini/rules.json`
- **Was:** `"MaxPublicMembersPerTypeApplyToTestFiles": false` eintragen.
- **Warum:** Schema-Konsistenz.

### Datei 6: `src/AiNetLinter.FastTests/Core/Checkers/MaxPublicMembersPerTypeTests.cs`
- **Was:** Tests `TestFile_Skipped_ByDefault` und `TestFile_Reported_WhenOptInFlagTrue` hinzufügen.
- **Warum:** Testabdeckung für Skip und Opt-in.

## Tests

- [ ] `TestFile_Skipped_ByDefault` in `MaxPublicMembersPerTypeTests.cs`
- [ ] `TestFile_Reported_WhenOptInFlagTrue` in `MaxPublicMembersPerTypeTests.cs`

## Definition of Done

- [ ] Alle „Konkrete Änderungen" umgesetzt
- [ ] `dotnet build` fehler- und warnungsfrei
- [ ] `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress` grün
- [ ] Code-Commit & Doku-Commit auf aktuellem Branch
- [ ] `step-002/step-result.md` geschrieben

## Rules-Refs

- `.agents/rules/AiNetLinterRichtlinien.mdc#1-grundprinzipien` — Test-Skip Konsistenz
- `.agents/rules/AiNetLinter.mdc#grenzwerte-produktion` — MaxPublicMembersPerType
