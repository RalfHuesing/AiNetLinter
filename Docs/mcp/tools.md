# MCP-Werkzeugwahl und Verträge

[Einrichtung](mcp-bootstrap.md) · [Serverbetrieb](server.md) · [Dead-Code-Details](dead-code.md) · [Konfiguration](../linter/configuration.md)

## Ziel und Eingaben

`targetPath` ist der absolute, vorhandene Pfad einer `.sln`/`.slnx` (Source) oder verwalteten `.dll`/`.exe` (Assembly). Die Endung bestimmt den Modus. Keine Verzeichnisse, cwd-Suche oder Elternpfad-Fallbacks. Source-Regeln stammen ausschließlich aus `ainetlinter-rules.json` neben der Solution. Ohne Datei bleibt Navigation möglich, regelgebundene Analyse ist `not_configured`. Assembly-Linting ist `unsupported`.

`tools/list` der laufenden Version ist die vollständige Parameterspezifikation. Unbekannte Properties werden abgelehnt. Parameter nicht aus Anzeigenamen ableiten: `find_symbol` nutzt `pattern` oder `namePatterns`, `search_pattern` nutzt `pattern` und `includePatterns`, Body-/Metrik-Batches nutzen `symbolIdentifiers`.

## Antwort auswerten

Jede Toolantwort enthält genau einen nichtleeren Text-Content-Block mit LF-Zeilenenden. Fachliche Ergebnisse, Status, Vollständigkeit, Fehlerhinweise und kopierbare Handoff-IDs stehen darin. Fehler tragen zusätzlich `isError=true`.

| Beobachtung | Folgeentscheidung |
| --- | --- |
| `isError=true`, `[ERROR]` | Code, Feldpfad und Hint auswerten; Eingabe/Umgebung korrigieren. Auch `SYMBOL_NOT_FOUND` ist ein Fehler. |
| `operation=retry`, `isError=false` | Target lädt; kurz warten und identischen Aufruf wiederholen. Kein Trefferergebnis. |
| `operation=running`, `operationToken` | Identischen Aufruf mit Token fortsetzen, bis das Endergebnis vorliegt. |
| Erfolgreich, aber gekürzt/partiell | Sichtbare Teilmenge verwenden; `completeness`, `truncatedBy`, Diagnostics und `next` prüfen. Keine Vollständigkeit unterstellen. |
| Erfolgreich leer | Nur innerhalb des tatsächlich geprüften Scopes keine Treffer. |
| `RESPONSE_BUDGET_TOO_SMALL` | Identischen Request mit `maxResponseBytes=minimumResponseBytes` wiederholen. Gemessen wird sichtbarer Content in UTF-8. |
| `HANDOFF_UNKNOWN` | Symbol anhand sichtbarer Signatur/Fundstelle erneut suchen; neuen Handle übernehmen. |
| `ASSEMBLY_TARGET_UNSUPPORTED` | Source-Ziel oder ein passendes Assembly-Tool wählen. |
| `INVALID_CONTINUATION_TOKEN` | Neuen Scan starten; der alte Snapshot ist nicht mehr verfügbar. |

Ausführung, Antwortvollständigkeit und fachliche Analyseabdeckung sind verschiedene Größen. Ein frischer Snapshot kann partielle Assembly-Diagnostics enthalten. `freshness=stale`, `degraded=true`, `degradedReason=refresh-failed` kennzeichnen einen fehlgeschlagenen Refresh mit älterem Stand.

## Handoffs, Budgets und Fortsetzungen

- Explizit ausgewiesenes `h:…` unverändert übernehmen. Der absolute `targetPath` bleibt gleich; der Handle ersetzt nur die Symboladresse.
- `symbolIdentifier` und `helperSymbol`: Einzelwert. `symbolIdentifiers`: Array, auch für ein Symbol. `get_file_skeleton.filePaths` akzeptiert Dateipfade und Source-Handoffs für das deklarierende Dokument.
- Namen, Doc-IDs und `Datei:Zeile` nur als vom jeweiligen Schema erlaubten Fallback verwenden. `resolve_type_origin`: genau `symbolIdentifier` für einen Typ-Handle oder `typeName` für einen Typnamen.
- Handles gelten während des Daemon-/Hostlaufs; Session-Eviction allein invalidiert sie nicht. Nach Host-Neustart erneut suchen.
- Enge Scopes und kleine Ergebnislisten zuerst; anschließend ausgewählte Bodies/Referenzen laden. Unterstützte Scope-Felder unterscheiden sich je Tool. `maxResults` begrenzt Ausgabe, nicht pauschal Analysezeit.
- Lange Aufrufe: `verify`, neuer `get_verify_advisories`-Scan, `find_duplicates`, `pattern_detect`, `search_assembly`, `inspect_assembly`, `find_assembly_extensions`, `get_assembly_context`. Jeder Abruf wartet höchstens 15 Sekunden. Mit denselben fachlichen Argumenten und `operationToken` weiter abrufen; 30 Minuten ohne Abruf lassen die Operation verfallen.
- `continuationToken` ist erst nach dem Endergebnis für weitere Ausgabeseiten zuständig. Bei `get_verify_advisories` niemals beide Tokenarten gleichzeitig senden. Die letzte Seite macht einen partiellen Scan nicht vollständig.

