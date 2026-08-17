---
status: active
task: find-dead-code
derived_from: konzept.md
created_at: 2026-08-17T17:16:00+02:00
last_updated: 2026-08-17T17:16:00+02:00
created_by_model: gemini-2.5-pro
created_by_model_knowledge_cutoff: 2026-01
---

# Roadmap: find-dead-code

Grober Anker, kein Detailplan — Detail-Steps entstehen erst JIT im Step-Modus des Planers.

## Tech-Stack-Notiz

- **Build-Command:** `dotnet build`
- **Test-Command:** `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress && dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress`
- **Lint-Command:** `dotnet run --project src/AiNetLinter -- --sync-agent-rules-only`
- **Code-Style-Kurzfassung:** C# / .NET 10, `sealed` für konkrete Klassen, `#nullable enable`, flache Methoden (≤60 Zeilen), McCabe-Komplexität ≤12, Cognitive Complexity ≤15, AIContextFootprint ≤2500, kein DI-Container, Result-Pattern bevorzugen.
- **Commit-Konventionen:** Conventional Commits auf Deutsch, imperativ, mit Suffix `[find-dead-code]`.

## Regel-Index

- `.agents/rules/AiNetLinter.mdc` — Automatisch generierte Grenzwerte (LOC, CC, Footprint, Parameter) und C#-Qualitätsregeln aus rules.json.
- `.agents/rules/AiNetLinterRichtlinien.mdc` — Architektur-Leitplanken (monolithisch, kein ALC/DI), Windows/PowerShell-Toolregeln, Test- und Commit-Konventionen sowie Qualitätsdrift-Prävention.

## Epics

- [x] EPIC-01: Core-Scanner & Scope-Bounding-Pipeline — Implementierung von `FindDeadCodeScanner` und Datenmodellen mit Document-Scoped Search ($O(\text{doc})$), Top-Down-Container-Pruning, Whitelisting (Compiler/Runtime/Entry-Points/Utility-Ctors) und Interface/Override-Kaskadierung (→ step-001).
- [x] EPIC-02: Diagnosen & Locals-Erkennung (Mode-Support) — Integration von Compiler- und Roslyn-Diagnosen (`CS0169`, `CS0414`, `IDE0051`, `IDE0052`) für `mode: locals` / `both` (→ step-002).
- [ ] EPIC-03: MCP-Tool-Wrapper & Registrierung — Implementierung von `FindDeadCodeTool`, Tool-Registrierung in `AnalysisToolRegistrations.cs`, Aktualisierung von `ServerInstructions.cs`, Formatierung von Structured Output und Text-Output inklusive `limitsApplies`-Matrix und `recommendedNextAction` (in Arbeit -> step-003).
- [ ] EPIC-04: Testsuite & Integration-Verifikation — Unit- & Component-Tests in `AiNetLinter.FastTests` (Interface-Kaskadierung, Scope-Bounding, Whitelist-Sonderfälle, Filter-Kombinationen, Pagination) und Live-Dogfooding-Test in `AiNetLinter.IntegrationTests` (Bezug: `konzept.md` §3.7, §Definition of Done).
