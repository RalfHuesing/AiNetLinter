# Epic 2 — Decompilation-Snapshot und metadata-only-Garantie

Status: abgeschlossen als read-only Audit. Stand: 2026-09-01.

## Evidence und Scope

Geprüft wurde ausschließlich Epic 2:

- metadata-only-Garantie und Ausführungsgrenze;
- dekompilierte Dokumente, synthetischer Roslyn-Workspace und Snapshot-/Generation-Lebenszyklus;
- Signatur-/Body-Trennung, Syntaxdiagnosen und Body-Auflösung;
- Generics, Attribute, Parameter und stabile Analyse-IDs;
- die unterscheidbare Herkunft `source-backed` gegenüber `decompiled`.

Nicht geprüft und nicht verändert wurden Produktionscode, Tests, Konfiguration, Produktdokumentation, Builds, Testläufe und Git-Zustand. Die Falllabels sind ausschließlich `GIT-01`, `LOCAL-01`, `LOCAL-02`, `LOCAL-03` und `FALSE-01`.

### Read-only-Evidence

Nur gelesen wurden:

- `AGENTS.md`;
- `.agents/rules/AiNetLinter-McpWorkflow.mdc`, `.agents/rules/AiNetLinter.mdc` und `.agents/rules/AiNetLinterRichtlinien.mdc`;
- `.agents/skills/implement/SKILL.md`;
- `tasks/decompiled-assembly-audit/Konzept.md`, `roadmap.md` und die bestehende `code-map.md`;
- die lokale, gitignorierte Matrix `temp/decompiled-assembly-audit-examples.md` zur Auflösung der fünf opaken Labels;
- relevante aktuelle Produktions- und Testdateien per Dateilesung/Textsuche. Die Testdateien wurden nur gelesen, kein Test wurde ausgeführt.

Die Matrix diente nur zur Eingabeauflösung. Konkrete Assembly-Namen, externe Namespaces, externe Pfade, externe URLs und dekompilierte Inhalte sind deshalb absichtlich nicht Bestandteil dieses Berichts.

### Tatsächlich per AiNetLinter-MCP abgefragt

Alle zielgebundenen Aufrufe verwendeten das aktuelle Schema mit `targetType` und absolutem `targetPath`. Für Assembly-Ziele ist der absolute lokale Pfad im Artefakt aus Datenschutz-/Matrixgründen als `<absoluter Matrixpfad des Labels, redigiert>` dargestellt.

