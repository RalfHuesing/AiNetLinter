# MCP-Nutzungs-Audit – AiNetLinter.slnx

**Datum:** 2026-09-24
**Umfang:** Read-only-Audit des AiNetLinter-MCP-Servers für `C:\\Daten\\Entwicklung\\Ralf\\AiNetLinter\\AiNetLinter.slnx`. Es wurden keine Builds oder Tests ausgeführt und keine Repository-Dateien geändert. Der Bericht ist die einzige erzeugte Datei.

## Kurzbefund

- `verify(scope: "solution")` war erfolgreich: `verdict=pass`, `completeness=complete`, Score 10.0, 0 Lint-Verstöße.
- Dieselbe Antwort meldete 467 Dead-Code-Kandidaten (151 `test_only`, 316 `unreferenced`), aber nur 13 sichtbare Kandidaten; 454 waren abgeschnitten. `deadCode.next=review_now`. Das Gate-Verdict blieb trotz der Advisories auf `pass`.
- Der aktuell geladene Policy-Wert ist `closed_solution`; die MCP-Ausgabe meldete `apiProtected=0`. Quelltext belegt, dass nur `closed_solution` und `external_library` gelten und `unknown` erhalten und als ungültig erkannt wird. Den Fehlerpfad habe ich nicht durch Konfigurationsänderung ausgeführt.
- Direkte Referenzprüfungen bestätigten einen `test_only`- und einen statisch unreferenzierten privaten Kandidaten. Sie belegen keine Abwesenheit dynamischer oder externer Nutzung.
- Die erfolgreichen Handoffs von Verify-ID → Referenzsuche → Teststellen-Body funktionierten. Einzelne zusammengesetzte Ausgaben markieren Trunkierung, erklären die aggregierten Evidenzzahlen aber nicht vollständig.

## Reproduzierbare Aufrufe und Evidenz

Alle Aufrufe nutzten den absoluten Zielpfad:

`C:\\Daten\\Entwicklung\\Ralf\\AiNetLinter\\AiNetLinter.slnx`

### 1. Solution-Gate und Bestandsaufnahme

Aufruf:

```json
{"targetPath":"C:\\Daten\\Entwicklung\\Ralf\\AiNetLinter\\AiNetLinter.slnx","scope":"solution"}
```

Antwortkern (wortgetreu):

```text
verdict: pass
completeness: complete
score: 10.0
violationCount: 0
scope: solution
evidence: returned=13/492; truncation=evidence_limit, response_budget
deadCode: status=complete; candidates=467; testOnly=151; unreferenced=316; apiProtected=0; undecidable=0; shown=13; truncatedBy=454; next=review_now
advisories: count=13; completeness=complete; review_required; static_evidence
```

Die gemessene Größe des JSON-serialisierten MCP-Ergebnisobjekts betrug **4.059 UTF-8-Bytes** (das ist eine Byte-Messung, keine Tokenmessung). Eine grobe Tokenumrechnung wäre nur eine Näherung; die Antwort enthält strukturierte Felder und wiederholte Pfade.

**Schweregrad: mittel – Trunkierung und Gate-Verhältnis.** Die 13 angezeigten Advisories sind nicht die vollständige Kandidatenliste. 454 von 467 Kandidaten bleiben in dieser Antwort unsichtbar. `completeness=complete` beschreibt hier die Ausführung/Antwort des Verify-Aufrufs, während Dead-Code selbst mit `shown=13` und `truncatedBy=454` klar begrenzt ist. Die getrennten Werte `evidence=returned=13/492` und `advisories count=13` erklären nicht, wie sich 492 Evidenzeinträge zu 467 Dead-Code-Kandidaten verhalten. Das erschwert die schnelle Auswertung. Advisories beeinflussen den Gate-Verdict in diesem Lauf nicht: trotz 467 Kandidaten blieb das Gate `pass`. `next=review_now` ordnet ihre manuelle Prüfung nach dem Gate ein.

**Auswirkung:** Ein Nutzer kann aus dem grünen Gate allein nicht schließen, dass keine ungenutzten Symbole vorliegen. Zugleich ist die Aufteilung nach Typ und die Zahl der ausgelassenen Einträge nützlich. Für vollständige Gegenprüfungen braucht es einen weiteren Weg, um nicht angezeigte Kandidaten einzeln abzurufen; diese Audit-Runde untersuchte deshalb nur ausgegebene IDs.

