# MCP-Nutzungs-Audit – SAN-Plattform (Blazor, ca. 180k LOC)

**Datum:** 2026-09-24
**Ziel:** `C:\Daten\Entwicklung\SAN\San.smart.Planner.Platform\San.smart.Planner.Platform.slnx`
**MCP-Server:** `ainetlinter`; ausschließlich lesende Aufrufe. Keine Builds oder Tests.

## Ergebnis

**Schweregrad: mittel** – Das Solution-Gate lief erfolgreich durch und lieferte Dead-Code-Advisories. Der Audit fand aber eine konkrete Lücke bei Razor-Bindungen: Eine Methode, die in `.razor` über `OnClick` verwendet wird, hatte laut `find_references` keine Aufrufstellen. Das passt zu den 111 nicht im Symbolgraph abgedeckten Razor-Dateien aus `get_index_scope`; Dead-Code-Advisories sollten daher bei Blazor nicht allein anhand der C#-Referenzliste bewertet werden.

## Reproduzierbare Aufrufe und Evidenz

Alle Aufrufe nutzten den absoluten Solution-Pfad oben.

| Zweck | Aufrufparameter | Zeit | Antwortgröße |
|---|---|---:|---:|
| Health | `get_server_health({targetPath})` | 251 ms (Cache warm) | 811 UTF-8-Bytes |
| Gate und Advisories | `verify({targetPath, scope:"solution"})` | 138.857 ms | 4.069 UTF-8-Bytes |
| Indexabdeckung | `get_index_scope({targetPath})` | 564 ms | 2.625 UTF-8-Bytes |
| Solution-Übersicht | `get_namespace_tree({targetPath, depth:1, includeTypes:false, maxResults:30, maxResponseBytes:12000})` | 144 ms | 1.459 UTF-8-Bytes |
| Referenzen | `find_references({targetPath, symbolIdentifier:"h:ciU1", scopeType:"all", maxResults:30})` | 671 ms | 190 UTF-8-Bytes |
| Referenzen | `find_references({targetPath, symbolIdentifier:"h:ciXV", scopeType:"all", maxResults:30})` | 637 ms | 545 UTF-8-Bytes |
| Referenzen | `find_references({targetPath, symbolIdentifier:"h:ciYd", scopeType:"all", maxResults:30})` | 671 ms | 209 UTF-8-Bytes |
| Razor-Suche | `search_pattern({targetPath, pattern:"OpenDialog|DialogLayoutTestPage", isRegex:true, scopeType:"all", includePatterns:["**/*.razor"], maxResults:20, contextLines:3, enrichCSharp:true})` | 300 ms | 502 UTF-8-Bytes |

Antwortgrößen sind die UTF-8-Bytezahl von `JSON.stringify(toolResult)`, also des serialisierten MCP-Ergebnisobjekts mit Envelope. Tokenzahlen wurden nicht gemessen.

`get_server_health` meldete `Loaded`, 15 Projekte und 2.445 Roslyn-Dokumente. Der Indexbericht zählte 3.129 physische Dateien: 2.367 `.cs`, 111 `.razor`, 141 `.json` und weitere Dateien. 90 Dokumente sind generiert, 1.103 gehören zu Testprojekten. Die 111 Razor-Dateien sind laut Index nicht vom Symbolgraph abgedeckt.

Die Solution-Übersicht listete 15 Projekte, darunter das Blazor-Executable `San.smart.Planner.Platform` mit 1.007 Typen und mehrere Testprojekte. Die MCP-Ausgaben ließen sich nach dem ersten Indexaufbau schnell abfragen. Der erste parallele Aufrufblock einschließlich Solution-Verify dauerte rund 141 s; wegen eines Messskriptfehlers wurden seine Resultate nicht gespeichert. Danach wurde genau ein verwertbarer Verify-Lauf verwendet; dieser dauerte 138,9 s. Das ist eine relevante Wartezeit für eine Routineabfrage auf dieser Solution.

## Verify, Advisories und Gate

Der verwertbare Lauf gab aus:

