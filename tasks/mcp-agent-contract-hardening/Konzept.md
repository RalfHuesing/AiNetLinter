---
status: draft
execution_mode: autonomous
open_questions: []
---

# MCP-Agentenvertrag härten

## Freigabe- und Umsetzungsvertrag

Dieses Konzept beschreibt einen vollständigen, harten Schnitt auf einen einzigen
agentengerechten MCP-Vertrag. `execution_mode: autonomous` erlaubt dem späteren
Orchestrator, alle beschriebenen Slices seriell und ohne Zwischenfreigaben bis
zum Release-Gate umzusetzen. Lokale Typ-, Datei- und Methodennamen darf er an die
bestehende Architektur anpassen; Wireform, Semantik, Muss-Kriterien,
Akzeptanzkriterien und Gates sind verbindlich.

Der Status bleibt bewusst `draft`, bis der Nutzer das Konzept ausdrücklich
freigibt. Es gibt keine offene fachliche Nutzerentscheidung. Der Orchestrator
startet nicht automatisch.

### Verbindlicher harter Schnitt

- Nach der Umsetzung existiert genau ein öffentlicher MCP-Vertrag.
- Es gibt keine Legacy-, Kompatibilitäts-, Alias-, Migrations-, Dual-Read- oder
  Dual-Write-Pfade und keinen Feature-Schalter für die ersetzte Form.
- Ersetzte Parser, Formatter, Statusmodelle, DTO-Felder, Trimmer, Adapter,
  Tests, Fixtures, Snapshots und Dokumentationsbeispiele werden im selben Task
  vollständig gelöscht.
- Entfernte Felder bleiben weder als `null`, leerer Platzhalter noch als
  versteckter Alias erhalten. Unbekannte alte Felder sind `INVALID_ARGUMENT`.
- Produktdokumentation und READMEs beschreiben ausschließlich den nach der
  Umsetzung gültigen Ist-Zustand. Sie enthalten keine Historie,
  Migrationshinweise, Versionsvergleiche oder Formulierungen wie „früher“,
  „bisher“, „deprecated“ oder „weiterhin unterstützt“.
- Architektur- oder Linter-Verstöße werden fachlich behoben. Regeln werden
  nicht abgeschwächt, umgangen oder durch Ausnahmen neutralisiert. Zu große
  Verantwortungs- oder Namespace-Bereiche werden in fachliche Subnamespaces und
  kohärente Komponenten zerlegt.

## Ziel

Der AiNetLinter-MCP-Server soll für programmierende Agenten pro verbrauchtem
Kontext möglichst viel belastbare, direkt weiterverwendbare Semantik liefern:

- strukturierte Antworten ermöglichen Folgeaufrufe ohne Textparsing;
- Status, Scope, Analysequalität und Antwortvollständigkeit sind widerspruchsfrei;
- Budgets schützen konkrete Evidenz und liefern ausführbare Recovery-Hinweise;
- Source- und Assembly-Routen sind deterministisch, bounded und sicher verkettbar;
- Text enthält Fachbefund statt IDs, redundanter Navigation oder Betriebsrauschen;
- gemeinsame Vertragslogik besitzt genau einen Owner, toolfachliche Auswahl
  bleibt beim jeweiligen Tool;
- keine generischen Trimmer ohne Fachsemantik, keine Doppelmodelle, keine toten
  Adapter und keine parallelen Responsepfade bleiben zurück.

Der Erfolg wird nicht an möglichst kleinen Antworten gemessen. Eine Antwort ist
gut, wenn jedes zusätzliche Byte eine relevante Entscheidung, Evidenz oder
einen verlässlichen nächsten Schritt unterstützt.

## Problem und belegter Handlungsbedarf

Ein read-only Agentenaudit des veröffentlichten Source- und Assembly-Verhaltens
hat neun voneinander unabhängige Vertragsabweichungen bestätigt:

| ID | Priorität | Bestätigter Ist-Befund | Verbindliche Zielrichtung |
|---|---:|---|---|
| F1 | P0 | Feature-Kontext wiederholt die kanonische Handoff-ID im Markdown. | IDs ausschließlich einmalig im StructuredContent. |
| F2 | P0 | Exakter Namespace-Drilldown kann bei vorhandenem Namespace `totalCount=0`, keine Entries und zugleich Budgettrunkierung melden, selbst am Maximalbudget. | Prefix vor Projektion auflösen; Empty und Truncated disjunkt; echte Counts und erreichbarer Next-Schritt. |
| F3 | P0 | Assembly-Analysequalität und Response-Completeness widersprechen sich zwischen Payload, Navigation und Text. | Getrennte, eindeutig benannte Achsen mit einer gemeinsamen Projektion. |
| F4 | P1 | Klassen-, Feature- und Testkontext besitzen nicht durchgehend den gemeinsamen Scope-/Generated-Vertrag. | Ein Scope-Owner und identische Parametersemantik über alle betroffenen Tools. |
| F5 | P0 | `RESPONSE_BUDGET_TOO_SMALL` trägt gleichzeitig Erfolgsoperation und vollständige Completeness. | Fehlercode, Operation, Completeness und `isError` atomar konsistent. |
| F6 | P1 | Einzelne `find_symbol`-Validierungen liefern keinen stabilen Feld- oder Indexpfad. | Jede Requestverletzung nennt den exakten JSON-Pfad. |
| F7 | P1 | Ungültige Assembly-Detailstufen werden still als normale Analyse ausgeführt. | Enumvalidierung vor Lease, Decompilation und Projektion. |
| F8 | P0 | Assembly-Symbolsuche mit Referenzen kann am dokumentierten Defaultbudget scheitern; der gemeldete Mindestwert reicht beim Retry nicht. | Default repräsentiert die Mindestprojektion; exakter, beim Retry erfolgreicher Mindestwert. |
| F9 | P0 | Assembly-Referenzsuche kann `includeReferences=false` ignorieren und denselben Referenzraum wie `true` durchsuchen. | Root-only und bounded reference closure sind getrennte, gespiegelt ausgewiesene Routen. |

Vier weitere bestätigte Qualitätsbefunde gehören in denselben Scope:

- Composite-Texte wiederholen Status und Folgeschritt mehrfach.
- File-Tree- und Index-Counts sind fachlich plausibel, ihre unterschiedlichen
  Populationen und Ausschlusseinheiten aber nicht unmittelbar erkennbar.
- Zielgebundener Health-Text enthält unnötige globale Daemon-, Prozess- und
  andere Targetinformationen.
- Einzelne Assembly-Argumentfehler geraten in umfangreiche Analyseprojektionen
  statt in den knappen Fehlervertrag.

Bereits korrektes Verhalten bleibt durch Regressionstests geschützt: kompakte
stateless Source-/Assembly-Handoffs, Source-Chaining, Production-first-Ranking,
disjunkte Symbolscopes, Generated-Markierung, eindeutige Call-Graph-Nodes und
-Edges, vollständige Graph-Budgeteinheiten, überwiegend feldgenaue Fehler sowie
Isolation beim Wechsel zwischen Source- und Assembly-Targets.

## Source of Truth und betroffene Bereiche

### Fachliche Source of Truth

