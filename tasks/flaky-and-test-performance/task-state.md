---
status: executing
task: flaky-and-test-performance
started_at: 2026-08-07T08:55:00+02:00
last_updated: 2026-08-07T13:15:00+02:00
rules_dir: .agents/rules  # aus konzept.md Frontmatter übernommen
total_steps: 6  # Summe aller Steps inkl. Korrekturen — Basis für den weichen Deckel (siehe ../spec.md §10.5); +1 für step-006 (in Planung/Implementierung)
current_step: step-006  # step-006 ist in Umsetzung
---

# Task State: flaky-and-test-performance

## Übersicht

- **Task-Status:** `executing`
- **Steps gesamt:** 6 (regulär + Korrekturen — weicher Check-in bei
  jedem Vielfachen von `soft_step_checkin_interval`, siehe Config)
- **Aktueller Schritt:** `step-006` (in Planung/Implementierung; fünfter
  EPIC-02-Batch — `Evals/`-Tests, 3 Klassen; ListEvalsCommandTests-Subprozess-
  Hypothese aus codemap.md step-002 durch JIT-Prüfung widerlegt → alle Unit)
- **Roadmap:** siehe `roadmap.md` (EPIC-01 abgehakt, EPIC-02 in Arbeit,
  übrige Epics offen)
- **Tech-Debt:** siehe `tech-debt.md` (2 Einträge: TD-001 mittel,
  TD-002 niedrig)
- **Gestartet:** 2026-08-07
- **Zuletzt aktualisiert:** 2026-08-07 (Schema-Update auf neues
  `total_steps`-/`Corrects`-Layout aus `drift-loop/spec.md` §10.1+§5; alle
  bisherigen Schritte als `done`/`approved`/`0/3` Fix-Runden übernommen)

## Steps

| Step | Epic | Status | Title | Corrects | Coded | Reviewed | Commit |
|------|------|--------|-------|----------|-------|----------|--------|
| step-001 | EPIC-01 | done | Spike — SymbolGraphMcpFixture auf ICollectionFixture umstellen, Vorher/Nachher messen | - | MiniMax-M3 | MiniMax-M3 | bf5de7e / cc395d0 |
| step-002 | EPIC-02 | done | Category-Traits für alle Tests in src/AiNetLinter.Tests/Suppression/ nachziehen (Batch 1 von N) | - | MiniMax-M3 | MiniMax-M3 | 3ae94c2 / 79d3d6d |
| step-003 | EPIC-02 | done | Category-Traits für src/AiNetLinter.Tests/Metrics/ nachziehen (Batch 2 von N) | - | MiniMax-M3 | MiniMax-M3 | 67fb86b / 03b04f4 |
| step-004 | EPIC-02 | done | Category-Traits für src/AiNetLinter.Tests/Web/ nachziehen (Batch 3 von N) | - | MiniMax-M3 | MiniMax-M3 | 57f7f03 / ecd9dfa |
| step-005 | EPIC-02 | done | Category-Traits für Arch/Diag/FalsePositives/Cache nachziehen (Batch 4 von N) | - | MiniMax-M3 | MiniMax-M3 | b15a198 / fe95a08 |
| step-006 | EPIC-02 | in_progress | Category-Traits für src/AiNetLinter.Tests/Evals/ nachziehen (Batch 5 von N) | - | - | - | - |

## Config

Defaults aus `../../.agents/Agent-Scaffolding/dev-loop/drift-loop/spec.md`
(kein `config.md` vorhanden):

```
max_fix_rounds_per_step: 3
soft_step_checkin_interval: 40
max_batch_items: 8
max_batch_diff_lines: 40
build_command: dotnet build
test_command: dotnet test
target_branch: main
model_planer: nicht festgelegt
model_coder: nicht festgelegt (Hinweis: Coder ohne Thinking-Mode)
model_kritiker: nicht festgelegt
```

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
