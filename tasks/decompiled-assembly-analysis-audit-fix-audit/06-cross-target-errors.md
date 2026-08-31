# MCP-Live-/Vertragsaudit: Cross-Target-, Graph- und Fehlerpfade

Datum: 2026-08-31
Status: abgeschlossen; ausschließlich nicht-destruktive MCP-Proben

## Rahmen

Geprüft wurden nur Wire-Verträge für `project target`, `repo-provided
assembly` und `installed vendor assembly A/B`. Konkrete Pfade, Assembly-,
Produkt-, Hersteller-, Namespace- und Symbolnamen sind redigiert:
`Type_1`, `Member_1` und `File_1` bezeichnen ausschließlich Werte, die aus
einer vorherigen Antwort für die jeweilige Folgeprobe übernommen wurden.
Es gab keine Produktions- oder Testcodeänderung, keinen Build, keinen Testlauf
und keine Dateioperation außerhalb dieses Berichts.

## Ergebnisregister

| ID | Umfang | Priorität | Disposition |
|---|---|---:|---|
| CT-DG-001 | Pfadvertrag von `dependency_graph` für dekompilierte Dokumente | P1 | Produktionsfix erforderlich; Folgepfad normalisieren oder direkt wiederverwendbaren Identifier ausgeben. |
| CT-ERR-001 | Fehler- und Unsupported-Wirevertrag mehrerer project-only-Tools | P1 | Produktionsfix erforderlich; Fehler als einheitliches StructuredContent mit konsistentem `isError` ausgeben. |
| CT-SES-001 | Assembly-Session-Identität bei kanonischem versus reparse-ähnlichem Zielpfad | P2 | Tech-Debt; Ziel vor der Session-Key-Bildung kanonisieren. |

## Probeprotokoll

Antwortgrößen sind ungefähr Textzeichen; sie sind ein Budgetindikator, keine
Tokenmessung. „Kein Finding“ bedeutet nur: innerhalb der geprüften
Parameterkombination kein zusätzlicher Vertragsbefund.

