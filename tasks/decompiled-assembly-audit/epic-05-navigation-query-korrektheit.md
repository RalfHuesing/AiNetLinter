# Epic 5 — Navigation und fachliche Query-Korrektheit

## Evidence-/Scope

Dieses Dokument prüft ausschließlich die Assembly-Pfade von `find_symbol`,
`get_symbol_body`, `find_references`, `get_call_tree`, `get_type_hierarchy`,
`dependency_graph`, `get_namespace_tree`, `get_file_skeleton`,
`get_class_structure`, `metrics_lookup`, `metrics_tree` und
`find_assembly_extensions`. Bewertet wurden Root-vs-Referenz-Grenzen,
Caller-/Calltree-Semantik, klassische Extension-Anwendbarkeit,
Symbolidentitäten sowie Ergebnis- und Body-Trunkierung. Produktionscode,
Tests, Konfiguration und Produktdokumentation wurden nicht geändert; Builds,
Tests, Assembly-Ausführung und Commits wurden nicht ausgeführt.

Die lokale Matrix wurde nur zur Auflösung der fünf zulässigen Labels verwendet.
Alle Assembly-`targetPath`-Werte, Referenz-IDs, generierten Pfade, Hashes,
externen Namen, Namespaces und dekompilierten Inhalte sind in diesem Bericht
redigiert. In den MCP-Parameterblöcken bedeutet
`<absoluter redigierter Matrixpfad>` den tatsächlich absolut übergebenen Wert;
die Redigierung ist wegen des Auditvertrags absichtlich Teil der Evidence.

`LOCAL-01`, `LOCAL-02` und `LOCAL-03` waren die relevanten positiven
Assembly-Navigationseingänge. `GIT-01` wurde in diesem Epic nicht als direkter
Assembly-Target verwendet, weil sein Matrixeintrag ein Provider-/Mapping-
Eingang und kein Assembly-Pfad ist. `FALSE-01` wurde nicht als positive
Navigation fortgesetzt, weil der verwaltete Snapshot-/Navigation-Vertrag erst
nach erfolgreicher .NET-Initialisierung greift; die sichere Negativbehandlung
gehört in den Betriebs-/Fehlerverhaltens-Scope. Diese Abgrenzungen sind keine
Aussagen über die konkrete externe Identität der Fälle.

Bewertung: P0 = Sicherheits-/Datenverlust oder harter Vertragsbruch, P1 = hohe
Korrektheitsrelevanz, P2 = relevante Robustheits-/UX-/Effizienzlücke, P3 =
kleinere Verbesserung. Größenklassen: S lokal, M mehrere eng gekoppelte
Stellen, L mehrere Komponenten/Verträge, XL Architektur-/Migrationsumfang.

## Findings — Bug

### E5-BUG-01 — `find_symbol(includeReferences=true)` kann Root-Treffer aus dem sichtbaren Budget verdrängen

**Priorität:** P1 · **Größe:** M · **Vertrauen:** hoch · **Disposition:**
`promoted-to-project-debt`

**Betroffene Stelle.**
`src/AiNetLinter/Mcp/Tools/SymbolGraph/AssemblySymbolSearch.cs:18-67`,
insbesondere die Sammlung in `FindMatchesAsync`, die globale Sortierung in
Zeile 47 und die Begrenzung in Zeile 53; die Root-first-Leaseauswahl liegt in
`src/AiNetLinter/Mcp/Tools/SymbolGraph/AssemblyNavigationSupport.cs:21-31`.

**MCP-Abfragen und Ergebnis.**

```json
{
  "targetType": "assembly",
  "targetPath": "<absoluter redigierter Matrixpfad von LOCAL-01>",
  "namePatterns": ["<redigiertes Typmuster>"],
  "kind": "Class",
  "maxResults": 5,
  "includeReferences": false
}
```

Ergebnis für `LOCAL-01`: `isError=false`; fünf sichtbare Matches aus dem
Root-Snapshot; `analysis.origin=decompiled`,
`analysis.contentMode=decompiledSignatureOnly`,
`analysis.bodyAvailability=onDemand`, `analysis.status=partial`,
`analysis.completeness=partial`.

```json
{
  "targetType": "assembly",
  "targetPath": "<absoluter redigierter Matrixpfad von LOCAL-01>",
  "namePatterns": ["<redigiertes Typmuster>"],
  "kind": "Class",
  "maxResults": 5,
  "includeReferences": true
}
```

Ergebnis für `LOCAL-01`: `isError=false`; fünf sichtbare Matches, davon
`rootShown=0` und `refShown=5`; `navigation.totalAssemblyCount=47`,
`navigation.searchedAssemblyCount=32`,
`navigation.assembliesTruncated=true`, `navigation.completeness=partial`.
Eine Kontrollabfrage mit identischem Filter und `maxResults=1000` lieferte
380 Matches, davon neun Root- und 371 Referenz-Matches. Der Root ist also nicht
trefferlos; er wird durch die globale Sortierung vor der Kappung unsichtbar.

Die Vergleichsabfragen mit demselben Schema wurden auch für `LOCAL-02` und
`LOCAL-03` ausgeführt. Bei `LOCAL-02` zeigt die Kontrollpaarung zusätzlich die
Grenzverwechslung aus E5-BUG-02: mit `maxResults=5` meldet die Antwort
`totalAssemblyCount=24`, `searchedAssemblyCount=24`, aber
`assembliesTruncated=true`; mit `maxResults=1000` bleibt die Assembly-Zahl
gleich, während `assembliesTruncated=false` wird.

