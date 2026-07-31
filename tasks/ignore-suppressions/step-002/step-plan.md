---
status: done
type: step-plan
task: ignore-suppressions
step: "002"
title: "Core Suppression Bypass Engine (IgnoreSuppressionsFilter) in SuppressionEvaluator, WebSuppressionDetector, DisableAllDetector und SuppressionScanner integrieren"
epic: EPIC-02
estimated_risk: medium
step_type: single
items: []
created_by: planer
created_by_model: Gemini 3.6 Flash (High)
created_by_model_knowledge_cutoff: 2026-01
created_at: 2026-07-31T08:36:00+02:00
related_to:
  - tasks/ignore-suppressions/step-001/step-plan.md
---

# Step 002: Core Suppression Bypass Engine (IgnoreSuppressionsFilter) in SuppressionEvaluator, WebSuppressionDetector, DisableAllDetector und SuppressionScanner integrieren

## Bezug

- **Task:** `ignore-suppressions`
- **Epic:** `EPIC-02` aus `roadmap.md` — Core Suppression Bypass Logik für C#, Razor, JS und CSS.
- **Konzept-Referenz:** `konzept.md` §Muss-Haben / §Wo im Projekt / §Entdeckte Mängel/Redundanzen.

## Aktueller Projektzustand (JIT-Kontext)

C#-Verstöße werden über `SuppressionEvaluator.IsSuppressed()` gefiltert (`LinterAnalyzer.cs`). Web-Verstöße (JS/CSS/Razor) werden über `WebSuppressionDetector.IsSuppressed()` gefiltert (`WebFileSeparationChecker.cs`). Dateiweite `disable all`-Kommentare werden über `DisableAllDetector` erkannt (`ViolationScopeFilter.cs`, `DebtReportBuilder.cs`). `SuppressionScanner.cs` sammelt aktiven Suppressions-Bestand. Bislang gab es keine Möglichkeit, diese Filterung dynamisch nach Sprachklasse zu übersteuern.

## Intention

Einführung der zentralen Klasse `IgnoreSuppressionsFilter` zur Repräsentation des Bypass-Zustands pro Sprache (`cs`, `razor`, `js`, `css`, `all`). Einbindung des Filters in `SuppressionEvaluator`, `WebSuppressionDetector`, `DisableAllDetector`, `SuppressionScanner`, `LinterAnalyzer` und `WebFileSeparationChecker`. Wenn der Bypass für eine Sprache aktiv ist, werden Suppressions ignoriert und alle Verstoße transparent gemeldet.

## Konkrete Änderungen

### Datei 1: `src/AiNetLinter/Suppression/IgnoreSuppressionsFilter.cs` [NEW]

- **Was:** Erstellung der `sealed record/class IgnoreSuppressionsFilter` zur Prüfung von `ShouldIgnoreSuppression(string languageKind)` und `ShouldIgnoreSuppressionForFile(string filePath)`.
- **Warum:** Zentrale, wiederverwendbare Entkopplung der Bypass-Logik für Roslyn- & Web-Analyzer.

### Datei 2: `src/AiNetLinter/Suppression/SuppressionEvaluator.cs`

- **Was:** Überladung / optionaler Parameter `IgnoreSuppressionsFilter? filter = null` für `IsSuppressed()`. Wenn der Filter für `cs` aktiv ist, wird `IsSuppressed` false geliefert (Verstoß wird nicht unterdrückt).
- **Warum:** Dynamischer Suppression-Bypass für C#-Analyzer.

### Datei 3: `src/AiNetLinter/Web/WebSuppressionDetector.cs`

- **Was:** Überladung / optionaler Parameter `IgnoreSuppressionsFilter? filter = null` und `string languageKind` für `IsSuppressed()`. Wenn der Filter für die Web-Sprache aktiv ist, wird `IsSuppressed` false geliefert.
- **Warum:** Dynamischer Suppression-Bypass für JS-, CSS- und Razor-Analyzer.

### Datei 4: `src/AiNetLinter/Suppression/DisableAllDetector.cs`

- **Was:** Überladung / optionaler Parameter `IgnoreSuppressionsFilter? filter = null` für `HasDisableAll` & `FileHasDisableAll`.
- **Warum:** Konsistente Behandlung bei Wave-Ready- & Debt-Filtern.

### Datei 5: `src/AiNetLinter/Suppression/SuppressionScanner.cs`

- **Was:** Erweitern von `ScanFile` & `ScanAllAsync` um `IgnoreSuppressionsFilter? filter = null`.
- **Why:** Berücksichtigung des Ignore-Filters beim Scannen von Suppressions.

### Datei 6: `src/AiNetLinter/Core/LinterAnalyzer.cs` & `src/AiNetLinter/Web/WebFileSeparationChecker.cs`

- **Was:** Übergabe des `IgnoreSuppressionsFilter` an `SuppressionEvaluator` und `WebSuppressionDetector` während der Verstoß-Filterung.
- **Warum:** Durchreichen des CLI-Bypass-Modus aus `LinterArgs` an den Analyse-Pipeline-Standard.

### Datei 7: `src/AiNetLinter.Tests/Suppression/IgnoreSuppressionsFilterTests.cs` [NEW]

- **Was:** Unit-Tests für `IgnoreSuppressionsFilter` sowie SuppressionEvaluator/WebSuppressionDetector Integration.
- **Warum:** Nachweis der korrekten Filterauswertung je Sprachklasse (`cs`, `razor`, `js`, `css`, `all`).

## Tests

- [ ] `IgnoreSuppressionsFilter_CsLanguage_BypassesCsOnly`
- [ ] `IgnoreSuppressionsFilter_All_BypassesAllLanguages`
- [ ] `SuppressionEvaluator_WithFilterActive_ReturnsNotSuppressed`
- [ ] `WebSuppressionDetector_WithFilterActive_ReturnsNotSuppressed`

## Definition of Done

- [ ] Alle „Konkrete Änderungen" umgesetzt
- [ ] Build-Command (`dotnet build`) grün
- [ ] Test-Command (`dotnet test`) grün
- [ ] Commit auf aktuellem Branch
- [ ] `tasks/ignore-suppressions/step-002/step-result.md` geschrieben
- [ ] `status` in `step-plan.md` von `in_progress` auf `done (pending audit)` gesetzt

## Rules-Refs

- `.agents/rules/AiNetLinter.mdc#Kurz-Stil` — `sealed` Klassen, flache Methoden.
- `.agents/rules/AiNetLinterRichtlinien.mdc#Quality` — Zero Warnings & xUnit v3 Tests.

## Bekannte Ausnahmen

- Keine.

## Notes

- `IgnoreSuppressionsFilter` unterstützt sowohl Sprach-Tokens (`cs`, `razor`, `js`, `css`, `all`) als auch automatische Dateiendungs-Ermittlung (`.cs`, `.razor`, `.js`, `.css`).
