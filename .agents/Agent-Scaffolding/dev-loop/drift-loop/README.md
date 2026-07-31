# drift-loop

Autonomer Plan → Code → Kritik-Loop mit **Just-in-Time-Planung**: Der
Planer plant immer nur den **nächsten** Step — mit dem tatsächlichen,
aktuellen Projektzustand als Kontext statt einer Prognose von vor dem
ersten Commit. Ein grobes `roadmap.md` (Epics, keine Detail-Steps) hält
fest, was insgesamt noch zu tun ist.

## Wann benutzen

Du hast eine solide `konzept.md` (siehe `../planning/`), und willst die
Aufgabe Schritt für Schritt autonom umsetzen lassen.

## Wie starten

```
<pfad-zu-dev-loop>/drift-loop/orchestrator.md <task-dir>
```

`<task-dir>` muss bereits eine `konzept.md` mit `status: ready` enthalten
(siehe `../planning/README.md`, falls das noch fehlt). Läuft automatisch
weiter, wenn `<task-dir>/task-state.md` schon existiert.

## Enthält

- **`orchestrator.md`** — die ausführbare Orchestrator-Anleitung
- **`spec.md`** — die volle Spezifikation: Rollen, Roadmap-Mechanik,
  Kritiker-Ebenen, Tech-Debt-Kanal, Fix-Step-Mechanik, Git-Strategie,
  Loop-Guard, Edge-Cases
- **`skills/`** — Rollen-Definitionen für Planer (zwei Modi:
  Roadmap/Step), Coder, Kritiker
- **`templates/`** — Ziel-Struktur der Dateien in `<task-dir>/`:
  `roadmap.md`, `tech-debt.md`, `step-plan.md`, `step-result.md`,
  `step-review.md`, `task-state.md`, `task-summary.md`

## Output

`<task-dir>/task-summary.md` + `<task-dir>/tech-debt.md` (gesammelte,
bewusst nicht gefixte Architektur-Beobachtungen), plus mehrere Commits im
Zielprojekt — siehe `spec.md` §10.3 und §12.
