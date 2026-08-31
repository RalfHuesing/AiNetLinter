# MCP-Live-/Vertragsaudit: Health-, Tree- und Index-Verträge

Datum: 2026-08-31
Status: abgeschlossen, keine Produktions-/Testcodeänderung

## Scope und Methode

Geprüft wurden ausschließlich die live erreichbaren MCP-Verträge für
Projekt- und Assembly-Ziele: `get_server_health`, `get_file_tree`,
`get_index_scope` sowie die im aktuellen Agenten-Toolkatalog sichtbaren
registrierten Parameterdefinitionen. Es wurden keine Builds oder Tests
ausgeführt. Die lokale Zielauflösung erfolgte nur für MCP-Aufrufe; konkrete
externe Identifikatoren werden hier nicht wiedergegeben.

Die initiale `code-map.md` enthielt keine übernommenen Altbefunde. Ältere
Task-Ordner wurden nicht als Evidenzquelle verwendet.

## Ergebnisübersicht

| ID | Kategorie / Umfang | Dringlichkeit | Disposition |
|---|---|---:|---|
| F-HEALTH-001 | `get_file_tree`-Tiefenvertrag, lokal am Tool | P1 | accepted-deferred; Fix im Produktionsscope erforderlich |
| F-HEALTH-002 | Root-`summary`-Payload und `maxResults`, lokal am Tool | P2 | accepted-deferred; Tech-Debt-Kandidat |
| F-HEALTH-003 | globaler Health-Payload, systemisch | P2 | accepted-deferred; Tech-Debt-Kandidat |
| F-HEALTH-004 | Fehler-/`isError`-Klassifikation, systemisch | P1 | accepted-deferred; Fix im Produktionsscope erforderlich |

Es gab in diesem Subagenten-Scope keine sichere, erlaubte Produktionskorrektur.

## Live-Proben

Größen sind Zeichenanzahlen der Textblöcke bzw. JSON-StructuredContent-
Nutzlasten, nur ungefähr und ohne Rohantworten.

| Probe | Zielklasse / relevante Parameter | Ergebnis und Nutzbarkeit |
|---|---|---|
| `get_server_health` global, Standard | kein Target; Diagnose standardmäßig aus | `isError=false`; global getrennte Projekt-/Assembly-Projektionen. Früher kompakter Zustand ca. 1,6k/1,9k Zeichen; nach dem Erwärmen der Assembly-Sessions ca. 43,1k/76,9k Zeichen. Diagnosezähler und `diagnosticLimit=20` sichtbar. Die Nutzbarkeit sinkt bei vielen residenten Sessions; siehe F-HEALTH-003. |
| `get_server_health` global, Diagnose | kein Target, `includeDiagnostics=true` | `isError=false`; begrenzte Samples werden sichtbar, `diagnosticLimit=20`. Im erwärmten Zustand ca. 66k Textzeichen; Trunkierung/Diagnosefelder je Assembly nachvollziehbar, aber nicht kompakt. |
| `get_server_health` projektgebunden, Standard | `targetType=project`, kanonisches project target | `isError=false`; genau eine geladene Projektsicht, keine Assemblyliste; ca. 0,8k/0,9k Zeichen. Gute agentische Projektion. |
| `get_server_health` projektgebunden, Diagnose | zusätzlich `includeDiagnostics=true`, `maxDiagnostics=2` | `isError=false`; `diagnosticsIncluded=true`, effektives Limit 2; keine unerwartete Diagnoseausweitung. Kein Befund. |
| `get_server_health` assemblygebunden, Standard | `targetType=assembly`, repo-provided assembly | `isError=false`; `loadState=partial`, `originKind=decompiled`, `completeness=partial`, `trust=untrusted`; Diagnoseübersicht mit Trunkierung. Herkunft und Unsicherheit sind agentisch erkennbar. Kein Befund. |
| `get_server_health` assemblygebunden, Diagnose | `includeDiagnostics=true`, `maxDiagnostics=1`; zusätzlich zwei installierte vendor assemblies A/B | `isError=false`; alle drei Proben zeigen `partial`/`decompiled` und begrenzte Diagnose-Samples. Bei einer Probe: Root insgesamt 100, transitive 100, zusammen 200; ein Sample sichtbar, Trunkierung wird ausgewiesen. Kein Befund zur Herkunfts-/Completeness-Projektion. |
| `get_server_health` Grenzwerte | `maxDiagnostics=0`, `-1`, sehr groß | Kein Fehler; 0 und -1 werden effektiv auf 20, sehr groß auf 50 begrenzt. Das effektive Limit wird zurückgegeben. Kein Befund; Dokumentation/Schema sollten die Clamp-Regel dennoch explizit machen. |
| `get_file_tree` Root summary | `view=summary`, `root="."`, `maxResults=1/100/2000` | `isError=false`, Scan vollständig, keine Dateitrunkierung; ca. 1,6k/24,3k Zeichen. `files=[]`, aber 186 Directory-Einträge und 20 Extension-Aggregate werden dennoch übertragen. `maxResults` ändert die Summary-Nutzlast nicht. Siehe F-HEALTH-002. |
| `get_file_tree` Root tree | `view=tree`, `treeDepth=0/1/2/3`, `maxResults=100` | Alle vier Aufrufe liefern dieselben Directory-Tiefen 0..6 und Dateitiefen bis 5; die Dateiliste wird bei 100 wegen `maxResults` abgeschnitten. `treeDepth` wirkt nicht. Siehe F-HEALTH-001. |
| `get_file_tree` tatsächliche Tiefe | `maxDepth=0/1/2` | Wirksam: maximale Directory-Tiefen 0/1/2 und jeweils entsprechend begrenzte Dateitiefen. Das bestätigt, dass die serverseitige Scanbegrenzung derzeit an `maxDepth` hängt, nicht an `treeDepth`. |
| `get_file_tree` Root tree klein | `treeDepth=0`, `maxResults=1` | `isError=false`, `truncated=true`, `truncatedBy=[maxResults]`; 186 Directory-Einträge plus eine Datei. Auch bei kleiner Dateigrenze bleibt die Directory-Nutzlast groß. Siehe F-HEALTH-002. |
| `get_file_tree` ungültige Ziele/Optionen | Assemblyziel, fehlendes Target-Paar, ungültige Ansicht, negative/überhohe Tiefe, `maxResults=0` | Die Antworten enthalten jeweils Diagnose-/Fehlertext ohne einheitliches StructuredContent; `isError` ist nicht einheitlich. Siehe F-HEALTH-004. |
| `get_index_scope` projektgebunden | `targetType=project`, project target | `isError=false`; ca. 0,3k/0,4k Zeichen; sechs Extension-Kategorien mit Dateianzahlen und `symbolGraphCovered`. Die Abdeckung ist direkt agentisch verwertbar. Kein Befund. |
| `get_index_scope` Assemblyziel | `targetType=assembly` | Unsupported-Vertrag wird als normaler Text signalisiert, `isError=false`, kein StructuredContent. In Kombination mit den übrigen Fehlerpfaden Teil von F-HEALTH-004. |

