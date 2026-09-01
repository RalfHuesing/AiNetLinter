---
status: ready
---

# Konzept: Robuste Analyse dekompilierter .NET-Assemblies

## Ziel und Nutzen

Der MCP-Server soll lokale .NET-Assemblies auch dann zuverlässig und kontextsparsam nutzbar machen, wenn nur dekompilierte Stubs verfügbar sind, Referenzen fehlen oder eine konfigurierte Quellzuordnung nur teilweise geladen werden kann. Ein Agent muss Analysezustand, Datenvollständigkeit und sicheren Folgeschritt maschinenlesbar erkennen können, ohne Antworttexte zu parsen oder auf temporäre Cache-Dateien zuzugreifen.

Die Lieferung korrigiert nachgewiesene Vertragsverletzungen (Filter, Trunkierung, Folge-IDs, Antwortgröße), verbessert die Transparenz von Decompilation und Source-Backing und bereinigt widersprüchliche Tool-Dokumentation. Sie ändert weder Produktivlogik des analysierten Systems noch lädt oder führt sie fremde Assemblies aus.

## Verifizierte Evidenz

Alle C#-Befunde wurden MCP-first gegen den geladenen Projekt-Key `C:\Daten\Entwicklung\Ralf\AiNetLinter` geprüft. `get_server_health` meldete am 31.08.2026 AiNetLinter 1.0.157 im Daemon-Modus mit geladenem Projekt.

| Befundbereich | Live-/Code-Evidenz | Disposition |
| --- | --- | --- |
| Gefilterte Assembly-Inspektion | `inspect_assembly` auf `Sagede.OfficeLine.Wawi.BelegEngine.dll` mit `typeName="Beleg"`, `maxResults=1`, `maxMembers=3` erzeugte 18.077 Byte Text. Trotz Filter enthielt der Text 32 Referenzen und 32 Referenz-Sessions; der nachträglich komprimierte JSON-Payload wies nur noch 4 bzw. 1 aus. | P1 – beheben |
| Receiver-Filter | `find_assembly_extensions` auf `San.OfficeLine.Core.dll` ergab ohne Filter und mit `receiverType="Receiver_404"` jeweils 188 Treffer; der erste Treffer besitzt den Receiver `Sagede.OfficeLine.Wawi.BelegEngine.Beleg`. | P1 – beheben |
| Navigationsdiagnosen | `find_references(includeReferences=true, maxResults=1)` für `GetSanIsDirty` lieferte 0 Aufrufstellen, aber 19.557 Byte Diagnose-Text. | P1 – beheben |
| `get_file_tree` | `view="summary", maxResults=20` lieferte weiterhin 181 Verzeichniseinträge, darunter Tiefe 6. `treeDepth` wird im Scanner nicht verwendet. | P1 – beheben |
| Globales Health | Nach der Referenzexpansion enthielt der parameterlose Health-Call 113 Assembly-Sessions und 49.332 Byte Text. | P2 – beheben |
| Folge-ID und Pfad | `find_symbol` für `GetSanIsDirty` gab nur den relativen generierten Dateinamen und die Signatur aus. Ein anschließendes `get_symbol_body` mit der ausgegebenen vollqualifizierten Signatur endete mit `SYMBOL_NOT_FOUND`. | P1 – beheben |
| Strukturfelder | Die Extension-DTOs besitzen im Modell Parameter-, Generic- und Constraint-Felder, doch der Live-Payload eines Einzeltreffers enthielt nach der Budgetkompaktierung nur `namespace`, `declaringType`, `name`, `signature`, `receiverType`, `applicability`. | P1 – als Teil der Budgetkorrektur beheben |
| Quell-Checkout | `ExternalSourceSnapshotMaterializer.OpenSolutionAsync` verwirft jede `WorkspaceFailed`-Meldung, auch wenn `OpenSolutionAsync` danach Projekte liefern könnte. Im aktuellen Projekt ist keine passende externe Source-Zuordnung konfiguriert; der konkrete Checkout-Fall ist daher nur statisch, nicht mit dem aktiven Key, reproduziert. | P1 – gezielt beheben |
| Fehlervertrag | `McpToolResults.Recoverable` setzt gemäß `Mcp/IsErrorPolicy.md` bewusst `isError=false`; Fehler sind aber text-only. Der `isError`-Befund ist daher **nicht** zu übernehmen, ein typisierter Fehlerpayload dagegen schon. | P1 – teilweise beheben |

