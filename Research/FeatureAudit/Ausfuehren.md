# Ausführungsanleitung — AiNetLinter Feature Audit

**Letzte Aktualisierung:** 2026-06-20  
**Gesamtaufwand:** 7–9 Agent-Runs (je 15–40 Min pro Run)

---

## Warum iterativ und nicht alles auf einmal?

Ein Research-Agent der alle 57 Tasks in einem Lauf erledigt würde das Kontextfenster sprengen — besonders durch die akkumulierten WebSearch-Ergebnisse aus Phase 1. Außerdem ist die Qualität pro Feature besser wenn der Agent fokussiert arbeitet, statt 50 Dinge gleichzeitig im Kopf zu halten.

**Faustregel:** Ein Agent-Run = eine Phase (oder ein halbe Phase bei größeren). Jeder Run startet frisch, liest Task.md für den aktuellen Stand und arbeitet nur den definierten Scope ab.

Das Modell: **Claude claude-sonnet-4-6** reicht für Phase 2–6. Für Phase 1 (viele WebSearches + komplexe Synthese) empfehle ich **claude-opus-4-8** für bessere Quellenqualität — aber Sonnet funktioniert auch.

---

## Übersicht: Alle Runs auf einen Blick

| Run | Phase | Scope | Erwartete Dateien | Schätzzeit |
|-----|-------|-------|-------------------|------------|
| 1 | Phase 1a | Paper-Cluster A, B, C, D | 4 Dateien in `temp\papers\` | 20–35 Min |
| 2 | Phase 1b | Paper-Cluster E, F, G, H | 4 Dateien in `temp\papers\` | 20–35 Min |
| 3 | Phase 2a | Metriken M01–M09 | 9 Dateien in `Result\metrics\` | 25–40 Min |
| 4 | Phase 2b | Metriken M10–M17 | 8 Dateien in `Result\metrics\` | 20–35 Min |
| 5 | Phase 3a | Boolean-Regeln R01–R10 | 10 Dateien in `Result\bool-rules\` | 25–40 Min |
| 6 | Phase 3b | Boolean-Regeln R11–R20 | 10 Dateien in `Result\bool-rules\` | 25–40 Min |
| 7 | Phase 4 | System-Features F01–F09 | 9 Dateien in `Result\features\` | 20–30 Min |
| 8 | Phase 5+6 | Index + Neue Feature-Vorschläge | `Result\index.md` + `Result\new-features\` | 30–45 Min |

**Gesamt:** ~3–5 Stunden (mit Pausen zwischen Runs)

---

## Vorbereitung (einmalig)

Stelle sicher dass der Agent Zugriff auf WebSearch hat. Falls du Claude Code verwendest:
```
claude --model claude-opus-4-8
```

Das Repo-Root muss das Arbeitsverzeichnis des Agenten sein (`C:\Daten\Entwicklung\Ralf\AiNetLinter`). Alle Pfade in den Prompts sind relativ dazu.

---

## Run 1 — Phase 1a: Paper-Cluster A bis D

**Starter-Prompt (kopieren und einfügen):**

```
Lies Research\FeatureAudit\Prompt.md vollständig, insbesondere Abschnitt 3 (Ablauf) und Abschnitt 4 (Paper-Template).

Deine Aufgabe in diesem Run: Erstelle ausschließlich die Paper-Cluster A, B, C und D.
Speicherorte:
  temp\papers\papers-A-komplexitaet.md
  temp\papers\papers-B-groessen.md
  temp\papers\papers-C-llm-agenten.md
  temp\papers\papers-D-csharp.md

Verwende für jeden Cluster die Suchqueries aus Prompt.md. Schreibe vollständige Zusammenfassungen nach dem Template aus Abschnitt 4. Hake nach jeder Datei die Checkbox in Research\FeatureAudit\Task.md ab.

Stoppe nach Cluster D. Starte Cluster E noch nicht.
```

**Verifikation nach Run 1:**
- [ ] `temp\papers\papers-A-komplexitaet.md` existiert und ist nicht leer
- [ ] `temp\papers\papers-B-groessen.md` existiert und ist nicht leer
- [ ] `temp\papers\papers-C-llm-agenten.md` existiert und ist nicht leer
- [ ] `temp\papers\papers-D-csharp.md` existiert und ist nicht leer
- [ ] Task.md: Cluster A–D abgehakt

---

## Run 2 — Phase 1b: Paper-Cluster E bis H

**Starter-Prompt:**

```
Lies Research\FeatureAudit\Prompt.md vollständig, insbesondere Abschnitt 3 und 4.

