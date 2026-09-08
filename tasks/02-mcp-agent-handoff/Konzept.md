---
status: ready
type: konzept
project_kind: brownfield
estimated_scope: large
execution_mode: autonomous
rules_dir: .agents/rules
last_updated: 2026-09-07
open_questions: []
depends_on:
  - tasks/01-mcp-unified-analysis-target/Konzept.md
  - tasks/ainetlinter-mcp-usage-audit/shared/Befundmatrix.md
  - tasks/ainetlinter-mcp-usage-audit/shared/MCP-Verbesserungsrahmen.md
related_tasks:
  - tasks/03-mcp-development-workflow/Konzept.md
  - tasks/04-mcp-assembly-quality/Konzept.md
supersedes: null
---

# Task 02: MCP-Agenten-Handoff und bounded Navigation

## Ziel und Problem

Ein Agent findet in einem unbekannten Repository den passenden Scope, ein
Symbol und einen tatsächlich kopierbaren Folgeinput. Heute unterscheiden
`get_file_tree`, `get_index_scope`, `search_pattern`, Symbolgraph und
Strukturtools Pfade, IDs, Ranking und Truncation nicht durchgängig; plausible
Zeilen können als falscher Handoff missverstanden werden.

## Primärer Agentennutzen

Primärer Agentennutzen: Discovery → Scope → Suche → Body/Struktur →
Referenzen/Call-Tree funktioniert in kleinen, ehrlichen Antworten. Der Task
hat einen eigenständigen Releasewert und verändert keine Analyseheuristiken
oder Quality Gates.

## Source of Truth und betroffene Bereiche

- Audit: Pakete 01–03 der Befundmatrix, vor allem `get_file_tree`,
  `search_pattern`, `find_symbol`, `find_references`, `get_call_tree`,
  `get_*structure`, `find_implementations` und die Discovery-/ID-Combos.
- Rahmen: Status, Handoff, Scope, Budget und Continuation.
- Code: `Tools/FileStructure/*`, `SearchPatternScanner`, Symbolgraph,
  `AnalysisSymbolIdentity`, `SymbolIdentifierResolver`, Truncation,
  Formatter und Registrierungs-/Raw-Wire-Tests.

## Scope

- Einen ausführbaren Contract-Katalog aus SDK-Schema, Runtimevalidierung und
  Beschreibung erzeugen; Toolkatalog, Einzeltool, Required-Felder, Enums und
  Capability sind dieselbe Wahrheit.
- Pfad-/Glob-/Produktions-Test-Semantik zwischen File Tree und Textsuche
  angleichen. Root-Discovery liefert korrekte Eltern, Summary/Tree-Defaults
  und einen maschinenlesbaren C#/Nicht-C#-Routinghint.
- Canonical DocComment-IDs für Quelle und (bei gemeinsamer Navigation) die
  Rahmen-Assemblyidentität ausgeben. Jede Zeile erklärt `handoff=true|false`,
  Ziel und erlaubte Folge-Tools. Partial-Typen und Aliase verlieren keine
  Memberidentität.
- Für Suche, Namespace, References, Call-Tree, Implementations und Strukturen
  Counts, Scope, Rankinggrund, `truncatedBy` und nur nötige Drilldowns
  vereinheitlichen. Keine Pauschal-Pagination und keine Diagnoseverdopplung.

## Muss-Kriterien

- Eine ausgegebene Handoff-ID funktioniert im versprochenen Folgecall;
  target-fremd, ambig und stale sind unterscheidbar.
- C#, Razor und JavaScript erhalten einen nachvollziehbaren Toolpfad. Ein
  Text-Miss wird nicht als C#-Symbolleermenge ausgegeben.
- `empty`, `complete`, `partial` und `truncated` sind pro Ergebnismenge
  wahr; Text und StructuredContent enthalten identische IDs, Counts und
  Folgeschritte.
- Kleine Defaults bevorzugen Produktion und direkt handlungsfähige Treffer;
  Test-/Artefakt-/Diagnosemengen sind sichtbar getrennt.

## Messbare Akzeptanzkriterien

- Für mindestens fünf Source- und zwei Assembly-Handoff-Ketten kopiert ein
  Raw-Wire-Test IDs ohne Parsen des Markdown in Body, Structure, References
  oder Call-Tree; jede erwartete Ablehnung prüft ihren Fehlercode.
- Root Discovery bleibt unter 8 KiB, zeigt höchstens 20 Primärzeilen und
  respektiert den Rahmen. Große Datenmenge erhält Count, Ursache und genau
  einen validen Drilldown; ein Cursor nur bei deterministischer Ordnung.
