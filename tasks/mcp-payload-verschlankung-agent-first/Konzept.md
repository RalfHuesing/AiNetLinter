---
status: draft
execution_mode: autonomous
open_questions: []
created: 2026-09-12
tags: [mcp, agent-first, payload, content-only, hard-cut, token-budget, architecture]
---

# MCP-Responses auf einen agentischen Content-Vertrag reduzieren

## Freigabe- und Umsetzungsvertrag

AiNetLinter erhält einen harten öffentlichen MCP-Schnitt: Toolantworten verwenden
für fachliche Ergebnisdaten ausschließlich `content`. Der optionale
MCP-Ergebniskanal `CallToolResult.structuredContent` wird vollständig entfernt.
Es gibt keinen Legacy-, Dual-Write-, Fallback-, Kompatibilitäts-, Feature-Flag-
oder Format-Negotiation-Pfad und keine Migration alter Responseformen.

Der Status bleibt bis zur ausdrücklichen Nutzerfreigabe `draft`. Danach darf der
Orchestrator den beschriebenen Scope autonom und seriell bis zum Release-Gate
umsetzen. Detailentscheidungen zur konkreten internen Klassenschneidung und zur
knappsten geeigneten Textdarstellung liegen innerhalb der unten festgelegten
Grenzen beim Orchestrator. Es besteht keine offene fachliche Nutzerentscheidung.

Dieses Konzept betrifft ausschließlich MCP-Toolergebnisse aus `tools/call`.
MCP-Protokollfelder wie `content` und `isError`, Eingabeschemas in `tools/list`
sowie intern benötigte JSON-Verarbeitung bleiben erhalten. Das vom SDK
bereitgestellte Property `CallToolResult.StructuredContent` kann nicht aus dem
externen Typ entfernt werden, wird von AiNetLinter aber weder gelesen noch
beschrieben und erscheint nicht auf dem Wire, auch nicht als `null`.

## Ziel und Problem

AiNetLinter soll Fragen eines Coding-Agenten zu beliebigen C#/.NET-Codebasen
semantisch korrekt, tokenarm und mit maximaler fachlicher Informationsdichte
beantworten. Der Produktwert liegt in der gezielten Auswahl aktueller Roslyn-,
Dateisystem-, Git- und Assembly-Evidenz, nicht in mehreren parallelen
Ausgabeformaten derselben Information.

Der aktuelle Vertrag erzeugt und pflegt neben agentisch sichtbarem Text eine
umfangreiche strukturierte Nutzlast. Daraus folgen doppelte oder miteinander
gekoppelte Formatter, DTO-Projektionen, Navigation, Budgetberechnung,
Trunkierung, Dokumentation und Testassertions. Teilweise enthält nur
`structuredContent` die für Folgeaufrufe nötigen IDs und Statusinformationen.
Damit existieren zwei ungleich vollständige Wahrheiten, und ein Content-only-
Client erhält nicht den vollständigen Agentenvertrag.

Der Endzustand besitzt deshalb genau eine öffentliche fachliche Darstellung:
einen vollständigen, deterministischen und kompakten Text in `content`. Weniger
Code ist dabei kein Selbstzweck. Entfernt wird ausschließlich Architektur, die
für den realen Agentenkonsumenten keinen zusätzlichen fachlichen Wert erzeugt.

## Produktdefinition

AiNetLinter ist eine semantische Retrieval- und Kompressionsengine für
Coding-Agenten:

1. Der Server berechnet vollständige typisierte Fachergebnisse aus der jeweils
   zuständigen Source of Truth.
2. Er wählt daraus deterministisch die kleinste für die Anfrage ausreichende
   Menge vollständiger Evidenzeinheiten.
3. Er rendert diese Auswahl genau einmal in einen agentisch natürlichen
   Content-Text.
4. Der Agent kann die Antwort ohne zusätzliche Textsuche verstehen und alle
   angebotenen Folgeaufrufe direkt ausführen.

AiNetLinter ist keine vollständige Codegraph-Dump-API. Ein theoretischer
Gesamtgraph in einer Markdown-Datei könnte einzelne Suchaufgaben beantworten,
wäre für reale Repositories jedoch zu groß, nach Änderungen veraltet und nicht
auf die konkrete Frage begrenzt. Der Server behält deshalb seinen semantischen
Index und seine spezialisierten Abfragen; nur die doppelte öffentliche
Darstellung entfällt.

