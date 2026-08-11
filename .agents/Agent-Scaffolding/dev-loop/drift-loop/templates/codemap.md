---
task: <TASK-NAME>
type: codemap
maintained_by: planer, coder, kritiker
last_updated: <ISO-8601>
---

# CodeMap: <TASK-NAME>

Task-scoped Landkarte — existiert nur für diesen Task, wird mit
`<task-dir>` gelöscht, kein projektweites Artefakt. Enthält **nur**, was
für diesen Task relevant ist (Module/Dateien/Bereiche, die ein Step
tatsächlich berührt hat oder für die Planung des nächsten Steps
gebraucht wird) — kein Anspruch auf vollständige Projektabdeckung.

**Pointer-Prinzip — wie Regel-Index (`roadmap.md`) und Tech-Debt-Index
(`tech-debt.md`):** Jeder Eintrag ist Ort + **ein Satz**, was dort ist
und wozu es für diesen Task relevant ist — keine Verhaltensbeschreibung,
kein „wie funktioniert das im Detail". Verhaltensbehauptungen veralten,
Ortsangaben kaum. Wer mehr wissen muss, liest die Datei selbst nach —
das ersetzt die Map nie, sie beschleunigt nur das Finden.

**Warum das trotzdem verlässlich bleibt (anders als generische Doku):**
Der gesamte Loop läuft strikt seriell — genau ein Subagent gleichzeitig
(`../spec.md` §6). Zwischen einem Coder-Update und dem nächsten Lesezugriff
kann sich am Code strukturell nichts geändert haben, was hier nicht auch
eingetragen wurde. Die Map ist also, solange sie gepflegt wird, tatsächlich
aktuell — kein Snapshot mit Drift-Risiko. **Schritt 2 im Step-Modus des
Planers („tatsächlichen Projektzustand lesen", `../spec.md` §7.2) bleibt
trotzdem Pflicht** — die Map sagt *wo* nachschauen, ersetzt nie das
Nachschauen selbst.

## Pflege — wer trägt wann ein

- **Planer, Roadmap-Modus (einmalig):** befüllt die Map initial aus dem
  Grobüberblick, den er beim Ableiten der Epics ohnehin über den
  Bestandscode gewinnt (`../skills/planer/SKILL.md` Roadmap-Modus
  Schritt 1).
- **Coder (jeder Step):** ergänzt/aktualisiert Einträge für tatsächlich
  angelegte oder geänderte Module, **vor** dem Doku-Commit
  (`../skills/coder/SKILL.md` Schritt 6a).
- **Planer, Step-Modus (jeder Step):** liest die Map vor dem Planen,
  ergänzt neue Bereiche, die er beim Lesen des Ist-Zustands entdeckt.
  Zusätzlich Grundlage für den Anti-Loop-Check (siehe unten).
- **Kritiker:** prüft stichprobenartig, ob die Map dem tatsächlichen Diff
  entspricht (Teil von Ebene 1, Plan-Erfüllung) — schreibt selbst nur bei
  offensichtlicher Lücke/Fehler nach, ist aber nicht Haupt-Pfleger.

## Anti-Loop-Nutzen

Bevor der Planer im Step-Modus einen neuen Step plant, gleicht er sein
Vorhaben gegen die hier verzeichneten, bereits getroffenen Entscheidungen
ab. Widerspricht der neue Plan erkennbar einem hier festgehaltenen,
bereits umgesetzten Stand (z. B. ein späterer Step würde zurückdrehen, was ein
früherer Schritt laut Map bewusst so gebaut hat): entweder im neuen Step-Plan explizit als
Erweiterung begründen, oder den alten Eintrag hier als „obsolet —
<Grund>" markieren (nicht löschen) — nie stillschweigend widersprechen.
Das verhindert kein Kreisen zu 100 %, macht ein Hin-und-Her aber
wenigstens sichtbar und begründungspflichtig statt stillschweigend.

## Karte

<Ein Eintrag pro relevantem Modul/Bereich. Format:>

- **`pfad/zu/modul`** — <ein Satz: was liegt hier, wozu für den Task
  relevant> (zuletzt: step-NNN)
- **`pfad/zu/anderem-modul`** — <ein Satz> (zuletzt: step-NNN)

<Bei obsolet gewordenen Einträgen: nicht löschen, sondern markieren, z. B.
„~~`pfad/zu/altem-modul`~~ — obsolet seit step-NNN, ersetzt durch
`pfad/zu/neuem-modul`".>
