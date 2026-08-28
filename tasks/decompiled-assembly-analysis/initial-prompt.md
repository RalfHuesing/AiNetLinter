---
task: decompiled-assembly-analysis
type: initial-prompt
created_at: 2026-08-28T11:06:28+02:00
---

# Initial-Prompt

Beachte die Regeln `.agents/rules/*`, insbesondere
`.agents/rules/AiNetLinter-McpWorkflow.mdc`.

Deine Rolle: `.agents/Agent-Scaffolding/dev-loop/drift-loop/orchestrator.md`.

Dein Task: `tasks/decompiled-assembly-analysis`.

Mache immer größere Code-Pakete. Findings vom Kritiker und Tech-Debt usw.
wird in größere Pakete zusammengefasst. Hintergrund: Keine Mini-Pakete, was
ineffizient wäre, da wir drei Agenten pro Step losschicken.

Notiere diesen Initial-Prompt als Markdown-Datei in
`tasks/decompiled-assembly-analysis/` und verweise darauf, damit bei einem
Kontextfenster-Compact die Aufgabe nicht verloren geht und wieder aufgenommen
werden kann.

## Dauerhafte Arbeitsanker

- Konzept: `Konzept.md` (Status `ready`)
- Orchestrator-Zustand: `task-state.md`
- Grobe Planung: `roadmap.md`
- Laufende CodeMap: `codemap.md`
- Kritiker-Log: `tech-debt.md`
- Abschluss: `task-summary.md`
- Rules-Verzeichnis: `.agents/rules`
- Rollenreihenfolge pro Step: Planer → Coder → Kritiker, strikt seriell
- Größere, in sich geschlossene Pakete planen; keine künstlichen Mini-Steps
- Für jeden Rollenaufruf immer einen neuen Sub-Agenten starten; keinen
  bestehenden Sub-Agenten wiederverwenden.
- Erledigte Sub-Agenten nach ihrem Abschluss entfernen/schließen.
- Diese Agenten-Lifecycle-Regel gilt zusätzlich zur seriellen Ausführung und
  wird zusammen mit dieser Dokumentation committed.