1. Roslyn-Symbole, `Solution`/`Project`/`Document`,
   `SymbolEqualityComparer`, die Assembly-Metadatenanalyse und der tatsächlich
   geladene Snapshot bestimmen fachliche Wahrheit.
2. Die normalisierten Contractmodelle unter `src/AiNetLinter/Mcp/` bestimmen
   Wirestatus, Handoff, Scope, Fehler und gemeinsame Navigation.
3. Toolnahe Domänenergebnisse und Budgetprojektoren bestimmen Auswahl,
   Reihenfolge, Counts und fachliche Mindestprojektion. Formatter stellen nur
   dar; sie erfinden oder korrigieren keinen Status.
4. Die mit `tools/list` veröffentlichten Schemas sind die allein gültige
   Requestoberfläche. Registrierung, Validierung und Handler verwenden dieselben
   Enumwerte, Defaults und Grenzwerte.
5. Contract-, Fast-, Integration- und Dogfood-Tests belegen den Vertrag.
   Produktdokumentation beschreibt ihn, ist aber keine Ersatzimplementierung.

Historische Taskdateien, Audittexte und Rohantworten sind keine Source of Truth
und werden weder als Runtime-Fixture noch als Produktdokumentation übernommen.

### Produktionsbereiche

- `src/AiNetLinter/Mcp/Registration/`: Navigation, Argumentvalidierung und
  Toolregistrierungen.
- `src/AiNetLinter/Mcp/Handoffs/` und
  `src/AiNetLinter/Mcp/Tools/SymbolGraph/`: Handoff-Auflösung, Symbolsuche,
  Referenzen und Assembly-Chaining.
- `src/AiNetLinter/Mcp/Scope/`: einzige Scope-/Generated-Klassifikation.
- `src/AiNetLinter/Mcp/Wire/` und toolnahe Response-Budgetklassen:
  UTF-8-Messung und fachliche Projektion.
- `src/AiNetLinter/Mcp/Tools/FeatureContext/`, `TestContext/`,
  `FileStructure/`, `ServerMaintenance/` und `AssemblyAnalysis/`.
- `src/AiNetLinter/Mcp/Assemblies/`: Sessions, Referenzauflösung,
  Decompilation, Registry und Analysequalität.
- `src/AiNetLinter/Mcp/IsErrorPolicy.md`, eingebettete Agent-Guides und
  Server-Instructions, soweit sie den aktiven Vertrag definieren.

### Tests und öffentliche Dokumentation

- Passende Gruppen unter `src/AiNetLinter.FastTests/Mcp/` und
  `src/AiNetLinter.IntegrationTests/Mcp/`.
- Repositoryeigene synthetische Source-/Assembly-Fixtures für Namespace,
  Generated, Scope, Budgets, Teilanalyse und Referenzgraphen.
- `Docs/agent-api.md`, `Docs/integration.md`, relevante Teile von
  `Docs/configuration.md`, `.agents/rules/AiNetLinter-McpWorkflow.mdc` und
  betroffene READMEs ausschließlich dann, wenn sie den MCP-Vertrag beschreiben.

Externe Assembly-Prüffälle bleiben vertraulich. Produktnamen, Pfade,
Namespaces, Symbole, Hashes, IDs und Rohantworten externer Fälle dürfen weder in
Konzept, Tests, Fixtures, Logs, Snapshots noch Produktdokumentation gelangen.

## Architekturziel und Ownership

### Ein Contractkern, toolnahe Fachprojektion

- Ein gemeinsames, unveränderliches Contractmodell besitzt Navigation,
  Status, Analysequalität, Scope, Next und Fehlersemantik. Source- und
  Assembly-Tools projizieren in dasselbe Modell.
- `McpArgumentValidationFilter` beziehungsweise ein fachlich gleichwertiger
  einzelner Owner validiert gemeinsame Typen, Grenzen, unbekannte Felder und
  Feldpfade vor dem Tooldispatch. Tools liefern ihre zulässigen Enums und
  fachlichen Cross-Field-Regeln, bauen aber keinen zweiten Fehlervertrag.
- `McpScopeClassifier` ist der einzige Owner für Production/Test/Unknown und
  Editable/Generated. Tools dürfen keine lokalen Test- oder Generated-
  Heuristiken unterhalten.
- Scanner erzeugen vollständige Domänenergebnisse. Toolnahe Budgetprojektoren
  wählen gerankte, vollständige semantische Einheiten. Formatter rendern exakt
  diese Auswahl in Text und StructuredContent.
- `McpResponseSize` darf kombinierte UTF-8-Bytes messen. Eine generische
  JSON-, Array-, String- oder „größte Sektion“-Kürzung ist verboten. Der
  vorhandene generische Trimmer und alle äquivalenten Pfade werden nach
  Umstellung sämtlicher Aufrufer gelöscht.
- Assembly-Referenzrouting besitzt einen Owner, der Rootsession,
  Referenzclosure, Grenzen, Diagnostics und Counts festlegt. Source- und
  Assembly-Varianten teilen Contractmodelle, nicht notwendigerweise Scanner.
- Health-Projektion trennt globalen Daemonzustand von zielgebundener
  Projekt-/Assembly-Gesundheit. Zielgebundene Antworten dürfen keine Daten
  anderer Targets ausgeben.

Neue oder bereits übergroße Bereiche werden anhand dieser Verantwortlichkeiten
in fachliche Subnamespaces zerlegt, etwa Contracts, Validation, Scope, Wire,
Assembly-Routing und toolnahe Projections. Ein bloßes Verschieben ohne klaren
Owner, ein pass-through Adapter oder ein zweites nahezu gleiches DTO ist kein
zulässiges Refactoring.

## Verbindlicher MCP-Responsevertrag

### Navigation und Status

Nach der Umsetzung existiert nur Contractversion `2`. Jede zielgebundene
Antwort nutzt dieselbe Navigationform; Version `1` wird weder erzeugt noch
akzeptiert oder dokumentiert.

```json
{
  "navigation": {
    "contractVersion": 2,
    "target": {
      "targetPath": "<canonical-target>",
      "analysisRoot": "<canonical-root>",
      "origin": "source|assembly"
    },
    "snapshot": {
      "fingerprint": "<fingerprint>",
      "kind": "source|assembly",
      "fresh": true
    },
    "status": {
      "operation": "ok|error",
      "completeness": "complete|empty|partial|truncated|not_applicable",
      "code": null
    },
    "analysis": {
      "mode": "source|decompiled|not_applicable",
      "quality": "complete|partial|unavailable|not_applicable",
      "limitationCodes": []
    },
    "scope": null,
    "next": null,
    "handoff": null
  }
}
```

- `status.completeness` beschreibt die nutzbare Antwort im angefragten Scope.
  `analysis.quality` beschreibt ausschließlich die Qualität des zugrunde
  liegenden Source-/Decompiler-Ergebnisses. `decompiled` ist ein Modus, keine
  Completeness.
- Die effektive Statuspriorität lautet: Fehler → `not_applicable`, Wire-/
  Mengenbegrenzung → `truncated`, unvollständige Analyse → `partial`, echte
  leere Vollmenge → `empty`, andernfalls `complete`. So kann eine Assembly-
  Response gleichzeitig `status.completeness=truncated` und
  `analysis.quality=partial` ausdrücken, ohne zwei Bedeutungen zu vermischen.