| MCP-Abfrage | Parameter und Ergebnis |
|---|---|
| `get_index_scope` | `targetType=project`, `targetPath=C:\Daten\Entwicklung\Ralf\AiNetLinter`; vollständiger C#-Symbolgraph (`.cs=886`), keine Vollständigkeitswarnung. |
| `get_file_tree` | Projektziel; `root=src/AiNetLinter/Mcp/Assemblies`, `view=tree`, `treeDepth=3`, `includeMetadata=true`, `includeLineCount=true`, `maxResults=200`; 97/97 Dateien, `scanCompleted=true`, `truncated=false`. |
| `get_feature_context` | Projektziel, fünf Kernsymbole, `includeCallers=true`, `includeTests=false`, `includeMetrics=true`, `includeViolations=false`, `maxCallers=20`; vollständige Kontexte für Adapter, Factory, Cache; Callerlisten der Session und des Reference-Resolvers auf 20 von 34 bzw. 39 begrenzt. |
| `get_class_structure` / `get_symbol_body` | Projektziel mit aktuellen Symbol-IDs für Adapter, Cache, Workspace-Factory, Session, Body-Resolver, Origin-/Snapshot-/Dokumentmodelle, `AnalysisSymbolIdentity`, `GetClassStructureTool` und Payload; vollständige Bodies/Strukturen innerhalb der jeweiligen MCP-Antworten. |
| `get_server_health` | zunächst projektgebunden mit Sessions/Diagnosen (`maxSessions=20`, `maxDiagnostics=20`), anschließend aggregiert nach Assembly-Abfragen; Session-/Diagnoseansichten waren durch die angeforderten Limits begrenzt. |
| `inspect_assembly` | für alle fünf Labels mit `targetType=assembly`, absolutem Matrixpfad, `publicOnly=true`, `includeReferences=false`, `maxResults=20`, `maxMembers=40`; zusätzlich typ-/membergefilterte Abfragen mit `exactTypeName=true`, `maxMembers=200`. Ergebnisse waren metadata-only; für `GIT-01`, `LOCAL-01`, `LOCAL-02` und `LOCAL-03` decompiled, für `FALSE-01` eine recoverable strukturierte Nicht-.NET-Diagnose. |
| `find_assembly_extensions` | für `GIT-01`, `LOCAL-01`, `LOCAL-02`, `LOCAL-03` und `FALSE-01` mit `targetType=assembly`, absolutem Matrixpfad und `maxResults=10`; Extension-Ergebnisse waren bounded bzw. leer, `FALSE-01` lieferte die strukturierte Nicht-.NET-Diagnose. |
| `find_symbol` | auf Assembly-Zielen mit `includeReferences=false`, `maxResults=20` und gezielten Namensmustern; Typ-/Member-IDs enthielten Content-Hash und Generation. Ein späterer LOCAL-03-Aufruf mit erneutem Muster lieferte zusätzlich eine nicht-recoverable Workspace-Diagnose (`path is empty`); Ursache ist nicht isoliert. |
| `get_class_structure` | Assembly-Ziele, `sortBy=lines`, `maxMembers=200`, für ausgewählte Typen aus `find_symbol`; Struktur-, Member-, Linien- und Trunkierungsdaten wurden verglichen. |
| `get_file_skeleton` | Assembly-Ziele mit einem aus der vorherigen Symbol-/Strukturabfrage stammenden generierten Dokumentpfad; Signatur-Snapshots und `CS0501`-Deklarationsdiagnosen wurden beobachtet. |
| `get_symbol_body` | Assembly-Ziele mit stabilen Symbol-IDs und alternativ einer `file:line:column`-Auflösung; gewöhnliche Methode, Property und Body eines Members waren on-demand verfügbar. Bei einem Konstruktor aus `get_file_skeleton` schlug die direkte Skeleton-ID fehl, die positionsbasierte Auflösung desselben Konstruktors gelang. |

## Herkunfts-, Snapshot- und Vollständigkeitsmatrix

Die folgenden Werte sind die MCP-Metadaten, nicht aus den dekompilierten Inhalten abgeleitet:

| Label | origin | sourcePath / snapshot | generation / status | completeness / truncation | confidence / trust | Fallback und Diagnosen |
|---|---|---|---|---|---|---|
| `GIT-01` | `decompiled` | `sourcePath=none`, `snapshot=none` | `1 / partial` | `partial`; Typ-/Referenz-/Diagnoseantworten durch `maxResults`, `maxDiagnostics`, Message-/Response-Budgets gekürzt | `medium / untrusted` | `provider-unavailable`; zwei Source-Provider-Diagnosen (Checkout nicht verifiziert, Bereinigung fehlgeschlagen), zusätzlich semantische Decompiler-/Referenzdiagnosen. |
| `LOCAL-01` | `decompiled` | `sourcePath=none`, `snapshot=none` | `1 / partial` | `partial`; Referenzen, Sessions und Diagnosen gekürzt | `medium / untrusted` | `mapping-not-found`; eine Source-Mapping-Diagnose, zusätzlich 54 semantische Decompiler-/Referenzdiagnosen. |
| `LOCAL-02` | `decompiled` | `sourcePath=none`, `snapshot=none` | `1 / partial` | `partial`; Referenzen, Sessions und Diagnosen gekürzt | `medium / untrusted` | `mapping-not-found`; eine Source-Mapping-Diagnose, zusätzlich 75 semantische Decompiler-/Referenzdiagnosen. |
| `LOCAL-03` | `decompiled` | `sourcePath=none`, `snapshot=none` | je nach Folgeabfrage `1` bzw. `3 / partial` | `partial`; Extensions, Typen, Referenzen, Sessions und Diagnosen gekürzt | `medium / untrusted` | `mapping-not-found`; eine Source-Mapping-Diagnose, zusätzlich mehrere tausend semantische Decompiler-/Referenzdiagnosen. |
| `FALSE-01` | kein Snapshot erstellt | kein `sourcePath`, kein `snapshot`, keine Generation | recoverable Fehler, keine Analysegeneration | nicht anwendbar; `truncated=false` im strukturierten Fehler | nicht anwendbar | `isError=false`, `code=WORKSPACE_DIAGNOSTIC`, `recoverable=true`: Datei enthält keine .NET-Metadaten. Es wurde kein Prozess und keine Assembly ausgeführt. |