**Abweichung und Auswirkung.** Die Implementierung sammelt zwar Root zuerst,
sortiert anschließend aber alle Einträge nach `Origin.CanonicalPath` und
kappt erst danach. Damit kann der empfohlene Discovery-Call trotz
`includeReferences=true` den eigentlichen Ziel-Assembly-Scope nicht zeigen.
Zusätzlich wird in `AssemblySymbolSearch.cs:62-65` eine Trefferlisten-Kappung
in das Feld `assembliesTruncated` der Assembly-Navigation eingerechnet,
obwohl `AssemblyNavigationSupport.CreateSummary` dieses Feld als
Assembly-Suchgrenze interpretiert. Agents können dadurch Root-Treffer
übersehen und die falsche Ursache der Partialität annehmen.

**Empfehlung.** Root-Ergebnisse vor Referenz-Ergebnissen deterministisch
reservieren bzw. sortieren und anschließend die Referenzliste begrenzen.
Trefferlisten-Trunkierung und Assembly-Suchraum-Trunkierung müssen getrennte
Felder und getrennte `truncatedBy`-Gründe erhalten. Eine Regression sollte
mindestens den Fall `maxResults=5` mit vollständiger Referenzsuche und den
Fall `maxResults=1000` vergleichen.

**Abgrenzung / Unsicherheit.** `includeReferences=false` ist vertragsgemäß
Root-only. Bei `LOCAL-01` und `LOCAL-03` ist die Referenzmenge selbst auf 32
Sessions begrenzt; dieser echte Scope bleibt eine separate Partialitätsursache
und wird nicht als Fehler der Root-first-Reihenfolge gezählt.

### E5-BUG-03 — Referenzgebundene Stable-ID aus `find_symbol` ist für `get_symbol_body` nicht weiterverwendbar

**Priorität:** P1 · **Größe:** M · **Vertrauen:** hoch · **Disposition:**
`promoted-to-project-debt`

**Betroffene Stelle.**
`src/AiNetLinter/Mcp/Registration/SymbolBodyToolRegistrations.cs:37-44`
registriert für Assembly-Ziele nur den Body-Lease, während
`src/AiNetLinter/Mcp/Tools/SymbolGraph/SymbolIdentifierResolver.cs:131-177`
die Stable-ID gegen genau die aktuelle Lease-Identität validiert.
`find_symbol` erzeugt Referenz-IDs in
`src/AiNetLinter/Mcp/Tools/SymbolGraph/AssemblySymbolSearch.cs:83-91`.

**MCP-Abfragen und Ergebnis.**

```json
{
  "targetType": "assembly",
  "targetPath": "<absoluter redigierter Matrixpfad von LOCAL-01>",
  "namePatterns": ["<redigiertes Typmuster>"],
  "kind": "Class",
  "maxResults": 5,
  "includeReferences": true
}
```

Der erste ausgegebene Match war nachweislich referenzgebunden; die
Referenzzuordnung wurde nur intern über die analysierte ID-Identität geprüft
und nicht in dieses Dokument übernommen.

```json
{
  "targetType": "assembly",
  "targetPath": "<absoluter redigierter Matrixpfad von LOCAL-01>",
  "symbolIdentifiers": ["<redigierte reference-bound assembly-id>"],
  "maxBodyLines": 20
}
```

Ergebnis: `isError=false`; StructuredContent enthielt ausschließlich den
Recoverable-Error-Envelope (`code`, `message`, `hint`, `recoverable`,
`analysis`), aber keine Body-Payload. Der Textfehler war `INVALID_ARGUMENT`
mit dem Hinweis, dass die Assembly-Symbol-ID nicht zur aktuellen
Assembly-Generation gehört. Eine Root-gebundene ID aus demselben Snapshot
konnte dagegen in der Body-Route aufgelöst werden.

**Abweichung und Auswirkung.** Die Referenzsuche von
`find_symbol(includeReferences=true)` liefert eine ID, die das Folge-Tool
wegen fehlender Referenz-Expansion im Body-Dispatch nicht akzeptiert. Das
bricht die im Dokumentationsvertrag beschriebene progressive Folge
`find_symbol` → `get_symbol_body` genau für Referenztreffer; Agents müssen
den Treffer verwerfen oder auf unpräzise Positionsauflösung zurückfallen.

**Empfehlung.** Entweder `get_symbol_body` um einen expliziten
`includeReferences`-/Origin-Route erweitern und die passende Referenz-Lease
auflösen, oder Referenz-Treffer mit einem maschinenlesbaren Folge-Target
ausgeben und ihre Weitergabe an den Body-Call ausdrücklich sperren. Die
Variante darf keine Assembly laden oder ausführen und muss
Content-Hash/Generation weiter validieren.

**Abgrenzung / Unsicherheit.** Das ist kein Stale-ID-Fehler bei einer alten
Generation; beide Calls zielten auf denselben aktuell residenten Root-Pfad.
Die Probe beweist nicht, dass jeder Referenz-Member keinen Body besitzt,
sondern nur, dass die vorhandene Body-Route den gelieferten
Referenz-Scope nicht adressieren kann.

### E5-BUG-04 — Konstruktor-ID aus `get_file_skeleton` scheitert beim Body-Roundtrip

**Priorität:** P1 · **Größe:** S · **Vertrauen:** hoch · **Disposition:**
`promoted-to-project-debt`

