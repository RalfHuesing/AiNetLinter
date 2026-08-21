---
status: vorschlag (Teil 1 empfohlen, Teile 2+3 evidenzabhängig)
type: konzept
project_kind: brownfield
estimated_scope: small-medium
priority: P3
agent_role: .agents/Agent-Scaffolding/dev-loop/planning/orchestrator.md
rules_dir: .agents/rules
last_updated: 2026-08-21
open_questions:
  - "Teile 2+3 (Prompts, Progress) erst nach Nutzungsdaten aus Aufgabe 01 entscheiden."
herkunft: Review-Finding 2026-08-21 (ox-alpha)
---

# Config-Resource und kleine MCP-Erweiterungen (bedingt)

Vier Kandidaten, bewusst klein und additiv gehalten. Jeder nennt die Bedingung, unter der
er umgesetzt werden sollte — analog zur Entscheidungsregel in `90_bewusst-nicht-umsetzen.md`.

## Teil 1: Effektive Regelkonfiguration als Resource (`ainetlinter://rules`) — empfohlen

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

## Teil 2: MCP Prompts Primitive — zurückgestellt bis Evidenz

**Verifiziert (2026-08-21):** 0 Treffer auf `McpServerPrompt` im Code — das dritte
MCP-Primitive neben Tools und Resources bleibt ungenutzt.

**Idee:** 2–3 Prompts (`pre-edit-context`, `quality-gate`, `refactor-drift-check`), die die
in `OverviewResourceRegistration` empfohlenen Workflow-Ketten als wiederverwendbare
Vorlagen exponieren.

**Dagegen spricht:** Host-Support für `prompts/list` ist heterogen; viele Coding-Agenten
ignorieren Prompts komplett. Die Workflows sind heute schon in Instructions und Overview
transportiert.

**Bedingung:** Nur umsetzen, wenn die Call-Log-Analyse (Aufgabe 01) zeigt, dass Agenten die
empfohlenen Ketten **nicht** von selbst laufen, oder wenn ein konkreter Ziel-Host Prompts
unterstützt. Sonst: in `90_bewusst-nicht-umsetzen.md` als verworfen nachtragen.

## Teil 3: Loading-Zustand präzisieren / Progress-Notification — Messung abwarten

**Verifiziert:** Während des Hintergrund-Loads antwortet jeder Tool-Call mit
`McpToolResults.Loading()` ("Bitte in wenigen Sekunden erneut versuchen"). Der Client muss
pollen. Wie oft das real passiert, misst die Log-Analyse aus Aufgabe 01
(Loading-Retry-Bursts).

**Idee:** Bei belegtem Bedarf SDK-seitig Progress-Notifications prüfen; alternativ minimal
eingreifen und nur den Text präzisieren (konkrete Zusage statt vager Formulierung).

**Bedingung:** Erst Messung, dann SDK-Fähigkeitscheck. Bei seltenen Loading-Fällen genügt
der Minimaleingriff.

## Teil 4: Multi-Solution-Unterstützung — zurückgestellt (hohe Beweislast)

**Heutiger Vertrag:** Eine Solution pro Prozess (`--solution` beim Start, resident).
Cross-Repo-Agenten starten mehrere Server-Instanzen — das ist ein sauberes, dokumentiertes
Muster und hält jede Instanz einfach (ein `_lock`, ein Catalog, ein Refresh-Zyklus).

**Bedingung:** Nur wenn Nutzungsdaten zeigen, dass Multi-Server-Setup in der Praxis
scheitert (Host-Limits, Konfigurationshürden). Bis dahin: nicht umsetzen.

## Definition of Done (je umgesetztem Teil)

- Teil 1: Resource registriert, Paritätstest gegen aktive Config, Doc-Sync in
  `Docs/agent-api.md`; `dotnet build` + beide Nicht-Stress-Testprojekte grün.
- Teil 2/3: jeweils vorher Evidenz aus Aufgabe 01 dokumentiert und hier verlinkt.
