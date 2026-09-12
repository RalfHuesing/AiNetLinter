# AiNetLinter — MCP-Tool-Referenz & Verträge

→ [MCP-Server & Daemon](server.md) | [MCP-Host-Integration](integration.md) | [MCP-Bootstrap](mcp-bootstrap.md) | [Linter-CLI](../linter/cli.md) | [README](../../README.md)

AiNetLinter stellt als **stdio-basierter MCP-Server** 32 spezialisierte Werkzeuge für AI-Coding-Agenten bereit. Diese Referenz beschreibt alle Tools, Eingabeparameter, Antwortstrukturen, Capability-Matrizen und vertraglichen Garantien.

---

## 1. Vertragsgrundlagen (Contract v2)

Bei MCP-`initialize` (Handshake) hält der Daemon mehrere Projekt-Keys
resident. Der registrierte `--mcp-server`-Prozess arbeitet dabei als ThinClient:
Er verbindet sich zuerst mit dem Named-Pipe-Daemon und startet genau einen
detached `--daemon-start`, falls kein Endpunkt erreichbar ist. Nach `hello` /
`welcome` werden stdio-Frames ohne MCP-SDK- oder JSON-RPC-Interpretation
weitergereicht; stdout bleibt ausschließlich MCP-Protokoll. Jeder
zielgebundene Tool-Aufruf erhält genau den absoluten, existierenden
`targetPath` einer konkreten `.sln`/`.slnx`-, `.dll`- oder `.exe`-Datei. Die
Endung bestimmt Source oder Decompiled-Assembly. Relative,
fehlende, nicht unterstützte oder auf ein Verzeichnis zeigende Pfade liefern
`invalid_argument` mit Feldpfad und nächstem Schritt. `--path` und `--config`
sind im MCP-Modus harte Fehler und bleiben dem Batch-Modus vorbehalten.

Jede zielgebundene Toolantwort enthält im einzigen sichtbaren Content den
finalen Status mit Target-, Snapshot- und nächstem-Schritt-Hinweisen. Die
fachliche Evidenz, Handoff-IDs sowie `operation` und `completeness` stehen
ebenfalls im Content. Der globale Health-Modus und ungebundenes
Observability-Feedback bleiben ohne Target.

Contract v2 trennt `status.operation` (Ausführung) und
`status.completeness` (Vollständigkeit der Antwort). Fachliche Analysequalität,
etwa unvollständige Assembly-Diagnostics, bleibt eine eigene Payloadaussage und
wird nicht als Erfolg, Empty oder Trunkierung umgedeutet. Ein
`RESPONSE_BUDGET_TOO_SMALL` nennt `fieldPath=$.maxResponseBytes`,
`requestedBytes` und `minimumResponseBytes`; mit demselben Request und Snapshot
führt genau dieser Mindestwert als `maxResponseBytes` zu einer ausführbaren
Mindestprojektion. Die Messung umfasst ausschließlich den finalen Content in
UTF-8.

Bei einer zielgebundenen `get_server_health`-Antwort beschreibt
`navigation.snapshot` den Snapshot, auf dem ein nachfolgender Analyseaufruf
aufsetzen kann. Für Source ist `snapshot.kind=source-files`; für eine
erfolgreich geladene Assembly `snapshot.kind=assembly`. `snapshot.fingerprint`
ist dabei mit dem Fingerprint des nachfolgenden passenden Analyseaufrufs
korrelierbar. `snapshot.fresh=true` bedeutet, dass ein aktuell geladener,
adressierbarer Analyse-Snapshot mit diesem Fingerprint existiert — nicht, dass
jede optionale Referenz ohne Diagnose geladen wurde. Eine Assembly darf deshalb
auch bei `partial` oder `degraded` einen frischen Snapshot melden, sofern der
Content-Hash und die analysierbare Generation vorhanden sind; bei `loading`,
`failed` oder fehlendem Hash bleibt der Snapshot `unavailable` bzw. `fresh=false`.

Kopierbare Folgecalls verwenden ausschließlich die als Handoff-ID markierte
Content-Zeile. IDs binden Target, Snapshot und Symbolidentität. FQNs,
DocCommentIds sowie Anzeigenamen sind keine Folgeinputs. Ein fremdes Target liefert `TARGET_MISMATCH`, ein veralteter
Snapshot `STALE_SNAPSHOT`; beide Fälle sind von `SYMBOL_NOT_FOUND` getrennt.

Ein recoverable Symbol-Miss wird im Envelope mit
`status.operation=symbol_not_found`, `status.completeness=not_applicable` und
`next.kind=refine_scope` projiziert. Ein erfolgreicher, geprüfter Scope ohne
Treffer bleibt dagegen `empty`.

Für Source ist die übergebene Solution bindend. Regeln werden ausschließlich
aus der optionalen Datei `ainetlinter-rules.json` direkt neben ihr gelesen;
fehlt sie, bleibt Navigation möglich und die Lint-Capability ist
`not_configured`. Ungültige oder nicht lesbare Regeln sind ein
Konfigurationsfehler, kein Fallback auf Default- oder Elternpfade.

`get_server_health` kann ohne Target global aggregieren oder optional einen
`targetPath` erhalten. Ohne Target liefert es ausschließlich serverweite
Zähler, Kapazitätszustand und begrenzte Fehlerzähler; Targetpfade, Hashes,
Diagnostics, Generationen und Lease-Details bleiben ausgeblendet.
`includeSessions` ist kein öffentlicher Input. Ein zielgebundener Aufruf bleibt
auf das angefragte Target begrenzt.



---

### Scope-Hinweis (C#-only)

Der Server schickt bei `initialize` und modernem `server/discover` denselben zentralen `ServerInstructions`-Text an den Agent. Er enthält nur globale Regeln: den `targetPath`-Vertrag, den optionalen Verweis auf den einmaligen Bootstrap über `ainetlinter://agent-guide`, die C#-Symbolgraph-Grenze mit `search_pattern`-Fallback, die Sufficiency-/Truncation-Regel und die `isError`-Policy. Der vollständige Bootstrap wird nicht bei jeder Discovery übertragen. Die vollständigen Tool- und Parameterschemas bleiben in `tools/list`; der Zielstatus steht in der Overview-Resource.

Der globale Text ist auf höchstens 1.200 UTF-8-Bytes begrenzt. Tool-spezifische
Parameter, Defaults und Grenzen sind deshalb ausschließlich dem aktuellen
`tools/list` zu entnehmen.

### Tool-Annotations

`tools/list` enthält für jedes registrierte Tool die vier MCP-Hinweise
`readOnlyHint`, `destructiveHint`, `idempotentHint` und `openWorldHint`. Analyse-,
Symbol-, Metrik- und Health-Abfragen liefern `true/false/true/false` und
`reload_config` `false/false/true/false` (jeweils in der genannten
Reihenfolge). Die Hints beschreiben erwartete Seiteneffekte und die geschlossene
Systemgrenze; sie sind keine Zugriffssteuerung und keine Sicherheitsgarantie und
ersetzen keine Berechtigungs- oder Pfadprüfung. `initialize` und modernes `server/discover` übertragen für
`tools/list` dieselben Annotationen.

### Tool-Referenz

Für jedes zielgebundene Tool ist der absolute, vorhandene `targetPath`
Pflichtparameter; die folgenden Zeilen listen die jeweiligen fachlichen
Zusatzparameter. Die Endung bestimmt Source (`.sln`/`.slnx`) oder Assembly
(`.dll`/`.exe`); `tools/list` weist aus, ob ein Tool beide Herkunftsarten,
nur Source oder nur Assembly unterstützt. `get_server_health` kann ohne Target
global aggregieren oder optional einen `targetPath` erhalten; Feedback bleibt
ungebunden.

### Capability-Matrix und gemeinsame Session

Alle Assembly-fähigen Abfragen verwenden denselben Target-Resolver, die gemeinsame
Assembly-Registry und denselben autarken Dekompilierungspfad wie `inspect_assembly`.
Assemblies (.dll / .exe) werden ausschließlich über dekompilierte Sessions analysiert
(`Quelle: Dekompilat`). Die Assembly wird nicht geladen oder ausgeführt. Die Aussage zu
`includeReferences` gilt nur für Assembly-Tools, deren öffentliche Registrierung
diesen Parameter anbietet: Dort bleibt `includeReferences=false` strikt root-only;
Referenzexpansion wird nur mit `includeReferences=true` und innerhalb der
dokumentierten Session-/Ergebnislimits aktiviert. `get_impact` akzeptiert für Assembly-
Targets ebenfalls `includeReferences` und folgt derselben Suchbreite. Für eine
verifizierte Handoff-ID aus einer Referenz-Assembly bedeutet `false` ausschließlich
deren adressierten Owner (`symbol_owner_only`), nicht Root, Geschwister oder eine
transitive Closure; nur `true` wählt `bounded_reference_closure`. Die Antwort
spiegelt `requestedIncludeReferences` und den effektiven Suchmodus.

Jede Tool-Antwort enthält genau einen nichtleeren Text-Content-Block und optional
`isError`; Zeilenenden sind unabhängig vom Serverbetriebssystem kanonisch `LF`
(`\n`).

| MCP-Familie | Projekt | dekompilierte Assembly | Grenze |
| :--- | :---: | :---: | :--- |
| `find_symbol`, `get_namespace_tree`, `get_file_skeleton`, `get_class_structure` | supported | supported | read-only Symbol-/Struktur-Snapshot |
| `get_symbol_body`, `find_references`, `get_call_tree`, `get_type_hierarchy`, `dependency_graph`, `resolve_type_origin`, `find_implementations` | supported | supported | nur statisch auflösbare Nodes; Diagnosen bleiben sichtbar |
| `metrics_tree`, `metrics_lookup` | supported | supported | berechnet auf dem jeweiligen Snapshot |
| `inspect_assembly`, `find_assembly_extensions` | n/a | supported | Assembly-Target-only; Extensions ohne Consumer ggf. `not_decidable` |
| `search_assembly` | n/a | supported | Assembly-Target-only; durchsucht den Decompiler-Root, ohne Root explizit `unsupported` |
| `get_file_tree` | supported | supported | physischer Projekt- oder dekompilierter SourceRoot-Dateibestand; bei Assembly ohne Root explizit `unsupported` |
| `get_index_scope`, `get_hotspots` | supported | unsupported | physischer Projekt-Dateibestand |
| `get_violations`, `safeguard`, `pattern_detect`, `find_magic_values`, `find_dead_code` | supported | unsupported | Regeln/Audits gelten nur für den Projekt-Key |
| `get_feature_context`, `get_test_context` | supported | unsupported | unsupported | Testbezug ist statische Zuordnung; keine Testausführung |
| `get_impact` | supported | supported | supported | Assembly: nur `symbolIdentifier`; kein Git-Diff/`gitRef` |
| `find_duplicates`, `search_pattern` | supported | unsupported | unsupported | Audit-/Dateisuche bleibt projektgebunden |
| `reload_config` | supported | unsupported | unsupported | lädt nur die Projekt-Regelkonfiguration neu |
| `get_server_health` | supported | supported | supported | ohne Target nur serverweite Aggregate; zielgebunden ausschließlich das angefragte Target |

`unsupported` ist ein expliziter Capability-Status und keine leere erfolgreiche
Antwort. `get_violations`/`safeguard` führen weder fremde Regeln noch Tests aus;
`get_test_context` und der Git-Diff-Zweig von `get_impact` liefern statische bzw.
Git-basierte Hinweise, während der Assembly-Zweig den Symbol-Impact im Snapshot liefert.
Source-backed Checkout-/Snapshot-Erzeugung und Decompilation bleiben read-only.

