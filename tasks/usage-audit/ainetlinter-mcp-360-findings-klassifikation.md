# AiNetLinter MCP – Klassifikation des 360°-Usage-Audits

- Grundlage: `ainetlinter-mcp-360-usage-test.md` und `chatverlauf-ids.md`
- Stand: 2026-09-10
- Ziel: Produktentscheidungen vorbereiten, keine Implementierungsplanung

## Kurzurteil

Der Audit zeigt kein Semantik- oder Zuverlässigkeitsproblem. Im Gegenteil: Die
Symbolauflösung, Snapshot-Bindung, Fehlerverträge und die spezialisierten
Roslyn-Abfragen liefern einen echten Mehrwert gegenüber Textsuche.

Das Hauptproblem ist die **Grenze zwischen Analysemodell und Agenten-Transport**:
Der Server liefert Maschineninformationen in einer Form, die für einen Menschen
oder ein Sprachmodell teuer ist. Auf einem 180-kLoC-Projekt wird dieser Effekt
bei Hot-Symbolen und polymorphen Graphen so groß, dass er den Nutzen einzelner
MCP-Calls aufzehrt. Das ist keine Frage der Korrektheit, sondern der
Antwortform, Defaults und Scoping-Strategie.

| Klasse | Bedeutung | Befunde |
|---|---|---|
| A – Produktproblem, hoher Hebel | verschlechtert den normalen Agenten-Loop unmittelbar | IDs im Text, Composite-Budget, Incoming-Call-Tree, fehlender Produktionsscope |
| B – Produktproblem, mittlerer Hebel | Antwort ist korrekt, aber schlecht priorisiert oder erklärt | Ranking, leere Audits, unpräzise Statusmeldungen, inkonsistente Zähler |
| C – Dokumentations-/Vertragsdrift | der Agent wird zu einem nicht vorhandenen oder überversprochenen Pfad geleitet | `view_file`, absolutes MCP-first-/Tokenversprechen |
| D – bewusstes Trade-off / kein Fehler | Verhalten ist im Audit sichtbar, aber fachlich sinnvoll oder nur mit enger Anfrage anders | Trunkierung, Typ-Call-Tree leer, Source-vor-eigener-DLL, `search_pattern` als Textsuche |
| E – Client-/Auditabhängig | erst nach Messung im tatsächlichen Zielclient priorisieren | Schema-Tax, Sichtbarkeit von `structuredContent`, Cursor-spezifische Call-Reibung |

## Die IDs: Diagnose und Entscheidung

### Was sie heute leisten

Eine Handoff-ID hat die Form:

```text
source:<base64url(solutionPath)>:<SHA-256(snapshot)>:<Roslyn-DocCommentId>
```

Sie ist nicht bloß ein Hash. Sie transportiert drei Sicherheits-/Robustheits-
eigenschaften ohne serverseitigen Zustand:

1. Das richtige Analyseziel wird gebunden.
2. Ein Symbol aus einem alten Snapshot wird abgelehnt.
3. Die genaue, überladungsfeste Roslyn-Symbolidentität bleibt erhalten.

Damit übersteht sie auch Daemon-Neustarts oder Cache-Eviction, solange derselbe
Inhalt wieder analysiert wird. Das ist ein gutes internes und
maschinenlesbares Design. Die Bestandteile stehen in
`src/AiNetLinter/Mcp/AnalysisSymbolIdentity.cs`; die Handoff-ID wird unter
anderem in `find_symbol` und `get_call_tree` aktuell zusätzlich in Markdown
gerendert.

### Das tatsächliche Problem

Die Kennung wird pro Treffer wiederholt, obwohl Ziel und Snapshot bereits im
gemeinsamen Navigation-Envelope stehen. Die volle ID ist deshalb in
Listen-Antworten meist eine **duplizierte Transportrepräsentation**, nicht
zusätzliche fachliche Information. Bei Bäumen kommt die Wiederholung desselben
Symbols entlang mehrerer Pfade hinzu.

Das Audit beschreibt daher nicht „IDs sind falsch“, sondern:

> Die richtige interne ID wird in der falschen Darstellungsform und zu oft
> sichtbar ausgeliefert.

### Kein globales `a = lange ID`

Ein nackter Alias wäre technisch möglich, aber als Primärvertrag schlechter:

- Er benötigt einen zustandsbehafteten Alias-Speicher je Verbindung, Ziel und
  Snapshot.
