---
status: executing
task: 03_get-impact-zum-diff-kontext-erweitern
started_at: 2026-08-22
last_updated: 2026-08-23
rules_dir: .agents/rules
total_steps: 9
current_step: step-009
---

# Task State: 03_get-impact-zum-diff-kontext-erweitern

## Übersicht

- **Task-Status:** `executing` (Fortsetzung nach Nutzer-Revision 2026-08-23)
- **Steps gesamt:** 9 (regulär + Korrekturen — weicher Check-in bei
  jedem Vielfachen von `soft_step_checkin_interval`, siehe Config)
- **Aktueller Schritt:** `step-009` (in_progress)

> **Nutzerentscheidung 2026-08-22 (Straffung):** Die restlichen Epics wurden
> zu größeren Steps konsolidiert (EPIC-3+4 → step-004). Die konsolidierten
> Step-Pläne schrieb der Orchestrator selbst (ausdrückliche Nutzeranweisung
> gegen Overkill); Coder→Kritiker-Zyklus bleibt je Step Pflicht.
>
> **Nutzerentscheidung 2026-08-23 (Revision):** Die Konsolidierung von
> EPIC-5+6+7 in einen großen Abschluss-Step wird zurückgenommen. EPIC-5,
> EPIC-6 und EPIC-7 werden wieder einzeln über den normalen JIT-Planer
> (Step-Modus) abgearbeitet, wie ursprünglich vorgesehen. Der vorab
> geschriebene konsolidierte Plan (ehemals step-005) wurde gelöscht, bevor
> ein Coder ihn ausgeführt hat. step-006 (Korrektur step-004) bleibt als
> nächstes Step-Objekt bestehen; danach plant der Planer wieder normal.

## Resume-Notiz (nächste Session, 2026-08-23)

**Wo wir stehen:** step-001..003 approved und committet. step-004 ist codiert
(7b3b0284) und dokumentiert (1588160a), hat im Review GENAU EIN MAJOR-Finding
→ Verdict `issues`; Korrektur-Step **step-006** ist als mechanisches Transkript
bereits fertig geplant (`step-006/step-plan.md`, Planer-Skip gemäß spec §6.2.1).

**Reihenfolge:**
1. Coder auf `step-006/step-plan.md` (kleine Quoting-Korrektur
   TestRecommendationBuilder + eine Test-Assertion), dann Kritiker (Modus
   step, `corrects: step-004` — bei approved gelten step-004 UND step-006 als
   done; Kettenbudget: 1. Korrektur in der Kette).
2. Danach normaler Planer (Step-Modus) für EPIC-5 (solutionweite Violations +
   diffbezogene Filterung) → Coder → Kritiker.
3. Dann EPIC-6 (get_impact-Vertrag change-context & strukturierte Antwort)
   → Coder → Kritiker.
4. Dann EPIC-7 (Docs/agent-api.md inkl. Grenzen, README, ROADMAP) → Coder →
   Kritiker.
5. Planer meldet „keine offenen Epics" → Kritiker Modus `global` →
   `task-summary.md` → Task `done`. Nutzer pusht selbst; TD-001 bleibt offen.

**Hinweise:** Review-Verdict-Details in `step-004/step-review.md` (Finding:
TestRecommendationBuilder.cs:62-65, unquotierter `|` im Mehrklassen-Filter).
MCP-Server läuft proaktiv mit (bei Bedarf get_server_health).
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
| step-004 | EPIC-3+EPIC-4 | done | Testfundament, gebatchte Test-Zuordnung & recommendedTestCommands | - | 7b3b0284 | approved (via 006) | 5a1c9952 / 1588160a |
| step-006 | EPIC-3+EPIC-4 | done | Korrektur step-004: Quoting des Mehrklassen-Filters | step-004 | 4b53579a | approved | 9399714c / a766f727 |
| step-007 | EPIC-5 | done | Solutionweite Violations & diffbezogene Filterung (interne Stufe) | - | 8bc3e919 | approved | b4925761 / 7f44405c |
| step-008 | EPIC-6 | done | get_impact-Vertrag change-context & strukturierte Antwort | - | 5425f95f | approved | 0791aec9 / 448acb2a |
| step-009 | EPIC-7 | in_progress | Doku agent-api.md inkl. Grenzen + README + ROADMAP | - | - | - | - |

## Config

Keine `config.md` vorhanden — es gelten die Defaults aus der drift-loop-Spec
(`max_fix_rounds_per_step: 3`, `soft_step_checkin_interval: 40`,
`max_batch_items: 8`, `max_batch_diff_lines: 40`). Keine rollenabhängige
Modellzuweisung durch den Nutzer genannt.

## Abbruch-/Pause-Bedingungen

- Kettenbudget (3 Korrekturen pro `corrects`-Kette ohne `approved`) → Step `blocked`
- Weicher Deckel (jedes Vielfache von 40 Steps) → Zwischenfrage an den Nutzer
- Blocker (Step `blocked`) → Loop pausiert
