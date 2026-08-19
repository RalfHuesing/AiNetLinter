---
status: executing
task: markdown-builder
started_at: 2026-08-19
last_updated: 2026-08-19
rules_dir: .agents/rules
total_steps: 4
current_step: step-004
---

# Task State: markdown-builder

## Übersicht

- **Task-Status:** `executing`
- **Steps gesamt:** 4 (regulär + Korrekturen — weicher Check-in bei
  jedem Vielfachen von `soft_step_checkin_interval`, siehe Config)
- **Aktueller Schritt:** `step-004` (in_progress)
- **Roadmap:** siehe `roadmap.md` für den Epic-Fortschritt
- **Tech-Debt:** siehe `tech-debt.md` für gesammelte, bewusst nicht gefixte Funde
- **Gestartet:** 2026-08-19
- **Zuletzt aktualisiert:** 2026-08-19

## Steps

| Step | Epic | Status | Title | Corrects | Coded | Reviewed | Commit |
|------|------|--------|-------|----------|-------|----------|--------|
| step-001 | EPIC-01 | done | MarkdownBuilder-Foundation + Bug-Fix-Callsites umstellen | - | fc603681 | step-001/step-review.md: issues | - |
| step-002 | EPIC-01 | done | MarkdownTableBuilder zeilenweise API + EPIC-01 DoD präzisieren | step-001 | b1a39ab1 | step-002/step-review.md: approved | - |
| step-003 | EPIC-02 | done | EPIC-02 Welle 1 — HotspotSectionFormatter löschen + ListRulesCommand + GetSymbolBodyTool | - | 107b2682 | step-003/step-review.md: approved | - |
| step-004 | EPIC-02 | in_progress | EPIC-02 Welle 2 — drei Generators-Callsites (Prio 5/7/8) auf MarkdownBuilder umstellen | - | - | - | - |
|------|------|--------|-------|----------|-------|----------|--------|

## Config (optional)

```
max_fix_rounds_per_step: 3
soft_step_checkin_interval: 40
max_batch_items: 8
max_batch_diff_lines: 40
build_command: dotnet build
test_command_fast: dotnet test src/AiNetLinter.FastTests --filter Category!=Stress
test_command_integration: dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress
target_branch: main
model_planer: <nicht festgelegt>
model_coder: <nicht festgelegt>
model_kritiker: <nicht festgelegt>
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
  sind reine Beobachtung, kein Steuerungssignal.
