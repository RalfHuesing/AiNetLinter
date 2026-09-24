# AiNetLinter-MCP-Nutzungs-Audit – SqlToAi

**Ziel:** `C:/Daten/Entwicklung/Ralf/SqlToAi/SqlToAi.slnx`
**Audit:** 2026-09-24, read-only; kein Build, keine Tests, keine Änderungen am Repository.
**MCP:** `ainetlinter`, absolute `targetPath`-Werte.

## Kurzbefund

- `verify(scope="solution")` lieferte `verdict=pass`, `score=10.0`, `violationCount=0`, `completeness=complete`. Dead-Code-Advisories beeinflussten das Gate-Verdict nicht.
- Der Scan meldete **92 Kandidaten** (40 `test_only`, 52 `unreferenced`, 0 API-geschützt, 0 unentscheidbar), zeigte aber nur 13; 79 wurden abgeschnitten. Die 13 sichtbaren Fälle sind deshalb keine Gesamtliste.
- Gegenproben bestätigten eine klare Nutzungsgrenze: `ITokenVault.Store` wird nur in Tests aufgerufen, während die Produktionsimplementierung für Token-Erzeugung `GetOrAddToken` aufruft. Das ist ein plausibles `test_only`-Advisory.
- Der Kandidat `DetailSchemaRenderer.ConstraintRow.ConstraintType` hat keine C#-Referenzen. Der Mapper-Datensatz wird per Dapper befüllt, aber die Property wird beim Rendern nicht verwendet; das stützt den Dead-Code-Hinweis.
- Der direkte Handoff aus `verify` für mehrere Konstruktor-Kandidaten schlägt anschließend mit `SYMBOL_NOT_FOUND` fehl. Die jeweils neu per `find_symbol`/Skeleton gefundenen Klassen lassen sich dagegen navigieren und zeigen Produktionsreferenzen. Diese Konstruktor-Advisories konnten daher nicht exakt über die ausgegebenen Handles verifiziert werden.
- Für diese Solution gilt in `ainetlinter-rules.json` `DeadCode.DefaultApiSurface="closed_solution"`. Die expliziten Alternativen `external_library` und ungültiges `unknown` wurden ohne Konfigurationsänderung nicht zur Laufzeit ausprobiert.

## Reproduzierbare Aufrufe und Evidenz

Alle Aufrufe unten verwenden `targetPath="C:/Daten/Entwicklung/Ralf/SqlToAi/SqlToAi.slnx"`.

### 1. Solution-Gate und Dead-Code-Advisories

```text
verify({targetPath: <absoluter SqlToAi.slnx-Pfad>, scope: "solution"})
```

Antwortkern:

```text
verdict: pass
completeness: complete
score: 10.0
violationCount: 0
scope: solution
evidence: returned=13/94; truncation=evidence_limit, response_budget
deadCode: status=complete; candidates=92; testOnly=40; unreferenced=52;
          apiProtected=0; undecidable=0; shown=13; truncatedBy=79; next=review_now
```

Die Lint-Evidenz nennt 94 Gesamtbelege, von denen 13 sichtbar sind. Für Dead Code ist die Zählung separat angegeben: 92 Kandidaten, 13 gezeigte, 79 abgeschnittene. `next=review_now` und der Advisory-Text fordern eine manuelle Gegenprüfung. `confidence=low` sowie `countercheck=reflection,DI,generators,dynamic,markup/config,external_consumers` grenzen die statische Aussage sichtbar ein. Kandidaten stehen neben dem Gate, ändern aber dessen `pass`-Ergebnis hier nicht.

Die vollständige serialisierte MCP-Antwort dieses `verify`-Aufrufs maß **4.013 UTF-8-Bytes** (JSON-serialisiertes Toolresultat in der lokalen Messung; Transport-Envelope nicht enthalten). Laufzeit: ca. **3,8 s**. Eine Tokenzahl wäre nur eine grobe Schätzung; 4 KB entsprechen je nach Text und Tokenizer ungefähr 1.000–1.500 Tokens.

### 2. Kandidaten mit direkt übergebenen `symbolIdentifier`-Werten

Aus `verify` direkt an `find_references` übergeben:

```text
find_references({targetPath: <absoluter Pfad>, symbolIdentifier: "h:ciL2", scopeType: "all"})
find_references({targetPath: <absoluter Pfad>, symbolIdentifier: "h:ciL3", scopeType: "all"})
find_references({targetPath: <absoluter Pfad>, symbolIdentifier: "h:ciL5", scopeType: "all"})
find_references({targetPath: <absoluter Pfad>, symbolIdentifier: "h:ciL6", scopeType: "all"})
find_references({targetPath: <absoluter Pfad>, symbolIdentifier: "h:ciLB", scopeType: "all"})
find_references({targetPath: <absoluter Pfad>, symbolIdentifier: "h:ciLC", scopeType: "all"})
find_references({targetPath: <absoluter Pfad>, symbolIdentifier: "h:ciLD", scopeType: "all"})
```