## Source of Truth und betroffene Bereiche

Fachliche Wahrheit liefern in dieser Reihenfolge:

1. Roslyn-Symbole, geladene `Solution`-Snapshots, Git-/Dateisystemzustand und
   metadata-only Assemblyanalyse;
2. unveränderliche, intern typisierte Scanner- und Domänenergebnisse;
3. toolnahe Auswahl vollständiger Evidenzeinheiten;
4. der eine öffentliche Agentenrenderer;
5. der tatsächliche Raw-Wire-Vertrag eines frischen MCP-Hosts.

Voraussichtlich betroffen sind:

- `McpToolResults` und alle direkten `CallToolResult`-Erzeuger;
- Navigation, Fehlerprojektion und Responsegrößenmessung;
- toolnahe Response-Budget- und Final-Projection-Klassen;
- Composite-Tools, die derzeit `StructuredContent` untergeordneter Toolresults
  lesen oder deserialisieren;
- DTOs und JSON-Optionen, soweit sie ausschließlich den entfernten Wirekanal
  bedienen;
- Toolbeschreibungen, Server-Instructions, Agent-Guide und MCP-Workflowregel;
- Fast-, Integration-, Wire- und Dogfoodtests des öffentlichen
  Responsevertrags.

Interne Modelle bleiben bestehen, wenn sie fachliche Zustände, Evidenz,
Auswahl, Sortierung, Limits, Fehler oder Composition sinnvoll typisieren. Ein
Typ wird nur deshalb gelöscht, wenn sein einziger Zweck die Serialisierung oder
Reprojektion von `structuredContent` war. Sinnvolle Modelle dürfen umbenannt
oder neu geschnitten werden, damit ihre Ownership nicht länger an den
entfernten Transportkanal gekoppelt ist.

## Verbindliche Architektur

### Eine typisierte interne Pipeline

Die Zielpipeline lautet:

```text
Roslyn / Scanner / Registry / Assemblyanalyse
    -> vollständiges typisiertes Domänenergebnis
    -> deterministische Auswahl ganzer Evidenzeinheiten
    -> ein konkreter AgentContentRenderer
    -> genau ein TextContentBlock
```

- Scanner und Analysen liefern keine vorformatierten `CallToolResult`-Objekte
  als internen Datentransport, wenn ihre Ergebnisse von Composite- oder
  Budgetlogik weiterverarbeitet werden müssen.
- Composite-Tools kombinieren typisierte Fachresultate. Sie lesen oder parsen
  weder `CallToolResult`, gerenderten Content noch JSON aus einem
  untergeordneten öffentlichen Toolresult.
- Auswahl, Sortierung, Limits und Trunkierung arbeiten auf typisierten ganzen
  Evidenzeinheiten. Erst danach findet die einmalige Darstellung statt.
- Es gibt genau einen konkreten öffentlichen Renderer-Einstiegspunkt. Kleine
  private, fachlich geschnittene Renderhilfen sind zulässig; eine öffentliche
  Rendererstrategie, ein Pluginmodell oder vorsorgliche Markdown-/JSON-
  Parallelrenderer sind nicht zulässig.
- Ein künftiger anderer Ausgabekanal würde denselben typisierten Kern mit einem
  bewusst neu eingeführten Renderer verwenden. Ein solcher Renderer wird in
  diesem Task ausdrücklich nicht vorbereitet.

### Ein öffentlicher Ergebniskanal

Jeder öffentliche Toolaufruf liefert:

- genau einen nicht leeren `TextContentBlock` in `content`;
- das bestehende MCP-Protokollsignal `isError`, soweit fachlich erforderlich;
- kein `structuredContent`-Feld auf dem Wire;
- keine fachliche Ersatznutzlast in `_meta`, Embedded Resources oder einem
  zweiten Contentblock.

Dies gilt für Source- und Assemblytools sowie für Erfolg, leere Ergebnismenge,
Loading, erwartbare Fehler, Malfunctions, Partialität, Trunkierung und
Budgetfehler. Der Hard Cut darf keine seltene Fehler- oder Lifecycle-Route
zurücklassen.

### Agentischer Content-Stil