- Er braucht Ablauf, Invalidierung, Parallelitäts- und Neustartsemantik.
- Ein Alias aus einem anderen Call oder nach einem Refresh ist sonst
  mehrdeutig oder, schlimmer, falsch.
- Er verliert die heute absichtlich stateless prüfbare Snapshot-Bindung.

`a`, `#12` oder `s12` sind folglich höchstens eine **optionale,
antwortlokale Bedienoberfläche**, nie ein Ersatz für die kanonische ID.

### Empfohlene Reihenfolge

1. **Markdown von kanonischen Handoff-IDs befreien.** Dort reichen
   `Datei:Zeile — Kurzsignatur` sowie ein klarer Hinweis, dass eine
   maschinenlesbare Handoff-Referenz vorhanden ist.
2. **Die kanonische ID in `structuredContent` erhalten.** Sie bleibt die
   Quelle für automatische Folge-Calls und die Robustheit über den Daemon
   hinweg.
3. **Den echten Client-Kontext messen.** Falls der Zielclient
   `structuredContent` ebenfalls vollständig in den Modellkontext serialisiert,
   spart Schritt 1 nur sichtbares Markdown, aber keine Wire-/Kontextkosten.
   Dann braucht es zusätzlich eine clientseitige Referenzübergabe oder eine
   kompaktere Maschinenrepräsentation.
4. **Erst danach kurze Handles evaluieren.** Ein Handle muss mindestens an
   Ziel und Snapshot gebunden sein und bei Staleness mit einer klaren,
   recoverable Fehlermeldung scheitern.

Eine mögliche spätere Form wäre ein kurzlebiger `handoffRef` im
StructuredContent, beispielsweise `h:7f3a:12`, mit serverseitiger Map. Sie
ist nur dann sinnvoll, wenn der Client die kanonische ID wirklich nicht
unsichtbar durchreichen kann. Der erwartete Gewinn muss gegen den Verlust der
heutigen Statelessness abgewogen werden.

## Priorisierte Findings

### A1 – IDs werden als Markdown-Zwangsausgabe repliziert

- **Klasse:** A, Transport-/Ergonomieproblem
- **Evidenz:** `find_symbol` formatiert `id: <…>` direkt im Text;
  Skeletons und Call-Trees transportieren dieselbe Handoff-Information je
  Member bzw. Knoten.
- **Folge:** große Antworten, schlechteres Signal-Rausch-Verhältnis;
  `get_file_skeleton` wird auf großen Dateien praktisch unattraktiv.
- **Zielbild:** lesbares Markdown ohne kanonische IDs; Handoff weiterhin
  maschinenlesbar und testbar.
- **Abhängigkeit:** vorab klären, ob der jeweilige MCP-Client
  `structuredContent` dem Modell separat, gar nicht oder als JSON im selben
  Kontext zuführt.

### A2 – Das Budget von Composite-Tools schützt Bytes, nicht Erkenntnis

- **Klasse:** A, Antwort-/Budgetdesign
- **Evidenz:** `get_feature_context` und `get_test_context` liefern bei
  heißen Symbolen nur Zähler und Budgetmetadaten; die ersten relevanten
  Caller/Testnamen gehen verloren.
- **Folge:** ein teurer Roundtrip beantwortet die operative Frage nicht und
  führt zu weiteren Calls.
- **Zielbild:** bei Trunkierung immer eine kleine, nützliche Projektion
  behalten: beispielsweise Top-3 Production-Caller, Top-3 Tests,
  Gesamtsummen und eine explizite Fortsetzungsanweisung. Das Budget sollte
  fachliche Repräsentanten vor Metadaten schützen.
- **Nicht tun:** den Default-Budgetwert einfach global erhöhen. Das verschiebt
  den Token-Flood statt die Auswahlstrategie zu verbessern.

### A3 – Incoming-Call-Tree modelliert ein DAG-Problem als Baum

- **Klasse:** A, Graph-/Defaultproblem
- **Evidenz:** bei einer virtuellen Basismethode werden gemeinsame Kinder
  unter mehreren polymorphen Pfaden wiederholt; der Test erreichte etwa
  114 KB.
- **Folge:** exponentiell wirkende Duplikation und Testrauschen genau bei den
  interessanten Architekturfragen.
- **Zielbild:** eingehend standardmäßig kompakter: pro Symbol einmal,
  Referenzanzahl aggregiert, Tests getrennt oder nachrangig. Für die visuelle
  Detailansicht eignet sich ein Graph mit referenzierten Knoten besser als ein
  duplizierender Baum.