Für die Konzentration auf den vorliegenden Scope werden keine Cache-Pfade als API vorgesehen: sie sind flüchtige interne Implementierungsdetails und in anderen Agenten-Umgebungen nicht zwingend lesbar.

## Betroffene Bereiche

- Assembly-Erzeugung und Source-Selection: `src/AiNetLinter/Mcp/Assemblies/ExternalSource/Repository/ExternalSourceSnapshotMaterializer.cs`, `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblySourceSelectionOrchestrator.cs`, `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisResponse.cs`.
- Assembly-Antwortvertrag: `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisResponseBudgetCompactor.cs`, `AssemblyAnalysisResponseLimits.cs`, `AssemblyAnalysisModels.cs`, `AssemblyAnalysisService.cs`, `InspectAssemblyTool.cs`, `InspectAssemblyFormatter.cs`, `FindAssemblyExtensionsTool.cs`.
- Navigation und Symbolkörper: `src/AiNetLinter/Core/Documents/SolutionDocumentPathResolver.cs`, `src/AiNetLinter/Mcp/Tools/SymbolGraph/FindSymbolTool.cs`, `src/AiNetLinter/Mcp/Tools/GetSymbolBodyTool.cs`, `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyDecompilationAdapter.cs`.
- Projektstruktur und Health: `src/AiNetLinter/Mcp/Tools/FileStructure/GetFileTreeScanner.cs`, `GetFileTreeTool.cs`, `GetHotspotsTool.cs`, `GetHotspotsScanner.cs`, `src/AiNetLinter/Mcp/Tools/ServerMaintenance/GetServerHealthResponseBuilder.cs`.
- Weitere MCP-Verträge: `McpToolResults.cs`, `AnalysisTargetResolver.cs`, `AssemblyAnalysisRegistry.cs`, `GetClassStructureTool.cs`, `GetCallTreeTool.cs`, `GetTypeHierarchyTool.cs`, `MetricsTree/MetricsTreeTool.cs`, `ReloadConfigTool.cs` und die jeweiligen Registrierungen.

## Zielvertrag und Muss-Kriterien

1. Jede Assembly-Antwort enthält Herkunft, Trust, Vollständigkeit, Session-Generation sowie gegebenenfalls einen maschinenlesbaren `fallbackReason`. Ein Fallback darf nicht mehr stillschweigend erfolgen.
2. Text und `structuredContent` beschreiben dieselbe Auswahl. Für jede gekappte Liste gilt: `shownCount == Array-Länge`, `totalCount >= shownCount`, `truncated` und der konkrete Grund sind korrekt.
3. Eine gezielte `inspect_assembly`-Abfrage gibt ohne ausdrücklichen Detailwunsch keine vollständigen Referenz- und Referenz-Sessionlisten aus. Die Gesamtsummen bleiben sichtbar.
4. `find_references` und `get_call_tree` geben Diagnosen in Text und JSON nur begrenzt als repräsentative Samples aus und weisen Gesamtzahl sowie Trunkierungsgründe aus.
5. `receiverType` schränkt `find_assembly_extensions` unabhängig von einer Consumer-Projektauflösung syntaktisch auf den ersten Extension-Parameter ein. Die semantische `applicability` bleibt bei fehlendem Consumer weiterhin `not_decidable`.
6. Für Assembly-Ziele gibt `find_symbol` eine generationsgebundene, direkt weiterverwendbare Symbol-ID aus. Relative dekompilierte Dokumentpfade müssen zuverlässig aufgelöst werden; mehrdeutige Dateinamen dürfen nicht geraten werden.
7. `get_symbol_body` unterscheidet klar zwischen vorhandenem Source-Body, einer on-demand dekompilierten Implementierung und einem tatsächlich bodylosen Symbol. Es darf nie einen Stub kommentarlos wie einen vollwertigen Methodenrumpf ausgeben.
8. `get_file_tree.treeDepth` wirkt wie dokumentiert; `summary` bleibt unabhängig von der Verzeichnisanzahl kompakt und respektiert seine Ausgabegrenze.
9. Der parameterlose Health-Call ist konstant klein und aggregiert Sessions. Detail-Sessionlisten sind ein expliziter Abruf; ein zielgebundener Health-Call bleibt für eine einzelne Session detailliert.
10. Public Tool-Schemas, Beispiele und Beschreibungen stimmen mit dem Runtime-Vertrag überein; `.dll` und verwaltete `.exe` werden als Assembly-Ziele unterstützt.

