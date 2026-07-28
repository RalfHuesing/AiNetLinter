---
status: open
type: step-plan
task: <TASK-NAME>
step: <NNN>              # im Fix-Modus: <NNN>/fix-<XX>
title: "<Titel des Steps>"
estimated_risk: <low|medium|high>  # Einschätzung des Planers, siehe SKILL.md §3a. Bei step_type: batch: Risiko aller Items ist per Definition low.
step_type: single  # single (Default) | batch — siehe ../../spec.md §7.7. Bei batch: items-Liste unten füllen.
items: []  # nur bei step_type: batch. Ein Eintrag pro gebündeltem Mini-Befund:
# items:
#   - id: item-01
#     title: "<Kurztitel des Befunds>"
#     source: "<Quelle, z. B. Aufgaben-Doku-Referenz>"
#   - id: item-02
#     title: "<Kurztitel>"
#     source: "<Quelle>"
created_by: planer
created_by_model: <Modell-ID deiner eigenen LLM-Instanz>
created_by_model_knowledge_cutoff: <Knowledge-Cutoff-Datum, z. B. 2026-01>
created_at: <ISO-8601>
related_to: []  # Pointer auf andere step-NNN (Task-interne Abhängigkeiten) oder auf step-review.md (Fix-Modus) — nie Fakten cachen, nur verweisen. Siehe ../../spec.md §7.6.
---

# Step <NNN>: <Titel>

## Bezug

- **Task:** `<TASK-NAME>`
- **Quelle:** <Verweis auf den Teil der Aufgaben-Doku, z. B. "audit-2026-07-24#12" oder "Feature X — Sektion 3">
- **Phase / Priorität:** <falls in der Aufgaben-Doku definiert>

## Intention

<2-3 Sätze: Was soll nach diesem Step erreicht sein? Warum genau so?>

## Konkrete Änderungen

**Bei `step_type: single`** (Standard-Struktur):

### Datei 1: `pfad/zu/datei.cs` (Zeile X-Y)

- **Was:** <konkrete Änderung, nicht "implementiere X" sondern "in Foo.cs Zeile 13 ergänze: ...">
- **Warum:** <kurz>

### Datei 2: ...

<Wiederholen für jede betroffene Datei. Wenn keine Datei, sondern z. B. nur Doku: hier explizit "Doku: README.md, Sektion X — Absatz Y erweitern um Z.">

**Bei `step_type: batch`** (siehe `../../spec.md` §7.7): statt „Datei N"
eine Unterüberschrift pro Item aus der `items`-Liste im Frontmatter,
Item-ID im Titel:

### item-01: <Kurztitel> — `pfad/zu/datei.md` (Zeile X)

- **Was:** <konkrete Änderung>
- **Warum:** <kurz>

### item-02: ...

<Ein Abschnitt pro Item, unabhängig davon ob die Items thematisch
zusammenhängen oder nicht.>

## Tests

- [ ] <Test 1 — exakter Name oder Beschreibung>
- [ ] <Test 2>
- [ ] <ggf. manueller Smoke-Test>

<Wenn keine Tests nötig sind (z. B. reine Doku-Änderung): "Keine — Begründung warum.">

## Definition of Done

- [ ] Alle „Konkrete Änderungen" umgesetzt
- [ ] Build-Command aus Tech-Stack-Notiz grün (0 Warnings, 0 Errors)
- [ ] Test-Command aus Tech-Stack-Notiz grün (Ausnahmen siehe „Bekannte Ausnahmen")
- [ ] Commit auf aktuellem Branch (Conventional Commit, aus Tech-Stack-Notiz/Projekt-Rules abgeleitete Sprache/Form)
- [ ] `step-NNN/step-result.md` geschrieben
- [ ] `status` in `step-plan.md` von `in_progress` auf `done (pending audit)` gesetzt

## Rules-Refs

- `<rules_dir>/<datei>#<Abschnitt>` — <was daran relevant ist> (projekt-root-relativ; `rules_dir` siehe Tech-Stack-Notiz / `task-state.md`-Frontmatter)
- <weitere>

## Bekannte Ausnahmen

- <Test der flaky ist, mit Begründung warum ignorierbar>

## Code-Skizze (optional)

```
// Wenn hilfreich: ein Code-Snippet das zeigt, wie die Änderung aussehen soll
```

## Notes

<Alles was sonst noch relevant ist: Edge-Cases, Stolperfallen, Hinweise auf bestehende Patterns die zu nutzen sind, …>
