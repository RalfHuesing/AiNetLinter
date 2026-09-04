---
status: ready
task: 03-mcp-paginierung-und-response-ergonomie
priority: 3
---

# Konzept: MCP-Ergebnis-Paging und Response-Ergonomie

## 1. Ziel und Nutzen

AiNetLinter soll bei begrenzten MCP-Ergebnissen niemals stillschweigend den
Eindruck erwecken, die sichtbare Teilmenge sei vollständig. Jeder Output, der
eine fachlich relevante Liste von Peer-Ergebnissen begrenzt, soll ohne weitere
Parameter einen kleinen, brauchbaren Default-Ausschnitt liefern und eine
verlässliche Fortsetzung anbieten.

Der Agent soll in einem kurzen Text erkennen können:

- wie viele Einträge in der aktuellen Antwort sichtbar sind;
- ob weitere Einträge existieren oder die Vollständigkeit unbekannt ist;
- warum begrenzt wurde;
- wie die Fortsetzung oder eine engere Filterung angefordert wird.

Das Vorhaben verbindet daher drei getrennte Probleme:

1. **Ergebnis-Paging:** Auswahl einer deterministischen Seite aus einer
   Ergebnisquelle mit einer serverseitig festgelegten Seitengröße und
   `cursor`/`nextCursor`.
2. **Response-Ergonomie:** Eine nützliche Textdarstellung und ein dazu passendes
   strukturiertes Ergebnis.
3. **Wire-Budget:** Begrenzung von Text plus `structuredContent`, ohne die
   fachliche Nutzlast durch einen nachträglichen, semantisch blinden DOM-Trim
   unbrauchbar zu machen.

`page`/`pageSize`, ein frei steuerbares `maxResults` und ein universelles
`Filter`-/`Category`-Argument sind nicht das Ziel. Die API soll für Agenten
einfach sein: fachlich notwendige Filter bleiben tool-spezifisch, die
Seitengröße ist eine interne Serverentscheidung.

## 2. Begriffe und Leitentscheidung

### 2.1 Vorgesehenes Modell: ein kleiner Cursor-Vertrag

Für alle relevanten flachen Listen gilt ein **Cursor-first-Modell**:

- Der erste Aufruf benötigt keinen Paging-Parameter.
- Der Server liefert eine kleine, feste Seitengröße von zunächst 50 Einträgen.
  Sie ist kein öffentliches Werkzeugargument. Eine kleinere effektive Ausgabe
  ist nur bei einem technischen Response-Budget zulässig und wird dann als
  technische Begrenzung kenntlich gemacht.
- Wenn weitere Ergebnisse existieren, enthält die Antwort `nextCursor`.
- Der Folgeaufruf verwendet exakt denselben Tool-Aufruf und setzt nur
  `cursor` auf diesen Wert.
- Fehlt `nextCursor`, ist die Ergebnisfolge beendet.
- Der Cursor ist für den Agenten opaque: Er wird nicht gelesen, verändert oder
  konstruiert. Intern darf er eine Position, Filteridentität und einen
  Quellenstand enthalten.

Der Agent arbeitet sich damit so durch:

```text
Agent:  find_symbol(namePattern="*Service*")
Server: 50 Treffer + nextCursor="..."
Agent:  find_symbol(namePattern="*Service*", cursor="...")
Server: nächste 50 Treffer + nextCursor="..."
Agent:  find_symbol(namePattern="*Service*", cursor="...")
Server: letzte Treffer, kein nextCursor
```

Der Agent muss weder eine Seitenzahl berechnen noch wissen, ob der Cursor ein
Offset, ein stabiler Schlüssel oder ein verschlüsseltes Token ist. Er holt nur
so viele Seiten, wie die Aufgabe benötigt. Wenn die Aufgabe ausdrücklich
Vollständigkeit verlangt, folgt er dem Cursor bis zum Ende; bei einer Suche
nach einem passenden Symbol stoppt er, sobald genug Evidenz gefunden wurde.