- `complete` ist nur zulässig, wenn Analysequalität und gefilterte Population
  für die behauptete Aussage vollständig sind und keine Ausgabe abgeschnitten
  wurde. Partielle Decompilation kann nie als `complete` erscheinen.
- `empty` ist nur bei vollständig geprüfter, leerer gefilterter Population
  zulässig. `empty` und `truncated` schließen einander aus.
- Ein erfolgreicher leerer Call hat `operation=ok`, `completeness=empty` und
  `code=null`. Ein Fehler hat `operation=error`,
  `completeness=not_applicable`, einen nichtleeren `code` und `isError=true`.
  `operation=ok` mit Fehlercode ist verboten.
- Rootpayload, Navigation, Textfooter und `isError` werden aus einem
  Statusobjekt abgeleitet. Toollokale konkurrierende Statusfelder werden
  gelöscht oder fachlich eindeutig umbenannt.
- `next=null`, wenn keine Aktion erforderlich ist. Andernfalls existiert genau
  eine erreichbare, strukturierte Recovery- oder Verfeinerungsaktion; Text
  wiederholt sie höchstens einmal.

### Handoffs und Text

- Kanonische Source- und Assembly-Handoff-IDs behalten das stateless Format
  mit Origin, Targettoken, Contenttoken und Roslyn-Dokumentations-ID.
- Eine Handoff-ID erscheint ausschließlich in dafür vorgesehenen Feldern des
  StructuredContent und dort pro fachlicher Identität nur einmal. Markdown,
  Statuszeilen, Diagnostics und Fehlerechos enthalten keine Handoff-ID.
- Ein Feld, das die komplette Handoff-ID trägt, heißt `id`; parallele
  `docCommentId`-, `symbolId`- oder Aliasfelder mit demselben Wert entfallen.
- Folgeaufrufe übernehmen die ID unverändert. Graphlokale Node-IDs sind
  responsegebunden und werden nie als `symbolIdentifier` akzeptiert.
- Handoffs bleiben über Registry-Eviction und Serverneustart gültig, solange
  Target und Inhalt unverändert sind. Falsches Target ist `TARGET_MISMATCH`,
  geänderter Inhalt `STALE_SNAPSHOT`, ungültige Form `INVALID_ARGUMENT` am
  konkreten Feld; Auflösung verwendet nie First-Match.
- Lange oder vertrauliche Eingaben werden in Fehlern nicht vollständig
  wiederholt.
- `ok + complete` braucht keinen Navigationsfooter. Empty, Partial, Truncated
  und Error erhalten höchstens eine kurze Statuszeile und eine Aktion.

### Scope und Generated

Folgende Tools veröffentlichen denselben Requestvertrag:

```text
scopeType: all | production | tests     Default: all
includeGenerated: false | true         Default: false
```

Verbindlich betroffen sind mindestens `find_symbol`, `find_references`,
`get_call_tree`, `dependency_graph`, `get_class_structure`,
`get_feature_context` und `get_test_context`.

- `scopeType` filtert jede auswählbare Evidenzcollection vor Ranking,
  `maxResults`, Traversal und Bytebudget. Die zur Identifikation des Seeds
  nötige Deklaration darf sichtbar bleiben, muss aber als Seed und mit ihrem
  eigenen Scope/SourceKind gekennzeichnet sein und zählt nicht in gefilterte
  Ergebniscounts.
- Beim Feature-Kontext filtert Scope Caller, Referenzen, verwandte Symbole und
  Testevidenz. `production` behauptet keine Testtreffer; `tests` behauptet keine
  Production-Caller. Seedmetriken und Seedviolations bleiben als Eigenschaften
  des angefragten Symbols getrennt sichtbar.
- Beim Testkontext führt `production` zu einem echten, vollständig geprüften
  Empty, sofern keine Production-Evidenzklasse definiert ist. Es gibt keinen
  stillen Fallback auf `all`.
- `includeGenerated=false` schließt Generated-Einträge und -Locations aus.
  Nur zur Erhaltung einer Graphkante notwendige Generated-Brücken dürfen
  kompakt sichtbar bleiben; sie sind stets `sourceKind=generated`, zählen
  separat und werden nie als editierbare Evidenz dargestellt.
- Klassenstrukturen gruppieren Partial-Deklarationen nach Symbolidentität,
  markieren jede Location und wenden den Scope vor Member-/Dateilimits an.
- `Unknown` bleibt nur unter `all` sichtbar. Ungültige oder leere unbekannte
  Werte werden nicht still zu `all` normalisiert.
- `totalCount` misst die vollständige gefilterte Population, `shownCount` die
  sichtbare Projektion. Ausgeschlossene Scope-/Generated-Mengen werden separat
  und mit Einheit ausgewiesen.

### Budgets

- Öffentliches `maxResponseBytes` verwendet die bestehenden zentralen
  Mindest-/Höchstgrenzen; Registration, Schema, Validierung und Implementierung
  teilen dieselben Konstanten.
- Jedes Tool besitzt eine fachliche Mindestprojektion und einen dokumentierten
  Default, der diese Projektion für jeden gültigen Request darstellen kann.
  Ein gültiger Defaultcall darf nicht allein am Envelope scheitern.
- Auswahlreihenfolge: vollständiges Domainergebnis → Scope/Generated → stabile
  Fachrangfolge → vollständige semantische Einheiten → Text und
  StructuredContent aus derselben Auswahl → Navigation → kombinierte
  UTF-8-Messung.
- Es wird nie mitten in Symbol, Location, Graphkante, Diagnostic, String oder
  JSON-Struktur gekürzt. Counts-only ist bei vorhandener Evidenz kein Erfolg.
- Passt die kleinste fachliche Einheit nicht, lautet der Fehler
  `RESPONSE_BUDGET_TOO_SMALL` mit `fieldPath=$.maxResponseBytes`,
  `requestedBytes` und `minimumResponseBytes`.
- `minimumResponseBytes` ist für exakt denselben Request und Snapshot
  ausführbar: Ein Retry mit genau diesem Wert muss erfolgreich sein. Der Wert
  wird aus der finalen Mindestprojektion inklusive Navigation und Encoding
  berechnet, nicht geschätzt oder auf einen generischen Sockel gesetzt.
- Größeres Budget fügt nur ganze, stabil gerankte Facheinheiten hinzu und
  entfernt keine zuvor sichtbare Einheit. Text und StructuredContent bleiben
  fachlich deckungsgleich.
- Assembly-Symbolsuche mit `includeReferences=true` funktioniert am
  veröffentlichten Defaultbudget und schützt mindestens Identität, direkten
  Treffer, Herkunft, Referenzscope, Analysequalität, Counts und Next.
- Generische JSON-/Stringtrimmer und nachträgliche Payloadmutation werden
  vollständig entfernt. Gemeinsame Wirelogik misst, toolnahe Projektoren
  entscheiden.

### Fehler und Fallback

- Unbekannte Felder, falsche JSON-Typen, ungültige Enums, leere
  Pflichtwerte/-elemente und Grenzwertverletzungen werden vor Targetlease,
  Decompilation, Scan und Budgetprojektion abgelehnt.