Ein `source-backed`-Nachweis wurde in diesem Lauf nicht erbracht. Der statische Source-Pfad im Code ist vorhanden: `BuildSourceBackedContext` setzt `origin=source-backed`, Snapshot-/Projektpfad, `trust=verified-clean`, `confidence=high`, `bodyAvailability=source` und `contentMode=source`; ein unusable Source-Selection-/Workspace-Fall fällt über `CreateWorkspaceFallback` auf Decompilation zurück. `GIT-01` blieb in der tatsächlich angesprochenen MCP-Projektumgebung bei `provider-unavailable`. Damit darf `GIT-01` nicht als source-backed gezählt werden.

## Findings — Bug

### E2-BUG-01 — Cache-Roundtrip verliert Dokument-Metadaten

- Priorität: **P1**
- Größe: **M**
- Vertrauen: **hoch** (statischer MCP-Symbolkörper; Cache-Hit-Ausgabe zusätzlich beobachtet)
- Aktuelle Stellen: `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyDecompilationCache.cs:238-255` (`ReadGeneration`), `:315-340` (`ReadDocuments`), `:352-368` (`WriteDocuments`), `:370-410` (`CreateManifest`); Modell `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisSessionModels.cs:99-103` (`DecompiledDocument`).

`DecompiledDocument` trägt ausdrücklich `GeneratedPath`, `TypeMetadataName`, `CSharpSource` und optional `MetadataToken`. Der Schreibpfad persistiert im Manifest jedoch nur die generierten relativen Dateinamen. Beim Lesen wird aus jedem relativen Pfad ein absoluter Cachepfad gemacht und ein neues Dokument mit `Path.GetFileNameWithoutExtension(fullPath)` als `TypeMetadataName` und ohne `MetadataToken` erzeugt. Damit ist ein Cache-Hit nicht metadatenidentisch zum frischen Decompilation-Snapshot; außerdem ändert sich `GeneratedPath` von der synthetischen Dokumentreferenz zum internen absoluten Cachepfad.

Auswirkung: Dokument-/Typzuordnung, Metadata-Token-Verfolgung und Herkunfts-/Navigationsevidence können nach einem Cache-Hit driften. Das ist besonders kritisch, weil die nachfolgenden IDs zwar Content-Hash und Generation tragen, aber der Dokumentdatensatz selbst seine ursprüngliche Typ- und Tokenzuordnung verloren hat. Der spätere `LOCAL-03`-Symbolaufruf auf einem erneut verwendeten Snapshot lieferte passend dazu eine `WORKSPACE_DIAGNOSTIC`-Antwort mit leerem Pfad; die Kausalität ist nicht vollständig isoliert, die Cache-Metadatenabweichung ist aber direkt im aktuellen Code belegt.

Empfehlung: Manifest oder versionskompatiblen Begleitmapper um `TypeMetadataName`, `MetadataToken` und den stabilen relativen `GeneratedPath` je Dokument erweitern; beim Lesen exakt wiederherstellen. Danach einen Cache-Roundtrip mit Fresh-vs-Cache-Vergleich für alle vier Felder und den Origin-/ID-Pfad ergänzen. Die sichere Pfadauflösung und den bestehenden Cache-Schemawechsel beibehalten.

