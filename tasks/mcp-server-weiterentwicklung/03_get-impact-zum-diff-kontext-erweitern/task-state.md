---
status: paused
task: 03_get-impact-zum-diff-kontext-erweitern
started_at: 2026-08-22
last_updated: 2026-08-23
rules_dir: .agents/rules
total_steps: 6
current_step: step-006
---

# Task State: 03_get-impact-zum-diff-kontext-erweitern

## Übersicht

- **Task-Status:** `paused` (Nutzer pausiert — Fortsetzung am 2026-08-23)
- **Steps gesamt:** 6 (regulär + Korrekturen — weicher Check-in bei
  jedem Vielfachen von `soft_step_checkin_interval`, siehe Config)
- **Aktueller Schritt:** `step-006` (Korrektur step-004) — Resume-Punkt für die nächste Session

> **Nutzerentscheidung 2026-08-22 (Straffung):** Die restlichen Epics werden
> zu größeren Steps konsolidiert (EPIC-3+4 → step-004, EPIC-5+6+7 → step-005).
> Die konsolidierten Step-Pläne schreibt der Orchestrator selbst (ausdrückliche
> Nutzeranweisung gegen Overkill); Coder→Kritiker-Zyklus bleibt je Step Pflicht.

## Resume-Notiz (nächste Session, 2026-08-23)

**Wo wir stehen:** step-001..003 approved und committet. step-004 ist codiert
(7b3b0284) und dokumentiert (1588160a), hat im Review GENAU EIN MAJOR-Finding
→ Verdict `issues`; Korrektur-Step **step-006** ist als mechanisches Transkript
bereits fertig geplant (`step-006/step-plan.md`, Planer-Skip gemäß spec §6.2.1).
Der große Abschluss-Step **step-005** (EPIC-5+6+7, Violations + change-context-
Vertrag + Doku) ist ebenfalls fertig geplant (`step-005/step-plan.md`) und
wartet.

**Reihenfolge morgen:**
1. Coder auf `step-006/step-plan.md` (kleine Quoting-Korrektur
   TestRecommendationBuilder + eine Test-Assertion), dann Kritiker (Modus
   step, `corrects: step-004` — bei approved gelten step-004 UND step-006 als
   done; Kettenbudget: 1. Korrektur in der Kette).
2. Coder auf `step-005/step-plan.md` (EPIC-5+6+7), dann Kritiker.
3. Planer meldet „keine offenen Epics" (EPIC-5/6/7 in roadmap.md abhaken) →
   Kritiker im Modus `global` → `task-summary.md` → Task `done`.
4. Danach: Nutzer pusht selbst; TD-001 (niedrig) bleibt offen.

**Hinweise:** Review-Verdict-Details in `step-004/step-review.md` (Finding:
TestRecommendationBuilder.cs:62-65, unquotierter `|` im Mehrklassen-Filter).
Konsolidierte Pläne sind Orchestrator-geschrieben (Nutzeranweisung) — der
Kritiker prüft sie trotzdem voll auf allen vier Ebenen. MCP-Server läuft
proaktiv mit (bei Bedarf get_server_health).
- **Roadmap:** siehe `roadmap.md` für den Epic-Fortschritt
- **Tech-Debt:** siehe `tech-debt.md` für gesammelte, bewusst nicht gefixte Funde
- **Gestartet:** 2026-08-22
- **Zuletzt aktualisiert:** 2026-08-22

## Steps

| Step | Epic | Status | Title | Corrects | Coded | Reviewed | Commit |
|------|------|--------|-------|----------|-------|----------|--------|
| step-001 | EPIC-1 | done | Traversierungs-Korrektur (EnqueueChildren) & Sufficiency-Hint-Parität | - | 232aec64 | approved | fe91bab8 / acd21e23 |
| step-002 | EPIC-2 | done | Strukturiertes DiffImpactAnalysis-Ergebnisobjekt im DiffImpactAnalyzer | - | 5b26c63b | approved | 59331e2e / 52f18833 |
| step-003 | EPIC-2 | done | Breiter Diff-Symbolscanner mit kollisionsfreien stabilen IDs | - | 85c7fdce | approved | e31006dd / bb34d3a7 |
| step-004 | EPIC-3+EPIC-4 | done (Korrektur ausstehend) | Testfundament, gebatchte Test-Zuordnung & recommendedTestCommands | - | 7b3b0284 | issues | 5a1c9952 / 1588160a |
| step-006 | EPIC-3+EPIC-4 | open | Korrektur step-004: Quoting des Mehrklassen-Filters | step-004 | - | - | - |
| step-005 | EPIC-5+EPIC-6+EPIC-7 | open | Violations-Stufe, get_impact-Vertrag change-context & Doku | - | - | - | - |

## Config

Keine `config.md` vorhanden — es gelten die Defaults aus der drift-loop-Spec
(`max_fix_rounds_per_step: 3`, `soft_step_checkin_interval: 40`,
`max_batch_items: 8`, `max_batch_diff_lines: 40`). Keine rollenabhängige
Modellzuweisung durch den Nutzer genannt.

## Abbruch-/Pause-Bedingungen

- Kettenbudget (3 Korrekturen pro `corrects`-Kette ohne `approved`) → Step `blocked`
- Weicher Deckel (jedes Vielfache von 40 Steps) → Zwischenfrage an den Nutzer
- Blocker (Step `blocked`) → Loop pausiert