- Jeder `INVALID_ARGUMENT` enthält `fieldPath`; Arrayfehler verwenden den
  exakten Index, beispielsweise `$.namePatterns[0]`. Enumfehler nennen die
  zulässigen Werte strukturiert.
- `find_symbol.kind` und alle veröffentlichten Assembly-`detailLevel`-Felder
  werden strikt gegen ihr Schema validiert. Ein unbekannter Wert startet keine
  Analyse und fällt nicht auf einen Default zurück.
- Argumentfehler verwenden ausschließlich den knappen Fehlervertrag, keine
  normalen Ergebnisarrays, Decompilerdiagnosen, Zielstatistiken oder
  umfangreiche Analyseprojektion.
- Erwartete Fehler sind recoverable, enthalten keinen Stacktrace und
  verschmutzen keinen Folgecall.
- Fehlende Assemblyreferenzen oder Decompilergrenzen dürfen bounded lokale
  Evidenz liefern, müssen dann aber `analysis.quality=partial`, konkrete
  `limitationCodes`, ehrliche Counts und einen erreichbaren Next-Schritt
  ausweisen. Sie werden nie silent-empty oder `complete`.
- Unentscheidbare Assembly-Anwendbarkeit bleibt explizit `not_decidable`; sie
  wird weder als Treffer noch als Fehler umgedeutet.

### Assembly-Routen und Referenzscope

- Die Dateiendung des absoluten `targetPath` bestimmt Source oder Assembly.
  Es gibt keine automatische Targetsuche und keinen Source-/Assembly-Fallback.
- `includeReferences=false` öffnet und durchsucht ausschließlich die
  Rootassembly. Navigation spiegelt `requestedIncludeReferences=false` und
  `effectiveIncludeReferences=false`; Referenzsessions und referenzierte
  Suchcounts sind null beziehungsweise `0`.
- `includeReferences=true` aktiviert eine bounded, deterministische
  Referenzclosure. Navigation spiegelt requested/effective, Suchgrenzen sowie
  Root-, entdeckte, geöffnete, durchsuchte, übersprungene und fehlgeschlagene
  Assemblyanzahlen mit eindeutig benannter Einheit.
- Ein Budget, Cachetreffer oder bereits geöffnete Session darf `false` niemals
  intern auf `true` anheben. Der Cachekey enthält Target, Snapshot,
  Include-References-Modus und fachlich relevante Optionen.
- Handoffs aus einer referenzierten Assembly tragen deren tatsächliche
  Herkunft einmal am Entry und lösen gegen dieses Contenttarget auf. Es gibt
  keine Aliasmap und keine Root-Umetikettierung.
- Source- und Assembly-Aufrufe im selben Serverprozess beeinflussen weder
  Snapshot, Scope, Counts noch Handoff-Auflösung des jeweils anderen Targets.
- Assemblies werden metadata-only analysiert und niemals ausgeführt oder
  dynamisch in den Produktprozess geladen.

### Namespace- und Populationsemantik

- `namespacePrefix` wird vor Pagination, `maxResults` und Byteprojektion exakt
  aufgelöst. Der gefundene Namespace ist selbst Root; direkte Typen sind nicht
  von vorhandenen Child-Namespaces abhängig.
- Bei vorhandener Population enthält eine trunkierte Antwort mindestens eine
  fachliche Einheit. Passt selbst die Mindestprojektion nicht, kommt der
  ausführbare Budgetfehler statt `totalCount=0`.
- Bei tatsächlichem Nichtfund: `operation=ok`, `completeness=empty`, echte
  Counts und `next=null` oder genau ein fachlicher Verfeinerungsschritt. Bei
  maximalem Budget darf niemals „Budget erhöhen“ empfohlen werden.
- File-Tree zählt physische Dateien und Verzeichnisse, Index-Scope zählt
  Roslyn-Dokumente. Die Payloads verwenden deshalb keine scheinbar
  austauschbaren Sammelcounts.
- File-Tree benennt mindestens `physicalFileCount`, `matchedFileCount`,
  `shownFileCount`, `excludedFileCount` und `skippedDirectoryCount`; nutzerseitige
  Pattern-Ausschlüsse und standardmäßig übersprungene Verzeichnisse bleiben
  getrennt.
- Index-Scope benennt mindestens `roslynDocumentCount`,
  `indexedDocumentCount`, `generatedDocumentCount` und `testDocumentCount`.
  Nicht anwendbare Counts fehlen statt eine andere Population zu imitieren.

## Muss-Kriterien

1. Ein einziger atomarer Contract-v2-Envelope ohne alte Responseform,
   Aliasfelder oder Parallelmodelle.
2. Handoff-IDs ausschließlich einmalig im StructuredContent; kein regulärer
   Text-, Status-, Diagnostic- oder Fehlerecho-Leak.
3. Widerspruchsfreie Operation-, Completeness-, Analysequalitäts-, Code- und
   `isError`-Semantik aus einem Owner.
4. Exakter Namespace-Drilldown mit ehrlichen Populationen, disjunktem
   Empty/Truncated und ausführbarer Budget-Recovery.
5. Gemeinsamer Scope-/Generated-Vertrag für alle Symbol-, Graph-, Klassen- und
   Composite-Kontexttools.
6. Toolfachliche Mindestprojektionen und exakte, monotone kombinierte
   UTF-8-Budgets; keine generischen Trimmer oder nachträgliche JSON-Mutation.
7. Strikte, feld- und indexgenaue Validierung aller öffentlichen Argumente vor
   jeder teuren oder fachlichen Arbeit.
8. Deterministisch getrennte Assembly-Routen für Root-only und bounded
   Referenzclosure einschließlich ehrlicher Counts und Analysegrenzen.
9. Knappe Textantworten mit höchstens einem Status und einem Next-Schritt;
   zielgebundener Health-Text enthält nur das angefragte Target.
10. Eindeutig benannte File-/Dokumentpopulationen und Ausschlusseinheiten.
11. Klare fachliche Namespaces und Owner; keine Doppelmodelle, toten Adapter,
    Middlemen, Magic Values oder regelwidrigen Großbereiche.
12. Vollständige Endzustandsdokumentation, Dogfood, Auditorprüfung und grüne
    Release-Gates ohne offene auftragsbezogene Findings.

## Akzeptanzkriterien

### Response, Status und Handoff

1. Jede zielgebundene Response trägt ausschließlich `contractVersion=2`; eine
   repositoryweite Suche findet keinen v1-Producer, -Parser, Test oder Beispiel.
2. Source-, Assembly-, Success-, Empty-, Partial-, Truncated- und Errorfälle
   werden aus demselben Statusmodell projiziert.
3. `operation=ok` besitzt immer `code=null`; jeder Fehler besitzt
   `operation=error`, `completeness=not_applicable`, nichtleeren Code und
   `isError=true`.
4. Eine partielle Assemblyanalyse erscheint nie als `complete`. Text,
   Navigation und fachlicher Payload widersprechen sich nicht.
5. Response-Completeness und Analysequalität sind unterschiedlich benannt und
   in kombinierten Fällen unabhängig prüfbar, etwa Partial plus Truncated.