## Findings

### F-HEALTH-001 – `treeDepth` wird wire-seitig ignoriert

- Klassifikation: finding; Umfang: lokal am `get_file_tree`-Vertrag; P1.
- Evidenz: Root-Tree-Aufrufe mit `treeDepth=0`, `1`, `2` und `3` lieferten
  identische Directory-Tiefen 0..6 sowie Dateien bis Tiefe 5. Separate
  `maxDepth=0`, `1`, `2`-Aufrufe änderten die Tiefen dagegen erwartungsgemäß.
- Agentische Auswirkung: Die in der MCP-Regel vorgeschriebene Root-Abfrage mit
  `treeDepth<=2` schützt nicht vor tiefer Struktur und größerer Nutzlast. Ein
  Agent kann deshalb trotz korrekter Vertragsnutzung unerwartet tiefen Kontext
  erhalten.
- Empfehlung: `treeDepth` entweder als echte Ausgabe-/Traversalbegrenzung
  implementieren oder eindeutig aus dem Schema entfernen und ausschließlich
  `maxDepth` anbieten. Regressionstests müssen mindestens 0, 1 und 2 sowie
  die Kombination mit `maxResults` abdecken.
- Tech-Debt: ja; keine sichere Korrektur innerhalb des erlaubten Berichtsscope.

### F-HEALTH-002 – Root-`summary` ist nicht kompakt und ignoriert `maxResults`

- Klassifikation: finding; Umfang: lokal am `get_file_tree`-Vertrag; P2.
- Evidenz: `view=summary` lieferte ca. 24,3k StructuredContent-Zeichen,
  186 Directory-Einträge und keine Dateien. `maxResults=1`, `100` und `2000`
  waren für die Summary identisch; die Directory-Liste blieb vollständig.
- Agentische Auswirkung: Die als token-effizient vorgesehene Root-Summary
  entspricht nicht der erwarteten kleinen Aggregatantwort. Bei größeren
  Repositories skaliert sie mit der Directory-Anzahl.