Der Renderer erzeugt kompakten, zeilenorientierten UTF-8-Text mit nur so viel
Markdown, wie für Hierarchie, Listen oder C#-Code die Verständlichkeit erhöht.
Das Format soll sich für ein Coding-Modell wie eine kurze, präzise technische
Antwort anfühlen und nicht wie ein serialisierter Transport-Envelope.

Verbindlich sind:

- Die direkte fachliche Antwort steht zuerst.
- Jeder relevante Fakt erscheint genau einmal.
- Pfad, Zeile, Symbolart, Signatur und Beziehung werden nur ausgegeben, wenn
  sie für die aktuelle Aussage oder einen Folgeaufruf relevant sind.
- Jede kanonische Handoff-ID steht genau einmal unmittelbar bei der
  Evidenzeinheit, zu der sie gehört.
- Eine normale vollständige Erfolgsantwort enthält keine wiederholten
  Target-, Snapshot-, Analysemodus- oder `operation=ok`-Metadaten.
- `EMPTY`, `PARTIAL`, `TRUNCATED` und `ERROR` werden knapp und eindeutig
  sichtbar, weil sie die Interpretation der Evidenz verändern. Fehlt ein
  solcher Marker, ist die erfolgreiche Antwort vollständig.
- Counts und Trunkierungsgründe erscheinen nur dort, wo sie Vollständigkeit,
  Auswahl oder den nächsten Schritt beeinflussen.
- `NEXT` erscheint nur mit einer konkreten, unmittelbar ausführbaren Aktion.
- Fehler enthalten mindestens stabilen Fehlercode, Ursache und – falls durch
  Argumentkorrektur oder Retry lösbar – den genauen Feldpfad und die konkrete
  Korrektur.
- C#-Bodies dürfen natürliche Markdown-Codeblöcke verwenden. Dekorative
  Überschriften, Badge-Serien, ASCII-Rahmen, wiederholte Zusammenfassungen und
  allgemeine Floskeln entfallen.
- XML ist ausgeschlossen. Kompaktes Markdown, Plaintext sowie JSON-/YAML-
  ähnliche Strukturen sind innerhalb desselben Renderers zulässig, wenn sie zum
  jeweiligen Inhalt nachweislich besser passen. Sie bilden weder parallele
  Renderer noch einen pauschalen serialisierten Ersatz-Envelope für
  `structuredContent`.

Der folgende Stil illustriert die Dichte, schreibt aber keine wortwörtliche
Syntax für jedes Tool fest:

```text
class AiNetLinter.Mcp.McpToolResults | src/AiNetLinter/Mcp/McpToolResults.cs:30 | id=s:...
```

```text
TRUNCATED 20/137 by=maxResults
NEXT retry maxResults=50
```

```text
ERROR INVALID_ARGUMENT field=$.depth
expected 1..3
RETRY depth=3
```

### Handoff-, Lebenszeit- und Fehlersicherheit

- Bestehende kanonische Handoff-Identität, Targetbindung, Snapshotbindung und
  Ownersemantik bleiben erhalten, sofern ein aktueller Gegentest nicht ihre
  fachliche Fehlerhaftigkeit belegt.
- Handoff-IDs werden nicht durch Anzeigenamen oder frei zusammengesetzte
  Clientstrings ersetzt. Sie wandern aus `structuredContent` direkt in den
  Content-Text.
- Ein Agent kann jede angebotene ID unverändert kopieren und im dokumentierten
  Folgetool verwenden.
- Stale-, Target-Mismatch- und Not-found-Fälle liefern im Content eine
  ausführbare Recovery. Interne Snapshotfingerprints werden nicht zusätzlich
  in jeder normalen Antwort angezeigt.
- Die bestehende `isError`-Policy bleibt ein Protokollvertrag und wird nicht
  durch versteckte Textkonventionen ersetzt. Ihre Dokumentation wird auf den
  Content-only-Vertrag bereinigt.
- Credentials, absolute interne Materialisierungspfade, unnötige PIDs und
  andere Betriebsidentitäten werden weder im Content noch in einem
  Ersatzkanal offengelegt.

### Budgetsemantik

`maxResponseBytes` begrenzt nach dem Hard Cut ausschließlich die UTF-8-Bytes
des finalen agentisch sichtbaren Textes in `content`. Der entfernte Kanal und
interne Modelle werden nicht mitgerechnet. JSON-RPC-Framing und SDK-
Envelope-Overhead sind nicht Teil dieses agentischen Inhaltsbudgets.

