---
workflow: asimov-loop
status: poc
---

# asimov-loop (PoC)

Eine Datei, ein Prompt. Sieben Gesetze, die nicht verhandelbar sind —
alles andere entscheidet der Agent selbst: Rollen, Zerlegung, Artefakte,
Kommunikation.

```
asimov-loop/orchestrator.md <task-dir>
```

Im `<task-dir>` liegt `konzept.md`. Angefangene Verzeichnisse (auch aus
`drift-loop`) werden übernommen, nicht neu gestartet.

**Der Unterschied zu [`dynamic-loop/`](../dynamic-loop/README.md):** Dort
stehen die harten Regeln in einer eigenen `kernel.md` und der
Orchestrator beschreibt ein Verfahren (Phasen, Artefakt-Layout). Hier
gibt es kein Verfahren — nur die Gesetze und „mach, was sinnvoll ist".

**Der Test:** Reicht das? Die Gesetze decken die Fehler ab, die *still*
oder *irreversibel* scheitern — Budget ohne Zahl, Tests die nicht
fehlschlagen können, umgeschriebene Vorgaben, gelöschte Arbeit,
Selbstermächtigung. Alles andere scheitert laut genug, dass ein gutes
Modell es selbst merkt. Ob diese Annahme trägt, entscheidet ein echter
Lauf, kein weiterer Absatz hier.

**79 Zeilen** im Prompt (103 mit dieser Datei) gegen ~330 in
`dynamic-loop` und ~2200 in `drift-loop`.