Prüfe Research\FeatureAudit\Task.md: Cluster A–D müssen abgehakt sein.

Deine Aufgabe in diesem Run: Erstelle die Paper-Cluster E, F, G und H.
Speicherorte:
  temp\papers\papers-E-architektur.md
  temp\papers\papers-F-smells.md
  temp\papers\papers-G-tests.md
  temp\papers\papers-H-meta-hypothese.md

Cluster H ist besonders wichtig: Er recherchiert ob AiNetLinter-Compliance tatsächlich die Agenten-Performance verbessert — die Meta-Frage des gesamten Audits. Sei hier gründlich.

Hake nach jeder Datei die Checkbox in Task.md ab. Stoppe nach Cluster H. Starte Phase 2 noch nicht.
```

**Verifikation nach Run 2:**
- [ ] Alle 8 `temp\papers\papers-[X]-*.md` Dateien existieren
- [ ] Task.md: Alle Phase-1-Checkboxen abgehakt
- [ ] papers-H enthält explizite Aussage ob Meta-Hypothese belegt, nicht belegt oder offen ist

---

## Run 3 — Phase 2a: Metriken M01 bis M09

**Starter-Prompt:**

```
Lies Research\FeatureAudit\Prompt.md vollständig, insbesondere die Templates in Abschnitt 5a.
Lies Research\FeatureAudit\FeatureList.md vollständig.

Prüfe Task.md: Alle Phase-1-Checkboxen müssen abgehakt sein.

Deine Aufgabe: Evaluiere die Metriken M01 bis M09 (nur diese neun).
Für jede Metrik:
  1. Lies die relevanten Paper-Cluster (aus FeatureList.md → "Relevante Paper-Cluster")
  2. Schreibe die Result-Datei nach Template 5a aus Prompt.md
  3. Hake die Checkbox in Task.md ab

Zieldateien:
  Result\metrics\M01-MaxLineCount.md bis M09-MaxDirectoryDepth.md

Stoppe nach M09. Starte M10 noch nicht.
```

**Verifikation nach Run 3:**
- [ ] 9 Dateien in `Result\metrics\` vorhanden (M01–M09)
- [ ] Jede Datei enthält Bewertung (🟢/🟡/🔴) + Empfohlene Range + Zeitliche Einordnung

---

## Run 4 — Phase 2b: Metriken M10 bis M17

**Starter-Prompt:**

```
Lies Research\FeatureAudit\Prompt.md (Abschnitt 5a Template) und FeatureList.md.

Prüfe Task.md: M01–M09 müssen abgehakt sein.

Evaluiere die Metriken M10 bis M17 (diese acht):
  Result\metrics\M10-MaxDirectoryChildren.md bis M17-CompoundSuppressions.md

Selbes Vorgehen wie bei Run 3: Paper-Cluster lesen → Result schreiben → Task.md abhaken.
Stoppe nach M17.
```

**Verifikation nach Run 4:**
- [ ] 17 Dateien in `Result\metrics\` vorhanden (M01–M17)
- [ ] Task.md: Alle Phase-2-Checkboxen abgehakt

---

## Run 5 — Phase 3a: Boolean-Regeln R01 bis R10

**Starter-Prompt:**

```
Lies Research\FeatureAudit\Prompt.md (Abschnitt 5b Template) und FeatureList.md.

Prüfe Task.md: Phase 2 vollständig abgehakt.

Evaluiere die Boolean-Regeln R01 bis R10 (erste Hälfte):
  Result\bool-rules\R01-EnforceSealedClasses.md bis R10-EnforceXmlDocumentation.md

Auch deaktivierte Regeln (Status: Deaktiviert) vollständig evaluieren — es könnte sich lohnen sie zu aktivieren.
Für jede Regel: Paper-Cluster lesen → Result schreiben (Template 5b) → Task.md abhaken.
Stoppe nach R10.
```

**Verifikation nach Run 5:**
- [ ] 10 Dateien in `Result\bool-rules\` vorhanden (R01–R10)

---

## Run 6 — Phase 3b: Boolean-Regeln R11 bis R20

**Starter-Prompt:**

```
Lies Research\FeatureAudit\Prompt.md (Abschnitt 5b Template) und FeatureList.md.

Prüfe Task.md: R01–R10 müssen abgehakt sein.

Evaluiere die Boolean-Regeln R11 bis R20 (zweite Hälfte):
  Result\bool-rules\R11-EnforceSemanticNaming.md bis R20-BanPublicNestedTypes.md