- Ein toolnaher Projektor wählt deterministisch vollständige Einheiten, bevor
  gerendert wird.
- Nach dem Rendern wird das tatsächliche Contentbudget geprüft.
- Es gibt keine generische nachträgliche Stringkürzung mitten in IDs,
  Pfaden, Signaturen, Code oder Fehleranleitungen.
- Passt die kleinste fachlich nutzbare Einheit nicht, folgt ein kompakter
  `RESPONSE_BUDGET_TOO_SMALL`-Fehler mit einem unmittelbar ausführbaren
  `minimumResponseBytes`-Retry.
- Der exakte Retrywert muss unter demselben Snapshot mindestens eine
  vollständige fachliche Einheit oder die vollständige unvermeidbare
  Status-/Fehlerantwort liefern.

## Muss-Kriterien

1. Kein AiNetLinter-Toolresult liest oder setzt
   `CallToolResult.StructuredContent`.
2. Auf dem Raw Wire fehlt `structuredContent` vollständig; ein explizites
   `structuredContent: null` ist nicht zulässig.
3. Jede Toolroute liefert genau einen nicht leeren Textcontent als alleinige
   fachliche Nutzlast.
4. Alle bisher ausschließlich strukturiert verfügbaren entscheidungsrelevanten
   Informationen werden entweder einmalig in den Content übernommen oder als
   nachweislich unnötig entfernt.
5. Kanonische IDs bleiben direkt verkettbar; kein Folgeaufruf erfordert das
   Parsen eines Anzeigenamens oder das Erfinden eines Identifikators.
6. Intern fachlich sinnvolle typisierte Modelle bleiben erhalten. Kein
   interner Consumer parst den gerenderten Text.
7. Es existiert genau eine Renderingstufe und keine vorbereitete alternative
   Ausgabeimplementierung.
8. Contentbudget, Auswahl und Recovery sind deterministisch und arbeiten auf
   vollständigen Evidenzeinheiten.
9. Öffentliche Toolbeschreibungen, Guides, Regeln und Tests kennen nur den
   Content-only-Vertrag.
10. Es verbleiben keine Legacy-, Dual-Write-, Adapter-, Feature-Flag- oder
    Formatparameterpfade für den entfernten Kanal.

## Akzeptanzkriterien

1. Ein globaler Architekturtest findet in Produktionscode keine Lese- oder
   Schreibreferenz auf `StructuredContent` und keine öffentliche
   `structuredContent`-Vertragsbeschreibung.
2. Ein Raw-Wire-Vertrag belegt für repräsentative erfolgreiche, leere,
   gekürzte und fehlerhafte Aufrufe die vollständige Abwesenheit des Feldes.
3. Der öffentliche Toolbestand kann mit minimalen gültigen und ungültigen
   Requests ausgeführt werden; jede beobachtete Toolantwort enthält genau
   einen nicht leeren Textblock und keine fachlichen Daten in anderen Kanälen.
4. `find_symbol -> get_symbol_body`, `get_file_skeleton -> get_symbol_body`,
   `get_feature_context -> Caller-Folgeaufruf` sowie repräsentative Source- und
   Assembly-Handoffs funktionieren ausschließlich mit IDs aus dem Content.
5. Complete, Empty, Partial, Truncated, Loading, korrigierbare Fehler und echte
   Malfunctions sind aus dem Content eindeutig unterscheidbar, ohne redundante
   Statusbäume.
6. Für eine repräsentative Matrix aus Symbolsuche, Body, Skelett, Callgraph,
   Featurekontext, Audit, Health und Assemblyanalyse ist der neue vollständige
   öffentliche Toolresult kleiner als die bisherige kombinierte
   Text-plus-StructuredContent-Nutzlast. Jede Abweichung muss fachlich
   begründet sein; ein pauschaler Prozentwert wird nicht optimiert.
7. In einem agentischen Dogfood-Audit kann ein frischer Agent die Antworten
   korrekt interpretieren, Handoffs ausführen, Trunkierung erkennen und
   Budgetfehler ohne Dokumentationssuche recovern.
8. Produktionscode enthält keine ausschließlich für den entfernten Wirekanal
   existierenden DTOs, Serializeroptionen, Projektoren oder Formatter.