## Nicht-Ziele und Scope-Grenzen

- Keine Ausführung, kein `AssemblyLoadContext` und keine Reflection-basierte Aktivierung von Drittanbieter-Assemblies.
- Keine Veröffentlichung absoluter Cache-, Checkout- oder generierter Temp-Pfade als Agenten-Schnittstelle.
- Kein generischer `AdhocWorkspace`-Fallback für einen fehlerhaften Git-Checkout in diesem Paket. Ein solcher Fallback verlöre Projektgrenzen und Referenzbindung; er wäre ein eigener, explizit zu attestierender Degradationsmodus.
- Keine Umstellung der dokumentierten `isError`-Policy: erwartbare Nutzer-/Argumentfehler bleiben `isError=false`, erhalten jedoch zusätzlich einen typisierten Fehlerpayload.
- Kein blindes Löschen der im Audit gemeldeten Low-Confidence-Member aus Win32-Interop-Strukturen oder intern testbaren Verträgen.
- Keine neue Sortieroption für `get_hotspots`: die Ausgabe ist bereits deterministisch nach Zeilenzahl sortiert. Nur Ergebnislimit und Schwelle sind echte Lücken.

## Betriebs-, Sicherheits- und Lebenszeitmodell

Fremde DLLs und ihre Metadaten bleiben untrusted input. Der Server verarbeitet sie ausschließlich über die bestehende Metadata-/Decompiler-Strecke, unter vorhandenen Größen-, Zeit-, Abhängigkeits- und Cancellation-Limits. Ein on-demand Body-Abruf arbeitet nur innerhalb einer vorhandenen read-only Assembly-Lease; diese Lease hält Assemblypfad, Referenzauflösung und Sessiongeneration bis zum Ergebnis gültig und wird danach wie heute freigegeben.

Ein Source-Snapshot ist nur `source-backed`, wenn Checkout, Attestierung und verwertbarer Roslyn-Solution-Snapshot vorhanden sind. Workspace-Diagnosen werden nicht versteckt: Bei weiter analysierbaren C#-Dokumenten führen sie zu `partial` plus Samples; ohne nutzbare C#-Dokumente erfolgt ein dekompilierter Fallback mit Grund. Trust wird nie aufgrund einer bloßen lokalen Dateipräsenz hochgestuft.

Antwortgrößen sind Bestandteil des Betriebsvertrags. Die Auswahl und zugehörigen Zähler werden vor Textformatierung und JSON-Serialisierung einmal bestimmt. Eine nachträgliche JSON-Manipulation darf nicht mehr Fakten löschen, die der bereits formatierte Text behauptet. Zeitintensive Detaildaten werden durch explizite Flags angefordert, nicht durch unbeschränkte Standardantworten.

## Umsetzungspakete

### Paket 1 – Vertragsintegrität und P1-Korrektheit

**Intention:** Falsche oder nicht weiterverwendbare Tool-Ergebnisse zuerst korrigieren, bevor zusätzliche Analyseinformationen eingeführt werden.

1. **Typisierte Fehler ohne Policy-Bruch**
   - In `src/AiNetLinter/Mcp/McpToolResults.cs` ein internes `McpErrorPayload` einführen (`code`, `message`, `context`, `hint`, `recoverable`, optionaler Target-Kontext) und in `BuildResult` für `Error` **und** `Recoverable` serialisieren.
   - `AssemblyAnalysisResponse.Unsupported` in `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisResponse.cs` muss den gleichen Payload hinter seinem Assembly-Header behalten.
   - Für fehlende CLI/PE-Metadaten in `AssemblySessionManager` und `AssemblySessionResultFormatter` eine fachliche, nicht Record-`ToString()`-basierte Meldung mit dem Hint „verwaltete .NET-.dll oder .exe mit IL erforderlich“ ausgeben.
   - `isError` bleibt exakt gemäß `src/AiNetLinter/Mcp/IsErrorPolicy.md` unverändert.