- `verdict: pass`, `completeness: complete`, `score: 10.0`, `violationCount: 0`, `scope: solution`.
- `evidence=returned:11/770`; `truncation=evidence_limit, response_budget`.
- Dead Code: `status=complete`, 714 Kandidaten insgesamt, davon 403 `test_only`, 311 `unreferenced`, 0 `apiProtected`, 0 `undecidable`.
- Sichtbar waren 11 Kandidaten; `shown=11`, `truncatedBy=703`, `next=review_now`. `advisories: count=11; completeness=complete; review_required; static_evidence`.

Die Ausgabe trennt das Gate-Verdict von den Advisories: Trotz 714 Kandidaten blieb das Gate bei `pass`, Score 10.0 und null Verstößen. `next=review_now` priorisiert die Kandidaten zur manuellen Prüfung nach den Gate-Verstößen. `completeness=complete` beschreibt den Lauf/Inventarstatus, während die Kandidatenliste selbst sichtbar auf 11 von 714 begrenzt ist. Die Zahlen und Trunkierung sind explizit; eine im Alltag relevante Grenze bleibt, dass bei 11 gezeigten Kandidaten und 703 nicht gezeigten die Auswahlreihenfolge die Sicht auf den Rest stark prägt.

Die sichtbare Reihenfolge begann mit einem `unreferenced`-Feld, danach folgten `test_only`-Kandidaten. Kandidaten tragen `confidence=high`, aber zugleich den Warnhinweis „Fehlalarm möglich“ und Countercheck-Kategorien für Reflection, DI, Generatoren, `dynamic`, Markup/Konfiguration und externe Consumer. Die hohe Confidence ist daher kein Löschbeweis.

### Kandidaten-Gegenprüfungen

1. **Unreferenced – unklar, mit statischer Evidenz für fehlende Verwendung.**
   Kandidat `h:ciU1`: `San.smart.Planner.Platform.Domain.XrmTermine.XrmTermineSendTerminanfrageCommandHandler._persistence`, Datei `San.smart.Planner.Platform.Domain.XrmTermine/Commands/XrmTermineSendTerminanfrageCommandHandler.cs:50`.
   `find_references` gab „Keine Aufrufstellen gefunden“ für das Feld aus. `get_symbol_body({targetPath, symbolIdentifiers:["h:ciU1"], maxBodyLines:35})` zeigte nur `_persistence = persistence`. Das stützt „Feld wird nur beschrieben“ im statischen C#-Graph. Nicht geprüft wurden Reflection, dynamische Zugriffe, Konfiguration oder Generatoren; daher kein bestätigter Löschbefund.

2. **Test-only – als fehlende produktive C#-Referenz plausibel, als unbenutztes Feld widerlegt.**
   Kandidat `h:ciXV`: `DataTableUserConfigLayoutTestPageBase._configBusAttachmentGeneration`.
   `find_references` lieferte zwei Feldzugriffe bei Zeile 88 und 95 in derselben Test-Layout-Klasse. `get_symbol_body` lieferte den symbolischen Feldinhalt, der im Textformat allein keinen Nutzungsnachweis bildet; die beiden Referenzstellen liefern diesen. Die Razor-Suche fand außerdem `DataTableUserConfigFilterLayoutTestPage.razor:4: @inherits DataTableUserConfigLayoutTestPageBase`. Die Einstufung `test_only` beschreibt fehlende produktive Referenzen; sie bedeutet nicht, dass das Feld insgesamt ungenutzt ist. Ein externer produktiver Markup-/Konfigurationsnutzer wurde nicht belegt.

3. **Test-only – Markup-Nutzung durch MCP-Referenzsuche übersehen.**
   Kandidat `h:ciYd`: `DialogLayoutTestPage.OpenDialog()`; Verify meldete `test_only` mit zwei Testreferenzen. `find_references` gab keine C#-Aufrufstellen aus und ergänzte `next: includeGenerated=true`. Der direkte Markup-Check fand `DialogLayoutTestPage.razor:14` mit `OnClick="OpenDialog"`. `get_symbol_body` bestätigte eine Methode, die den Testdialog öffnet. **Bestätigter Markup-Aufruf / erwartbarer Test-only-Fall**, aber **Fehlalarm**, wenn die fehlende C#-Referenz als fehlender tatsächlicher Aufruf interpretiert würde. Das Razor-Mapping wird in `find_references` nicht sichtbar; die zusätzliche Markup-Prüfung war nötig.