| Tool | Input | Output | C#-only | Trunkierung |
| :--- | :--- | :--- | :--- | :--- | :---: |
| `get_file_tree` | `targetPath` (Pflicht, absoluter vorhandener Solution- oder Assembly-Dateipfad), `root?` (relativ zum effektiven SourceRoot, Default `.`), `view?` (`tree` Default, `summary` oder `files`), `includeExtensions?`, `fileFilter?` und `excludePatterns?` (relative Pfad-Globs), `maxDepth?` (0–32), `treeDepth?` (0–32, Default 2), `maxResults?` (Default 20, Cap 2.000), `maxResponseBytes?` (Default 8.192, Cap 65.536), `sortBy?`, `includeMetadata?` (Default `true`), `includeLineCount?` (Default `false`) | Physische Dateilandkarte mit Dateipfaden, Root-/Elternaggregaten, getrennten Datei-/Verzeichnis-Counts und `completeness`; die Population ist `summary.scannedFileCount`, nicht die Roslyn-Dokumentmenge. `excludedPhysicalFileCount`, übersprungene Ausschluss- und Reparse-Point-Verzeichnisse sowie unlesbare Verzeichnisse bleiben getrennte Ursachen. `next` enthält bei Trunkierung genau einen sicheren Folgeschritt. `fileFilter`/`excludePatterns` sind relative Pfad-Globs. Bei Assembly wird der vorhandene Source- oder dekompilierte SourceRoot aus der Session verwendet; fehlt er, ist `unsupported`. | nein | ja |
| `get_namespace_tree` | `targetPath` (Pflicht, absoluter vorhandener Solution- oder Assembly-Dateipfad), `project?` (Projektname/Substring), `namespacePrefix?` (Start-Namespace), `depth?` (1-3, Default 1), `includeTypes?` (Default true), `kind?` (class/interface/record/struct/enum/all, Default all), `maxResults?` (Default 50, Cap 200), `maxResponseBytes?` (Default 16.384, Minimum 512, Cap 65.536; Werte außerhalb dieser Grenze sind feldgenau `INVALID_ARGUMENT`) | Hierarchischer Namespace- und Typ-Baum (3 Zoom-Stufen: Solution-Overview, Namespaces, Typ-Liste mit Datei/Zeile/Sichtbarkeit). Das Budget gilt allein für den Content. Bei Trunkierung nennt der Content `totalCount`, `shownCount`, `completeness`, `truncatedBy` und den nächsten Schritt. Passt die kleinste fachliche Einheit nicht, liefert der Fehlervertrag `RESPONSE_BUDGET_TOO_SMALL` mit `requestedBytes` und einem beim Retry ausführbaren `minimumResponseBytes`. Assembly-Targets beginnen mit `# Assembly Overview: <Name>` | ja | ja |
| `inspect_assembly` | `targetPath` (Pflicht, absoluter vorhandener `.dll`- oder `.exe`-Pfad), `namespace?`, `typeName?`, `exactTypeName?` (Default `false`, einfache oder vollqualifizierte Exaktsuche), `memberName?` (case-insensitive Teiltext), `memberNames?` (case-insensitive exakte OR-Auswahl), `publicOnly?` (Default `true`), `includeReferences?` (Default kontextabhängig; für explizite Referenzdetails `true`/`false` setzen), `maxResults?` (Typen, Default 100, Cap 1000; `0` = Default, negative Werte ungültig), `maxMembers?` (Member je Typ, Default 100, Cap 1000) | Assembly-Identität/-Referenzen sowie gefilterte Typen und Member (Methodensignaturen mit Parameternamen, strukturierte Parameter mit Typ, `ref`-Art, Optionalität und Defaultwert, Generics/Constraints, Properties, Felder, Events, Attribute); jeder Typ weist `totalMembers` und `membersTruncated` aus. Materialisat-, Workspace-, Cache- und Generationspfade werden nicht veröffentlicht. Referenzen und Referenz-Sessions sind zusätzlich auf 32 Einträge begrenzt und liefern `referenceSummary`; `diagnostics` sind Samples mit `diagnosticsSummary` (Root/transitiv, Counts, Samples, `truncatedBy`). Bei Diagnosen nennt der Content `completeness=partial` sowie Herkunft, Snapshot, Status, `bodyAvailability` und `contentMode`. | nein | ja |
| `find_assembly_extensions` | `targetPath` (Pflicht, absoluter vorhandener `.dll`- oder `.exe`-Pfad), `receiverType?`, `extensionName?`, `namespace?`, `maxResults?` (Default 100, Cap 1000; `0` = Default, negative Werte ungültig), `includeReferences?`, `maxResponseBytes?`, `detailLevel?`, `continuationToken?` | Klassische C#-Extensions mit strukturierten Parametern und Referenz-/Diagnose-Summaries; `continuationToken` ist die einzige öffentliche Fortsetzung. | nein | ja |
| `get_assembly_context` | `targetPath` (Pflicht, absoluter vorhandener `.dll`- oder `.exe`-Pfad), `symbolIdentifier?`, optionale Abschnitte für Metrics, References, Callers, Impact, Body und Class Structure, `includeReferences?` (Default `false`; steuert die Suchbreite aller Composite-Abschnitte), `maxResults?` (Default 100, Cap 1000; `0` = Default, negative Werte ungültig), `maxBodyLines?` (Default 80, Hard-Cap 1000), `maxCallers?` (Default 10, Hard-Cap 200), `depth?` (Default 1, Hard-Cap 3 für Caller/Impact), `topN?` (Default 10, Hard-Cap 200), `maxResponseBytes?`, `detailLevel?` (`compact`, `standard`, `full`) und `continuationToken?` | Kompakter Composite-Vertrag mit stabiler `contextId`, Identität, Herkunft, Scope, Completeness und `assemblyAnalysis`. `includeReferences=false` bleibt auch für Caller und Impact root-only; `true` verwendet die bounded Closure. `totalCount`, `returnedCount`, `truncatedBy` und genau ein `continuationToken` beschreiben begrenzte Abschnitte; `isTruncated` wird nicht veröffentlicht. Die vier positiv begrenzten Abschnitts-Limits `maxBodyLines`, `maxCallers`, `depth` und `topN` müssen mindestens `1` sein; `0` und negative Werte sowie Werte oberhalb des jeweiligen Hard-Caps liefern vor dem Dispatch feldgenau `INVALID_ARGUMENT`. | nein | ja |
| `search_assembly` | `targetPath` (Pflicht, absoluter vorhandener `.dll`- oder `.exe`-Pfad), `pattern?`, `isRegex?`, `searchKind?` (`text` Default, `data_access`, `external_calls`), `declarationOnly?` (Default `false`), `kind?` (`method`, `type`, `property`), `maxResults?` (Default 50, Cap 1000; `0` = Default, negative Werte ungültig), `maxFiles?`, `contextLines?` (Cap 5), `fileFilter?` (Regex), `maxResponseBytes?`, `continuationToken?` | Read-only Suche im verifizierten dekompilierten SourceRoot. `declarationOnly=true` schließt Treffer in Kommentaren, Strings und XML-Docs aus; `kind` schränkt auf Symbolarten ein. `text` benötigt ein eigenes Pattern; `data_access` und `external_calls` liefern ohne Pattern sichtbare eingebaute Regexe für typische Datenbank-/Datei-/Transaktions- bzw. HTTP-/RPC-/Socket-/Prozessaufrufe. Der Content enthält relative Trefferpfade, stabile Handoff-IDs, Matchbereiche, Kontext, `totalCount`, `returnedCount`, `completeness`, `truncatedBy` und genau ein `continuationToken` sowie Ursprung, Snapshot und Source-Policy. | nein | ja |
| `resolve_type_origin` | `typeName` (Pflicht), `targetPath` (Pflicht, absoluter vorhandener Solution- oder Assembly-Dateipfad) | Ermittelt deterministisch in < 100 ms den einfachen Assembly-Namen, absoluten DLL-Pfad auf der Festplatte, vollqualifizierten Typnamen und Symbol-Kind über Roslyn-Metadatenreferenzen. | ja | nein |
| `find_implementations` | `symbolIdentifier` (Content-Handoff-ID), `maxResults?` (Default 50), `targetPath` (Pflicht, absoluter vorhandener Solution- oder Assembly-Dateipfad) | Findet konkrete Implementierungen und Overrides. Sichtbare Einträge enthalten kopierbare Handoff-IDs für Folgeaufrufe. | ja | ja |
| `find_symbol` | `targetPath` (Pflicht, absoluter vorhandener Solution- oder Assembly-Dateipfad), `namePatterns` (Array von Namens-Mustern), max. 10 Patterns pro Call; unterstützt Substrings, Wildcards `*` und `?`, punktseparierte Pfade wie `Type.Member` sowie `Method()`; `kind?` (`class`/`method`/`property`/`interface`/`record`/`struct`/`enum`/`delegate`), `maxResults?` (Default 50), `scopeType?`, `includeGenerated?`, `includeReferences?` (Default `false`; bei einem Assembly-Target bounded Referenz-Assemblies durchsuchen), `maxResponseBytes?` | Fundstellen und strukturierte Treffer. Scope, Generated-Opt-in und Budget begrenzen die sichtbare Menge; `status.completeness` und `next` beschreiben Trunkierung. | ja | ja |
| `find_references` | `targetPath` (Pflicht, absoluter vorhandener Solution- oder Assembly-Dateipfad), `symbolIdentifier` (Content-Handoff-ID), `maxResults?` (Default 50), `depth?` (Default 1, hard cap 3), `scopeType?`, `includeGenerated?`, `includeReferences?` (Default `false`), `maxResponseBytes?` | Statische Aufrufstellen mit kopierbaren Handoff-IDs. Scope, Tiefe und Budget bestimmen die sichtbaren Einträge; der Content-Marker `completeness` nennt Trunkierungsgründe. | ja | ja |
| `get_call_tree` | `targetPath` (Pflicht, absoluter vorhandener Solution- oder Assembly-Dateipfad), `symbolIdentifier` (Content-Handoff-ID), `depth?` (Default 2, hard cap 5), `format?` (`ascii` Default oder `mermaid`), `topN?` (Default 10), `direction?`, `scopeType?`, `includeGenerated?`, `includeReferences?`, `includeBcl?`, `maxResponseBytes?` | Statischer Graph mit `nodes` und `edges`. Scope, Generated-Opt-in, Tiefe, Fan-out und Budget bestimmen die sichtbaren ganzen Kanten; die Content-Marker `completeness` und `next` zeigen Begrenzungen. | ja | ja |
| `get_impact` | `targetPath` (Pflicht, absoluter vorhandener Solution- oder Assembly-Dateipfad), `gitRef?` (Git-Commit-Ref; ohne jeden Parameter aufgerufen = Standardfall: uncommittete Änderungen) **oder** `symbolIdentifier?` (exklusiv!), `maxResults?` (mindestens 1; `0` oder negative Werte = recoverable `INVALID_ARGUMENT`, Default 50), `depth?` (Symbol-Branch: mindestens 1; `0` oder negative Werte = recoverable `INVALID_ARGUMENT`, Default 1, hard cap 3; im gesamten Git-Branch (callers UND change-context) wirkungslos und auch bei `0` zulässig), `detailLevel?` (`"callers"` Default oder `"change-context"`, case-insensitive; nur im Git-Diff-Modus, nie zusammen mit `symbolIdentifier`), `maxChangedSymbols?` (Default 20, Cap 100; `0` = keine explizite Begrenzung, intern Default), `maxTestsPerSymbol?` (Default 10, Cap 50; `0` = keine explizite Begrenzung, intern Default), `includeReferences?` (Default `false`; bei Assembly bounded Closure). Bei einem Assembly-Target ausschließlich `symbolIdentifier`. | `callers` (Default): betroffene Call-Sites; der Symbol-Branch verwendet für jede Tiefe dieselbe `callSites`/`completeness`-Struktur wie `find_references`. Bei Assembly-Zielen ergänzt der gemeinsame Wrapper ein `analysis`-Objekt mit Target, Herkunft, Snapshot, Status und Vollständigkeit; `navigation`/`origin` werden nicht als `get_impact`-eigene Call-Site-Struktur ausgegeben. `change-context`: strukturiertes Objekt mit geänderten Dateien und Symbolen, Call-Sites, statisch zugeordneten Tests, diffbezogenen Violations, empfohlenen `dotnet test`-Befehlen und Completeness-Metadaten (siehe Detailabschnitt unten) | ja | ja |
| `get_type_hierarchy` | `targetPath` (Pflicht, absoluter vorhandener Solution- oder Assembly-Dateipfad), `symbolIdentifier` (Content-Handoff-ID), `maxResults?` (Default 50), `scopeType?`, `includeGenerated?`, `maxResponseBytes?` | Basisklassen, Interfaces sowie abgeleitete und implementierende Typen; sichtbare Einträge enthalten kopierbare Handoff-IDs. | ja | ja |
| `dependency_graph` | `targetPath` (Pflicht, absoluter vorhandener Solution- oder Assembly-Dateipfad), `filePath?` (ganze Datei) **oder** `symbolIdentifier?` (ein Typ, engerer Scope, exklusiv!), `direction?` (`incoming`/`outgoing`/`both`, Default `both`), `depth?` (Default 1, hard cap 3, transitiv auf Datei-Ebene, hart begrenzt auf 150 besuchte Dateien), `maxResults?` (Default 50) | Datei-zu-Datei-Abhängigkeitskanten (annotiert mit den zugrunde liegenden Typnamen und Referenzzahl), abgeleitet aus echten `SemanticModel`-Typreferenzen statt `using`-Direktiven; optional Projekt-Referenzen des Zielprojekts | ja | ja |
| `get_file_skeleton` | `targetPath` (Pflicht, absoluter vorhandener Solution- oder Assembly-Dateipfad), `filePaths` (Array von Pfaden fuer Batch in 1 Turn), `maxResponseBytes?` (Default 0, Cap 65.536) | Struktur-Skelett (Typen, Signaturen ohne Bodies). Der Content enthält stabile Handoff-IDs für `get_symbol_body.symbolIdentifiers`; das Budget hält nur vollständige sichtbare Evidenzeinheiten. | ja | ja |
| `get_class_structure` | `targetPath` (Pflicht, absoluter vorhandener Solution- oder Assembly-Dateipfad), `symbolIdentifier` (Content-Handoff-ID), `sortBy?`, `maxMembers?`, `maxResponseBytes?` | Tabellarische Memberübersicht. `maxResponseBytes` begrenzt den Content; sichtbare Member und Handoff-IDs bleiben vollständig. | ja | nein |
| `get_index_scope` | `targetPath` (Pflicht, absoluter vorhandener `.sln`- oder `.slnx`-Pfad) | Dynamische Aufschlüsselung jeder tatsächlich vorhandenen Dateiendung in den Projektverzeichnissen (generierte Pfade und Null-Einträge entfallen); die Roslyn-Population wird getrennt von physischen Dateien mit `roslynDocumentCount` ausgewiesen. `.cs` ist als einzige Kategorie vollständig vom Symbolgraphen abgedeckt. Der Content nennt für C# `find_symbol(pattern)` beziehungsweise für Nicht-C# `search_pattern(pattern, scopeType, fileFilter)`; Status-/Bindungsfehler werden klar markiert. | nein | nein |
| `get_hotspots` | `targetPath` (Pflicht, absoluter vorhandener `.sln`- oder `.slnx`-Pfad), `scopeFilter?` (Projekt-Name oder solution-relativer Pfad), `maxResults?` (Default 50, Cap 200), `minLinePercentage?` (Default 80, Bereich 0–100), `scopeType?` (`production` Default, `tests` oder `all`) | `.cs`-Dateien des gewählten Scopes ab der angeforderten Auslastungsschwelle; der Content zeigt nur `critical`/`warning`-Dateien (kein `ok`-Eintrag pro Datei), bleibt nach absteigender Zeilenzahl und Pfad deterministisch sortiert und nennt `totalHotspots`, `shownHotspots`, Trunkierung, effektive Parameter sowie Einträge. | nein | ja |
| `metrics_tree` | `targetPath` (Pflicht, absoluter vorhandener `.sln`- oder `.slnx`-Pfad), `root?` (Teilbaum, Default Solution-Root), `mode?` (`code_size` [Default], `comment_density`, `violation_density`, `complexity`; fehlend/null wird zu `code_size`), `depth?` (1-5, Default 1), `topN?` (Default 10), `fileFilter?` (Regex auf den Pfad) | ASCII-Baum mit `totalCount`, `returnedCount`, `completeness` (Status, Counts, Trunkierungsgründe) und nächstem Schritt im Content; bei Trunkierung nennt er die Anzahl der ausgelassenen Knoten. | nein (zwei der vier Modi sind reiner Datei-Walk) | ja (Top-N pro Ebene) |
| `metrics_lookup` | `targetPath` (Pflicht, absoluter vorhandener Solution- oder Assembly-Dateipfad), `symbolIdentifiers` (Array von Symbol-IDs/Namen fuer Batch in 1 Turn; auch fuer genau ein Symbol) | Punktgenaue Metriken (Netto-LOC, zyklomatische/kognitive Komplexität, effektive Parameteranzahl, AI-Context-Footprint, Member-Counts) und Schwellwert-Abgleich gegen aktive `ainetlinter-rules.json` für ein oder mehrere C#-Symbole; liefert lesbares Markdown mit Status-Badges (`[OK]`, `[WARN]`, `[VIOLATION]`). | ja | nein |
| `get_feature_context` | `targetPath` (Pflicht, absoluter vorhandener `.sln`- oder `.slnx`-Pfad), `symbolIdentifier` (Pflicht), `maxCallers?`, `maxTests?`, `maxResponseBytes?` | Composite-Kontext für Deklaration, Metriken, statischen Impact, Testevidenz und Violations. Der Content weist `callerId` und `callerLocation` für direkte Folgeaufrufe aus; Budget und Status benennen unvollständige Abschnitte. | ja | ja |
| `get_test_context` | `targetPath` (Pflicht, absoluter vorhandener `.sln`- oder `.slnx`-Pfad), `symbolIdentifier` (Pflicht: Typname, Methode, Datei:Zeile oder DocCommentId), `maxResults?` (Default 30, Cap 100; mindestens 1, Werte über 100 liefern vor dem Dispatch feldgenau `INVALID_ARGUMENT`) | Statische Test-Zuordnung für ein C#-Symbol: ermittelt zielgerichtet alle zugeordneten Testdateien, Testklassen, Testmethoden, Test-Kategorien (Unit/Integration), Zuordnungsgründe und direkt ausführbare `dotnet test` Filterbefehle im Content. | ja | ja |
| `get_violations` | `targetPath` (Pflicht, absoluter vorhandener `.sln`- oder `.slnx`-Pfad), `scopeFilter?`, `maxResults?` (Default 50), `contextLines?` (0-5, Default 2; Werte außerhalb des Bereichs liefern vor dem Dispatch feldgenau `INVALID_ARGUMENT`), `includeSnippet?` (Default `false`) | Aktuelle Lint-Verstöße für die adressierte Solution inkl. Regel-ID und optionalen Quellcode-Snippets; ohne benachbarte Regeln `not_configured`, nicht „0 Violations“ | ja | ja |
| `safeguard` | `targetPath` (Pflicht, absoluter vorhandener `.sln`- oder `.slnx`-Pfad), `scopeFilter?` (Projekt-Name oder solution-relativer Pfad), `minScore?` (Default 8.0), `maxViolations?` (Default 20; `0` schaltet nur Remediation-/Top-Violation-Einträge aus; negative Werte werden rückwärtskompatibel auf `0` gekappt, nicht als `INVALID_ARGUMENT` abgelehnt) | Structured JSON (siehe unten): deterministischer Quality-Gate-Score für den geprüften Scope, Pass/Fail gegen `minScore`, Top-Violations, strukturierter Remediation-Hint. Konfigurierte `FileFilters` definieren den effektiven Scope: bewusst ausgeschlossene Dokumente werden nicht bewertet, aber in `excludedDocumentCount` ausgewiesen und machen einen ansonsten entscheidbaren Scope nicht unentscheidbar. `scoreIsNotScope=true` verhindert, dass der Score als Vollständigkeitsbeweis gelesen wird; bei nicht entscheidbarem/unkonfiguriertem Scope fehlen `score` und `passed` statt einer falschen 0-/PASS-Aussage. | ja | nein |
| `pattern_detect` | `targetPath` (Pflicht, absoluter vorhandener `.sln`- oder `.slnx`-Pfad), `patterns?` (Default: alle 6 — god-class, async-void, long-method, public-without-doc, empty-catch, feature-envy), `scopeFilter?` (Projekt-Name oder solution-relativer Pfad), `maxResultsPerPattern?` (Default 20) | Structured JSON + Text: Lint-Verstöße nach Pattern-Kategorie gruppiert. Jede Kategorie weist `status` (`checked`/`empty`/`truncated`/`not_configured`/`not_decidable`), `cause`, `confidence`, `next` und `truncatedBy` aus; `summary.completeness` fasst nur diese Kategorien zusammen und behauptet nichts außerhalb des Scopes. Bewusst durch `FileFilters` ausgeschlossene Dokumente liegen außerhalb des effektiven Scopes und machen die verbleibenden Pattern-Kategorien nicht automatisch `not_decidable`. | ja | ja (je Pattern) |
| `find_magic_values` | `targetPath` (Pflicht, absoluter vorhandener `.sln`- oder `.slnx`-Pfad), `scopeFilter?` (Projekt-Name oder Pfad-Substring), `valueType?` (`all` Default / `strings` / `numbers`), `categoryFilter?` (`all` Default / `config_candidates` / `constant_candidates` / `enum_candidates` / `nameof_candidates` / `localization_candidates` / `standard_candidates` / `security_candidates`), `minOccurrences?` (Default 2, Mindestvorkommen; 1 für Einzelvorkommen), `maxResults?` (Default 50; 0 und negative Werte werden rückwärtskompatibel auf 1 gekappt), `ignoreNumbers?` (optional), `includeTests?` (Default false; filtert `/Tests/`, `/FastTests/` aus dem relativen Pfad), `includeSuppressed?` (Default false; wirksam via `SyntaxTrivia`-Auswertung am Literal), `changedOnly?` (Default false; nutzt `DiffImpactAnalyzer.RunGitDiff` + `ParseGitDiffHunks`, leere Diffs → 0 Dateien) | Kandidaten (`resultType=candidate`) mit sieben disjunkten Kategorien und je Kategorie `status`, `cause`, `confidence`, `next`, `truncatedBy`, `evidenceBoundary`, `scope` und `recommendation` im Content; die Summary trägt dieselbe Entscheidbarkeits- und Trunkierungsmetadaten. Ein Scope ohne analysierbare Datei ist `status=not_decidable` und kein globaler Clean-Claim; `empty` bedeutet nur: der geprüfte Scope enthält keinen Kandidaten. | ja | ja |
| `find_dead_code` | `targetPath` (Pflicht, absoluter vorhandener `.sln`- oder `.slnx`-Pfad), `accessibility?` (`private_internal` Default / `all` / `private` / `internal` / `public`), `confidence?` (`both` Default / `high` / `low`), `kind?` (`all` / `type` / `class` / `method` / `field` / `property` / `event` / `delegate`), `scopeFilter?`, `includeTests?` (Default `false`), `mode?` (`members` Default / `locals` / `both`), `maxResults?` (Default 50; kleiner als 1 ist `invalid_argument`) | Statische Kandidaten für unreferenzierte Typen, Member, Felder, Events oder Locals. Ohne Kandidaten: `dead_code: 0 candidates`; ohne analysierbare Dokumente: `dead_code: no analyzable documents`. Kandidaten enthalten Fundstelle, Confidence, Symbolart, Grund und individuelle Limits; bei Kürzung nennt der Header Gesamt- und angezeigte Anzahl. Die Heuristik ist kein Löschbeweis. Eine symbolgenaue Ausnahme steht direkt vor der Deklaration: `// ainetlinter-disable DeadCode — Begründung` oder `// ainetlinter-disable DeadCode -- Begründung`; ohne Begründung ist sie unwirksam. | ja | ja |
| `get_symbol_body` | `targetPath` (Pflicht, absoluter vorhandener Solution- oder Assembly-Dateipfad), `symbolIdentifiers` (Array stabiler IDs/Namen/Dateizeilen fuer Batch in 1 Turn), `maxBodyLines?` (Default 80) | Markdown-Block mit Symbol-Body bzw. -Bodies, getrennt durch Divider, hart gekappt bei `maxBodyLines` mit Ellipse-Indikator; der Content nennt pro Eintrag `requestedIdentifier`, stabile Handoff-ID, relativen `filePath`, `startLine`, `bodyAvailability`, `contentMode`, Body und Trunkierungs-/Vollständigkeitsangaben. Bei dekompilierten Assembly-Targets stammen verfügbare Bodies aus dem eager WholeProjectDecompiler-Projekt-Snapshot; interne Materialisatpfade werden nicht projiziert. Interface- sowie abstract-/extern-Member bleiben `bodyAvailability=unavailable`. | ja | nein (Body) |
| `search_pattern` | `targetPath` (Pflicht, absoluter vorhandener `.sln`- oder `.slnx`-Pfad), `pattern` (Text oder Regex), `isRegex?` (Default `null` = Auto-Erkennung und Promotion bei 0 Treffern; `true` = explizit Regex, `false` = Plain-Substring), `scopeType?` (`all` [Default], `production` schliesst Tests aus, `tests`), `maxResults?` (Default 20, Cap 2.000), `maxFiles?`, `contextLines?`, `maxResponseBytes?` (Default 8.192, Cap 65.536), `scope?`, `includePatterns?`, `excludePatterns?`, `enrichCSharp?` (Default `false`) | Begrenzte, deterministisch sortierte Treffer im Produktions-/Test-/All-Korpus (alle Dateitypen) mit Match-Bereichen, optionalem Kontext und `completeness` (`totalCount`, `returnedCount`, getrennte Produktions-/Test-Counts, `truncatedBy`) sowie genau einem `next`-Hinweis. `pattern` und die Include-/Exclude-Globfelder sind die kanonischen Suchfelder; Aliasfelder wie `query`, `searchPattern`, `fileFilter` oder `includePattern` werden nicht akzeptiert. Bei `isRegex=null` automatische Regex-Erkennung und Promotion bei 0 Plain-Treffern; bei `enrichCSharp=true` zusätzlich `semantic` für sichtbare Treffer geladener C#-Dokumente | nein (Fallback) | ja |
| `reload_config` | `targetPath` (Pflicht, absoluter vorhandener `.sln`- oder `.slnx`-Pfad) | Liest ausschließlich die optionale `ainetlinter-rules.json` neben der adressierten Solution neu. Fehlt sie, bleibt der Status `not_configured`; ein Konfigurationspfad-Override ist nicht vorgesehen. Vorher/Nachher-Zusammenfassung inkl. Delta bei aktivierten Regeln | nein | nein |
| `get_server_health` | kein Target (globale Aggregation) oder `targetPath?` (absoluter vorhandener `.sln`/`.slnx`/`.dll`/`.exe`-Pfad), `includeDiagnostics?` (Default `false`, nur zielgebunden), `maxDiagnostics?` (Default 20, Cap 50; muss mindestens 1 sein) | ohne Target ausschließlich serverweite Aggregate, Kapazitätszustand und begrenzte Fehlerzähler; keine Targetpfade, Hashes, Diagnostics, Generationen oder Lease-Details. Mit `targetPath` bleibt der Aufruf auf dieses Target begrenzt; `includeDiagnostics=true` begrenzt dort Diagnosesamples über `maxDiagnostics`. `maxDiagnostics <= 0` ist auf Global-, Source-, Assembly-, Stdio- und Daemon-/Thin-Client-Routen ein recoverable `INVALID_ARGUMENT` mit `fieldPath=$.maxDiagnostics` | nein | ja |
| `find_duplicates` | `targetPath` (Pflicht, absoluter vorhandener `.sln`- oder `.slnx`-Pfad), `mode?` (`clone` Default, `refactoring-drift` oder `structural`), `scopeType?` (`production` Default, `all`, `tests`), `minTokens?` (Default aus `ainetlinter-rules.json`, 30), `similarityThreshold?` (`exact`/`near`/`fuzzy`, Default `fuzzy` — niedrigste noch angezeigte Stufe, bei `mode=clone` und `mode=structural`), `normalizeIdentifiers?` (Default `false`, nur `mode=clone`), `scopeDir?` (Default Solution-Root), `maxResults?` (Default 20; mindestens 1), `helperSymbol?` (Datei:Zeile:Spalte, Datei:Zeile ohne Spalte, stabile DocumentationCommentId oder qualifizierter Name wie bei `find_references`; Pflicht bei `mode=refactoring-drift`, bei `mode=structural` ignoriert) | `mode=clone`: Token-basierte Code-Clone-Detection (Jaccard-N-Gram, Method-Granularität) als transitiv gruppierte Cluster (nicht isolierte Paare), gestaffelt nach exact/near/fuzzy-Ähnlichkeit (inkl. Top-Cluster-Übersicht bei >20 Treffern). `mode=refactoring-drift`: Methoden, die den per `helperSymbol` angegebenen Helper strukturell nachbauen statt ihn aufzurufen ("absence-of-calls"-Heuristik, Murphy-Hill 2005) — als Kandidaten (nicht Verstöße) gelistet, siehe Detail-Abschnitt unten. `mode=structural`: Erkennt semantisch ähnliche Hilfsmethoden anhand eines Roslyn-Strukturprofils und Cosine-Similarity (Typ-4/Intended Duplication), liefert manuell zu prüfende Kandidatencluster mit Strukturprofil-Kurzfassung — keine automatische `DuplicateCode`-Violation, eigene Cosine-Schwellwerte aus `ainetlinter-rules.json` (`StructuralDuplicate*Threshold`) | ja | ja |

