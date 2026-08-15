---
status: active
task: ainetlinter-feedback-r1
derived_from: konzept.md
created_at: 2026-08-15T19:10:00+02:00
last_updated: 2026-08-15T19:10:00+02:00
created_by_model: gemini-3.7-flash
created_by_model_knowledge_cutoff: 2026-01
---

# Roadmap: ainetlinter-feedback-r1

## Tech-Stack-Notiz

- **Build-Command:** `dotnet build`
- **Test-Command:** `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress && dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress`
- **Lint-Command:** `dotnet run --project src/AiNetLinter -- --sync-agent-rules-only`
- **Code-Style-Kurzfassung:** C# 13 / .NET 10, Immutability & Record-Types, `#nullable enable`, `sealed` für konkrete Klassen, keine dynamischen Plugins, keine DI-Container.
- **Commit-Konventionen:** Conventional Commits auf Deutsch, imperativ, Suffix `[ainetlinter-feedback-r1]`.

## Regel-Index

- `.agents/rules/AiNetLinter.mdc` — Automatisch generierte Linter-Regeln, Grenzwerte und Metriken für C#-Code.
- `.agents/rules/AiNetLinterRichtlinien.mdc` — Architektur-Leitplanken, Windows/Tool-Regeln, Testsuite-Vorgaben und Verhaltensregeln.

## Epics

- [x] EPIC-01: FB-02 — `AvoidExcessiveMiddleMen` für Testfiles überspringen (`MiddleManChecker.cs`, Testfall in FastTests). (→ step-001)
- [x] EPIC-02: FB-03 — `MaxPublicMembersPerType` für Testfiles standardmäßig überspringen mit Opt-in-Flag (`PublicMembersChecker.cs`, `MetricsConfig.cs`, `rules.json`, Baseline-Config, FastTests). (→ step-002)
- [ ] EPIC-03: FB-04 — `find_duplicates` UX-Verbesserungen: Top-Cluster Summary bei >20 Treffern und neuer `scopeType`-Filter (`all` | `production` | `tests`) (`DuplicateDetectionScanner.cs`, `DuplicateDetectionTool.cs`, Fast- und IntegrationTests).
- [ ] EPIC-04: B — Code-Snippet in `get_violations` direkt mitgeben (`contextLines`, `includeSnippet`, Truncation, `GetViolationsScanner.cs`, `ViolationMarkdownFormatter.cs`, Fast- und IntegrationTests).
- [ ] EPIC-05: A — Neues MCP-Tool `get_class_structure` für tabellarische Member-/Zeilen-Übersicht (`GetClassStructureTool.cs`, Registrierung in `FileStructureToolRegistrations.cs`, Fast- und IntegrationTests).
- [ ] EPIC-06: FB-01 — Heuristik für „declaration-only types" im `AIContextFootprint` (`AIContextFootprintCalculator.cs`, FastTests).
- [ ] EPIC-07: Doku-, Schemata- und Konfig-Abschluss-Synchronisation (`Docs/configuration.md`, `Docs/agent-api.md`, `Docs/ROADMAP.md`, Agent-Rules-Sync).