9. FastTests prüfen Fachresultate, Auswahl und den einen Renderer;
   Integrationstests prüfen nur repräsentative reale MCP-/Prozess-/Assembly-
   Grenzen und nicht dieselbe Fallmatrix erneut.
10. Build, beide Non-Stress-Suiten und das MCP-Quality-Gate sind vollständig
    grün.

## Erster Rot-Test und Umsetzungsstrategie

Die Umsetzung beginnt mit einem absichtlich breiten Rot-Test
`McpContentOnlyContractTests`, nicht mit lokalen Löschungen.

Der erste Testvertrag erzwingt als globale Invariante:

```text
Für jedes öffentliche Toolresult:
  content enthält genau einen nicht leeren Textblock
  structuredContent fehlt auf dem Wire
```

Er umfasst mindestens die gemeinsamen Erfolgs-, Empty-, Loading-, Fehler- und
Budgetpfade und berichtet alle gefundenen Verstöße gesammelt, damit der
Hard-Cut-Fortschritt sichtbar bleibt. Ergänzend wird ein Produktions-
Architekturguard angelegt, der direkte `StructuredContent`-Lese- und
Schreibreferenzen sowie öffentlich verbliebene Vertragsliterale findet. Dieser
Guard ist für den ausdrücklich verlangten vollständigen Hard Cut zulässiger
und gewünschter Architekturschutz, obwohl er bewusst die verbotene
Implementierungsform prüft.

Der Rot-Test wird erst grün, wenn auch seltene Assembly-, Daemon-, Health-,
Config-, Feedback-, Loading- und Malfunction-Routen bereinigt sind. Lokale
Tooltests dürfen währenddessen schrittweise grün werden; der globale Guard
bleibt das unverhandelbare Endsignal gegen vergessene Pfade.

## Umsetzungsslices

### Slice 01 – Globalen Content-only-Vertrag rot absichern

- Den globalen Toolresult- und Produktionsguard zuerst anlegen.
- Repräsentative Raw-Wire-Negativtests für vorhandenes `structuredContent`
  ergänzen.
- Den heutigen Responsebestand und eine kleine Vergleichsmatrix für UTF-8-
  Größe und notwendige Agenteninformationen erfassen.

**Exit:** Die neuen Tests scheitern ausschließlich und nachvollziehbar am
vorhandenen Dual-Output-Vertrag; die Zielinvariante ist vollständig sichtbar.

### Slice 02 – Typisierte interne Ownership herstellen

- Scanner-, Analyse-, Composition- und Auswahlresultate von
  `CallToolResult`/JSON-Transportmodellen trennen.
- Composite-Tools auf direkte typisierte Fachresultate umstellen.
- Fachlich sinnvolle Records behalten oder umbenennen; reine Wire-DTOs und
  JSON-Reprojektoren entfernen, sobald ihr letzter Consumer entfällt.

**Exit:** Kein interner Fachconsumer benötigt gerenderten Text oder
`StructuredContent`; alle Daten erreichen den Renderer typisiert.

### Slice 03 – Einen Agentenrenderer und das Contentbudget etablieren

- Einen konkreten Renderer-Einstiegspunkt mit dem verbindlichen Content-Stil
  einführen.
- Handoff-IDs, nichttriviale Vollständigkeit und ausführbare Recovery einmalig
  in den Content integrieren.
- Budgetauswahl und exakten Mindest-Retry auf den finalen sichtbaren Text
  umstellen.
- Renderer- und Budgetvarianten als FastTests absichern.

**Exit:** Jede fachliche Antwort ist über den neuen Content allein vollständig,
deterministisch, lesbar und budgetierbar.

### Slice 04 – `structuredContent` restlos hart entfernen

- Alle Zuweisungen, Reads, Merge-/Navigation-/Final-Projection-Pfade und
  Structured-Wiretests entfernen oder auf den neuen Fach-/Contentvertrag
  umstellen.
- Keine leeren Hüllen, Nullfelder, veralteten Serializeroptionen oder
  vorsorglichen alternativen Renderer belassen.
- Alle öffentlichen Toolrouten einschließlich Fehler- und Lifecyclepfaden
  durch den einen Ergebnisvertrag führen.

**Exit:** Globaler Produktionsguard und Toolresult-Vertrag sind grün; eine
Negativsuche findet keinen aktiven öffentlichen StructuredContent-Vertrag.