2. **Budgetkompaktierung als typisierte Projektion statt JSON-Surgery**
   - `AssemblyAnalysisResponseBudgetCompactor.Compact` und `AssemblyAnalysisResponseLimits.EnsureStructuredContentBudget` durch eine Vorformatierungs-Projektion ersetzen. Die Auswahl von Types, Members, Extensions, Parametern, Referenzen, Sessions und Diagnosen geschieht im jeweiligen Tool, bevor `McpToolResults.Text(text, payload)` aufgerufen wird.
   - `AssemblyAnalysisResponse.Enrich` darf anschließend Metadaten ergänzen, aber keine Fachdatenarrays oder Pflichtfelder mehr kürzen. `AssemblyAnalysisResponseBudgetCompactor` wird entweder auf reine optionale Diagnose-Samples reduziert oder entfernt, wenn alle Producer die gemeinsame Projektion verwenden.
   - Eine sichtbare Extension bzw. ein sichtbarer Member behält stets die strukturierten Felder `parameters`, `genericParameters` und `constraints`. Reicht das Budget nicht, wird die Zahl sichtbarer Items reduziert und über `shownCount`, `totalCount`, `truncated` und `truncatedBy=["responseBudget"]` deklariert.
   - `InspectAssemblyFormatter.FormatText` muss aus derselben Projektion formatieren. Die derzeitige Signatur in `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/InspectAssemblyFormatter.cs` erhält dazu einen Anzeige-/Detailmodus statt nur `publicOnly`.

3. **Receiver-Filter reparieren**
   - `AssemblyExtensionSearchOptions` in `AssemblyAnalysisModels.cs` um `ReceiverType` erweitern; `FindAssemblyExtensionsTool.BuildResult` übergibt `arguments.ReceiverType`.
   - In `AssemblyAnalysisService.FindExtensions` nach Namespace und Name einen `MatchesReceiverType`-Filter auf `pair.Method.Parameters[0].Type` anwenden.
   - Die Normalisierung entfernt nur Darstellungspräfixe wie `global::`. Ein unqualifizierter Suchwert wird gegen `ITypeSymbol.Name`, ein qualifizierter gegen den C#-Fehlerformatnamen verglichen; kein unsicheres `EndsWith` und keine case-insensitive Semantik.

4. **Folge-ID und Dokumentpfad reparieren**
   - `FindSymbolTool.FormatSymbolLocationEntries` erweitert `SymbolLocationEntry` um die für Assembly-Sessions mit `AnalysisSymbolIdentity.Format(...)` erzeugte generationsgebundene ID. Text zeigt sie als kopierbares `id:`; JSON führt sie als eigenes Feld.
   - `SolutionDocumentPathResolver.Find` darf relative Pfade nicht mehr gegen das Prozess-CWD auflösen. Es vergleicht zuerst normalisierte Dokumentpfade und danach nur eindeutige virtuelle/generated Document-Namen innerhalb der aktuellen Solution. Mehrere Kandidaten ergeben weiterhin keinen Treffer bzw. einen deutlichen Recoverable-Hint.
   - `GetFileSkeletonTool` und `GetSymbolBodyTool` konsumieren die generierte ID unverändert; der aktuelle End-to-End-Vertrag ist in `src/AiNetLinter.FastTests/Mcp/Assemblies/AssemblyAnalysisPathContractTests.cs` zu erweitern.

5. **`get_file_tree`-Parameter und Summary-Vertrag**
   - In `GetFileTreeScanner.Scan` ist die effektive Tiefe `input.MaxDepth ?? input.TreeDepth`; der Wert `0` bedeutet Root-Ebene und nicht „unbegrenzt“. `maxDepth` hat bei gleichzeitiger Angabe Vorrang; Registrierung und Doku machen dies explizit.
   - `FileTreeAccumulator.Build` trennt vollständige Aggregation von ausgegebenen Directory-Entries. In `summary` werden nur Summary-Zahlen und begrenzte Top-Level-Aggregate ausgegeben; `maxResults` begrenzt auch sichtbare Verzeichnisse. `tree` bleibt für einen Drill-down zuständig und weist Verzeichnis-Trunkierung separat aus.

