# Orchestrator-Arbeitsweise

Der Orchestrator wird im Chat ausdrücklich aktiviert, zum Beispiel:

> Setze diesen Task als Orchestrator um und verwende Implementer und Reviewer.

Der Orchestrator liest zuerst
`.agents/skills/project-orchestrator/SKILL.md` und die benötigten Rollen. Ohne
ausdrückliche Orchestrierung arbeitet der Hauptagent eine normale Aufgabe
direkt mit dem passenden Projektkontext ab.

## Feste Grenzen

- ein fachlich zusammenhängender Slice pro Delegations-/Review-/Commit-Zyklus
- mehrere Slices nur innerhalb eines ausdrücklich orchestrierten Tasks
- höchstens drei aktive Subagenten
- keine verschachtelte Delegation
- schreibende Subagenten nicht parallel im gemeinsamen Working Tree
- Reviewer und Auditor sind read-only
- nur der Orchestrator aktualisiert Roadmap-Status und erstellt Commits
- Konzeptplanung und Umsetzung bleiben getrennte Phasen

Task-lokale Roadmaps oder Notizen werden nur angelegt, wenn der Task sie
braucht oder ausdrücklich verlangt. Kleine Aufgaben erhalten keine künstliche
Dokumentationspflicht.
