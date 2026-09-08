---
status: draft
type: konzept
project_kind: brownfield
estimated_scope: large
execution_mode: requires_user_decision
rules_dir: .agents/rules
last_updated: 2026-09-08
open_questions:
  - Task 01 muss den Widerspruch zwischen `usedDefaultConfig=true` im Health-Payload eines regelosen Source-Targets und dem öffentlichen `not_configured`-Vertrag auflösen oder ausdrücklich als internes Legacy-Feld markieren.
  - Der öffentliche Handoff-ID-Vertrag muss vor der Umsetzung freigegeben werden: Empfehlung ist eine selbständige, target- und snapshotgebundene ID-Familie für Source und Assembly ohne öffentliche Cache-Generation.
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

## Ergebnis der Kursprüfung

Die Release-Reihenfolge bleibt richtig: Task 01 liefert den einzigen
`targetPath`-Einstieg, Task 02 muss darauf Discovery, IDs und begrenzte
Navigation aufsetzen. Task 02 ist aber in der bisherigen Fassung noch nicht
freigabefest, weil sie bereits vorhandene Statusprojektionen mit einem echten
Handoffvertrag gleichsetzt.

Der MCP-Server 1.0.177 bestätigt die Task-01-Basis: `.slnx`, `.dll` und
zielgebundene Antworten werden aufgelöst; `navigation` enthält Target, Origin,
Fingerprint, Capability und Status. Die Live-Kette `find_symbol →
get_symbol_body` funktioniert für eine Source-ID ebenso wie für eine
Assembly-ID.

Für Task 02 fehlen oder widersprechen sich jedoch noch zentrale Eigenschaften:

- `navigation` wird nachträglich aus dem fertigen Payload abgeleitet. Sie ist
  kein gemeinsames Aggregat vor Text- und StructuredContent-Projektion.
- Source-Fingerprints werden aktuell aus der Solution-Datei selbst gebildet;
  eine Änderung geladener Quelldateien erzeugt damit nicht zuverlässig einen
  neuen Snapshot.
- Source-Folgeobjekte wie Caller und Call-Tree-Knoten besitzen keine eigenen
  kopierbaren Symbol-IDs. `reachedFromSymbolId` identifiziert nur den
  ursprünglichen Anker.
- Assembly-IDs enthalten aktuell `assembly:<hash>:<generation>:<symbolId>`.
  Damit ist die Cache-Generation öffentlich und ein Neustart-/Eviction-Detail
  Teil des Agenteninputs; das widerspricht dem gemeinsamen Rahmen und Task 04.
- Ein unbekanntes Symbol wird im aktuellen Envelope als `operationStatus=ok`,
  `result.available=true` und `completeness=complete` projiziert. Ein
  begrenztes `find_symbol` meldet außerdem nur `maxResults + 1` als
  Ersatz-Count, nicht den belastbaren Gesamtcount.
- `get_index_scope` liefert Dateityp-Counts, aber keinen maschinenlesbaren
  Routinghinweis für C# gegenüber Razor/JavaScript. Beim File Tree ist die
  Root-Zeile als Aggregat sichtbar, ihr `childDirectoryCount` bleibt im
  Root-Fall jedoch 0.
- Ein regeloses Fixture liefert fachlich korrekt `get_violations =
  NOT_CONFIGURED`, aber Health weist weiterhin `usedDefaultConfig=true` aus.
  Das ist mindestens ein widersprüchlicher öffentlicher Status und muss vor
  Task 02 geklärt werden.

Daher lautet die Empfehlung: Task 02 beibehalten, aber den bisherigen
`status: ready`-Stand zurücknehmen. Nach der Task-01-Revalidierung und der
Freigabe des ID-Schnitts kann der Draft wieder auf autonom umsetzbar gestellt
werden.

## Ziel und Problem

Ein Agent findet in einem unbekannten Repository den passenden Scope, ein
Symbol und einen tatsächlich kopierbaren Folgeinput. Heute unterscheiden
`get_file_tree`, `get_index_scope`, `search_pattern`, Symbolgraph und
Strukturtools Pfade, IDs, Ranking und Truncation nicht durchgängig. Plausible
Zeilen können dadurch als falscher Handoff missverstanden werden, und ein
vollständig aussehendes Ergebnis kann tatsächlich nur ein begrenzter Ausschnitt
sein.

## Primärer Agentennutzen