### Paket 2 – Progressive Disclosure, Diagnosen und Health

**Intention:** Kontextfenster vor Metadatenrauschen schützen, ohne Vollständigkeit oder Wahrheit über die Analyse zu verlieren.

1. **Gezielte `inspect_assembly`-Antworten**
   - `InspectAssemblyArguments` und `AssemblyAnalysisToolRegistrations` erhalten `includeReferences` (Default `false` bei spezifischem Type-/Member-Filter, sonst kompatibler Detailmodus). Alternativ ist ein expliziter `detailLevel` zulässig, wenn er alle Assembly-Tools einheitlich abdeckt; es darf nicht beides parallel entstehen.
   - Bei zielgerichteten Abfragen führt der Payload nur `referenceSummary` und `referenceDetailsIncluded=false`; Referenzlisten und Sessions werden erst bei explizitem Detailwunsch ausgegeben. Der Text nennt nur Zähler und den Folgeaufruf.
   - Für die bestehende gefilterte Sage-Abfrage wird als Akzeptanzgrenze ein Text- und JSON-Payload von jeweils höchstens 8 KiB festgelegt, solange kein Detailflag gesetzt ist.

2. **Navigation-Diagnosen zentral begrenzen**
   - `TransitiveCallGraphFormatter` sowie `AssemblyGetCallTreeTool` verwenden eine gemeinsame, begrenzte Diagnoseprojektion statt `AddRange(...)` beziehungsweise `.Take(100)`.
   - Text zeigt höchstens fünf normalisierte Samples; Structured Content enthält dieselbe Sampleliste plus `totalCount` und `truncatedBy`. Vollständige Rohdiagnosen werden nicht alternativ im JSON versteckt, weil das das Antwortbudget nur verschiebt.
   - Die Referenzabfrage für `GetSanIsDirty` mit 0 Calls wird auf höchstens 8 KiB bei `maxResults=1` begrenzt und behält `completeness=partial` sowie die 32-Session-Grenze sichtbar bei.

3. **Kompakter globaler Health-Call**
   - `GetServerHealthResponseBuilder.Build` erzeugt ohne Target eine Aggregation (`totalAssemblySessions`, Status- und Diagnosezähler, begrenzte Statusverteilung), keine Liste aller Sessions.
   - `GetServerHealthOptions` und Registrierung erhalten `includeSessions` (Default `false`) und ein serverseitig gedeckeltes `maxSessions`. Ein konkretes Assembly-Target darf weiterhin die eine passende Session detailliert ausgeben.
   - Payload und Text kennzeichnen Unterdrückung/Trunkierung. Der Live-Fall mit 113 Sessions muss ohne `includeSessions` unter 8 KiB bleiben.

4. **Bestehende strukturierte Erfolgspayloads vervollständigen**
   - `GetCallTreeTool` erhält für Root-only denselben oder einen klar kompatiblen `CallTreePayload` wie der Assembly-Zweig.
   - `GetTypeHierarchyTool`, `MetricsTreeTool` und `ReloadConfigTool` bekommen DTOs, die jeweils die bereits gezeigten Fakten abbilden; der Text bleibt additiv erhalten. Keine Top-Level-Arrays, damit der MCP-Schema-Vertrag erhalten bleibt.
   - DTO-Felder werden aus strukturierten Scannerergebnissen aufgebaut, nicht durch erneutes Parsen des Markdown-Textes.

### Paket 3 – Source-Backing und brauchbare Body-/Metadata-Navigation

**Intention:** Quell- und Decompilationsergebnisse offen unterscheiden und nutzbare Folgeaufrufe ermöglichen.