**Betroffene Stelle.**
`src/AiNetLinter/Maps/Skeleton/SkeletonSyntaxWalker.cs:214-220` erzeugt die
Konstruktor-ID aus `TryGetDocCommentId`; für Assembly-Ziele wird sie in
`src/AiNetLinter/Mcp/Tools/FileStructure/GetFileSkeletonTool.cs:113-117`
mit der Assembly-Identität versehen. Die Auflösung erfolgt anschließend über
`src/AiNetLinter/Mcp/Tools/SymbolGraph/SymbolIdentifierResolver.cs:131-177`.

**MCP-Abfragen und Ergebnis.**

```json
{
  "targetType": "assembly",
  "targetPath": "<absoluter redigierter Matrixpfad von LOCAL-01>",
  "namePatterns": ["<redigiertes Root-Typmuster>"],
  "kind": "Class",
  "maxResults": 1,
  "includeReferences": false
}
```

Die Root-Typ-Fundstelle wurde als Input für den nächsten Call verwendet.

```json
{
  "targetType": "assembly",
  "targetPath": "<absoluter redigierter Matrixpfad von LOCAL-01>",
  "filePaths": ["<von find_symbol gelieferter redigierter Snapshot-Dateipfad>"]
}
```

Ergebnis: `isError=false`, Text-Output ohne StructuredContent; das Skelett
enthielt genau einen Typ und einen Konstruktor mit einer ausgegebenen
Assembly-ID.

```json
{
  "targetType": "assembly",
  "targetPath": "<absoluter redigierter Matrixpfad von LOCAL-01>",
  "symbolIdentifiers": ["<redigierte skeleton-constructor-id>"],
  "maxBodyLines": 20
}
```

Ergebnis: `isError=false`, Textfehler `SYMBOL_NOT_FOUND`, kein Body. Ein
anschließendes `get_class_structure` mit der root-gebundenen Typ-ID fand die
Struktur und den Konstruktor; Positions-Inputs auf dessen Zeile wurden zwar
aufgelöst, lieferten aber einen separaten `unavailable`-Body-Hinweis. Damit
ist die ID-Auflösung vom späteren Decompiler-Ergebnis unterscheidbar.

**Abweichung und Auswirkung.** Der dokumentierte Skeleton-zu-Body-Workflow
ist für Konstruktoren nicht geschlossen. Ein Agent kann die ID nicht einfach
als stabilen Identifier weiterreichen und erhält statt einer fachlichen
Body-Verfügbarkeit eine irreführende Symbol-nicht-gefunden-Antwort.

**Empfehlung.** Die vom Skeleton erzeugte Konstruktor-DocumentationCommentId
gegen genau dieselbe Roslyn-Deklarationsauflösung roundtrip-fähig machen;
alternativ muss der Resolver eine explizite Konstruktor-Normalisierung mit
gleichwertiger Overload-/Parameterprüfung anbieten. Danach müssen auch
statische Konstruktoren und generische/verschachtelte Typen geprüft werden.

**Abgrenzung / Unsicherheit.** Die Probe bewertet nur den Root-Snapshot von
`LOCAL-01`. Sie zeigt keinen Fehler bei jeder Property-, Event- oder
Methoden-ID und enthält absichtlich weder die konkrete ID noch dekompilierten
Inhalt.

### E5-BUG-02 — `find_symbol` meldet Trefferlisten-Kappung als Assembly-Kappung

**Priorität:** P2 · **Größe:** S · **Vertrauen:** hoch · **Disposition:**
`promoted-to-project-debt`

**Betroffene Stelle.**
`src/AiNetLinter/Mcp/Tools/SymbolGraph/AssemblySymbolSearch.cs:47-65` und
`src/AiNetLinter/Mcp/Tools/SymbolGraph/AssemblyNavigationSupport.cs:41-55`.

**MCP-Abfragen und Ergebnis.**

```json
{
  "targetType": "assembly",
  "targetPath": "<absoluter redigierter Matrixpfad von LOCAL-02>",
  "namePatterns": ["<redigiertes Typmuster>"],
  "kind": "Class",
  "maxResults": 5,
  "includeReferences": true
}
```

Ergebnis: `isError=false`, `navigation.totalAssemblyCount=24`,
`navigation.searchedAssemblyCount=24`, aber
`navigation.assembliesTruncated=true`. Die gleiche Abfrage mit
`maxResults=1000` liefert `totalAssemblyCount=24`,
`searchedAssemblyCount=24`, `assembliesTruncated=false`.

**Abweichung und Auswirkung.** Die Boolean-Bedeutung wird im Aufrufer mit
`leaseSet.AssembliesTruncated || distinct.Count > shown.Count` erweitert.
Damit ist `assembliesTruncated` nicht mehr die dokumentierte Aussage, ob der
bounded Assembly-Suchraum gekürzt wurde. Der Fehler erschwert insbesondere
die Unterscheidung zwischen mehr Treffern im bereits vollständig durchsuchten
Scope und nicht durchsuchten Referenz-Assemblies.

**Empfehlung.** Einen eigenen Trefferlisten-Indikator (z. B.
`resultsTruncated`/`truncatedBy`) einführen und `assembliesTruncated` nur aus
`GetLeases` übernehmen. Text und StructuredContent müssen dieselbe getrennte
Aggregation verwenden.

**Abgrenzung / Unsicherheit.** Der Befund betrifft die Navigation-Metadaten,
nicht die eigentliche `maxResults`-Kappung der Trefferliste. Für
`LOCAL-01`/`LOCAL-03` ist `assembliesTruncated=true` zusätzlich wegen der
realen 32-Session-Grenze plausibel; `LOCAL-02` isoliert den Fehler.