- **Sofort nutzbarer Schutz:** conservative Defaults für `incoming`, etwa
  niedrige Tiefe/Fan-out sowie ein deutlicher Hinweis bei virtuellen oder
  interfacebasierten Zielsymbolen.

### A4 – Testcode dominiert semantische Ergebnisse ohne Scope-Regler

- **Klasse:** A, Query-/Rankingproblem
- **Evidenz:** `find_symbol`, `find_references`, Implementierungen und
  Hierarchie liefern Teststubs bzw. Tests sehr früh; der Audit fand 97
  Implementierungen mit vielen verschachtelten Testtypen.
- **Folge:** die Produktionsfrage wird korrekt, aber schlecht beantwortet.
  Anschließend muss der Agent Textsuche einsetzen, um Produktion kompakt zu
  sehen.
- **Zielbild:** konsistenter optionaler Scope `all | production | tests` für
  alle listen- und graphbasierten C#-Tools sowie eine explizite
  Production-first-Standardsortierung bei allgemeiner Entwicklung.
- **Offene Produktentscheidung:** Wie Produktion erkannt wird (Testprojekt,
  Test-Attribut, Pfadmuster) muss transparent im Payload stehen; es darf keine
  stillschweigende Negativaussage für ausgeschlossenes Testmaterial geben.

### B1 – Suchranking und Completeness sind fachlich richtig, aber leicht falsch lesbar

- **Klasse:** B, Interpretierbarkeit
- **Befunde:** Tests vor Produktionsdeklarationen; `kind=interface` bei einem
  Klassenvertrag kann mit `complete` missverstanden werden.
- **Zielbild:** Ergebnistext nennt aktiv, welchen Filter `complete` meint;
  Ranking bevorzugt Deklarationen in Production-Projekten. Bei einem
  Nulltreffer nach `kind` könnten nahe Treffer anderer Arten als strukturierte
  Alternative mitgeliefert werden.

### B2 – Leere und Status-Antworten haben zu viel bzw. die falsche Erklärung

- **Klasse:** B, UX-/Vertragsproblem
- **Befunde:** lange Remediation bei `find_magic_values`/`pattern_detect` ohne
  Treffer; `get_impact` vermischt „kein Git-Repository“ und „Working Tree
  clean“; `reload_config` nennt einen Teilzähler ohne Einordnung.
- **Zielbild:** bei leerem Ergebnis eine kurze, abschließende Aussage mit
  Scope und Limit. Statusfälle fachlich disjunkt formulieren. Zähler mit
  Bezeichnung liefern, etwa „18 boolesche Regeln aktiviert; 34 effektive
  Grenzwerte geladen“.

### B3 – Mehrere Übersichten verwenden unterschiedliche Zählmodelle

- **Klasse:** B, Vertrauens-/Erklärproblem
- **Befund:** `get_index_scope` und `get_file_tree` berichten stark
  unterschiedliche `.cs`-Zahlen, ohne beide Nenner direkt gegenüberzustellen.
- **Zielbild:** jedes Ergebnis nennt seine Population (Roslyn-Dokumente,
  physisch gefundene Dateien, ausgeschlossene Verzeichnisse, Trunkierung).
  Keine Gesamtsummen, die leicht addiert oder verglichen werden können, wenn
  sie nicht dieselbe Menge meinen.

### B4 – Nützliche Spezialergebnisse haben vermeidbare Asymmetrien

- **Klasse:** B, Verbesserungsbacklog
- **Befunde:** `get_namespace_tree(includeTypes=true)` zählt Typen, zeigt aber
  keine Namen; `resolve_type_origin` ist für NuGet präziser als für eigene
  Source-Typen; DI-Hinweise in Hierarchien werden von Testsetups dominiert.
- **Zielbild:** Typnamen in einer begrenzten Drilldown-Projektion, für
  Source-Typen bevorzugt Quellpfad plus Projekt statt irreführendem
  DLL-Dateinamen, und DI-Funde nach Production/Test separieren.

### C1 – Workflow-Dokumentation und exponierte Tools sind nicht deckungsgleich

- **Klasse:** C, Dokumentationsdrift
- **Befund:** Die Workflow-Regel empfiehlt `view_file`; das Tool ist im
  geprüften Namespace nicht vorhanden. Außerdem ist die Formulierung,
  semantische MCP-Calls seien generell kontextärmer, für große Graphen und
  Textfragen nicht haltbar.
