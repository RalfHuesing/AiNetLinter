---
task: verbesserungen-mcp
type: task-config
last_updated: 2026-08-05
---

# Task-Config: verbesserungen-mcp

Override-Datei für die Default-Grenzwerte aus `../spec.md` §10.5/§10.6
(siehe auch `task-state.md` Config-Block). Nur abweichende Felder — alles
andere bleibt beim Spec-Default.

```
max_batch_diff_lines: 160   # Default 40 (../spec.md §10.6), siehe Begründung unten
```

## Begründung `max_batch_diff_lines: 160`

Angepasst durch den Planer in `step-004` (EPIC-03-Batch, vier
Muss-Haben-Punkte aus `Konzept.md` Scope P2/P3). Der Default 40 ist laut
Spec-Kommentar ("50 Dateien mit je 1 Zeile") auf rein kosmetische
Batches kalibriert (z. B. Kommentar-/Text-Korrekturen ohne eigene
Test-Logik). EPIC-03 verlangt aber laut `Konzept.md` „Definition of
Done" für **jeden** der vier Punkte einen eigenen Regressionstest — vier
unabhängige, aber jeweils dedizierte Tests kosten strukturell mehr als
40 Diff-Zeilen, auch wenn jede einzelne Änderung für sich klein und
`estimated_risk: low` ist (siehe `step-004/step-plan.md` „Aktueller
Projektzustand" für die Einzelschätzung je Item, Summe ca. 120-160
Zeilen inkl. Tests).

Zusätzlicher Kontext: Nutzer-Vorgabe in `task-state.md` („größere
Brocken, keine Mini-Steps") legt nahe, den Micro-Batch-Mechanismus für
genau diesen Fall (EPIC-03 ist laut `roadmap.md` explizit dafür
vorgesehen) großzügig zu nutzen, statt vier Items künstlich auf zwei
separate Batch-Steps aufzuteilen, nur um unter dem generischen Default
zu bleiben. Gilt nur für diesen Task (`spec.md` §10.6: „konfigurierbar
pro Task").

`max_batch_items` bleibt beim Default (8) — 4 Items sind unkritisch.
