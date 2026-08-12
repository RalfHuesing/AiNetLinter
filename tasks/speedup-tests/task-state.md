---
status: executing  # executing | done | aborted
task: speedup-tests
started_at: 2026-08-12
last_updated: 2026-08-12
rules_dir: .agents/rules
total_steps: 2  # Summe aller Steps inkl. Korrekturen — Basis für den weichen Deckel (siehe Config, ../spec.md §10.5)
current_step: step-002
---

# Task State: speedup-tests

## Übersicht

- **Task-Status:** `executing`
- **Steps gesamt:** 0 (regulär + Korrekturen — weicher Check-in bei
  jedem Vielfachen von `soft_step_checkin_interval`, siehe Config)
- **Aktueller Schritt:** `step-002`
- **Roadmap:** siehe `roadmap.md` für den Epic-Fortschritt (noch nicht erzeugt)
- **Tech-Debt:** siehe `tech-debt.md` für gesammelte, bewusst nicht gefixte Funde
- **Gestartet:** 2026-08-12
- **Zuletzt aktualisiert:** 2026-08-12

## Steps

<Diese Tabelle wächst mit jedem Planer-Aufruf (oder Orchestrator-
Transkript bei eindeutigen Korrekturen, siehe `../spec.md` §6.2.1) um
genau eine Zeile. Die Spalte „Corrects" bleibt bei regulären Steps leer,
bei Korrekturen steht dort der Step, den sie korrigieren — daraus ergibt
sich die Kettenlänge fürs Fix-Budget (§10.5), keine separate Zählung
mehr nötig.>

| Step | Epic | Status | Title | Corrects | Coded | Reviewed | Commit |
|------|------|--------|-------|----------|-------|----------|--------|
| step-001 | EPIC-1 | done | Fundament: Zielprojekte + TestProject.props | - | b1fe9eb | approved | 9d20376 |
| step-002 | EPIC-1 | done (Korrektur ausstehend) | Fundament: Migrationsledger, Guards, Baseline-Messung | - | cd1c80f | issues | c5d4b10 |

## Config (optional)

Falls `<task-dir>/config.md` existiert, hier die Overrides dokumentieren.
Andernfalls gelten die Defaults aus `../spec.md`.

```
max_fix_rounds_per_step: 3        # Kettenlänge über `corrects`, siehe ../spec.md §10.5
soft_step_checkin_interval: 40    # weicher Deckel, kein Hard-Abort — siehe ../spec.md §10.5
max_batch_items: 8          # siehe ../spec.md §10.6 (Micro-Batches innerhalb eines Epics)
max_batch_diff_lines: 40    # siehe ../spec.md §10.6
build_command: <aus roadmap.md Tech-Stack-Notiz>
test_command: <aus roadmap.md Tech-Stack-Notiz>
target_branch: <aktueller Branch, nicht hartcodiert>
model_planer: Sonnet 5, Stufe High
model_coder: Sonnet 5, Stufe Medium
model_kritiker: Sonnet 5, Stufe Medium
```

<Die drei `model_*`-Felder sind optional und halten eine vom Nutzer
genannte, rollenabhängige Modellwahl fest (typisch: günstigeres Modell
für den Coder, stärkeres für Planer/Kritiker). Werte sind freier Text —
der Workflow validiert sie nie. Sie stehen hier statt nur im Start-Prompt,
weil ein Task in einer **neuen Session** fortgesetzt werden kann
(`../orchestrator.md` Schritt 1, Fall B läuft ohne Rückfrage weiter) —
sonst liefen die Subagenten nach einem Resume still auf dem
Default-Modell. Nicht gesetzt = keine Vorgabe, der Orchestrator fragt
auch nicht nach. Siehe `../spec.md` §10.8.>

## Abbruch-/Pause-Bedingungen

- **Kettenbudget erreicht** (`max_fix_rounds_per_step`, Default 3, über
  die `corrects`-Kette gezählt, ohne `approved`): der zuletzt korrigierte
  Step → `blocked`, Loop pausiert für diese Kette, Nutzer klärt. **Kein**
  Task-Abbruch dadurch.
- **Weicher Deckel erreicht** (`soft_step_checkin_interval`, Default 40,
  bei jedem Vielfachen der Gesamt-Step-Zahl): Zwischenfrage an den
  Nutzer, kein automatischer Abbruch. Nur eine ausdrückliche Ablehnung →
  Task `aborted`, siehe `task-summary.md`.
- **Blocker aufgetreten** (Step mit Status `blocked`): Loop pausiert,
  Nutzer klärt.
- **Tech-Debt-Einträge lösen NIE einen Abbruch oder Blocker aus** — sie
  sind reine Beobachtung, kein Steuerungssignal (siehe `../spec.md` §9).
  Auch `auto_fixable: ja`-Einträge lösen nichts eigenständig aus, sie
  werden nur an ohnehin laufende Steps angehängt.
</content>
</invoke>