Discovery → Scope → Suche → Body/Struktur → Referenzen/Call-Tree funktioniert
mit kleinen, ehrlichen Antworten. Jeder ausgegebene Folgeinput ist direkt aus
StructuredContent kopierbar, an das konkrete Target gebunden und mit einem
sicheren nächsten Schritt versehen. Der Task verändert keine Analyseheuristik,
keine Lintregel und kein Quality Gate.

## Source of Truth und betroffene Bereiche

- Audit: Pakete 01–03 der Befundmatrix, vor allem `get_file_tree`,
  `get_index_scope`, `search_pattern`, `find_symbol`, `find_references`,
  `get_call_tree`, `get_*structure`, `find_implementations` sowie die
  Discovery-/ID-/Cross-Target-Kombinationen.
- Öffentlicher Rahmen: `MCP-Verbesserungsrahmen.md` für Status, Handoff,
  Scope, Budget und Continuation.
- Contract-Quelle: Registrierung, SDK-Schema, Runtimevalidierung und
  Toolbeschreibung müssen aus einem Contract-Katalog ableitbar bzw. gegen
  denselben Contract-Test prüfbar sein.
- Code: `Tools/FileStructure/*`, `SearchPatternScanner`, Symbolgraph,
  `AnalysisSymbolIdentity`, `SymbolIdentifierResolver`, Snapshot-/ID-Projektion,
  Truncation, Formatter und Registrierungs-/Raw-Wire-Tests.
- Vorbedingung: Task-01-Targetauflösung und der regelose `not_configured`-Pfad
  werden nur revalidiert. Eine Reparatur des früheren Targetvertrags wird nicht
  still in Task 02 eingeschoben.

## Verbindlicher Schnitt

### Target- und Snapshot-Identität

Target-Identität und Analyse-Snapshot werden getrennt geführt:

- Die Target-Identität bindet den kanonischen `targetPath` und verhindert
  Cross-Target-Handoffs.
- Die Snapshot-Identität beschreibt den tatsächlich analysierten Source- bzw.
  Assemblystand und erkennt Änderungen desselben Targets.
- `stale_snapshot` darf nicht aus einem bloßen Solution-Datei-Fingerprint
  abgeleitet werden. Der genaue Snapshotwert bleibt intern ableitbar, muss aber
  in jeder snapshotgebundenen ID und Continuation verlässlich geprüft werden.

### Handoff-ID

Eine Handoff-ID ist eine kanonische, kopierbare Zeichenkette, die neben der
fachlichen Symbolidentität die erforderliche Target-/Snapshotbindung trägt.
Sie wird im vorhandenen `symbolIdentifier`-/Batch-ID-Slot akzeptiert, sodass
keine Markdown-Analyse erforderlich ist. Bare DocComment-IDs dürfen als
explizite Benutzereingabe weiter unterstützt werden, werden aber nicht als
Handoff deklariert, wenn die Zielbindung nicht beweisbar ist.

Source-IDs basieren auf der kanonischen DocComment-ID. Assembly-IDs binden
kanonischen Assemblypfad, Content-Hash und DocComment-ID. Eine Cache-Generation
ist niemals Teil der öffentlichen ID. Ein anderer Targetpfad, ein anderer
Assembly-Hash oder ein veralteter Source-Snapshot führt zu einem eigenen,
unterscheidbaren Fehlercode und nicht zu `symbol_not_found`.

Jeder Treffer, jeder Call-Tree-Knoten und jede Strukturzeile erklärt explizit:
`handoff=true|false`, ID, Targetbindung, Symbolart und die erlaubten
Folge-Tools. Eine sichtbare Zeile, ein FQN oder ein Pfad ist ohne diese
Kennzeichnung keine Handoff-ID.

### Status, Resultat und Completeness

Der gemeinsame Envelope bleibt schmal und fachpayloadspezifisch. Er wird aus
derselben Aggregation wie Text und StructuredContent erzeugt und darf keine
fachliche Completeness nachträglich erraten oder überschreiben.

- Ein erfolgreich geprüfter, trefferloser Scope ist `empty`, mit
  `result.available=false` und einem passenden nächsten Schritt.
- `SYMBOL_NOT_FOUND` und `AMBIGUOUS_SYMBOL` bleiben recoverable, sind aber im
  Resultat, in Completeness und im nächsten Schritt klar von Erfolg mit Treffern
  getrennt.
- `target_mismatch`, `stale_snapshot`, `invalid_argument`, `unsupported` und
  `not_configured` behalten die Semantik des gemeinsamen Rahmens.