Ergänzende Aufrufe:

```json
{"targetPath":"C:\\Daten\\Entwicklung\\Ralf\\AiNetLinter\\AiNetLinter.slnx"}
```

für `get_index_scope` ergab 920 physische `.cs`-Dateien und 938 Roslyn-Dokumente (darunter 23 generierte und 425 Dokumente in Testprojekten). `get_server_health` meldete `LoadState=Loaded`, 4 Projekte, 938 Dokumente und `lint=configured`. Damit war die Solution laut Serverstatus geladen; ein eigener Staleness- oder Refresh-Test fand nicht statt.

### 2. Kandidaten gegenprüfen

**A. Bestätigtes statisches `test_only`: `h:ciD2`**

Direkte Folgeaufrufe:

```json
{"targetPath":"C:\\Daten\\Entwicklung\\Ralf\\AiNetLinter\\AiNetLinter.slnx","symbolIdentifier":"h:ciD2","scopeType":"all","maxResults":50}
{"targetPath":"C:\\Daten\\Entwicklung\\Ralf\\AiNetLinter\\AiNetLinter.slnx","symbolIdentifier":"h:ciD2","scopeType":"production"}
{"targetPath":"C:\\Daten\\Entwicklung\\Ralf\\AiNetLinter\\AiNetLinter.slnx","symbolIdentifier":"h:ciD2","scopeType":"tests"}
{"targetPath":"C:\\Daten\\Entwicklung\\Ralf\\AiNetLinter\\AiNetLinter.slnx","symbolIdentifier":"h:ciD2","maxCallers":5,"maxTests":5,"maxResponseBytes":8192}
```

Verify bezeichnete den Kandidaten als `test_only`, `confidence=high`, `testReferences=3`. `find_references(all)` lieferte drei Treffer in zwei Tests; `find_references(production)` lieferte keine; `find_references(tests)` lieferte dieselben drei Stellen. Der Body zeigt die explizite Interface-Property `IAssemblyAnalysisRegistry.ResidentCount`. Der Test-Body-Handoff `h:ciKo` aus dem Referenztreffer ließ sich direkt an `get_symbol_body` übergeben und zeigte den Testzugriff `composition.Sessions.ResidentCount`.

**Einordnung:** Als statisch nur von Tests referenziert bestätigt. Das ist kein Löschbeweis; die Tests verwenden die Property bewusst.

**B. Bestätigtes statisch unreferenziertes privates Symbol: `h:ciIN`**

Aufrufe:

```json
{"targetPath":"C:\\Daten\\Entwicklung\\Ralf\\AiNetLinter\\AiNetLinter.slnx","symbolIdentifier":"h:ciIN","scopeType":"all","maxResults":50}
{"targetPath":"C:\\Daten\\Entwicklung\\Ralf\\AiNetLinter\\AiNetLinter.slnx","symbolIdentifier":"h:ciIN","maxCallers":5,"maxTests":5,"maxResponseBytes":8192}
{"targetPath":"C:\\Daten\\Entwicklung\\Ralf\\AiNetLinter\\AiNetLinter.slnx","symbolIdentifiers":["h:ciIN"],"maxBodyLines":30}
```

Verify gab `unreferenced`, `confidence=high`, `reason=no_production_static_reference` aus. `find_references` meldete keine Aufrufstellen. `get_feature_context` identifizierte eine private statische Methode `DetermineImpactStatus(int)`; der Body ist eine vierzeilige expression-bodied Methode, und der zusammengesetzte Kontext fand keine statischen Referenzen.

**Einordnung:** Statisch unreferenziert bestätigt. Laufzeit-/Reflection-Aufrufe, Generatoren, dynamische Bindungen und externe Verbraucher sind mit diesen Abfragen nicht ausgeschlossen.

**C. Bestätigtes `test_only`, aber im Composite-Antwortteil gekürzt: `h:ciCP`**

Aufrufe:

```json
{"targetPath":"C:\\Daten\\Entwicklung\\Ralf\\AiNetLinter\\AiNetLinter.slnx","symbolIdentifier":"h:ciCP","scopeType":"all","maxResults":50}
{"targetPath":"C:\\Daten\\Entwicklung\\Ralf\\AiNetLinter\\AiNetLinter.slnx","symbolIdentifier":"h:ciCP","maxCallers":5,"maxTests":5,"maxResponseBytes":8192}
```

