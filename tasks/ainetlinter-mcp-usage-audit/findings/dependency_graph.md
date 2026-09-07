# Finding: `dependency_graph`

Datum: 2026-09-07  
Anker Projekt: `T:Sample.Project.Infrastructure.Sql.DataExecutor` / Datei `Sample.Project/Infrastructure/Data/DataExecutor.cs` / `ComponentRegistrationAttribute`  
Anker Assembly: `C:\ExternalAssemblies\Version-9\Example.External.Core.dll` — `T:Example.External.Core.AI.Chat.AIProxy.Model.ChatLog` / `TableColumn` / `externalMailResult`

## 1. Schema-Kurzfazit

Beschreibung steuert den Call **im Kern richtig**: genau ein Anker — `filePath` (Alias `path`) **oder** `symbolIdentifier` (Aliase `symbol` / `identifier` / `name`), nie beide und nie keins. `direction` `incoming`|`outgoing`|`both` (Default both). `depth` Default 1, hard cap 3, max. 150 Dateien. `maxResults` Default 50. Cross-Target `project` + `assembly`.

Was in die Irre führt:

- JSON-`required` sind nur `targetType`/`targetPath`. Runtime verlangt XOR Anker — Schema hat kein `oneOf`, `direction` kein Enum.
- Kurzname `DataExecutor` ist **mehrdeutig** (Klasse + viele DI-Properties + Partial doppelt). `[ComponentRegistration]` als `ComponentRegistration` → `SYMBOL_NOT_FOUND`; der Typ heißt `ComponentRegistrationAttribute`.
- Truncation-Hinweis lautet „depth reduzieren oder maxResults erhöhen“, auch wenn `depth` schon 1 ist. `depth>3` wird still auf 3 geclampt.
- Assembly: ohne vorheriges `find_symbol` kennt der Agent keinen Typ; Fehler-Header liefert nur `Properties/AssemblyInfo.cs` (leerer Graph). Kanten enthalten **keine** `T:`-IDs.
- Cursor-Auto-Review verwechselt die XOR-Regel „Datei oder Typ“ mit `filePath`+`targetType` und blockiert gültige Truncation-/Fehlercalls.

## 2. Ausgeführte Calls

Alle Calls: `CallDynamicTool` / `user-AiNetLinter` / `dependency_graph`. Wartezeit durchweg **schnell** (kein Timeout). Projekt-`targetPath` = Workspace-Root.