1. **Source-Snapshot robust materialisieren und Fallback erklären**
   - `ExternalSourceSnapshotMaterializer.OpenSolutionAsync` sammelt Workspace-Diagnosen statt nur ein Boolean zu setzen. Nach `OpenSolutionAsync` gilt: keine Projekte oder keine C#-Dokumente ist ein Materialisierungsfehler; verwertbare Dokumente mit Diagnose führen zu einem als `partial` markierten Source-Snapshot mitsamt begrenzter Diagnosezusammenfassung.
   - `AssemblySourceSelectionOrchestrator.ResolveAsync` erzeugt für Konfigurationsfehler, fehlenden/mehrdeutigen Mapping-Treffer, untrusted Snapshot und Provider-/Workspace-Fehler einen stabilen `fallbackReason` und übernimmt sichere Diagnosecodes in die Selection.
   - `AssemblyAnalysisResponse.Enrich` projiziert `fallbackReason` und die gekürzte Source-Diagnosesummary in Header und Structured Content. Die Decompilation bleibt der Fallback, nicht eine als Erfolg maskierte Source-Analyse.
   - Kein Adhoc-Fallback in diesem Paket. Erst ein separates Konzept darf den Verlust von Projektzuordnung, Referenzen und Trust für einen Dateikatalog-Workspace erlauben.

2. **On-demand Body-Dekompilation**
   - Der initiale Assembly-Aufbau in `AssemblyDecompilationAdapter.CreateDecompiler` behält `DecompileMemberBodies=false`.
   - Die Adapter-/Entry-Factory-Strecke erhält eine leasegebundene Body-Resolver-Fähigkeit. Sie ordnet eine gültige `AnalysisSymbolIdentity` dem deklarierenden Metadata-Type zu, erstellt mit denselben `AssemblyReferenceResolution`-Daten einen zweiten Decompiler mit Bodies und extrahiert nur das angeforderte Symbol aus dem dekompilierten umschließenden Type.
   - `GetSymbolBodyTool.RenderSingleSymbolAsync` fragt diese Fähigkeit nur für dekompilierte Assembly-Symbole ab. Source-backed Symbole behalten den Roslyn-Body; `abstract`, `extern`, Interface-Member und nicht dekompilierbare Fälle liefern einen expliziten `bodyAvailability`-/Hinweis statt eines irreführenden Stubs.
   - Body-Text bleibt durch `maxBodyLines` begrenzt, verarbeitet Cancellation und gibt niemals interne Cache-Pfade aus.

3. **Enum- und Ladezustandsdaten**
   - `GetClassStructureTool.ExtractMembers` formatiert `IFieldSymbol.HasConstantValue` mit einem zentralen C#-Literal-Formatter (invariant, Strings gequotet, `null`, `char`, negative Werte korrekt) in `Signature` und Structured Content.
   - Statt eines pauschal auf `true` gesetzten `metadataOnly`-Flags erhält `AssemblyResponseMetadata` ein semantisches `bodyAvailability`/`contentMode` (`source`, `decompiledSignatureOnly`, `decompiledBodyOnDemand`). Das unterscheidet Quelle, Stub und späteren Body-Abruf zutreffend.

### Paket 4 – Kompatibilität, kleinere API-Lücken und Dokumentation

**Intention:** Die korrigierte Analyse ist auffindbar, konsistent dokumentiert und unterstützt übliche .NET-Artefakte.

1. **`.exe` als verwaltetes Assembly-Ziel**
   - Eine gemeinsame, interne Prüffunktion für erlaubte Assembly-Erweiterungen nutzen; keine vier getrennten `.dll`-Abfragen. Betroffen sind `AnalysisTargetResolver`, `AssemblyAnalysisService`, `ExternalSourceMappingValidator` und `AssemblySourceMatchResolver`.
   - Zulässig sind nur `.dll` und `.exe`; Existenz- und Metadatenprüfung bleiben unverändert. Der Fehlertext nennt beide erlaubten Formen.

2. **Registry-Pfadidentität mit klarer Vorbedingung**
   - `AssemblyAnalysisRegistry.LeaseAsync` verwendet bereits `Path.GetFullPath`; daraus folgt keine Reparse-/8.3-Kanonisierung. Vor einer Änderung muss ein reproduzierbarer Alias-Test zeigen, dass zwei physische Schreibweisen zwei Generationen anlegen.
   - Erst dann eine Windows-spezifische, getestete finale Handle-Pfadauflösung an einer zentralen Stelle einführen. Ohne Test bleibt dieser Befund als P2-Projekt-Technical-Debt zurückgestellt, damit kein falsches Reparse-Verhalten eingeführt wird.

