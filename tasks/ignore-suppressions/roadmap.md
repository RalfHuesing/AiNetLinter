---
status: active
task: ignore-suppressions
derived_from: konzept.md
created_at: 2026-07-31T08:36:00+02:00
last_updated: 2026-07-31T08:36:00+02:00
created_by_model: Gemini 3.6 Flash (High)
created_by_model_knowledge_cutoff: 2026-01
---

# Roadmap: ignore-suppressions

Grober Anker, kein Detailplan — Detail-Steps entstehen erst JIT im Step-Modus des Planers. Diese Datei wird laufend angepasst.

## Tech-Stack-Notiz

- **Build-Command:** `dotnet build`
- **Test-Command:** `dotnet test`
- **Lint-Command:** `dotnet run --project src/AiNetLinter -- <args>`
- **Code-Style-Kurzfassung:** C# / .NET 10 CLI Tool (`AiNetLinter`), `#nullable enable`, `sealed` für konkrete Klassen, flache Methoden, System.CommandLine.
- **Commit-Konventionen:** Conventional Commits (Englisch in Git, Deutsch in Vorschlägen).

## Regel-Index

- `.agents/rules/AiNetLinter.mdc` — Auto-generierte Roslyn-Linter Grenzwerte & Codequalitätsregeln aus rules.json.
- `.agents/rules/AiNetLinterRichtlinien.mdc` — Architektur-Leitplanken (monolithisch, kein ALC, kein DI), Windows/PowerShell-Umgebung und Doku-Synchronisationspflicht.

## Epics

- [ ] EPIC-01: CLI Option & Argument Parsing — `--ignore-suppressions` in `CliOptions`, `CliOptionFactory` und `LinterArgs` registrieren inkl. Argument-Parsing (Default `all`, Alias `c#` zu `cs`, Validierung).
- [ ] EPIC-02: Core Suppression Bypass Engine — `IgnoreSuppressionsFilter`/Mode implementieren und in `SuppressionScanner` sowie Web-Analyzern (C#, Razor, JS, CSS) einbinden.
- [ ] EPIC-03: Header & Report Output Rendering — CLI-Header-Ausgaben, `DebtReportBuilder` und Playbook-Output um active ignore-suppressions Status ergänzen.
- [ ] EPIC-04: Integration & Unit Test Coverage — Unit- und Integrationstests für Sprachfilter, Alias-Normalisierung, Parsing-Fehler und Bypass-Verhalten erstellen.
- [ ] EPIC-05: Documentation & Roadmap Sync — `Docs/configuration.md`, `Docs/ROADMAP.md` und `README.md` mit `--ignore-suppressions` Spezifikation aktualisieren.
