# Audit Gruppe C – Symbol-Chaining

## Methodik

Read-only-Agent-UX-Audit gegen das Source-Target `AiNetLinter.slnx`. Die
Symbol-IDs wurden ausschließlich aus `structuredContent` übernommen und ohne
Transformation an Folge-Tools weitergereicht. Zusätzlich wurden die
Assembly-Routen mit den anonymisierten Labels `Testassembly A` und
`Testassembly B` geprüft. Es wurden keine Code-, Test- oder Buildänderungen
vorgenommen.

Geprüfte Ketten und Grenzfälle:

- `find_symbol` mit `maxResults=1`, deterministische Wiederholung sowie
  `scopeType=all|production|tests` und `includeGenerated=false|true`.
- `find_symbol` → `get_symbol_body`; strukturierter Typ-/Member-Handoff aus
  `get_file_skeleton` → `get_symbol_body`.
- `find_symbol` → `get_feature_context`; anschließend
  `find_references` → `get_impact`.
- `get_class_structure` → einzelner Member → `get_symbol_body`.
- `get_call_tree` für `incoming`, `outgoing`, `both`, ASCII/Mermaid und
  mehrere monotone Wirebudgets.
- Assembly: `find_symbol` ohne/mit `includeReferences`, Body-Handoff und
  `find_references` mit beiden Werten; danach korrekte Wiederholung nach
  Fehlern.
- Ungültige Werte (`maxResults=0`, `depth=0`, unbekanntes `scopeType`,
  Graph-Node-ID als Symbol-Identifier) und unmittelbarer korrekter Retry.

## Positive Ergebnisse / Evidenz

- `find_symbol(pattern="Get", maxResults=1)` lieferte `763` gefilterte
  Treffer, genau einen gezeigten Treffer, `isTruncated=true`, einen klaren
  nächsten Schritt und `completeness=truncated`. Die Treffer-ID stand im
  `structuredContent`; die reguläre Textantwort enthielt sie nicht.
- Die ausgegebene Source-ID hatte zwei 22-Zeichen-Base64url-Tokens. Der direkte
  Handoff derselben ID an `get_symbol_body` lieferte einen verfügbaren Source-
  Body ohne ID-Echo im Text. Auch `get_file_skeleton` lieferte stabile IDs an
  vollständigen Skeleton-Einheiten; der Member-ID-Handoff zum Body war direkt
  erfolgreich.
- Die Kette `find_symbol` → `get_feature_context` wurde mit der
  strukturierten ID akzeptiert. `find_references` lieferte strukturierte
  `callSites`, Scope, Snapshot und Handoff-Metadaten; `get_impact` auf
  derselben ID meldete `impact_found`, fünf statische Call-Sites und die
  betroffenen Projekte.
- `get_class_structure` akzeptierte die Typ-ID, meldete `23` Gesamtmember,
  bei `maxMembers=5` fünf sichtbare Member, `truncated=true` und einen
  handlungsweisenden nächsten Schritt. Ein einzelner Member ließ sich danach
  per kanonischem Klassen-/Membernamen an `get_symbol_body` weiterreichen.
- Call-Graph-Antworten verwenden Nodes/Edges. Für dieselbe Anfrage hatten
  ASCII und Mermaid jeweils `7` Nodes und `6` Edges; die strukturierten
  Graphen waren somit projektionsgleich. Bei einer größeren `both`-Anfrage
  waren `45` Node-IDs alle eindeutig. Incoming-Kanten waren korrekt als
  `caller -> seed` orientiert. Kleinere Budgets reduzierten ganze Graph-
  Einheiten monoton (`4/2`, `10/8`, `20/17`, `45/41` Nodes/Edges), ohne
  Stringfragmentierung.
- Scope und Ranking waren nachvollziehbar: Beim Scanner-Muster enthielt
  `all` zwei Production- und einen Testtreffer; `production` und `tests`
  waren disjunkt, Production stand bei `all` zuerst.
  `includeGenerated=true` fügte eine als `generated` markierte Location
  hinzu, statt sie unmarkiert vor Editable-Code zu stellen.