- Schema-Snapshot, Runtime-Validator und Toolbeschreibung werden je betroffener
  Registration gegen denselben Contract-Test geprüft.

## Non-Goals

Keine Targetmigration, Regeln-/Lintänderung, umfassende Such-Recall-Neuheit,
neue Assembly-Lifecyclelogik, Qualitätsheuristik oder globaler Seitenbrowser.

## Architekturannahmen

Der Contract-Kern des Rahmens ergänzt typisierte Toolpayloads; Scanner bleiben
bei ihren Tools. Die ID-Projektion darf nicht bloß im Formatter entstehen,
sondern muss vor Text und StructuredContent dieselbe Aggregation verwenden.

## Fehler-, Fallback- und Lebenszeitsemantik

Ein Resultat ohne Treffer ist nur dann `empty`, wenn sein expliziter Scope
vollständig geprüft wurde. Nicht indexierbarer Nicht-C#-Content führt zum
Textsuche-Hint, nicht zu `unsupported`; Capability-Mismatch bleibt
`unsupported`. IDs tragen Target-Fingerprint; ein stale Snapshot verweist auf
neuen Such-/Detailcall. Continuations binden Query, Filter, Sortierung und
Snapshot und verfallen bei Abweichung explizit.

## Abhängigkeiten

Task 01 liefert den einzigen Target- und Statusvertrag. Task 03 übernimmt IDs
und Scope für Impact/Lint, Task 04 erweitert nur den Assemblyteil, ohne ihn
erneut zu definieren.

## Risiken und Sackgassen

Ein universeller Envelope oder Cursor würde Toolnutzen verschleiern. Deshalb
gibt es nur den schmalen Rahmenkern und Continuation nur nach sichtbarer,
stabiler Reihenfolge. ID-Stabilität darf keine falsche Cross-Target-
Gleichsetzung bedeuten.

## Alternativen mit Konsequenzen

- Nur `find_symbol` reparieren: schnelle Demo, aber Discovery und Member-
  Handoff bleiben unsicher.
- Alle Tools mit Cursor ausstatten: technisch gleichförmig, für Agenten
  tokenintensiv und häufig ohne fachliche Ordnung.
- IDs nur im Text dokumentieren: bricht bei Hosts mit StructuredContent und
  ist deshalb verworfen.

## Konkrete Agenten-Szenarien

| Fall | Call / muss enthalten / darf nicht behaupten | Nächster Schritt |
| --- | --- | --- |
| A: unbekanntes Repo | `get_file_tree` Summary → `get_index_scope` liefert Scope/Routing → `find_symbol` oder `search_pattern`; jeder Treffer hat Handoff-ID oder `handoff=false`. | Body/Struktur und dann Richtungscall mit derselben ID. |
| C: große Menge | `find_symbol`/Tree liefert gerankte 20, `totalCount`, getrennte Testcounts, `truncatedBy` und einen gebundenen Drilldown. | Scope/Pattern verfeinern oder zulässige Continuation. |
| F: stale ID | Detailcall meldet `stale_snapshot` mit ursprünglichem Target und Wiedereinstieg. | Such-/Katalogcall wiederholen, nicht ID auf anderes Target kleben. |

## Token-/Antwortbudget-Annahmen

Die Rahmenlimits gelten. Tabellen geben nur Primärtreffer, ID, Art, relativen
Ort und Rankinggrund aus; Bodies und Diagnosen sind Detailcalls. Ein Composite
verweist statt Caller/Body mehrfach zu rendern auf den Detailcall.

## Verifikation

Fast-, Integration-, Schema- und Raw-Wire-Folgecall-Tests, C#/Razor/JS-Proben,
Root-/Glob-/Empty-/Truncation-Proben, frischer Dogfood-Call sowie die
gemeinsamen Build-, Nicht-Stress-, Diff- und Dokumentationsgates.

## Dokumentationsbedarf

Agent API und MCP-Instructions erhalten eine kurze Discovery-Tabelle,
ID-/Scope-Regeln, die präzise Empty-/Truncation-Semantik und Beispiele für
einen Folgecall, aber keine vollständigen Rohschemas.

## Release-Gate

Ein nicht kopierbarer als Handoff deklarierter Wert, widersprüchliches
StructuredContent oder eine vermeintlich vollständige gekürzte Menge blockiert
den Release.

## Nächste fachliche Entscheidung

Nach Release anhand eines unbekannten realen Repositories prüfen, ob 20
Primärtreffer und die Drilldown-Auswahl genügend sind; erst dann Task 03.