3. **`get_hotspots` und konsistente Parameterbenennung**
   - `GetHotspotsTool`/Registrierung erhalten `maxResults` und `minLinePercentage`; vorhandene Sortierung nach absteigender Zeilenzahl wird beibehalten und dokumentiert.
   - In `FeatureContextToolRegistrations`, `TestContextToolRegistrations`, `Docs/agent-api.md` und den Agentenhinweisen `symbolIdentifier` als primären Namen ausweisen. Bestehende Aliase bleiben kompatibel.

4. **Dokumentation aktualisieren**
   - `Docs/agent-api.md`: das JSON-RPC-Beispiel für `find_symbol` um `targetType` und absoluten `targetPath` ergänzen; Health-Abschnitt „Projekt- **oder** Assembly-Target“ korrigieren; Assembly-Detailflags, `bodyAvailability`, `.exe`-Support und strukturierte DTOs dokumentieren.
   - `src/AiNetLinter/Mcp/Registration/FileStructureToolRegistrations.cs`: Textdefault von 100 auf den tatsächlichen `GetFileTreeTool.DefaultMaxResults` (200) korrigieren.
   - `Docs/integration.md`: Progressive-Disclosure-Regel konkret ergänzen (für breite Listen klein starten, semantisch verfeinern, Detailflags bewusst setzen). `Docs/configuration.md` nur ändern, wenn die Umsetzung eine neue persistente Konfiguration einführt; aktuell sind ausschließlich Tool-Argumente geplant.

## Befundregister

| Themencluster | Ursprungsbefunde | Priorität / Umfang / Impact | Entscheidung |
| --- | --- | --- | --- |
| Source-Backing und Transparenz | EXTSRC-001, MF-006 | P1, systemisch, echte Quelle wird fälschlich verworfen bzw. Fallback bleibt unsichtbar | Paket 3 |
| Text-/JSON-Drift und Payloadverlust | TOK-001, TOK-002, MF-002, MF-003, MF-007 | P1, systemisch, fehlerhafte Zähler und hohe Tokenkosten | Paket 1–2 |
| Fehlerpayload | MF-001, ERR-002 | P1, systemisch, maschinelle Fehlerbehandlung fehlt | Paket 1; `isError`-Kritik verworfen |
| Receiver-Filter | ASM-002, MF-004 | P1, lokal, falsche Kandidatenlisten | Paket 1 |
| Symbolpfade und Folgeaufrufe | MF-005, NAV-002, NAV-004 | P1/P2, mehrere Tools, Agent kann gefundenes Symbol nicht zuverlässig weiterverwenden | Paket 1 und 3 |
| Datei-Discovery | MF-009, MF-011, DOC-002 | P1/P2, lokal, Parameter und kompakte Übersicht widersprechen Vertrag | Paket 1 und 4 |
| Health und Erfolgspayloads | MF-008, MF-010 | P1/P2, mehrere Tools, große bzw. text-only Antworten | Paket 2 |
| Enum-Werte / Ladeart | NAV-003, MF-013 | P2, lokal, Daten fehlen bzw. Zustand ist nur indirekt ableitbar | Paket 3 |
| Assembly-Formate | FEAT-001 | P2, mehrere Validierer, verwaltete Executables ausgeschlossen | Paket 4 |
| Registry-Alias | MF-012 | P2, systemisch, bislang nicht reproduzierter Ressourcenverlust | zurückgestellt bis Alias-Test |
| Hotspots und Dokumentation | MET-001, DOC-001, DOC-002, MF-014, MF-015, SRC-001 | P2/P3, Tool-Ergonomie und Onboarding | Paket 4 |
| Git-Hygiene | ORCH-001 | P2, Prozess, kein Produktivcode | beim Commit dieser Lieferung: nur explizite Taskpfade stagen |
| Cache-Pfade | Architekturfrage | P3, Schnittstellenkapselung | bewusst verworfen |

## Test- und Verifikationsvertrag für die spätere Umsetzung

Die heutige Konzeptarbeit führt gemäß Auftrag weder Build noch Tests aus. Nach Implementierung sind zusätzlich zu den regulären Abschluss-Gates diese gezielten Fälle erforderlich:

