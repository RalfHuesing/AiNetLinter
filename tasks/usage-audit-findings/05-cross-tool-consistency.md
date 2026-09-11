# Gruppe E – Cross-Tool-Konsistenz (Agenten-UX-Audit)

## Methode

- Read-only-Aufrufe gegen `AiNetLinter.slnx` im Source-Modus; anschließend derselbe repräsentative Ablauf gegen `LOCAL-01` im Assembly-Modus.
- Repräsentativ geprüft: `get_server_health`, `find_symbol`, `get_feature_context`, `find_references`, `get_call_tree`, `get_test_context`, `dependency_graph`, `get_file_skeleton`, `get_class_structure`, `get_namespace_tree`, `get_index_scope`, `resolve_type_origin`, `inspect_assembly` und `find_assembly_extensions`.
- Chaining: `find_symbol` → Body/References/Call-Tree/Test-/Impact-Kontext; identische Source-Abfrage vor und nach Assembly-Abfrage; Namespace-Drilldown; Byte-/Statusgrenzfall.
- Externe Assembly-Evidenz ist ausschließlich als `LOCAL-01` bezeichnet. Produktnamen, externe Pfade, Namespaces, Symbole, Hashes und IDs sind aus diesem Bericht entfernt.
- Keine Builds, Tests, Codeänderungen oder Commits.

## Positives

- Source- und Assembly-Routen werden nach einem Moduswechsel im selben MCP-Server sauber getrennt. Die Source-Abfrage vor und nach `LOCAL-01` lieferte denselben Snapshot, dieselbe Herkunft und dieselben Treffer.
- Die getesteten erfolgreichen Source-Responses besitzen einen `navigation`-Block mit `contractVersion=1`, Target, Snapshot, Status und `next`; Handoff-Informationen sind bei References/Impact/Call-Tree vorhanden.
- `find_symbol` liefert bei gleichem Match die editierbare Produktionsquelle vor Tests; `scopeType` und `includeGenerated` werden in den geprüften Symbol-/Graph-Responses strukturiert gespiegelt.
- Der Call-Tree liefert ein gemeinsames Nodes-/Edges-Modell. In der geprüften Incoming-Abfrage waren Knoten eindeutig und Kanten als `caller → callee` gerichtet.
- Handoff-Chaining im Source-Modus funktioniert für eine tatsächlich gelieferte ID: Body, References, Call-Tree und Testkontext konnten mit der aus `find_symbol`/Dateizeile abgeleiteten Identität aufgerufen werden.
- Ungültige Parameter werden grundsätzlich maschinenlesbar als `INVALID_ARGUMENT` mit `fieldPath` gemeldet; der Versuch, `scopeType` an `get_class_structure` zu übergeben, zeigt die Schemaabweichung explizit statt stillschweigend zu ignorieren.

## Findings

### [Major] Befund E-01: Handoff-ID erscheint weiterhin im Feature-Markdown

- **Tools:** `find_symbol`, `get_feature_context`
- **Evidenz:** Ein gültiger Source-Call `get_feature_context(symbolIdentifier=<aus find_symbol>, targetPath=AiNetLinter.slnx)` liefert im regulären `content` unter „Symbol & Deklaration“ eine Zeile `DocCommentId: s:<target-token>:<content-token>:T:AiNetLinter.Core.TestCoverage.TestCoverageScanner`.
- **Problem:** Die kanonische Folge-Call-ID ist nicht auf `structuredContent` begrenzt. Ein Agent, der nur die Markdown-Antwort liest oder sie weiterreicht, bekommt damit genau die Identität, die laut Vertrag aus dem Agententext entfernt werden sollte. Die gleiche Antwort enthält zusätzlich dieselbe ID maschinenlesbar unter `structuredContent.declaration.docCommentId`; diese strukturierte Vorkommensweise ist korrekt, die Textkopie nicht.
- **Reproduktion:** 1. `find_symbol(pattern="TestCoverageScanner", maxResults=3)`; 2. erste Produktions-Typ-ID entnehmen; 3. `get_feature_context(symbolIdentifier=<ID>)`; 4. `content` nach `DocCommentId` durchsuchen.
- **Konzeptbezug:** Muss 2; AK 6, 8, 37; Slice 01–07 und Slice 15.
- **Empfehlung:** `DocCommentId` aus allen regulären Feature-Textprojektionen entfernen; nur `structuredContent` darf die kanonische Handoff-Identität enthalten. Danach kombinierte Text-/Structured-Bytes erneut messen.