Die Testinformationen von `get_feature_context`, `get_test_context` und `get_impact` mit `detailLevel="change-context"` (`testAssociations`) sind eine **statische Test-Zuordnung**. Der Scanner führt keine instrumentierte Laufzeit-Coverage durch und liest keine Coverage-Dateien. Der Testbezug sagt daher nicht aus, ob ein Test den Zielpfad tatsächlich ausführt oder Assertions für diesen Pfad enthält.

Bei `get_feature_context` ist der Caller-Bereich ebenfalls statisch: Er basiert auf
Roslyn-Referenzstellen und wird als `statische Referenzen/Call-Sites` ausgegeben.
Die Auswahl vor `maxCallers` sowie die Violations-Ausgabe sind nach Pfad, Zeile,
Projekt/Mitglied, Regel-ID und Nachricht deterministisch sortiert. Ein erfolgreich
leerer Violations-Scan hat `status="complete"`; Fehler, fehlende Quelldateien und
Begrenzungen werden nicht als leerer Scan dargestellt. Cancellation bricht den
Aufruf ab und erzeugt keine partielle Feature-Context-Payload.

Projektgebundene Antworten, die nach einem fehlgeschlagenen Refresh auf dem letzten
guten Solution-Stand beruhen, behalten den bestehenden `[WARN]`-Textkopf. Zusätzlich
nennt ihr Content die Hinweise `degraded=true`, `freshness="stale"`,
`degradedReason="refresh-failed"` und `freshnessWarning`.

