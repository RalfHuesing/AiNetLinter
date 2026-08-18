---
status: active
task: 01-namespace-tree
derived_from: konzept.md
created_at: 2026-08-18T23:35:00+02:00
last_updated: 2026-08-18T23:35:00+02:00
created_by_model: claude-3-7-sonnet
created_by_model_knowledge_cutoff: 2026-01
---

# Roadmap: 01-namespace-tree

Grober Anker, kein Detailplan — Detail-Steps entstehen erst JIT im
Step-Modus des Planers, siehe `spec.md` §7.2.

## Tech-Stack-Notiz

- **Build-Command:** `dotnet build`
- **Test-Command (Gate/Final):** `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress; dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress` (vollständiger Verifikationslauf über alle Subprozesse, ~2 Min)
- **Fast-Test-Command (Iteration während Dev):** `dotnet test src/AiNetLinter.FastTests --filter Category=Unit` bzw. `Category=Component` (schnelles Feedback in-memory, <10s)
- **Lint-Command:** MCP-Tool `get_violations`
- **Code-Style-Kurzfassung:** C# .NET 10, `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`, `sealed` für konkrete Klassen, flache Methoden (≤60 Zeilen, max. 4 Parameter), `Result<T>`/`McpToolResults` ohne unnötige Exceptions, keine ALCs oder Reflection-Plugins, Footprint < 2500 Zeilen.
- **Commit-Konventionen:** Conventional Commits auf Deutsch, imperativ (`feat:`, `fix:`, `docs:`, `test:`, `refactor:`) mit Task-Suffix `[01-namespace-tree]`.

## Regel-Index

- `.agents/rules/AiNetLinter.mdc` — Auto-generierte Linter-Grenzwerte (LOC, Komplexität, Footprint, sealed, nullable, agent-resilience).
- `.agents/rules/AiNetLinterRichtlinien.mdc` — Architektur-Leitplanken, MCP-Dogfooding, Test-Strategie (Fast vs. Integration vs. Stress) und Code-Style-Vorgaben.

## Epics

- [x] EPIC-01: Core Scanner & Models (`GetNamespaceTreeScanner`) — Roslyn-basierte Extraktion für 3 Zoom-Stufen (Projekte, Namespaces, Typen) inkl. Filterung (Source-Only, Compiler-Generated) und Truncation. (→ step-001)
- [x] EPIC-02: Tool-Registrierung & MCP-Integration (`get_namespace_tree`) — Registrierung in `FileStructureToolRegistrations`, `OverviewResourceRegistration`, `ServerInstructions` und Verifikation via `McpServerOptionsFactory`. (→ step-002)
- [ ] EPIC-03: Umfassende FastTests & IntegrationTests — Testabdeckung in `AiNetLinter.FastTests` (>15 Tests für Zoom-Stufen, Filter, Truncation, Errors) und IntegrationTest-Verifikation. (Bezug: `konzept.md` §Definition of Done #11-13)
- [ ] EPIC-04: Dokumentation, Backlog & Roadmap-Synchronisation — Aktualisierung von `Docs/configuration.md`, `Docs/integration.md`, `Docs/ROADMAP.md`, `README.md`, `tasks/features/00-uebersicht.md` und `tasks/features/01-namespace-tree.md`. (Bezug: `konzept.md` §Definition of Done #14-15)