- `AssemblyAnalysisToolTests.cs`: Receiver ohne Treffer gegenüber vollqualifiziertem/kurzem Receiver, vollständige Parameter-/Generic-/Constraint-Felder eines sichtbaren Items, gefilterte Inspection ohne Referenzdetails und korrekte Summary-Zähler.
- Neue bzw. erweiterte Tests für `AssemblyAnalysisResponseLimits`/Budgetprojektion: Text und JSON stimmen bei jeder Kappung überein; Pflichtfelder eines sichtbaren Items gehen nicht verloren.
- `FindReferencesTransitivePositionTests.cs`, `GetSymbolBodyToolTests.cs` und `AssemblyAnalysisPathContractTests.cs`: `find_symbol`-ID ist direkt in `get_symbol_body` nutzbar, relative generated paths lösen eindeutig auf, on-demand Body/Stub/abstract unterscheiden sich.
- `GetFileTreeScannerTests.cs`: `treeDepth=0/1/2`, Priorität von `maxDepth`, kompakte Summary mit gekappten Verzeichnisentries und korrekte Completeness.
- `ExternalSourceSnapshotMaterializerTests.cs` sowie `AssemblyAnalysisToolSupportTests.cs`: Workspace-Diagnose mit nutzbaren C#-Dokumenten, fehlende C#-Dokumente, jeder `fallbackReason`, Trust bleibt fail-closed.
- `GetServerHealthToolTests.cs`: globales Aggregat bei vielen Sessions, explizite Sessiondetails und Grenzen; `find_references`/`get_call_tree` begrenzen Diagnose-Samples bei 0 Treffern.
- `McpToolResultsTests.cs`, Tool-Registrierungs-/Schema-Tests sowie DTO-spezifische Tests für die vier derzeit text-only Erfolgspayloads.
- Eine verwaltete Test-`.exe` und eine native PE-DLL als Fixture: erstere wird analysiert, letztere liefert den typisierten, hilfreichen Recoverable-Fehler.
- Dokumentationsbeispiele gegen aktuelle Tool-Registrierungen prüfen.

Als Gesamtabschluss gelten anschließend die Projektregeln: `dotnet build`, `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress` und `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress`. Stress-Tests nur auf expliziten Auftrag.

## Audit-Zusatzbefunde

Der read-only MCP-Audit über `src/AiNetLinter/Mcp` fand acht Near-/Fuzzy-Duplikatcluster, 37 ausschließlich Low-Confidence-Dead-Code-Kandidaten und 245 Magic-Value-Kandidaten. Keiner ist ohne zusätzliche Fachentscheidung sicher im Scope zu ändern:

- Die ähnliche Parametrisierung von `FindAssemblyExtensionsTool.CreateParameters` und `InspectAssemblyTool.CreateParameters` ist während Paket 1 erneut zu bewerten; erst bei gemeinsamer Verantwortlichkeit darf ein Helper entstehen.
- Der einzige nahe Duplikatcluster in `AssemblyReferenceResolver` und die wiederholten Cache-/Transport-Methoden benötigen separate Fehlersemantik-Prüfung.
- Dead-Code-Funde betreffen unter anderem Interop-Layoutfelder und internal Verträge mit `InternalsVisibleTo`; sie werden nicht gelöscht.
- Die Magic-Value-Treffer umfassen überwiegend bereits zentral definierte Diagnosecodes oder einmalige Cache-Vertragswerte. Die bereits geplanten Budgetkonstanten werden nur dort zentralisiert, wo sie fachlich denselben Antwortvertrag ausdrücken.

Diese Auditbefunde sind als `accepted-deferred`, nicht als Erweiterung des aktuellen Umsetzungsumfangs, zu behandeln.

## Offene Punkte und Freigabestatus

Das Konzept ist zur Umsetzung freigegeben. Nicht blockierend, aber vor Paket 4 zu entscheiden, ist nur die konkrete Windows-Strategie für Reparse-/8.3-Pfadkanonisierung; ohne Reproduktion bleibt sie aus dem Code heraus. Die Detailentscheidungen zu Budgetkonstanten, neuen optionalen Tool-Flags und DTO-Namen sind innerhalb der festgelegten Verträge umsetzbar und blockieren keinen Start.