### Structured Output

Die Assembly-Tools liefern zusätzlich strukturierte Payloads: `inspect_assembly` verwendet
`InspectAssemblyPayload`, `find_assembly_extensions` verwendet
`FindAssemblyExtensionsPayload`. Beide enthalten `completeness`, Diagnosen und die
Trunkierungsmetadaten; die Extension-Payload kennzeichnet die Roslyn-Anwendbarkeit als
`applicable`, `not_applicable` oder `not_decidable`.

`search_assembly` verwendet `AssemblySearchPayload`. Die Treffer verwenden relative
Pfade zum effektiven Analyse-Root; Herkunft, Revision und Source-Policy
stehen einmalig im gemeinsamen `analysis`-Block. Für weitere Seiten wird der
`continuationToken` mit derselben Anfrage wiederverwendet. Bei `truncatedBy=maxFiles`
ist zusätzlich `maxFiles` zu erhöhen, weil der Token nur innerhalb des gewählten
Dateiscope fortsetzt.

```json
{
  "assemblySearch": {
    "searchKind": "data_access",
    "pattern": "...",
    "scope": "assembly-source-root",
    "results": [{
      "id": "asm-search:...",
      "filePath": "Namespace/Service.cs",
      "line": 42,
      "matchRanges": [{ "column": 13, "length": 12 }],
      "lineText": "...",
      "contextBefore": [],
      "contextAfter": []
    }],
    "totalCount": 12,
    "returnedCount": 1,
    "completeness": "truncated",
    "truncatedBy": ["maxResults"],
    "continuationToken": "1"
  }
}
```

`search_pattern` bleibt die projektgebundene Textsuche für Nicht-C#-Dateien und
Konfiguration; `search_assembly` ist die passende Assembly-Operation. Semantische
C#-Fragen zu gefundenen Symbolen werden anschließend über `find_symbol`,
`get_symbol_body`, `find_references`, `get_call_tree` oder `get_impact` mit dem
Assembly-Target fortgesetzt.

`inspect_assembly` begrenzt mit `maxResults` die Anzahl der Typen und mit `maxMembers`
die Member je Typ. `typeName` bleibt standardmäßig eine Teiltextsuche; mit
`exactTypeName=true` wird gegen den einfachen oder vollqualifizierten Typnamen verglichen.
`memberName` bleibt eine Teiltextsuche. `memberNames` ergänzt sie um eine exakte
ODER-Auswahl. Die Member eines Typs enthalten bei Methoden und Indexern zusätzlich:

```json
{
  "name": "Save",
  "signature": "bool Example.Document.Save(bool abortOnWarning)",
  "parameters": [
    {
      "name": "abortOnWarning",
      "type": "bool",
      "refKind": "none",
      "isOptional": false,
      "defaultValue": null
    }
  ]
}
```

`AssemblyTypeDto.totalMembers` zählt die Member nach allen Filtern vor dem
Member-Limit. `membersTruncated=true` zeigt an, dass nur ein Teil der passenden
Member ausgegeben wurde. Die Parameterdaten stammen ausschließlich aus den
.NET-Metadaten; XML-Dokumentationsdateien werden nicht benötigt.

Assembly-Diagnostics werden nach Herkunft in `diagnosticsSummary.root` und
`diagnosticsSummary.transitive` gezählt. Die gemeinsame `samples`-Liste ist
whitespace-normalisiert und pro Meldung auf 256 Zeichen begrenzt. Zusätzlich gilt
für jede Assembly-Antwort standardmäßig eine harte globale Grenze von 16 KiB (16.384 UTF-8-Bytes)
für den vollständig gerenderten Content einschließlich
Navigation, Referenz-Sessions, wiederholter Summaries und Metadaten. Die Ausgabe
dedupliziert zuerst wiederholte Diagnostic-Samples und kürzt danach optionale
Detailfelder bzw. Listen deterministisch; `truncatedBy` benennt die wirksame
Grenze. `diagnostics` im Payload und der Diagnoseabschnitt im Text enthalten die
gleiche finale Sample-Auswahl. Referenz- und Referenz-Session-Listen zeigen
höchstens 32 Einträge und weisen ihre Gesamt- und Anzeigezahlen in
`referenceSummary` aus.

Positionsangaben im `Datei:Zeile:Spalte`-Format sind 1-basiert. Zeile und Spalte
werden vor dem Roslyn-Zugriff gegen den konkreten `SourceText` geprüft; `0`,
negative Werte und Werte außerhalb der jeweiligen Zeilenbreite liefern den
recoverable Fehler `INVALID_ARGUMENT` mit einem Bereichshinweis. Workspace- oder
Roslyn-Fehler bleiben davon getrennte `WORKSPACE_DIAGNOSTIC`-Fehler.

Assemblies (.dll / .exe) werden ausschließlich über den autarken Dekompilierungspfad
(ILSpy / Roslyn-In-Memory-Workspaces) analysiert. Bei dekompiliertem Code meldet
die Herkunftsausgabe kurz und bündig `Quelle: Dekompilat`.

`get_server_health` liefert ohne Target standardmäßig ein kleines Aggregat:
`sessionsIncluded=false`, `shownSessionCount=0` und keine Sessionliste; sichtbar
bleiben ausschließlich serverweite Gesamt- und Statuszähler sowie begrenzte
Fehlerzähler. `includeSessions` ist kein öffentlicher Input. Ein zielgebundener
Projekt- oder Assembly-Call bleibt auf das angefragte Target begrenzt; erst
`includeDiagnostics=true` aktiviert dort begrenzte Samples und `maxDiagnostics`
wird serverseitig auf 50 gedeckelt. Der Wert muss positiv sein; `0` und negative
Werte werden nicht stillschweigend ersetzt, sondern als recoverable
`INVALID_ARGUMENT` mit `fieldPath=$.maxDiagnostics` zurückgegeben. Die gleiche
Validierung gilt unabhängig davon, ob der Aufruf direkt über Stdio oder über den
Thin-Client-/Daemon-Transport läuft. Bei einem zielgebundenen Aufruf enthält
`navigation.snapshot` den korrelierbaren Snapshot-Fingerprint für den nächsten
Analysecall (siehe Handoff-Kern oben).

Für alle zielgebundenen Tools erfolgt die Argumentprüfung vor der SDK-Bindung.
Fehlende Pflichtfelder, `null` in Pflichtfeldern und falsche Array-Elementtypen
werden als recoverable `INVALID_ARGUMENT` (`isError=false`) mit einem konkreten
`fieldPath` gemeldet; bei Arrays enthält der Pfad den nullbasierten Index, etwa
`$.symbolIdentifiers[1]`. Für die normale öffentliche Listen- und
Traversierungsfamilie müssen die Limits (`maxResults`, `topN`, `maxMembers`,
`maxBodyLines`, `maxCallers`, `maxTests`, `depth`) mindestens `1` sein. `0` oder
negative Werte liefern ein recoverable `INVALID_ARGUMENT` mit dem konkreten
Feldpfad. Die vier Assembly-Tools `search_assembly`, `inspect_assembly`,
`find_assembly_extensions` und `get_assembly_context` sind ausdrücklich
ausgenommen: Dort bedeutet `maxResults=0` den jeweiligen Default (50 bzw. 100),
negative Werte bleiben ungültig. `get_impact` im Git-Diff-Modus mit
`detailLevel="change-context"` ist ebenfalls ausgenommen: Für
`maxChangedSymbols` und `maxTestsPerSymbol` bedeutet `0` keine explizite
Begrenzung; der aktuelle Code normalisiert diesen Wert wie ein weggelassenes
Feld auf den Default von 20 bzw. 10. Werte über den Caps 100 bzw. 50 werden
gekappt. Außerdem ist `depth` im gesamten Git-Diff-Branch wirkungslos; `0`
oder negative Werte werden dort deshalb nicht vorab als ungültig abgelehnt.
Im Symbol-Branch bleibt `depth` dagegen positiv und `0`/negative Werte liefern
feldgenau `INVALID_ARGUMENT`. `find_magic_values` verwendet eine abweichende
Clampsemantik: `maxResults=0` und negative Werte werden auf 1 gekappt, nicht als
`INVALID_ARGUMENT` abgelehnt. Diese Ausnahmen gehören deshalb nicht zur
allgemeinen Positivgrenze.
Für die Composite-Assembly-Abfrage `get_assembly_context` sind dagegen
`maxBodyLines`, `maxCallers`, `depth` und `topN` echte positive Abschnitts-
Limits. Die Hard-Caps (Body 1000, Caller 200, Tiefe 3, Fan-out 200) werden
bereits vor dem Dispatch geprüft. Ein Wert außerhalb des Bereichs liefert
recoverable `INVALID_ARGUMENT` mit dem konkreten Feldpfad; der Handler erhält
keine stillschweigend gekappte Anfrage.
Unbekannte Enumwerte werden nicht still auf einen Default zurückgesetzt:
`minSeverity` akzeptiert `info`, `warning`, `error`; `get_call_tree.format`
akzeptiert `ascii` und `mermaid`.

Wo ein Tool eine angeforderte Tiefe wegen einer Hard-Cap begrenzt, bleiben die
bisherigen Angaben erhalten und der Content nennt `requestedDepth`,
`effectiveDepth` und `depthWasClamped`. Das gilt insbesondere für `get_call_tree`,
`dependency_graph` und den Drilldown von `get_namespace_tree`.

Die Assembly-Contentausgabe nennt `InspectAssemblyPayload`
enthält `assemblyPath`, `identity`, `namespaces`, `references`, `types`,
`diagnostics`, `completeness`, `truncated`, `totalTypes`, `shownCount` und
`truncatedBy`; `AssemblyTypeDto` ergänzt `totalMembers` und `membersTruncated`,
`AssemblyMemberDto` strukturierte Parameter mit Typ, `refKind`, Optionalität und
Defaultwert. `FindAssemblyExtensionsPayload` verwendet analog `extensions`,
`totalExtensions`, `shownCount` und die gemeinsamen Diagnose-/Trunkierungsfelder.
Assembly-fähige Symbol- und Strukturtools erhalten zusätzlich ein `analysis`-Objekt
mit Herkunft, absolutem Target, Hash, Status, Vollständigkeit,
`bodyAvailability` und `contentMode`. Dekompilierte Assembly-Projekte liefern ihre
Bodies aus den bereits geladenen Roslyn-Syntaxbäumen; der Snapshot ist mit
`analysis.contentMode=decompiledProject` ausgewiesen, entsteht eager mit
`WholeProjectDecompiler` und wird als echte Roslyn-Dokumente in einem
`AdhocWorkspace` geführt. `get_symbol_body` weist den direkten
`SourceSymbolBodyResolver`-Pfad mit `contentMode=source` aus und meldet nicht
verfügbare Bodies als `unavailable` mit einem Hinweis. Verwaltete `.dll`- und `.exe`-Dateien
sind gleichwertige Assembly-Targets; native PE-Dateien ohne .NET-Metadaten liefern
statt einer Analyse einen typisierten, recoverable Diagnose-/Hinweis-Response.

Progressive Disclosure bleibt die empfohlene Agentenstrategie: zuerst mit kleinen
Limits und engeren Filtern (`maxResults`, `scopeFilter`, `typeName` oder
`symbolIdentifier`) orientieren, dann die gefundene stabile ID bzw. das Symbol
gezielt an `get_symbol_body`, `get_class_structure` oder Referenztools weitergeben.
Bei Tools mit öffentlichem `includeReferences`-Parameter sind
`includeReferences=true` und Assembly-Detailflags bewusst explizite Folgeschritte;
breite Assembly-Listen nicht als ersten Call ungegrenzt anfordern.

Der vollständige Maschinenvertrag steht pro Tool in `tools/list`. Zielgebundene
Antworten verwenden den Content für Status, Vollständigkeitsmarker und stabile
IDs. `get_file_tree` legt seine Payload unter `fileTree` ab; bei
`includeMetadata=false` fehlen `sizeBytes` an Dateieinträgen.

**`search_pattern` — strukturierte Treffer und C#-Enrichment:** Die gemeinsame sichtbare Match-Liste
liefert `filePath`, 1-basierte `line`-/`matchRanges`-Positionen, unveränderten `lineText`, optional
`contextBefore`/`contextAfter`, `projectName` sowie `completeness`, `scope` und `snapshot`. Bei
`enrichCSharp=false` bleibt `semantic` nicht gesetzt. Bei `true` enthält es `kind`, `resolution`
und, wenn Roslyn eine stabile ID liefert, `symbolId`:

```json
{
  "filePath": "src/App/OrderService.cs",
  "line": 42,
  "matchRanges": [{ "column": 18, "length": 10 }],
  "lineText": "    return await PlaceAsync(order);",
  "projectName": "App",
  "semantic": {
    "kind": "symbol_reference",
    "resolution": "resolved",
    "symbolId": "M:App.OrderService.PlaceAsync"
  }
}
```

