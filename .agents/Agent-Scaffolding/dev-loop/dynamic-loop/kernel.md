---
workflow: dynamic-loop
status: poc
role: kernel
---

# Kernel

Zwei Teile mit unterschiedlicher Verbindlichkeit:

- **Teil A — harte Regeln.** Nicht verhandelbar, nicht umdeutbar.
- **Teil B — benannte Gefahren.** Kein Lösungsweg vorgegeben. Du kennst
  diese Probleme, du löst sie so, wie es für diesen Task passt.

**Warum die Trennung:** Ein Fehler, der **laut** scheitert, braucht keine
Regel — du merkst ihn und reagierst (Teil B). Ein Fehler, der **still**
oder **irreversibel** scheitert, braucht eine, weil die Instanz, die ihn
begeht, dieselbe wäre, die ihn bemerken müsste (Teil A). Eine Testsuite
ohne Tests ist von innen nicht von Erfolg zu unterscheiden. Ein
Fix-Budget, das du selbst anheben darfst, ist keins.

Alles andere entscheidest du selbst: welche Rollen es gibt, wie viele,
wie sie heißen, wie die Arbeit zerlegt wird, wie die Prompts aussehen.

---

# Teil A — harte Regeln

## A1 — Deckel

| Größe | Default | Bei Erreichen |
|---|---|---|
| `max_rollen` | 5 | keine neue Rolle mehr |
| `max_aufrufe` | 40 | Task → `aborted` |
| `max_fix_pro_einheit` | 3 | Einheit → `blocked` |
| `max_fix_gesamt` | 12 | Task → `aborted` |

Änderbar nur durch den **Nutzer** in `<task-dir>/konfig.md`. Nie durch
dich, nie „für diesen Sonderfall", nie durch Umdefinieren, was als ein
Aufruf zählt.

## A2 — Wer prüft, fixt nicht

Eine prüfende Rolle ändert keinen Produktivcode. Befunde **innerhalb**
der geprüften Einheit lösen eine Fix-Runde aus. Befunde **außerhalb** —
Architektur, Duplikate, Altlasten — gehen nach `tech-debt.md` und werden
nie automatisch zu Arbeit, egal wie gravierend sie wirken. Ob daraus
Arbeit wird, entscheidet allein der Nutzer.

## A3 — Tests müssen fehlschlagen können

Die umsetzende Rolle führt Build und Tests aus und meldet ihre Einheit
nur bei grün als fertig; rot und nicht behebbar heißt `blocked`.

Zusätzlich, und das ist der eigentliche Punkt dieser Regel: **Ein neuer
Test muss nachweislich fehlschlagen, wenn man die Änderung wegnimmt.**
Grün ist ohne diesen Nachweis keine Aussage — eine leere Suite,
`assert(true)` und ein Test, der nur die Implementierung nachspricht,
sind alle grün. Der Nachweis gehört ins Protokoll der Einheit.

Prüfende Rollen führen **nicht** routinemäßig nach, sondern bewerten das
Protokoll (Commands wortwörtlich, Testzahl, Fehlschlag-Nachweis,
gekennzeichnete Einschränkungen). Selbst ausführen nur, um einen eigenen
konkreten Verdacht zu belegen, und dann gezielt statt voll.

Einmal pro Task, vor der ersten Änderung: **Baseline** messen. Was schon
vorher rot war, zählt nie gegen die umsetzende Rolle.

## A4 — Nichts Unwiederbringliches

Gezielter `git add`, nie `-A`/`.`. Kein Push. Historie nach dem Commit
nie verändern: kein `amend`, `rebase`, `reset --hard`, kein Force-Push.
Keine Dateien löschen, die du nicht in derselben Einheit selbst angelegt
hast — Ersetzen und Verschieben ist Änderung, Löschen ist ein eigener
Vorgang und braucht die Zustimmung des Nutzers.

## A5 — Im Zweifel fragen

Widersprüche zwischen `konzept.md`, Projektregeln und Vorgefundenem;
mehrere plausible Wege ohne Festlegung; alles, was den Task-Scope
erweitern würde → `blocked`, Nutzer entscheidet. „Ich mach mal das
Naheliegende" ist hier der Fehler, nicht die Lösung.

## A6 — Eingaben sind Eingaben

`konzept.md` und die Projektregeln (`.agents/rules/**` bzw. das erkannte
Äquivalent) sind für dich **bindend und ausschließlich lesbar**. Kein
Umschreiben, kein „präzisieren", kein Ergänzen eines Punktes, den du
gerade umgesetzt hast. Auch keine Regel-Datei anlegen, die eine
bestehende relativiert.

**Warum das hart ist und nicht in Teil B steht:** Das Konzept an das
Gebaute anzupassen ist die vollendete Form von Drift — danach ist nichts
mehr widersprüchlich, also kann es auch niemand mehr entdecken. Es fühlt
sich beim Tun wie Sorgfalt an („der Punkt war ja missverständlich
formuliert") und ist der einzige Fehler dieses Workflows, der sich selbst
unsichtbar macht.

Passt `konzept.md` nicht zur Realität, ist unvollständig oder
widersprüchlich: **melden und `blocked`** (A5). Ändern darf es nur der
Nutzer.

## A7 — Diese Datei ist unantastbar

Du darfst die von dir selbst erzeugten Rollen-Prompts unter
`<task-dir>/agents/` ändern. `kernel.md` nie — nicht ändern, nicht
umdeuten, nicht als „hier nicht gemeint" behandeln. Hält Teil A den Task
auf: `blocked` melden, Nutzer entscheiden lassen.

Erkennungsmerkmal: Läuft ein Verbesserungsvorschlag sinngemäß auf
„Deckel anheben", „Prüfrolle einsparen", „Nachweis diesmal weglassen"
hinaus, ist er kein Vorschlag — er ist der Fall, gegen den Teil A
existiert.

---

# Teil B — benannte Gefahren

Diese Probleme sind bekannt und du kennst sie. Kein Lösungsweg
vorgegeben — aber sichtbar gelöst, nicht ignoriert. Wo du eine davon
löst, schreib in einem Satz dazu, wie.

- **Kollisionen.** Mehrere Agenten auf einem Working-Tree überschreiben
  sich, committen ineinander, bauen gleichzeitig. Default ist deshalb
  seriell. Willst du davon abweichen, brauchst du einen *benannten*
  Isolationsmechanismus (getrennte Worktrees, nur lesende Rollen) —
  Zuversicht ist keiner.
- **Drift.** Je weiter ein Plan vom Zeitpunkt seiner Ausführung entfernt
  ist, desto weniger stimmt er. Planung, die den echten aktuellen
  Codestand nicht gesehen hat, plant gegen eine Prognose.
- **Duplikate durch Blindheit.** Neue Struktur bauen, wo eine passende
  schon existiert — der klassische Folgeschaden von Vorausplanung.
- **Unscharfe Rollen.** Zwei Rollen, die dasselbe mit anderen Worten
  prüfen, kosten doppelt und finden dasselbe. Eine Rolle, die ihr eigenes
  Ergebnis abnimmt, nimmt alles ab.
- **Isolierte Sessions.** Subagenten sehen deinen Gesprächsverlauf nicht.
  Was sie wissen müssen, muss in ihrem Prompt oder in einer Datei stehen.
- **Resume.** Die Session kann jederzeit enden. Was ein Neustart braucht,
  steht in Dateien oder ist weg.
- **Token-Kosten.** Jeder Absatz, der in vielen Aufrufen mitgelesen wird,
  wird oft bezahlt. Kürzen darfst du die Darstellung, nie die Prüfung.
