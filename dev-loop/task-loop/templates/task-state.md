---
status: executing  # executing | done | aborted
task: <TASK-NAME>
started_at: <ISO-8601>
last_updated: <ISO-8601>
rules_dir: <.agents/rules | .cursor/rules | <custom-pfad> | keins>  # einmalig erkannt (siehe ../spec.md §3.1), von konzept.md übernommen falls vorhanden
total_fix_rounds: 0  # Summe aller Fix-Runden über alle Steps (Task-weiter Not-Anker, siehe Config)
current_step: step-001
---

# Task State: <TASK-NAME>

## Übersicht

- **Task-Status:** `executing`
- **Fix-Runden gesamt:** 0 (Not-Anker bei `max_total_fix_rounds`, siehe Config)
- **Aktueller Schritt:** `step-001`
- **Gestartet:** <ISO-8601>
- **Zuletzt aktualisiert:** <ISO-8601>

## Steps

| Step | Status | Title | Fix-Runden | Coded | Reviewed | Commit |
|------|--------|-------|------------|-------|----------|--------|
| step-001 | open | <Titel> | 0/3 | - | - | - |
| step-002 | open | <Titel> | 0/3 | - | - | - |
| ... | ... | ... | ... | ... | ... | ... |

<Wird vom Orchestrator gepflegt. Status pro Step: open / in_progress /
done / done (fix-XX pending) / blocked. „Fix-Runden" = Anzahl vorhandener
`fix-XX`-Unterordner / `max_fix_rounds_per_step` (Default 3/3). Bei
Batch-Steps (`step_type: batch`, siehe ../spec.md §7.7) im Titel optional
die Item-Zahl vermerken, z. B. „Micro-Batch: 6 Doku-Korrekturen" — das
Fix-Budget gilt trotzdem pro Step, nicht pro Item.>

## History

<Append-only Log. Ein Eintrag pro Status-Wechsel oder signifikantem Event.
Format: `- <ISO-8601> — <Was passiert ist>`.>

- <ISO-8601> — Task angelegt
- <ISO-8601> — Planer hat N Steps generiert (Pfade: …), Commit `<SHA>`
- <ISO-8601> — step-001: open → in_progress (coder-Aufruf gestartet)
- <ISO-8601> — step-001: in_progress → done (pending audit), Code-Commit
  `<SHA>`, Doku-Commit `<SHA>`
- <ISO-8601> — step-001: auditer-Verdict `approved`, Commit `<SHA>`
- <ISO-8601> — step-002: open → in_progress
- ...
- <ISO-8601> — step-004: auditer-Verdict `issues` → fix-01 angelegt, Commit `<SHA>`
- ...

## Config (optional)

Falls `<task-dir>/config.md` existiert, hier die Overrides dokumentieren.
Andernfalls gelten die Defaults aus `../spec.md`.

```
max_fix_rounds_per_step: 3
max_total_fix_rounds: 12
max_batch_items: 8          # siehe ../spec.md §7.7 (Micro-Batches)
max_batch_diff_lines: 40    # siehe ../spec.md §7.7 (Micro-Batches)
build_command: <aus Tech-Stack-Notiz>
test_command: <aus Tech-Stack-Notiz>
target_branch: <aktueller Branch, nicht hartcodiert>
```

## Abbruch-Bedingungen

- **Fix-Budget eines Steps erreicht** (`max_fix_rounds_per_step`, Default
  3, ohne `approved`): dieser eine Step → `blocked`, Loop pausiert,
  Nutzer klärt. Andere, unabhängige Steps sind davon nicht betroffen —
  ein Blocker in einem Step ist kein Alarmsignal für den ganzen Task.
- **Task-weiter Not-Anker erreicht** (`max_total_fix_rounds`, Default 12,
  über alle Steps summiert): Task → `aborted`, siehe `task-summary.md`.
- **Blocker aufgetreten** (Step mit Status `blocked`): Loop pausiert,
  Nutzer klärt. Gilt unabhängig von der Blocker-Ursache identisch — auch
  ein infrastruktur-/tooling-bedingter Blocker (z. B. Dienst nicht
  erreichbar, Tool fehlt) erzeugt keinen Fix-Step und zählt **nicht**
  gegen das Fix-Budget (siehe `../skills/coder/SKILL.md` Schritt 4a).