Diese Stichprobe bestätigt nicht die Trefferqualität aller 714 Kandidaten. Sie zeigt einen statischen Unreferenced-Fall und einen klaren Razor-Aufruf, den eine reine C#-Referenzabfrage nicht abbildete. Bei den restlichen 711 sichtbaren/unsichtbaren Fällen ist keine Gegenprüfung erfolgt.

## API-Policy

Die Konfigurationssuche

`search_pattern({targetPath, pattern:"\"DeadCode\"|\"DefaultApiSurface\"|\"ApiSurface\"|\"unknown\"", isRegex:true, scopeType:"all", includePatterns:["**/ainetlinter-rules.json"], maxResults:30, contextLines:3})`

fand in `ainetlinter-rules.json` den Eintrag `"DeadCode": { "DefaultApiSurface": "closed_solution" }`. Der Verify-Preflight stoppte nicht; die Solution wurde vollständig analysiert. `apiProtected=0` ist das ausgegebene Ergebnis für diesen Lauf.

Der in AGENTS.md/MCP-Workflow festgelegte Vertrag ist praktisch klar: fehlender Wert verwendet `closed_solution`; gültige explizite Werte sind `closed_solution` und `external_library`; ein ausdrücklich gesetztes `unknown` ist ungültig und soll `DEAD_CODE_API_SURFACE_NOT_CONFIGURED` auslösen. Der aktuelle SAN-Lauf belegt die Nutzung des Standardwerts. Der ungültige explizite Wert wurde im Read-only-Audit nicht injiziert: `verify` nimmt keinen Policy-Override als Toolparameter an und das Ändern der Solution-Konfiguration wäre außerhalb des erlaubten Umfangs. Die Invalid-Policy-Fehlerbehandlung ist daher hier dokumentiert, aber nicht dynamisch verifiziert.

## Tool-Ergonomie und Grenzen

- **Schweregrad mittel:** `find_references(h:ciYd)` gab keine Aufrufstellen aus, obwohl die Razor-Datei eine `OnClick`-Bindung auf `OpenDialog` enthält. Die Referenzantwort markierte die mögliche Fortsetzung nur als `next: includeGenerated=true`; sie signalisierte nicht explizit, dass Razor-Markup außerhalb des C#-Graphen liegt. `get_index_scope` macht diese Abdeckungslücke zwar sichtbar, doch sie muss dem Nutzer bekannt sein.
- **Schweregrad niedrig:** `search_pattern` lieferte für Markup-Treffer `handoff: not_applicable` statt einer navigierbaren Handoff-ID. Für die genaue Zeile half die Textausgabe, aber der Übergang in Symboltools musste anderweitig erfolgen. Die DataTable-Suche gab dieselbe `.razor`-Zeile zweimal aus. Das ist ein kleines Redundanz-/Handoff-Problem.
- **Schweregrad niedrig:** Verify lieferte trotz `completeness=complete` nur 11 der 714 Advisory-Kandidaten. Die explizite Zähler- und Trunkierungsangabe ist gut; die stark begrenzte Kandidatenansicht erfordert jedoch Folgeaufrufe bzw. eine gezielte Auswahl, bevor ein Audit viele Fälle repräsentativ beurteilen kann.
- **Schweregrad mittel für SAN-Laufzeit:** Der Solution-Verify brauchte 138,9 s. Danach waren semantische Referenz- und Markup-Suchen in 0,6–2,5 s fertig. Für begrenzte Stichproben ist der Server damit gut nutzbar, ein wiederholter Voll-Verify zum Explorieren wäre auf dieser Solution teuer. Es wurden keine weiteren Voll-Verify-Aufrufe ausgeführt.

## Grenzen

Es erfolgten keine Builds, Tests, Code-/Konfigurationsänderungen oder Repository-Schreibvorgänge. Die Prüfung umfasste genau drei vom Verify ausgegebene Kandidaten; besonders der `unreferenced`-Fall bleibt wegen möglicher dynamischer/konfigurationsbasierter Nutzung unklar. Die Wirkung eines expliziten `unknown`-Werts wurde nicht mutierend getestet. Tokenzahlen wurden nicht gemessen; Antwortgrößen sind tatsächliche UTF-8-Bytes des serialisierten MCP-Resultats, keine Tokenzählung.