## Werkzeugwahl

Alle folgenden Aufrufe erhalten zusätzlich `targetPath`. `S` = Source, `A` = Assembly. Zahlen sind Defaults, sofern nicht als Cap bezeichnet. Exakte Zusatzparameter und Validierungsgrenzen gezielt über `tools/list` lesen.

| Tool | Ziel | Zweck / wesentliche Eingaben |
| --- | --- | --- |
| `get_file_tree` | S/A | Physische Landkarte. Einstieg `view="summary"`; danach `root`, `fileFilter`, `includeExtensions`, `excludePatterns`; `view="files"` für eingegrenzte Pfadlisten. `maxResults=20`, `maxResponseBytes=8192`. |
| `get_namespace_tree` | S/A | Semantische Struktur: `project`, `namespacePrefix`, `depth=1` (Cap 3), `includeTypes=true`. Ohne Filter: Source-Projektübersicht, Assembly-Namespacebaum. |
| `get_index_scope` | S | Roslyn-Dokumentpopulation und Dateitypen; generierte Dokumente, Tests und physische Dateien unterscheiden. |
| `find_symbol` | S/A | `pattern` oder `namePatterns` für Batch; `kind`, `scopeType`, `includeGenerated`; `maxResults=50`. Handoffs für Folgecalls. |
| `get_file_skeleton` | S/A | Batch `filePaths`: Typen und Signaturen ohne Bodies; `maxResponseBytes=24576`. |
| `get_class_structure` | S/A | Member eines Typs via `symbolIdentifier`; `sortBy="lines"`, `kindFilter`, `nameFilter`, `maxMembers=50` (Cap 200). |
| `get_symbol_body` | S/A | Batch `symbolIdentifiers`; `maxBodyLines=80` je Body. `startLine=1`, optional `endLine`, relativ zum Body. `bodyAvailability` und `contentMode` beachten. |
| `get_feature_context` | S | Symbolkontext mit Deklaration, Metriken, Callern, Tests, Violations; `symbolIdentifier`, `maxCallers=10`, `maxTests=10`. |
| `get_test_context` | S | Statische Testkandidaten/Kategorien/Zuordnungsgründe für `symbolIdentifier`; `maxResults=30`. Keine Testausführung. |
| `find_references` | S/A | Call-Sites zu `symbolIdentifier`; `depth=1` (Cap 3), `maxResults=50`. Bei `coverage=razor_markup_not_indexed` die ausgegebene Markup-Suche ausführen. |
| `get_call_tree` | S/A | Aufrufbaum: `symbolIdentifier`, `direction="incoming"`/`outgoing`/`both`, `depth=2` (Cap 5), `topN=10`, `format="ascii"`/`mermaid`. Incoming zählt Aufrufe, keine Override-Deklarationen. |
| `get_type_hierarchy` | S/A | Basis-/Subtypen, Interfaces, erkannte DI-Registrierungen; `symbolIdentifier`, `maxResults=50`. |
| `find_implementations` | S/A | Konkrete Implementierungen und Overrides; `symbolIdentifier`, `maxResults=50`. |
| `dependency_graph` | S/A | Typreferenzen: genau `filePath` oder `symbolIdentifier`; `direction="both"`, `depth=1` (Cap 3), `maxResults=50`. |
| `get_impact` | S/A | Source ohne Zusatzargumente: uncommittete Änderungen; alternativ `gitRef` oder `symbolIdentifier`, exklusiv. Assembly benötigt `symbolIdentifier`. Symboltiefe `depth=1` (Cap 3). |
| `resolve_type_origin` | S/A | Definierende Assembly, Typname und DLL-Pfad: genau `symbolIdentifier` oder `typeName`. DLL-Pfad wird `targetPath` der Assembly-Folgeabfrage. |
| `metrics_tree` | S/A | Verzeichnis-/Dateimetriken; `mode="code_size"`/`complexity`, `root`, `fileFilter`, `depth=1`, `topN=10`. |
| `metrics_lookup` | S | Regelgebundene Metriken für Batch `symbolIdentifiers`; benötigt Source-Konfiguration. Dekompilierte Assembly-Ziele werden abgelehnt. |
| `get_hotspots` | S | Dateien nahe `MaxLineCount`; `scopeType="production"`, `scopeFilter`, `minLinePercentage=80`, `maxResults=50` (Cap 200). |
| `search_pattern` | S | Text/Regex in C# und Nicht-C#: `pattern`, `isRegex`, `scope`, `includePatterns`, `excludePatterns`; `maxResults=20`, `contextLines=0`, `maxResponseBytes=8192`. `enrichCSharp=true` ergänzt auflösbare C#-Symbolbelege. |
| `pattern_detect` | S | Gruppierte Heuristiken; `patterns`, `scopeFilter`, `maxResultsPerPattern`. Keine Gate-Verstöße. |
| `find_duplicates` | S | `mode="clone"`/`refactoring-drift`/`structural`; Drift benötigt `helperSymbol`. `scopeType="production"`, `scopeDir`, `minTokens=30`, `normalizeIdentifiers=false`, `maxResults=20`. |
| `verify` | S | Festes Gate; `scope="changes"` (Default) oder `solution`. Fertiges `pass` nur bei Score `10.0` und `violationCount=0`. |
| `get_verify_advisories` | S | `category="dead_code"`; ohne Seitentoken neuer Scan, mit `continuationToken` denselben Snapshot weiterlesen. |
| `inspect_assembly` | A | API/Metadaten: `namespace`, `typeName`, `exactTypeName`, `memberName`/`memberNames`, `publicOnly=true`, `maxResults=100`, `maxMembers=100`. |
| `search_assembly` | A | Dekompilat: `searchKind="text"` mit `pattern`, alternativ `data_access`/`external_calls`; `declarationOnly`, `kind`, `fileFilter`, `maxResults=50`, `contextLines` bis 5. |
| `find_assembly_extensions` | A | Extensions nach `receiverType`, `extensionName`, `namespace`; `includeReferences=false`, `maxResults=100`. |
| `get_assembly_context` | A | Composite: Identität, optional `symbolIdentifier`, Body, Caller, Impact, Struktur. Abschnittsflags gezielt aktivieren; `includeMetrics=false` (Dekompilat-Metriken hier nicht verfügbar). |
| `get_server_health` | S/A/global | Ohne Target serverweite Aggregate. Mit Target ausschließlich dessen Status; `includeDiagnostics=false`, `maxDiagnostics=20` (Cap 50). |
| `reload_config` | S | Benachbarte Regeldatei neu laden; neuer Regel-Snapshot ohne Server-Neustart. |

