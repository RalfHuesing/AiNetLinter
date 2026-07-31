---
status: done (pending audit)
type: step-plan
task: ignore-suppressions
step: "004"
title: "End-to-End Linter Integrationstests für --ignore-suppressions über C#, Razor, JS und CSS erstellen"
epic: EPIC-04
estimated_risk: low
step_type: single
items: []
created_by: planer
created_by_model: Gemini 3.6 Flash (High)
created_by_model_knowledge_cutoff: 2026-01
created_at: 2026-07-31T08:36:00+02:00
related_to:
  - tasks/ignore-suppressions/step-001/step-plan.md
  - tasks/ignore-suppressions/step-002/step-plan.md
  - tasks/ignore-suppressions/step-003/step-plan.md
---

# Step 004: End-to-End Linter Integrationstests für --ignore-suppressions über C#, Razor, JS und CSS erstellen

## Bezug

- **Task:** `ignore-suppressions`
- **Epic:** `EPIC-04` aus `roadmap.md` — Integration & Unit Test Coverage.
- **Konzept-Referenz:** `konzept.md` §Muss-Haben / §Definition of Done.

## Aktueller Projektzustand (JIT-Kontext)

In Step 001 wurden CLI-Option-Parsing-Tests, in Step 002 Engine-Tests für `IgnoreSuppressionsFilter` und in Step 003 Header-Tests erstellt. Was noch fehlt, ist ein umfassender End-to-End Integrationstest (`IgnoreSuppressionsIntegrationTests.cs`), der das Linter-Verhalten bei temporären Testdateien mit Suppressions für C#, Razor, JS und CSS verifiziert.

## Intention

Erstellung von Integrationstests in `src/AiNetLinter.Tests/Cli/IgnoreSuppressionsIntegrationTests.cs`, die nachweisen, dass:
1. Ohne `--ignore-suppressions` Suppressions in Quelldateien beachtet werden (keine Verstöße gemeldet).
2. Mit `--ignore-suppressions` (ohne Parameter oder `all`) Suppressions in C#, Razor, JS und CSS ignoriert werden (Verstöße werden gemeldet).
3. Mit `--ignore-suppressions cs,razor` Suppressions in C# und Razor ignoriert werden, während JS und CSS Suppressions beibehalten.

## Konkrete Änderungen

### Datei 1: `src/AiNetLinter.Tests/Cli/IgnoreSuppressionsIntegrationTests.cs` [NEW]

- **Was:** Erstellung der Integrationstest-Klasse für `--ignore-suppressions` mit temporären Test-Dateien (C#, Razor, JS, CSS) und `LinterEngine` / `WebFileSeparationChecker` Aufrufen.
- **Warum:** Strikte Absicherung des geforderten Systemverhaltens gemäß Definition of Done in `konzept.md`.

## Tests

- [ ] `IgnoreSuppressions_OffByDefault_SuppressesViolations`
- [ ] `IgnoreSuppressions_AllActive_BypassesSuppressionsInAllLanguages`
- [ ] `IgnoreSuppressions_SpecificLanguages_BypassesSelectedLanguagesOnly`

## Definition of Done

- [ ] Alle „Konkrete Änderungen" umgesetzt
- [ ] Build-Command (`dotnet build`) grün
- [ ] Test-Command (`dotnet test`) grün
- [ ] Commit auf aktuellem Branch
- [ ] `tasks/ignore-suppressions/step-004/step-result.md` geschrieben
- [ ] `status` in `step-plan.md` von `in_progress` auf `done (pending audit)` gesetzt

## Rules-Refs

- `.agents/rules/AiNetLinter.mdc#Kurz-Stil` — `#nullable enable`, `sealed` Testklasse.
- `.agents/rules/AiNetLinterRichtlinien.mdc#Build & Test` — xUnit v3 Integrationstests.

## Bekannte Ausnahmen

- Keine.

## Notes

- Für die Web-Dateien (JS/CSS/Razor) nutzt der Test temporäre Dateien mit Inline-Suppressions (`// ainetlinter-disable ...`, `/* ... */`, `@* ... *@`) und prüft die gemeldeten Violations.
