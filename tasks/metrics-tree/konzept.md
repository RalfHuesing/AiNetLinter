---
status: draft
type: konzept
project_kind: brownfield
estimated_scope: medium
rules_dir: .agents/rules
last_updated: 2026-08-08
open_questions: []
---

# Konzept: metrics_tree — interaktive Codebase-Landkarte (MCP-Tool)

## Ziel (Was)

Ein neues MCP-Tool `metrics_tree`, mit dem ein Agent sich Ebene fuer Ebene
durch die Verzeichnishierarchie einer Solution arbeiten kann, statt einen
kompletten Kontext-Dump zu bekommen. Pro Aufruf liefert das Tool eine
aggregierte Sicht auf einen Knoten (Datei-/LoC-Zahlen, Comment-Ratio,
Violation-Dichte oder Komplexitaet, je nach Modus) plus die Top-N-Kinder
darunter — der Agent bohrt gezielt tiefer, statt alles auf einmal zu
lesen.

## Warum / Kontext

Aus `tasks/features/05-roadmap.md` §3, Epic S2.5 (User-Idee 2026-08-06):
grösster ungedeckter Bedarf ist token-effiziente, drill-down-faehige
Exploration einer unbekannten/grossen Codebase — bestehende Tools decken
das nicht ab (`get_hotspots` ist flach, `get_file_skeleton` ist pro Datei,
kein aggregierter Hierarchie-Blick). Kombiniert CodeGraphs
`codegraph_files`-Idee, Aiders Repo-Map-Pattern und die bestehenden
CLI-`--map`-Subcommands (`MapCommand.cs`) als interaktives MCP-Tool.

**Hinweis zur Umsetzungsgranularitaet (User-Vorgabe 2026-08-08):** Der
Planer im `drift-loop` soll fuer diesen Task **grosse, in sich
geschlossene Code-Bloecke** ansetzen, keine Mini-Steps. Begruendung des
Nutzers: die Agenten (insbesondere der Coder) sind leistungsfaehig genug,
dass kleinteiliges Zerlegen nur Overhead erzeugt statt Sicherheit zu
gewinnen. Konkret **zwei Epics/Bloecke**, nicht mehr:

1. **Datei-Walk-Modi** (`code_size`, `comment_density`) + gemeinsamer
   ASCII-Tree-Renderer + Input-Parameter (`root`/`depth`/`top_n`/
   `file_filter`) + Sufficiency-Hinweis + Tool-Registrierung + Tests —
   in einem Block, nicht pro Modus/Datei einzeln aufgesplittet.
2. **Roslyn-Modi** (`violation_density`, `complexity`) auf demselben
   Renderer aufsetzend + Tests — zweiter Block.

Referenzgroesse fuer „gross": vergleichbar mit dem Zuschnitt von
`safeguard` (S1.2) — dort wurden Tool + Scanner + Score-Logik +
Remediation + Test-Suite als wenige grosse Schritte umgesetzt, nicht als
viele kleine. Der Planer bleibt an die uebliche Step-Groessen-Heuristik
(committable/review-faehig/in sich geschlossen) gebunden, soll sie aber am
oberen statt unteren Ende dieser Spanne anwenden — **jeder** der beiden
Bloecke ist trotzdem vollstaendig durch Tests abgesichert, das ist keine
Lockerung (siehe Test-Strategie unten).

## Scope

### Muss-Haben

- Neues Tool `metrics_tree` in `FileStructureToolRegistrations.cs`
  (analog zu `get_hotspots`/`get_file_skeleton`)