- **Zielbild:** `view_file` entweder tatsächlich bereitstellen oder aus der
  Regel entfernen. Die Toolwahlregel als Entscheidungsmatrix dokumentieren:
  MCP für Semantik; Read/rg für Text und bereits bekannte kleine Kontexte;
  Composite- und Graph-Tools nur mit passenden Grenzen.

### D – Sichtbare Trade-offs, die nicht als Fehler priorisiert werden sollten

| Befund | Einordnung |
|---|---|
| `search_pattern` ist kein semantisches Grep | korrekt: Es ist eine Textsuche und darf in dieser Rolle gegen `rg` verlieren. |
| Typ als Root eines Call-Trees ergibt keinen Methodenbaum | korrekt, wenn der Vertrag ausdrücklich methodenzentriert ist; ein Hinweis wäre nett, aber kein P1. |
| Eigene Source ist besser als die eigene dekompilierte DLL | erwartbar und richtig; Assembly-Tools sind für fremden/geschlossenen Code. |
| Trunkierung und `completeness` | grundsätzlich vorbildlich. Verbessert werden muss die Auswahl *vor* der Trunkierung, nicht die Ehrlichkeit der Markierung. |
| Snapshotgebundene lange Handoff-ID | intern richtig; nur die Textpräsentation und eventuell der Clienttransport sind problematisch. |

### E – Noch zu messende, clientabhängige Befunde

| Befund | Warum noch nicht als Server-Backlog priorisieren |
|---|---|
| Schema-Tax durch `tools/list`/dynamische Tools | teils MCP-Clientvertrag und teils ausführliche Serverbeschreibung. Erst pro Client Token-/Latenzanteil messen. |
| Doppelte Kosten von Markdown und `structuredContent` | hängt davon ab, welche Blöcke der Client ins Modellfenster serialisiert. Das entscheidet, ob „IDs nur in StructuredContent“ genügt. |
| `mcpDetails.description`-Reibung | im Audit Cursor-spezifisch; kein AiNetLinter-Serverfehler. |

## Sinnvolle Reihenfolge für spätere Umsetzung

1. Einen kleinen, reproduzierbaren Response-Size-Test für dieselben
   `find_symbol`-, `get_file_skeleton`- und `get_call_tree`-Fälle mit und ohne
   sichtbare IDs erstellen. Dabei Text, StructuredContent und das tatsächlich
   zum Modell geschickte Client-Payload getrennt messen.
2. A1 entscheiden und umsetzen: Markdown-Handoff entkoppeln, kanonische
   StructuredContent-Übergabe erhalten. Dies ist der kleinste Eingriff mit
   potenziell größtem Breitengewinn.
3. A2 und A3 anhand von Hot-Symbol-Fixtures red-test-first lösen. Die Tests
   sollen nicht nur Bytegrenzen, sondern mindestens einen sichtbaren
   Production-Caller bzw. Testnamen und keine mehrfach ausgegebene Knoten-ID
   verlangen.
4. A4 als gemeinsame Scope-Abstraktion konzipieren, damit nicht jedes Tool
   eigene, widersprüchliche Testheuristiken erhält.
5. B- und C-Punkte zusammenziehen: Antworttexte, Zählnenner und
   Workflow-Dokumentation sind ein Vertrauensvertrag und sollten in einer
   kleinen Konsistenzrunde bereinigt werden.

## Produktprinzip, das aus dem Audit folgt

AiNetLinter sollte nicht versuchen, besseres Grep zu sein. Sein Wert ist die
präzise, begrenzte Beantwortung semantischer Fragen. Dafür braucht jede Antwort
zwei klar getrennte Ebenen:

1. **Agentenansicht:** kurze Belege, priorisierte Produktion, wenige
   repräsentative Treffer, klare Vollständigkeit.
2. **Maschinenansicht:** kanonische Symbol-ID, Snapshot und Handoff-Metadaten,
   die ein Client ohne Abschreiben weiterreichen kann.

Solange diese Ebenen im selben ausführlichen Markdown vermischt werden,
bezahlt das Modell für Serverbuchhaltung. Werden sie getrennt, bleiben die
heutigen Sicherheitsgarantien erhalten und der MCP kann bei den Fragen
gewinnen, für die Roslyn tatsächlich einen Vorteil hat.