`kind` kann `declaration`, `symbol_reference`, `comment`, `string`, `code` oder `unknown` sein.
`resolution` kann `resolved`, `not_applicable`, `unknown`, `ambiguous` oder `unavailable` sein.
Kommentare und String-Literale werden nicht als Symbolreferenzen ausgegeben. Die Anreicherung nutzt
nur eindeutig zuordenbare Dokumente des residenten Roslyn-Snapshots; fehlende Dokumente oder ein
abweichender Snapshot-Zeilentext werden als `unavailable`, mehrdeutige Symbolkandidaten als
`ambiguous` sichtbar. Die lexikalische Treffer-, Scope- und Budgetauswahl sowie die Textausgabe
bleiben unverändert. Bei Trunkierung oder `unavailable`/`ambiguous` sind Scope-Verfeinerung,
niedrigere Limits oder ein gezielter semantischer Folgeaufruf der vorgesehene nächste Schritt.

**`find_references` — transitive Structured Response:** Das Tool liefert ein JSON-Objekt mit deterministisch sortierten und deduplizierten Treffern. `filePath` ist solution-relativ mit Forward-Slashes; `depth` ist die Traversierungsstufe; `reachedFromSymbolId` ist die stabile `DocumentationCommentId` des in diesem Schritt untersuchten Symbols (bei fehlender ID ein deterministischer qualifizierter Anzeigename). Call-Sites, deren Aufrufer eine lokale Funktion ist, tragen in `reachedFromSymbolId` die eindeutige Sonderform `<ID des einschließenden Members>#lf:<Name>@<Zeile>:<Spalte>` — ohne diesen Sonderfall wuerde die Doc-ID der lokalen Funktion mit der ihres einschliessenden Members kollidieren; der String-Wert aenderte sich dadurch von der (geerbten, mehrdeutigen) Methoden-ID zu einer eindeutigen ID.

Bei einem Assembly-`targetPath` bleiben `includeReferences=false` und der bisherige Root-Snapshot der Default. Mit `includeReferences=true` werden höchstens 32 eröffnete Root-/Referenz-Sessions geprüft. Jede Assembly-Herkunft bleibt über `origin` an Treffern beziehungsweise über `navigation` am Payload sichtbar; `navigation.completeness` wird bei Session-Diagnostics, nicht auflösbaren Referenzen oder dem Session-Limit auf `partial` gesetzt. Der dekompilierte Root-Snapshot wird beim Öffnen der Session eager mit `WholeProjectDecompiler` als Projekt materialisiert; die erzeugten `.cs`-Dateien werden als echte Roslyn-Dokumente geladen. Fehlende Call-Sites sind deshalb ein begrenztes Ergebnis des geladenen Snapshots und der Traversierungsgrenzen, kein Beleg dafür, dass außerhalb dieses Scopes keine Aufrufer existieren. `get_symbol_body` liest verfügbare Bodies aus diesen bereits geladenen Syntaxbäumen über den direkten `SourceSymbolBodyResolver`-Pfad und weist `contentMode=source` aus; eine nachträgliche Dekompilierung einzelner Bodies findet nicht statt. Interface- sowie abstract-/extern-Member bleiben als `bodyAvailability=unavailable` mit Hinweis sichtbar.

Eine kanonische Referenz-Handoff-ID behält ihren belegten Owner über Eviction und
Serverneustart. Mit `includeReferences=false` wird nur diese Owner-Assembly
(`symbol_owner_only`) geöffnet; Root, Geschwister und transitive Referenzen bleiben
geschlossen. Die Referenz-Closure ist ausschließlich der explizite
`includeReferences=true`-Pfad. Handoff-IDs erscheinen genau einmal im passenden
Structured-`id`-Feld und nie als `docCommentId`-/`symbolId`-Kopie oder im regulären
Markdown.

**Gemeinsamer Traversal-Datentyp von `find_references` und dem Symbol-Branch von `get_impact`:** Beide Ergebnisse verwenden die folgende `callSites`-/`completeness`-Struktur. Der Assembly-spezifische Vertrag von `get_impact` steht im nächsten Abschnitt; die dort beschriebene `navigation`-Erweiterung gehört ausschließlich zu `find_references`.

```json
{
  "callSites": [
    {
      "filePath": "src/App/OrderService.cs",
      "line": 42,
      "symbolName": "OrderService.PlaceAsync",
      "projectName": "App",
      "depth": 2,
      "reachedFromSymbolId": "M:App.OrderFacade.PlaceAsync"
    }
  ],
  "completeness": {
    "requestedDepth": 2,
    "effectiveDepth": 2,
    "visitedNodeCount": 8,
    "totalCallSiteCount": 14,
    "shownCallSiteCount": 14,
    "truncatedByMaxResults": false,
    "truncatedByNodeLimit": false,
    "depthWasClamped": false
  }
}
```

`totalCallSiteCount` zählt die ungekappte Menge innerhalb des Traversierungs-Hard-Caps; `shownCallSiteCount` zählt die tatsächlich in `callSites` enthaltenen Einträge. `truncatedByMaxResults`, `truncatedByNodeLimit` und `depthWasClamped` sind unabhängig voneinander und können gleichzeitig `true` sein. Die Textantwort wird aus derselben gezeigten Trefferliste formatiert und bleibt für Textclients kompatibel.

**`get_impact` (Symbol-Branch) — Assembly-Vertrag:** Bei einem Assembly-`targetPath` ist ausschließlich `symbolIdentifier` zulässig; ein leerer Aufruf oder `gitRef` liefert einen recoverable `INVALID_ARGUMENT`. `includeReferences=false` bleibt root-only; bei einer verifizierten Assembly-Handoff-ID wird ausschließlich deren Owner geöffnet. `includeReferences=true` verwendet die bounded Closure des Root- beziehungsweise Handoff-Owners. Das 32-Session-Limit wird nicht als `get_impact`-Antwortvertrag wiederholt; der gemeinsame `navigation`-Block bleibt auch hier vorhanden.

Die tatsächliche Symbol-Antwort enthält `callSites` und `completeness` aus `ReferenceTraversalResult`. Bei einem Assembly-Target ergänzt `AssemblyAnalysisResponse.Enrich` den strukturierten Payload um `analysis` mit absolutem `targetPath`, `origin` (`decompiled`), Hash, Status, Vollständigkeit, Body-Verfügbarkeit und Content-Modus. Die Herkunft (`Quelle: Dekompilat`) steht damit im `analysis`-Objekt beziehungsweise im `[ASSEMBLY]`-Textheader; die Call-Site-Einträge dieser Route tragen keine separate `navigation`- oder `origin`-Struktur.

**`get_impact` (`detailLevel=change-context`) — Content im Detail:** Der Git-Diff-Zweig liefert bei `detailLevel="change-context"` einen eigenen sichtbaren Evidenzabschnitt statt der `CallSiteEntry`-Liste des Default-Modus. Der Content nennt:

```json
{
  "mode": "gitDiff",
  "detailLevel": "change-context",
  "changedFiles": [
    { "filePath": "src/App/OrderService.cs", "ranges": [{ "startLine": 40, "lineCount": 8 }] }
  ],
  "changedSymbols": [
    {
      "documentationCommentId": "M:App.OrderService.PlaceAsync",
      "displayName": "OrderService.PlaceAsync",
      "kind": "Method",
      "accessibility": "Public",
      "projectName": "App",
      "filePath": "src/App/OrderService.cs",
      "startLine": 37,
      "endLine": 61
    }
  ],
  "callSites": [],
  "testAssociations": [
    {
      "symbolId": "M:App.OrderService.PlaceAsync",
      "filePath": "tests/App.Tests/OrderServiceTests.cs",
      "testMethods": ["PlaceAsync_ValidOrder_Persists"],
      "matchReason": "Direct Member Match / Invocation"
    }
  ],
  "violations": [
    { "filePath": "src/App/OrderService.cs", "lineNumber": 44, "ruleName": "...", "severity": "warning", "details": "..." }
  ],
  "recommendedTestCommands": ["dotnet test tests/App.Tests --filter FullyQualifiedName~OrderServiceTests"],
  "completeness": {
    "changedSymbolsTotal": 3,
    "changedSymbolsShown": 3,
    "symbolsTruncated": false,
    "callSitesTruncated": false,
    "testsTruncated": false
  }
}
```

Die Feldnamen sind vertraglich exakt (zentrale CamelCase-Policy, durch Vertragstests gepinnt); `accessibility` ist bewusst ein String (z. B. `"Public"`), keine Zahl. `callSites` verwendet dieselbe `TransitiveCallSiteEntry`-Struktur wie der transitive Abschnitt oben. `matchReason` traegt die getrennten Evidenzarten der statischen Zuordnung in ihren Literal-Formen — `"Direct Member Match / Invocation"`, `"Naming Convention Match"`, `"Explicit @covers Comment"`, `"Direct typeof Reference"` — priorisiert in dieser Reihenfolge.

Vertragsregeln:

- `detailLevel="change-context"` ist nur im Git-Diff-Modus zulaessig. Die Validierung ist case-insensitive; die Kombination mit `symbolIdentifier` und jeder unbekannte `detailLevel`-Wert liefern ein recoverable `INVALID_ARGUMENT` — im Kombinationsfall mit dem Hinweis, fuer den Kontext eines einzelnen Symbols `get_feature_context` zu nutzen. Weglassen, leer oder `"callers"` waehlt das bestehende Call-Site-Verhalten (Default).
- `maxChangedSymbols` (Default 20, Cap 100) und `maxTestsPerSymbol` (Default 10, Cap 50) sind optionale change-context-Caps: `0` bedeutet keine explizite Begrenzung und wird wie Weglassen auf den jeweiligen Default normalisiert; negative Werte sind dagegen `INVALID_ARGUMENT`, Werte über dem Cap werden auf den Cap gekappt. Die Symbol-Kappung sitzt im Analyzer-Kern nach der Symbolermittlung und VOR den teuren Call-Site-, Test- und Violations-Analysen; die Kappungsreihenfolge ist deterministisch (Projekt → Datei → Startzeile → Symbol-ID), weggekappte Symbole erscheinen nirgends in der Antwort, `completeness.changedSymbolsTotal` spiegelt die Zahl vor der Kappung.
- `maxResults` (Default 50) kappet in diesem Modus nur die Symbol-/Violation-Toplisten der Textantwort, nicht das strukturierte Objekt.
- Die Textantwort ist eine kompakte Zusammenfassung (Kennzahlen, Symbol- und Violation-Topliste, empfohlene Befehle). Bei vollstaendigem Ergebnis haengt der Sufficiency-Hinweis an, sonst eine Trunkierungs-Meta-Zeile mit den Kappungsgruenden.
- „Kein Git-Repository oder leerer Diff" ist kein Fehlerfall: das Tool liefert ein leeres, aber vertragsgueltiges Objekt (alle Listen leer, `completeness` mit `0`/`false`).
- `violations` sind bewusst kompakt — ohne Snippets oder Source-Ausschnitte.
- `recommendedTestCommands` ist dedupliziert: genau ein Befehl je betroffenem Testprojekt, dessen Filter die Vereinigung der Trefferklassen des Projekts enthaelt (nur aus den GEZEIGTEN Testtreffern gebaut).

**Dokumentierte Grenzen** des change-context-Modus:

- **Gelöschte Dateien** liefern keine Hunks — der Diff-Parser wertet `+++ /dev/null` nicht aus. Gelöschte Dateien erscheinen daher weder in `changedFiles` noch in `changedSymbols`; das ist eine dokumentierte Grenze, kein Fehlerfall.
- **Umbenennungen:** Mit Git-Rename-Detection landen die Hunks unter dem neuen Pfad; ohne Detection erscheinen Löschung und Neuanlage als getrennte Ereignisse — die Löschseite faellt unter dieselbe Grenze wie gelöschte Dateien.
- **`depth` ist im gesamten Git-Branch wirkungslos** (callers UND change-context); die Tiefe der gelieferten Call-Sites ergibt sich aus dem Traversal-Ergebnis, nicht aus dem Parameter.
- **Die stabile ID** (`documentationCommentId`, `testAssociations[].symbolId`) ist eine `DocumentationCommentId`; fehlt diese, greift ein deterministischer FullyQualified-Fallback. Lokale Funktionen erhalten die ID des einschliessenden Members plus das eindeutige Suffix `#lf:<Name>@<Zeile>:<Spalte>`.
- **Die Testinformationen sind eine statische Zuordnung** (siehe Notiz unter der Tool-Tabelle) — keine Laufzeit-Coverage, keine Coverage-Dateien.
- **Multi-Hunk-Container-Regel:** Die innerste Deklaration wird dateiweit ueber alle Hunks entschieden. Trifft ein Hunk einen Member und ein zweiter Hunk derselben Datei die Deklarationszeile des enthaltenen Typs, erscheint nur der Member.

**`safeguard` — Content im Detail:** Der Score aggregiert deterministisch aus dem aktuellen Solution-Zustand Lint-Violations (gewichtet nach Severity), durchschnittliche Cognitive Complexity und AI-Context-Footprint über alle konkreten Klassen im Scope (relativ zu den `Metrics`-Limits aus `ainetlinter-rules.json`) sowie einen Sealed-Klassen-Bonus (falls `EnforceSealedClasses` aktiv ist). Die Top-Einträge in `violations` enthalten jeweils Datei, Zeile, Regel, Severity, Details als Problemtext und konkrete Guidance. `totalViolationCount` zählt alle Violations vor der `maxViolations`-Auswahl; `shownViolationCount` zählt die ausgegebenen Top-Einträge; `violationsTruncated` ist `true`, wenn die Ausgabe wegen `maxViolations` gekürzt wurde. `maxViolations=0` ist eine bewusste Sondersemantik: Es werden keine Remediation-/Top-Violation-Einträge ausgegeben, der Score und die übrigen Status-/Completeness-Felder bleiben jedoch erhalten. Negative Werte werden auf 0 gekappt. Der Content nennt:

