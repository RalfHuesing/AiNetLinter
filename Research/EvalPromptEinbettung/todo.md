# TODO: Externe MD-Dateien im Eval einbetten

## Kontext (Stand Code-Analyse)

`EvalAssembler` baut den Prompt aus drei Teilen:
1. Template (eingebettete Ressource `Docs/Evals/<name>.md`)
2. `{{SPEC}}` → roher Inhalt der `--spec`-Dateien via `SpecLoader` (mit `\n\n---\n\n` konkateniert)
3. `{{VOCABULARY_MAP}}` / `{{STRUCTURE_MAP}}` → Auto-generiertes Markdown aus den Map-Buildern

---

## Problem 1: Überschriften-Kollision

Der Template-Rahmen verwendet `#`, `##`, `---`. Die eingebetteten Spec-Dateien bringen
eigene `#`-Hierarchien mit. Für das LLM entstehen damit Heading-Ebenen, die visuell
Geschwister des Template-Rahmens sind, aber inhaltlich Unterdokumente sein sollen.

**Optionen:**

- ` ```markdown ``` ` Fenced Code Block
  - Pro: Headings isoliert, kein Clash
  - Con: Claude behandelt Code-Blöcke als Artefakt, nicht als Aufgaben-Kontext — wahrscheinlich schlechter
- **XML-Tags** (`<spec>...</spec>`, `<doc name="README.md">...</doc>`)
  - Pro: Claude verarbeitet diese nativ als Kontext-Container; de-facto Standard in Anthropic-Prompts
  - Con: Leicht ungewohnt für menschliche Leser des Prompts
  - **→ Wahrscheinlich beste Option**
- Heading-Offset (alle Spec-Headings um N Ebenen nach unten schieben, `#` → `###`)
  - Pro: Lesbar im Prompt-MD
  - Con: Aufwändig zu implementieren, fragil bei tiefen Hierarchien

---

## Problem 2: `---`-Separator-Konflikt

`SpecLoader` trennt mehrere Spec-Dateien mit `\n\n---\n\n`.
Das Template selbst verwendet ebenfalls `---` als Abschnitts-Trenner.
Das LLM kann die Grenze zwischen Template-Abschnitten und Spec-Inhalt nicht sicher erkennen.

**Lösung:** XML-Tags würden dieses Problem gleich mit lösen — kein `---` nötig.

---

## Problem 3: Reihenfolge der Abschnitte

Aktuelle Template-Reihenfolge:
1. Rolle / System-Instruktion
2. `{{SPEC}}` (Domänen-Spezifikation)
3. `{{VOCABULARY_MAP}}` / `{{STRUCTURE_MAP}}` (Evidence aus Code)
4. "Deine Aufgabe" (Task-Instruktion)

Frage: Sollte die Task-Instruktion **vor** den Daten stehen (Frage zuerst, dann Kontext)?
→ Bei langen Docs (Spec + Map) geht die eigentliche Aufgabe unter.
→ Anthropic-Best-Practice: Instruktion zuerst, dann Daten — prüfen ob das hier besser ist.

---

## Problem 4: Token-Budget

Spec-Dateien können groß sein. Map-Output (VocabularyMap, StructureMap) ist ebenfalls
unkontrolliert groß. Zusammen leicht 10k–50k+ Tokens.

- `StructureMapBuilder` hat bereits einen `maxLineCount`-Parameter — aber wird er ausreichend begrenzt?
- Für Spec-Dateien gibt es keine Begrenzung
- Überlegung: Warnung ausgeben wenn assemblierter Prompt einen Schwellwert überschreitet

---

## Feature: Konkrete Handlungsempfehlungen im Prompt

Aktuell: "Deine Aufgabe" beschreibt Analyse-Schritte, aber kein explizites Output-Format
mit Prioritäten.

**Ziel:** Prompt soll das Modell anweisen, nach der Analyse eine priorisierte
Empfehlungsliste auszugeben.

Vorschlag für Output-Abschnitt im Template:

```
## Empfehlungen (Pflichtformat)

| Priorität | Befund | Empfehlung | Aufwand |
|-----------|--------|------------|---------|
| P1 - Sofort | ... | ... | Klein/Mittel/Groß |
| P2 - Bald   | ... | ... | ... |
| P3 - Später | ... | ... | ... |
```

- P1 = blockiert Qualitätsziele oder erzeugt aktiv Drift
- P2 = wichtig, aber kein unmittelbarer Schaden
- P3 = nice-to-have / langfristig

Alternativ: MoSCoW (Must/Should/Could/Won't) — einfacher für nicht-technische Leser.

---

## Nächste Schritte

- [ ] XML-Tag-Ansatz in einem Beispiel-Prompt ausprobieren und vergleichen
- [ ] `SpecLoader` anpassen: Dateien einzeln in `<doc name="...">` wickeln statt `---`-Trenner
- [ ] Template-Reihenfolge testen: Task-Instruktion vor den Daten vs. nach den Daten
- [ ] Token-Warnung einbauen (einfach: `prompt.Length / 4 > Schwellwert`)
- [ ] "Empfehlungen"-Abschnitt in beide Templates (`naming-drift.md`, `architecture-intent.md`) ergänzen
- [ ] Output-Format im Template festlegen (Tabelle mit Prio-Spalte)