- **4 Modi** (reduziert aus den 5 in `05-roadmap.md:244` vorgeschlagenen —
  `method_count` bewusst weggelassen, siehe „Verworfene Alternativen"):
  `code_size`, `comment_density`, `violation_density`, `complexity`
- Input-Parameter: `root` (optional, default Workspace-Root), `mode`,
  `depth` (1-5, default 1), `top_n` (default 10), `file_filter`
  (optional, Regex)
- ASCII-Tree-Output mit aggregierten Werten pro Knoten + sortierten
  Top-N-Kindern (Sortierkriterium je nach Modus, siehe Roadmap)
- Sufficiency-Hinweis im Output (naechster sinnvoller Drill-down-Call)
- `code_size`/`comment_density`: reiner Datei-Walk (kein Roslyn),
  `violation_density`/`complexity`: ueber `LinterEngine`/
  `ComplexityCalculator`
- Bestehende CLI-`--map`-Subcommands bleiben unveraendert erhalten;
  `metrics_tree` ist die MCP-Variante, kein Ersatz
- Nach Fertigstellung: Epic S2.5 in `tasks/features/05-roadmap.md`
  (Zeile 103 Uebersichtstabelle + Zeile 211ff. Detail-Abschnitt) als
  `[x]` abhaken
- Walk-Kern von `GetHotspotsScanner.cs` generalisieren/extrahieren und
  fuer die Datei-Walk-Modi von `metrics_tree` wiederverwenden statt einer
  zweiten, unabhaengigen Walk-Implementierung (siehe „Entdeckte
  Maengel/Redundanzen")

### Nice-to-Have (Zwischenspeicher — vor `status: ready` aufgelöst)

- <noch keine>

### Non-Goals (bewusst NICHT Teil davon)

- Mermaid-/Grafik-Output: verworfen, Projekt-Standard ist ASCII-Tree fuer
  Hierarchien (Decision D10, `05-roadmap.md:501`)
- Neue Linter-Regeln/Patterns (das ist `pattern_detect`, S2.2 — separates
  Epic)

## Zielplattformen / Technischer Rahmen

Kein neuer Stack — reiner Ausbau des bestehenden .NET-10-MCP-Servers
(`src/AiNetLinter/Mcp/**`). Zwei Datenquellen je nach Modus: schneller
Datei-Walk (`Directory.EnumerateFiles` + Regex) fuer die zwei
Nicht-Roslyn-Modi, `LinterEngine`/`ComplexityCalculator` fuer die zwei
Roslyn-basierten Modi.

## Verworfene Alternativen

- **Ein Tool pro Modus (5 einzelne Tools) statt eines parametrisierten
  Tools:** nicht gewaehlt — waere gegen bestehende Tool-Anzahl-Sparsamkeit
  (`AIContextFootprint`, siehe Kommentar in `FileStructureToolRegistrations.cs`)
  und gegen D7 (keine Tool-Flut).
- **Mermaid/Graph-Visualisierung statt ASCII-Tree:** verworfen laut
  Decision D10 in `05-roadmap.md` — Konsistenz mit bestehendem
  Output-Format-Standard.
- **`method_count`-Modus (Anzahl Methoden, Ø LoC/Methode):** verworfen —
  aus Agenten-Sicht durchdacht (dieser MCP-Server laeuft im eigenen
  Projekt produktiv mit) liefert er kein Signal, das nicht schon
  `code_size` (LoC/Datei) oder `complexity` (CC/CogC, impliziert grob
  auch Methodengroesse) abdeckt. Der eigentliche Mehrwert eines
  "zu viele Methoden pro Klasse"-Signals waere eine God-Class-Erkennung —
  die ist bereits als eigenes Epic S2.2 `pattern_detect` geplant und dort
  praeziser (echte Schwellenwert-Regel statt Heatmap-Naeherung)
  umsetzbar. Vier gute Modi statt fuenf mit einem schwachen.

## Wo im Projekt

- `src/AiNetLinter/Mcp/FileStructureToolRegistrations.cs` — hier werden
  die dateistruktur-orientierten Tools registriert, `metrics_tree` reiht
  sich neben `get_hotspots`/`get_file_skeleton`/`get_index_scope` ein
- `src/AiNetLinter/Mcp/Tools/GetHotspotsTool.cs` +
  `GetHotspotsScanner.cs` — naechstliegendes bestehendes Muster fuer
  Datei-Walk + `scopeFilter`-Handling, moeglicher Wiederverwendungs-
  kandidat fuer die zwei Datei-Walk-Modi (siehe „Entdeckte
  Maengel/Redundanzen")
- `src/AiNetLinter/Metrics/ComplexityCalculator.cs` — Quelle fuer
  Ø CC/max CC/max CogC im `complexity`-Modus
- `src/AiNetLinter/Commands/MapCommand.cs` — bestehende CLI-`--map`-
  Subcommands, bleiben unveraendert (Decision D6/roadmap), aber relevant
  als Referenz fuer bereits vorhandene Aggregations-Logik
- `AiNetLinter.mdc` (Regeldatei fuer `LinterEngine`) — Quelle fuer
  Violation-Daten im `violation_density`-Modus

## Entdeckte Mängel/Redundanzen

- **Datei-Walk-Duplikation (potenziell)**
  - **Gefunden:** `GetHotspotsScanner.cs` implementiert bereits einen
    Verzeichnis-Walk mit `scopeFilter`-Filterung ueber die Solution —
    strukturell aehnlich zu dem, was die zwei Datei-Walk-Modi von
    `metrics_tree` brauchen (nur mit Aggregation pro Hierarchie-Ebene
    statt einer flachen Liste)
  - **Bezug:** kein expliziter Regel-Verstoss, aber
    `AiNetLinterRichtlinien.mdc` §1 „Einfachheit vor Abstraktion... nur
    dort wo sie echten Mehrwert liefert (Wiederverwendung +
    Verstaendlichkeit)"
  - **Vorschlag:** pruefen, ob der Walk-Kern aus `GetHotspotsScanner`
    extrahiert/generalisiert werden kann, statt eine zweite,
    unabhaengige Walk-Implementierung zu schreiben
  - **Entscheidung:** uebernommen ins Scope (→ siehe Muss-Haben,
    Block 1 „Datei-Walk-Modi" in „Hinweis zur Umsetzungsgranularitaet")

## Wie (grober Ansatz)

Ein Tool, zwei interne Pfade: schneller Datei-Walk fuer die zwei
nicht-Roslyn-Modi (aggregiert pro Verzeichnisebene bis `depth`), Roslyn-
Pfad ueber bestehende `LinterEngine`/`ComplexityCalculator`-Aufrufe fuer
die zwei Compile-basierten Modi. Ein gemeinsamer ASCII-Tree-Renderer
formatiert beide Ergebnistypen einheitlich (Knoten + sortierte
Top-N-Kinder + Sufficiency-Hinweis). Detailaufteilung (welche Klassen,
welche Methoden) ist Sache des Planers im `drift-loop` — hier nur die
grobe Skizze, siehe auch Hinweis zur Umsetzungsgranularitaet oben.

## Test-Strategie waehrend der Umsetzung (User-Vorgabe 2026-08-08)

Ein voller `dotnet test`-Lauf dauert auf der Entwicklungsmaschine
aktuell mehrere hundert Sekunden (beobachtet: >800s) — das darf im Loop
nicht pro Step/Commit anfallen, sonst wird der Task unpraktikabel
langsam (vgl. [[project-test-optimierung]], noch nicht umgesetzt).
**Explizite Abweichung vom Standard-Gate des Coder-Skills** (das dort
einen Volllauf vor **jedem** Commit vorsieht):

- **Waehrend beider Bloecke (Iteration):** gezielt/gefiltert testen —
  `dotnet test --filter Category=Unit` (~23-24s laut `AGENTS.md` §2) oder
  auf die neu/veraendert beruehrten Testklassen eingegrenzt. Das ist
  bereits dokumentierte Projekt-Konvention (`AGENTS.md` §2), keine
  Neuerfindung.
- **Vor jedem der zwei Block-Commits:** nur der gezielte/gefilterte Lauf
  ist Pflicht, **kein** vollstaendiger `dotnet test`.
- **Genau ein vollstaendiger `dotnet test`-Lauf fuer den gesamten Task**
  — erst ganz am Ende, nach Block 2, als Abschluss-Verifikation (analog
  `AGENTS.md` §2 Punkt 2 „Abschluss-Verifikation vor Task-Beendigung").
  Erst danach gilt der Task als fertig.
- **Tradeoff, den der Nutzer bewusst in Kauf nimmt:** eine Regression aus
  Block 1 faellt im ungünstigsten Fall erst beim Abschluss-Volllauf nach
  Block 2 auf, nicht schon direkt nach Block 1. Dafuer keine
  mehrfachen 800s-Wartezeiten waehrend der Umsetzung.

## Modellwahl fuer die drift-loop-Rollen (User-Vorgabe 2026-08-08)

Fuer den `task-state.md`-Config-Block bei Start von
`../drift-loop/orchestrator.md` (siehe dort Schritt 1 Fall A, Punkt 2):

- `model_planer`: Sonnet 5, Reasoning-Stufe High
- `model_coder`: Sonnet 5, Reasoning-Stufe Medium
- `model_kritiker`: Sonnet 5, Reasoning-Stufe Medium

## Definition of Done / Erfolgskriterien

- Tool `metrics_tree` mit allen 4 Modi ueber MCP aufrufbar
- ASCII-Tree-Renderer mit korrekter Top-N-Sortierung pro Modus
- `root`/`depth`/`top_n`/`file_filter` funktionieren wie spezifiziert
- Sufficiency-Hinweis im Output
- Ausreichende Testabdeckung (mind. 1 Test pro Modus + Edge-Cases: leeres
  Verzeichnis, single File, `depth=5`) + 1 Integrationstest auf dem
  Live-Repo (Dogfooding, siehe `AiNetLinterRichtlinien.mdc` §4)
- `dotnet build` (TreatWarningsAsErrors) gruen, gezielte Testlaeufe
  gruen pro Block, **ein** vollstaendiger `dotnet test`-Lauf am Ende des
  gesamten Tasks gruen (siehe Test-Strategie oben — kein Volllauf pro
  Step/Commit)
- Doku aktualisiert: `Docs/agent-api.md`, `Docs/ROADMAP.md`,
  ggf. `README.md`
- Bestehende CLI-`--map`-Subcommands unveraendert funktionsfaehig
- Epic S2.5 in `tasks/features/05-roadmap.md` als `[x]` abgehakt

## Offene Punkte

<keine>

Bereit fuer `../drift-loop/orchestrator.md tasks/metrics-tree`, sobald
der Nutzer `status: ready` bestaetigt.