```json
{
  "passed": true,
  "score": 10.0,
  "threshold": 8.0,
  "violations": [
    { "filePath": "...", "lineNumber": 42, "ruleName": "...", "details": "...", "severity": "warning", "guidance": "..." }
  ],
  "totalViolationCount": 1,
  "shownViolationCount": 1,
  "violationsTruncated": false,
  "scope": "solution",
  "excludedDocumentCount": 0,
  "scoreIsNotScope": true,
  "completeness": "complete",
  "status": "passed",
  "statusCause": "Der Score ist ein Quality-Gate; die analysierten Dokumente und Regeln sind im Scope entscheidbar.",
  "remediation": {
    "topIssue": "...",
    "actionableSteps": ["..."],
    "documentationHint": "Docs/linter/configuration.md"
  },
  "summary": "Safeguard-Score: 10.00/10 (Threshold 8.00) — PASS. 1 Verstoß, 178 Klassen analysiert."
}
```

`scope` und `completeness` beziehen sich auf die tatsächlich analysierten Dokumente und die Regelkonfiguration. `excludedDocumentCount` weist Dokumente aus, die durch die bewusste `FileFilters`-Konfiguration außerhalb des effektiven Scores liegen; solche Ausschlüsse vergiften das Quality-Gate nicht. `scoreIsNotScope=true` bleibt immer gesetzt: Ein hoher oder niedriger Score ist kein Beweis, dass außerhalb dieses Scopes keine Verstöße existieren. Fehlen analysierbare Dokumente oder aktivierte Regeln, liefert das Tool `status=not_decidable` bzw. `status=not_configured` ohne numerischen `score` oder `passed`-Wert.

Die Text-Antwort wiederholt die Top-Auswahl als `Top-Befunde` mit den Labels
`Problem`, `Datei`, `Zeile`, `Regel`, `Severity` und `Guidance`. Bei einer
Kürzung nennt die Summary zusätzlich `Top-Auswahl wegen maxViolations` und fordert
für die vollständige Liste zum Aufruf von `get_violations` auf.

`IsError` ist ausschließlich bei einer echten Malfunction `true` (LinterEngine-Fehler oder ein Projekt, das trotz `SupportsCompilation == true` auch nach internen Retries keine Compilation liefert) — ein normaler Score-Output mit `passed: false` ist kein Fehler, sondern das erwartete Quality-Gate-Ergebnis.

**`pattern_detect` — Content im Detail:** Reine Aggregation bereits von der `LinterEngine` erzeugter Lint-Verstöße nach 6 Pattern-Kategorien — kein neuer Detection-Code. Unterstützte Patterns: `god-class` (`AIContextFootprint`/`MaxPublicMembersPerType`/`MaxLineCount`), `async-void` (`BanAsyncVoid`), `long-method` (`MaxMethodLineCount`/`MaxCyclomaticComplexity`/`MaxCognitiveComplexity`), `public-without-doc` (`EnforceXmlDocumentation`), `empty-catch` (`EnforceNoSilentCatch`) und `feature-envy` (`AvoidExcessiveMiddleMen`). Der Content nennt:

```json
{
  "patterns": [
    {
      "id": "god-class",
      "description": "...",
      "occurrences": 3,
      "status": "checked",
      "cause": "Alle Treffer im angeforderten Scope wurden geprueft.",
      "confidence": "high",
      "next": { "action": "inspect", "reason": "Treffer im Detail pruefen." },
      "truncatedBy": 0,
      "items": [
        { "filePath": "...", "line": 42, "ruleName": "AIContextFootprint", "details": "..." }
      ]
    }
  ],
  "summary": { "patternsWithHits": 2, "totalOccurrences": 5, "completeness": "complete" }
}
```

Eine Violation gehört immer zu genau einem Pattern (die 6 RuleId-Gruppen überschneiden sich nicht); trifft bei `god-class` mehr als eine Regel auf dieselbe Klasse zu, sind das separate Items (keine Dedupe-Logik, identisch zu `get_violations`). `items` ist je Pattern auf `maxResultsPerPattern` gekappt (Default 20), `occurrences` bleibt die volle Trefferzahl. Jede Kategorie weist zusätzlich Status, Ursache, Confidence, nächsten Schritt und `truncatedBy` aus. Ist eine zugrunde liegende Regel (z. B. `BanAsyncVoid`) deaktiviert oder im Scope nicht vollständig entscheidbar, wird das ausdrücklich als `not_configured` bzw. `not_decidable` ausgegeben — nie als globaler Clean-Claim.

**`dependency_graph` — Content im Detail:** Knoten sind Dateien (Solution-relative Pfade), Kanten sind Datei-zu-Datei, annotiert mit den Typnamen, die den Übergang ausgelöst haben — abgeleitet aus echten `SemanticModel`-Typreferenzen (nicht nur `using`-Direktiven), gefiltert auf Typen, die in der geladenen Solution deklariert sind (BCL-/NuGet-Rauschen ausgeschlossen). `filePath` scannt die ganze Datei (Union aller darin deklarierten Typen), `typeIdentifier` scannt nur die Deklaration dieses einen Typs — enger als die ganze Datei. Ab `depth > 1` traversiert die BFS ausschließlich auf Datei-Ebene (kein Typ-Scope mehr ab Hop 2), zyklische Abhängigkeiten werden über ein Visited-Set abgefangen: eine bereits besuchte Datei wird nicht erneut expandiert, die schließende Kante bleibt aber im Ergebnis sichtbar. Der Content nennt:

```json
{
  "target": { "kind": "file", "path": "src/AiNetLinter/Mcp/Tools/SymbolGraph/FindReferencesTool.cs", "typeName": null },
  "direction": "both",
  "edges": [
    { "from": "...", "to": "...", "direction": "outgoing", "typeNames": ["SymbolIdentifierResolver"], "referenceCount": 2 }
  ],
  "projectReferences": [ { "project": "AiNetLinter.IntegrationTests", "references": ["AiNetLinter", "AiNetLinter.TestKit"] } ],
  "truncated": false
}
```

`maxResults` kappt die angezeigten Kanten (Default 50); die Traversierung selbst ist unabhängig davon hart auf 150 besuchte Dateien begrenzt (Scan-Kosten-Grenze bei großen Solutions) — beide Kappungsarten setzen `truncated: true` und unterdrücken den Sufficiency-Hinweis. Projekt-Referenzen (`Project.ProjectReferences` des Zielprojekts) sind eine günstige Zusatz-Sicht, keine vollständige Projekt-Graph-Traversierung; NuGet-Vulnerability-Scanning ist bewusst nicht Teil dieses Tools.

**`find_duplicates` — Content im Detail:** Token-basiertes Clone-Detection (CCFinder/Jaccard-N-Gram-Ansatz, Method-Granularität) über dieselbe `DuplicateDetectionEngine`, die auch der Linter-Checker `DuplicateCode` nutzt. Transitiv ähnliche Methoden (A~B, B~C) werden zu einem Cluster gruppiert statt als isolierte Paare gemeldet, gestaffelt nach `exact` (≥0.95), `near` (≥0.80) und `fuzzy` (≥0.65) Jaccard-Similarity — `similarityThreshold` bestimmt die niedrigste noch angezeigte Stufe (Default `fuzzy` zeigt alles). Der Content nennt:

```json
{
  "clusters": [
    {
      "bucket": "exact",
      "score": 1.0,
      "members": [
        { "filePath": "...", "line": 42, "signatureName": "MyNamespace.HandlerA.BuildOptions()", "tokenCount": 36, "resultType": "candidate", "confidence": "medium", "evidenceBoundary": "statische Aehnlichkeit innerhalb des Source-Scopes", "countercheck": ["Reflection", "DI", "Generatoren", "dynamic"] },
        { "filePath": "...", "line": 18, "signatureName": "MyNamespace.HandlerB.BuildOptions()", "tokenCount": 36, "resultType": "candidate", "confidence": "medium", "evidenceBoundary": "statische Aehnlichkeit innerhalb des Source-Scopes", "countercheck": ["Reflection", "DI", "Generatoren", "dynamic"] }
      ]
    }
  ],
  "summary": { "methodsScanned": 240, "totalClusters": 3, "shownClusters": 3, "truncated": false, "mode": "clone", "resultType": "candidate", "deletionClaim": false, "status": "checked", "truncatedBy": 0, "next": "review_candidates: Kandidaten manuell pruefen." },
  "resultType": "candidate",
  "deletionClaim": false
}
```

`minTokens` filtert triviale Methoden (leere `Dispose`/`ToString`-Overrides) heraus; generierte, nicht zum Solution-Quellbereich gehörende sowie vom zentralen Source-Katalog ausgeschlossene Dateien werden nicht fingerprinted. Bei `mode=refactoring-drift` nennt ein abgelehnter Helper den tatsächlich ermittelten Grund (etwa Tokenzahl, Scope oder GeneratedCode) statt einer Liste möglicher Ausschlüsse. `normalizeIdentifiers` (Default `false`) schaltet die Erkennung umbenannter Klone (Type-2) an, indem Identifier-/Literal-Tokens vor dem Vergleich normalisiert werden. `scopeDir` grenzt auf einen Teilbereich ein (case-insensitiver Substring-Abgleich auf den Dateipfad, wie `scopeFilter` bei `get_violations`). `maxResults` kappt die gezeigten Cluster (Default 20, aus `ainetlinter-rules.json` überschreibbar); bei `truncated: true` weist `summary.next` explizit auf `continue` mit größerem `maxResults` oder engerem `scopeDir` hin. Cluster und Mitglieder sind Kandidaten (`resultType=candidate`, `deletionClaim=false`) mit Evidenzgrenze und Countercheck.

**`find_duplicates mode=structural` — Content im Detail:** Deterministisches Roslyn-Strukturprofil und Cosine-Similarity (keine Embeddings/Netzwerkzugriffe) zur Erkennung semantisch ähnlicher Hilfsmethoden mit unterschiedlichen Namen und Literalen (Typ-4/Intended Duplication). Das Profil enthält normalisierte Rückgabe-/Parametertypen, Kontrollfluss-Form, aufgelöste Zieltypen bei `switch`/Pattern-Interaktionen sowie grobe Verhaltensmarker (Purity, Literal-Klassen). `similarityThreshold` filtert über eigene Cosine-Schwellwerte aus `ainetlinter-rules.json` (`StructuralDuplicateExactThreshold`/`NearThreshold`/`FuzzyThreshold`, Standard 0.90/0.80/0.70) — unabhängig von den Jaccard-`DuplicateCode*Threshold`-Werten. Ergebnisse sind manuell zu prüfende Kandidatencluster, keine automatischen Verstöße. `helperSymbol` wird ignoriert. Kleiner Helper oft nur mit `minTokens` unter dem Lint-Default 30 sichtbar. Der Content nennt dasselbe Schema wie `mode=clone`, ergänzt um `structureProfile` je Mitglied:

```json
{
  "clusters": [
    {
      "bucket": "near",
      "score": 0.87,
      "members": [
        { "filePath": "src/Tools/GetClassStructureTool.cs", "line": 42, "signatureName": "GetClassStructureTool.GetTypeKindDescription(INamedTypeSymbol)", "tokenCount": 18, "structureProfile": "ret=string; params=INamedTypeSymbol; cf=switch-expr; targets=TypeKind; lits=string; pure; form=switch" },
        { "filePath": "src/Tools/GetNamespaceTreeScanner.cs", "line": 77, "signatureName": "GetNamespaceTreeScanner.DescribeTypeKind(INamedTypeSymbol)", "tokenCount": 16, "structureProfile": "ret=string; params=INamedTypeSymbol; cf=switch-expr; targets=TypeKind; lits=string; pure; form=switch" }
      ]
    }
  ],
  "summary": { "methodsScanned": 312, "totalClusters": 4, "shownClusters": 4, "truncated": false, "mode": "structural" }
}
```

Ergebnisse sind Prüfempfehlungen, keine automatischen Verstöße — `DuplicateCodeChecker`/`safeguard` bleiben auf dem tokenbasierten Verhalten; die höhere False-Positive-Unsicherheit semantischer Ähnlichkeit fließt nicht als Lint-Gate-Verletzung ein.

**`find_duplicates mode=refactoring-drift` — Content im Detail:** Eigenes Antwortschema (nicht `clusters`/`bucket`) — findet Methoden, die strukturell einem per `helperSymbol` benannten Helfer `H` ähneln (Jaccard-Score ≥ `near`-Schwellwert aus `ainetlinter-rules.json`, `DuplicateCodeNearThreshold`), ihn aber nachweislich nicht aufrufen ("absence-of-calls"-Heuristik, Murphy-Hill 2005). `helperSymbol` wird wie bei `find_references` aufgelöst (Datei:Zeile:Spalte, Datei:Zeile ohne Spalte, stabile DocumentationCommentId oder qualifizierter Name); löst der Identifikator nicht auf ein Symbol oder mehrdeutig auf, liefert das Tool denselben `SYMBOL_NOT_FOUND`/`AMBIGUOUS_SYMBOL`-Fehler wie `find_references`. Nur gewöhnliche Methoden/lokale Funktionen sind als Helfer zulässig (Konstruktoren/Properties/Felder werden von der zugrunde liegenden Engine nicht fingerprinted) — `similarityThreshold` wird in diesem Modus ignoriert. Der Content nennt:

```json
{
  "candidates": [
    { "filePath": "...", "line": 42, "signatureName": "MyNamespace.DriftedA.Build()", "tokenCount": 36, "score": 1.0 }
  ],
  "summary": { "helperSymbol": "MyNamespace.OptionsHelper.BuildDefault()", "methodsScanned": 240, "totalCandidates": 2, "shownCandidates": 2, "truncated": false, "resultType": "candidate", "deletionClaim": false, "status": "checked", "truncatedBy": 0, "next": "review_candidates: Kandidaten manuell pruefen." },
  "resultType": "candidate",
  "deletionClaim": false
}
```

Feldname bewusst `candidates`, nicht `violations` — False-Positive-Budget ist höher als bei `mode=clone` (Ziel < 25 %), weil strukturelle Ähnlichkeit nicht zwingend Refactoring-Drift bedeutet (z. B. mehrere legitime, ähnlich aufgebaute `Dispose()`-Implementierungen). Der Content benennt das Ergebnis konsistent als Kandidaten zur manuellen Prüfung, nie als automatisch gemeldete Verstöße — anders als `mode=clone` fließt dieser Modus **nicht** in `DuplicateCodeChecker`/`safeguard` ein (On-Demand-only, kein Lint-Gate).

