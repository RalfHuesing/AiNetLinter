---
status: reference
type: task-prompt-reference
task: get-file-tree
recorded_at: 2026-08-26T22:02:09+02:00
---

# Ausgangsprompt-Referenz: get-file-tree

Diese Datei hält die dauerhaften Arbeitsaufträge des Startprompts fest, damit
der Task nach einer Kontext-Komprimierung anhand stabiler Projektpfade
fortgesetzt werden kann.

## Verbindliche Nutzeraufträge

- `tasks/get-file-tree/` vollständig umsetzen.
- Die Rolle aus `.agents/Agent-Scaffolding/dev-loop/drift-loop/orchestrator.md`
  verwenden.
- Die Regeln unter `.agents/rules/*`, insbesondere
  `.agents/rules/AiNetLinter-McpWorkflow.mdc`, beachten.
- Den AiNetLinter-MCP-Server proaktiv und bei semantischen C#-Fragen vorrangig
  verwenden.
- Größere, zusammenhängende Coding-Pakete bilden.
- Tests ergänzen, wenn sie für die Änderung sinnvoll sind.
- Wenn der Coder den vollständigen Testlauf grün beendet, soll der Kritiker den
  Testlauf nicht routinemäßig wiederholen; die unabhängige Code-/Rules-/Logik- und
  Konzeptprüfung bleibt verpflichtend.
- Dokumentation einschließlich der relevanten Agenten-/MCP-Workflow-Dokumente
  mit dem implementierten Verhalten synchronisieren.
- Autonom und agentisch bis zum tatsächlichen Task-Abschluss arbeiten.

## Arbeitsanker

- Konzept: `tasks/get-file-tree/Konzept.md` (`status: ready`)
- Taskzustand: `tasks/get-file-tree/task-state.md`
- Orchestrator: `.agents/Agent-Scaffolding/dev-loop/drift-loop/orchestrator.md`
- Workflow-Spezifikation: `.agents/Agent-Scaffolding/dev-loop/drift-loop/spec.md`
- Projektregeln: `.agents/rules/`
- Projektleitfaden: `AGENTS.md`

## Fortsetzungsregel

Vor jeder Fortsetzung zuerst `task-state.md`, `roadmap.md`, `codemap.md` und den
letzten Step-Plan/-Result/-Review lesen; danach den tatsächlichen Codezustand
und den AiNetLinter-MCP-Kontext erneut prüfen. Keine bereits abgeschlossenen
Steps von vorn beginnen.