- `complete`, `partial` und `truncated` gelten für das benannte Ergebnisfeld
  bzw. den benannten Composite-Abschnitt. Ein Assembly-`partial` darf nicht
  durch einen pauschalen Envelope-`complete` verdeckt werden.
- Text und StructuredContent enthalten dieselben IDs, Counts, Statuswerte und
  Folgeschritte. Der Text fasst nur zusammen.

### Bounded Navigation

Defaults liefern maximal 20 gerankte Primärtreffer oder 8 KiB serialisierte
Nutzdaten, jeweils durch serverseitige Caps begrenzt. Produktion wird vor
Tests, Artefakten und Diagnosen gerankt; getrennte Counts machen den Rest
sichtbar.

Bei Kürzung sind `totalCount`, `returnedCount`, `truncatedBy` und genau ein
fachlich passender nächster Schritt Pflicht. Ein Cursor/ContinuationToken wird
nur bei deterministischer Sortierung ausgegeben und bindet Target, Snapshot,
Query, Filter und Sortierung. Wo keine sinnvolle stabile Reihenfolge existiert,
verweist `next` auf Scope-, Filter- oder Detailverfeinerung statt auf beliebige
Seiten.

## Muss-Kriterien

- Eine ausgegebene Handoff-ID funktioniert im versprochenen Folgecall. Ein
  target-fremder, ambiger, veralteter oder capability-fremder Einsatz ist
  jeweils unterscheidbar und liefert einen sicheren nächsten Schritt.
- C#, Razor und JavaScript besitzen einen nachvollziehbaren Routingpfad:
  C#-Symbole über den Symbolgraph, Nicht-C#-Content über `search_pattern` mit
  passendem Scope-/Glob-Hinweis. Ein Text-Miss wird nicht als C#-Symbolleere
  ausgegeben.
- `empty`, `complete`, `partial` und `truncated` sind pro Ergebnismenge wahr;
  Text und StructuredContent sind in IDs, Counts und Folgeschritten identisch.
- `find_symbol`, Referenzen, Call-Tree, Implementierungen, Namespace-,
  Klassen- und File-Skeleton-Ergebnisse nennen belastbare Counts und die
  tatsächliche Truncation-Ursache. Kein `maxResults + 1`-Schein-Count.
- Kleine Defaults bevorzugen handlungsfähige Produktionstreffer; Test-,
  Artefakt- und Diagnosesegmente bleiben sichtbar getrennt.
- Der Task führt keine öffentliche Cache-Generation als Agenteninput ein und
  ändert weder Assembly-Decompiler-Qualität noch Assembly-Lease-Lifecycle.

## Messbare Akzeptanzkriterien

- Mindestens fünf Source-Raw-Wire-Ketten und zwei Assembly-Raw-Wire-Ketten
  kopieren IDs ausschließlich aus StructuredContent, ohne Markdown zu parsen,
  in Body, Structure, References oder Call-Tree. Jede erwartete Ablehnung
  prüft den exakten Fehlercode und den nächsten Schritt.
- Die Source-Ketten decken mindestens ab: File Tree → Index Scope →
  `find_symbol` → Body, `find_symbol` → References, `find_symbol` → Call-Tree,
  `find_symbol` → Structure/Implementations sowie den C#/Razor/JS-
  Routingfallback.
- Die Assembly-Ketten decken mindestens Katalog/Suche → Typ → Body/Structure
  und Typ → References/Call-Tree ab. Die ID enthält keine Generation und
  überlebt einen Neustart bei gleichem Pfad-/Hash-Snapshot.
- Root Discovery bleibt unter 8 KiB, zeigt höchstens 20 Primärzeilen und
  liefert korrekte Root-/Elternaggregate, Scope und Routinghint. Große Mengen
  enthalten Count, Ursache und genau einen validen Drilldown oder eine gebundene
  Continuation.
- Für jede betroffene Registration beweisen Schema-Snapshot,
  Runtime-Validator und Toolbeschreibung denselben Contract einschließlich
  Required-Feldern, Enums, Alias-Slot, Capability und nächster Aktion.
- Tests für unbekannt, ambig, target-fremd, stale, loading, empty, partial,
  unsupported, not_configured und truncation beweisen, dass kein Fall als
  scheinbar vollständiges `ok/complete` erscheint.

## Non-Goals

Keine Targetmigration, keine Reparatur des Task-01-Vertrags ohne separaten
Befund, keine Regeln-/Lintänderung, keine umfassende Such-Recall-Neuheit,
keine Quality-Heuristik, kein globaler Seitenbrowser und keine neue
Assembly-Lifecycle-, Decompiler- oder Cache-Qualitätslogik. Task 04 übernimmt
die fachliche Assemblyqualität und darf den hier festgelegten ID-/Statusschnitt
nicht neu definieren.