- Grenzfehler waren maschinenlesbar und recoverable: jeweils
  `INVALID_ARGUMENT`, Feldpfad, konkrete Korrektur und kein Stack-Trace.
  Nach `maxResults=0` bzw. ungültigem Scope funktionierte ein korrekter
  Folgeaufruf unverändert. Die Übergabe einer Graph-Node-ID wurde nicht als
  Symbol-ID akzeptiert, sondern als `SYMBOL_NOT_FOUND` mit Retry-Hinweis.
- Assembly-Symbolsuche ohne Referenzsuche war als dekompilierte, partielle
  Antwort erkennbar; der daraus entnommene Member-Handoff lieferte einen
  verfügbaren Decompile-Body. Assembly-Referenzpfade enthielten partielle
  Diagnosen und waren nicht silent-empty.

## Findings

### [Major] Befund 1: Feature-Kontext wiederholt kanonische ID im Text

- **Tools:** `find_symbol`, `get_feature_context`
- **Evidenz:** `find_symbol` lieferte eine `s:`-ID ausschließlich im
  `structuredContent`. Derselbe Wert wurde unverändert an
  `get_feature_context` übergeben; dessen `structuredContent` war fachlich
  erfolgreich, die reguläre Markdown-Antwort enthielt jedoch eine
  `DocCommentId`-Zeile mit der vollständigen kanonischen ID.
- **Problem:** Ein Agent, der Text und StructuredContent gemeinsam in den
  Kontext übernimmt, bekommt die teure/kanonische Handoff-ID doppelt. Das
  durchbricht den verbindlichen Vertrag „ID nur StructuredContent“ und kann
  bei großen Feature-Antworten wieder genau die Metadatenkosten erzeugen, die
  der Task beseitigen sollte.
- **Reproduktion:** Source-Target wählen → `find_symbol` mit Muster
  `AnalysisSymbolIdentity` → ID aus
  `structuredContent.results[].matches[].id` nehmen →
  `get_feature_context(symbolIdentifier=<ID>)` → Text auf `s:t` bzw.
  `DocCommentId` prüfen.
- **Konzeptbezug:** AK 1–6, insbesondere AK 6 („Keine reguläre Textantwort
  ... enthält eine Handoff-ID“), Umsetzungsvertrag „kanonische IDs stehen
  ausschließlich im normalisierten StructuredContent“.

### [Major] Befund 2: Assembly-`find_symbol(includeReferences=true)` scheitert
mit Defaultbudget und gibt einen unbrauchbaren Mindestwert aus

- **Tool:** `find_symbol` auf `Testassembly A`
- **Evidenz:** Mit `includeReferences=true`, `pattern="A"`,
  `maxResults=1` und ohne explizites Budget (Default 16 KiB) kam
  `INVALID_ARGUMENT`: der minimale Wire-Envelope sei nicht repräsentierbar.
  Der Hint empfahl mindestens `2048` Bytes. Die Wiederholung mit `2048`,
  `8192` und `16384` Bytes scheiterte mit derselben Meldung; erst `32768`
  Bytes war erfolgreich und lieferte `35239` Gesamtmatches sowie
  `completeness=partial`.
- **Problem:** Der dokumentierte optionale Referenzpfad ist mit seinem
  Default nicht nutzbar. Der Korrekturhinweis führt den Agenten in drei weitere
  ungültige Aufrufe; die tatsächliche Untergrenze ist weder erkennbar noch
  schema-konform.
- **Reproduktion:** Frische Assembly-Route → `find_symbol` mit
  `includeReferences=true` und Defaultbudget → denselben Call mit dem im
  Fehlerhint genannten Wert → erst mit `maxResponseBytes=32768` wiederholen.
- **Konzeptbezug:** AK 14–16 (Budget/Minimum/monotone Erweiterung), AK 37
  (Assemblysuche mit relevanter Evidenz), Assembly-Teil des Symbol-Chaining-
  Prüfkatalogs.