### Slice 05 – Integrationstests und Dokumentation auf den Endzustand schneiden

- Fachvarianten in FastTests beim typisierten Owner oder Renderer halten.
- Pro realer Boundary nur repräsentative Wire-, Prozess-, MSBuild-/Roslyn- und
  Assembly-Verträge behalten.
- Server-Instructions, Toolbeschreibungen, Agent-Guide, API-/Integrationsdoku,
  `IsErrorPolicy.md` und MCP-Workflowregel auf Content-Handoffs umstellen.
- Die separate Roadmap zur Integrationstestverschlankung gegen den neuen
  Vertrag neu bewerten; ihre StructuredContent-Paritätsforderungen gelten nach
  diesem Hard Cut nicht mehr.

**Exit:** Code, Toolschemas, Runtime, Tests, Regeln und Dokumentation beschreiben
ausschließlich den Content-only-Endzustand.

### Slice 06 – Agentische Verifikation und Release-Gate

- Repräsentative Source-, Assembly-, Fehler-, Budget- und Handoffketten gegen
  einen frischen Host dogfooden.
- Vorher-/Nachher-Größen und Informationsgehalt der vereinbarten Responsematrix
  vergleichen.
- Tote Wiremodelle, Projektoren, Formatter, Serializeroptionen und Testhelper
  gezielt suchen und entfernen.
- Vollständiges Projekt-Release-Gate seriell schließen.

**Exit:** Ein Agent kann den Server ohne StructuredContent-Vertrag zuverlässig,
tokenarm und vollständig nutzen; kein entfernter Pfad bleibt im Produkt.

## Verifikation und Release-Gate

Während der Umsetzung laufen zunächst ausschließlich fokussierte FastTests und
die jeweils betroffenen repräsentativen Integrationstests. Vor Abschluss sind
seriell verpflichtend:

```powershell
dotnet build
dotnet test src/AiNetLinter.FastTests --filter Category!=Stress
dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress
git diff --check
```

Zusätzlich erforderlich:

- globaler `McpContentOnlyContractTests`-Nachweis;
- Produktions- und Doku-Negativsuche nach aktivem `StructuredContent`/
  `structuredContent`-Vertrag; historische Taskdokumente bleiben davon
  ausgenommen;
- Raw-Wire-Inventarprüfung des öffentlichen Toolbestands;
- Dogfoodketten für Source, Assembly, Empty, Partial, Truncated, Loading,
  InvalidArgument, Malfunction und Budgetretry;
- Vergleich des gesamten früheren Text-plus-Structured-Wireumfangs mit dem
  neuen Content-only-Ergebnis für die repräsentative Responsematrix;
- `safeguard` mit `minScore: 10`, `get_violations` ohne Verstöße sowie gezielte
  `find_dead_code`- und `find_magic_values`-Prüfungen gemäß Projektregel.

StressTests werden nicht automatisch ausgeführt. Ein grüner Build oder einzelne
Tooltests ersetzen weder den globalen Negativguard noch das agentische
Dogfooding.

## Dokumentationsbedarf

- `.agents/rules/AiNetLinter-McpWorkflow.mdc`: IDs und Status ausschließlich
  aus dem Content übernehmen; keine StructuredContent-Anweisung.
- `Docs/agent-api.md` und `Docs/integration.md`: Content-only-Vertrag,
  Handoffstil, Vollständigkeitsmarker und Budgetrecovery beschreiben.
- Runtime-Agent-Guide, `Docs/mcp-bootstrap.md`, Server-Instructions und
  Toolbeschreibungen: nur den neuen Agentenfluss nennen.
- `src/AiNetLinter/Mcp/IsErrorPolicy.md`: Fehlertexte und `isError` ohne
  strukturierten Nebenkanal eindeutig erklären.
- `Docs/configuration.md` und `ainetlinter-rules.json` nur ändern, wenn ihr
  tatsächlicher implementierter Vertrag betroffen ist.
- `README.md` bleibt ohne separaten ausdrücklichen Auftrag unverändert.

Öffentliche Dokumentation enthält keine Migrationsgeschichte und keine
Empfehlung, beide Formate zu unterstützen. Historische Taskartefakte werden
nicht rückwirkend umgeschrieben.

## Non-Goals

