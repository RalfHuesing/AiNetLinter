# task-loop

Autonomer Plan → Code → Audit-Loop. Ein Orchestrator (die aufrufende
Session) startet nacheinander drei Subagenten-Rollen — Planer, Coder,
Auditer — die eine bestehende Aufgaben-Doku ohne weitere Rückfragen
abarbeiten, prüfen und ggf. nachbessern.

## Wann benutzen

Du hast bereits eine solide Aufgaben-Doku (Ziel, Kontext, Scope,
Definition of Done — siehe [`spec.md`](spec.md) §6 für die exakten
Mindestanforderungen) und willst sie unbeaufsichtigt umsetzen lassen.
Fehlt dir das noch: erst [`../planning/`](../planning/README.md) nutzen.

## Wie starten

```
<pfad-zu-dev-loop>/task-loop/orchestrator.md <task-dir>
```

Läuft automatisch weiter, wenn `<task-dir>/task-state.md` schon
existiert (Resume nach Unterbrechung).

## Enthält

- **`orchestrator.md`** — die ausführbare Orchestrator-Anleitung (das,
  was du tatsächlich aufrufst)
- **`spec.md`** — die volle Spezifikation dahinter: Phasen, Rollen,
  Fix-Step-Mechanik, Micro-Batches für triviale Low-Risk-Änderungen
  (§7.7), Git-Strategie, Loop-Guard, Edge-Cases
- **`skills/`** — Rollen-Definitionen für Planer, Coder, Auditer
  (jeweils `SKILL.md`), vom Orchestrator komplett in den jeweiligen
  Subagenten-Prompt eingebettet
- **`templates/`** — Ziel-Struktur der Dateien, die in `<task-dir>/`
  entstehen (`step-plan.md`, `step-result.md`, `step-review.md`,
  `task-state.md`, `task-summary.md`)

## Output

`<task-dir>/task-summary.md`, plus mehrere Commits im Zielprojekt (Code,
Tests, Task-Doku) — siehe `spec.md` §7.3 und §9.