6. `RESPONSE_BUDGET_TOO_SMALL` erfüllt den Fehlervertrag und kann von keinem
   Client als vollständiger Erfolg gelesen werden.
7. Keine reguläre Textantwort enthält eine Source- oder Assembly-Handoff-ID.
8. StructuredContent enthält pro Symbol genau ein kanonisches Handofffeld;
   gleichwertige `docCommentId`-/`symbolId`-Kopien sind entfernt.
9. Source- und Assembly-Handoffs funktionieren nach Eviction und Neustart bei
   unverändertem Inhalt; Target- und Snapshotabweichung liefern die vorgesehenen
   disjunkten Codes.
10. Zehn Entries serialisieren Roottarget, Snapshot, Status und Follow-up-
    Toolmengen jeweils höchstens einmal.

### Scope, Namespace und Population

11. Alle sieben verbindlich betroffenen Tools veröffentlichen dieselben beiden
    Scopeparameter mit denselben Defaults und Enumwerten.
12. `all`, `production` und `tests` sind nach Seed-Ausnahme disjunkt und werden
    vor Limits/Budget angewandt; Invalidwerte zeigen `$.scopeType`.
13. `includeGenerated=false` blendet Generated-Evidenz aus; `true` zeigt sie
    markiert. Notwendige Graphbrücken sind separat markiert und gezählt.
14. `get_class_structure` markiert jede Location mit Scope/SourceKind, gruppiert
    Partials und mischt Generated nicht unmarkiert in editierbare Quellen.
15. Feature-Kontext respektiert Scope in Caller-, Related- und Testsektionen;
    Testkontext fällt bei Production nicht still auf All zurück.
16. Exakter Namespace-Prefix liefert am Default- und Maximalbudget den Root und
    direkte Typen oder einen ausführbaren Mindestbudgetfehler, niemals falsches
    Empty plus Truncated.
17. `totalCount` bleibt bei Trunkierung die gefilterte Vollmenge; `shownCount`
    misst ausschließlich sichtbare Einheiten.
18. File-Tree- und Index-Scope-Responses nennen Populationseinheit und
    Ausschlüsse eindeutig; übersprungene Verzeichnisse sind keine
    ausgeschlossenen Dateien.

### Validierung und Budgets

19. Ungültiges `find_symbol.kind` liefert `INVALID_ARGUMENT` mit
    `fieldPath=$.kind` und den erlaubten Werten.
20. Jedes leere `namePatterns`-Element liefert den exakten Indexpfad.
21. Jede Assembly-Detailstufe außerhalb des veröffentlichten Enums wird vor
    Assemblyarbeit mit `fieldPath=$.detailLevel` abgelehnt.
22. Ein Contracttest prüft alle in `tools/list` veröffentlichten Enumfelder
    gegen Registrierung und Validator; kein unbekannter Wert wird still
    übernommen.
23. Fehler auf gültigem Assemblytarget enthalten keine normale Analysepayload,
    keine Decompilerdiagnosen und keine umfangreichen Targetmetadaten.
24. Jeder dokumentierte Default stellt die fachliche Mindestprojektion eines
    gültigen Requests dar.
25. Der gemeldete `minimumResponseBytes`-Wert führt bei identischem Request und
    Snapshot exakt zum erfolgreichen Retry.
26. Text-UTF8 plus StructuredContent-UTF8 inklusive Navigation bleibt innerhalb
    des effektiven Budgets.
27. Größeres Budget erweitert die sichtbare Evidenz monoton um ganze Einheiten;
    ein erfolgreicher Counts-only-Fallback ist ausgeschlossen.
28. Kein Produktionscode enthält generische JSON-/Array-/Stringtrimmer oder
    deren alte Tests; gemeinsame Wirelogik misst nur.

### Assembly-Chaining und Signalqualität

29. Assembly-`find_symbol(includeReferences=true)` funktioniert mit dem
    veröffentlichten Default und liefert mindestens einen handlungsfähigen
    Treffer oder einen fachlich echten Empty-/Partial-Zustand.
30. `find_references(includeReferences=false)` durchsucht nur die Rootassembly,
    spiegelt requested/effective `false` und meldet keine geöffneten
    Referenzsessions.
31. Derselbe Call mit `true` nutzt ausschließlich die bounded Referenzclosure,
    spiegelt requested/effective `true` und weist alle Assemblycounts aus.
32. False und True besitzen getrennte Cache-/Ausführungspfade; ein vorausgehender
    True-Call kontaminiert den folgenden False-Call nicht und umgekehrt.
33. Handoff eines Treffers aus einer referenzierten Assembly funktioniert bei
    nachfolgendem Body-/Reference-Call ohne Root-Umetikettierung.
34. Fehlende Referenzen liefern lokale Evidenz plus `analysis.quality=partial`
    und Limitations, nicht Silent-Empty oder Complete.
35. Zielgebundener Health-Text enthält weder PID, globale Daemon-Keys noch
    andere Targetpfade; globaler Health behält globale Aggregate.
36. Feature- und Testkontext geben Status und denselben Next-Schritt im Text
    jeweils höchstens einmal aus.
37. Nach einem Validierungs-, Budget- oder Assemblyfehler funktioniert ein
    gültiger Folgecall ohne Zustandskontamination.

### Architektur, Dokumentation und Erhalt

38. Source- und Assembly-Projektionen verwenden dasselbe Contractmodell; eine
    Typ-/Verwendungsprüfung findet keine zweite gleichwertige Status-, Scope-
    oder Fehlerhierarchie.
39. Toollokale Test-/Generated-Heuristiken, pass-through Adapter, ersetzte DTOs,
    unreferenzierte Typen und Magic Values sind entfernt.
40. Alle im Änderungsscope auftretenden Violations sind fachlich behoben;
    Namespace-/Größenverstöße führen zu sinnvoller Zerlegung statt Suppression.
41. Call-Graph-Node-/Edge-Eindeutigkeit, Rendererparität, Production-first-
    Ranking, Partial-Gruppierung und Source-/Assembly-Isolation bleiben grün.
42. Produktdokumentation, READMEs, Guides, Server-Instructions und Beispiele
    zeigen ausschließlich Contract v2 und das finale Scope-, Budget-, Status-
    und Assembly-Verhalten ohne Historien- oder Migrationssprache.
43. Kein Test, Snapshot, Log oder Dokument enthält vertrauliche externe
    Assemblyidentitäten oder Rohantworten.
44. Sämtliche Test-, Dogfood-, Auditor-, Dokumentations- und Release-Gates sind
    grün; es bleibt kein offenes auftragsbezogenes Finding.

## Non-Goals

- Keine neue Lint-Capability-Matrix im Health-Envelope. Targetherkunft,
  Analysequalität und bestehende Toolverträge reichen für diesen Scope.
- Keine erfundenen Testmethoden für reine `DirectTypeUse`-, Typkonventions-
  oder andere Type-Level-Evidenz. Methodennamen bleiben konkreter
  Member-/Invocation-Evidenz vorbehalten.
- Keine Änderung externer MCP-Clients, Modellprompts oder deren
  StructuredContent-Serialisierung.
- Kein allgemeiner Ersatz für Textsuche, Dateilesen, Git oder Roslyn.
- Kein exaktes, modellunabhängiges Tokenbudget; Servergrenze bleiben
  kombinierte UTF-8-Bytes.
