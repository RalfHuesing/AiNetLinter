---
status: done
type: step-review
task: <TASK-NAME>
step: <NNN>              # im Fix-Modus: <NNN>/fix-<XX>
step_type: single  # single | batch — aus step-plan.md übernehmen, siehe ../../spec.md §7.7
reviewed_by: auditer
reviewed_by_model: <Modell-ID deiner eigenen LLM-Instanz>
reviewed_by_model_knowledge_cutoff: <Knowledge-Cutoff-Datum, z. B. 2026-01>
reviewed_at: <ISO-8601>
verdict: approved  # approved | issues | blocked
---

# Review Step <NNN>: <Titel>

## Verdict

- [x] **approved** — alle drei Prüfebenen ok
- [ ] **issues** — Fix-Step `step-<NNN>/fix-<XX>` angelegt mit Fix-Plan
- [ ] **blocked** — Nutzer-Entscheidung nötig (siehe Frage unten)

## Geprüft

- [ ] Plan-Erfüllung: alle im `step-plan.md` genannten Änderungen erfolgt
- [ ] Rules-Konformität: `<rules_dir>/**` eingehalten
- [ ] Logische Korrektheit: Code macht was er soll, nicht nur „grün"
- [ ] Build: selbst nachgeprüft, grün
- [ ] Tests: selbst nachgeprüft, grün (oder Baselines beachtet)

## Befund

### Plan-Erfüllung

<Pro Punkt aus dem Plan: erfüllt / teilweise / nicht erfüllt.
Bei „teilweise" oder „nicht": konkret was fehlt.>

### Rules-Konformität

<Pro relevante Regel: eingehalten / verletzt. Bei Verletzung: Datei + Zeile
+ Regel-Name + Soll-Zustand.>

### Logische Korrektheit

<Eigene Beobachtungen zur Semantik. „Sieht richtig aus, aber…" ist das,
was hier rein soll. Auch: Test-Coverage-Lücken die nicht aus dem Plan
kommen, aber gefunden wurden.>

### Build-Status

```
<Build-Command + Ergebnis>
```

### Test-Status

```
<Test-Command + Ergebnis>
```

## Findings (bei `issues` — zwingend CRITICAL oder MAJOR)

<Nummerierte Liste. Jeder Punkt MUSS mit [CRITICAL] oder [MAJOR] getaggt sein: Datei:Zeile + Schweregrad + Was + Wie fixen.>

1. `pfad/zu/datei.cs:42` — [CRITICAL|MAJOR] <Befund>. **Fix:** <konkret>.
2. ...

<Bei `step_type: batch`: jedem Finding zusätzlich die Item-ID voranstellen,
z. B. `item-03: pfad/zu/datei.cs:42 — [MAJOR] ...` — der Fix-Modus des
Planers plant dann ausschließlich dieses Item nach, siehe `../../spec.md`
§7.7. Items ohne Finding kurz als geprüft/unauffällig vermerken (z. B.
unter „Sonstige Beobachtungen" oder direkt hier als „item-01..item-05:
geprüft, keine Findings"), damit klar ist, dass sie bewusst ausgelassen
wurden statt übersehen zu sein.>

## Frage an Nutzer (bei `blocked`)

<Was genau unklar ist. Z. B. „Plan schlägt vor, X in einen Helper zu
extrahieren — das berührt aber Y in einem anderen Modul. Soll das in
diesem Step mitgemacht werden, oder als eigener Refactoring-Step
angelegt werden?">

## Sonstige Beobachtungen / MINOR / NITPICK (führt NICHT zu issues, Verdict bleibt approved)

<Dinge die gesehen wurden, die aber MINOR/NITPICK sind (z. B. Linter-Hinweise in Test-Dateien, kosmetische Stilfragen, Re-Factor-Ideen). Pro Punkt: kurze Beschreibung. Diese Liste fließt in den globalen 360°-Audit am Ende.>
