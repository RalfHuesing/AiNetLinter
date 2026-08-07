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
  Kritiker-Ebenen, Tech-Debt-Kanal, Korrektur-Step-Mechanik, Git-Strategie,
  Loop-Guard, Edge-Cases
- **`skills/`** — Rollen-Definitionen für Planer (zwei Modi:
  Roadmap/Step), Coder, Kritiker
- **`templates/`** — Ziel-Struktur der Dateien in `<task-dir>/`:
  `roadmap.md`, `codemap.md`, `tech-debt.md`, `step-plan.md`,
  `step-result.md`, `step-review.md`, `task-state.md`, `task-summary.md`

## Output

`<task-dir>/task-summary.md` + `<task-dir>/tech-debt.md` (gesammelte,
bewusst nicht gefixte Architektur-Beobachtungen), plus mehrere Commits im
Zielprojekt — siehe `spec.md` §10.3 und §12.

## Vorgeschichte: `dynamic-loop`-Experiment beendet

Es gab zeitweise einen zweiten Umsetzungs-Workflow, `dynamic-loop/`: ein
schlankerer Gegenentwurf (kurzer Regel-Kern aus harten Regeln + benannten
Gefahren, Wie bleibt dem Modell-Urteil überlassen), inspiriert von
[Claude-of-Duty](https://github.com/mshumer/Claude-of-Duty) — siehe
`docs/references.md`, Abschnitt 2026-08-01. Ein realer Task
(`codegraph-mcp-server`-Umsetzung in einem Fremdprojekt, über 11
Einheiten) zeigte zwei Reibungspunkte: Einheiten wurden teils kleinteiliger
geschnitten, als der Fixkosten-Sockel pro Rollenwechsel (Kontext neu
laden, Testlauf) rechtfertigt, und der Kernel machte keine verbindliche
Aussage zur Test-Kadenz (voller Testlauf lief in der Praxis fast jede
Einheit, statt gezielt während der Arbeit + einmal als Gate).

Beides löst `drift-loop` bereits strukturell: `spec.md` §10.6
(Micro-Batches) bündelt triviale Low-Risk-Einzeländerungen **innerhalb
eines Epics** zu einem Schritt statt für jede einen eigenen
Planer→Coder→Kritiker-Zyklus zu starten, mit konkreten, konfigurierbaren
Deckeln (`max_batch_items`, `max_batch_diff_lines`) statt reinem
Modell-Urteil. Die fehlende Test-Kadenz-Regel wurde ergänzt (siehe
`skills/coder/SKILL.md` Schritt 4). Zusätzlich zeigte sich beim
Vergleich: der oft geäußerte Verdacht, `spec.md` (~700 Zeilen) sei als
Prompt zu lang, trägt nicht — Subagenten laden pro Aufruf nur ihre
`skills/<rolle>/SKILL.md` (~150-270 Zeilen), `spec.md` wird darin nur per
Abschnittsnummer referenziert, nicht vollständig mitgeladen.

`dynamic-loop/` wurde daraufhin entfernt (Git-Historie bleibt abrufbar) —
zwei parallele Umsetzungs-Workflows für dieselbe Aufgabe waren mehr
Verwirrung als Nutzen, und der schlankere Ansatz hat sein eigenes
Kern-Experiment ("reicht ein kurzer Kernel?") in der Praxis nicht
bestätigt.