## Kopierbare Aufrufmuster

Beispielpfade durch vorhandene Ziele ersetzen; `h:…` durch einen tatsächlich ausgegebenen Handle. Pro Block ist genau ein Toolaufruf dargestellt.

`find_symbol`:

```json
{"targetPath":"C:\\repos\\MyApp\\MyApp.slnx","pattern":"OrderService","kind":"class","maxResults":10}
```

`get_symbol_body`:

```json
{"targetPath":"C:\\repos\\MyApp\\MyApp.slnx","symbolIdentifiers":["h:…"],"maxBodyLines":80}
```

`search_pattern` für Razor-Verwendungen:

```json
{"targetPath":"C:\\repos\\MyApp\\MyApp.slnx","pattern":"OrderService","isRegex":false,"includePatterns":["**/*.razor"],"maxResults":20}
```

`get_impact` für Änderungskontext:

```json
{"targetPath":"C:\\repos\\MyApp\\MyApp.slnx","detailLevel":"change-context","maxChangedSymbols":10,"maxTestsPerSymbol":5}
```

`detailLevel="change-context"` ist ausschließlich im Git-Modus zulässig. Es liefert geänderte Symbole, Call-Sites, statische Testzuordnung, Filterbefehle und kontextuelle Violations; kein Gate-Verdict. Im Symbol-Modus nennt die Impact-Summary Risiko sowie direkte/transitive Call-Sites. Begrenzte/dekompilierte Sicht kann `not_decidable` ergeben.