- Kein zweiter oder paralleler JSON-, YAML-, XML- oder
  StructuredContent-Renderer; passende Syntax innerhalb des einen
  Agentenrenderers bleibt zulässig.
- Keine Content-Negotiation und kein `format`-Parameter.
- Keine allgemeine öffentliche Codegraph-Dump-API.
- Keine Abschaffung intern typisierter Fachresultate.
- Keine Änderung der eigentlichen Roslyn-, Git-, Datei- oder Assemblyanalyse,
  sofern kein Rot-Test eine Kopplung an den entfernten Kanal belegt.
- Keine neuen UI-, IDE- oder programmatischen Consumer-Verträge.
- Keine Optimierung auf dekorative menschliche Darstellung.
- Keine automatische Ausführung von StressTests.

## Risiken und Gegenmaßnahmen

| Risiko | Gegenmaßnahme |
| --- | --- |
| Relevante IDs oder Completeness verschwinden zusammen mit dem Wiremodell. | Vor jeder Entfernung die agentische Aussage der bisherigen Payload inventarisieren; Content-Handoffs und Nicht-Complete-Marker zuerst rot absichern. |
| Der eine Renderer wird zur neuen Mega-Klasse. | Ein konkreter öffentlicher Einstiegspunkt mit kleinen privaten fachlichen Hilfen; keine zweite öffentliche Strategie oder Transportownership. |
| Interner Code beginnt Text zu parsen. | Typisierte Domänenergebnisse und direkte Composition als Architekturtest und Reviewkriterium erzwingen. |
| Freitext wird mehrdeutig oder schwer kopierbar. | Stabiler zeilenorientierter Stil, IDs direkt am Eintrag, deterministische Reihenfolge und agentische Dogfoodketten. |
| Moderne StructuredContent-fähige Clients verlieren maschinelle Typisierung. | Bewusster Non-Goal: AiNetLinter optimiert den universell sichtbaren Agentenkanal. Ein realer späterer Consumer rechtfertigt einen neuen Renderer, heutiger Spekulationsbedarf nicht. |
| Content wächst durch bislang unsichtbare Pflichtinformationen. | Redundante Navigation und Defaultmetadaten entfernen; gesamte frühere Dual-Payload statt nur den alten Text als Vergleichsbasis messen. |
| Generische Stringkürzung beschädigt Evidenz. | Auswahl kompletter typisierter Einheiten vor Rendering; finaler Fit-Check und exakter Mindest-Retry. |
| Parallel laufende Integrationsverschlankung erzeugt Konflikte oder neue Paritätstests. | Hard-Cut-Umsetzung erst auf sauberem bekannten Baseline-Commit starten; fremde laufende Änderungen nicht übernehmen. Danach verbleibende Roadmaps gegen den Endvertrag neu schneiden. |

## Verworfene Alternativen

- **Aktuellen Dual-Output-Vertrag behalten:** Erhält doppelte öffentliche
  Ownership, Budgetkopplung, Wartung und Tests ohne erforderlichen zweiten
  Produktkonsumenten.
- **StructuredContent behalten und nur Text verschlanken:** Löst die doppelte
  Buchführung und den unvollständigen kleinsten gemeinsamen Clientvertrag nicht.
- **Content und StructuredContent automatisch aus demselben Modell erzeugen:**
  Reduziert Drift, behält aber zwei Wirekopien, zwei öffentliche Verträge und
  deren Kompatibilitätskosten.
- **StructuredContent optional oder per Client/Formatparameter schalten:**
  Erzeugt Negotiation, Verzweigungen und Testmatrizen statt des gewünschten
  harten Schnitts.
- **Nur `structuredContent` verwenden:** Nicht der universell sichtbare
  Agentenkanal und damit kein tragfähiger Vertrag für alle Zielclients.
- **Alle internen DTOs zusammen mit dem Wirekanal löschen:** Würde sinnvolle
  Fachtypisierung durch Strings oder lose Maps ersetzen und die Architektur
  verschlechtern.

## Offene Entscheidungen

Keine. Der Hard Cut, der alleinige Content-Kanal, die Beibehaltung sinnvoller
interner Typisierung, ein konkreter Renderer ohne vorsorgliche Alternativen und
der globale erste Rot-Test sind verbindlich entschieden. Der Draft wartet nur
noch auf die ausdrückliche inhaltliche Freigabe des Nutzers.