**`find_magic_values` — Content im Detail:** On-Demand-Audit ueber alle `.cs`-Dokumente der Solution. Klassifiziert Literale nach fachlichen Refactoring-Zielen (`config_candidates` fuer URLs/Pfade/Connection-Strings/Timeouts, `constant_candidates` fuer Format-Strings/Schwellenwerte und duplizierte `const`-Felder, `enum_candidates` fuer if-else-/switch-Kaskaden mit ≥ 3 Vergleichen gegen denselben Identifier, `nameof_candidates` fuer String-Literale, die exakt einem Symbol-Namen im Scope entsprechen, `localization_candidates` fuer User-Facing Exception-Messages > 15 Zeichen, `standard_candidates` fuer HTTP-Statuscodes + kontextgebundene Buffer-Konstanten, `security_candidates` fuer hartcodierte Secrets/Credentials via Name- oder Praefix-Heuristik). Jede Fundstelle gehoert genau einer Kategorie; `categoryFilter` schränkt die Ausgabe auf diese Kategorie ein. Trivial-/Attribut-/Index-/Loop-/GetHashCode-Filter verhindern false positives; `ignoreNumbers` ergaenzt die Trivial-Liste um projektspezifische Zahlen (z. B. 24/60/360/1000). `localization_candidates` liefert in der Praxis selten Treffer (heuristisch auf Exception-Konstruktoren mit Message > 15 Zeichen beschraenkt) — Trefferquote ist abhaengig vom Codebase-Stil. Der Content nennt:

```json
{
  "resultType": "candidate",
  "magicValues": [
    {
      "filePath": "src/AiNetLinter/Api/Controllers/UsersController.cs",
      "line": 42,
      "column": 25,
      "valueType": "string",
      "value": "https://api.example.com/v1",
      "category": "config_candidates",
      "recommendation": "appsettings.json (ApiSettings/BaseUrl o. ae.)",
      "contextHint": "URL-Literal",
      "occurrences": 1,
      "evidenceBoundary": "Statisches URL-/Pfad-Muster im angeforderten C#-Scope",
      "scope": "production"
    }
  ],
  "categories": [
    {
      "category": "config_candidates",
      "total": 1,
      "returnedCount": 1,
      "status": "checked",
      "cause": "Alle 1 Kandidaten im angeforderten Scope wurden geprüft.",
      "confidence": "high",
      "next": { "action": "review_candidates", "reason": "Kandidaten und Evidenz prüfen" },
      "truncatedBy": 0,
      "evidenceBoundary": "Statisches URL-/Pfad-Muster im angeforderten C#-Scope",
      "scope": "production",
      "recommendation": "Konfigurationswert in appsettings.json oder eine typisierte Options-Klasse auslagern.",
      "resultType": "candidate"
    }
  ],
  "summary": {
    "total": 17,
    "shownOccurrences": 17,
    "byCategoryConfig": 12,
    "byCategoryConstant": 4,
    "byCategoryStandard": 1,
    "status": "checked",
    "cause": "Alle Kandidaten im angeforderten Scope wurden geprüft.",
    "confidence": "high",
    "next": { "action": "review_candidates", "reason": "Kandidaten und Evidenz prüfen" },
    "truncatedBy": 0,
    "resultType": "candidate"
  }
}
```

`occurrences` zaehlt identische Literale in derselben Datei (Aggregation ueber `(category, value, filePath)`-Tupel); `minOccurrences` (Default 2 — Mindestvorkommen; 1 fuer auch Einzelvorkommen) filtert unterhalb der Schwelle. `byCategory*`-Felder aggregieren ueber alle Kategorie-Treffer (vor `maxResults`-Trunkierung). `status` ist `checked`, `empty`, `truncated` oder `not_decidable`; `cause`, `confidence`, `next` und `truncatedBy` erklären die Aussagegrenze. Bei `status=not_decidable` enthält der Content eine Scope-Warnung und keinen globalen Clean-Claim. `truncatedBy` ist sowohl global als auch je Kategorie vorhanden.

**Suppression-Sonderfall (bewusste Ausnahme):** `find_magic_values` unterstuetzt Suppression ueber `// ainetlinter-disable MagicValues` (oder `/* ainetlinter-disable MagicValues */`), allerdings bewusst pro Fundstelle via `SyntaxTrivia` (Leading + Trailing) statt ueber den dateiweiten `SuppressionScanner`. Abweichung von der sonst projektweiten Suppression-Semantik ist gewollt: bei dutzenden Magic-Value-Funden pro Datei waere ein dateiweiter Disable-Kommentar nutzlos (ein einzelner Kommentar wuerde alle Funde der Datei stumm schalten). Diese feinere Granularitaet ist eine bewusste Ausnahme und nicht als Inkonsistenz misszuverstehen — die Knoten-Auswertung am `LiteralExpressionSyntax` laesst sich performant im selben AST-Walk miterledigen. Implementierte Granularitaet: `SingleLineCommentTrivia` und `MultiLineCommentTrivia` mit exaktem Substring `ainetlinter-disable MagicValues` (Block-Kommentare werden ebenfalls ausgewertet, solange der Heuristik-Pfad sauber bleibt). `includeSuppressed: false` ist der wirksame Default; `includeSuppressed: true` zeigt auch stummgeschaltete Funde (kein Heuristik-Unterschied). Andere Regel-Namen (z. B. `// ainetlinter-disable SomeOtherRule`) und dateiweite `// ainetlinter-disable all`-Semantik werden nicht ausgewertet.

Beispiel-Aufruf (JSON-RPC über stdio):

```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "tools/call",
  "params": {
    "name": "find_symbol",
    "arguments": {
      "targetPath": "C:\\Projects\\MyApp\\MyApp.slnx",
      "namePatterns": ["LinterEngine"],
      "maxResults": 5
    }
  }
}
```

### Trunkierungs-Format

Vier Listen-Tools (`find_symbol`, `find_references`, `get_impact`, `search_pattern`) respektieren den `maxResults`-Parameter (Default 50) und hängen bei Überschreitung eine **einheitliche Meta-Zeile** an die Ausgabe an. Zwei semantisch unterschiedliche Meta-Zeilen existieren:

**Listen-Trunkierung** (Treffer-Liste, `McpTruncation.cs:40`):

```
[N Treffer gesamt, M gezeigt — Pattern verfeinern oder maxResults erhöhen]
```

