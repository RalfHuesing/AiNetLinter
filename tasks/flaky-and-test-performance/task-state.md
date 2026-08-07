---
status: executing
task: flaky-and-test-performance
started_at: 2026-08-07T08:55:00+02:00
last_updated: 2026-08-07T11:00:00+02:00
rules_dir: .agents/rules  # aus konzept.md Frontmatter übernommen
total_fix_rounds: 0
current_step: step-002
---

# Task State: flaky-and-test-performance

## Übersicht

- **Task-Status:** `executing`
- **Fix-Runden gesamt:** 0 (Not-Anker bei `max_total_fix_rounds`, siehe Config)
- **Aktueller Schritt:** `step-001` (noch nicht geplant — folgt nach Roadmap-Modus)
- **Roadmap:** wird im ersten Schritt vom Planer im Roadmap-Modus erzeugt
- **Tech-Debt:** siehe `tech-debt.md` (leer angelegt)
- **Gestartet:** 2026-08-07
- **Zuletzt aktualisiert:** 2026-08-07

## Steps

<Wächst mit jedem Planer-Aufruf im Step-Modus um genau eine Zeile.>

| Step | Epic | Status | Title | Fix-Runden | Coded | Reviewed | Commit |
|------|------|--------|-------|------------|-------|----------|--------|
| step-001 | EPIC-01 | done | Spike — SymbolGraphMcpFixture auf ICollectionFixture umstellen, Vorher/Nachher messen | 0/3 | MiniMax-M3 | MiniMax-M3 | bf5de7e / cc395d0 |
| step-002 | EPIC-02 | in_progress | Category-Traits für alle Tests in src/AiNetLinter.Tests/Suppression/ nachziehen (Batch 1 von N) | 0/3 | - | - | - |

## Config

Defaults aus `../spec.md` (kein `config.md` vorhanden):

```
max_fix_rounds_per_step: 3
max_total_fix_rounds: 12
max_batch_items: 8
max_batch_diff_lines: 40
build_command: dotnet build
test_command: dotnet test
target_branch: main
model_planer: nicht festgelegt
model_coder: nicht festgelegt (Hinweis: Coder ohne Thinking-Mode)
model_kritiker: nicht festgelegt
```

## Abbruch-Bedingungen

- Fix-Budget eines Steps erreicht → dieser Step `blocked`.
- Task-weiter Not-Anker (`max_total_fix_rounds = 12`) erreicht → Task `aborted`.
- Step mit Status `blocked` → Loop pausiert, Nutzer klärt.
- Tech-Debt-Einträge lösen NIE einen Abbruch oder Blocker aus.