Abgrenzung: Die Cache-Kompatibilitätsprüfung für Hash, Pfad, Decompiler-/Options-/Schema-Identität und Referenzen ist vorhanden; Befund ist der Datenverlust nach erfolgreicher Kompatibilitätsprüfung, nicht die Cache-Key-Bildung.

### E2-BUG-02 — `get_class_structure` mischt relative Gesamtzeilen mit absoluten Memberzeilen

- Priorität: **P2**
- Größe: **S**
- Vertrauen: **hoch** (aktueller MCP-Body plus wiederholte Assembly-Beobachtung)
- Aktuelle Stellen: `src/AiNetLinter/Mcp/Tools/FileStructure/GetClassStructureTool.cs:152-182` (`CollectDeclarationFilesAsync`), `:256-287` (`CreateMemberEntry`), `:48-111` (`ExecuteAsync`); Response-Modell `src/AiNetLinter/Mcp/Tools/FileStructure/GetClassStructureModels.cs:29-37` (`ClassStructurePayload`).

`CollectDeclarationFilesAsync` berechnet `TotalLines` als `EndLine - StartLine + 1` der Typdeklaration. `CreateMemberEntry` gibt dagegen absolute 1-basierte Quellzeilen aus. Das Payload enthält weder den Typ-Start noch eine Koordinatenbasis. Bei einer Projektstrukturabfrage war der aktuelle Typ `AnalysisSymbolIdentity` mit `TotalLines=44` gemeldet, während seine Deklaration bis Zeile 51 reicht; die Assembly-Strukturabfragen bestätigten dasselbe Muster, unter anderem `LOCAL-02` mit Member-Ende 51 bei `TotalLines=48` und `LOCAL-03` mit Member-Ende 19 bei `TotalLines=14`. Die Antworten waren dabei nicht wegen `maxMembers` gekürzt.

Auswirkung: Ein Consumer kann `TotalLines` fälschlich als absolute Endzeile verwenden und erhält ungültige Bereichs-/Navigationsergebnisse. Die Decompilation selbst ist davon nicht beschädigt, aber die Struktur-/Snapshot-Evidence ist für Zeilenvergleiche unzuverlässig.

Empfehlung: Entweder `TotalLines` als echte Dokumentzeilenzahl aus demselben `SourceText` berechnen oder zusätzlich `StartLine`/`EndLine` des Typs ausgeben und die Semantik eindeutig benennen. Eine Invariante `max(Member.EndLine) <= documentEndLine` beziehungsweise eine explizite relative Koordinate sollte in der Strukturprojektion geprüft werden.

Abgrenzung: Dies ist kein behaupteter Fehler in Roslyn-Syntaxspans und kein Trunkierungsbefund; es ist ein Response-/Koordinatenvertrag der Strukturabfrage.

### E2-BUG-03 — Konstruktor-ID aus `get_file_skeleton` ist nicht direkt an `get_symbol_body` anschließbar

- Priorität: **P2**
- Größe: **M**
- Vertrauen: **hoch** (zwei direkte ID-Auflösungen fehlgeschlagen, positionsbasierte Gegenprobe erfolgreich)
- Aktuelle Stellen: `src/AiNetLinter/Mcp/Assemblies/Analysis/AnalysisSymbolIdentity.cs:12-50` (`Format`, `Matches`, `TryParse`), `src/AiNetLinter/Mcp/Assemblies/Analysis/Bodies/AssemblyDecompiledBodyResolver.cs:149-172` (`MatchesMethod`), `:194-232` (Parameter-/Typvergleich); Skeleton-/Body-Dispatch über `get_file_skeleton` und `get_symbol_body`.

Die Skeleton-Antwort für `LOCAL-01` gab für einen Konstruktor eine stabile, content-hash-/generation-präfixierte Member-ID aus. Die direkte Weitergabe dieser ID an `get_symbol_body` ergab `SYMBOL_NOT_FOUND`. Eine positionsbasierte ID im Format `file:line:column` für denselben Konstruktor löste dagegen einen verfügbaren `decompiledBodyOnDemand`-Body auf. Gewöhnliche Methoden- und Property-IDs aus den übrigen Assemblyabfragen waren direkt verwendbar.