- Keine unbounded Assembly-Referenzanalyse und kein Ausführen oder dynamisches
  Laden untersuchter Assemblies.
- Kein allgemeiner Neuaufbau von Lint-, Metrik-, Call-Graph-,
  Testevidenz- oder Decompilationsfachlichkeit außerhalb der benannten
  Vertragsabweichungen und notwendigen Architekturrefactorings.
- Keine Aufnahme historischer Auditdateien oder externer Fälle als
  Produktfixture.
- Keine automatischen Stressläufe und kein Publish/Deployment in diesem Task.
- Keine Bewahrung alter Responseformen zu Diagnose- oder Migrationszwecken.

## Architektur- und Betriebsannahmen

- AiNetLinter bleibt ein eigenständiges, statisch kompiliertes CLI-/MCP-Tool
  ohne DI-Container, Pluginmechanismus, `AssemblyLoadContext` oder dynamisches
  Laden fremder Assemblies.
- Zielgebundene Calls verwenden genau einen absoluten existierenden
  `.sln`/`.slnx`- oder `.dll`/`.exe`-Pfad; die Endung bestimmt die Route.
- Registry-Leases, Caches und Assemblysessions sind serverinterne
  Lebenszeitoptimierungen. Öffentliche Handoffs bleiben stateless und hängen
  nur von Target und Snapshotinhalt ab.
- Scope- und Budgetdefaults sind Teil des öffentlichen Vertrags und stehen in
  `tools/list`; Dokumentation dupliziert nur entscheidungsrelevante Beispiele.
- Teure Roslyn- und Decompilerarbeit wird nicht wiederholt, wenn ein
  snapshotgebundener Cache dasselbe fachliche Ergebnis liefern kann. Cachekeys
  enthalten alle semantikverändernden Requestdimensionen.
- Stress-Tests laufen nur auf ausdrückliche Anforderung.

## Serieller Umsetzungsplan

Für jeden Slice gilt: Arbeitsbaum und Baseline prüfen, genannten Scope plus
direkte Abhängigkeiten lesen, einen reproduzierenden Red-Test schreiben,
kleinstmöglich fachlich implementieren, fokussierte Tests grün ausführen,
Violations prüfen, ersetzte Strukturen löschen, Diff und `git diff --check`
prüfen und erst danach den nächsten Slice beginnen. Es laufen nie zwei Rollen,
Builds, Tests oder MCP-Prüfungen parallel.

### Slice 01 – Status- und Analysevertrag atomar normalisieren

**Lesen:** Navigationprojektion/-text, `McpToolResults`, `IsErrorPolicy.md`,
Assembly-Navigation-/Responsebuilder, Feature-/Test-Budgetfehler und zugehörige
Contracttests.

**Bauen:** Ein Contract-v2-Statusmodell mit getrennten Achsen für
Response-Completeness und Analysequalität; einheitliche Projektion in Text,
StructuredContent und `isError`; alte v1-Felder und konkurrierende Statusmodelle
atomar entfernen.

**Rot:** Kombinationen Source Complete/Empty/Truncated, Assembly
Partial/Truncated sowie `RESPONSE_BUDGET_TOO_SMALL` reproduzieren. Vorher müssen
die belegten Widersprüche sichtbar fehlschlagen.

**Exit:** AK 1–6 grün; kein Producer/Parser/Test für Contract v1; partielle
Assemblyanalyse und Budgetfehler sind maschinenlesbar eindeutig.

### Slice 02 – Validierung vor Dispatch vereinheitlichen

**Lesen:** alle Toolregistrierungen, `McpArgumentValidationFilter`, Input-
Normalizer, `find_symbol`-Batch-/Kindlogik, Assembly-Detaillevel und
Argumentvalidierungs-E2E-Tests.

**Bauen:** Gemeinsame Feld-/Indexpfade, Enumquellen und knappen Fehlervertrag;
Cross-Field-Regeln toolnah; Validierung vor Lease/Decompilation; unbekannte
oder leere Enumwerte ohne stillen Default ablehnen.

**Rot:** Unbekanntes Kind, leeres Batchelement, ungültige Assembly-Detailstufe,
unbekanntes Feld, falscher JSON-Typ und gültiger Retry.

**Exit:** AK 19–23 und 37 grün; `tools/list`, Validator und Handler besitzen
keine abweichenden Enumlisten oder Defaults; Invalid-Assembly-Calls starten
keine Analyse.

### Slice 03 – Scope und Generated auf Klassen-/Composite-Tools erweitern

**Lesen:** `Mcp/Scope`, Symbol-/Graph-Scopereferenz, ClassStructure,
FeatureContext, TestContext, TestDetector und alle jeweiligen Modelle,
Registrierungen, Formatter, Budgets und Tests.

**Bauen:** Ein gemeinsamer Scopeinput und eine gemeinsame Klassifikation für
alle sieben Tools; Seed-Ausnahme explizit modellieren; Scope vor Limits und
Budgets; Partial-Locations gruppieren; toollokale Heuristiken löschen.

**Rot:** Gemischte Production-/Test-/Generated-Fixture, Partialtyp,
Featurecaller und Testevidenz für `all|production|tests` sowie Generated Opt-in.

**Exit:** AK 11–15, 17 und 38–39 grün; Toolschemas sind identisch; keine
unmarkierte Generated-Datei und kein stiller Fallback auf All.

### Slice 04 – Namespace-Drilldown und Counts reparieren

**Lesen:** Namespace-Scanner, Projection, ResponseBudget, Modelle,
Registrierung und Fast-/Dogfoodtests.

**Bauen:** Prefixauflösung vor Projektion; Root plus direkte Typen;
vollständige gefilterte Counts; fachliche Mindestprojektion und erreichbares
Next; Empty/Truncated strikt disjunkt.

**Rot:** Existierender Leaf-Namespace am Default- und Maximalbudget,
nicht existierender Namespace, Namespace mit Children, direkter Typ ohne Child
und Budget unter dem tatsächlichen Minimum.

**Exit:** AK 16–17 und Budget-AK 24–27 grün; kein vorhandener Namespace wird
als `totalCount=0` ausgegeben und kein Maximalbudget empfiehlt Erhöhung.

### Slice 05 – Fachliche Budgetarchitektur und Assembly-Mindestprojektion

**Lesen:** alle Klassen unter `Mcp/Wire`, gemeinsame Budgetlimits,
FindSymbol-/Feature-/Test-/Class-/Namespace-/CallGraph-Budgetprojektoren,
Assembly-ResponseLimits und Budgettests.

**Bauen:** Toolnahe Mindestprojektionen, finale kombinierte Messung,
deterministische Berechnung von `minimumResponseBytes`; Assembly-
Referenzsuche am Default repräsentierbar machen; generischen Trimmer und alle
Aufrufer/Tests vollständig löschen.

**Rot:** Defaultbudget mit großer Referenzpopulation, 512/Minimum/Maximum,
Retry exakt am gemeldeten Minimum, monotone Budgets und Navigationsoverhead.

**Exit:** AK 24–29 grün; keine generische Payloadmutation; Defaultcalls liefern
Fachevidenz und jeder Mindestwert ist ausführbar.

