---
workflow: dynamic-loop
status: poc
---

# dynamic-loop (PoC)

Schlanker Gegenentwurf zu [`drift-loop/`](../drift-loop/README.md): feste
Rollen (Planer, Coder, Kritiker), aber ein kurzer Kernel aus harten
Regeln statt einer ~2200-Zeilen-Spezifikation mit Roadmap/Epics,
Batch-Steps und Modell-Zuweisung.

```
dynamic-loop/orchestrator.md <task-dir>
```

Im `<task-dir>` liegt `konzept.md`. Angefangene Verzeichnisse (auch aus
`drift-loop`) werden übernommen, nicht neu gestartet.

## Vorgeschichte: warum es diese Datei so und nicht anders gibt

Diese Loop ist die zweite Fassung. Die erste bestand aus zwei getrennten
Experimenten: `dynamic-loop` (Rollen entstehen zur Laufzeit, Kernel als
harte Grenze) und `asimov-loop` (kein Verfahren, nur sieben Gesetze).
Beide waren der Versuch, `drift-loop`s Umfang zu hinterfragen — inspiriert
von [Claude-of-Duty](https://github.com/mshumer/Claude-of-Duty), dessen
Prompt Subagenten weitgehend frei lässt.

Recherche dazu (vollständig in
[`../../docs/references.md`](../../docs/references.md), Abschnitt
2026-08-01) hat beide Experimente relativiert, statt sie zu bestätigen:

- **Rollen zur Laufzeit erfinden lassen ist noch nicht zuverlässig
  belegt.** [The Meta-Agent Challenge](https://arxiv.org/abs/2606.04455)
  zeigt für aktuelle Frontier-Modelle hohe Varianz und teils Reward-
  Hacking, wenn sie sich selbst eine Agenten-/Rollenstruktur bauen
  sollen. [MetaGPT](https://arxiv.org/abs/2308.00352) — das etablierteste
  Multi-Agent-Framework mit festen Rollen + SOP-Artefakten — nennt
  dynamische Rollenwahl explizit als *zukünftige*, nicht als heute
  erprobte Richtung. Und selbst der Namensgeber
  [Claude-of-Duty](https://github.com/mshumer/Claude-of-Duty) trägt die
  radikale Lesart nur bedingt: sein eigentlicher Bauplan ist
  `ARCHITECTURE.md`, ein festes Vertragsdokument, gegen das alle
  Subagenten arbeiten — näher an `drift-loop`s festen Artefakten als an
  „kein Verfahren". Deshalb hier: **feste Rollen**, keine Laufzeit-
  Erfindung mehr.
- **Wenig Verfahrenstext + Modell-Urteil statt vieler Einzelregeln ist
  dagegen gut belegt** — aber an die Fähigkeit des jeweiligen Modells
  gekoppelt, nicht daran, wie die Rollen zustande kommen. Anthropic hat
  für Claude Opus/Fable 5 [80 % des Claude-Code-Systemprompts
  gestrichen](https://www.developersdigest.tech/blog/claude-5-context-engineering-rules-hn-analysis)
  („rules become judgment") ohne Eval-Verlust, und
  [Claude's Constitution](https://www.aigl.blog/claudes-constitution/)
  ist strukturell fast deckungsgleich mit diesem Kernel: harte
  Constraints plus abgestufte Priorität für den Rest, statt einer langen
  Regel-Liste. Deshalb hier: **kurzer Kernel** bleibt, statt zurück zu
  `drift-loop`s vollem Verfahren.
- **Harte Zahlen-Deckel bleiben Teil A, nicht Teil B.** Nicht jedes Modell
  bremst sich beim „noch besser machen" selbst — insbesondere manche
  nicht-westlichen Modelle neigen dazu, Linter-Meckerei oder eigene
  Commits immer weiter nachzupolieren, statt den Task voranzutreiben. Das
  scheitert *still* (sieht wie Sorgfalt aus, kostet aber Budget für nichts
  Angefordertes) — genau das Kriterium, das in diesem Workflow einen
  Platz in Teil A statt Teil B verlangt. Daraus folgt A5 („Fertig ist
  fertig") als neue Regel.

**`asimov-loop/` ist damit entfallen** — seine beiden tragenden Ideen
(Kürze, harte Gesetze statt Verfahren) leben in diesem Kernel weiter,
seine dritte Idee (kein Verfahren, keine festen Rollen) nicht mehr.

## Unterschied zu `drift-loop` in einer Zeile

`drift-loop` legt Roadmap-Mechanik, Batch-Steps, Kritiker-Ebenen-Details,
Git-Konventionen und Modell-Zuweisung explizit fest — reproduzierbar,
aber ausführlich. `dynamic-loop` gibt dieselben drei Rollen und denselben
JIT-Grundgedanken vor, überlässt das *Wie* innerhalb der Kernel-Grenzen
aber dem Modell-Urteil der jeweiligen Session.

## Status: PoC, ungetestet

Offen und bewusst noch nicht entschieden:

- **Trägt der kürzere Kernel trotz fester Rollen?** Die Rollenfrage ist
  jetzt durch Literatur gestützt beantwortet, die Frage „wie viel
  Verfahrenstext braucht selbst eine feste Rolle" nicht — das zeigt erst
  ein echter Lauf im Vergleich zu `drift-loop`.
- **Ist A5 („Fertig ist fertig") die richtige Grenze?** Sie ist aus einer
  konkreten Beobachtung entstanden (bestimmte Modelle polieren
  Commits/Linter-Meckerei endlos nach), nicht aus Literatur — ob sie zu
  eng oder zu weit gefasst ist, zeigt sich erst im Einsatz mit
  unterschiedlichen Modellen.
- **Ist die Grenze zwischen Teil A und B sonst richtig gezogen?**
  „Scheitert laut vs. still" ist ein Kriterium, keine Messung.

Der sinnvolle nächste Schritt ist kein weiterer Absatz hier, sondern
derselbe Task einmal durch `drift-loop` und `dynamic-loop` — verglichen an
Wall-Clock, Fix-Runden und Token-Verbrauch.

**521 Zeilen** in `kernel.md` + `orchestrator.md` + `agents/*.md` (621 mit
dieser Datei) gegen ~2200 in `drift-loop/spec.md` allein.