Das ist im MCP-Ökosystem das Standardmuster für paginierbare Listen: Die
MCP-Spezifikation beschreibt opaque `cursor`/`nextCursor`, serverseitige
Seitengrößen und das Ende durch einen fehlenden `nextCursor`.[MCP Pagination](https://modelcontextprotocol.io/specification/draft/server/utilities/pagination)
Auch Slack und Stripe verwenden Cursor für große Sammlungen; deren SDKs bieten
zusätzlich automatische Schleifen über alle Seiten.[Slack Pagination](https://api.slack.com/apis/pagination)
[Stripe Pagination](https://docs.stripe.com/api/pagination)

Aktuelle Agenten kommen damit grundsätzlich klar, sofern die Toolbeschreibung
und die Antwort explizit sagen: „Bei `nextCursor` denselben Call mit genau
diesem `cursor` wiederholen.“ Das MCP-Protokoll erledigt diese Schleife für
Custom-Tools nicht automatisch; das Sprachmodell entscheidet, ob weitere
Seiten für die konkrete Aufgabe nötig sind. Genau deshalb müssen Text und
Structured Content dieselbe, klar markierte Seite und eine kurze Folgeanleitung
enthalten.

### 2.2 Warum kein universelles `PaginationArgs` mit generischem Filter

Ein gemeinsames fachliches Filtermodell wäre weiterhin falsch. `find_symbol`,
`search_pattern`, `get_violations`, `get_file_tree` und Graph-Abfragen haben
unterschiedliche Filter- und Sortiersemantiken. Ein universelles `Filter`- oder
`Category`-Feld würde unklare Überlappungen, Regex-Risiken und tool-spezifische
Sonderfälle erzeugen.

Der öffentliche Paging-Anteil bleibt bewusst minimal:

```json
{
  "items": ["..."],
  "nextCursor": "opaque-token"
}
```

`nextCursor` ist die einzige zwingende Fortsetzungsinformation. `totalCount`
wird nur ausgegeben, wenn es ohne zusätzliche Vollauswertung verlässlich und
billig bekannt ist. `hasMore`, `returnedCount` oder ein Truncation-Grund können
bei einem Tool sinnvoll sein, sind aber kein universelles Pflichtfeld. Ein
fehlender Cursor bedeutet vollständig beendet; ein unbekannter Scanstatus darf
nicht als „vollständig“ formuliert werden.

Es wird weder ein universelles `PaginationArgs` noch ein erzwungenes
`PagedResult<T>` eingeführt. Ein einzelner kleiner gemeinsamer Cursor-Helfer
ist nur dann zulässig, wenn er tatsächlich Duplikation reduziert. Die
fachlichen Payloads dürfen ihre natürliche Form behalten; entscheidend sind
die einheitlichen Namen `cursor` und `nextCursor`, nicht eine Klassenhierarchie.

### 2.3 Einheitliche Parameter- und Schema-Reihenfolge

Alle neu eingeführten Paging-Parameter heißen exakt `cursor` und sind vom Typ
`string?`. Alle Folgeinformationen heißen exakt `nextCursor` und sind vom Typ
`string?`. `continuationToken`, `page`, `pageSize`, `limit` und Varianten wie
`next_page` werden für diesen Zweck nicht eingeführt.

In den Tool-Schemas und Request-Records gilt eine feste Reihenfolge:

1. Target-Kontext: `targetPath`, `targetType`;
2. primärer Selektor: beispielsweise `symbolIdentifier`, `symbol`, `pattern`
   oder `namePattern`;
3. fachliche Filter und Scope;
4. strukturelle Grenzen wie `depth`, `root` oder `scope`;
5. Darstellung und Detailgrad wie `view`, `format` oder `detailLevel`;
6. `cursor` als letztes optionales Argument.

Die vorhandenen Selektor- und Filterparameter werden nicht künstlich auf einen
gemeinsamen Namen gezwungen, wenn ihre Fachsemantik verschieden ist. Für den
Agenten entscheidend ist, dass die neu eingeführte Fortsetzung überall gleich
heißt, dieselbe Position im Schema hat und immer unverändert in denselben
Tool-Aufruf zurückkopiert wird. Jede Toolbeschreibung erhält denselben kurzen
Hinweis: „Optional: `cursor` aus `nextCursor` des vorherigen Aufrufs; exakt
unverändert wiederverwenden.“

## 3. Aktueller Projektstand

### 3.1 MCP-Oberfläche

Der aktuelle Server registriert 31 Tools. Für die Paging-Frage sind sie in
folgende Gruppen einzuordnen:

| Gruppe | Aktuelle Werkzeuge / Semantik | Konsequenz |
|---|---|---|
| Flache, begrenzte Peer-Listen | `find_symbol`, `find_references`, `get_impact`-Symbolzweig, `get_type_hierarchy`, `dependency_graph`, `get_violations`, `search_pattern`, `get_hotspots`, `get_test_context`, `find_magic_values`, `find_dead_code`, `find_duplicates`, `pattern_detect` | Einheitlichen `cursor`/`nextCursor`-Vertrag ergänzen; `maxResults` entfernen, wenn es nur die Seite steuert |
| Assembly-Listen | `inspect_assembly`, `find_assembly_extensions`, `search_assembly` | Vorhandenes Muster auf den einfachen Vertrag umstellen; numerische Alt-Offsets und `continuationToken` entfernen |
| Detailsequenzen | `get_class_structure`-Member, `get_symbol_body`-Zeilen, Member innerhalb von `inspect_assembly` | Eigene Detail-Cursor oder Zeilenfenster statt globaler Seitennummer |
| Physische/hierarchische Listen | `get_file_tree`, `get_namespace_tree`, `get_call_tree`, `metrics_tree` | Eigene Datei-/Namespace-/Kindknoten- oder Drill-down-Semantik; kein blindes globales Paging |
| Composite | `get_assembly_context`, `get_feature_context` | Vorschau plus section-spezifische Fortsetzung bzw. Verweis auf Spezialtools |
| Aggregate/Sample-/Steuerantworten | `get_server_health`, `get_index_scope`, `metrics_lookup`, `safeguard`, `reload_config`, `report_observability_feedback`, `get_file_skeleton` | Keine künstliche globale Seite; begrenzte Samples/Aggregate müssen ihre Aussagegrenze klar benennen |

Die Einordnung bedeutet: Eine Liste in einer JSON-Struktur ist noch kein
Grund für einen gemeinsamen Cursor. Paging ist dort Pflicht, wo ausgelassene
Peer-Ergebnisse die fachliche Antwort verändern können. Aggregatwerte,
statische Vorschauen und hierarchische Zoomstufen benötigen andere Verträge.

### 3.2 Belegte Ist-Befunde

- `AssemblyAnalysisResponse.ApplyWireBudget` ersetzt ab
  `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisResponse.cs` bei
  Überschreitung zunächst den gesamten Text durch die Meldung, dass
  `StructuredContent` die kanonische Nutzlast sei. Das verletzt die
  Text-Ergonomie für Clients, die primär `content[0].text` verwenden.
- Derselbe Wrapper führt anschließend in `TrimStructured` eine wiederholte
  JSON-DOM-Kürzung aus. Die Kürzung kennt zwar einige Collection- und
  Abschnittsnamen, besitzt aber keine fachliche Kenntnis des ursprünglichen
  Queries und kann nicht zuverlässig eine neue Seite aus der Datenquelle
  ableiten. Diese allgemeine Nachbearbeitung ist ein Hauptkandidat für
  Entfernung oder eine sehr kleine reine Sicherheitsgrenze.
- `AssemblyPaging` existiert bereits für Assembly-Responses, verwendet aber
  derzeit einen einfachen numerischen Offset. Das ist ein brauchbarer interner
  Startpunkt, aber kein vollständiger Cursorvertrag, weil Query, Filter,
  Sortierung und Assembly-Generation nicht gebunden sind. Der Name und die
  doppelte Alt-Semantik werden im Zuge der Vereinfachung entfernt.
- `search_assembly` sortiert Treffer bereits deterministisch nach relativem
  Pfad, Zeile und stabiler ID und bietet einen Vorläufer des Cursor-Musters.
  Die Suche materialisiert jedoch weiterhin den vollständigen Trefferbestand;
  die öffentliche API mischt dabei `continuationToken`, numerischen Offset,
  `maxResults` und `maxFiles`.
- `inspect_assembly` und `find_assembly_extensions` besitzen bereits Cursor-
  und Budgetpfade. Member innerhalb eines Typs sowie Referenz-/Diagnose-
  Sammlungen haben davon getrennte Grenzen.
- `get_assembly_context` verwendet einen globalen Cursor für die primäre
  Assembly-Auswahl. Optionale Abschnitte (Metrics, Body, Class Structure,
  Callers, Impact) haben keine unabhängige Seitenidentität. Die Textausgabe
  nennt aktuell überwiegend nur `Abschnitt: ...`; sie transportiert für die
  Fachabschnitte zu wenig Inhalt.
- `McpTruncation` erzeugt einheitliche Text-Metazeilen, aber noch keinen
  wiederholbaren Cursor für die strukturierten Projekt-Listen.
- `get_file_tree` verwendet ein gemeinsames `maxResults` für sichtbare Dateien
  und Verzeichnisse. `FileTreeCompleteness` besitzt nur ein globales
  `Truncated` und `ShownFileCount`; die getrennte Datei-/Verzeichnisprojektion
  existiert derzeit vor allem als nachgelagerte Assembly-Wire-Budget-Korrektur.
- `get_symbol_body` begrenzt mit `maxBodyLines`, besitzt aber kein
  Zeilenfenster/Offset für die Fortsetzung eines langen Bodys.
- `get_namespace_tree` und mehrere Audit-/Graph-Tools liefern bei
  `maxResults` eine Truncation-Metazeile, aber keinen Folgecursor.

### 3.3 Bestehende Stärken, die erhalten bleiben sollen

- `search_assembly` verwendet relative Trefferpfade und stabile Treffer-IDs.
- Viele Tools sortieren bereits deterministisch und weisen
  `totalCount`/`shownCount` beziehungsweise `completeness` aus.
- Assembly-Responses kennzeichnen Origin, Snapshot/Generation, Status und
  Diagnostics; diese Metadaten müssen auch Cursor-Validierung und Stale-
  Cursor-Fehler unterstützen.
- Regex-Suche verwendet in `search_assembly` bereits ein Timeout. Diese
  Sicherheitssemantik muss bei neuen Filtern erhalten bleiben.
- Die MCP-Error-Policy unterscheidet erwartbare, recoverable Argumentfehler
  von echten Malfunctions. Ein ungültiger oder veralteter Cursor gehört in die
  recoverable Kategorie.

### 3.4 Renderer- und Abstraktionsbefund

Eine Klasse mit dem exakten Namen `NodeRenderer` wurde nicht gefunden. Die
relevanten aktuellen Bausteine sind `MetricsTreeRenderer` (67 Zeilen),
`CallTreeMermaidRenderer` (80 Zeilen), `GetFileTreeRenderer` (149 Zeilen) und
`CallGraphTreeBuilder` (287 Zeilen). Der größte Kandidat ist damit eher ein
mit Traversierung, Gruppierung, Formatierung und Node-Aufbau vermischter
Tree-Builder als ein einzelner Renderer.

Dieser Task darf diese Komplexität abbauen, wenn sie direkt mit der
Response-/Tree-Ausgabe zusammenhängt. Ziel ist keine neue Renderer- oder
Node-Abstraktion, sondern möglichst wenige klare Datenstrukturen und ein
einfacher Ausgabeweg. ASCII und Mermaid werden nur dann parallel behalten,
wenn beide tatsächlich genutzt werden; ansonsten wird die schwächere
Darstellung mitsamt Tests entfernt. Ein Umbau darf die fachliche
Traversierungsgrenze (`depth`, Node-Cap) nicht still in Paging umdeuten.

## 4. Zielarchitektur

### 4.1 Gemeinsames Ergebnis- und Textmodell

Die Implementierung soll die sichtbare Seite genau einmal auswählen. Text und
`structuredContent` werden aus dieser identischen Auswahl erzeugt. Kein Tool
darf eine vollständige strukturierte Liste und eine davon unabhängige gekürzte
Textliste als scheinbar gleichwertige Antwort liefern.

Für eine paginierbare Peer-Liste gelten nur zwei zentrale Regeln:

- Request: optional `cursor`;
- Response: optional `nextCursor`, nur wenn eine weitere Seite existiert.

`totalCount` wird nur ausgegeben, wenn es ohne zusätzliche Vollauswertung
verlässlich und billig bekannt ist. `returnedCount`, `hasMore`,
`truncatedBy` und `completeness` bleiben optionale fachliche Metadaten für
Tools, bei denen sie wirklich etwas erklären. Es gibt keine Pflicht, alle
Antworten in einen künstlichen Envelope zu pressen.

Bestehende sinnlose oder doppelte Felder werden beim Umbau entfernt. Das gilt
insbesondere für `continuationToken`, numerische Sondercursor und
`maxResults`-Parameter, die ausschließlich eine technische Ausgabeseite
begrenzen. Ein absichtlicher API-Bruch ist in diesem Projekt erlaubt und wird
für klare Namen und weniger Code genutzt.

### 4.2 Cursor-Vertrag

Ein Cursor muss:

1. an Tool, Target, Filter, Sortierung und den relevanten Detailmodus gebunden
   sein;
2. auf einen stabilen Snapshot, eine Assembly-Generation, einen Content-Hash
   oder einen gleichwertigen Quellenachweis verweisen;
3. eine eindeutige Fortsetzungsposition enthalten, vorzugsweise eine stabile
   Sortierschlüsselposition statt ausschließlich eines fragilen Offsets;
4. ohne serverseitig unbegrenzt wachsenden Zustand validierbar sein oder eine
   klar definierte TTL-/Eviction-Semantik besitzen;
5. bei verändertem Query, Snapshot oder inkompatiblem Detail-/Strukturmodus
   nicht still eine andere Liste liefern.

Vorgesehen ist ein stateless, opaque Token mit möglichst einfacher
validierbarer Anfrage- und Snapshotbindung. Der Agent muss den Token nicht
interpretieren. Ein Cursor aus Antwort A darf nicht unverändert auf eine
semantisch andere Anfrage B angewendet werden.

Die Implementierung soll dafür nicht vorab eine Cursor-Klassenlandschaft
aufbauen. Ein kleines Encoding/Decoding-Stück und eine gemeinsame Validierung
reichen, solange die Tools dieselben zwei Feldnamen verwenden. Falls ein
stabiler Quellenstand nicht wirtschaftlich bindbar ist, ist ein klar
dokumentierter, kurzlebiger Positionscursor besser als ein komplexes
serverseitiges Paging-Session-System.

Ein falscher, beschädigter oder veralteter Cursor liefert eine klare,
recoverable `INVALID_ARGUMENT`-Antwort mit dem Hinweis, die erste Seite der
aktuellen Anfrage neu zu starten. Es wird kein stilles Leerergebnis erzeugt.

### 4.3 Deterministische Sortierung vor der Seitenauswahl

Die Sortierung muss vor `Skip`/Cursor-Auswahl und vor der Serialisierung
stattfinden. Der Vertrag wird pro Tool explizit dokumentiert:

- Dateien/Pfade: normalisierter Forward-Slash-Pfad, danach Ordinal-Tie-Breaker;
- Typen/Symbole: vollqualifizierter Name oder stabile ID, danach Pfad, Zeile
  und Spalte;
- Violations/Diagnostics: Pfad, Zeile, Spalte, Regel-ID beziehungsweise
  Severity-Rang, danach stabile Text-/ID-Tie-Breaker;
- Call-Sites/Graphkanten: Tiefe, Quell-/Ziel-Symbol-ID, Pfad und Position;
- Member: deklarierte stabile Sortierung des Tools plus stabile ID.

Ein neuer Treffer darf bei unverändertem Snapshot nicht zwischen zwei bereits
ausgegebenen Seiten erscheinen. Nach einer Snapshotänderung wird der alte
Cursor als veraltet behandelt; eine scheinbar nahtlose, aber inkonsistente
Fortsetzung ist nicht zulässig.

### 4.4 Filter-first

Filter werden vollständig vor der Seitenauswahl angewendet. Der Cursor bindet
die effektiven Filterwerte, darunter:

- Symbolname/-kind und Assembly-Referenzmodus;
- Pattern, Regexmodus, Scope, Include-/Exclude-Patterns und Dateiscope;
- Regel-/Severity-/Projekt-/Pfadfilter, soweit für das jeweilige Tool sinnvoll;
- Kategorie-, Kind-, Accessibility- oder Confidence-Filter der Audit-Tools.

Ein universelles Regex-Filterfeld wird nicht eingeführt. Regex bleibt opt-in,
tool-spezifisch und mit Timeout. Für Agenten häufige neue Filter werden nur
dort ergänzt, wo sie fachlich eindeutig sind. Filterfehler bleiben
recoverable und dürfen keine leere, scheinbar vollständige Antwort erzeugen.

### 4.5 Textdarstellung

Die Textantwort bleibt für jeden erfolgreichen Daten-Call die primäre
Orientierung:

```text
Treffer 1–50; Sortierung: stabil; weitere Treffer vorhanden.
... sichtbare Treffer ...
[Fortsetzung: denselben Call mit cursor=<token> wiederholen]
```

Vorgaben:

- Bei vorhandenen Treffern niemals den gesamten Text durch einen Hinweis auf
  `StructuredContent` ersetzen.
- Der Text muss mindestens Zusammenfassung, sichtbare fachliche Einträge und
  eine kurze Folgeanleitung enthalten.
- Bei unbekannter Gesamtzahl wird nicht `von N` behauptet, sondern die
  Unsicherheit sichtbar gemacht (`Teilresultat; Gesamtzahl unbekannt`).
- Bei fehlendem `nextCursor` wird kein Cursor-Hinweis ausgegeben.
- Bei leeren Ergebnissen lautet die Antwort fachlich eindeutig `0 Treffer`
  beziehungsweise die tool-spezifische Leermengenmeldung; ein Cursor wird
  nicht erzeugt.

Die Tokenlänge muss gegen das Wire-Budget abgewogen werden. Für text-only
Clients soll der tatsächliche Folgewert im kurzen Hinweis stehen; Clients mit
`structuredContent` erhalten dieselbe Information zusätzlich strukturiert.

### 4.6 Wire-Budget

Das Budget ist eine Transportgrenze und keine fachliche Seitenauswahl. Die
Reihenfolge ist:

1. Query, Filter, Scope und Snapshot auflösen;
2. deterministisch sortieren beziehungsweise den stabilen Datenquellen-
   Cursor anwenden;
3. eine fachliche Seite auswählen;
4. Text und strukturierte Nutzlast aus derselben Seite rendern;
5. bei Bedarf optionale Details oder die Seitengröße budgetbewusst reduzieren
   und die Envelope-Metadaten neu berechnen.

Der bestehende Assembly-DOM-Trim muss so ersetzt oder eingeschränkt werden,
dass er keine fachliche Cursorposition erfindet. Falls ein Wire-Budget nach
der Auswahl noch zu klein ist:

- werden zuerst optionale Detailsektionen, Kontextzeilen und Diagnose-Samples
  reduziert;
- bleiben Envelope, Status, Gründe, sichtbare Kerninformationen und eine
  Folgeanleitung erhalten;
- wird ein Cursor nur ausgegeben, wenn die zugrunde liegende Datenquelle diese
  Seite tatsächlich wiederholbar fortsetzen kann;
- liefert ein unrepräsentierbares Mindestbudget einen klaren recoverable
  Fehler statt einer leeren Erfolgsmeldung.

Ein `responseBudget`-Trim darf nicht als `maxResults`-Paging ausgegeben werden.
Beide Ursachen müssen in `truncatedBy` getrennt sichtbar bleiben.

### 4.7 Lebenszeit, Snapshot und Sicherheit

- Ein Cursor verlängert keine Assembly-, Projekt- oder Source-Lease. Jeder
  Folgeaufruf löst Target und Snapshot gemäß dem normalen Lebenszyklus neu auf.
- Assembly-Cursor binden an Assembly-Hash/Generation und die gemeinsame
  Sessionidentität. Nach Generationwechsel: recoverable Stale-Cursor-Hinweis.
- Projekt-/Dateisystem-Cursor benötigen einen belastbaren Quellenfingerprint
  oder werden bewusst nur innerhalb eines residenten, unveränderlichen
  Snapshots akzeptiert.
- Cursor dürfen keine Credentials enthalten und keine zusätzlichen internen
  Cachepfade in die Textantwort leaken.
- Die interne Seitengröße, Cursor und Filter bleiben serverseitig validiert und
  gedeckelt; Pagination darf keine unbeschränkte Speicher- oder CPU-Anforderung
  öffnen. Eine öffentliche `maxResults`-Option wird dafür nicht benötigt.
- Regex-Timeouts, Pfadgrenzen, Ausschlüsse und Assembly-Ausführungsverbot
  bleiben unverändert wirksam.

## 5. Fachliche Paging-Grenzen

### 5.1 Flache Peer-Listen: echte Fortsetzung

Für `find_symbol`, `find_references`, den Symbolzweig von `get_impact`,
`get_type_hierarchy`, `dependency_graph`, `get_violations`, `search_pattern`,
`get_hotspots`, `get_test_context`, `find_magic_values`, `find_dead_code`,
`find_duplicates` und `pattern_detect` wird ein eigener, querygebundener
Continuation-Pfad vorgesehen. Der erste Call bleibt parameterarm; die
Seitengröße ist intern festgelegt. Ein öffentliches `maxResults` wird aus
jedem dieser Tools entfernt, wenn es nur die Anzahl der aktuellen Treffer
steuert. `cursor` ist das einzige zusätzliche Paging-Argument.

Die Text- und Structured-Ausgabe müssen dieselbe sichtbare Seite enthalten.
Graph- und Transitivitätsgrenzen (`depth`, besuchte Knoten, Node-Cap) bleiben
separat von der Seitengröße und werden separat in `truncatedBy` ausgewiesen.

### 5.2 Assembly-Listen: auf einen einfachen Cursor umstellen

Für `inspect_assembly`, `find_assembly_extensions` und `search_assembly` wird
das bestehende Vorläuferverhalten auf `cursor`/`nextCursor`,
Anfrage-/Snapshotbindung und klare Textausgabe umgestellt. Der numerische
Offset und `continuationToken` verschwinden.

Die Suche erhält genau einen fachlichen Ergebnisstrom, sortiert nach relativem
Pfad, Position und stabiler Treffer-ID. `maxFiles` und `maxResults` werden aus
der öffentlichen API entfernt. Falls der Scanner aus Sicherheitsgründen eine
Dateigrenze benötigt, ist sie eine interne Hard-Cap; eine echte Fortsetzung
setzt die Dateiposition im selben opaque Cursor fort. Es gibt keinen zweiten
öffentlichen Cursor und keine konkurrierenden Paging-Achsen.

### 5.3 Detailsequenzen

- `get_class_structure`: Member innerhalb eines einzelnen Typs erhalten den
  normalen `cursor`, gebunden an genau diesen Typ. `maxMembers` wird entfernt,
  wenn es nur die Seite steuert. Die Reihenfolge der
  Primary-Constructor-Parameter bei Records muss stabil sein.
- `get_symbol_body`: `maxBodyLines` wird entfernt und durch den normalen
  `cursor` für ein gebundenes Body-Zeilenfenster ersetzt. Der Folgeaufruf darf
  nicht erneut den gesamten bisherigen Body übertragen müssen.
- `inspect_assembly`: Type-Paging und Member-Paging werden getrennt behandelt.
  Ein Member-Cursor darf nicht als Cursor für die Typ-Liste missverstanden
  werden. Für einen einzelnen gefundenen Typ bleibt
  `get_class_structure`/`get_symbol_body` der bevorzugte Drill-down.

### 5.4 Datei- und Namespace-Strukturen

`get_file_tree` verwendet genau einen Cursor für die aktive Sicht:

- `view=files`: eine Seite von Dateien plus `nextCursor`;
- `view=summary`: eine Seite von Verzeichnisaggregaten plus `nextCursor`;
- `view=tree`: eine begrenzte Strukturvorschau mit klarer Anzeige, dass dies
  kein vollständiger Durchlauf ist. Für eine vollständige Dateiliste wechselt
  der Agent auf `files`, für einen Teilbaum verengt er `root` beziehungsweise
  die Tiefe.

Die bisherige gemeinsame `maxResults`-Grenze sowie `maxFiles` und
`maxDirectories` werden nicht als öffentliche Paging-Parameter fortgeführt.
Datei- und Verzeichnisstatus werden aus der aktiven Sicht verständlich
ausgewiesen; zwei konkurrierende Cursor in einer Antwort entfallen.

`get_namespace_tree` nutzt bereits `depth`, `namespacePrefix`, `kind` und
`includeTypes` als Zoom-/Filterkonzept. Ein `cursor` gilt immer für die
ausgewählte logische Ebene und bindet diese Parameter. Die Antwort sagt klar,
ob Namespaces oder Typen paginiert werden; ein globales Durchlaufen aller
Baumknoten wird nicht vorgetäuscht.

### 5.5 Hierarchische und Composite-Tools

`get_call_tree` und `metrics_tree` sind keine flachen Ergebnislisten. Ein
globaler Cursor würde auf Seite 2 häufig den Elternkontext verlieren. Sie
behalten daher `depth`/`topN`/`root` als strukturelle Grenzen und müssen bei
abgeschnittenen Kindlisten eine knappe, eindeutige Drill-down-Anleitung nennen.
`topN` ist dabei keine Seitengröße; falls es im konkreten Tool nur eine
Darstellungsgrenze kaschiert, wird es entfernt. Für Call-Sites ist
`find_references` der paginierbare Peer-Listen-Drill-down.

`get_assembly_context` und `get_feature_context` liefern kompakte Vorschauen.
Ein einzelner globaler Cursor über Metrics, Body, Callers und Impact ist nicht
zulässig, weil nicht klar wäre, welche Sektion Seite 2 meint. Jede Sektion
weist ihre eigene Vollständigkeit aus; für vollständige Listen verweist die
Antwort auf das zuständige Spezialtool. Der Assembly-Typ-Cursor darf nur dann
global exponiert bleiben, wenn seine Reichweite im Vertrag explizit auf die
primäre Assembly-Liste beschränkt ist.

Bei Budgetknappheit werden Composite-Sektionen nach einer festen Priorität
reduziert. Ein weggelassener optionaler Abschnitt wird als solcher markiert,
nicht als leerer fachlicher Abschnitt.

### 5.6 Aggregate und Samples

`get_server_health` liefert ohne Target bewusst ein Aggregat und begrenzte
Samples. `safeguard` liefert primär einen Score; Top-Violations können auf
`get_violations` verweisen. `metrics_lookup`, `get_index_scope`,
`get_file_skeleton`, `reload_config` und Feedback sind keine Kandidaten für
eine künstliche globale Seite. Diese Antworten müssen trotzdem eine echte
Sample-/Limitgrenze offenlegen, wenn sie intern kürzen.

## 6. Muss-Kriterien

1. Alle in Abschnitt 5.1 genannten Peer-Listen besitzen bei weggelassenem
   Cursor einen sinnvollen, begrenzten Default und bei weiteren Treffern eine
   sichere Fortsetzung.
2. Der erste brauchbare Call benötigt keinen Paging-Parameter. Öffentliche
   `maxResults`-/`maxMembers`-/`maxBodyLines`-Parameter, die nur die aktuelle
   Seite begrenzen, sind entfernt; echte fachliche Grenzen bleiben klar
   benannt.
3. Filter werden vor Pagination angewendet; Cursor werden gegen Anfrage,
   Sortierung und Snapshot/Generation validiert.
4. Die Sortierung ist vor der Seitenauswahl deterministisch und pro Tool
   dokumentiert.
5. `nextCursor` erscheint genau dann, wenn die betreffende Ergebnisfolge
   fortgesetzt werden kann. Optionale Zähler und Gründe sind semantisch
   korrekt; unbekannte Gesamtstände werden nicht als vollständig dargestellt.
6. Text und `structuredContent` zeigen dieselbe Seite. Bei vorhandenen
   Treffern wird Text nie vollständig durch eine generische Structured-
   Content-Meldung ersetzt.
7. Wire-Budget-Truncation, fachliches Paging, Scanfehler, Cancellation und
   Tiefen-/Node-Caps bleiben unterscheidbar. Ein DOM-Trim darf keinen falschen
   fachlichen Cursor erzeugen.
8. `get_file_tree` paginiert je aktiver Sicht mit genau einem `cursor` und
   zeigt bei `tree` unmissverständlich den Vorschaucharakter; das
   Assembly-Wire-Budget darf diesen Status nicht verfälschen.
9. `get_symbol_body`, `get_class_structure` und Memberlisten von
   `inspect_assembly` haben eine dokumentierte Detail-Fortsetzung oder eine
   bewusst begründete, stabile Spezialtool-Weiterleitung.
10. Composite- und hierarchische Tools behaupten keine globale Seitenfolge,
    wenn mehrere unabhängige Sektionen oder Elternkontexte betroffen sind.
11. Cursor-Fehler sind recoverable und enthalten eine konkrete Neustart- oder
    Filteranleitung. Credentials, Tokens und interne Geheimnisse werden nicht
    in Konzept-/Codeartefakten persistiert.
12. Die MCP-Toolbeschreibungen und die zuständigen Verträge dokumentieren
    `cursor`, `nextCursor`, interne Seitengrößen beziehungsweise strukturelle
    Grenzen, Truncation-Gründe und den vorgesehenen Folgeaufruf.

## 7. Akzeptanzkriterien und Nachweise

### 7.1 Automatisierte Verhaltenstests

- Ein Fixture mit mehr Ergebnissen als der internen Seitengröße zeigt bei jedem
  paginierten Tool: begrenzte erste Antwort, verständliche Textzeilen und
  einen verwendbaren `nextCursor`.
- Eine Folgeaufrufserie mit unveränderter Anfrage liefert disjunkte,
  lückenlose, deterministisch sortierte Ergebnisse. Die Vereinigung aller
  Seiten entspricht der vollständigen, gefilterten Ergebnisquelle.
- Dieselbe Anfrage und derselbe Snapshot liefern byte- beziehungsweise
  inhaltlich stabile Seiten; Query-, Filter- oder Snapshotänderung mit altem
  Cursor wird recoverable abgelehnt.
- Filter-Tests beweisen, dass kein Ergebnis außerhalb des Filters durch eine
  Seite rutscht und dass die Cursorbindung einen Filterwechsel erkennt.
- Text- und Structured-Content-Tests vergleichen die sichtbaren IDs bzw.
  Fundstellen direkt; Text darf bei einer begrenzten Antwort nicht leer oder
  nur ein generischer Wire-Budget-Hinweis sein.
- Ein Wire-Budget-Test mit vielen Assembly-Treffern bewahrt fachliche
  Trefferzeilen, Status/Envelope und eine konkrete Fortsetzung. Die Meldung
  `StructuredContent ist die kanonische Nutzlast` darf nicht mehr die gesamte
  Textantwort ersetzen.
- `get_file_tree`-Tests zeigen die getrennte Semantik der `files`-,
  `summary`- und `tree`-Sicht mit genau einem Folgecursor je aktivem Aufruf.
- `get_symbol_body`-Tests lesen ein langes Body-Fenster in mehreren Aufrufen,
  ohne die vorherige Zeilenmenge erneut als einzige Möglichkeit zu benötigen.
- Composite-/Tree-Tests prüfen, dass ein Cursor ausschließlich die
  bezeichnete Sektion fortsetzt und keine falsche globale Vollständigkeit
  behauptet.
- MCP-Contract-/JSON-RPC-Tests prüfen die reduzierten Parameter,
  `cursor`/`nextCursor`, Structured-Content-Objekte und Tool-Annotations.

### 7.2 Pflicht-Verifikation für die spätere Umsetzung

Vor Abschluss der Implementierung gelten aus `AGENTS.md` mindestens:

```powershell
dotnet build
dotnet test src/AiNetLinter.FastTests --filter Category!=Stress
dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress
```

Stress-Tests werden nicht automatisch ausgeführt. Bei großen oder abgeschnittenen
Testausgaben sind TRX-Dateien zur Diagnose zu verwenden. Vor einem größeren
Abschluss ist zusätzlich der MCP-Audit für DRY, Refactoring-Drift, Dead Code
und Magic Values vorgesehen.

## 8. Bewusste Scope-Grenzen / Non-Goals

- Kein generisches JSON-Schema und keine Klassenhierarchie, die alle Tools in
  dieselbe Payload zwingt.
- Kein pauschales `page/pageSize` für alle 31 Tools.
- Keine Schonung alter Parameter oder Fragmente aus Kompatibilitätsgründen.
  Wenn `maxResults` oder ein anderer Grenzwert nur eine technische Seite
  simuliert, wird er entfernt und der betroffene Code sauber bereinigt.
- Keine pauschale Einführung eines fachlich unbestimmten
  `Filter`-/`Category`-Parameters.
- Kein globaler Cursor für Composite- oder hierarchische Antworten, wenn die
  Fortsetzungssektion oder der Elternkontext nicht eindeutig ist.
- Keine Vermischung von fachlichem Paging mit internen Hard-Caps oder
  strukturellen Baumgrenzen.
- Keine implizite Vollständigkeitsgarantie bei Cancellation, Dateisystemfehlern,
  unauflösbaren Roslyn-Referenzen oder Traversal-Hard-Caps.
- Keine Änderung der Git-Download-/External-Source-Architektur und keine
  Ausweitung auf SQL-/LINQ-Heuristiken; solche Root-Cause-Themen bleiben
  eigenständige Vorhaben.
- Keine README-Änderung. Die fachlich zuständigen MCP-/Integrationsdokumente
  werden erst bei der Implementierung aktualisiert.

## 9. Betroffene Bereiche und Übergabepakete

Die Umsetzung lässt sich in wenige verifizierbare Pakete teilen:

1. **Klarer Minimalvertrag:** `cursor`/`nextCursor`, Cursorvalidierung,
   Text-/Structured-Parität und Wire-Budget-Verhalten; Assembly-Black-Hole
   zuerst beheben. Nur einen gemeinsamen Helfer einführen, wenn er nachweisbar
   Duplikation reduziert.
2. **Flache projekt- und assemblygebundene Listen:** vorhandene Limits,
   die ausschließlich Seitengrößen sind, entfernen; deterministische
   Sortierung, Filter-first, Cursor und Tool-Contract-Tests umsetzen.
3. **Detail- und Dateisichten:** Member-/Body-Fortsetzung sowie unabhängige
   aktive Datei-/Verzeichnissicht für `get_file_tree`; sinnlose Renderer- und
   Node-Abstraktionen im selben Zug abbauen.
4. **Composite-/Hierarchie-Ergonomie:** section-spezifische Metadaten,
   Drill-down-Hinweise und Tests gegen irreführende globale Cursor.
5. **Dokumentation und Gesamtverifikation:** `Docs/agent-api.md`,
   `Docs/integration.md`, bei Assembly-Budget-/Konfigurationsänderungen
   `Docs/configuration.md`, gegebenenfalls `Docs/ROADMAP.md` sowie der
   vollständige Build-/Test-/Audit-Nachweis.

Diese Pakete sind fachliche Grenzen für die spätere Umsetzung, keine vom
Konzeptplaner anzulegenden Step-Dateien oder Ausführungs-Roadmap.

## 10. Festgelegte Entscheidungen

1. **Einheitlicher interner Seitenschnitt:**
   Alle paginierten Listen starten mit einer internen Seitengröße von 50.
   `cursor` ist das einzige öffentliche Paging-Argument. `maxResults`,
   `maxMembers` und `maxBodyLines` werden entfernt, wenn sie nur die aktuelle
   Seite begrenzen; echte fachliche Grenzen bleiben klar benannt.
2. **Einheitlicher Paging-Vertrag:**
   Requests verwenden überall `cursor`, Responses überall `nextCursor`.
   `continuationToken`, `page`, `pageSize`, `limit` und numerische
   Sondercursor werden für diesen Zweck entfernt. `cursor` steht in allen
   Tool-Schemas als letztes optionales Argument.
3. **Baum-/Composite-Fortsetzung:**
   Es gibt keine globale Seitenfolge über mehrere Eltern oder Sektionen.
   Stattdessen liefern diese Tools begrenzte Vorschauen mit klarer
   Spezialtool-/`root`-Weiterleitung. Ein per-Kind-Cursor kommt nur hinzu,
   wenn die konkrete Baumsemantik ihn ohne neue Abstraktionsschicht einfach
   macht.
4. **Wire-Budget-Strategie:**
   Fachliches Paging findet vor dem Rendern statt. Bekannte Projektionen
   reduzieren optionale Details zuerst; der allgemeine JSON-DOM-Trim wird
   entfernt oder auf eine kleine letzte Sicherheitsgrenze reduziert. Ein
   DOM-Trim darf keine fachliche Seite simulieren.
5. **Renderer-Bereinigung:**
   `CallGraphTreeBuilder` und die Tree-Renderer werden im Zuge der Umsetzung
   auf Verantwortungsvermischung, Duplikation und echte Nutzung geprüft.
   Unnötige Teile werden entfernt oder direkt zusammengelegt. ASCII und
   Mermaid bleiben nur erhalten, wenn sie tatsächlich gebraucht werden. Eine
   neue generische Renderer-Infrastruktur wird nicht eingeführt.

Diese Festlegungen schließen den fachlichen Konzept- und Scope-Entscheid ab.
Tool-spezifische Detailimplementierungen wie die konkrete Cursor-Codierung,
die exakte Cursorbindung an den Quellenstand und die Auswahl der internen
Hard-Caps bleiben Umsetzungsentscheidungen innerhalb dieses Vertrags.