- Empfehlung: Summary standardmäßig auf Root-Aggregate und kompakte
  Extensionwerte reduzieren; alternativ Directory-Aggregate explizit mit
  eigener Grenze versehen und `maxResults` konsistent anwenden. Die Antwort
  sollte Trunkierung der Directory-Aggregate separat ausweisen.
- Tech-Debt: ja.

### F-HEALTH-003 – globaler Default-Health wächst ungebunden mit Sessions

- Klassifikation: finding; Umfang: systemisch/globaler Aggregationspfad; P2.
- Evidenz: Derselbe parameterlose Health-Aufruf war im frühen Zustand ca.
  1,6k/1,9k Zeichen groß und nach dem Erwärmen vieler Assembly-Sessions ca.
  43,1k/76,9k Zeichen groß; Diagnosemodus erreichte ca. 66k Textzeichen.
  Die Antwort enthielt 107 Assembly-Einträge mit Herkunft, Completeness und
  Diagnoseübersichten.
- Agentische Auswirkung: Der Default ist zwar semantisch korrekt und enthält
  `diagnosticsIncluded=false`, aber seine Größe hängt von residenten Sessions
  ab. Das erschwert zuverlässige Kontextplanung und kann den behaupteten
  kompakten Health-Use-Case verfehlen.
- Empfehlung: globalen Health aggregieren statt alle Sessiondetails zu
  expandieren; Details nur zielgebunden oder über explizite Pagination/
  `maxResults` ausgeben. Ein Wire-Budget und ein sichtbares Truncationfeld für
  globale Listen sollten verbindlich sein.
- Tech-Debt: ja.

### F-HEALTH-004 – Fehlerpfade signalisieren Vertragsfehler inkonsistent

- Klassifikation: finding; Umfang: systemisch über Health, Tree und Index;
  P1.
- Evidenz: Bei `get_server_health` mit nur einem Teil des Target-Paars,
  ungültigem `targetType` oder relativem Pfad kam Fehlertext mit
  `isError=false`; ein nicht vorhandenes Projektziel kam mit `isError=true`.
  Bei `get_file_tree` war ein fehlendes Target-Paar `isError=true`, ein nicht
  initialisiertes Ziel dagegen `isError=false`; ungültige Ansicht und
  ungültige Grenzwerte waren ebenfalls `isError=false`. Unsupported-
  Assemblyziele bei Tree/Index wurden als normaler Text ohne StructuredContent
  signalisiert.
- Agentische Auswirkung: Ein Client kann nicht einheitlich zwischen
  erfolgreicher Leermenge, ungültiger Anfrage, nicht initialisiertem Ziel und
  unsupported Target unterscheiden. Das erschwert sichere Retry-/Fallback-
  Entscheidungen; zusätzlich fehlt bei diesen Fehlern ein stabiles
  StructuredContent-Fehlerschema.
- Empfehlung: Fehlerklassen und `isError`-Semantik toolübergreifend
  standardisieren. Mindestens `code`, `category`, `targetKind` und eine
  redigierte, menschenlesbare Meldung in StructuredContent liefern; normale
  Leermengen davon trennen. Die dokumentierte `isError`-Policy muss mit den
  tatsächlich ausgelieferten Pfaden abgeglichen werden.
- Tech-Debt: nein; P1-Produktionsfix empfohlen.

## Kein Befund / Grenzen

- Assembly-Health projiziert `originKind=decompiled`, `loadState=partial`,
  `completeness=partial` und `trust=untrusted` nachvollziehbar. Diagnose-
  Samples und Root/transitive Trunkierung werden getrennt ausgewiesen.
- `maxDiagnostics` wird serverseitig begrenzt und das effektive Limit wird
  sichtbar zurückgegeben; für die geprüften Grenzen kein Befund.
- Projekt-`get_index_scope` liefert eine kleine, klare Abdeckungstabelle;
  Assemblyziele sind dort nachvollziehbar unsupported.
- Im aktuellen Agenten-Toolkatalog sind die relevanten registrierten
  Parameterdefinitionen sichtbar. Ein separat aufrufbares `tools/list` war in
  dieser Oberfläche nicht registriert; daher wurde die Schema-Sicht über den
  bereitgestellten Toolkatalog geprüft, nicht über eine zusätzliche Rohabfrage.
- Keine Produktions-/Teständerung, kein Build, kein Testlauf, kein Push.

## Ausgeführte MCP-Abfragen

`get_server_health` global/projektgebunden/assemblygebunden mit Standard-,
Diagnose- und Grenzparametern; `get_file_tree` als Root-`summary` und Root-
`tree` mit Tiefen-/Resultatgrenzen sowie ungültigen Optionen;
`get_index_scope` projektgebunden und mit unsupported Assemblyziel.
