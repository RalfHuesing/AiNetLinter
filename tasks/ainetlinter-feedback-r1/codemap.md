---
task: ainetlinter-feedback-r1
type: codemap
maintained_by: planer, coder, kritiker
last_updated: 2026-08-15T19:10:00+02:00
---

# CodeMap: ainetlinter-feedback-r1

## Karte

- **`src/AiNetLinter/Core/Checkers/MiddleManChecker.cs`** — Prüft Klassen auf Excessive-Middle-Man-Muster; hier wird `ctx.IsTestFile` Skip eingebaut. (zuletzt: step-001)
- **`src/AiNetLinter/Core/Checkers/PublicMembersChecker.cs`** — Prüft Typen auf Überschreitung von `MaxPublicMembersPerType`; hier wird Test-Skip mit Opt-in-Flag eingebaut.
- **`src/AiNetLinter/Configuration/MetricsConfig.cs`** — Konfigurationsmodell für Metriken; hier wird `MaxPublicMembersPerTypeApplyToTestFiles` ergänzt.
- **`src/AiNetLinter/Metrics/AIContextFootprintCalculator.cs`** — Berechnet den transitiven Zeilen-Footprint; hier wird die Heuristik für declaration-only types integriert.
- **`src/AiNetLinter/Mcp/Tools/DuplicateDetection/`** — Tool und Scanner für `find_duplicates`; hier werden `scopeType` und Summary-Header ergänzt.
- **`src/AiNetLinter/Mcp/Tools/Analysis/GetViolationsScanner.cs`** — Scanner für `get_violations`; hier wird die Source-Snippet-Extraktion integriert.
- **`src/AiNetLinter/Output/ViolationMarkdownFormatter.cs`** — Rendert Violations in Markdown; hier werden Code-Snippets formatiert.
- **`src/AiNetLinter/Mcp/Tools/FileStructure/`** — Struktur-Tools; hier wird das neue Tool `GetClassStructureTool.cs` implementiert.
- **`src/AiNetLinter/Mcp/FileStructureToolRegistrations.cs`** — Registrierung der FileStructure-Tools im MCP-Server.
- **`src/AiNetLinter/Mcp/McpJsonOptions.cs`** — JSON-Optionen und Schemadefinitionen für MCP-Aufrufe.
- **`rules.json`** — Globale Standardkonfiguration von AiNetLinter.
- **`tests/Fixtures/BaselineMini/rules.json`** — Test-Fixture-Konfiguration für Baseline- und Integrationstests.