**Datei-Listen-Trunkierung** (Miss-Hint-Fallback in `find_symbol` bei 0 C#-Treffern, `McpTruncation.cs:66`):

```
[N Dateien mit Textfund, M gezeigt — search_pattern fuer Details]
```

Beide Meta-Zeilen sind wortwörtlich aus `src/AiNetLinter/Mcp/McpTruncation.cs` übernommen — der Code ist die Source of Truth.

Ausnahme: der `get_impact`-Zweig `detailLevel="change-context"` respektiert `maxResults` ebenfalls (Default 50), kappet damit aber nur die Symbol-/Violation-Toplisten seiner Textzusammenfassung und haengt bei Trunkierung statt dieser einheitlichen Meta-Zeile eine eigene `[Teilergebnis: …]`-Zeile an (siehe Detailabschnitt oben).

### Miss-Hint (find_symbol Fallback)

Wenn `find_symbol` mit einem Pattern ohne C#-Treffer aufgerufen wird, liefert das Tool eine trunkierte Datei-Liste der Nicht-C#-Treffer mit der Datei-Listen-Meta-Zeile (siehe oben). Empfohlener Folge-Schritt: `search_pattern` mit demselben Pattern aufrufen.

### Resources für Agenten

Für eine neue Integration ist die direkte Resource `ainetlinter://agent-guide`
der erste Einstieg. Sie ist ohne Target lesbar. Ihr Inhalt entspricht der eingebetteten
Bootstrap-Dokumentation `Docs/mcp/mcp-bootstrap.md` und enthält anschließend die
separat eingebettete dauerhafte `AiNetLinter-McpWorkflow.mdc`. Sie beschreibt
die Auswahl eines absoluten vorhandenen Targets, die optionale benachbarte
`ainetlinter-rules.json`, MCP-Registrierung und das Kopieren der Regeldatei nach
`.agents/rules` oder `.cursor/rules`.
Abruf: `resources/read` mit `{"uri": "ainetlinter://agent-guide"}`. Offline
stehen `ainetlinter --docs mcp-bootstrap` für den Bootstrap und
`ainetlinter --docs mcp-rule` für die dauerhafte Regel zur Verfügung.

Nach dem Bootstrap liefert die Status-Resource `ainetlinter://overview` unter
`ainetlinter://overview?targetPath=<url-encoded>` bei jedem `resources/read`
eine frisch erzeugte kurze Statuskarte: Target-Pfad,
geladene Solution, verwendete Regelquelle und nächste Einstiegspunkte. Die
vollständigen Tool- und Parameterschemas stehen in `tools/list`. Beispiel:
`{"uri": "ainetlinter://overview?targetPath=C%3A%2Frepos%2Fmein-projekt%2Fsrc%2Fmein-projekt.slnx"}`.

Die Resource `ainetlinter://rules{?targetPath}` liefert für denselben adressierten
Solution-Key bei jedem `resources/read` eine frisch aus dessen atomarem Regel-Snapshot
generierte Markdown-Karte. Sie enthält die Regelherkunft (benachbarte
`ainetlinter-rules.json` oder `not_configured`), aktive und deaktivierte Regeln sowie die effektiven
Metrik-Schwellwerte. Projekt- und Pfad-Overrides werden als vorhandene Muster ausgewiesen;
die konkrete Anwendung erfolgt weiterhin pro Roslyn-Projekt bzw. Datei. Beispiel:
`{"uri": "ainetlinter://rules?targetPath=C%3A%2Frepos%2Fmein-projekt%2Fsrc%2Fmein-projekt.slnx"}`.
Die Ausgabe spiegelt auch Änderungen wider, die über `reload_config` in denselben
residenten Solution-Key geladen wurden.

Beide Status-Resources enthalten Textmetadaten des gemeinsamen Envelopes:
`contractVersion`, `target`, `snapshot`, `status`, `scope` und `next`. Die URI
bleibt auf den URL-kodierten absoluten `targetPath` beschränkt.

### stdout-Schutz (strukturelle JSON-RPC-Absicherung)

Im MCP-Server-Modus ist `stdout` der Transport-Kanal des JSON-RPC-Protokolls. Bereits ein einziger `Console.WriteLine(...)`-Call aus irgendeiner wiederverwendeten CLI-Klasse wuerde das Framing der gesamten Session zerstoeren, weil die naechste JSON-RPC-Zeile von einem nicht-JSON-Leak praefixiert waere und der MCP-Host den Frame nicht mehr parsen kann.

Der Schutz ist **strukturell**, nicht ueber Disziplin geloest: im MCP-Modus wird statt `LinterConsole` die `McpLintConsole`-Implementierung aktiviert (in `Program.cs` als expliziter Parameter an `McpServerCommand.RunAsync` uebergeben), die `ILintConsole.WriteLine(...)` zwingend nach `stderr` umleitet. Ein unbeabsichtigter `Console.WriteLine`-Call in einer Tool-Implementierung oder einem Helper wuerde weiterhin ein Leak sein, aber der zentrale `ILintConsole`-Pfad ist abgesichert.

Regressions-Schutz: E2E-Framing-Tests in `McpServerCommandJsonRpcFramingTests` spawnen `AiNetLinter.exe` als Subprozess und schreiben `initialize` beziehungsweise modernes `server/discover` mit anschließendem `tools/list` manuell auf stdin. Sie prüfen **jede** Zeile auf stdout als gültigen JSON-RPC-Frame (`jsonrpc == "2.0"`), vergleichen Instructions und Toolnamen mit der registrierten Collection und messen Zeichen sowie UTF-8-Bytes. Kein SDK-Parser zwischen Subprozess und Assertions — ein unerwarteter Leak würde als nicht-JSON-Zeile sichtbar.

### Compile-Diagnostics

Normale erfolgreiche MCP-Tool-Antworten enthalten keine automatisch aggregierten oder datei-spezifischen Compile-Fehler-Hinweise. Der residente Roslyn-Workspace kann während laufender Bearbeitung einen Zwischenstand enthalten; der Build-Status wird weiterhin durch `dotnet build` bestimmt. Echte Tool- oder Workspace-Fehler werden gemäß dem strukturierten Fehlervertrag ausgegeben.

### Staleness-Invalidierung

`McpCodeGraphServer.GetCurrentSolution()` wird vor **jedem** Tool-Aufruf aufgerufen und prüft pro Document, ob die Datei auf der Platte neuer ist als der zuletzt gesehene `mtime`. Bei Abweichung wird der SHA-256-Hash verglichen, um reine `mtime`-Touchups (z. B. durch einen IDE-Save) zu ignorieren, und nur bei tatsächlicher Inhaltsänderung ein inkrementelles `WithDocumentText`-Update gefahren. **Es findet kein Komplett-Reload des MSBuildWorkspace statt.**

Zusätzlich laufen pro Refresh zwei Erweiterungen:

- **Verzeichnis-Sweep** hängt `.cs`-Dateien, die seit dem Solution-Load neu auf der Platte angelegt wurden, automatisch via `Solution.AddDocument` ein (Filter: `*.cs`, `IsGeneratedPath`-Ausschluss, neues Document landet im ersten passenden Nicht-Test-Projekt bzw. Fallback erstes Projekt). So liefert `find_symbol` auch für gerade erstellten Code Treffer, statt stillschweigend „keine Treffer".
- **Document-Removal** entfernt Documents, deren Datei zwischenzeitlich von der Platte gelöscht wurde, aus dem Solution-Modell (`Solution.RemoveDocument`). So liefert `find_symbol` keine Geister-Treffer auf nicht mehr existente Dateien.

Beide Pfade sind „best-effort": `<Compile Remove=…>`-Ausschlüsse aus `.csproj` werden bewusst nicht gelesen — csproj-Parsing würde den MCP-Server unnötig komplex machen.

### Symbolgraph-Erweiterungen

Drei neue Features erweitern den Symbolgraph um praxisrelevante Hebel:

#### `get_symbol_body` und stabile Symbol-IDs (E.1)

`get_symbol_body` liefert den Source-Body eines oder mehrerer C#-Symbole per stabiler
`DocumentationCommentId` (z. B. `M:AiNetLinter.Mcp.Tools.GetSymbolBodyTool.ExecuteAsync`)
oder per klassischem `Datei:Zeile:Spalte`-Format (Fallback ohne Spalte:
`Datei:Zeile` — bei genau einem quelltext-eigenen Symbol auf der Zeile wird
dieses aufgeloest, bei mehreren liefert das Tool `AMBIGUOUS_SYMBOL` mit
Kandidatenliste analog zur Namensauflösung). 

**Batch-Support:** Über `symbolIdentifiers: ["M:...1", "M:...2"]` können mehrere Symbol-Bodies
in einem **einzigen Turn** geladen werden — spart massiv Roundtrips und Tool-Framing-Overhead.
Einzelne Symbol-Bodies werden ebenfalls als Ein-Element-Array unter
`symbolIdentifiers: ["M:..."]` angefordert.
`maxBodyLines` kappt hart je Symbol (Default 80), die Ausgabe enthaelt einen Ellipse-Indikator plus
Voll-Laengen-Hinweis am Ende. Token-Budget: 15 Zeilen Body statt 500
Zeilen Datei.

`get_file_skeleton` rendert pro Member zusaetzlich einen `id:...`-Marker
in derselben `DocumentationCommentId`-Notation. Damit kann der Agent:

1. `get_file_skeleton` aufrufen, alle relevanten Members + stabile IDs einsammeln.
2. `get_symbol_body` mit einer oder mehreren ausgewaehlten IDs aufrufen (`symbolIdentifiers`), um nur die Bodys genau dieser Member in 1 Turn zu laden.

Die ID ueberlebt Zeilenverschiebungen (solange der Symbol-FQN stabil
bleibt — Refactorings, die den FQN aendern, generieren eine neue ID, by
Design). Overloads werden ueber die voll-qualifizierte Parameter-Signatur
in der ID disambiguiert (`ProcessOrder(int)` vs.
`ProcessOrder(OrderDto)` bekommen unterschiedliche IDs).

Bei einer dekompilierten Assembly wird der konkrete Member im eager erzeugten
`WholeProjectDecompiler`-Projekt-Snapshot gegen den echten Roslyn-C#-Syntaxbaum
gesucht; `get_symbol_body` verwendet dafür den direkten
`SourceSymbolBodyResolver`-Pfad und meldet `contentMode=source`. Enthält dieser durch VB.NET-Optionalparameter
mehr Parameter mit Defaultwerten als die angefragte Metadaten-Signatur, werden die
zusätzlichen Defaultparameter für die Zuordnung berücksichtigt. Ist eine
Parametertype-Referenz im Assembly-Snapshot nicht auflösbar, darf im `partial`-Scope
nur der einfache Typname als Fallback dienen; ein Body wird nicht behauptet, wenn
der konkrete Member weiterhin nicht eindeutig gefunden wird. Für Interface- sowie
abstract-/extern-Member bleibt der Body `unavailable`; der Resolver liefert dafür
keinen ausführbaren Body, sondern den entsprechenden Hinweis.

#### `depth`-Parameter fuer `find_references` / `get_impact` (E.2)

Beide Tools haben einen optionalen `depth`-Parameter (Default 1, hard
cap 3). `depth = 1` liefert direkte Aufrufstellen; `depth > 1`
loest transitive Aufrufstellen ueber `SymbolFinder.FindReferencesAsync`
und aggregiert sie zu derselben strukturierten `callSites`/`completeness`-
Antwortform. Die Eintraege werden vor der `maxResults`-Kappung dedupliziert
und deterministisch nach Tiefe, Pfad, Zeile und Symbolname sortiert.
`completeness` trennt `maxResults`, das Knotenlimit von 200 besuchten
Symbolen und einen auf 3 gekappten Depth-Wert. Der Content wird aus einer
gemeinsamen Aggregation erzeugt.

**Verhaltenskorrektur bei `depth > 1` (nicht nur additive Erweiterung):**
Die Kinder-Expansion enqueued je Referenzlocation das einschliessende
Aufrufer-Member statt der referenzierten Definition. `depth > 1` liefert
seit dieser Korrektur echte mehrstufige Aufruferketten (`A → B → C`) mit
korrekter `Depth`-/`reachedFromSymbolId`-Zuordnung statt faktisch nur
Override-/Interface-Expansion; lokale Funktionen erscheinen dabei als
Reached-From-Knoten mit eindeutigen `#lf:`-IDs. Die Korrektur aendert
Bestandsausgaben und betrifft `find_references` UND den `get_impact`-
Symbol-Branch.

`get_impact` ignoriert `depth` im gesamten Git-Branch (callers und
change-context — es gibt keine Symboltiefe fuer `gitRef`-basierte
Diff-Analyse).

#### DI-Registrierungs-Hinweis in `get_type_hierarchy` (E.3)

`get_type_hierarchy` haengt eine zusaetzliche Sektion

```
DI-Registrierungen (heuristisch, Convention-/Factory-basiertes Scanning nicht abgedeckt):
AddScoped: IReporter, ConsoleReporter (src/Di/Program.cs:9) — AddScoped<IReporter, ConsoleReporter>
...
```

an, sobald die Heuristik mindestens eine Registrierung findet. Die
Heuristik scant alle `.cs`-Dateien per `\b`-Word-Boundary-Regex auf
`AddScoped<...>`, `AddSingleton<...>`, `AddTransient<...>` und filtert
auf Treffer, deren Typ-Parameter den voll-qualifizierten Namen des
Hierarchie-Typs enthalten. Convention-basierte und Factory-basierte
Registrierungen werden bewusst nicht über Reflection erkannt. Bei
0 Treffern wird die Sektion weggelassen.

Fehlt die optionale `ainetlinter-rules.json` neben einer Source-Solution, wird
Navigation trotzdem aufgebaut und die Lint-Capability als `not_configured`
ausgewiesen; ein Lint-Resultat darf daraus keine scheinbare leere oder saubere
Analyse machen. Eine ungültige oder nicht lesbare Regeldatei ist ein
Konfigurationsfehler; es gibt keine Nachbarsuche und keinen stillen Default-
Fallback.

### Error-Reporting

Fehlermeldungen folgen dem bestehenden strukturierten Format auf `stderr` und im Tool-Response-Text:

```
[ERROR]: <CODE>: <Kurzmeldung>
  context: <Datei oder Schritt>
  hint:    <umsetzbare Empfehlung>
```

### Error-Codes im MCP-Kontext

| Code | Bedeutung im MCP-Kontext |
| :--- | :--- |
| `BASELINE_NOT_FOUND` | Baseline-Datei nicht gefunden |
| `BASELINE_INVALID` | Baseline-Datei nicht parsebar |
| `WORKSPACE_DIAGNOSTIC` | Roslyn/MSBuild-Compile-Fehler (auch Defensiv-Wrapper der Tools) |
| `PROJECT_NOT_RESTORED` | Projekt ohne frischen `dotnet restore` — `get_violations`/`safeguard`/`pattern_detect`/`metrics_tree` melden dafür eine Diagnose pro Projekt statt tausender Phantom-Dependency-Folgefehler (`DetectAndBanPhantomDependencies` wird für dieses Projekt unterdrückt), siehe `rationale.md` §13 |
| `ANALYSIS_FAILED` | Analyse-Laufzeit-Fehler |
| `RESOURCE_NOT_FOUND` | Datei/Solution-Pfad nicht gefunden (Server-Start oder `get_file_skeleton`) |
| `DRIFT_DETECTED` | Generierter Inhalt weicht von gespeicherter Datei ab |
| `SYMBOL_NOT_FOUND` | `symbolIdentifier` / `typeIdentifier` löst zu keinem Symbol auf |
| `AMBIGUOUS_SYMBOL` | `symbolIdentifier` löst zu mehreren Symbolen auf (Kandidaten in `context`) |
| `TARGET_MISMATCH` | Eine Handoff-ID gehört zu einem anderen kanonischen Target |
| `STALE_SNAPSHOT` | Eine Handoff-ID gehört zu einem veralteten Analyse-Snapshot |
| `INVALID_ARGUMENT` | Leeres Pattern, ungültige Regex, exklusive Parameter verletzt (`get_impact`), Pflichtparameter fehlt/falsch benannt; `targetPath` fehlt, ist relativ, nicht vorhanden, ein Verzeichnis oder hat eine nicht unterstützte Endung |
| `ASSEMBLY_TARGET_UNSUPPORTED` | Das angegebene Tool unterstützt kein Assembly-Target; ein `.dll`-/`.exe`-Target ist für dieses Tool unsupported |
| `SOLUTION_NOT_FOUND` | Die über `targetPath` angegebene `.sln`-/`.slnx`-Datei existiert nicht oder wird nicht unterstützt |
| `RULES_INVALID` | Die optionale benachbarte `ainetlinter-rules.json` ist lesbar, aber nicht gültig; es werden keine technischen Ersatzregeln geladen |
| `PROJECT_LOAD_FAILED` | Laden der über `targetPath` adressierten Solution fehlgeschlagen; der nächste Aufruf versucht den Load erneut |

### Verhalten bei fehlendem oder falsch benanntem Pflichtparameter

Jedes Tool mit einem Pflicht-Identifikator/-Pfad-Parameter (`find_symbol.namePatterns`,
`find_references`/`get_call_tree.symbolIdentifier`, `get_type_hierarchy.typeIdentifier`,
`get_symbol_body.symbolIdentifiers`, `get_file_skeleton.filePaths`, `metrics_lookup.symbolIdentifiers`,
`search_pattern.pattern`, `metrics_tree.mode`, `find_duplicates`-`mode=refactoring-drift`s `helperSymbol`) deklariert diesen
Parameter auf SDK-Ebene als optional (Default `null`), damit ein fehlender oder falsch benannter
Parameter im JSON-RPC-Aufruf (z. B. `symbolIdentifier` statt des von `get_type_hierarchy`
erwarteten `typeIdentifier`) nicht schon vor Erreichen des Tool-Codes an der Argument-Bindung
scheitert. Der Tool-Code selbst prüft den Parameter danach explizit auf `null`/leer und liefert bei
Verletzung ein reguläres `[ERROR]: INVALID_ARGUMENT`-Ergebnis (`isError = false`, siehe
Error-Codes-Tabelle) mit einem Hint, der den korrekten Parameternamen und das erwartete Format
nennt — kein Server-Crash und keine rohe SDK-Fehlermeldung. Die je Tool bewusst unterschiedlichen
Parameternamen (semantisch passend zum jeweiligen Identifikator-Typ) bleiben davon unberührt.

### Verhalten bei nicht-ladbarer Solution

Schlägt der Kalt-Load eines Projekt-Keys fehl, bleibt der Transport verfügbar.
Der adressierte Tool-Aufruf liefert `PROJECT_LOAD_FAILED` mit Ursprungsmeldung
und Restore-Hinweis; der FAILED-Marker wird nicht negativ gecacht. Ein späterer
Aufruf versucht den Key erneut. Ein Fehler beim inkrementellen Refresh lässt den
letzten guten Stand resident; Antworten tragen bis zur Heilung einen `[WARN]`-
Kopf und Health meldet `LastGoodStateUtc` sowie `LastLoadError`.

### Drei-Zustands-Lifecycle des MCP-Servers

Der Server-Start entkoppelt den MCP-Transport-Handshake vom Solution-Load: `initialize` antwortet sofort, der eigentliche `MSBuildWorkspace.OpenSolutionAsync`-Aufruf läuft im Hintergrund. Dadurch gibt es drei unterscheidbare Zustände, die sich semantisch klar trennen:

| Zustand | Erkennbar an | Reaktion für den Agent |
| :--- | :--- | :--- |
| **Loading** (transient) | `[INFO]: Server laedt die Solution noch. ...` (kein `isError`) | Kurz warten und erneut versuchen (Polling im Sekunden-Takt). Echte Tool-Ergebnisse erscheinen, sobald der Load abgeschlossen ist. |
| **Loaded** (regulär) | Volle Tool-Antworten, `[ERROR]: ...` nur bei tatsächlichen Problemen | Normale Workflow-Schritte ausführen. |
| **LoadFailed** (für diesen Key) | `[ERROR]: PROJECT_LOAD_FAILED: ...` | Solution-/Build-Ursache prüfen und denselben Projekt-Key erneut aufrufen. |

Der `Loading`-Zustand ist bewusst **kein** Fehler (`isError == false`), weil der Tool-Aufruf nicht falsch war — der Server braucht nur wenige Sekunden für den ersten Solution-Load. MCP-Hosts (Claude Desktop, eigene Test-Harness) erkennen den Info-Text und können den Aufruf nach kurzer Pause wiederholen.

---

### Handoff-IDs für Symbol-Chaining

`get_file_skeleton` nennt für jeden Typ `namespace`, `typeKind`, `name`,
`relativePath` und eine stabile Handoff-ID sowie die `members`. Member nennen
`kind`, `signature` und, sofern Roslyn eine Identität vergeben kann, ebenfalls
eine Handoff-ID. Diese IDs können direkt als
`get_symbol_body.symbolIdentifiers` verwendet werden.

`get_feature_context` nennt bei Aufrufstellen `callerId` und `callerLocation`
(`filePath`, `startLine`, `endLine`). `callerId` ist die stabile Handoff-ID des
aufrufenden Symbols und kann direkt an symbolbezogene Folge-Tools übergeben werden.


---

> [AiNetLinter](https://github.com/RalfHuesing/AiNetLinter) — Quellcode, Changelog und Issues auf GitHub.
