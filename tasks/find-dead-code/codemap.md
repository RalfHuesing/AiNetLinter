---
task: find-dead-code
type: codemap
maintained_by: planer, coder, kritiker
last_updated: 2026-08-17T17:16:00+02:00
---

# CodeMap: find-dead-code

Task-scoped Landkarte — existiert nur für diesen Task, wird mit `<task-dir>` gelöscht. Pointer-Prinzip: Ort + ein Satz, was dort ist und wozu relevant.

## Pflege — wer trägt wann ein

- **Planer, Roadmap-Modus (einmalig):** befüllt die Map initial aus dem Grobüberblick.
- **Coder (jeder Step):** ergänzt/aktualisiert Einträge für tatsächlich angelegte oder geänderte Module vor dem Doku-Commit.
- **Planer, Step-Modus (jeder Step):** liest die Map vor dem Planen, Anti-Loop-Check.
- **Kritiker:** prüft stichprobenartig Konsistenz.

## Karte

- **`src/AiNetLinter/Mcp/Tools/Analysis/`** — Beherbergt analyse-orientierte Scanner und Tool-Wrapper wie `GetViolations*`, `SearchPattern*` und `ViolationScopeFilter`. (zuletzt: init)
- **`src/AiNetLinter/Mcp/Tools/DeadCode/`** — `FindDeadCodeScanner`, `DeadCodeModels` und `DeadCodeWhitelist` für Symbol-Graph Dead-Code-Analysen. (zuletzt: step-001)
- **`src/AiNetLinter/Mcp/AnalysisToolRegistrations.cs`** — Registriert alle analyse-orientierten Tools an der MCP-Tool-Collection. (zuletzt: init)
- **`src/AiNetLinter/Mcp/ServerInstructions.cs`** — Enthält die System-Prompts und Tool-Beschreibungen für MCP-Clients. (zuletzt: init)
- **`src/AiNetLinter.FastTests/Mcp/Tools/DeadCode/`** — In-Memory Component-Tests für `FindDeadCodeScanner` gegen Adhoc-Workspaces. (zuletzt: step-001)
- **`src/AiNetLinter.IntegrationTests/McpLiveRepositoryTests.cs`** — Live-Dogfooding-Tests der MCP-Tools gegen das eigene AiNetLinter-Repository. (zuletzt: init)