### [Major] Befund E-02: Assembly-Analyse und Navigation melden konkurrierende Completeness-/Origin-Achsen

- **Tools:** `inspect_assembly`, `find_assembly_extensions`, `find_symbol`, `get_symbol_body` (Assembly-Modus)
- **Evidenz:** Bei `LOCAL-01` meldet ein erfolgreicher `get_symbol_body`-Call für eine direkt aus `find_symbol` entnommene Assembly-ID gleichzeitig `structuredContent.analysis.completeness=partial`, `structuredContent.analysis.origin=decompiled` und `structuredContent.navigation.status.completeness=complete`, `structuredContent.navigation.target.origin=assembly`. `inspect_assembly`/`find_symbol` zeigen dieselbe Kombination mit Navigation `truncated`; `find_assembly_extensions` zeigt zusätzlich eine fachliche Completeness `partial` bei Navigation `truncated`.
- **Problem:** Der Agent kann nicht erkennen, ob `partial` die fachliche Herkunfts-/Decompiler-Qualität oder die Antwortvollständigkeit beschreibt. Gleich benannte Felder (`completeness`, `origin`) stehen in derselben Response für verschiedene, nicht bezeichnete Achsen. Dadurch kann ein Agent einen fachlich partiellen Assembly-Zustand als vollständige Antwort weiterverarbeiten oder eine bloße Wire-Trunkierung als fachliche Unvollständigkeit interpretieren.
- **Reproduktion:** 1. `inspect_assembly(targetPath=LOCAL-01)`; 2. `find_symbol(targetPath=LOCAL-01, pattern=<generisches Muster>, maxResults=1)`; 3. die gelieferte Assembly-ID an `get_symbol_body` übergeben; 4. `analysis` und `navigation.status` nebeneinander vergleichen.
- **Konzeptbezug:** Muss 3, 10; AK 8, 31, 37; Slices 14, 18, 19.
- **Empfehlung:** Response-Vollständigkeit und fachliche Analysequalität unterschiedlich benennen (z. B. `navigation.status.completeness` versus `analysis.analysisCompleteness`) oder eine eindeutige Prioritätsregel dokumentieren. `navigation.target.origin` muss die Vertragswerte `source`/`assembly` behalten; `decompiled` sollte nur als explizit benannte Herkunftsqualität erscheinen.

### [Major] Befund E-03: `get_class_structure` verletzt den gemeinsamen Scope-/Generated-Vertrag

- **Tools:** `find_symbol`, `get_class_structure`
- **Evidenz:** `find_symbol` mit Default `includeGenerated=false` liefert den Produktions-Typ editierbar. `get_class_structure` für denselben Typ zeigt dagegen drei Dateien, darunter eine `obj/...g.cs`-Datei, ohne `scopeType` oder `sourceKind` an den Dateien/Members; der Text markiert die Datei ebenfalls nicht als Generated. Ein zusätzlicher Call mit `scopeType="production"` wird vom tatsächlichen Schema als `INVALID_ARGUMENT: Unbekanntes Argument: scopeType` abgelehnt.
- **Problem:** Der gleiche Symbol-/Klassenkontext kann nicht auf denselben Scope reduziert werden wie Symbol-, Referenz- und Graph-Tools. Generated-Evidenz wird standardmäßig unmarkiert in die Klassenstruktur gemischt; ein Agent kann sie für editierbare Quelle halten oder nicht reproduzierbar ausblenden.
- **Reproduktion:** 1. `find_symbol(pattern="TestCoverageScanner")`; 2. `get_class_structure(symbolIdentifier=<Typ-ID>)`; 3. `files`/Text auf `obj`/`.g.cs` prüfen; 4. denselben Call mit `scopeType="production"` wiederholen.
- **Konzeptbezug:** Muss 8, 10; AK 18–22, 31, 37; Slices 08–10 und 19.
- **Empfehlung:** Die gemeinsame Scope-/Generated-Projektion auch auf `get_class_structure` anwenden; mindestens `scopeType`, `includeGenerated`, `scopeType`/`sourceKind` in Locations und konsistente Default-/Opt-in-Semantik bereitstellen.