| Probe | Status / Zielklasse / Parameter | Herkunft, Completeness, Truncation, Diagnostics | Größe / Nutzerwirkung / Evidenz | Finding, Umfang, Priorität, Disposition |
|---|---|---|---|---|
| Discovery | erfolgreich; `project target`; `get_file_tree` als Root-`summary`, anschließend `get_index_scope` | Scan vollständig, nicht gekürzt; Symbolindex deckt den sichtbaren C#-Bestand ab | ca. 1,6k bzw. 0,3k; kleine Indexantwort ist sofort nutzbar, Summary enthält weiterhin eine breite Verzeichnisprojektion | Kein zusätzlicher Befund; bestehende Summary-Budgetgrenze bleibt außerhalb dieses Scopes. |
| Projektgraph | erfolgreich; `project target`; `dependency_graph` auf `File_1`, `both`, Tiefe 1, Limit 3 | strukturierter Graph; 15 Kanten gesamt, 3 gezeigt, `truncated=true` | ca. 0,5k; Richtung, Slice und Kürzung sind für Folgeabfragen eindeutig | Kein Finding; lokal, P2. |
| Projektimpact, Default | erfolgreich; `project target`; `get_impact` ohne Symbol- oder Git-Parameter | keine Treffer für den Default-Scope, keine StructuredContent-Projektion | ca. 0,1k; klare Leermenge, aber für den Default kein maschinenlesbarer Leermengentyp | Kein zusätzlicher Befund; lokaler Defaultpfad, P2. |
| Projektimpact, Symbol | erfolgreich; `project target`; `get_impact` auf `Type_1`, Aufrufer, Tiefe 1, Limit 3 | 17 Aufrufer insgesamt, 3 gezeigt; voll strukturierte Completeness und Resultatbegrenzung | ca. 0,5k; symbolbezogener Branch ist semantisch brauchbar | Kein Finding; lokal, P2. |
| Testkontext | erfolgreich; `project target`; `get_test_context` auf `Type_1`, Limit 3 | zwei Testdateien, 13 Testmethoden; vollständig, nicht gekürzt | ca. 1,9k; statische Zuordnung und Testempfehlung sind agentisch verwertbar | Kein Finding; lokal, P2. |
| Textsuche ohne/mit Anreicherung | erfolgreich; `project target`; kleine `maxResults`/`maxFiles`/`contextLines`/`maxResponseBytes`, `enrichCSharp=false` und `true` | in beiden Fällen wegen aller drei Budgets gekürzt; mit Anreicherung sichtbare Treffer korrekt als nicht anwendbar beziehungsweise aufgelöst markiert | je ca. 0,5k; opt-in-Anreicherung verändert nur die semantische Projektion, nicht den Textslice | Kein Finding; lokaler Suchvertrag, P2. |
| Assemblygraph, Root/Richtungen | erfolgreich; `repo-provided assembly`; `Type_1`, Default sowie `outgoing`, `incoming`, `both`, Tiefe 1/2 und kleine Limits | decompiled, untrusted, partial; umfassende Compilerdiagnostik sichtbar; Root-Scope hatte keine Kanten und war nicht gekürzt | ca. 0,7–0,8k; Root-, Richtungs- und Limitfelder sind klar, die Leermenge ist wegen partial nicht als globale Negativaussage nutzbar | Kein Finding; Assemblygraph nur unter Herkunfts-/Completeness-Vorbehalt verwenden. |
| Assemblygraph, positive Gegenprobe | erfolgreich; `installed vendor assembly A/B`; `Type_1`, `both`/`outgoing`, Tiefe 1/2, Limit 1/3 | decompiled, untrusted, partial; sichtbare Kanten, bei kleinen Limits korrekt `truncated=true`; Diagnostik getrennt sichtbar | ca. 0,7–0,9k; beweist, dass die Graph-Engine Assembly-Kanten semantisch liefern kann, ohne Vollständigkeit zu behaupten | Kein Finding; toolweit, P2. |
| Assemblydatei absolut | erfolgreich; `repo-provided assembly`; `dependency_graph` mit einem aus der Antwort stammenden absoluten `File_1` | decompiled, partial; strukturierter leerkantiger File-Scope mit Herkunft, Generation und Diagnostics | ca. 0,7k; absoluter dekompilierter Folgepfad ist nutzbar | Referenzevidenz für CT-DG-001. |
| Assemblydatei relativ / ungültig | Fehlertext, aber `isError=false`; `repo-provided assembly`; derselbe `File_1` relativ sowie nicht existierendes `File_1` | keine StructuredContent-, Completeness- oder Diagnostics-Projektion nach dem Fehler | ca. 0,5k; der in der Herkunft sichtbare relative Pfad lässt sich nicht als nächster `filePath` verwenden | **CT-DG-001**, Tool lokal, P1; relativen Vertrag reparieren oder eindeutig als nicht wiederverwendbar ausweisen. |
| Project-only auf Assembly | Unsupported-Text, jeweils `isError=false`; `get_index_scope`, `get_impact`, `get_test_context`, `search_pattern` gegen `repo-provided assembly` | kein StructuredContent, keine Fehlermetadaten; identischer lesbarer Unsupported-Code | je ca. 0,4k; für Menschen verständlich, für Clients nicht von erfolgreicher Textantwort unterscheidbar | **CT-ERR-001**, systemisch, P1; strukturiertes Unsupported-Ergebnis und konsistente Fehlersemantik ergänzen. |
| Gegenseitige Graphprobe | erfolgreich; `project target`; `dependency_graph` auf `Type_1`, `incoming`, Tiefe 1, Limit 1 | strukturierter, korrekt gekürzter Projektgraph | ca. 0,2k; bestätigt die project-/assembly-spezifische Routinggrenze ohne Cross-Target-Vermischung | Kein Finding; toolweit, P2. |
| Negativziele | Fehlertext, jeweils `isError=false`; relativer/nicht existierender `project target`, Verzeichnis statt Assembly, fremde Endung, leere/fehlende Identifier, beide Identifier | keine StructuredContent-Fehlerdaten; Traversalpfad wird auf das kanonische Ziel aufgelöst | ca. 0,1–0,5k; Validierung verhindert die unzulässigen Anfragen, aber die Maschinenklassifikation ist nicht konsistent nutzbar | **CT-ERR-001**, systemisch, P1; positive Validierung beibehalten, Wirefehler vereinheitlichen. |
| Reparse-ähnlicher Zielpfad | erfolgreich; `repo-provided assembly`; opaque/reparse-ähnliche absolute Schreibweise mit `Type_1` | gleicher Assembly-Hash, gleiche decompiled/partial-Herkunft und gleiches Ergebniskernbild; jedoch Generation 1 statt 2 für den kanonischen Pfad | ca. 0,8k; keine fachliche Datenlecke sichtbar, aber derselbe Inhalt wird als zweite Assembly-Session geführt | **CT-SES-001**, Session-Registry, P2; vor Key-Bildung kanonisieren, damit Caching und Retry stabil bleiben. |
| Wiederholbarkeit / Isolation | erfolgreich; Wechsel `project target` → `installed vendor assembly A/B` → `repo-provided assembly`, danach Wiederholung des kleinen Graphaufrufs | Projektindex blieb gleich; je Assembly blieben Hash, Herkunft, partial-Status und Response-Kern im jeweiligen Ziel stabil | ca. 0,3–0,8k; keine Cross-Target-Antwort, kein Hash-/Origin-Leak zwischen verschiedenen Zielklassen beobachtet | Kein Finding zur Datenisolation; CT-SES-001 betrifft Session-Deduplizierung, nicht eine beobachtete Datenvermischung. |