Auswirkung: Progressive Disclosure ist für Konstruktoren nicht symmetrisch: Ein Agent kann eine vom Skeleton empfohlene ID nicht zuverlässig als nächsten Body-Request verwenden. Das schwächt die stabile-ID-Garantie genau an einer häufigen Memberform.

Empfehlung: Kanonische Konstruktor-ID-Erzeugung und Resolver-Parsing auf denselben Roslyn-/DocComment-Identifier abgleichen; Skeleton → Body für Konstruktor, überladene Methode, Indexer/Property und generischen Member als Roundtrip prüfen. Die Generation-/Content-Hash-Schranke aus `AnalysisSymbolIdentity` beibehalten.

Abgrenzung: Die Gegenprobe belegt keinen Verlust der Decompilation; sie belegt eine Diskrepanz zwischen zwei MCP-Symboladressierungen.

## Findings — Optimierung

### E2-OPT-01 — On-demand-Body-Decompilation pro Anfrage erneut aufbauen

- Priorität: **P2**
- Größe: **M**
- Vertrauen: **hoch** (aktueller MCP-Symbolkörper)
- Aktuelle Stellen: `src/AiNetLinter/Mcp/Assemblies/Analysis/Bodies/AssemblyDecompiledBodyResolver.cs:54-101` (`DecompileBodyAsync`), `:103-121` (`ToReflectionTypeName`/`FindMember`); Decompiler-Fabrik `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyDecompilationAdapter.cs:126-149` (`CreateDecompiler`).

Der normale Snapshot wird bewusst mit `decompileMemberBodies=false` erzeugt. Ein Body-Request erzeugt anschließend pro Anfrage einen neuen Decompiler, dekompiliert den gesamten enthaltenden Typ mit `decompileMemberBodies=true`, parst den Typ erneut und sucht darin das Member. Die Fallabfragen bestätigen, dass die progressive Body-Auflösung funktioniert; bei mehreren Members desselben großen Typs wiederholt sie jedoch dieselbe teure Arbeit.

Auswirkung: Mehrere Body-Abfragen erhöhen CPU-, Speicher- und Latenzkosten, ohne die metadata-only-Grenze zu verletzen. Das Risiko ist bei `LOCAL-03` wegen der großen Snapshot-/Diagnosemenge höher.

Empfehlung: Einen bounded, generation- und Content-Hash-gebundenen Body-Cache für bereits aufgelöste Typ-/Member-Kombinationen vorsehen oder einen sicher wiederverwendbaren, nicht gemeinsam mutierten Decompiler-Kontext kapseln. TTL, Cancellation, Maximalzeilen und Fehler-/Trunkierungsstatus müssen Teil des Cache-Schlüssels bzw. Ergebnisses bleiben.

Abgrenzung: Kein Funktionsfehler und kein Vorschlag, die Signatur-Snapshots generell mit Bodies zu materialisieren; die aktuelle Trennung `decompiledSignatureOnly` plus `decompiledBodyOnDemand` bleibt sinnvoll.

## Findings — Missing Feature

Keine zusätzlichen Missing-Feature-Findings mit ausreichender Evidenz. Die geprüften Anforderungen sind grundsätzlich modelliert: `DecompiledDocument` besitzt Typ-/Token-Felder, `AssemblyRoslynSnapshot` besitzt Solution/Project/Compilation/Documents/Origins, `AssemblyOrigin` unterscheidet Snapshot-/Projektpfad, Trust, Statusnähe und Content-Modus, und `AnalysisSymbolIdentity` bindet IDs an Content-Hash und Generation. Der Cache-Datenverlust und die Konstruktor-ID-Diskrepanz sind deshalb als Bugs erfasst; die gewünschte Source-backed-Abgrenzung ist als bestehender, aber in `GIT-01` nicht erfolgreich nachgewiesener Pfad dokumentiert.

