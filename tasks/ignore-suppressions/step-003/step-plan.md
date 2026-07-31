---
status: done (pending audit)
type: step-plan
task: ignore-suppressions
step: "003"
title: "Transparente Header-Ausgabe des Ignore-Suppressions-Modus in CLI, DebtReportBuilder und RepoPlaybookGenerator"
epic: EPIC-03
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
---

# Step 003: Transparente Header-Ausgabe des Ignore-Suppressions-Modus in CLI, DebtReportBuilder und RepoPlaybookGenerator

## Bezug

- **Task:** `ignore-suppressions`
- **Epic:** `EPIC-03` aus `roadmap.md` — Header & Report Output Rendering.
- **Konzept-Referenz:** `konzept.md` §Muss-Haben / §Wo im Projekt.

## Aktueller Projektzustand (JIT-Kontext)

`Program.cs` gibt zu Beginn eines Linter-Laufs `# Run: <timestamp>` aus. `DebtReportBuilder.cs` generiert die Überschrift `# AiNetLinter - debt report`. `RepoPlaybookGenerator.cs` generiert das Playbook-Dokument. Bislang gab es keine Ausweisung des active `--ignore-suppressions` Status in den Headern.

## Intention

Transparente Ergänzung der Header-Ausgaben um `[Ignore-Suppressions: <sprachen>]` (z. B. `[Ignore-Suppressions: cs, razor]` oder `[Ignore-Suppressions: all]`) in allen Berichten und CLI-Outputs, wenn `--ignore-suppressions` aktiv ist.

## Konkrete Änderungen

### Datei 1: `src/AiNetLinter/Program.cs`

- **Was:** Ergänzung der `# Run:` Konsolenausgabe um ` [Ignore-Suppressions: <sprachen>]`, falls `linterArgs.GetNormalizedIgnoreSuppressions()` aktiv ist.
- **Warum:** CLI-Transparenz für den Anwender beim Ausführen des Linters.

### Datei 2: `src/AiNetLinter/Output/DebtReportBuilder.cs` & `src/AiNetLinter/Commands/DebtReportCommand.cs`

- **Was:** `DebtReportBuilder.BuildAsync` um optionalen Parameter `IReadOnlyList<string>? ignoreSuppressions = null` erweitern und den Header um ` [Ignore-Suppressions: <sprachen>]` ergänzen. `DebtReportCommand.cs` übergibt `args.IgnoreSuppressions`.
- **Warum:** Transparente Ausweisung im Tech-Debt-Bericht.

### Datei 3: `src/AiNetLinter/Generators/RepoPlaybookGenerator.cs` & `PlaybookTypes.cs`

- **Was:** `PlaybookOptions` um `IReadOnlyList<string>? IgnoreSuppressions` erweitern und im Playbook-Header/Metadatenbereich den aktiven Bypass ausweisen.
- **Warum:** Transparenz in generierten Repository-Playbooks.

### Datei 4: `src/AiNetLinter.Tests/Output/DebtReportBuilderHeaderTests.cs` [NEW]

- **Was:** Unit-Tests für `DebtReportBuilder` Header-Ausgabe mit und ohne `--ignore-suppressions`.
- **Warum:** Nachweis der korrekten Formatierung im Bericht.

## Tests

- [ ] `DebtReportBuilder_WithoutIgnoreSuppressions_HeaderStandard`
- [ ] `DebtReportBuilder_WithIgnoreSuppressions_IncludesIgnoreNoticeInHeader`
- [ ] `Program_FormatHeader_WithIgnoreSuppressions_AppendsCanonicalLanguages`

## Definition of Done

- [ ] Alle „Konkrete Änderungen" umgesetzt
- [ ] Build-Command (`dotnet build`) grün
- [ ] Test-Command (`dotnet test`) grün
- [ ] Commit auf aktuellem Branch
- [ ] `tasks/ignore-suppressions/step-003/step-result.md` geschrieben
- [ ] `status` in `step-plan.md` von `in_progress` auf `done (pending audit)` gesetzt

## Rules-Refs

- `.agents/rules/AiNetLinter.mdc#Kurz-Stil` — `#nullable enable`, flache Methoden.
- `.agents/rules/AiNetLinterRichtlinien.mdc#Quality` — Zero Warnings & xUnit Tests.

## Bekannte Ausnahmen

- Keine.

## Notes

- Wenn `GetNormalizedIgnoreSuppressions()` `["all"]` enthält, wird kanonisch `[Ignore-Suppressions: all]` ausgegeben. Bei spezifischen Sprachen z. B. `[Ignore-Suppressions: cs, razor]`.