### E5-BUG-05 — `find_assembly_extensions` markiert Response-Budget-Trimming als Extension-Listen-Trunkierung

**Priorität:** P2 · **Größe:** S · **Vertrauen:** hoch · **Disposition:**
`promoted-to-project-debt`

**Betroffene Stelle.**
`src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/Responses/FindAssemblyExtensionsResponseBuilder.cs:24-63`
erstellt die Extension-Payload; die Budgetreduktion in
`src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisResponseLimits.Budget.cs:38-58,92-128,205-268`
trimmt zunächst Referenz-Sessions, Referenzen und Diagnosen. Der gemeinsame
`MarkResponseBudget` setzt dabei `Truncated=true`, auch wenn kein Extension-
Eintrag entfernt wurde.

**MCP-Abfrage und Ergebnis.** Das Schema wurde für alle drei positiven lokalen
Fälle ausgeführt:

```json
{
  "targetType": "assembly",
  "targetPath": "<absoluter redigierter Matrixpfad von LOCAL-01|LOCAL-02|LOCAL-03>",
  "receiverType": "<redigierter Receiver-Typ>",
  "extensionName": null,
  "namespace": null,
  "maxResults": 1000
}
```

Alle drei Antworten waren `isError=false`, `origin=decompiled`,
`status=partial`, `completeness=partial`; jeweils
`extensions=[]`, `totalExtensions=0`, `shownCount=0`, aber
`truncated=true` und `truncatedBy=["responseBudget"]`. Referenzen,
Referenz-Sessions und Diagnosesamples waren in denselben Antworten sichtbar
gekürzt. Die leere Extension-Liste ist kein Nachweis, dass es außerhalb des
geprüften Root-Snapshots keine Extensions gibt.

**Abweichung und Auswirkung.** Die Textzeile „0 von 0 (gekürzt:
responseBudget)“ lässt einen Agenten annehmen, dass Extension-Treffer aus der
Liste entfernt wurden. Tatsächlich wurde nur begleitende Response-Metadaten-
last reduziert. Das verfälscht die Trunkierungsgrenze und kann unnötige
Wiederholungsabfragen auslösen.

**Empfehlung.** Extension-Auswahl-Trunkierung (`maxResults`) von
Response-Budget-Trunkierung trennen, z. B. mit `extensionsTruncated` und
`responseTruncated` samt jeweils eigener Ursache. `shownCount`/`totalExtensions`
dürfen nur auf der Extension-Liste basieren; die Begleitlisten sollen ihre
eigenen Counts behalten.

**Abgrenzung / Unsicherheit.** Die Assembly-Route durchsucht absichtlich den
Root-Kontext; `ExpandAssemblyReferences=true` stellt begleitende Referenz-
Informationen bereit, macht aber aus der Extension-Suche keine Suche in
Referenz-Assemblies. Die klassische Extension-Erkennung selbst ist in
`AssemblyAnalysisService.FindExtensions` statisch nachvollziehbar.

## Findings — Optimierung

Kein belastbarer, scope-naher Optimierungsfund wurde zur Umsetzung vorgeschlagen.
Der vorgeschriebene read-only Qualitätscheck ergab im Produktionsscope
`src/AiNetLinter/Mcp/Tools` keine exakten Clone-Cluster und keinen heuristischen
Dead-Code-Fund. Der Magic-Value-Scan meldete bestehende, fachlich erkennbare
Filter-/Bufferwerte; daraus folgt ohne Codeänderung und ohne bestätigte
gemeinsame Semantik keine sichere Epic-5-Optimierung. Die mögliche Kostenfrage
der bounded Referenzexpansion bleibt wegen der Epic-6-Abgrenzung dort.

## Findings — Missing Feature

### E5-MF-01 — Strukturelle und metrische Assembly-Abfragen haben keine opt-in Referenzsicht

**Priorität:** P2 · **Größe:** L · **Vertrauen:** mittel · **Disposition:**
`accepted-deferred`

**Betroffene Stellen.**
`src/AiNetLinter/Mcp/Registration/SymbolGraphToolRegistrations.cs:175-214`
leitet `get_type_hierarchy` und `dependency_graph` im Assembly-Fall direkt an
die Root-Server-Solution weiter. Gleiches gilt für
`src/AiNetLinter/Mcp/Registration/FileStructureToolRegistrations.cs:95-179`
bei `get_namespace_tree`, `get_class_structure` und `get_file_skeleton` sowie
für `src/AiNetLinter/Mcp/Registration/AnalysisToolRegistrations.cs:140-180`
bei `metrics_tree` und `metrics_lookup`. Keine dieser Schemas besitzt
`includeReferences`.

**Nachweis.** Die aktuellen Tool-Schemas wurden mit `targetType="assembly"`
und absolutem redigiertem Matrixpfad geprüft. Nur `find_symbol`,
`find_references` und `get_call_tree` besitzen im Assembly-Routing einen
expliziten `includeReferences`-Parameter; `find_assembly_extensions` expandiert
Referenz-Sessions für Begleitmetadaten, aber
`AssemblyAnalysisService.FindExtensions` in
`src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisService.cs:108-131`
scannt nur `context.Assembly.GlobalNamespace`.