### [Major] Befund E-04: Feature- und Testkontext können nicht mit dem gemeinsamen Scope eingeschränkt werden

- **Tools:** `find_symbol`, `find_references`, `get_call_tree`, `dependency_graph`, `get_feature_context`, `get_test_context`
- **Evidenz:** Die geprüften Listen-/Graph-Tools akzeptieren `scopeType` und `includeGenerated`. Die tatsächlichen Schemas von `get_feature_context` (`maxCallers`, `maxTests`, `symbolIdentifier`, `targetPath`) und `get_test_context` (`maxResults`, `symbolIdentifier`, `targetPath`) enthalten diese Parameter nicht. Der Feature-Call liefert dennoch gleichzeitig Produktions-Call-Sites und Testkontext.
- **Problem:** Ein Agent kann eine Lösung nicht toolübergreifend auf `production` oder `tests` begrenzen. Besonders problematisch ist das Composite-Tool: Es zieht Testcode in eine als Feature-Kontext angeforderte Antwort, ohne dass der Aufrufer den gemeinsamen Scope-Vertrag kontrollieren kann. Das erschwert Vergleich, Ranking und reproduzierbares Budgetverhalten.
- **Reproduktion:** 1. `find_symbol`/`find_references` mit `scopeType="production"`; 2. denselben Handoff an `get_feature_context` und `get_test_context` weiterreichen; 3. feststellen, dass `scopeType` laut Schema nicht übergeben werden kann und die Antworten ihre eigene Population liefern.
- **Konzeptbezug:** Muss 5, 8, 9, 10; AK 11–13, 18–20, 28, 31, 37; Slices 08–12 und 15–16.
- **Empfehlung:** Den gemeinsamen Scope-/Generated-Vertrag für Composite-Kontext explizit implementieren oder im Schema klar als bewusst nicht anwendbar markieren und die Populationen strikt getrennt projizieren. Ein stilles Weglassen des Filters ist für Agenten nicht ausreichend.

### [Major] Befund E-05: Exakter Namespace-Drilldown liefert reproduzierbar einen falschen leeren/trunkierten Zustand

- **Tools:** `get_namespace_tree`, `get_file_tree`, `get_index_scope`
- **Evidenz:** Der Call `get_namespace_tree(project="AiNetLinter", namespacePrefix="AiNetLinter.Mcp.Tools.TestContext", depth=3, includeTypes=true, maxResults=50, maxResponseBytes=65536)` liefert `totalCount=0`, `shownCount=0`, `namespaces=[]`, aber `truncated=true`, `truncatedBy=["maxResponseBytes"]` und einen `next`-Hinweis zum Erhöhen des Budgets. Der Namespace ist in den Source-Dateien vorhanden; ohne Prefix zeigt der Projektbaum viele Namespaces, sodass dies kein echter Empty-Scope-Fall ist.
- **Problem:** Empty und truncated sind nicht disjunkt. Ein Agent erhält weder den erwarteten Namespace-Root noch die direkten Typen und wird zugleich zu einer Budgeterhöhung aufgefordert, obwohl bereits das maximale Budget verwendet wurde. Der in der Konzept-Dogfood-Sequenz geforderte exakte Namespace-Schritt ist damit nicht zuverlässig verkettbar.
- **Reproduktion:** Den obigen Call zweimal mit identischen Parametern ausführen; beide Antworten sind identisch leer und trunkiert. Anschließend `get_namespace_tree(project="AiNetLinter", depth=3, includeTypes=true)` als Kontrollabfrage ausführen.
- **Konzeptbezug:** Muss 10, 11; AK 22, 31, 35, 37; Slice 19 und Dogfood-Schritt 6.
- **Empfehlung:** Prefix vor Pagination/Budget auflösen und den passenden Namespace als Root mit direkten Typen projizieren. Bei tatsächlichem Empty-Scope `completeness=empty` und `next=null` liefern; bei Trunkierung muss mindestens ein fachlicher Treffer oder ein ehrlicher, erreichbarer Verfeinerungsschritt enthalten sein.