Beobachtungen:

- `h:ciL2` (`AnonymizationMode.Scramble`) ergab „Keine Aufrufstellen gefunden“. Die ergänzende Textsuche fand die Deklaration `AnonymizationMode.cs:13`, aber keine Verwendung dieses Konstantennamens; Konfigurationswerte und Doku verwenden `ScramblePattern`. Das ist **bestätigt innerhalb der Solution**, mit der Grenze, dass externe Verbraucher unter der aktuellen `closed_solution`-Policy nicht erfasst werden.
- `h:ciL6` wurde als `SqlToAi.Anonymization.ITokenVault.Store(string,string)` aufgelöst. `find_references` fand 15 Stellen, alle in Tests. `get_feature_context` bestätigte 15 Referenzen und 10/15 zurückgegebene Call-Sites (Rest durch `maxCallers` abgeschnitten; Composite `truncated`). `Anonymizer.Tokenize` ruft laut Body `ITokenVault.GetOrAddToken` auf; es ruft `Store` nicht auf. **`test_only` ist für diese Methode plausibel bestätigt**, nicht gleichbedeutend mit „keine Referenzen“.
- `h:ciLB` (`DetailSchemaRenderer.ConstraintRow.ConstraintType`) ergab keine Aufrufstellen. Neue Handoffs über `find_symbol` lieferten Property `h:cjgg`; `find_references` und `get_feature_context` bestätigten 0 statische Referenzen. `get_symbol_body` für `GetSchemaConstraintsAsync` zeigt Dapper `QueryAsync<ConstraintRow>` und SQL-Aliase `ConstraintType`, der Renderer schreibt anschließend `"DEFAULT"` bzw. `"CHECK"` als Literal und liest `dc.ConstraintType`/`cc.ConstraintType` nicht. **Bestätigter ungenutzter Property-Wert**; der Mapper setzt ihn vermutlich reflektiv, aber diese Befüllung ist keine Nutzung der Property im Produktfluss.
- `h:ciLC` (`IIndexSuggestionService.SuggestIndexesAsync`) zeigte vier Referenzen in `IndexSuggestionServiceIntegrationTests.cs` und keine sichtbaren Produktionsstellen. Der Interface-Overload ist ein öffentliches Vertragsmitglied; die Implementierungsklasse ist produktiv. Ohne externe Consumer ist dieser konkrete Overload **unklar**: test-only ist statisch nachvollziehbar, API-Nutzbarkeit hängt von der `closed_solution`-Grenze ab.
- `h:ciL3`, `h:ciL4`, `h:ciL5` und `h:ciLD` endeten bei `find_references` mit `SYMBOL_NOT_FOUND`. Der Fehlertext zeigt eine abgeschnittene Dokumentations-ID des jeweiligen Konstruktors (`...#ctor(...)`). Dasselbe passierte mit neu erzeugten Konstruktor-Handoffs aus `get_file_skeleton` (`h:cjg8`, `h:cjgn`, `h:cjfX`). Damit ist der Handoff für diese Konstruktoren reproduzierbar fehlerhaft. Das ist ein MCP-Navigationsfehler, kein Beleg für fehlende Referenzen.

### 3. Abgleich der Konstruktor-Advisories mit navigierbaren Typen

```text
find_symbol({targetPath: <absoluter Pfad>, namePatterns: ["Anonymizer", "AnonymizationPolicyResolver", "IndexSuggestionService", "AppSettingsMigrator"], kind: "class", scopeType: "all"})
find_references({targetPath: <absoluter Pfad>, symbolIdentifier: "h:ciCi", scopeType: "all"})
find_references({targetPath: <absoluter Pfad>, symbolIdentifier: "h:ciCg", scopeType: "all"})
find_references({targetPath: <absoluter Pfad>, symbolIdentifier: "h:ciCk", scopeType: "all"})
find_references({targetPath: <absoluter Pfad>, symbolIdentifier: "h:cje5", scopeType: "all"})
```

Die Typ-Handoffs funktionieren. `AnonymizationPolicyResolver` wird in `Program.cs:200` instanziiert; `Anonymizer` wird über mehrere Produktdateien und `Program.cs:198` referenziert; `IndexSuggestionService` wird in `Program.cs:189` referenziert. Das widerlegt keine Konstruktor-Kandidaten auf Symbol-Ebene, belegt aber, dass Typen/Produktfunktionen produktiv eingebunden sind. Exakte Konstruktor-Call-Sites bleiben wegen des Handoff-Fehlers offen. **Schweregrad mittel** für Advisory-Verlässlichkeit/Navigierbarkeit, da ein Reviewer einen Kandidaten über das vorgesehene Handle nicht reproduzierbar prüfen kann.

`AppSettingsMigrator`-Typreferenzen umfassen `Program.cs:28` und mehrere Aufrufe in Tests. Die zwei Advisory-Zeilen bei `AppSettingsMigrator.cs:20–21` wurden nicht über eigene Symbol-Handoffs geprüft und bleiben in dieser Stichprobe unklassifiziert.