### Slice 06 – Assembly-Referenzrouting und Handoff-Chaining trennen

**Lesen:** AssemblyFindSymbol/-References, ReferenceNavigator/-Resolver,
Sessionexpander, Cachekeys, Navigationmodelle, Handoff-Lifecycle- und
IncludeReferences-Integrationtests.

**Bauen:** Explizite Root-only- und bounded-Closure-Requests; requested/effective
Spiegelung; eindeutige Zähler; semantikvollständige Cachekeys; korrekte
Herkunft referenzierter Handoffs; partielle Referenzfehler in Analysequalität.

**Rot:** False nach True, True nach False, kalter/warm gecachter Call,
fehlende Referenz, referenzierter Treffer mit Body-/Reference-Handoff,
Eviction/Neustart und Source-/Assembly-Wechsel.

**Exit:** AK 29–34, 37 und 41 grün; False öffnet keine Referenzsession, True
bleibt bounded, Cache und Handoff ändern den angefragten Scope nicht.

### Slice 07 – ID-Leaks und Text-Signalqualität schließen

**Lesen:** Feature-/Test-/Symbolformatter, Navigationstext, Diagnostics-
Formatter, Health-Responsebuilder/-Formatter, Handofffelder und Textsnapshots.

**Bauen:** IDs aus allen Textpfaden entfernen; ein kanonisches strukturiertes
ID-Feld; doppelte Status-/Next-Zeilen konsolidieren; zielgebundenen Health-Text
auf Targetstatus reduzieren; globale Projektion nur global verwenden.

**Rot:** Source- und Assembly-Featuretexte, Fehlertexte, truncierte Feature-/
Testkontexte sowie globaler versus zielgebundener Health.

**Exit:** AK 7–10 und 35–36 grün; Text enthält keine IDs oder fremde
Target-/Daemoninformationen und wiederholt keine Folgeaktion.

### Slice 08 – Discovery-Populationen und Ausschlüsse präzisieren

**Lesen:** FileTree- und IndexScope-Scanner, Records, Renderer,
Registrierungen, Ausschlusslogik, Tests und relevante Dokumentation.

**Bauen:** Physische Datei-/Verzeichniseinheiten von Roslyn-Dokumenten
trennen; benannte Counts; Defaultverzeichnisse und Nutzerpatterns separat;
mehrdeutige alte Sammelfelder samt Tests löschen.

**Rot:** Rootsummary, C#-Filter, Nutzerexclude, Defaultskip, Generated- und
Testdokumente aus derselben synthetischen Solution.

**Exit:** AK 18 grün; jede Zahl besitzt eine erkennbare Population und Einheit;
File-Tree und Index-Scope behaupten keine Gleichheit verschiedener Mengen.

### Slice 09 – Architekturhärtung und vollständiger harter Schnitt

**Lesen:** Vollständiger Diff, Referenzen aller ersetzten Typen/Felder,
Namespace-/Komplexitätsviolations, Contract-/DTO-/Formatter-/Adapterstruktur.

**Bauen:** Tote oder doppelte Pfade, DTOs, Parser, Formatter, Trimmer, Adapter,
Tests und Snapshots löschen; fachliche Namespaces/Dateien zerlegen; Magic
Values zentralisieren; alle im Scope gefundenen Violations sachlich beheben.

**Rot:** Architekturtests und gezielte Negativsuchen für v1, alte Felder,
Aliasnamen, generische Trimmer, lokale Scopeheuristiken und unreferenzierte
Adapter müssen vor Bereinigung anschlagen.

**Exit:** AK 38–41 grün; `find_references`, Compiler und Tests zeigen keinen
Restpfad; keine Suppression, Ausnahme oder auskommentierte Altimplementierung.

### Slice 10 – Endzustandsdokumentation, Dogfood, Auditor und Release

**Lesen:** `Docs/agent-api.md`, `Docs/integration.md`, relevante
`Docs/configuration.md`-Abschnitte, MCP-Workflowregel, eingebettete Guides,
Server-Instructions, READMEs mit MCP-Vertrag, vollständiger Diff und alle 44 AK.

**Bauen:** Ausschließlich finale Contract-v2-, Scope-, Status-, Budget-,
Namespace- und Assembly-Beispiele; veraltete Beispiele löschen; Dogfoodmatrix
ausführen; Auditor seriell für DRY, Drift, Dead Code und Magic Values einsetzen;
alle Findings beheben; Abschlussgates ausführen.

**Rot:** Dokumentations-Smokes und Negativsuche nach v1-/Historien-/Aliasformen;
Dogfood muss jeden bestätigten Befund zunächst reproduzierbar abdecken.

**Exit:** AK 42–44, alle Muss-Kriterien, Dogfood, Auditor und Release-Gate
vollständig grün; kein Finding, kein vertrauliches Artefakt und keine ersetzte
Form verbleiben.

## Teststrategie

### Red-Test-First je Befund

- FastTests prüfen reine Statuszustände, Scopeprojektion, FieldPaths,
  Namespaceauswahl, Budgetmonotonie, Textfilter und Cachekeysemantik.
- IntegrationTests prüfen echte `tools/list`-Schemas, MCP-Wirebytes,
  `isError`, Source-/Assembly-Handoffs, Registry-Eviction, Serverneustart,
  Referenzsessions und Cross-Target-Isolation.
- Tests vergleichen fachliche Invarianten und exakte Contractfelder, nicht
  private Methoden oder zufällige Formatierungsdetails.
- Budgettests messen UTF-8 von Text plus StructuredContent inklusive finaler
  Navigation. Sie prüfen Default, Minimum, genau gemeldeten Retrywert,
  Maximum und monotone Erweiterung.
- Alle Assembly-Fixtures sind repositoryeigen, synthetisch, klein und werden
  metadata-only verarbeitet. Externe Auditdaten werden nicht kopiert.
- Tests bleiben parallel ausführbar; die Taskausführung selbst bleibt seriell.
  Stress bleibt ausgeschlossen.

### Verbindliche Dogfood-Szenarien

1. Source-Symbolsuche → Feature-Kontext → Body/References: ID nur strukturiert,
   gleiche Snapshotbindung, kein Erfolgsfooter.
2. Feature- und Testkontext mit `all|production|tests` und
   `includeGenerated=false|true`: disjunkte Populationen, markierte Generated-
   Evidenz und korrekte Counts.
3. Klassenstruktur eines Partial-/Generated-beteiligten Typs: eine Identität,
   markierte Locations, Scope vor Memberlimit.
4. Exakter Leaf-Namespace bei Default- und Maximalbudget: Root und direkte
   Typen; echter Miss getrennt; kein falsches Empty/Truncated.
5. Budget unter Minimum: Error/NotApplicable, exakter FieldPath und ein Retry
   mit genau `minimumResponseBytes` ist erfolgreich.
6. `find_symbol` mit ungültigem Kind und leerem Batch-Element: exakter Feld-/
   Indexpfad; anschließender gültiger Call funktioniert.
7. Assemblytool mit ungültiger Detailstufe: knapper Fehler vor Analyse, keine
   Decompilerprojektion.