| # | Parameter | Größe (ca.) | Truncation / completeness | Wartezeit |
|---|-----------|-------------|---------------------------|-----------|
| 1 | project, `symbolIdentifier=DataExecutor`, `both`, depth 1 | ~2k Fehler | `AMBIGUOUS_SYMBOL` (Properties + 2× `T:` Partial) | schnell |
| 2 | project, `DataExecutor`, `incoming` | wie #1 | wie #1 | schnell |
| 3 | project, `DataExecutor`, `outgoing` | wie #1 | wie #1 | schnell |
| 4 | project, `filePath` + `symbolIdentifier=DataExecutor` | ~250 | `INVALID_ARGUMENT` XOR | schnell |
| 5 | project, `filePath=this/path/does-not-exist/UnknownFile.cs` | ~250 | `RESOURCE_NOT_FOUND` | schnell |
| 6 | assembly, `symbolIdentifier=Core` | Header + Fehler | `SYMBOL_NOT_FOUND`; Header `completeness=partial` | schnell |
| 7 | project, `T:…DataExecutor`, `both`, depth 1 | ~4k, ~70 Zeilen | **119 Kanten, 50 gezeigt** | schnell |
| 8 | project, `filePath=…/DataExecutor.cs`, `incoming` | ~4k | **102 Kanten, 50 gezeigt**; Tests zuerst | schnell |
| 9 | project, `filePath=…/DataExecutor.cs`, `outgoing` | ~1.5k | 11 Kanten, „vollständig“ | schnell |
| 10 | project, `symbolIdentifier=ComponentRegistration` | ~200 | `SYMBOL_NOT_FOUND` | schnell |
| 11 | project, `filePath=…/DataExecutor.cs`, depth 3, **maxResults=5**, `both` | ~1.2k | **234 Kanten, 150-Datei-Cap, 5 gezeigt**; incoming **leer** | schnell |
| 12 | assembly, `filePath=Properties/AssemblyInfo.cs` | Header + leer | outgoing/incoming keine; „vollständig“ | schnell |
| 13 | project, `name=ComponentRegistrationAttribute`, `both` | ~2.5k | 22 Incoming inkl. Domain-Handler; outgoing leer; vollständig | schnell |
| 14 | project, `T:…DataExecutor`, `outgoing`, **maxResults=200** | ~2k | 17 Kanten, vollständig | schnell |
| 15 | project, **ohne** Anker | ~250 | `INVALID_ARGUMENT` XOR (beide oder keins) | schnell |
| 16 | assembly, `filePath=does/not/exist.cs` | Header + Fehler | `RESOURCE_NOT_FOUND` | schnell |
| 17 | assembly, `filePath` + `symbolIdentifier=Core` | Header + Fehler | `INVALID_ARGUMENT` XOR | schnell |
| 18 | project, `T:…DataExecutor`, `incoming`, **maxResults=200** | ~8k, ~100 Zeilen | behauptet vollständig; Tests + Admin/UI | schnell |
| 19 | project, Alias `name=ComponentRegistrationAttribute`, `incoming`, **depth=2** | ~4k | **188 Kanten, 150-Cap, 50 gezeigt** | schnell |
| 20 | project, Alias `path` Backslash, `outgoing` | ~1.5k | wie #9, vollständig | schnell |
| 21 | assembly, absoluter `generatedPath` als `filePath` | Header + leer | wie #12 | schnell |
| 22 | assembly, `symbolIdentifier=Sample.ExternalSuite` | Header + Fehler | `SYMBOL_NOT_FOUND` | schnell |
| 23 | project, `direction=sideways` | ~200 | `INVALID_ARGUMENT`, erlaubte Werte genannt | schnell |
| 24 | project, `T:…DataExecutor`, `outgoing`, **depth=4** | ~3.5k | stiller Clamp auf 3; Duplikat-Kanten; „vollständig“ | schnell |
| 25 | project, `filePath=…/AiSiteWorkspaceLayout.razor` | ~250 | `RESOURCE_NOT_FOUND` (`.razor` nicht indexiert) | schnell |
| 26 | assembly, Alias `identifier=Exception` | Header + Fehler | `AMBIGUOUS_SYMBOL` + copyable `assembly:…:P:` | schnell |
| 27 | project, Alias `symbol=T:…ComponentRegistrationAttribute`, `both` | ~2.5k | wie #13 | schnell |
| 28 | assembly, `T:…externalMailResult`, `both` | Header + leer | outgoing/incoming keine; „vollständig“ | schnell |
| 29 | assembly, `filePath=Example.External.Core.EMail/externalMailResult.cs`, `incoming` | Header + leer | vollständig, leer | schnell |
| 30 | assembly, `assembly:<sha>:11:P:…externalMailResult.Exception` | Header + leer | löst auf den **Typ** `externalMailResult`, Graph leer | schnell |
| 31 | project, `filePath=…/AiSiteWorkspaceLayout.razor.cs`, `incoming` | ~800 | 5 Test-Caller, vollständig | schnell |
| 32 | assembly, `name=TableColumn`, `both` | ~1k | outgoing leer; incoming `TabellenSchemaService` | schnell |
| 33 | assembly, `T:…ChatLog`, `both` | ~1.5k | outgoing `ChatRequest`/`ChatResponse`; incoming `ChatRequest`/`AIProxyChatService` | schnell |

## 3. Verdict

**Teilweise** wie beschrieben.

Projekt-Happy-Path mit FQN oder relativer `.cs`-Datei liefert echte SemanticModel-Kanten (DataExecutor → Contracts-SQL-Interfaces; ComponentRegistrationAttribute → Domain-/Admin-Handler). Fehlertexte zu XOR, unbekanntem Dateipfad und ungültigem `direction` sind klar und agententauglich.