## Architekturannahmen

Ein kleiner Contract-Kern ergänzt typisierte Toolpayloads; Scanner bleiben bei
ihren Tools. Die Handoff-ID- und Resultataggregation entsteht vor beiden
Projektionen. Registrierungen bleiben explizit; kein Plugin-System, DI-
Container, Reflection-Registration, Command-Bus oder Mega-DTO.

Project- und Assembly-Leases bleiben getrennt. Task 02 definiert nur die
öffentliche, lifecycleunabhängige Identitätsprojektion; interne Assembly-
Generation, Eviction und Cleanup werden nicht zum Agentenvertrag.

## Fehler-, Fallback-, Ownership- und Lebenszeitsemantik

Ein Resultat ohne Treffer ist nur dann `empty`, wenn der benannte Scope
vollständig geprüft wurde. Nicht indexierbarer Nicht-C#-Content führt zum
Textsuche-Hint; Capability-Mismatch bleibt `unsupported`. IDs und Continuations
prüfen Target- und Snapshotbindung. Geänderter Inhalt ergibt
`stale_snapshot`; ein anderes Target ergibt `target_mismatch`. Eine abgelaufene
Continuation wird nicht still neu interpretiert, sondern sicher abgelehnt.

Die Antwort besitzt keine implizite Ownership über Dateien oder Sessions. Der
Agent darf eine Handoff-ID nur innerhalb des konkret adressierten Targets
verwenden; die Serverregistries bleiben alleinige Besitzer ihrer Leases.

## Risiken und Alternativen

Ein universeller Envelope oder Cursor würde Toolnutzen verschleiern. Deshalb
gibt es nur den schmalen Contract-Kern und Continuation nur nach sichtbarer,
stabiler Ordnung. Ein nachträgliches Formatter-Patching ist als alleinige
Lösung ausgeschlossen, weil es Text und StructuredContent auseinanderlaufen
lassen kann.

Alternativen:

- Nur `find_symbol` reparieren: löst weder Discovery noch Member-Handoff und
  ist verworfen.
- Alle Tools mit Cursor ausstatten: erhöht Token- und Statekosten ohne
  fachlichen Nutzen und ist verworfen.
- Generation in Assembly-IDs behalten: kurzfristig einfacher, aber
  Neustart/Eviction wird zum Agentenproblem und widerspricht Task 04.
- Nur Markdown-IDs dokumentieren: bricht bei StructuredContent-Hosts und ist
  verworfen.

## Verifikation und Release-Gate

Verbindlich sind Fast-, Integration-, Schema- und Raw-Wire-Folgecall-Tests,
C#/Razor/JS-Proben, Root-/Glob-/Empty-/Truncation-/Stale-Proben, ein frisch
gestarteter MCP-Dogfood-Call, `dotnet build`, beide Nicht-Stress-Suiten,
Dokumentationsabgleich und `git diff --check`. Der zugeordnete Auditkatalog
und die positive Baseline werden geschützt; der vollständige 45-Finding-
Nachlauf bleibt Task 04 vorbehalten.

Ein Release ist blockiert, solange ein als Handoff deklarierter Wert nicht
kopierbar ist, ein Call-Site-/Knotenwert keine belastbare ID besitzt,
StructuredContent und Text widersprechen, Generation öffentlich erforderlich
ist, Snapshotwechsel als `symbol_not_found` erscheint oder ein begrenztes
Ergebnis `ok/complete` behauptet.

## Dokumentationsbedarf

`Docs/agent-api.md`, `Docs/integration.md` und die MCP-Instructions erhalten
eine kurze Discovery-Tabelle, Routingregeln, ID-/Target-/Snapshotregeln,
präzise Empty-/Partial-/Truncation-/Stale-Semantik und je ein Source- und
Assembly-Folgebeispiel. Vollständige Rohschemas bleiben beim Schema-Endpoint;
die Beschreibung darf keine zweite Contract-Wahrheit enthalten.

## Nächste fachliche Entscheidung

Nach Auflösung der beiden offenen Punkte wird dieser Draft mit
`status: ready`, `execution_mode: autonomous` und `open_questions: []`
freigegeben. Erst danach startet — auf ausdrücklichen Wunsch — der
Project-Orchestrator für das gesamte Task-Verzeichnis. Task 03 bleibt bis zum
Task-02-Release nachgeordnet.
