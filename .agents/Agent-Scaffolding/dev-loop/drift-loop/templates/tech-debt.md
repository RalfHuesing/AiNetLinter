---
task: <TASK-NAME>
type: tech-debt-log
maintained_by: kritiker
last_updated: <ISO-8601>
---

# Tech-Debt-Log: <TASK-NAME>

Append-only. Jeder Eintrag ist eine vom Kritiker während eines
Step-Reviews beobachtete, aber bewusst **nicht** gefixte Auffälligkeit
außerhalb des Scopes des jeweiligen Steps (Architektur, Anti-Pattern,
Duplikation, Konsistenz) — siehe `../spec.md` §8.3/§9.

**Priorität ist reine Sortierhilfe für den Menschen, kein Auslöser.**
Bewusst `hoch`/`mittel`/`niedrig` (deutsch) statt `CRITICAL`/`MAJOR`/
`MINOR`, um jede Verwechslung mit den blockierenden Findings in
`step-review.md` auszuschließen — kein Eintrag hier führt automatisch zu
einem eigenen Korrektur-Step oder einem neuen Epic. Das entscheidet
grundsätzlich der Nutzer (manuell, z. B. durch Ergänzen eines Epics in
`roadmap.md` mit Verweis auf die Tech-Debt-ID).

**`auto_fixable` (`ja`/`nein`, siehe `../spec.md` §9.1) ist die einzige
Ausnahme:** rein mechanische, entscheidungsfreie Fixes ohne
Architektur-Ermessen dürfen vom Planer opportunistisch an einen ohnehin
laufenden Step angehängt werden (§10.6) — kein eigener Step, kein
eigener Sweep. Default bei Unsicherheit ist `nein`.

## Index

<Eine Zeile pro Eintrag — **Kurzfassung, kein Volltext**. Zweck
(identisch zum Regel-Index in `roadmap.md`): Der Planer liest diese Datei
bei **jedem** Step-Modus-Aufruf, sie wächst aber append-only über den
ganzen Task. Gebraucht wird dabei nur „gibt es im Bereich, den ich gerade
plane, schon eine bekannte Schwachstelle" — dafür reicht diese Tabelle.
Nur wenn eine Zeile den aktuellen Bereich berührt, wird der zugehörige
Volltext-Eintrag unten gelesen. Siehe `../spec.md` §9 /
`../skills/planer/SKILL.md` Schritt 3.

Der Kritiker pflegt Index-Zeile und Volltext-Eintrag **immer zusammen**
(ein Fund = eine Zeile hier + ein Abschnitt unten) — ein Eintrag ohne
Index-Zeile ist für den Planer praktisch unsichtbar.>

| ID | Bereich / Datei | Priorität | Auto-Fixable | Kurzfassung |
|---|---|---|---|---|
| TD-XXX | `pfad/zu/modul` | mittel | nein | <ein Halbsatz, worum es geht> |

## Einträge

### TD-XXX — <Kurztitel> [Priorität: hoch|mittel|niedrig] [Auto-Fixable: ja|nein]

- **Gefunden in:** step-NNN (Kritiker-Review vom <ISO-8601>)
- **Ort:** `pfad/zu/datei.ext:Zeile` (ggf. weitere Fundstellen)
- **Befund:** <was ist das Problem, konkret>
- **Warum nicht sofort gefixt:** <außerhalb des Scopes von step-NNN,
  beträfe z. B. mehrere frühere Steps>
- **Vorschlag:** <grobe Fix-Richtung, kein Detailplan — das wäre Aufgabe
  eines künftigen Epics/Steps, falls der Nutzer sich dafür entscheidet>
- **Auto-Fixable:** ja|nein — `ja` nur bei rein mechanischer Korrektur
  ohne Architektur-Ermessen und ohne Verhaltensänderung (siehe `../spec.md`
  §9.1). Bei `ja`: darf vom Planer opportunistisch an einen ohnehin
  laufenden Step angehängt werden, statt auf eine Nutzer-Entscheidung zu
  warten.
- **Status:** offen  # offen | erledigt | verworfen — Änderung ist
  manuell (Nutzer) bzw. automatisch auf „erledigt" nach erfolgreicher
  Bündelung eines `auto_fixable: ja`-Eintrags; kein Subagent ändert den
  Status eines `nein`-Eintrags selbst

### TD-YYY — ...
