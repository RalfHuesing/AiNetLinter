---
status: done  # executing | done | aborted
task: verbesserungen-mcp
started_at: 2026-08-05
last_updated: 2026-08-05
completed_at: 2026-08-05
rules_dir: .agents/rules
total_fix_rounds: 1  # Summe aller Fix-Runden über alle Steps (Task-weiter Not-Anker, siehe Config)
current_step: step-004
---

# Task State: verbesserungen-mcp

## Übersicht

- **Task-Status:** `done` — globaler Kritiker hat `done` verdictiert, alle Muss-Haben-Punkte aus `konzept.md` addressiert, keine ausstehenden Fixes
- **Fix-Runden gesamt:** 1 (Not-Anker bei `max_total_fix_rounds`, siehe Config)
- **Aktueller Schritt:** `step-004` (EPIC-03 abgeschlossen, Abschluss-Check durchgeführt)
- **Roadmap:** siehe `roadmap.md` (Status `done`, alle drei Epics `[x]`)
- **Task-Summary:** siehe `task-summary.md` (Pflicht-Abschluss-Doku)
- **Tech-Debt:** siehe `tech-debt.md` für gesammelte, bewusst nicht gefixte Funde (5 Einträge, alle `offen`)
- **Gestartet:** 2026-08-05
- **Abgeschlossen:** 2026-08-05

## Steps

<Diese Tabelle wächst mit jedem Planer-Aufruf im Step-Modus um genau
eine Zeile.>

| Step | Epic | Status | Title | Fix-Runden | Coded | Reviewed | Commit |
|------|------|--------|-------|------------|-------|----------|--------|
| step-001 | EPIC-01 | done | Blazor-Partial-Fixture anlegen und Symbolgraph-Diskrepanz reproduzieren | 0/3 | fbc399f | approved | fbc399f/ea60a32 |
| step-002 | EPIC-01 | done | Roslyn-Paket-Versions-Bump: Razor-Source-Generator-Integration tatsächlich zum Laufen bringen | 1/3 | 7f4d6ba | approved | 7f4d6ba/a14b3cd |
| step-002/fix-01 | EPIC-01 | done | SkeletonSyntaxWalker: semantischen Fallback für Basistyp bei fehlender Basisliste ergänzen | - | c614348 | approved | c614348/097dcea |
| step-003 | EPIC-02 | done | Einheitlicher Symbol-Identifikator-Parser | 0/3 | 48d596c | approved | 48d596c/033f61e |
| step-004 | EPIC-03 | done | EPIC-03-Batch: get_symbol_body-ID-Korruption, get_violations-Meldung, ainetlinter://overview-Status-Race, depth-Hard-Cap-Doku | 0/3 | e1d0124 | approved | e1d0124/ed0e878 |

## Config (optional)

```
max_fix_rounds_per_step: 3
max_total_fix_rounds: 12
max_batch_items: 8          # siehe ../spec.md §10.6 (Micro-Batches innerhalb eines Epics)
max_batch_diff_lines: 40    # siehe ../spec.md §10.6
build_command: <aus roadmap.md Tech-Stack-Notiz>
test_command: <aus roadmap.md Tech-Stack-Notiz>
target_branch: main
model_planer: Sonnet 5, Stufe High
model_coder: Sonnet 5, Stufe Medium
model_kritiker: Sonnet 5, Stufe Medium
```

Zusätzliche Nutzer-Vorgabe (Prompt-Kontext für Planer, gilt für den
ganzen Task): **Steps in größeren Brocken planen** — der Nutzer will die
Subagent-Kette nicht für Mini-/Micro-Änderungen aufrufen. Der Planer soll
den Micro-Batch-Mechanismus (`step_type: batch`, `../spec.md` §10.6)
großzügig nutzen und Epics/Steps eher grob als fein schneiden, innerhalb
der bestehenden Leitplanken (Commit-, Review- und Risiko-Grenzen).

## Abbruch-Bedingungen

- **Fix-Budget eines Steps erreicht** (`max_fix_rounds_per_step`, Default
  3, ohne `approved`): dieser eine Step → `blocked`, Loop pausiert,
  Nutzer klärt.
- **Task-weiter Not-Anker erreicht** (`max_total_fix_rounds`, Default 12,
  über alle Steps summiert): Task → `aborted`, siehe `task-summary.md`.
- **Blocker aufgetreten** (Step mit Status `blocked`): Loop pausiert,
  Nutzer klärt.
- **Tech-Debt-Einträge lösen NIE einen Abbruch oder Blocker aus** — sie
  sind reine Beobachtung, kein Steuerungssignal (siehe `../spec.md` §9).