Verify meldete `test_only`, `confidence=low`, `testReferences=9`. Die dedizierte Referenzsuche gab alle neun Teststellen in `AnalysisCacheManagerIsolationTests.cs` aus. Der zusammengesetzte Feature-Kontext zeigte nur 5 von 9 Call-Sites und markierte `Composite-Completeness=truncated`, `TruncatedBy=maxCallers`; sein nächster Schritt nannte `maxCallers` erhöhen und Call-Sites erneut abfragen. Die separate Referenzsuche erledigte denselben Erkenntnisschritt direkt und vollständig.

**Einordnung:** Statisch nur von Tests referenziert bestätigt. Die Beschränkung war sichtbar und der dedizierte MCP-Aufruf vermeidet eine unnötige Wiederholung des großen Composite-Aufrufs.

**Keine verifizierten Fehlalarme in dieser Stichprobe.** Die Prüfung war auf die drei angezeigten Kandidaten beschränkt. Nicht angezeigte 454 Kandidaten sind nicht bewertet. Ein statischer Trefferstatus sagt nichts Abschließendes über Reflection, DI, Generatoren, `dynamic`, Konfigurationsbindung oder externe API-Nutzung aus.

### 3. API-Policy

Aktuelle Konfiguration, gelesen über MCP:

```json
{"targetPath":"C:\\Daten\\Entwicklung\\Ralf\\AiNetLinter\\AiNetLinter.slnx","pattern":"apiSurface|ApiSurface|DeadCode","isRegex":true,"includePatterns":["ainetlinter-rules.json"],"maxResults":50,"contextLines":1,"maxResponseBytes":8192}
```

Ausgabe:

```text
ainetlinter-rules.json:380:   "DeadCode": {
ainetlinter-rules.json:381:     "DefaultApiSurface": "closed_solution"
```

Die C#-Quelltextsuche und Semantikabfragen ergaben dazu:

- `DeadCodeConfig.DefaultApiSurface` hat den Initialwert `"closed_solution"`.
- `DeadCodeApiSurfacePolicy.IsKnown` prüft exakt auf `"closed_solution"` oder `"external_library"`.
- `FindUnconfiguredProjects` löst pro Kandidatenprojekt die Konfiguration auf und sammelt Werte, die keiner dieser beiden Zeichenfolgen entsprechen. Der Verify-Scanner ruft diese Validierung auf.
- Die vorhandenen Policy-Tests enthalten explizit `unknown` sowie `Closed_Solution` und prüfen, dass sie erhalten bleiben und `IsKnown=false` ergeben. Sie enthalten außerdem den Default-Test für ausgelassene Policy-Felder.

Reproduzierbare Quelltextaufrufe waren:

```json
{"targetPath":"C:\\Daten\\Entwicklung\\Ralf\\AiNetLinter\\AiNetLinter.slnx","pattern":"DEAD_CODE_API_SURFACE_NOT_CONFIGURED|DefaultApiSurface|closed_solution|external_library","isRegex":true,"includePatterns":["src/**/*.cs"],"maxResults":30,"contextLines":2,"maxResponseBytes":8192}
{"targetPath":"C:\\Daten\\Entwicklung\\Ralf\\AiNetLinter\\AiNetLinter.slnx","filePaths":["src/AiNetLinter/Mcp/Tools/Verify/DeadCode/DeadCodeApiSurfacePolicy.cs","src/AiNetLinter/Configuration/DeadCodeConfig.cs","src/AiNetLinter/Configuration/ProjectConfigResolver.cs"],"maxResponseBytes":8192}
{"targetPath":"C:\\Daten\\Entwicklung\\Ralf\\AiNetLinter\\AiNetLinter.slnx","symbolIdentifiers":["h:cjgR","h:cjgT"],"maxBodyLines":100}
{"targetPath":"C:\\Daten\\Entwicklung\\Ralf\\AiNetLinter\\AiNetLinter.slnx","symbolIdentifier":"h:cjhy"}
```

Die letzte Abfrage öffnete den vollständigen Test `ConfigLoader_DefaultsApiSurfaceAndPreservesInvalidPolicies`; der Verify-Scanner-Handoff `h:cjhz` zeigte den Aufruf von `FindUnconfiguredProjects`.