### 4. Policy, Solution-Umfang, Fehler-/Ausgabequalität

```text
search_pattern({targetPath: <absoluter Pfad>, pattern: "apiSurface|ApiSurface|closed_solution|external_library|DeadCode", isRegex: true, includePatterns: ["**/ainetlinter-rules.json", "**/*.json", "**/*.csproj"]})
get_index_scope({targetPath: <absoluter Pfad>})
get_server_health({targetPath: <absoluter Pfad>})
```

- `search_pattern` fand `ainetlinter-rules.json:367–368`: `"DeadCode": { "DefaultApiSurface": "closed_solution" }`. `verify` lief regulär und meldete `apiProtected=0`; dies bestätigt den Default-Verwendungspfad praktisch.
- .NET-Solution-Scope: 189 physische Dateien, 180 C#-Dateien, 2 Projekte, 89 Dokumente in Testprojekten und 11 generierte Dokumente. Die Index-Aufschlüsselung enthält keine `.razor`-Dateien; bei dieser Nicht-Blazor-Solution war keine Markup-Gegenprüfung erforderlich. JSON-Konfiguration wurde im obigen Suchaufruf gezielt einbezogen.
- Health meldete `LoadState=Loaded`, `projects=2`, `documents=189`, `lint=configured`; keine Health-Fehler.
- Die Muster-Suche `Scramble|ConstraintType|DetailSchemaRenderer` lieferte **51 Treffer**, zeigte 38 und zugleich `Antwort wegen maxResponseBytes begrenzt` sowie `[NEXT: refine_scope]`. Das ist klar gekennzeichnet, erfordert bei breiter Anfrage aber einen weiteren engeren Aufruf. In diesem Fall waren der SQL-Quellkontext und die Property durch gezielte Bodies dennoch erreichbar.
- Die Ausgaben markieren fehlende Symbole eindeutig als `[ERROR]: SYMBOL_NOT_FOUND` mit `operation=error`; die Verwechslungsgefahr entsteht hauptsächlich daraus, dass `verify` Kandidaten mit Handoffs ausgibt, die bei Konstruktoren dann nicht auflösbar sind.

## API-Policy: praktische Prüfbarkeit und Grenzen

Im aktiven Ziel ist `closed_solution` explizit gesetzt, und `verify` liefert ohne Policy-Preflight-Abbruch Kandidaten mit `apiProtected=0`. Das zeigt, dass der Default-/aktuelle Pfad benutzbar ist. Gemäß Vorgabe im Projektworkflow sind `closed_solution` und `external_library` gültig; explizites `unknown` soll ungültig sein. Das Audit hat `external_library` und `unknown` nicht durch eine temporäre Konfigurationsänderung ausprobiert, weil ausschließlich Berichte geschrieben werden durften. Somit sind die Validierungsreaktion und die Forderung „unknown nicht still migrieren“ für diese Solution hier **nicht zur Laufzeit belegt**. Die bestehende Konfiguration enthält kein `unknown`.

## Schweregrad und Auswirkungen

| Schweregrad | Finding | Auswirkung |
|---|---|---|
| Mittel | Konstruktor-Handoffs aus `verify` schlagen reproduzierbar mit `SYMBOL_NOT_FOUND` fehl. | Die vorgegebene Kandidatenprüfung kann exakte Call-Sites nicht bequem weiterverfolgen; Reviewer müssen per Typ-/Symbolsuche ausweichen. |
| Mittel | Große `verify`-Ausgabe ist trotz klarer Counter nur eine Teilstichprobe: 13/92 Advisory-Kandidaten sichtbar. | Manuelle Gegenprüfung ist ohne weitere MCP-Aufrufe unvollständig; die Antwort signalisiert dies immerhin mit `truncatedBy=79`/`next=review_now`. |
| Niedrig | Breite `search_pattern`-Antwort erreicht Byte-Budget und schneidet Treffer ab. | Erfordert Scope-Verfeinerung; die Ausgabe meldet den Folgeschritt eindeutig. |
| Niedrig | API-Policy `external_library`/`unknown` wurde nicht aktiv getestet. | Aussagen zur Default-Policy sind belegt, Aussagen zum Fehlerpfad/Override-Verhalten bleiben offen. |

## Grenzen

Read-only-Audit ohne Builds oder Tests. Semantische Referenzen erfassen keine beliebigen externen Consumers, dynamische Aufrufe, Reflection oder alle Laufzeitbindungen. `closed_solution` bedeutet, dass solche externen Nutzungen nicht durch den getesteten Scope abgesichert sind. Die Kandidatenbewertung bezieht sich auf die sichtbare Stichprobe plus ausgewählte Folgeaufrufe, nicht auf alle 92 Advisories. Die tatsächlich transportierte Bytezahl kann vom hier gemessenen serialisierten JSON-Toolresultat abweichen; Tokenäquivalente sind grobe Näherungen.