## Assembly-Grenzen

Assemblies werden dekompiliert/als Metadaten gelesen, nicht ausgeführt. Ein virtueller Roslyn-Snapshot ermöglicht Bodies und Symbolgraphen. Statisch unauflösbare Referenzen bleiben Diagnosen; fehlende Source-/Dekompilat-Roots sind `unsupported`, keine leeren Suchergebnisse.

Bei Tools mit `includeReferences`: `false` beschränkt die Suche auf das Root; ein verifizierter Referenz-Handle öffnet ausschließlich seinen Owner (`symbol_owner_only`). `true` erlaubt die begrenzte Referenz-Closure. `inspect_assembly` hat ohne Typ-/Memberfilter standardmäßig `includeReferences=true`, mit Filter `false`. Angeforderten und effektiven Suchmodus im Content prüfen.

`search_assembly` erwartet Text/Regex, keine Handles als `pattern`. Eindeutige Deklarationstreffer liefern Handoffs für `get_symbol_body`. Reine Texttreffer erhalten keinen erfundenen Symbolbezug. `get_assembly_context` unterstützt `detailLevel=compact/standard/full`; Detailflags und Ausgabelimits beeinflussen die sichtbaren Abschnitte.

## Gate und Advisories

`verify` ist ein Source-Linter-Gate; es führt keine Tests aus. Scope, Score und Verstoßzahl erst aus dem fertigen Ergebnis lesen. Ein unvollständiger Änderungskontext ist kein Nachweis für die ganze Solution; dazu `scope="solution"` verwenden.

Dead-Code-Advisories ändern Verdict/Score/Verstoßzahl nicht. `verify` gibt dafür nur `deadCode` mit Status, beobachteter Kandidatenzahl und Scanabdeckung oder Ursache aus. `deadCodeHint` nennt `get_verify_advisories(category=dead_code)`; wenn ein Verify-Snapshot vorliegt, enthält der Aufruf dessen `continuationToken`. Einzelne Dead-Code-Zeilen, Confidence, `test_only` und `undecidable` werden hier nicht ausgegeben. Standard: 10 Sekunden zusätzliches Scanbudget, bei explizitem Solution-Scan/neuem Advisory-Scan 60 Sekunden. `evidence: returned=X/Y` zählt Gate-Verstöße plus die einzeln ausgegebenen anderen Advisory-Kategorien; Dead-Code-Kandidaten zählen nicht dazu.

`scanCompleteness` beschreibt Analyseabdeckung, `listCompleteness` nur gespeicherte Ausgabeseiten. Null Kandidaten bei partiellem Scan sind keine Entwarnung. Der Detailabruf liefert Prüfkandidaten, die konkrete Gegenprüfung von Laufzeitbindung und externen Verträgen verlangen. [Nutzungsregeln und Grenzen](dead-code.md), [API-Policy und Budgets](../linter/configuration.md#dead-code-advisory).

## Populationen und Resources

`get_file_tree` zählt physische Dateien, `get_index_scope` Roslyn-Dokumente. Generierte Dokumente, ausgeschlossene physische Dateien, übersprungene Verzeichnisse/Reparse-Points und unlesbare Verzeichnisse sind verschiedene Populationen; Counts nicht addieren oder gleichsetzen. Fehlende C#-Call-Sites erfassen nicht automatisch Razor, XAML, Reflection oder externe Consumer.

Resources: `ainetlinter://agent-guide` einmalig; danach `ainetlinter://overview?targetPath=<URL-kodiert>` und für Source `ainetlinter://rules?targetPath=<URL-kodiert>`. Regeln werden aus dem effektiven Snapshot dargestellt. Tool-Annotations in `tools/list`: Analyse/Health `readOnlyHint=true`; `reload_config=false`. Annotations sind Hinweise auf Seiteneffekte, keine Zugriffssteuerung.

## Implementierungsbelege

Registrierungen: [Mcp/Registration](../../src/AiNetLinter/Mcp/Registration). Ausführung/Formatter: [Mcp/Tools](../../src/AiNetLinter/Mcp/Tools). Assembly-Sessions: [Mcp/Assemblies](../../src/AiNetLinter/Mcp/Assemblies). Versionierte Dokumentation und laufender Server können unterschiedliche Stände haben; bei Abweichungen das gelieferte Schema für den laufenden Prozess verwenden.
