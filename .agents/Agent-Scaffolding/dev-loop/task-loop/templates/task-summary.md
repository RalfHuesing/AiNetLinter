---
task: <TASK-NAME>
completed_at: <ISO-8601>
final_status: done  # done | aborted
total_iterations: <N>
total_commits: <N>
---

# Task Summary: <TASK-NAME>

## Ergebnis

<2-5 Sätze: Was wurde durch diesen Loop erreicht? Passt es zur
ursprünglichen Intention des Tasks?>

## Steps-Übersicht

| Step | Status | Title | Commit | Notiz |
|------|--------|-------|--------|-------|
| step-001 | done | <Titel> | `<SHA>` | approved |
| step-002 | done | <Titel> | `<SHA>` | superseded by step-005 |
| step-003 | blocked | <Titel> | - | Nutzer-Entscheidung ausstehend |
| step-004 | done | <Titel> | `<SHA>` | approved |
| step-005 | done | <Titel> | `<SHA>` | approved (Folge-Step aus 002) |
| ... | ... | ... | ... | ... |

## Globale 360°-Audit-Befunde

<Ergebnis des finalen Auditer-Aufrufs (Modus `global`).
Strukturiert nach:>

### Task-Intention erfüllt?

<Passt das Ergebnis zur ursprünglichen Aufgaben-Definition? Was fehlt
möglicherweise?>

### Seiteneffekte / Regressionen

<Gibt es Stellen im Projekt, die durch die Steps gebrochen wurden?
Build-Status, Test-Status, Smoke-Test-Ergebnisse.>

### Konsistenz

<Nutzen alle Steps einheitliche Patterns, Naming, Conventions? Oder
„jeder Step sein eigener Stil"?>

### Vollständigkeit

<Gibt es Punkte aus der Original-Aufgabe, die in keinem Step gelandet
sind? Falls ja: warum?>

### Rules-Konformität (Stichproben)

<Aus 2-3 zufällig gewählten Steps: Rules weiterhin eingehalten?>

## Offene Punkte

- [ ] <Punkt 1 — was nicht erledigt ist, mit Begründung>
- [ ] <Punkt 2>
- ...

## Empfehlungen

<Was als nächstes zu tun ist — z. B. „Punkte X und Y aus den offenen
Punkten in einen neuen Task `<neuer-task-dir>` überführen", „Vor dem
Push nochmal lokalen Smoke-Test laufen lassen", „PR öffnen gegen
Hauptbranch wenn alle Punkte grün", …>

## Statistik

- **Anzahl Steps:** <N>
- **Davon approved:** <N>
- **Davon superseded:** <N>
- **Davon blocked:** <N>
- **Anzahl Commits:** <N>
- **Loop-Iterationen (Folge-Steps):** <N> / 3
- **Laufzeit:** <von started_at bis completed_at>
