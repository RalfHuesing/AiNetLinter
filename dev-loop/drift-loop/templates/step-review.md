---
status: done
type: step-review
task: <TASK-NAME>
step: <NNN>              # flach, Task-weite Sequenz — auch Korrekturen liegen hier, nie in einem Unterordner
epic: <EPIC-NN>
step_type: single  # single | batch — aus step-plan.md übernehmen
reviewed_by: kritiker
reviewed_by_model: <Modell-ID deiner eigenen LLM-Instanz>
reviewed_by_model_knowledge_cutoff: <Knowledge-Cutoff-Datum, z. B. 2026-01>
reviewed_at: <ISO-8601>
verdict: approved  # approved | issues | blocked
tech_debt_ids: []  # z. B. [TD-003, TD-004] — welche tech-debt.md-Einträge dieser Review-Durchgang erzeugt hat, falls welche
---

# Review Step <NNN>: <Titel>

<**Umfang ist verdict-abhängig — gekürzt wird die Darstellung, nie die
Prüfung.** Bei `approved`: je Befund-Ebene ein Satz, leere Abschnitte
ganz weglassen statt „Keine." schreiben. Bei `issues`/`blocked`: volle
Ausführlichkeit. Begründung und Details: `../skills/kritiker/SKILL.md`
Schritt 5, `../spec.md` §10.7. Dieser Hinweis-Block gehört wie alle
`<…>`-Blöcke **nicht** in die erzeugte Datei.>

## Verdict

- [x] **approved** — alle vier Prüfebenen ok
- [ ] **issues** — Korrektur-Step `step-<MMM>` angelegt (`corrects: step-<NNN>`)
- [ ] **blocked** — Nutzer-Entscheidung nötig (siehe Frage unten)

## Geprüft

- [ ] Plan-Erfüllung: alle im `step-plan.md` genannten Änderungen erfolgt
- [ ] Rules-Konformität: `<rules_dir>/**` eingehalten
- [ ] Logische Korrektheit: Code macht was er soll, nicht nur „grün"
- [ ] Konzept-Treue: passt die Umsetzung zu `konzept.md` (Scope, Non-Goals, Muss-Haben)
- [ ] Build: selbst nachgeprüft, grün
- [ ] Tests: selbst nachgeprüft, grün (oder Baselines beachtet)

## Befund

<Bei `approved`: je Ebene **ein Satz**, z. B. „Alle vier Plan-Punkte
umgesetzt, Tests vorhanden und grün." Keine Aufzählung pro Plan-Punkt,
keine Wiederholung des Plan-Inhalts. Bei `issues`/`blocked`: ausführlich
wie unten beschrieben.>

### Plan-Erfüllung

<Pro Punkt aus dem Plan: erfüllt / teilweise / nicht erfüllt.>

### Rules-Konformität

<Pro relevante Regel: eingehalten / verletzt. Bei Verletzung: Datei + Zeile
+ Regel-Name + Soll-Zustand.>

### Logische Korrektheit

<Eigene Beobachtungen zur Semantik. „Sieht richtig aus, aber…" gehört
hierhin.>

### Konzept-Treue (Ebene 4)

<Weicht die Umsetzung erkennbar von `konzept.md` ab — Scope überschritten,
ein Non-Goal umgesetzt, ein Muss-Haben-Punkt ausgelassen? Ein Fund hier
zählt für das Verdict genauso wie ein Fund auf den drei Ebenen oben — kein
eigener, laxerer Maßstab.>

### Build-/Test-Status

<Bei grün: eine Zeile je Command, kein Volldump — der Output interessiert
niemanden mehr, sobald er grün ist. Bei rot: gekürzter Fehler-Output
(nur die relevanten Zeilen), denn der wird tatsächlich gebraucht.>

```
<Build-Command> → grün
<Test-Command>  → grün (<N> Tests, 0 Fehler)
```

## Findings (nur bei `issues` — Abschnitt sonst weglassen)

<Zwingend CRITICAL oder MAJOR, auf einer der vier Ebenen oben.>

<Nummerierte Liste. Jeder Punkt MUSS mit [CRITICAL] oder [MAJOR] getaggt
sein: Datei:Zeile + Schweregrad + Ebene (Plan/Rules/Logik/Konzept) + Was
+ Wie fixen.>

1. `pfad/zu/datei.cs:42` — [CRITICAL|MAJOR] [Konzept-Treue] <Befund>. **Fix:** <konkret>.
2. ...

<Je präziser „Fix" (konkrete Anweisung statt vager Richtung), desto eher
kann der Orchestrator den Korrektur-Plan mechanisch selbst schreiben und
den Planer-Aufruf überspringen — siehe `../spec.md` §6.2.1. Ist bei einem
Finding Ermessen nötig: das ruhig so benennen statt eine falsche
Eindeutigkeit vorzutäuschen.>

<Bei `step_type: batch`: jedem Finding zusätzlich die Item-ID voranstellen.>

## Frage an Nutzer (nur bei `blocked` — Abschnitt sonst weglassen)

<Was genau unklar ist.>

## Sonstige Beobachtungen / MINOR / NITPICK (weglassen, wenn keine)

<In-Scope-Kleinigkeiten (Stilfragen, kosmetische Punkte) — führt NICHT zu
issues, Verdict bleibt approved. NICHT der Ort für Architektur-/
Anti-Pattern-Funde außerhalb des Step-Scopes, die gehören in
`tech-debt.md` (siehe unten). Hast du nichts zu vermerken: Abschnitt
weglassen, nicht „Keine." schreiben.>

## Tech-Debt-Einträge aus diesem Review (weglassen, wenn keine)

<Nur Architektur-/Anti-Pattern-/Duplikations-Beobachtungen AUSSERHALB des
Step-Scopes, die NICHT zu einem Korrektur-Step führen sollen — siehe
`../spec.md` §8.3. Pro Fund **eine Zeile**: ID + ein Satz. Volltext steht
ausschließlich in `tech-debt.md` (Pointer-Prinzip, nicht doppelt
pflegen).>

- `TD-003` (siehe `tech-debt.md`) — <ein Satz Kurzfassung>