Die tatsächlich ausgeführten Root-Calls für `LOCAL-01` lieferten bei
`get_type_hierarchy`, `dependency_graph`, `get_namespace_tree`,
`get_file_skeleton`, `get_class_structure`, `metrics_lookup` und `metrics_tree`
jeweils `isError=false` und Assembly-Analyse-Metadaten mit
`origin=decompiled`, `contentMode=decompiledSignatureOnly`,
`bodyAvailability=onDemand`, `status=partial`, `completeness=partial`; die
Navigation blieb der eine materialisierte Root-Snapshot. Bei
`dependency_graph` war die Ergebnisliste mit `maxResults=5` sichtbar
gekürzt (`truncated=true`); die übrigen Root-Strukturen zeigten ihre jeweils
eigene Detailgrenze.

**Nutzungslücke.** Ein Agent kann Referenz-Symbole finden und Caller über
`includeReferences=true` prüfen, aber Namespace-, Typ-, Klassen-, Datei- und
Metrikansichten desselben Assembly-Suchraums nicht bounded erweitern. Das ist
kein Verstoß gegen den aktuellen Root-only-Default, aber eine fachliche
Paritätslücke für Assembly-übergreifende Navigation.

**Empfehlung.** Als bewusst opt-in gestaltete Erweiterung ein gemeinsames
`includeReferences` für die betroffenen Struktur-/Metriktools einführen oder
die Root-only-Grenze im StructuredContent maschinenlesbar ausweisen. Bei einer
Erweiterung müssen Origin, Assembly-Count, per-Tool Counts und
Trunkierungsgründe getrennt bleiben; `metrics_tree` braucht zusätzlich eine
definierte Aggregation über mehrere Snapshots.

**Abgrenzung / Unsicherheit.** Die aktuelle Dokumentation beschreibt für diese
Tools Assembly-Support und jeweilige Snapshot-/Node-Grenzen, verspricht aber
keine Referenzexpansion für jede Route. Deshalb ist dies ein Missing Feature,
nicht E5-BUG-01.

### E5-MF-02 — Metrics- und Calltree-Ergebnisse weisen die Signatur-only-Basis nicht fachlich genug aus

**Priorität:** P2 · **Größe:** M · **Vertrauen:** mittel · **Disposition:**
`accepted-deferred`

**Betroffene Stellen.** Assembly-Dispatch in
`src/AiNetLinter/Mcp/Registration/AnalysisToolRegistrations.cs:140-180`,
`src/AiNetLinter/Mcp/Tools/MetricsLookup/MetricsLookupTool.cs:25-86` und
`src/AiNetLinter/Mcp/Tools/SymbolGraph/AssemblyGetCallTreeTool.cs:18-90`.
Die Basissolution ist laut
`src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisResponse.cs:32-79`
`decompiledSignatureOnly`; Bodies werden separat on demand erzeugt.

**MCP-Abfragen und Ergebnis.**

```json
{
  "targetType": "assembly",
  "targetPath": "<absoluter redigierter Matrixpfad von LOCAL-01>",
  "symbolIdentifiers": ["<redigierte root-bound assembly-id>"]
}
```

`metrics_lookup` war `isError=false`, lieferte ein Ergebnis mit
`codeLines=1`, `cyclomaticComplexity=1`, `cognitiveComplexity=0` und
`totalParameters=effectiveParameters=1`; `analysis.contentMode` blieb
`decompiledSignatureOnly` und `analysis.bodyAvailability=onDemand`. Ein
separater `get_symbol_body`-Call verwendet dagegen `maxBodyLines=80` und
liefert entweder `decompiledBodyOnDemand` oder einen expliziten
`unavailable`-Hinweis.

Für `get_call_tree` wurde mit
`depth=2`, `format="ascii"`, `topN=5`, `direction="incoming"` und sowohl
`includeReferences=false` als auch `true` geprüft. Die geprüften dekompilierten
Stub-Symbole ergaben jeweils `children=0`, `truncated=false` und bei der
erweiterten Route eine partielle Navigation mit bounded Diagnostics.

**Nutzungslücke.** Das globale `analysis`-Objekt enthält zwar
`contentMode`/`bodyAvailability`, aber `methodMetrics`, Tree-Knoten und
Calltree-Leerresultate tragen keine eigene Aussage, ob die fachliche Aussage
auf einem Body, einem Signatur-Stub oder einer nicht verfügbaren Body-Abfrage
beruht. Netto-LOC und Komplexität können dadurch wie Vollmetriken wirken,
obwohl sie nur den Snapshot messen.

**Empfehlung.** In den strukturierten Nutzdaten eine explizite
`measurementBasis`/`bodyAvailability`-Projektion und bei Stub-basierten
Metriken einen klaren Status ergänzen. Beim leeren Calltree sollte zusätzlich
zwischen „keine Call-Sites im Snapshot“ und „Call-Sites wegen
Signature-only-/Partial-Grenze nicht entscheidbar“ unterschieden werden.

**Abgrenzung / Unsicherheit.** Die bestehende Dokumentation erlaubt
dekompilierte API-Stubs und sagt ausdrücklich, dass fehlende Call-Sites keine
globale Negativaussage sind. Der Befund ist daher eine Transparenz-/Feature-
Lücke, kein Nachweis falscher Roslyn-Referenzen.

### E5-MF-03 — Consumer-basierte klassische Extension-Anwendbarkeit fehlt im Standalone-Assembly-Call

**Priorität:** P2 · **Größe:** L · **Vertrauen:** hoch · **Disposition:**
`accepted-deferred`

**Betroffene Stellen.**
`src/AiNetLinter/Mcp/Registration/AssemblyAnalysisToolRegistrations.cs:105-125`
setzt im Assembly-Dispatch `ConsumerSolution` nicht; die Beschreibung nennt
den Schritt ausdrücklich consumer-los. Die eigentliche Suche und Reduktion
liegen in
`src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisService.cs:108-177`.
Ohne `context.Receiver` wird jede gefundene Extension als `not_decidable`
ausgegeben; mit Receiver würde `ReduceExtensionMethod` zwischen
`applicable`, `not_applicable` und `not_decidable` unterscheiden.

