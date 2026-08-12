---
status: executing  # executing | done | aborted
task: speedup-tests
started_at: 2026-08-12
last_updated: 2026-08-12
rules_dir: .agents/rules
total_steps: 11  # Summe aller Steps inkl. Korrekturen — Basis für den weichen Deckel (siehe Config, ../spec.md §10.5)
current_step: step-011
---

# Task State: speedup-tests

## Übersicht

- **Task-Status:** `executing`
- **Steps gesamt:** 11 (regulär + Korrekturen — weicher Check-in bei
  jedem Vielfachen von `soft_step_checkin_interval`, siehe Config)
- **Aktueller Schritt:** `step-011`
- **Roadmap:** siehe `roadmap.md` für den Epic-Fortschritt
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
| step-002 | EPIC-1 | done | Fundament: Migrationsledger, Guards, Baseline-Messung | - | cd1c80f | issues→approved (via step-003) | c5d4b10 |
| step-003 | EPIC-1 | done | Korrektur: Nachweis Ledger-Guard | step-002 | c16be1a | approved | c16be1a |
| step-004 | EPIC-1 | done | Fundament: IVT, Safety Envelope, Legacy-Gate-Switch | - | a303edb | issues→approved (via step-005) | 59dcff9 |
| step-005 | EPIC-1 | done | Korrektur: AiNetLinterRichtlinien.mdc §4 | step-004 | bffe3e3 | approved | 2c9611c |
| step-006 | EPIC-2 | done | Testplattform: RoslynTestSolutionFactory + PreparedSolutionFixture | - | f258992 | approved | 45322c3 |
| step-007 | EPIC-2 | done | Testplattform: IsolatedFixtureLease + MsBuildFixtureHost | - | b2ebfbb | approved | 45361b5 |
| step-008 | EPIC-2 | done | Testplattform: FilterMini-Fixture (Disk + In-Memory) | - | 968c35a | issues→approved (via step-009) | 243f2db |
| step-009 | EPIC-2 | done | Korrektur: FilterMiniFidelityTests IsTestProject | step-008 | 1d64b47 | approved | 296447f |
| step-010 | EPIC-3 | done | Checkers-Kohorte -> Unit (28 Klassen) | - | 8c1552f | approved | 9245277 |
| step-011 | EPIC-3 | done | Web-Parser-Kohorte -> Unit (5 Klassen) | - | b720e1b | approved | 317f90c |

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
model_planer: GPT-5.6 Sol, Stufe Medium
model_coder: GPT-5.6 Luna, Stufe High
model_kritiker: GPT-5.6 Terra, Stufe Medium
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
