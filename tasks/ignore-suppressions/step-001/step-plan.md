---
status: open
type: step-plan
task: ignore-suppressions
step: "001"
title: "CLI Option --ignore-suppressions in CliOptions, CliOptionFactory, LinterArgs und CliCommandBuilder integrieren"
epic: EPIC-01
estimated_risk: low
step_type: single
items: []
created_by: planer
created_by_model: Gemini 3.6 Flash (High)
created_by_model_knowledge_cutoff: 2026-01
created_at: 2026-07-31T08:36:00+02:00
related_to: []
---

# Step 001: CLI Option --ignore-suppressions in CliOptions, CliOptionFactory, LinterArgs und CliCommandBuilder integrieren

## Bezug

- **Task:** `ignore-suppressions`
- **Epic:** `EPIC-01` aus `roadmap.md` — Registrierung und Argument-Parsing des CLI-Schalters `--ignore-suppressions`.
- **Konzept-Referenz:** `konzept.md` §Muss-Haben / §Wo im Projekt.

## Aktueller Projektzustand (JIT-Kontext)

Die CLI-Optionen werden über System.CommandLine in `CliOptions.cs`, `CliOptionFactory.cs` und `CliCommandBuilder.cs` registriert. `CliParsedArgs` hält das Parsing-Ergebnis vor und `Program.cs` wandelt es in ein `LinterArgs`-Objekt um. `LinterArgs.Validate()` prüft Argumente und Kombinationen.

## Intention

Einführung der CLI-Option `--ignore-suppressions` mit flexibler Sprachauswahl (`all`, `cs`/`c#`, `razor`, `js`, `css`). Behandelt Aufrufe ohne Werte (Default: `all`), Komma- und Leerzeichen-getrennte Werte, Alias-Mapping (`c#` -> `cs`), Normalisierung (Kleinschreibung, Trim) sowie Fehlervalidierung bei unbekannten Sprachen.

## Konkrete Änderungen

### Datei 1: `src/AiNetLinter/Cli/CliOptions.cs`

- **Was:** `Option<string[]>` IgnoreSuppressions zur `CliOptions`-Record-Struktur hinzufügen. Erweiterung von `CliParsedArgs` um `IReadOnlyList<string>? IgnoreSuppressions`.
- **Warum:** Typisierte Erfassung der übergebenen Sprachwerte in System.CommandLine und `CliParsedArgs`.

### Datei 2: `src/AiNetLinter/Cli/CliOptionFactory.cs`

- **Was:** `CreateIgnoreSuppressionsOption()` Methode hinzufügen (`Option<string[]>("--ignore-suppressions")` mit `Arity = ArgumentArity.ZeroOrMore`, `AllowMultipleArgumentsPerToken = true` und prägnanter Description).
- **Warum:** System.CommandLine Option-Erzeugung für den CLI-Schalter.

### Datei 3: `src/AiNetLinter/Cli/CliCommandBuilder.cs`

- **Was:** `options.IgnoreSuppressions` im `RootCommand` registrieren und in `Parse()` via `ParseCommaSeparated()` parsen. Falls die Option angegeben wurde, aber keine Argumente übergeben wurden (`Arity.ZeroOrMore`), `["all"]` als Standard festlegen. Falls die Option gar nicht im CLI-Aufruf vorkam, `null` zurückliefern.
- **Warum:** Unterscheidung zwischen „Option nicht gesetzt (`null`)" und „Option ohne Argumente gesetzt (`["all"]`)".

### Datei 4: `src/AiNetLinter/Cli/LinterArgs.cs`

- **Was:** Eigenschaft `IReadOnlyList<string>? IgnoreSuppressions { get; init; }` und `IReadOnlyList<string> NormalizedIgnoreSuppressions { get; }` oder Parsed/Normalized Support hinzufügen. Validierung in `Validate()` ergänzen, dass Werte nur `all`, `cs`, `c#`, `razor`, `js`, `css` sein dürfen (`c#` wird zu `cs` normalisiert).
- **Warum:** Strikte Validierung und Kanonisierung (`c#` -> `cs`) gemäß Konzept.

### Datei 5: `src/AiNetLinter/Program.cs`

- **Was:** Zuordnung von `parsed.IgnoreSuppressions` nach `linterArgs.IgnoreSuppressions` in `ToLinterArgs()` vornehmen.
- **Warum:** Übertragung der CLI-Argumente an das Ausführungsobjekt.

### Datei 6: `src/AiNetLinter.Tests/Cli/IgnoreSuppressionsCliTests.cs` [NEW]

- **Was:** xUnit-Tests für die Validierung und Normalisierung der `--ignore-suppressions` Argumente (Default `all`, Alias `c#` -> `cs`, ungültige Werte, Kombinationen).
- **Warum:** Sicherstellung der CLI-Argument-Parsing-Logik gemäß DoD.

## Tests

- [ ] `IgnoreSuppressions_NoValue_DefaultsToAll`
- [ ] `IgnoreSuppressions_AliasCSharp_NormalizesToCs`
- [ ] `IgnoreSuppressions_InvalidLanguage_ReturnsValidationError`
- [ ] `IgnoreSuppressions_MultipleLanguages_NormalizesAndDeduplicates`

## Definition of Done

- [ ] Alle „Konkrete Änderungen" umgesetzt
- [ ] Build-Command (`dotnet build`) grün
- [ ] Test-Command (`dotnet test`) grün
- [ ] Commit auf aktuellem Branch
- [ ] `tasks/ignore-suppressions/step-001/step-result.md` geschrieben
- [ ] `status` in `step-plan.md` von `in_progress` auf `done (pending audit)` gesetzt

## Rules-Refs

- `.agents/rules/AiNetLinter.mdc#Kurz-Stil` — `#nullable enable`, `sealed` Klassen, flache Methoden.
- `.agents/rules/AiNetLinterRichtlinien.mdc#Build & Test` — xUnit Tests und Zero-Warning-Direktive.

## Bekannte Ausnahmen

- Keine.

## Notes

- `System.CommandLine` liefert bei `Arity.ZeroOrMore` ein leeres Array `[]`, wenn der Schalter ohne Wert angegeben wird (`--ignore-suppressions`). Wenn der Schalter im CLI-Aufruf nicht vorhanden ist, kann dies über `parseResult.FindResultFor(options.IgnoreSuppressions) != null` geprüft werden.