**Read-only-Nachweis und MCP-Abfrage.** Die vorhandenen Tests
`src/AiNetLinter.FastTests/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisToolTests.cs:99-185`
bestätigen die klassische `IsExtensionMethod`-Erkennung, Filterung des ersten
`this`-Parameters und das erwartete `not_decidable` ohne Consumer. Die lokalen
Gegenproben wurden mit folgendem vollständigem Schema ausgeführt:

```json
{
  "targetType": "assembly",
  "targetPath": "<absoluter redigierter Matrixpfad von LOCAL-01|LOCAL-02|LOCAL-03>",
  "receiverType": "<redigierter Receiver-Typ>",
  "extensionName": null,
  "namespace": null,
  "maxResults": 1000
}
```

Alle drei MCP-Antworten waren `isError=false`, decompiled/partial und
enthielten in diesem konkreten Root-Scope keine sichtbaren Extension-Einträge;
deshalb wurde keine externe Extension-Identität und kein Applicability-Wert
aus einem realen Produktfall reproduziert. Die statische Quell- und
Fixture-Evidence zeigt jedoch die bewusste Standalone-Grenze.

**Nutzungslücke.** `receiverType` ist im Assembly-Call ein syntaktischer
Filter, aber kein Consumer-Kontext. Ein Agent kann so klassische Extensions
finden, aber nicht belastbar entscheiden, ob sie für einen realen Consumer
kompilieren würden.

**Empfehlung.** Einen separaten, expliziten Consumer-/Projekt-Target-Parameter
oder eine source-backed Consumer-Route anbieten; dabei die bestehende
metadata-only-Grenze und `not_decidable` bei fehlenden Typinformationen
beibehalten. Keine implizite Runtime- oder Assembly-Ausführung.

**Abgrenzung / Unsicherheit.** Das aktuelle `not_decidable` ist gemäß
Beschreibung und Tests korrekt und wird nicht als Bug klassifiziert. Die
Gegenprobe mit leerer Ergebnisliste beweist weder fehlende Extensions im
gesamten Referenzraum noch eine fehlerhafte klassische Extension-Erkennung.

## Tatsächlich ausgeführte MCP-Abfragen

Alle zielgebundenen Abfragen enthielten das aktuelle Schema mit absolutem
`targetType` und `targetPath`. Werte, die eine externe Identität tragen
könnten, sind unten als Platzhalter wiedergegeben.