## Einordnung der Assembly-Graphnutzbarkeit

`dependency_graph` ist für Assembly-Ziele semantisch nutzbar, sofern ein Agent
die Ausgabe als dekompilierten, partiellen Graphen behandelt. Die positive
Gegenprobe über beide installed vendor assemblies liefert Kanten und korrekt
markierte Resultatslices. Der leere Root-Scope des repo-provided assembly ist
dagegen lediglich eine scoped Leermenge unter sichtbarer Diagnostik, kein Beleg
für fehlende Abhängigkeiten außerhalb der geöffneten dekompilierten Session.

Robuste Agentenfolge: kleinen Typ-Scope wählen, `origin`, Hash, Generation,
`status`, `completeness`, Diagnostics und `truncated` vor der Interpretation
auswerten; nach Targetwechsel keinen vom Text abgeleiteten relativen
Dekompilationspfad wiederverwenden.

## Kein Befund / Grenzen

- Root-Default und explizites `both` liefern im geprüften Assembly-Scope
  konsistente Richtungsergebnisse.
- `depth` und `maxResults` wirken in der positiven Assemblyprobe; sichtbare
  Kürzung ist strukturiert markiert.
- Absolute dekompilierte `File_1`-Identifier, Projektgraph, Symbolimpact,
  Testkontext sowie die C#-Anreicherung der Textsuche sind im kleinen Scope
  nutzbar.
- Verzeichnis-, Endungs-, relativer Ziel- sowie leere/widersprüchliche
  Identifier-Eingaben wurden nicht ausgeführt oder dereferenziert, sondern
  abgewiesen; es wurde keine Datei angelegt, verändert oder gelöscht.
- Ein traversal-artiger Pfad wurde auf das kanonische Ziel zurückgeführt. Für
  die opaque/reparse-ähnliche Schreibweise wurde keine unerlaubte Zielöffnung
  beobachtet; lediglich eine getrennte Sessiongeneration, siehe CT-SES-001.

### Commit-Vorschlag

docs: dokumentiere Cross-Target-Fehlerproben
