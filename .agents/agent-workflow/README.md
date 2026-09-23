# Agent-Workflow

Portables Kit: Konzept, Roadmap, Umsetzung. Drei getrennte Schritte, jeder nur wenn der Nutzer ihn startet. Kein Prompt startet den nächsten.

Kopierbar. Projektwissen steht nicht hier, sondern in `AGENTS.md` und den Rules des jeweiligen Repos.

## Aufruf

```text
Führe 01-konzept-planung.md aus. Task: tasks/<name>
Führe 02-roadmap-erstellung.md aus. Task: tasks/<name>
Führe 03-orchestrierte-umsetzung.md aus. Task: tasks/<name>
```

`@.cursor/agent-workflow/<datei>.md` plus Taskpfad ist gleichwertig. Ohne Taskverzeichnis: nur danach fragen.

| Datei | Schritt | Tut | Tut nicht |
|---|---|---|---|
| [01-konzept-planung.md](01-konzept-planung.md) | 1 | Sparring, Konzept persistieren | Roadmap, Code, Schritt 2 |
| [02-roadmap-erstellung.md](02-roadmap-erstellung.md) | 2 | Konzept in Checkboxen zerlegen | Umsetzen, Schritt 3 |
| [03-orchestrierte-umsetzung.md](03-orchestrierte-umsetzung.md) | 3 | Leafs sequenziell delegieren, Audit je Milestone | Selbst implementieren, anderen Workflow starten |

## Artefakte

```text
tasks/<name>/
  Konzept.md          # oder konzept/ wenn der Stoff mehrere Kapitel braucht
  roadmap.md          # klein: die Checkboxen stehen hier
  roadmap/            # nur bei großen Vorhaben
    <nr>-<kurzname>/
      roadmap.md
      tasks/Mx.y-Tz.md
```

Klein: eine Konzeptdatei, eine Roadmap, ein paar `- [ ]`. Keine Leaf-Dateien, keine Milestones aus Prinzip.

Ein ausführbarer Punkt ist eine Agent-Session (Analyse, Umsetzung, Tests, Doku, Abhaken, Commit). Grober Richtwert: ~512k Kontext. Zu groß → vorher teilen.

Resume = erste offene Checkbox. Keine `execution-log.md`, `tech-debt.md`, `code-map.md`, Step-Dateien.

## Pflichtabschnitte

**Konzept, Minimum:** Intention (warum und welches Ergebnis). Dazu nur das, was dieses Vorhaben braucht — typisch Ziel, Scope, Nicht-Ziele, Verifikation. Scope binär: Muss oder Nicht. Kein Optional, kein Nice-to-have.

```markdown
---
status: draft
---

# <Titel>

## Intention

<Warum, welches Ergebnis.>

## Scope

### Muss
### Nicht
```

`status: ready` nur nach ausdrücklicher Freigabe, und nur wenn keine Entscheidung mehr offen ist. Offene Punkte und Arbeitsgedächtnis gehören nicht in `ready`.

**Roadmap, Minimum:** geordnete Checkboxen, ausführbar ohne zu raten. Konzept nicht nachentscheiden. Parent-Checkboxen sind Aggregate: `[x]` erst wenn Kinder und Abnahme stimmen.

```markdown
# Roadmap: <Titel>

- [ ] **Punkt A — <Ergebnis>**
  - Intention: …
  - Nicht: …
  - Abnahme: …
- [ ] **Punkt B — …**
- [ ] **Audit**
```

**Leaf nur wenn nötig:** eigene Datei, die der Auftrag ist.

```markdown
# Mx.y-Tz – <Name>

## Intention
## Scope
## Nicht-Ziele
## Verträge und Invarianten
- **Verbindlich:** …
## Akzeptanz
- [ ] …
## Checkliste
- [ ] Ist-Stand geprüft
- [ ] Scope umgesetzt, Nicht-Ziele eingehalten
- [ ] Akzeptanz erfüllt, gegen Konzept und Anwendung geprüft
- [ ] Checkboxen in der Roadmap geschlossen
- [ ] atomarer Commit
## Abschlussnachweis
```

Haken nur setzen, was selbst geprüft wurde.

## Rollen in Schritt 3

- **Orchestrator:** Reihenfolge, ein Sub-Agent nach dem anderen, Diff/Checkbox-Stichprobe. Kein Produktionscode.
- **Leaf-Agent:** genau einen Punkt, Selbstprüfung gegen Checkboxen + Konzept + Anwendung, `[x]`, Commit des Slices.
- **Audit-Agent:** nach jedem Milestone (ohne Schnitt: einmal am Ende). Nur lesen und Feedback. Kein Produktionscode. Danach höchstens **ein** Korrektur-Leaf. Kein Loop.