Vorgehen wie Run 5. Stoppe nach R20.
```

**Verifikation nach Run 6:**
- [ ] 20 Dateien in `Result\bool-rules\` vorhanden (R01–R20)
- [ ] Task.md: Alle Phase-3-Checkboxen abgehakt

---

## Run 7 — Phase 4: System- und CLI-Features F01 bis F09

**Starter-Prompt:**

```
Lies Research\FeatureAudit\Prompt.md (Abschnitt 5c Template) und FeatureList.md.

Prüfe Task.md: Phase 3 vollständig abgehakt.

Evaluiere alle System-Features F01 bis F09:
  Result\features\F01-Baseline-Ratchet.md bis F09-EnablePerformanceProfiling.md

Nutze für jedes Feature das Template 5c. Vergleiche explizit mit anderen Linter-Tools (SonarQube, ESLint, StyleCop, NDepend).
Hake nach jeder Datei Task.md ab. Stoppe nach F09.
```

**Verifikation nach Run 7:**
- [ ] 9 Dateien in `Result\features\` vorhanden (F01–F09)
- [ ] Task.md: Alle Phase-4-Checkboxen abgehakt

---

## Run 8 — Phase 5 + 6: Index + Neue Feature-Vorschläge

**Starter-Prompt:**

```
Lies Research\FeatureAudit\Prompt.md vollständig (besonders Abschnitte 5d, 6 und Phase 6 in Abschnitt 3).

Prüfe Task.md: Alle Phasen 1–4 müssen vollständig abgehakt sein.

Deine Aufgabe in diesem Run — zwei Teile:

TEIL A — Phase 5: Erstelle Result\index.md.
Lies dazu alle Result-Dateien (metrics, bool-rules, features) und fasse zusammen: Bewertungsmatrix, Top-Empfehlungen, offene Fragen. Template in Abschnitt 6 von Prompt.md.

TEIL B — Phase 6: Neue Feature-Vorschläge.
Recherchiere welche C#-Code-Muster NICHT von AiNetLinter abgedeckt sind, aber empirisch als LLM-Problem belegt. Fokus auf:
  - Async/await-Anti-Patterns (async void, .Result/.Wait(), ConfigureAwait)
  - LINQ-Komplexität (lange Chains)
  - Was aus papers-C als Halluzinations-Ursache benannt wurde ohne passendes Feature
Erstelle zuerst Result\new-features\proposals.md (Übersicht), dann je eine Datei pro Vorschlag mit starker Evidenz. Template in Abschnitt 5d.

Hake alle verbleibenden Checkboxen in Task.md ab.
```

**Verifikation nach Run 8:**
- [ ] `Result\index.md` existiert mit vollständigen Matrizen
- [ ] `Result\new-features\proposals.md` existiert
- [ ] Task.md: Alle 57 Checkboxen abgehakt

---

## Was tun wenn ein Run abbricht?

1. `Task.md` öffnen — alle abgehakten Items sind erledigt und müssen nicht wiederholt werden
2. Den nächsten Run mit dem entsprechenden Starter-Prompt starten
3. Im Starter-Prompt die Einschränkung auf "ab Feature X" anpassen wenn nötig

Beispiel für Wiederaufnahme mitten in Run 5 nach Abbruch bei R06:
```
Lies Prompt.md (Abschnitt 5b) und FeatureList.md.
Prüfe Task.md: R01–R05 sind bereits abgehakt.
Evaluiere R06 bis R10. Starte bei R06.
```

---

## Was tun wenn ein Paper-Cluster zu dünn ist?

Wenn du bei einer Feature-Evaluation merkst dass ein Cluster wenig Substanz hat: Starte einen Mini-Run:
```
Lies temp\papers\papers-[X]-*.md.
Ergänze den Cluster um [spezifisches Thema]. Suchquery: [...]
Hänge die neuen Quellen an die bestehende Datei an.
```

---

## Nach dem Audit: Extensions

Für neue Fragen die nach dem Audit entstehen: Lies `Extensions\README.md`.  
Template anlegen, Paper-Cluster prüfen ob vorhanden, dann einen separaten Agent-Run starten.

**Typische Dauer für eine Extension:** 1 Run, 15–25 Min.

---

## Checkliste für den Start

- [ ] Arbeitsverzeichnis ist `C:\Daten\Entwicklung\Ralf\AiNetLinter`
- [ ] Agent hat WebSearch-Zugriff
- [ ] `Research\FeatureAudit\Task.md` zeigt 0 abgehakte Items
- [ ] `Research\FeatureAudit\temp\papers\` ist leer
- [ ] `Research\FeatureAudit\Result\` Unterordner sind alle leer
- [ ] Bereit für Run 1