**Schweregrad: niedrig für die aktuelle Solution-Konfiguration; mittel für die nicht dynamisch ausgeführte Fehlerprobe.** Der Default und die zulässigen Werte sind klar und die Test- und Policy-Implementierung unterstützen die Anforderungen. Weil das Audit ausschließlich lesend sein sollte, wurde die Konfiguration nicht auf `unknown` geändert und Verify nicht mit einer temporär ungültigen Datei ausgeführt. Damit ist der Preflight-Fehlerpfad hier durch Code und Tests belegt, aber nicht in einem MCP-Lauf dieser Solution beobachtet.

### 4. Ausgabegröße, Handoffs und Alltagsergonomie

Gemessen wurde die UTF-8-Größe des JSON-serialisierten MCP-Ergebnisobjekts (nicht der tatsächliche Transport-Frame). Beispiele:

| MCP-Aufruf | Ergebnisobjekt | Beobachtung |
|---|---:|---|
| `verify(solution)` | 4.059 Bytes | 13 Advisories plus verdichtete Kennzahlen, starke Kandidatentrunkierung |
| `get_feature_context(h:ciCP)` | 3.447 Bytes | Kombiniert Deklaration, Metriken, Caller, Testkontext und Violations; Caller-Teil trotzdem auf 5/9 gekürzt |
| `find_references(h:ciCP)` | 1.763 Bytes | Dedizierte Call-Sites, hier vollständige 9 Treffer |
| `get_symbol_body(h:ciIN)` | 520 Bytes | Kompakter Body und direkt nutzbarer Folge-Handoff |
| `get_file_skeleton(policy-Dateien)` | 3.582 Bytes | Drei Dateien in einem Aufruf mit direkten Handoffs |

Die Werte sind reproduzierbare Byte-Messungen des serialisierten Ergebnisses. Tokenzahlen wurden nicht vom Server geliefert; eine Umrechnung aus Bytes wäre nur eine Näherung und wird hier nicht als exakte Tokenzahl ausgegeben.

**Schweregrad: niedrig bis mittel – Composite-Redundanz.** `get_feature_context` spart Folgeaufrufe, bündelt aber Daten, die im konkreten Audit nicht alle gebraucht wurden; die Ausgabe wurde außerdem durch `maxCallers` gekürzt. Für eine einzelne Dead-Code-Frage war `find_references` kleiner und direkter. Die explizite `truncated`-Kennzeichnung und der benannte nächste Schritt sind hilfreiche Ergonomie.

**Handoffs:** Die IDs `h:ciD2`, `h:ciIN`, `h:ciCP`, `h:cjgR`, `h:cjgT` aus vorherigen MCP-Antworten funktionierten unverändert. Auch ein Call-Site-Handoff (`h:ciKo`) funktionierte im Body-Tool. Kein beobachteter falscher oder defekter Handoff.

**Fehlerkennzeichnung und Vollständigkeit:** In dieser Solution traten keine MCP-Fehler auf. Verify nennt zugleich `completeness=complete` und begrenzt die sichtbare Dead-Code-Liste separat; die Kandidatenzähler und `truncatedBy` machen die Begrenzung deutlich, aber die Relation `returned=13/492` zu `candidates=467` bleibt ohne zusätzliche Erläuterung. `get_feature_context` kennzeichnet seine partielle Call-Site-Ausgabe klar.

## Grenzen

- Nur die in Verify sichtbaren 13 Kandidaten standen für Kandidatenstichproben zur Verfügung; drei davon wurden geprüft.
- Referenzsuche ist statisch. Reflection, DI nach Zeichenfolgen, Generatoren, `dynamic`, Markup/Config-Bindungen und externe Verbraucher sind durch diese C#-Solution-Abfragen nicht vollständig abgedeckt.
- Die ungültige Policy wurde nicht auf die reale Konfiguration angewandt. Ihr Verhalten ist hier durch gelesene Implementierung und vorhandene Testquelle nachgewiesen, nicht durch einen frischen Fehlerlauf.
- Die MCP-Bytewerte zählen das serialisierte Ergebnisobjekt, nicht Protokoll-Overhead. Es wurden keine exakten Tokenmessungen behauptet.