### [Major] Befund E-06: `RESPONSE_BUDGET_TOO_SMALL` ist im Navigation-Envelope als Erfolg markiert

- **Tools:** `get_test_context` (Vergleich mit anderen Budgettools)
- **Evidenz:** `get_test_context(..., maxResponseBytes=512)` antwortet korrekt mit `code=RESPONSE_BUDGET_TOO_SMALL`, `fieldPath=$.maxResponseBytes` und Mindestwert `2301`. Der zugehörige `structuredContent.navigation.status` lautet jedoch `operation="ok"`, `completeness="complete"`, `code="RESPONSE_BUDGET_TOO_SMALL"`.
- **Problem:** Ein maschinenlesbarer Fehlercode wird mit Erfolgsstatus und `complete` kombiniert. Ein Agent, der zuerst den Navigation-Status auswertet, kann die Antwort als erfolgreich/vervollständigt behandeln; ein Agent, der den Fehlercode auswertet, erhält widersprüchliche Zustandsinformationen. Andere Tools verwenden für Response-Budget-Grenzen entweder einen direkten `INVALID_ARGUMENT`-Fehler ohne Envelope oder eine andere Statusform.
- **Reproduktion:** `get_test_context(targetPath=AiNetLinter.slnx, symbolIdentifier=<gültige Member-ID>, maxResponseBytes=512)` aufrufen und `code` mit `navigation.status.operation/completeness` vergleichen.
- **Konzeptbezug:** Muss 5, 6, 10; AK 14, 15, 31; Slices 15–17.
- **Empfehlung:** Für `RESPONSE_BUDGET_TOO_SMALL` eine eindeutige Fehleroperation und Fehler-Completeness verwenden (z. B. `operation="error"`, `completeness="not_applicable"` oder eine dokumentierte eigene Budget-Statusklasse). Die Kombination `operation=ok` plus nicht-null `status.code` darf nicht als gültiger Erfolg erscheinen.

### [Minor] Befund E-07: Trunkierte Composite-Texte wiederholen den Folgeschritt mehrfach

- **Tools:** `get_feature_context`, `get_test_context`
- **Evidenz:** Ein Feature-Call mit begrenztem `maxCallers`/`maxTests` enthält denselben nächsten Schritt im Kopf, im betroffenen Abschnitt und nochmals als abschließende Statuszeile. `get_test_context(maxResults=1)` enthält zusätzlich einen langen empfohlenen Testfilter trotz nur eines sichtbaren Kandidaten.
- **Problem:** Statusinformationen verdrängen fachliche Evidenz und machen die eigentliche Aktion schwer erkennbar. Das steht im Widerspruch zur Vorgabe „genau eine entscheidungsrelevante Zeile“ für recoverable/partial/truncated.
- **Reproduktion:** `get_feature_context(..., maxCallers=2, maxTests=2)` und `get_test_context(..., maxResults=1)` aufrufen; Text auf wiederholte `Nächster sicherer Schritt`-/`Status`-Zeilen prüfen.
- **Konzeptbezug:** Muss 4, 5, 6; AK 10–16; Slices 15–16.
- **Empfehlung:** Status/Folgeschritt nur einmal nach der fachlichen Projektion ausgeben; lange Testfilter nur auf explizite Nachfrage oder als strukturierte, gekürzte Empfehlung bereitstellen.

## Begrenzungen

- Die Assembly-Checks verwendeten ausschließlich den zulässigen anonymen Label `LOCAL-01`; es wurden keine externen Produkt- oder Symbolidentifikatoren in diesen Bericht übernommen.
- Kein vollständiger End-to-End-Diamond-/Zyklus-Test wurde in dieser Gruppe neu erzeugt; die Live-Call-Tree-Antwort wurde auf eindeutige Nodes/Edges und Renderer-/Envelope-Form geprüft.
- `tools/list` wurde über die vom Server veröffentlichten Tool-Schemas der realen Aufrufe ausgewertet; eine separate Rohserialisierung des vollständigen Server-`tools/list`-Payloads war für diesen Cross-Tool-Fokus nicht erforderlich.
- Es wurde keine Codekorrektur vorgenommen. Die Befunde sind Ist-Zustand gegen `Konzept.md`, nicht gegen einen lokalen Test- oder Buildlauf.