Abweichend: Kurzname scheitert oder ist mehrdeutig; Datei- vs. Typ-Scope differiert (Partial `DataExecutor.OptimisticConcurrency.cs` nur im Typ-Anker); `both`+kleines `maxResults` verschluckt incoming; Depth>3 still; Depth-3-Duplikate; Assembly-Graph oft leer + „vollständig“ trotz `completeness=partial`; Kanten ohne Symbol-IDs; jede Dateiantwort hängt die volle csproj-Referenzliste an.

## 4. Schwere

**degraded**

Begründung: Mit FQN/`T:` oder `.cs`-Pfad erreicht der Agent das Ziel im Projekt. Default-50 schneidet Incoming so, dass fast nur Tests sichtbar sind. Assembly ist ohne Folgetool-Namenraten kaum startbar und behauptet Vollständigkeit bei leeren oder BCL-gefilterten Graphen. Nicht dauerhaft kaputt, aber riskant.

## 5. Nutzbarkeit

**nur mit Workaround**

- Nicht `DataExecutor` / `ComponentRegistration` / `Core` als Kurzname.
- Anker: `T:Sample.Project.Infrastructure.Sql.DataExecutor` oder `…/DataExecutor.cs`; Attribut `ComponentRegistrationAttribute`.
- `direction` getrennt (`outgoing` bzw. `incoming`), nicht `both` bei kleinem `maxResults`.
- Incoming: `maxResults` ≥ 200 oder Agent unterschätzt Produktions-Caller.
- Assembly: erst über `AMBIGUOUS_SYMBOL`/`find_symbol` einen konkreten Typ holen (`ChatLog`, `TableColumn`); `AssemblyInfo.cs` nicht als Anker.
- `assembly:<sha>:<generation>:…`-IDs nicht cachen (Generation stieg in der Session).
- `.razor` geht nicht; `.razor.cs` ja.

## 6. Bugs, False Positives / False Negatives

**Ist-Stichprobe (Tool-Antwort gegen bekanntes Platform-/Assembly-Ist, kein Vollabgleich, kein `rg` in diesem Auftrag):**

- Typ-`DataExecutor` outgoing nennt `IDataQueryExecutor`, `IExternalDataQueryExecutor`, `DataConnectionFactory`, `DataParameters` — passt zur Rolle der Klasse. **TP.**
- Datei-`DataExecutor.cs` outgoing **ohne** `SqlOptimisticMutationResult` / `SqlRowVersionEncoding`; Typ-Anker **mit**. Partial `DataExecutor.OptimisticConcurrency.cs` — **kein Bug**, aber undokumentierter Scope-Unterschied. Agent kann das als FN der Datei lesen.
- Default-Incoming (50 von 102) startet mit `Tests.Ama`/`Tests.Support`. Produktions-Handler (`PlatformServiceExtensions`, Admin-Stores) erst danach. Mit `maxResults=200` sichtbar. **FN durch Truncation/Ranking, nicht durch Roslyn-Miss.**
- Domain-Handler fehlen im DataExecutor-Incoming — architektonisch plausibel (Contracts-`IDataQueryExecutor`, nicht Platform-Typ). Kein FN behauptet.
- `ComponentRegistration` → NOT_FOUND; `ComponentRegistrationAttribute` incoming enthält `CompanyCalendarHandler`, `EmployeeScheduleHandler`, `ComponentRegistrationAssemblyScanner`. **TP.**
- `#11` `both`+`maxResults=5`+depth 3: 5 outgoing (u. a. `PlatformConnectionNames`), incoming **(keine)** obwohl #8 102 Incoming hat. **Anzeige-Bug:** Kontingent geht an outgoing.
- `#24` depth 4 → 3: `DataExecutionScope.cs` mehrfach, dieselben Typen wiederholt. **FP/Duplikat** der Traversierung.
- `.razor` NOT_FOUND, `.razor.cs` 5 Test-Caller von `AiSiteWorkspaceLayout`. **C#-only, undokumentiert.**
- Assembly `TableColumn` incoming → `TabellenSchemaService` (1 Typ). Passt zum bekannten Dekompilat-Ist. Outgoing leer (BCL vermutlich gefiltert, ungesagt). **TP incoming / undokumentierter FN outgoing.**
- `externalMailResult` / `AssemblyInfo` leer + „vollständig“ bei Header `completeness=partial`. **Widersprüchliche Completeness.**
- `ChatLog` outgoing `ChatRequest`/`ChatResponse`, incoming `ChatRequest`/`AIProxyChatService`. **TP**, einziger wirklich nützlicher Assembly-Happy-Path.
- `P:`-Assembly-ID zeigt den enthaltenden Typ, nicht die Property-Abhängigkeit. **Irreführend, kein Absturz.**
- Partial `DataExecutor` in Ambiguity **zweimal** `T:` (`.cs:9` und `.OptimisticConcurrency.cs:11`). Kosmetischer FP.