| Tool / Fall | Vollständige fachlich relevante Parameter | Redigiertes Ergebnis |
|---|---|---|
| `find_symbol` / `LOCAL-01` | `targetType=assembly`, `targetPath=<absolut redigiert>`, `namePatterns=[<redigiertes Typmuster>]`, `kind=Class`, `maxResults=5`, `includeReferences=false` | fünf Root-Matches; `isError=false`; decompiled, signature-only, partial |
| `find_symbol` / `LOCAL-01` | identisch, `includeReferences=true` | fünf Matches, Root 0/Referenz 5; `totalAssemblyCount=47`, `searchedAssemblyCount=32`, Assembly-Scope gekürzt, partial |
| `find_symbol` / `LOCAL-01` | identisch, `maxResults=1000`, `includeReferences=true` | 380 Matches, Root 9/Referenz 371; dieselbe 32-Assembly-Scope-Grenze |
| `find_symbol` / `LOCAL-02` | wie oben, `maxResults=5` und Kontrollcall `maxResults=1000`, `includeReferences=true` | 24/24 gesuchte Assemblies; Flag mit kleinem Ergebnisbudget falsch, mit großem Budget korrekt |
| `find_symbol` / `LOCAL-02`, `LOCAL-03` | Root-/Expanded-Paar wie oben | `isError=false`, origin decompiled, status/completeness partial; keine konkrete Identität persistiert |
| `get_symbol_body` / `LOCAL-01` | `targetType=assembly`, `targetPath=<absolut redigiert>`, `symbolIdentifiers=[<root-bound assembly-id>]`, `maxBodyLines=80` | Text-only; Body on demand möglich bzw. explizit unavailable; keine strukturierte Body-Payload |
| `get_symbol_body` / `LOCAL-01` | `symbolIdentifiers=[<reference-bound assembly-id>]`, `maxBodyLines=20` | recoverable `INVALID_ARGUMENT` als Text, `isError=false`; StructuredContent nur als Error-Envelope, keine Body-Payload |
| `get_file_skeleton` / `LOCAL-01` | `targetType=assembly`, `targetPath=<absolut redigiert>`, `filePaths=[<redigierter Snapshot-Dateipfad>]` | Text-only; ein Typ/ein Konstruktor; ID im Skelett vorhanden |
| `get_class_structure` / `LOCAL-01` | `symbolIdentifier=<root-bound assembly-id>`, `sortBy=lines`, `maxMembers=20`, `kindFilter=all`, `nameFilter=null` | eine Datei, ein sichtbarer Konstruktor; `isError=false`, partial analysis |
| `find_references` / `LOCAL-01` | `symbolIdentifier=<root-bound assembly-id>`, `depth=3`, `maxResults=5`, `includeReferences=false` | `callSites=0`; requested/effective depth 3; visited 1; keine Ergebnis-/Knoten-/Depth-Kappung |
| `find_references` / `LOCAL-01` | identisch, `includeReferences=true` | `callSites=0`; Navigation 47/32, Assembly-Scope partial; Diagnostics 100 gesamt/5 Samples, `truncatedBy=[maxDiagnostics]` |
| `find_references` / `LOCAL-02`, `LOCAL-03` | `symbolIdentifier=<root-bound assembly-id>`, `depth=2`, `maxResults=5`, beide `includeReferences`-Werte | Root und expanded jeweils `callSites=0`; expanded jeweils bounded Diagnostics/partial; keine globale Negativaussage |
| `get_call_tree` / `LOCAL-01` | `symbolIdentifier=<root-bound assembly-id>`, `depth=2`, `format=ascii`, `topN=5`, `direction=incoming`, beide `includeReferences`-Werte | Root/expanded `children=0`, `truncated=false`; expanded Navigation partial und Diagnostics bounded |
| `get_type_hierarchy` / `LOCAL-01` | `symbolIdentifier=<root-bound type-id>`, `maxResults=5` | `isError=false`; Struktur-Payload plus partial assembly analysis |
| `dependency_graph` / `LOCAL-01` | `symbolIdentifier=<root-bound type-id>`, `direction=both`, `depth=1`, `maxResults=5`, `filePath=null` | `isError=false`; sichtbare Kanten 5; `truncated=true`; partial assembly analysis |
| `get_namespace_tree` / `LOCAL-01` | `depth=1`, `includeTypes=true`, `kind=all`, `maxResults=5`, `namespacePrefix=null`, `project=null` | `isError=false`; Root-Snapshot-Struktur; `truncated`/Counts im Payload; partial analysis |
| `get_class_structure` / `LOCAL-01` | wie oben | `isError=false`; `totalMemberCount=1`, `shownMemberCount=1`, keine Member-Kappung |
| `metrics_lookup` / `LOCAL-01` | `symbolIdentifiers=[<root-bound assembly-id>]` | ein Ergebnis; Codezeilen 1, CC 1, kognitive Komplexität 0, ein Parameter; signature-only basis |
| `metrics_tree` / `LOCAL-01` | `mode=code_size`, `root=null`, `depth=1`, `topN=5`, `fileFilter=null` | `isError=false`; Root-Snapshot-Baum, analysis partial |
| `find_assembly_extensions` / `LOCAL-01`, `LOCAL-02`, `LOCAL-03` | `receiverType=<redigiert>`, `extensionName=null`, `namespace=null`, `maxResults=1000` | jeweils 0/0 Extensions, trotzdem `truncated=true`, `truncatedBy=[responseBudget]`; Begleitlisten gekürzt |
| `inspect_assembly` / `LOCAL-01` | `publicOnly=true`, `includeReferences=false`, `maxResults=5`, `maxMembers=8`, absoluter redigierter `targetPath` | metadata-only Baseline: decompiled, medium confidence, untrusted, signature-only, on-demand body, partial |

Zusätzliche projektgebundene MCP-Abfragen dienten der semantischen
Quellnavigation (`get_feature_context` für `AssemblySymbolSearch.FindMatchesAsync`,
`AssemblyReferenceNavigator.FindReferencesAsync`, `AssemblyAnalysisService.FindExtensions`,
`GetTypeHierarchyTool.ExecuteAsync` und `MetricsLookupTool.RenderMetricsLookupsAsync`)
mit `targetType=project`, absolutem Projektpfad, Callers/Metrics/Tests/Violations
aktiviert und begrenzten Counts. Sie bestätigten die oben zitierten aktuellen
Dateien und Symbole.

Der Abschlusscheck der `audit`-Skill wurde read-only ausgeführt:

- `find_duplicates`: `targetType=project`, absoluter Projektpfad,
  `mode=clone`, `minTokens=30`, `similarityThreshold=exact`,
  `scopeDir=src/AiNetLinter/Mcp/Tools`, `scopeType=production`,
  `maxResults=20` — keine Duplikat-Cluster, 716 Methoden gescannt.
- `find_dead_code`: `targetType=project`, absoluter Projektpfad,
  `accessibility=private_internal`, `confidence=both`, `kind=all`,
  `mode=members`, `scopeFilter=src/AiNetLinter/Mcp/Tools`,
  `includeTests=false`, `maxResults=50` — kein Fund im Scope.
- `find_magic_values`: `targetType=project`, absoluter Projektpfad,
  `valueType=all`, `categoryFilter=all`, `minOccurrences=2`,
  `includeTests=false`, `includeSuppressed=false`, `changedOnly=false`,
  `scopeFilter=src/AiNetLinter/Mcp/Tools`, `maxResults=50` — zehn heuristische
  Treffer, davon keine sichere, scope-nahe Korrektur ohne Produktionsänderung.

## Nur read-only gelesene Nachweise

- Vollständig gelesen: `AGENTS.md`, relevante `.agents/rules/*.mdc`,
  `tasks/decompiled-assembly-audit/Konzept.md`, `roadmap.md`,
  `code-map.md` und `.agents/skills/implement/SKILL.md`; zusätzlich für den
  Abschlusscheck `.agents/skills/audit/SKILL.md`.
- Quelltext gelesen: die Assembly-Dispatch-Registrierungen,
  `AnalysisToolCall`, `AssemblyNavigationSupport`,
  `AssemblySymbolSearch`, `AssemblySymbolResolver`,
  `AssemblyReferenceNavigator`, `TransitiveCallGraphFormatter`,
  `SymbolIdentifierResolver`, `GetSymbolBodyTool`,
  `AssemblyDecompiledBodyResolver`, die Struktur-/Metriktools sowie
  `AssemblyAnalysisService` und das Extension-Response-Budget.