## Semantische Schlussfolgerungen und Unsicherheiten

- Die metadata-only-Garantie ist statisch klar: `AssemblyDecompilationAdapter.CreateDecompiler` erhält standardmäßig keine Member-Bodies; `AssemblyRoslynWorkspaceFactory` erstellt einen `AdhocWorkspace`, fügt nur `SourceText` und Metadatenreferenzen ein und verwirft den Workspace bei Fehlern. Die Assembly wird nicht geladen oder ausgeführt.
- Die dekompilierten Dokumente werden aus Top-Level-Metadatenhandles ausgewählt, budgetiert, als C# geparst und bei leerer/zu großer/ungültiger Syntax übersprungen. Compiler-generierte Nested Types und State-Machine-Attribute werden aus dem Snapshottext entfernt.
- Die Signatur-Snapshots können erwartbare deklarations-only-Diagnosen `CS0501` sichtbar machen; `AssemblyDiagnosticCodes.IsExpectedDeclarationOnlyDiagnostic` erkennt aktuell nur `EmptyEventAccessor` und `EmptyMemberBody`. Das erklärt die beobachteten synthetischen Workspace-Diagnosen und ist nicht mit echter Source-Vollständigkeit gleichzusetzen.
- Body-Matching berücksichtigt enthaltende Typen, Konstruktoren, Methoden, Properties, Events, Parameteranzahl, `ref`/`out`/`in` und normalisierte Typnamen. Die Abdeckung für `ref readonly`, `scoped`, `params`, Extension-`this` und weitere moderne Parametermodifikatoren ist aus dem aktuellen Matcher nicht ableitbar und bleibt eine offene Unsicherheit; dafür wurde kein sicherer positiver oder negativer Fall konstruiert.
- Die Assembly-Abfragen zeigten generische Rückgabetypen, generische Methodensignaturen, Attribute und strukturierte Parameterdaten. Wegen `partial`-Status, fehlenden/versionsabweichenden Referenzen sowie Response-Budgets wurde keine Vollständigkeitsbehauptung über alle generischen Constraints/Attribute der Fallassemblies abgegeben.
- `source-backed` ist im aktuellen Code mit `confidence=high`, verifiziertem Trust, Snapshot-Identität und leerem generiertem Dokumentpfad modelliert. `decompiled` setzt dagegen `confidence=medium`, `trust=untrusted`, `contentMode=decompiledSignatureOnly` und `bodyAvailability=onDemand`. Die Fallmatrix liefert in diesem Lauf nur die zweite Klasse, mit Ausnahme des Negativfalls ohne Snapshot.
- Die Cache-Hit-Daten wurden über statische MCP-Symbolkörper beurteilt; wegen des Read-only-Vertrags wurde kein Cache erzeugt, gelöscht oder verändert und kein Testlauf durchgeführt. Der Cache-Befund ist daher ein Codepfadnachweis, kein ausgeführter Roundtrip-Test.

## Kurzer Hand-off

Erstellt wurde nur dieser Bericht; die task-lokale `code-map.md` wird als einziger nächster Artefakt-Schritt aktualisiert. Ausgeführt wurden: vollständige Vorgabedatei-/Tasklektüre, Projekt-Index-/Baum-/Health-Abfragen, MCP-Symbol-/Struktur-/Body-Abfragen für die Epic-2-Kernpfade, `inspect_assembly` und `find_assembly_extensions` für die fünf Labels, gezielte Assembly-Skeleton-/Symbol-/Body-Gegenproben sowie read-only Text-/Testdateiinspektionen. Ergebnis: metadata-only und Herkunftssignale grundsätzlich vorhanden; drei Bugs und eine Optimierung oben belegt; kein source-backed Nachweis für `GIT-01`; `FALSE-01` recoverable abgewiesen. Keine Builds, Tests, Produktions-/Konfigurations-/Produktdokumentationsänderungen oder Commits.

