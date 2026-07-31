---
status: done
type: step-result
task: ignore-suppressions
step: "001"
epic: EPIC-01
step_type: single
coded_by: coder
coded_by_model: Gemini 3.6 Flash (High)
coded_by_model_knowledge_cutoff: 2026-01
coded_at: 2026-07-31T08:36:00+02:00
code_commit_hash: 03602ea
status_after: done
blocker_category: n/a
---

# Result Step 001: CLI Option --ignore-suppressions in CliOptions, CliOptionFactory, LinterArgs und CliCommandBuilder integrieren

## Zusammenfassung

Die CLI-Option `--ignore-suppressions` wurde in der System.CommandLine Definition registriert (`CliOptions.cs`, `CliOptionFactory.cs`, `CliCommandBuilder.cs`). `LinterArgs.cs` und `Program.cs` wurden erweitert, um übergebene Sprachwerte (default `all`, Aliase `c#` -> `cs`, `razor`, `js`, `css`) zu parsen, zu normalisieren und strikt zu validieren. Um der Roslyn-Linter-Regel `MaxCyclomaticComplexity` zu genügen, wurde `Validate()` flach refaktoriert.

## Geänderte Dateien

- `src/AiNetLinter/Cli/CliOptions.cs` — `Option<string[]>` und `CliParsedArgs`-Aggregat erweitert.
- `src/AiNetLinter/Cli/CliOptionFactory.cs` — `CreateIgnoreSuppressionsOption()` hinzugefügt.
- `src/AiNetLinter/Cli/CliCommandBuilder.cs` — System.CommandLine Anbindung und `ParseCommaSeparated()` Handling für `--ignore-suppressions`.
- `src/AiNetLinter/Cli/LinterArgs.cs` — `IgnoreSuppressions`, `GetNormalizedIgnoreSuppressions()` und `Validate()` Prüfungen hinzugefügt.
- `src/AiNetLinter/Program.cs` — Zuordnung in `ToLinterArgs()` ergänzt.
- `src/AiNetLinter.Tests/Cli/IgnoreSuppressionsCliTests.cs` (neu) — 5 xUnit Tests für CLI Parsing, Validation und Normalisierung.

## Commit

- **Code-Commit-Hash:** `03602ea`
- **Message:** `feat(cli): add --ignore-suppressions CLI option and argument parsing`
- **Branch:** main
- **Push:** nein (lokal)

## Build-/Test-Output

```
dotnet build → grün
dotnet test  → grün (1003 Tests, 0 Fehler)
```

## Abweichungen vom Plan

Keine — Plan 1:1 umgesetzt.

## Beobachtungen

Keine.

## Bekannte Unschärfen

Keine.
