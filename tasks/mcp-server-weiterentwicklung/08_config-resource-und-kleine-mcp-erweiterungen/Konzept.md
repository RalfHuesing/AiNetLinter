---
status: ready
type: konzept
project_kind: brownfield
estimated_scope: small-medium
priority: P3
agent_role: .agents/Agent-Scaffolding/dev-loop/planning/orchestrator.md
rules_dir: .agents/rules
last_updated: 2026-08-26
herkunft: Review-Finding 2026-08-21 (ox-alpha)
---

# Effektive Regelkonfiguration als MCP-Resource (`ainetlinter://rules`)

**Lücke:** Ein Agent sieht Violations (`get_violations`), aber nicht die aktiven Schwellwerte
(MaxMethodLineCount=42? MaxCyclomaticComplexity=?). Um "darf ich diese Methode so lassen?"
zu beantworten, muss der Agent raten oder `rules.json` selbst suchen/lesen — außerhalb des
Servers, mit Pfad-Raten (Default vs. projekteigene Config).

**Vorschlag:** Resource `ainetlinter://rules` (Markdown, analog `ainetlinter://overview`,
frisch pro Read generiert via `GetConfigSnapshot()`): aktive Regeln + effektive Schwellwerte +
Config-Herkunft (Default oder Pfad).

**Aufwand:** klein (eine Registration + Formatter + Paritätstest). Kein neues Tool.
**Evidenzbedarf:** gering — die Information existiert serverseitig bereits, sie ist nur
nicht exponiert. Eher Vollendung denn Neuerung.

## Definition of Done

- Resource registriert und als `ainetlinter://rules{?projectRoot}` adressierbar.
- Paritätstest gegen die effektive Config des adressierten Projekt-Keys.
- `Docs/agent-api.md` beschreibt Resource und Ausgabe.
- `dotnet build` sowie beide Nicht-Stress-Testprojekte sind grün.