## 7. Token / IDs

- Erfolgszeile: `relativerPfad (n Typ: Name)` — **keine** `T:`/`M:` in den Kanten. Folgetool muss den Namen selbst zu `T:` bauen oder `find_symbol` nutzen.
- `AMBIGUOUS_SYMBOL` ist der brauchbarste ID-Kanal (`T:…`, `P:…`, `assembly:<sha>:<gen>:P:…`).
- Assembly-IDs generationsgebunden (Session: generation 1→11). Copy-Paste in denselben Turn ging (#30); Cache über Calls hinweg riskant.
- Jede Projekt-Antwort hängt **alle** `ProjectReference`s der Host-csproj an (AiEngine, Buchungsserver, vier Domains) — Token ohne Bezug zum Anker.
- Default 50 bei 100+ Incoming: ~4k Tokens, die falsche Hälfte (Tests). `maxResults=200` Incoming ~8k, brauchbar.
- `both`+depth 2 auf `ComponentRegistrationAttribute`: 188 Kanten / 150-Datei-Cap / 50 gezeigt — weder vollständig noch klein.
- Kein `cursor`/`continuationToken`. Fortsetzung = `maxResults` erhöhen oder `direction` splitten.
- Completeness-Satz „kein zusätzliches Read/Grep nötig“ bei trunkierten oder leeren Assembly-Graphen ist für den Agenten falsch.

## 8. Roslyn-konforme Wünsche

- Schema: `oneOf` Datei/Typ; `direction` Enum; `depth`/`maxResults` Min/Max; Runtime-XOR im Schema.
- Kanten mit stabiler `T:`-ID + relativem Pfad; Assembly-IDs ohne Generation oder mit Auto-Rebind.
- Truncation: incoming/outgoing getrennt zählen; Hinweis „maxResults erhöhen“, nicht „depth reduzieren“ bei depth=1. `continuationToken` statt nur Limit hochsetzen.
- Incoming-Ranking: `scopeType` `production`/`tests` oder Tests nachrangig — sonst Default-50 = Testflut.
- Depth-Clamp und 150-Datei-Cap im Antworttext nennen; Duplikate auf demselben `(Datei, Typ)` unterdrücken.
- Assembly: bei leerem Graph nicht „vollständig“, wenn BCL ausgefiltert oder Session `partial` ist; Header-Pfad nicht auf `AssemblyInfo.cs` defaulten, sondern 3–5 Typnamen vorschlagen.
- `.razor` → Hinweis auf `.razor.cs`, nicht bloß `RESOURCE_NOT_FOUND`.
- `ComponentRegistration` → Hint `ComponentRegistrationAttribute`; Kurzname-Klasse vor Properties in Ambiguity bevorzugen.
- csproj-Referenzblock weglassen oder hinter Flag; Token-Leiche auf jedem Datei-Call.

## 9. Phase 3

Nicht-ok-Befunde (Schwere `degraded` / `friction` / `wish`). Pfade relativ zu `C:\Daten\Entwicklung\Ralf\AiNetLinter\`.

1. **JSON-Schema ohne XOR/`oneOf`; `direction` kein Enum** (`friction`)  
   Pfad: `src\AiNetLinter\Mcp\Registration\SymbolGraphToolRegistrations.cs`  
   Symbol: `AddDependencyGraph` / `DependencyGraphDescription`  
   Ansatz: Schema aus der C#-Signatur (`string? filePath`, `string? symbolIdentifier`, `string? direction`) hat kein `oneOf` und kein Enum. Runtime-XOR sitzt in `DependencyGraphTool.ExecuteAsync`. MCP-`Create`-Option um `oneOf` Datei/Typ und `direction` als Enum ergänzen; `ParseDirection` bleibt Defense-in-Depth.

2. **Kurzname `DataExecutor` mehrdeutig; `ComponentRegistration` → `SYMBOL_NOT_FOUND`** (`degraded`)  
   Pfad: `src\AiNetLinter\Mcp\Tools\SymbolGraph\FindReferencesTool.cs`  
   Symbol: `ResolveByNameAsync` / `IsSymbolMatch`  
   Ansatz: `SymbolFinder.FindSourceDeclarationsAsync` mit `name == lastSegment` trifft Klasse **und** Properties gleichen Namens, ohne Kind-Ranking. Attribut-Kurzform ohne Suffix gibt es nicht (Roslyn-Konvention `ComponentRegistration` ↔ `ComponentRegistrationAttribute` via `MetadataName` / beide Namen prüfen). Ranking: `INamedTypeSymbol` vor `IPropertySymbol`; bei 0 Treffern `Attribute`-Suffix anhängen und erneut suchen.

3. **Truncation-Hinweis „depth reduzieren“ bei depth=1; `both`+kleines `maxResults` verschluckt incoming** (`degraded`)  
   Pfad: `src\AiNetLinter\Mcp\Tools\DependencyGraph\DependencyGraphTool.cs`, `DependencyGraphScanner.cs`  
   Symbol: `RenderText` / `BuildResult`  
   Ansatz: `BuildResult` sortiert `outgoing` vor `incoming`, dann `Take(maxResults)` — das ganze Kontingent geht an outgoing. Footer in `RenderText` ist fest „depth reduzieren oder maxResults erhoehen“. `maxResults` **pro Richtung** zählen (oder Round-Robin); Footer nur „maxResults erhöhen“, wenn `ClampedDepth == 1`; `NodeCapReached` getrennt nennen (`MaxVisitedFiles`).

4. **Incoming-Default zeigt Tests zuerst** (`degraded`)  
   Pfad: `src\AiNetLinter\Core\TestDetector.cs`, `src\AiNetLinter\Mcp\Tools\DependencyGraph\DependencyGraphScanner.cs`  
   Symbol: `IsTestFile` / `IsTestProjectFile` / `BuildResult`  
   Ansatz: `BuildResult` will Tests nachrangig (`ThenBy(IsTestProjectFile ? 1 : 0)`), aber `IsTestFile` matcht `.Tests/` und `/tests/`, nicht Platform-Pfade `*.Tests.Ama/*.cs`. Dadurch gewinnt die Ordinal-Sortierung (`.` vor `/`). `Document.Project` über `TestDetector.IsTestProject` / `HasTestProjectNameSuffix` (kennt `.Tests.` im Projektnamen) statt nur Dateisuffix.

5. **Kanten ohne `T:`-IDs** (`wish`)  
   Pfad: `src\AiNetLinter\Mcp\Tools\DependencyGraph\DependencyGraphModels.cs`, `DependencyGraphTool.cs`  
   Symbol: `DependencyEdge` / `FormatEdgeLine` / `EdgeAccumulator`  
   Ansatz: Akkumulator hält nur `TypeNames` (`INamedTypeSymbol.Name`). Beim `AddEdge` `TryGetDocCommentId()` (`RoslynSymbolExtensions`) mitführen; Assembly-IDs über `AnalysisSymbolIdentity.Format` ohne neue Generation in der Kante cachen.

6. **`depth>3` still geclampt; Depth-3-Duplikate** (`friction` / `degraded`)  
   Pfad: `src\AiNetLinter\Mcp\Tools\DependencyGraph\DependencyGraphScanner.cs`  
   Symbol: `ScanCoreAsync` (`Math.Clamp`) / `MergeHopEdges` / `ExpandFrontierAsync`  
   Ansatz: Clamp ist absichtlich (`MaxDepth = 3`); `ClampedDepth` steht im Result, wird im Text aber nicht als „requested→clamped“ ausgewiesen (Vorbild `TransitiveCallGraphFormatter` `DepthWasClamped`). Hop≥2 wechselt auf Datei-Scope und kann dieselbe `(Datei, Typ)`-Kante erneut mergen; schließende Zyklus-Kanten bleiben bewusst. Anzeige auf `(From, To, Direction)` plus Typ-DocId deduplizieren; Clamp im Footer nennen.

7. **Assembly-Graph leer + Sufficiency „vollständig“ trotz Header `completeness=partial`** (`degraded`)  
   Pfad: `src\AiNetLinter\Mcp\Tools\DependencyGraph\DependencyGraphScanner.cs`, `DependencyGraphTool.cs`; Envelope: `src\AiNetLinter\Mcp\Assemblies\Analysis\AssemblyAnalysisResponse.cs`  
   Symbol: `IsDeclaredInSource` / `BuildResponse` / `McpSufficiencyHints.Append` / `FormatHeader`  
   Ansatz: Outgoing filtert alles ohne Source-Location (BCL/Metadata). Leerer Graph ⇒ `Truncated=false` ⇒ Sufficiency-Hint. Session-Header kommt unabhängig aus `CreateEnriched` (`status=partial`). Sufficiency nicht anhängen, wenn Session `partial` ist oder Outgoing leer und nur Metadata-Typen da waren; Filter im Text nennen. `generatedPath` nicht als Datei-Anker vorschlagen.

8. **Assembly-Header defaultet auf `AssemblyInfo.cs`** (`friction`)  
   Pfad: `src\AiNetLinter\Mcp\Assemblies\Analysis\AssemblyAnalysisSession.cs`  
   Symbol: `CreateGenerationOrigin` (`documents.FirstOrDefault()?.GeneratedPath`)  
   Ansatz: Decompiler listet `Properties/AssemblyInfo.cs` zuerst. Statt First-Document: erstes öffentliches `INamedTypeSymbol` der Compilation (`GetTypeMembers` / Export-Types) als Beispielnamen in den Fehlerhint; `generatedPath` nicht als `filePath`-Vorschlag.

9. **`P:`-Assembly-ID wird zum enthaltenden Typ** (`friction`)  
   Pfad: `src\AiNetLinter\Mcp\Tools\DependencyGraph\DependencyGraphTool.cs`  
   Symbol: `ExecuteTypeScopeAsync` (`symbol as INamedTypeSymbol ?? symbol.ContainingType`)  
   Ansatz: Absichtliche Normalisierung auf den Typ. Im Target-Header `Kind` + angeforderte vs. aufgelöste DocId ausgeben (`TryGetDocCommentId`), damit der Agent die Promotion sieht.

10. **`.razor` → `RESOURCE_NOT_FOUND` ohne Hinweis auf `.razor.cs`** (`friction`)  
    Pfad: `src\AiNetLinter\Mcp\Tools\DependencyGraph\DependencyGraphTool.cs`, `src\AiNetLinter\Mcp\McpToolResults.cs`  
    Symbol: `ExecuteFileScopeAsync` / `FileNotFound`  
    Ansatz: `DiffImpactAnalyzer.FindDocumentByPath` indexiert nur Roslyn-`Document`s (`.cs`). Bei Suffix `.razor` Hint auf Geschwister `.razor.cs` (Pfadkonvention, optional `AdditionalDocuments`), nicht nur generisches `FileNotFound`.

11. **csproj-`ProjectReferences`-Block an jeder Dateiantwort** (`wish`)  
    Pfad: `src\AiNetLinter\Mcp\Tools\DependencyGraph\DependencyGraphScanner.cs`, `DependencyGraphTool.cs`  
    Symbol: `BuildProjectReferences` / `RenderText`  
    Ansatz: `Project.ProjectReferences` wird immer gerendert. Weglassen oder hinter Flag; Token-Leiche unabhängig vom Anker.

12. **Keine Continuation; Assembly-IDs generationsgebunden** (`wish`)  
    Pfad: `src\AiNetLinter\Mcp\AnalysisSymbolIdentity.cs`; Truncation: `BuildResult`  
    Symbol: `Format` / `TryParse` / `Matches`  
    Ansatz: ID trägt `Generation`; `Matches` verlangt Hash **und** Generation. Rebind nur über `ContentHash` (Generation intern). Kanten-Offset/`cursor` über die bereits gebaute `allEdges`-Liste (kein zusätzliches Roslyn).
