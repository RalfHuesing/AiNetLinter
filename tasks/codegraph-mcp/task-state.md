---
status: executing  # executing | done | aborted
task: codegraph-mcp
started_at: 2026-07-31T09:30:00Z
last_updated: 2026-07-31T09:30:00Z
rules_dir: .agents/rules
total_fix_rounds: 1  # Summe aller Fix-Runden über alle Steps (Task-weiter Not-Anker, siehe Config)
current_step: step-005
---

# Task State: codegraph-mcp

## Übersicht

- **Task-Status:** `executing`
- **Fix-Runden gesamt:** 0 (Not-Anker bei `max_total_fix_rounds`, siehe Config)
- **Aktueller Schritt:** `step-001`
- **Roadmap:** siehe `roadmap.md` für den Epic-Fortschritt
- **Tech-Debt:** siehe `tech-debt.md` für gesammelte, bewusst nicht gefixte Funde
- **Gestartet:** 2026-07-31T09:30:00Z
- **Zuletzt aktualisiert:** 2026-07-31T09:30:00Z

## Steps

<Diese Tabelle wächst mit jedem Planer-Aufruf im Step-Modus um genau
eine Zeile.>

| Step | Epic | Status | Title | Fix-Runden | Coded | Reviewed | Commit |
|------|------|--------|-------|------------|-------|----------|--------|
| step-001 | EPIC-01 | done | CLI-Einstiegspunkt --mcp-server + minimaler stdio-MCP-Server | 0/3 | yes | approved | 3ae6230 |
| step-002 | EPIC-02 | done | Resident McpCodeGraphServer + Lazy-Staleness-Invalidierung | 0/3 | yes | approved | 81cf007 |
| step-003 | EPIC-03 | done | Tool-Infrastruktur + erstes Tool find_symbol (fix-01: solution-not-loaded coverage) | 1/3 | yes | approved | 9d6cecc |
| step-004 | EPIC-03 | done | Zweites Tool find_references | 0/3 | yes | approved | a9e91ed |
| step-005 | EPIC-03 | in_progress | Drittes Tool get_impact | 0/3 | - | - | - |

## Config (optional)

Falls `<task-dir>/config.md` existiert, hier die Overrides dokumentieren.
Andernfalls gelten die Defaults aus `../spec.md`.

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

<Die drei `model_*`-Felder sind optional und halten eine vom Nutzer
genannte, rollenabhängige Modellwahl fest (typisch: günstigeres Modell
für den Coder, stärkeres für Planer/Kritiker). Werte sind freier Text —
der Workflow validiert sie nie. Sie stehen hier statt nur im Start-Prompt,
weil ein Task in einer **neuen Session** fortgesetzt werden kann
(`../orchestrator.md` Schritt 1, Fall B läuft ohne Rückfrage weiter) —
sonst liefen die Subagenten nach einem Resume still auf dem
Default-Modell. Nicht gesetzt = keine Vorgabe, der Orchestrator fragt
auch nicht nach. Siehe `../spec.md` §10.8.>

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