### [Major] Befund 3: `find_references(includeReferences=false)` ignoriert den
Assembly-Referenzschalter

- **Tool:** `find_references` auf `Testassembly A` und `Testassembly B`
- **Evidenz:** Nach einer Assembly-`find_symbol`-Suche ohne Referenzen wurde
  `find_references` mit `includeReferences=false` aufgerufen. In beiden
  Fällen meldete `structuredContent.navigation` dennoch
  `includeReferences=true`, `totalAssemblyCount=16` und
  `searchedAssemblyCount=16`; die Antwort war wegen Decompilerdiagnosen
  `completeness=partial`. Der explizite `true`-Aufruf zeigte denselben
  Referenzumfang.
- **Problem:** Ein Agent kann den gewünschten bounded Scope nicht steuern und
  kann den Unterschied zwischen „nur Zielassembly“ und „mit Referenzen“ nicht
  aus der Antwort ableiten. Das verletzt den veröffentlichten Parametervertrag
  und macht Kosten-/Vollständigkeitsentscheidungen unzuverlässig.
- **Reproduktion:** Für `Testassembly A` oder `Testassembly B` eine Member-ID
  aus `find_symbol(..., includeReferences=false)` nehmen →
  `find_references(symbolIdentifier=<ID>, includeReferences=false)` →
  `navigation.includeReferences` und Assemblyzähler prüfen.
- **Konzeptbezug:** AK 8, AK 19 und AK 37; ausdrücklich „Source vs. Assembly,
  includeReferences und failures“.

### [Major] Befund 4: Hochvertrauens-Testevidenz im Feature-Kontext bleibt ohne
konkrete Testmethoden

- **Tool:** `get_feature_context`
- **Evidenz:** Für den Hot-Type `AnalysisSymbolIdentity` meldete die
  strukturierte Antwort `totalMatchingTests=22` in sechs Testdateien. Die
  sichtbaren Testdateieinträge hatten jedoch `evidenceKind=directTypeUse`,
  `confidence=high`, aber jeweils `testMethods=[]`; auch
  `displayedTestMethods` war `0`. Die Antwort erklärte gleichzeitig
  `completeness=truncated` und verwies auf ein erneutes Abfragen von
  `maxTests`.
- **Problem:** Der Agent erhält eine hohe Vertrauensbehauptung und Counts,
  aber keinen direkt ausführbaren Testkandidaten. Für die im Konzept geforderte
  Hot-Feature-Minimalprojektion ist damit die konkrete Testevidenz nicht
  handlungsfähig; das ähnelt dem ausdrücklich ausgeschlossenen Counts-only-
  Fallback. Die type-convention-Fälle für einen Member wurden dagegen korrekt
  als `confidence=low` ohne erfundene Methoden ausgegeben.
- **Reproduktion:** `get_feature_context(symbolIdentifier=<strukturierte
  Type-ID>, maxCallers=5, maxTests=5, maxResponseBytes=65536)` →
  `testContext.testFiles[*].evidenceKind/confidence/testMethods` und
  `displayedTestMethods` prüfen.
- **Konzeptbezug:** AK 11–13, AK 28 und AK 37; Handover-Risikostelle
  „direct=high/method, type-convention=low/class/count“.

## Grenzen und Datenschutz

Die Assemblyprüfungen wurden nur als `Testassembly A/B` dokumentiert. Die
laufenden Toolantworten enthielten naturgemäß absolute Assembly-/Decompiler-
Herkunft sowie diagnostische Symbol- und Pfadtexte; diese Werte wurden weder
in diese Datei noch in die Befundzusammenfassung übernommen. Es wurden keine
Assemblies geladen oder ausgeführt. Nicht geprüft wurden Health/Discovery,
Impactstatus-Matrix und Dokumentations-/`tools/list`-Kosten außerhalb der für
Chaining benötigten Schemas.
