---
status: executing
task: find-dead-code
started_at: 2026-08-17T17:16:00+02:00
last_updated: 2026-08-17T17:16:00+02:00
rules_dir: .agents/rules
total_steps: 4
current_step: 004
---

# Task State: find-dead-code

## Übersicht

- **Task-Status:** `completed`
- **Steps gesamt:** 4 (regulär + Korrekturen — weicher Check-in bei jedem Vielfachen von `soft_step_checkin_interval`, siehe Config)
- **Aktueller Schritt:** `004`
- **Roadmap:** siehe `roadmap.md` für den Epic-Fortschritt
- **Tech-Debt:** siehe `tech-debt.md` für gesammelte, bewusst nicht gefixte Funde
- **Gestartet:** 2026-08-17T17:16:00+02:00
- **Zuletzt aktualisiert:** 2026-08-17T17:46:00+02:00

## Steps

| Step | Epic | Status | Title | Corrects | Coded | Reviewed | Commit |
|------|------|--------|-------|----------|-------|----------|--------|
| 001 | EPIC-01 | done | Core-Scanner, Datenmodelle & Scope-Bounding-Pipeline | - | ja | approved | `06d49fc` |
| 002 | EPIC-02 | done | Diagnosen & Locals-Erkennung (Mode: locals & both) | - | ja | approved | `6189330` |
| 003 | EPIC-03 | done | MCP-Tool-Wrapper, Registrierung & Server-Instructions | - | ja | approved | `669064c` |
| 004 | EPIC-04 | done | Erweiterte Testsuite & Live-Dogfooding-Verifikation | - | ja | approved | `695246d` |

## Config (optional)

```
max_fix_rounds_per_step: 3
soft_step_checkin_interval: 40
max_batch_items: 8
max_batch_diff_lines: 40
build_command: dotnet build
test_command: dotnet test src/AiNetLinter.FastTests --filter Category!=Stress && dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress
target_branch: main
model_planer: 
model_coder: 
model_kritiker: 
```

## Abbruch-/Pause-Bedingungen

- **Kettenbudget erreicht** (`max_fix_rounds_per_step`, Default 3, über die `corrects`-Kette gezählt, ohne `approved`): der zuletzt korrigierte Step → `blocked`, Loop pausiert für diese Kette, Nutzer klärt. **Kein** Task-Abbruch dadurch.
- **Weicher Deckel erreicht** (`soft_step_checkin_interval`, Default 40, bei jedem Vielfachen der Gesamt-Step-Zahl): Zwischenfrage an den Nutzer, kein automatischer Abbruch. Nur eine ausdrückliche Ablehnung → Task `aborted`, siehe `task-summary.md`.
- **Blocker aufgetreten** (Step mit Status `blocked`): Loop pausiert, Nutzer klärt.
- **Tech-Debt-Einträge lösen NIE einen Abbruch oder Blocker aus** — sie sind reine Beobachtung, kein Steuerungssignal (siehe `spec.md` §9). Auch `auto_fixable: ja`-Einträge lösen nichts eigenständig aus, sie werden nur an ohnehin laufende Steps angehängt.