8. Assemblyanalyse mit begrenzter Decompilation: Analysequalität Partial,
   Response-Completeness ehrlich und Text/StructuredContent konsistent.
9. Assembly-Symbolsuche mit Referenzen am Defaultbudget: handlungsfähige
   Mindestprojektion; kleineres Budget liefert ausführbaren Mindestwert.
10. Assembly-Referenzen False → True → False: Root-only bleibt root-only,
    Closure bleibt bounded, requested/effective und Counts stimmen.
11. Handoff eines referenzierten Assemblytreffers zu Body und References nach
    Eviction/Neustart; keine Root-Umetikettierung.
12. Source → Assembly → Source im selben Prozess: identischer Source-Snapshot,
    gleiche Trefferfolge und keine Sessionkontamination.
13. Zielgebundener Health versus globaler Health: nur der globale Call enthält
    Daemonaggregate; Targetantwort nennt keine anderen Pfade.
14. File-Tree-Summary, C#-Filter und Index-Scope derselben Solution:
    Populationen und Ausschlusseinheiten sind selbsterklärend.
15. Call-Graph-Diamond/Zyklus und Rendererparität bleiben unverändert grün.

Falls vertrauliche externe Assemblies zusätzlich manuell dogfooded werden,
bleiben alle Identitäten und Rohantworten außerhalb des Repositories und der
persistierten Testausgaben. Nur anonymisierte Pass/Fail-Invarianten dürfen im
temporären Arbeitsgedächtnis erscheinen.

## Auditor-Gate

Nach allen Implementierungsslices und vor den vollständigen Tests wird die
`auditor`-Rolle genau einmal seriell eingesetzt. Sie prüft den vollständigen
Änderungsscope mit den passenden MCP-Tools auf:

- DRY und konkurrierende Contract-/Status-/Scope-/Budgetmodelle;
- Refactoring-Drift und tote Altpfade;
- Dead Code, pass-through Adapter und unreferenzierte DTO-Felder;
- Magic Values bei Version, Status, Enums, Budgetgrenzen und Counts;
- Namespace-/Dateigröße, Kopplung, Verantwortlichkeit und regelkonforme
  Architektur.

Jedes belastbare Finding wird vor dem Release-Gate fachlich behoben und erneut
geprüft. Waiver, Suppression oder bloße Dokumentation eines behebbaren Findings
erfüllen das Gate nicht.

## Dokumentations-Gate

- `tools/list`, Server-Instructions, Agent-Guide, Produktdokumentation,
  Workflowregel und Tests verwenden dieselben Felder, Enums, Defaults und
  Beispiele.
- Dokumentation erklärt knapp, wie Status versus Analysequalität,
  Scope/Generated, Budgetminimum, Namespace-Drilldown und Assembly-
  Referenzscope auszuwerten sind.
- Alle Beispiele sind synthetisch oder repositoryeigen und enthalten keine
  vertraulichen Daten.
- Eine repositoryweite Negativsuche nach Contract v1, entfernten DTO-Feldern,
  Aliasnamen, alten Enumwerten, generischen Trimmern und historischer Sprache
  ist leer oder jeder Treffer ist fachlich nachweisbar kein Produktvertrag.
- READMEs werden nur geändert, wenn sie heute den betroffenen MCP-Vertrag
  beschreiben; danach zeigen auch sie ausschließlich den finalen Zustand.

## Release-Gate

Nach Dogfood und Auditor laufen strikt seriell:

```powershell
dotnet build
dotnet test src/AiNetLinter.FastTests --filter Category!=Stress
dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress
git diff --check
```

Danach werden vollständiger Diff, Git-Status, alle 12 Muss-Kriterien und alle
44 Akzeptanzkriterien geprüft. Zusätzlich müssen scope-passende
`get_violations`-/`safeguard`-Prüfungen ohne offene auftragsbezogene
Violations sein. Stress-Tests laufen nur auf ausdrücklichen Nutzerwunsch.

Ein Release ist blockiert, solange ein Vertragspfad doppelt existiert, ein
Status widersprüchlich ist, ein Budgetretry nicht ausführbar ist, ein
Assemblyscope ignoriert wird, Dokumentation Historie statt Endzustand enthält
oder ein Auditor-/Dogfood-Finding offen ist.

## Risiken und Gegenmaßnahmen

- **Brechender Wirevertrag:** Contractversion `2` wird atomar in Producer,
  Consumer, Tests, Guides und Dokumentation umgestellt; keine Übergangsphase.
- **Zu breiter gemeinsamer Scope:** Gemeinsame Klassifikation und Reihenfolge,
  aber toolnahe Projektion; Seed-Ausnahme und Graphbrücken sind explizit.
- **Verlorene Evidenz durch Budgetierung:** Red-Tests für Mindestprojektion,
  exakten Retrywert und Monotonie; keine generische Kürzung.
- **Assemblykosten:** False bleibt Root-only; True besitzt harte Grenzen,
  transparente Counts und semantikvollständige Cachekeys.
- **Statusinflation:** Ein Statusowner, getrennte Analysequalität und genau ein
  Next; Formatter dürfen keinen zweiten Zustand erfinden.
- **Refactoringdrift:** Fachliche Subnamespaces, gezielte Architekturtests,
  Negativsuchen und Auditor vor Release.
- **Vertrauliche Evidenz:** ausschließlich synthetische/repositoryeigene
  Fixtures; keine externen Identitäten oder Rohantworten persistieren.

## Verworfene Alternativen

- Contract v1 um optionale Felder ergänzen: erhält Mehrdeutigkeit und
  Parallelformen.
- Status nur im Text korrigieren: StructuredContent bleibt widersprüchlich und
  nicht sicher automatisierbar.
- `partial` und `truncated` weiterhin in einem unklaren Feld mischen: Herkunft,
  Analysequalität und Wireabdeckung bleiben nicht entscheidbar.
- Scope nur dokumentieren oder Composite-Tools ausnehmen: Toolketten bleiben
  nicht reproduzierbar.
- Budgetdefault pauschal erhöhen: verdeckt falsche Mindestprojektion und
  ungenaue Retrywerte.
- Generischen Trimmer behalten und Sonderfälle ergänzen: zentrale
  Größenheuristik kann Fachwert nicht beurteilen und erzeugt weitere Ausnahmen.
- `includeReferences=false` nachträglich aus einer bereits erweiterten Session
  filtern: Kosten, Counts und Cachezustand bleiben falsch.
- Alte DTO-Felder als leere Platzhalter behalten: vergrößert Wireform und
  suggeriert weiterhin unterstützte Semantik.

## Offene Entscheidungen

Keine. Das Konzept bleibt ausschließlich wegen der noch ausstehenden
ausdrücklichen Nutzerfreigabe im Status `draft`.

## Abschlussbedingung

Der Task ist erst abgeschlossen, wenn alle zehn Slices seriell beendet, alle
12 Muss-Kriterien und 44 Akzeptanzkriterien belegt, sämtliche ersetzten Pfade
vollständig entfernt, Dogfood und Auditor ohne offene Findings sowie Build und
beide Nicht-Stress-Suiten grün sind. Ein einzelner korrigierter Auditfall, eine
kompatible Parallelform oder ein dokumentierter Restfehler genügt nicht.
