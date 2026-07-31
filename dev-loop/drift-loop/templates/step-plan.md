---
status: open
type: step-plan
task: <TASK-NAME>
step: <NNN>              # im Fix-Modus: <NNN>/fix-<XX>
title: "<Titel des Steps>"
epic: <EPIC-NN>          # Bezug zum Epic in roadmap.md, dem dieser Step zuarbeitet
estimated_risk: <low|medium|high>  # Einschätzung des Planers, siehe skills/planer/SKILL.md
step_type: single  # single (Default) | batch — siehe ../spec.md §10.6. Bei batch: items-Liste unten füllen.
items: []  # nur bei step_type: batch. Ein Eintrag pro gebündeltem Mini-Befund innerhalb des Epics:
# items:
#   - id: item-01
#     title: "<Kurztitel des Befunds>"
#     source: "<Quelle, z. B. konzept.md-Referenz>"
created_by: planer
created_by_model: <Modell-ID deiner eigenen LLM-Instanz>
created_by_model_knowledge_cutoff: <Knowledge-Cutoff-Datum, z. B. 2026-01>
created_at: <ISO-8601>
related_to: []  # Pointer auf andere step-NNN (Task-interne Abhängigkeiten) oder auf step-review.md (Fix-Modus) — nie Fakten cachen, nur verweisen. Siehe ../spec.md §10.6.
---

# Step <NNN>: <Titel>

## Bezug

- **Task:** `<TASK-NAME>`
- **Epic:** `<EPIC-NN>` aus `roadmap.md` — <Kurzbezug, was an diesem Epic
  offen ist>
- **Konzept-Referenz:** <Verweis auf den Abschnitt in `konzept.md`, der
  diesen Step motiviert>

## Aktueller Projektzustand (JIT-Kontext)

<Kurz, was der Planer beim Lesen des aktuellen Codes vorgefunden hat, das
diesen Step-Plan beeinflusst hat — insbesondere bereits bestehende
Strukturen, die wiederverwendet statt neu gebaut werden sollen. Das ist
der Kern des JIT-Ansatzes: hält fest, was beim Planen vorgefunden wurde.>

## Intention

<2-3 Sätze: Was soll nach diesem Step erreicht sein? Warum genau so?>

## Konkrete Änderungen

**Bei `step_type: single`** (Standard-Struktur):

### Datei 1: `pfad/zu/datei.cs` (Zeile X-Y)

- **Was:** <konkrete Änderung>
- **Warum:** <kurz>

### Datei 2: ...

**Bei `step_type: batch`** (siehe `../spec.md` §10.6): statt „Datei N"
eine Unterüberschrift pro Item aus der `items`-Liste im Frontmatter:

### item-01: <Kurztitel> — `pfad/zu/datei.md` (Zeile X)

- **Was:** <konkrete Änderung>
- **Warum:** <kurz>

## Tests

- [ ] <Test 1 — exakter Name oder Beschreibung>
- [ ] <Test 2>

<Wenn keine Tests nötig sind: "Keine — Begründung warum.">

## Definition of Done

- [ ] Alle „Konkrete Änderungen" umgesetzt
- [ ] Build-Command aus Tech-Stack-Notiz (`roadmap.md`) grün
- [ ] Test-Command aus Tech-Stack-Notiz grün
- [ ] Commit auf aktuellem Branch (Conventional Commit)
- [ ] `step-NNN/step-result.md` geschrieben
- [ ] `status` in `step-plan.md` von `in_progress` auf `done (pending audit)` gesetzt

## Rules-Refs

- `<rules_dir>/<datei>#<Abschnitt>` — <was daran relevant ist>

## Bekannte Ausnahmen

- <Test der flaky ist, mit Begründung warum ignorierbar>

## Code-Skizze (optional)

```
// Wenn hilfreich: ein Code-Snippet das zeigt, wie die Änderung aussehen soll
```

## Notes

<Edge-Cases, Stolperfallen, Hinweise auf bestehende Patterns die zu
nutzen sind — insbesondere Verweise auf Strukturen, die dieser Step
bewusst wiederverwendet statt dupliziert.>
