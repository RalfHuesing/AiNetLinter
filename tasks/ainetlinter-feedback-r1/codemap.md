---
task: ainetlinter-feedback-r1
type: codemap
maintained_by: planer, coder, kritiker
last_updated: 2026-08-15T19:51:00+02:00
---

# CodeMap: ainetlinter-feedback-r1

## Karte

- **`src/AiNetLinter/Models/RuleViolation.cs`** — Repräsentiert Regelverstöße; um `Snippet`-Property erweitert. (zuletzt: step-004)
- **`src/AiNetLinter/Core/Checkers/MiddleManChecker.cs`** — Prüft Klassen auf Excessive-Middle-Man-Muster; hier wird `ctx.IsTestFile` Skip eingebaut. (zuletzt: step-001)
- **`src/AiNetLinter/Core/Checkers/PublicMembersChecker.cs`** — Prüft Typen auf Überschreitung von `MaxPublicMembersPerType`; hier wird Test-Skip mit Opt-in-Flag eingebaut. (zuletzt: step-007)
- **`src/AiNetLinter/Configuration/MetricsConfig.cs`** — Konfigurationsmodell für Metriken; hier wird `MaxPublicMembersPerTypeApplyToTestFiles` ergänzt. (zuletzt: step-002)
- **`src/AiNetLinter/Metrics/AIContextFootprintCalculator.cs`** — Berechnet den transitiven Zeilen-Footprint; hier wird die Heuristik für declaration-only types integriert. (zuletzt: step-007)
- **`src/AiNetLinter/Mcp/Tools/DuplicateDetection/`** — Tool und Scanner für `find_duplicates`; hier werden `scopeType` und Summary-Header ergänzt. (zuletzt: step-003)
- **`src/AiNetLinter/Core/DuplicateDetection/`** — Engine und Models für Clone-Detection; hier werden `scopeType` und Testfile-Filterung integriert. (zuletzt: step-007)
- **`src/AiNetLinter/Mcp/Tools/Analysis/GetViolationsScanner.cs`** — Scanner für `get_violations`; hier wird die Source-Snippet-Extraktion integriert. (zuletzt: step-004)
- **`src/AiNetLinter/Mcp/Tools/Analysis/GetViolationsTool.cs`** — Tool-Handler für `get_violations`. (zuletzt: step-007)
- **`src/AiNetLinter/Output/ViolationMarkdownFormatter.cs`** — Rendert Violations in Markdown; hier werden Code-Snippets formatiert. (zuletzt: step-007)
- **`src/AiNetLinter/Mcp/Tools/FileStructure/GetClassStructureTool.cs`** — Liefert tabellarische Member-/Zeilen-Übersicht eines Typs. (zuletzt: step-007)
- **`src/AiNetLinter/Mcp/Tools/FileStructure/GetClassStructureModels.cs`** — Structured-Content Models für `get_class_structure`. (zuletzt: step-005)
- **`src/AiNetLinter/Mcp/FileStructureToolRegistrations.cs`** — Registrierung der FileStructure-Tools im MCP-Server. (zuletzt: step-005)
- **`src/AiNetLinter/Mcp/OverviewResourceRegistration.cs`** — Übersicht der MCP-Tools und Server-Status. (zuletzt: step-005)
- **`Docs/configuration.md`** — Konfigurationshandbuch; um `MaxPublicMembersPerTypeApplyToTestFiles` aktualisiert. (zuletzt: step-007)
- **`Docs/agent-api.md`** — MCP- & CLI-API Referenz; um 20 Tools (`get_class_structure`), Snippets und `scopeType` aktualisiert. (zuletzt: step-007)
- **`Docs/ROADMAP.md`** — Roadmap & Changelog; um Feedback-Runde 1 ergänzt. (zuletzt: step-007)
- **`.agents/rules/AiNetLinter.mdc`** — Automatisch generierte Agenten-Regeln; re-synchronisiert. (zuletzt: step-007)
- **`rules.json`** — Globale Standardkonfiguration von AiNetLinter. (zuletzt: step-002)
- **`tests/Fixtures/BaselineMini/rules.json`** — Test-Fixture-Konfiguration für Baseline- und Integrationstests. (zuletzt: step-002)
