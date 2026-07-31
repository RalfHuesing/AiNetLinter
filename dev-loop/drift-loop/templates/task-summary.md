---
task: <TASK-NAME>
completed_at: <ISO-8601>
final_status: done  # done | aborted
total_iterations: <N>
total_commits: <N>
total_epics: <N>
total_tech_debt_entries: <N>
---

# Task Summary: <TASK-NAME>

## Ergebnis

<2-5 Sätze: Was wurde durch diesen Loop erreicht? Passt es zur
ursprünglichen Intention aus `konzept.md`?>

## Roadmap-Status

<Kurzer Verweis: alle Epics aus `roadmap.md` abgehakt? Falls nicht, welche
offen/obsolet und warum — siehe `roadmap.md` für Details, hier nur
Zusammenfassung, kein Duplikat.>

## Steps-Übersicht

| Step | Epic | Status | Title | Commit | Notiz |
|------|------|--------|-------|--------|-------|
| step-001 | EPIC-01 | done | <Titel> | `<SHA>` | approved |
| step-002 | EPIC-01 | blocked | <Titel> | - | Nutzer-Entscheidung ausstehend |
| ... | ... | ... | ... | ... | ... |

## Globale Audit-Befunde (Kritiker, Modus `global`)

### Konzept erfüllt?

<Passt das Ergebnis zur ursprünglichen `konzept.md`? Was fehlt
möglicherweise?>

### Seiteneffekte / Regressionen

<Gibt es Stellen im Projekt, die durch die Steps gebrochen wurden?
Build-Status, Test-Status.>

### Rules-Konformität (Stichproben)

<Aus 2-3 zufällig gewählten Steps: Rules weiterhin eingehalten?>

## Tech-Debt-Zusammenfassung

<Aggregation aus `tech-debt.md` — Volltext bleibt dort (Pointer-Prinzip),
hier nur Übersicht:>

- **Hoch:** <N> Einträge — `<TD-IDs>`
- **Mittel:** <N> Einträge — `<TD-IDs>`
- **Niedrig:** <N> Einträge — `<TD-IDs>`

<Kurzer Hinweis, falls einzelne Einträge aus Nutzersicht dringend
erscheinen — keine Empfehlung, die selbst schon entscheidet, nur ein
Hinweis, worauf sich ein Blick lohnt.>

## Offene Punkte

- [ ] <Punkt 1 — was nicht erledigt ist, mit Begründung>
- ...

## Empfehlungen

<Was als nächstes zu tun ist — z. B. „TD-002 als eigenes Epic in einem
Folge-Task aufnehmen", „Vor dem Push nochmal lokalen Smoke-Test laufen
lassen", …>

## Statistik

- **Anzahl Epics:** <N>, davon abgehakt: <N>
- **Anzahl Steps:** <N>
- **Davon approved:** <N>
- **Davon blocked:** <N>
- **Anzahl Commits:** <N>
- **Anzahl Tech-Debt-Einträge:** <N>
- **Loop-Iterationen (Fix-Runden):** <N> / 12 (Task-Not-Anker)
- **Laufzeit:** <von started_at bis completed_at>