- Dokumentation gelesen: `Docs/agent-api.md` und `Docs/integration.md`,
  insbesondere Target-/Origin-/Completeness-/Progressive-Disclosure- und
  Stable-ID-Abschnitte.
- Tests nur gelesen: `AssemblyAnalysisToolTests.cs`,
  `AssemblyAnalysisPathContractTests.cs`, relevante
  `get_class_structure`-, Calltree- und Navigation-Vertragstests. Kein Test
  wurde gestartet. Die gesichteten Tests decken Extension-Filter und
  `not_decidable` sowie einige Stable-ID-/Generationspfade ab, aber nicht den
  direkten Skeleton-zu-Body-Konstruktor-Roundtrip.
- Die lokale Matrixdatei wurde nur für Falllabels und Pfadauflösung gelesen;
  sie ist gitignoriert und wurde nicht in dieses Dokument übernommen.

## Offene Unsicherheiten und spätere Verifikation

- Es ist noch zu entscheiden, ob Referenz-IDs grundsätzlich Bodies und
  Strukturabfragen adressieren sollen oder ob der öffentliche Vertrag eine
  strikt root-only Body-/Strukturroute bevorzugt. E5-BUG-03 bleibt unabhängig
  davon wegen der aktuell ausgegebenen, nicht konsumierbaren ID bestehen.
- Die Konstruktor-ID-Probe sollte später mit einer kleinen managed Fixture für
  instance/static, generische und verschachtelte Konstruktoren ergänzt werden;
  diese Umsetzung ist nicht Teil dieses Read-only-Audits.
- Für die Referenz-Opt-in-Variante müssen per-Tool Aggregation, Deduplizierung,
  Origin und `maxResults` getrennt spezifiziert werden; insbesondere darf ein
  Referenz-Scope nicht stillschweigend eine Root-Aussage ersetzen.
- Die leeren Caller-/Calltree-Ergebnisse der lokalen Fälle sind wegen
  signature-only/partial Snapshots nicht als globale Negativaussage belastbar.
  Ein späterer Verifikationstask braucht eine kontrollierte Fixture mit
  tatsächlich dekompilierbaren Bodies und bekannten Aufruferketten.
- Für `find_assembly_extensions` wurde in den drei geprüften Root-Snapshots
  kein Treffer gezeigt; die klassische Extension-Evidence stammt daher aus
  Quelltext und gelesenen Fixtures, nicht aus einem externen dekompilierten
  Treffer.

## Nach der letzten Code-Map-Änderung: gezielte Navigations-Spotchecks

Nach der letzten Änderung an `code-map.md` wurde die wichtigste Kette
`find_symbol(includeReferences=true)` → Stable-ID → `get_symbol_body`
wiederholt und um Referenz-/Calltree-Spotchecks ergänzt. Die Werte blieben
redigiert:

```json
{
  "find_symbol": {
    "targetType": "assembly",
    "targetPath": "<absoluter redigierter Matrixpfad von LOCAL-01>",
    "namePatterns": ["<redigiertes Typmuster>"],
    "kind": "Class",
    "maxResults": 5,
    "includeReferences": true
  },
  "get_symbol_body": {
    "targetType": "assembly",
    "targetPath": "<absoluter redigierter Matrixpfad von LOCAL-01>",
    "symbolIdentifiers": ["<erste redigierte reference-bound assembly-id>"],
    "maxBodyLines": 20
  },
  "find_references": {
    "targetType": "assembly",
    "targetPath": "<absoluter redigierter Matrixpfad von LOCAL-01>",
    "symbolIdentifier": "<redigierte root-bound assembly-id>",
    "depth": 3,
    "maxResults": 5,
    "includeReferences": true
  },
  "get_call_tree": {
    "targetType": "assembly",
    "targetPath": "<absoluter redigierter Matrixpfad von LOCAL-01>",
    "symbolIdentifier": "<redigierte root-bound assembly-id>",
    "depth": 2,
    "format": "ascii",
    "topN": 5,
    "direction": "incoming",
    "includeReferences": true
  }
}
```

Ergebnis des finalen Spotchecks: `find_symbol` blieb bei fünf sichtbaren
Matches mit Root 0/Referenz 5 und `navigation.totalAssemblyCount=47`,
`searchedAssemblyCount=32`, `assembliesTruncated=true`,
`completeness=partial`. Der Root-only-Kontrollcall lieferte einen Root-Match.
Die Weitergabe der ersten Referenz-ID an `get_symbol_body` reproduzierte
`isError=false` mit Error-Envelope und `INVALID_ARGUMENT`, ohne Body. Der
Root-Body-Call blieb `isError=false`, ohne Body-StructuredContent, mit
sichtbarem `bodyAvailability`-Marker und ohne Trunkierungsmarker bei
`maxBodyLines=5`. `find_references` lieferte erneut `callSites=0`,
`requestedDepth=3`, `effectiveDepth=3`, `visitedNodeCount=1`, keine
MaxResults-/Node-/Depth-Kappung, aber 100 Diagnostics gesamt und fünf Samples
(`truncatedBy=["maxDiagnostics"]`). `get_call_tree` lieferte erneut
`children=0`, `truncated=false` und dieselbe partielle 47/32-Navigation mit
bounded Diagnostics. Es wurden nach diesem Spotcheck keine Änderungen an
`code-map.md` mehr vorgenommen.
