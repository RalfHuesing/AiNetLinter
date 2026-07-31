---
workflow: dynamic-loop
status: poc
---

# dynamic-loop (PoC)

Gegenentwurf zu [`drift-loop/`](../drift-loop/README.md): Statt Rollen,
Prompts und Zerlegung **vorab** festzuschreiben, entwirft der Orchestrator
sie **zur Laufzeit** für den konkreten Task — auf Basis von `konzept.md`,
den Projektregeln und dem echten Codestand.

Festgeschrieben ist nur [`kernel.md`](kernel.md), und der zerfällt in
zwei Teile mit unterschiedlicher Verbindlichkeit:

- **Teil A — harte Regeln** (7). Deckel, „wer prüft fixt nicht", Tests
  müssen fehlschlagen können, nichts Unwiederbringliches, im Zweifel
  fragen, `konzept.md`/Projektregeln nur lesbar, Kernel unantastbar.
- **Teil B — benannte Gefahren.** Kollisionen, Drift, Duplikate,
  unscharfe Rollen, isolierte Sessions, Resume, Token-Kosten. Stichworte
  ohne Lösungsweg — die löst der Loop selbst.

**Das Kriterium für die Trennung:** Ein Fehler, der *laut* scheitert,
braucht keine Regel — der Agent merkt ihn und reagiert (Teil B). Ein
Fehler, der *still* oder *irreversibel* scheitert, braucht eine, weil die
Instanz, die ihn begeht, dieselbe wäre, die ihn bemerken müsste (Teil A).
Eine Testsuite ohne Tests ist von innen nicht von Erfolg zu
unterscheiden; ein Deckel, den der Loop selbst anheben darf, ist keiner;
ein an das Gebaute angepasstes `konzept.md` sieht hinterher völlig
konsistent aus.

## Starten

```
dynamic-loop/orchestrator.md <task-dir>
```

Gleicher Einstieg wie bei `drift-loop`: Im `<task-dir>` liegt
`konzept.md`, alles Weitere entsteht dort. Optional `konfig.md` für
abweichende Deckel.

**Angefangene Task-Verzeichnisse werden übernommen**, auch solche aus
`drift-loop` mit vorhandenen `step-NNN/`. Fremde Artefakte werden
gelesen und bleiben liegen — nichts wird konvertiert oder gelöscht,
fertige Arbeit nicht wiederholt. Status-Labels gelten dabei als
Behauptung: Was wirklich fertig ist, entscheidet `git log` und der Code,
nicht ein `status: done` im Frontmatter. Uncommittete Änderungen im
Working-Tree werden dir gezeigt, nicht stillschweigend mitcommittet oder
verworfen (Orchestrator Phase 0, Fall 3).

## Was der Loop selbst erzeugt

`<task-dir>/agents/*.md` — die Rollen-Prompts dieses Tasks. Sie sind
Artefakte wie jedes andere: committet, im `git log` nachvollziehbar, und
durch das **Meta-Review** (Orchestrator Phase 4) im Lauf änderbar, wenn
sich zeigt, dass eine Prüfrolle nur durchwinkt oder eine Rolle ihre
Aufrufe nicht verdient. Der Kernel bleibt dabei unantastbar (A7).

## Unterschied zu `drift-loop` in einer Zeile

`drift-loop` weiß vorher, wie gearbeitet wird, und ist dadurch
reproduzierbar. `dynamic-loop` entscheidet es unterwegs und ist dadurch
anpassungsfähig — auf Kosten der Reproduzierbarkeit: zwei Läufe über
dasselbe `konzept.md` können unterschiedliche Rollen hervorbringen.

## Status: PoC, ungetestet

Offen und bewusst noch nicht entschieden:

- **Trägt der Kernel?** Rund 130 Zeilen gegen ~2200 in `drift-loop`. Ob
  die weggelassenen Regeln fehlen, zeigt erst ein echter Lauf.
- **Ist die Grenze zwischen Teil A und B richtig gezogen?** „Scheitert
  laut vs. still" ist ein Kriterium, keine Messung. Serialität steht
  jetzt in Teil B — wenn der erste Lauf zwei Agenten gleichzeitig auf
  denselben Tree lässt, gehört sie zurück nach A.
- **Taugt das Meta-Review?** Eine Rolle, die den Flow kritisiert, kann
  auch zum Selbstbestätigungsapparat werden.
- **Wie gut sind selbstgeschriebene Rollen-Prompts?** Sie entstehen
  einmalig und blind zu Task-Beginn, ohne die Korrekturschleife, die
  handgeschriebene Prompts über mehrere Iterationen bekommen haben.

Der sinnvolle nächste Schritt ist kein weiterer Absatz hier, sondern
derselbe Task einmal durch beide Workflows — verglichen an Wall-Clock,
Fix-Runden und der entscheidenden Frage: **Hat der kurze Flow irgendwo
weitergemacht, wo der lange gestoppt hätte?**
