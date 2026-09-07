# Finding: `find_references`

Anker: `T:Sample.Project.Infrastructure.Sql.DataExecutor` (Klasse `Sample.Project/Infrastructure/Data/DataExecutor.cs`) und `[ComponentRegistration]` `T:Sample.Project.Handlers.Form.FormComponentHandler`. Assembly: `Example.External.Business.Record` in `Example.External.Business.dll`.

## 1. Schema-Kurzfazit

Die Tool-Beschreibung steuert den Call **weitgehend korrekt**: `symbolIdentifier` in den Formaten `M:…`, `Datei.cs:42:10`, `Datei.cs:42`, `Klasse.Methode`; `maxResults` Default 50; `depth` Default 1, Hard-Cap 3, Traversal-Cap 200; `includeReferences` nur für `targetType=assembly`; Zielvertrag `project` vs. `assembly`.

Irreführend oder unvollständig:

- JSON-Schema markiert nur `targetType`/`targetPath` als `required`; zur Laufzeit ist `symbolIdentifier` (oder `symbol`) Pflicht (`INVALID_ARGUMENT`). Agent, der dem Schema folgt, schickt einen leeren Call.
- Beschreibung nennt Alias **nur** `symbol`. Schema hat zusätzlich `identifier` und `name` — beide funktionieren live, stehen aber nicht in der Beschreibung.
- Beschreibung verspricht `structuredContent.callSites` plus `completeness` (Tiefe, Herkunft, getrennte Trunkierungsgründe). Auf der Agenten-Textfläche kommen nur Zeilen `Pfad:Zeile - Aufruf von '…'` ohne Symbol-IDs, ohne `callSites`-JSON, ohne `cursor`.
- Truncation-Hinweis lautet „Pattern verfeinern oder maxResults erhöhen“. Dieses Tool hat **kein** `pattern`. `cursor` / `continuationToken` / `detailLevel` / Batch-IDs (`symbolIdentifiers[]`) gibt es im Schema **nicht**. Extra-Parameter `cursor`/`continuationToken` werden still ignoriert (kein Paging, kein Fehler).
- `targetType` im Schema ohne Enum; gültige Werte nur im Fließtext.

Ohne vollqualifizierte ID ist der erste Call bei realen Namen (`DataExecutor`, `Record`, `DataExecutor.QueryAsync`) fast immer `AMBIGUOUS_SYMBOL` — das ist dokumentiert („Identifikator präzisieren“), aber der Happy Path aus der Beschreibung (`Klasse.Methode`) ist in dieser Codebase der Normal-Misserfolg.

## 2. Ausgeführte Calls

Alle Calls über `CallDynamicTool` / `user-AiNetLinter` / `find_references`. Wartezeit überall **schnell** (kein Timeout; Assembly nach Cache ebenfalls schnell).

| # | Parameter | Antwort | Truncation / completeness | Größe (grob) |
|---|-----------|---------|---------------------------|--------------|
| A | project, `symbolIdentifier=DataExecutor`, `maxResults=50` | `AMBIGUOUS_SYMBOL`, 9 Kandidaten inkl. kopierbarer `T:`/`P:`-IDs, Hint präzisieren | — | ~2 KB / ~15 Zeilen |
| B | project, `T:Sample.Project.Infrastructure.Sql.DataExecutor`, default 50 | 246 Treffer, 50 gezeigt, alle Tests.Ama/Components/Logic | ja; Hint „Pattern verfeinern“ | ~8 KB / ~51 Zeilen |
| C | wie B, `maxResults=5` | 246 gesamt, 5 gezeigt | ja | ~1 KB / 6 Zeilen |
| D | project, unbekannt `ThisSymbolDoesNotExistAnywhere_X9zQ` | `SYMBOL_NOT_FOUND`, Hint `find_symbol` | — | ~0,3 KB / 3 Zeilen |
| E | assembly RecordEngine.dll, `RecordEngine`, `includeReferences=true` | Assembly-Header `origin=decompiled; completeness=partial`, dann `SYMBOL_NOT_FOUND` | — | ~0,8 KB |
| F | project, `Datei…/DataExecutor.cs:9`, `maxResults=20` | dieselben 246 Refs wie Typ-ID | ja, 20/246 | ~4 KB |
| G | project, `T:…DataExecutor`, `depth=3`, `maxResults=15` | „3385 Treffer gesamt (depth=3, hard-cap 200), 15 gezeigt“ + „Traversal auf 200 Knoten begrenzt“ | ja, widersprüchliche Zähler | ~3 KB |
| H | assembly, `T:Example.External.Business.Record`, `depth=2`, `includeReferences=true`, `maxResults=20` | 20 „transitiver Aufrufer“ auf Cache-Pfaden; Duplikate gleicher Zeile; 100 Decompiler-Diagnosen, 5 Samples | „20 Treffer gesamt (depth=2, hard-cap 200), 20 gezeigt“; `completeness=partial` | ~8 KB + Diagnosen |
| I | assembly, `T:…Record`, `includeReferences=false`, `maxResults=10`, depth default 1 | 109 Treffer, 10 gezeigt; Labels `Aufruf von 'Record'` / `'Record..ctor'` | ja, Hint wieder „Pattern“ | ~4 KB |
| J | project, Alias `symbol=` gleiche T-ID, `maxResults=10` | identisch zu B | ja | ~2 KB |
| K | project, Alias `name=` gleiche T-ID, `maxResults=5` | identisch | ja | ~1 KB |
| L | project, Alias `identifier=` gleiche T-ID, `maxResults=3` | identisch | ja | ~0,7 KB |
| M | project, **ohne** Symbol | `INVALID_ARGUMENT`: Pflicht `symbolIdentifier` (oder `symbol`) | — | ~0,3 KB |
| N | project, `includeReferences=true` auf DataExecutor | still ignoriert, gleiche 246/5 | ja | ~1 KB |
| O | project, `T:…FormComponentHandler`, `maxResults=20` | 117 Treffer, 20 gezeigt, alle Tests.Components | ja | ~4 KB |
| P | project, `maxResults=246` (voll) | 246 Zeilen, Hinweis „vollständig … kein zusätzliches Read/Grep“ | nein | **41,3 KB / 248 Zeilen** |
| Q | project, `DataExecutor.QueryAsync` | `AMBIGUOUS_SYMBOL` inkl. Test-Fakes **vor** echter Methode; kopierbare `M:`-IDs | — | ~4 KB |
| R | project, volle `M:…DataExecutor.QueryAsync``1(…DataExecutionScope…)` , `maxResults=8` | 24 Treffer; erste Zeilen sind Domain-Aufrufe von **`IDataQueryExecutor.QueryAsync`** | ja | ~2 KB |
| S | assembly, Kurzname `Record` | `AMBIGUOUS_SYMBOL`; IDs im Format `assembly:<hash>:<gen>:T:…` / `:P:…` | — | ~6 KB |
| T | project, Extra `cursor=dummy` + `continuationToken=dummy`, `maxResults=3` | still ignoriert, erste 3 der 246, kein Paging | ja | ~0,7 KB |

`detailLevel` und Batch-IDs: im Schema nicht vorhanden, daher nicht sinnvoll aufrufbar.

## 3. Verdict

**Teilweise wie beschrieben.** Auflösung, Fehlercodes, `maxResults`, `depth`-Hard-Cap, Assembly-Dekompilat und `includeReferences`-Diagnosen funktionieren. Dagegen lügen Truncation-Hints, das JSON-Schema unterschlägt die Pflicht-ID, Paging existiert nicht, Default-50 blendet Produktion aus, und die versprochene StructuredContent-Fläche (IDs, getrennte Completeness) kommt beim Agenten nicht an.

## 4. Schwere

`degraded`

Begründung: Mit `T:`/`M:`-ID und hohem `maxResults` ist echte Arbeit möglich. Mit Schema-Default (50, Kurzname) kommt der Agent nicht zum Produktions-Ist und wird durch falsche Hints und Token-Müll (Tests zuerst, Assembly-Diagnosen, depth-Explosion) fehlgeleitet. Nicht dauerhaft kaputt, aber riskant.

## 5. Nutzbarkeit

**nur mit Workaround**

Workaround: nicht den Kurznamen nehmen; aus `AMBIGUOUS_SYMBOL` die `T:`/`M:`-ID kopieren; `maxResults` mindestens auf die gemeldete Gesamttrefferzahl setzen (hier 246); `depth` auf 1 lassen, außer transitiv wirklich gewollt; Assembly nur mit `T:Vollname` und `includeReferences=false`, bis Diagnosen entkoppelt sind. Ohne das: nein (Default liefert nur Tests bzw. Ambiguity).

## 6. Bugs, False Positives / False Negatives vs. Ist

Stichprobe gegen Quelltext / Dekompilat (kein Vollabgleich):

**Stimmt (TP):**

- `AdminAiExplorationPlatformSupport.cs:21` — Parameter `DataExecutor executor` (Ist).
- `AdminComponentListHandler.cs:17` — Primärkonstruktor `DataExecutor dataExecutor` am `[ComponentRegistration]` (Ist).
- `PlatformServiceExtensions.cs:264–268` — `AddSingleton<DataExecutor>()` und drei Interface-Registrierungen (Ist, aber erst Rang 212–215 von 246).
- `DataExecutor.cs:9` als Datei:Zeile löst denselben Typ auf wie `T:…DataExecutor`.
- Unbekanntes Symbol → `SYMBOL_NOT_FOUND`, nicht leere Trefferliste.
- `FormLoadQueryRequest.DataExecutor` ist `IDataQueryExecutor` (Property-Name, nicht der Typ) und erscheint **nicht** in der Typ-Referenzliste — kein FP.

**Problematisch:**

- **Default-50 = False Negative für Produktion.** `rg` findet Dutzende Produktionsdateien unter `Sample.Project/`. In der vollständigen Liste beginnen diese erst bei Zeile 208/246. Default und selbst `maxResults=50` zeigen ausschließlich Testhits (Pfad-Sortierung `Tests.Ama` zuerst). Agent „wer injiziert DataExecutor?“ sieht die falsche Welt.
- **Gleicher Effekt beim ComponentRegistration:** `FormComponentHandler` hat Produktionsnutzer (`FormLoadCoordinator`, `FormResultMapper`, `ComponentMetadataServiceCollectionExtensions`). Die ersten 20/117 sind nur `Tests.Components`.
- **Duplikate:** dieselbe Zeile zweimal (`Record` und `Record..ctor`; analog `FormComponentHandler` + `..ctor`). Zählt als zwei Treffer, bläht `maxResults`.
- **Self-Hits:** Typ-Suche `Record` listet Konstruktor-Ketten in `Record.cs:7438/7445` (`public Record(…) : this(…)`). FindReferences-semantisch vertretbar, für „wer nutzt die Klasse von außen?“ Rauschen.
- **Methoden-Lookup driftet aufs Interface:** `M:…DataExecutor.QueryAsync``1(DataExecutionScope,…)` liefert u. a. `RecordPersistenceDispose.cs:61` — Ist ist `_sqlQueryExecutor.QueryAsync<string>(…)` auf `IDataQueryExecutor`. Nützlich, aber das Label lautet `IDataQueryExecutor.QueryAsync`, nicht DataExecutor. Agent kann das als FP oder als andere Typ-Suche missverstehen.
- **Ambiguity priorisiert Test-Fakes:** `DataExecutor.QueryAsync` listet `ThrowingDataExecutor` / `FakeTerminDetailsDataExecutor` vor der echten Produktionsmethode.
- **Truncation-Copy-Paste:** „Pattern verfeinern“ ist für dieses Tool falsch.
- **Zähler vs. Cap bei depth=3:** „3385 Treffer gesamt“ und gleichzeitig „Traversal auf 200 Knoten begrenzt — weitere Treffer nicht enthalten“. Completeness ist unklar: ist 3385 Schätzung, Overflow oder Lüge?
- **`includeReferences=true`:** keine zusätzlichen Fremd-DLL-Call-Sites in den ersten 20, dafür 100 Decompiler-Diagnosen (`CS0535` auf `Collection`, `CS1525` in `DateTimeFormat.cs`, `NullablePublicOnlyAttribute`). Diagnosen stammen aus Referenz-Assemblies, nicht aus der Frage „wer ruft Record auf?“.
- Assembly-Treffer zeigen **Cache-Absolutpfade** (`…\cache\asm.cursor\…\generation-…\Record.cs:7438`), nicht den DLL-Pfad. Generation in der ID (`assembly:…:5:T:…`) driftet (Probe: generation 1→5 in derselben Session).

Keine stillen Leermengen bei gültiger ID beobachtet.

## 7. Token / IDs / Hints

- **Token:** Default-Antwort für einen Kern-Infrastrukturtyp ist Testhit-Liste. Vollabruf 41 KB für 246 Zeilen ohne Aggregation (keine Gruppierung nach Projekt/Produktion). `depth>1` multipliziert. Assembly+`includeReferences` hängt Diagnosen an, die den eigentlichen Call-Sites die Tokens stehlen.
- **IDs:** Nur der **Ambiguity-/Not-Found-Pfad** liefert stabile, folgetaugliche IDs (`T:…`, `M:…`, assembly-präfixierte IDs). Die Trefferliste selbst hat **keine** Symbol-IDs, nur `Datei:Zeile`. Folge-Call geht über selbst gebautes `Datei.cs:Zeile` — das traf hier, ist aber nicht als ID ausgewiesen.
- **Hints:** `SYMBOL_NOT_FOUND` → `find_symbol` ist gut. Ambiguity → voller Name oder `Datei:Zeile:Spalte` ist gut. Truncation-Hint ist falsch und erwähnt kein Paging (weil es keines gibt).
- **StructuredContent:** Beschreibung ja, Agent-Text nein. Ohne `callSites`/Completeness-Objekt muss der Agent die Fließtext-Footer parsen (`[246 Treffer gesamt, 50 gezeigt]`).

## 8. Roslyn-konforme Wünsche

Alles statisch/Roslyn machbar, kein LLM:

1. **Paging statt „maxResults erhöhen“:** `continuationToken` auf der bereits berechneten Referenzliste (DocumentId+Span oder SymbolKey). Footer: „weitere N, Token=…“, nicht „Pattern verfeinern“.
2. **Sortierung/Filter:** Produktion vor Tests, oder `scopeType` analog `search_pattern` (`production`/`tests`/`all`). Default sollte nicht 50 Testhits sein.
3. **Treffer mit Symbol-ID:** jede Call-Site als `file:line` **und** `id` (`T:`/`M:`/containing method), damit `get_symbol_body` ohne Raten folgt.
4. **Schema = Runtime:** `symbolIdentifier` required; Aliase in der Beschreibung; `targetType` Enum; `includeReferences` als no-op am Projekt **sagen** oder ablehnen.
5. **Duplikate zusammenziehen:** Typ-Nennung und Konstruktor auf derselben Span als ein Treffer mit Kind-Flags.
6. **depth-Completeness ehrlich:** besuchte Knoten, gekappte Knoten, getrennte Gründe (`maxResults` vs. traversal-cap 200 vs. depth). Keine „3385 gesamt“ plus „200 begrenzt“ ohne Erklärung.
7. **Assembly:** Call-Sites relativ zum Dekompilat-Stamm oder als `assembly:…:T:`-ID; Decompiler-Diagnosen nicht in die Referenzliste mischen (eigenes `diagnostics`-Feld, Default aus). `includeReferences` nur Call-Sites aus gebundenen Referenz-Sessions, nicht CS0535-Spam.
8. **Ambiguity:** Produktions-`T:` vor Test-Fakes; optional `kind=NamedType` damit `DataExecutor` die Klasse trifft statt Properties gleichen Namens.

## 9. Phase-3-Platzhalter

AiNetLinter-Repo `C:\Daten\Entwicklung\Ralf\AiNetLinter`, read-only. Je Nicht-ok-Befund: Pfad, Symbol, Roslyn-Ansatz.

### friction — JSON-`required` ohne `symbolIdentifier`; Alias-Dokumentation lückenhaft

- **Pfad:** `src\AiNetLinter\Mcp\Registration\SymbolGraphToolRegistrations.cs`; `src\AiNetLinter\Mcp\Tools\SymbolGraph\FindReferencesTool.cs`
- **Symbol:** `SymbolGraphToolRegistrations.AddFindReferences` / `FindReferencesDescription`; `FindReferencesTool.ExecuteAsync`
- **Ansatz:** Runtime prüft `FindReferencesRequest.EffectiveSymbolIdentifier` bereits (`INVALID_ARGUMENT`). Beschreibung um Aliase `identifier` und `name` ergänzen (Lambda hat sie). Schema: `targetType` als Enum `project|assembly` (Resolver in `AnalysisTargetResolver.ResolveTargetType` kennt nur die zwei). Pflichtfeld-Overlay analog `find_symbol`: mindestens eines von `symbolIdentifier`/`symbol`/`identifier`/`name`.

### friction — Truncation-Hint „Pattern verfeinern“; Extra-`cursor` still ignoriert

- **Pfad:** `src\AiNetLinter\Mcp\Tools\SymbolGraph\TransitiveCallGraphFormatter.cs`; `src\AiNetLinter\Mcp\McpTruncation.cs`
- **Symbol:** `TransitiveCallGraphFormatter.CreateMaxResultsMessage`; `McpTruncation.TruncateLines`
- **Ansatz:** Depth-1-Footer ohne das Wort `pattern` — z. B. „maxResults erhöhen oder continuationToken“. Paging: die bereits in `CallGraphTraversal.TraversalState.CreateResult` geordnete Liste (`ordered`) tokenisieren (DocumentId + Span oder Index), `cursor`/`continuationToken` in der Registration nicht mehr still droppen. Unbekannte Extra-Keys: `INVALID_ARGUMENT`, kein No-Op.

### degraded — StructuredContent-IDs fehlen im Agenten-Text

- **Pfad:** `src\AiNetLinter\Mcp\Tools\SymbolGraph\TransitiveCallGraphFormatter.cs`; `src\AiNetLinter\Mcp\Tools\SymbolGraph\CallGraphTraversal.cs`; `src\AiNetLinter\Mcp\Tools\SymbolGraph\TransitiveCallGraphModels.cs`
- **Symbol:** `TransitiveCallGraphFormatter.FormatEntry`; `CallGraphTraversal.CreateCallSiteEntry`; `TransitiveCallSiteEntry.ReachedFromSymbolId`
- **Ansatz:** `CreateCallSiteEntry` schreibt schon `GetStableSymbolId` (`DocumentationCommentId.CreateDeclarationId`). `FormatEntry` um `` id: `{entry.ReachedFromSymbolId}` `` und die enclosing-Member-ID ergänzen (über `ResolveEnclosingMemberAsync`, analog Call-Tree). Dann folgt `get_symbol_body` ohne `Datei:Zeile`-Raten. StructuredContent `callSites` bleibt die Quelle; Text und JSON aus derselben Entry-Liste.

### degraded — Default-50 / Pfadsortierung blendet Produktion aus

- **Pfad:** `src\AiNetLinter\Mcp\Tools\SymbolGraph\CallGraphTraversal.cs`
- **Symbol:** `CallGraphTraversal.TraversalState.CreateResult`
- **Ansatz:** `OrderBy(FilePath)` durch Ranking ersetzen: zuerst `!TestDetector.IsTestFile(FilePath)` (`AiNetLinter.Core.TestDetector`), dann Produktion, dann Tests; innerhalb Gruppe Pfad/Zeile. Optional `scopeType` analog `SearchPatternScanner.IsExcludedByScopeType` (`production`/`tests`/`all`). `maxResults` schneidet **nach** dem Ranking, damit Default 50 Produktions-Call-Sites zeigt.

### degraded — Duplikate Typ + Konstruktor dieselbe Span

- **Pfad:** `src\AiNetLinter\Mcp\Tools\SymbolGraph\CallGraphTraversal.cs`
- **Symbol:** `CallGraphTraversal.AppendReferenceLocations` / `CreateCallSiteEntry`
- **Ansatz:** `SymbolFinder.FindReferencesAsync` liefert für `INamedTypeSymbol` oft Typ- und `.ctor`-`ReferencedSymbol` auf derselben `Location`. Dedup-Key `(SourceTree.FilePath, SourceSpan)` bzw. `(FilePath, Line, ReachedFromSymbolId)`. Ein Treffer, `SymbolName` mit Kind-Flags (`type`+`ctor`). `Distinct()` auf dem Record reicht nicht, weil sich `SymbolName` unterscheidet.

### degraded — Methoden-Lookup driftet aufs Interface; Ambiguity-Test-Fakes zuerst

- **Pfad:** `src\AiNetLinter\Mcp\Tools\SymbolGraph\CallGraphTraversal.cs`; `src\AiNetLinter\Mcp\Tools\SymbolGraph\FindReferencesTool.cs`
- **Symbol:** `CallGraphTraversal.TraverseAsync` (`SymbolFinder.FindReferencesAsync`); `FindReferencesTool.ResolveByNameAsync`
- **Ansatz:** Nach `FindReferencesAsync` nur `ReferencedSymbol` behalten, deren `Definition` per `SymbolEqualityComparer.Default` dem Seed entspricht (kein stilles Cascade auf `IDataQueryExecutor.QueryAsync`). Optional explizites Flag für Related-Definitions. Ambiguity-Liste in `ResolveByNameAsync`: `INamedTypeSymbol` und `!TestDetector.IsTestFile` vor Test-Fakes/`IPropertySymbol` gleichen Namens sortieren, bevor `AmbiguousSymbol` formatiert.

### degraded — `depth=3`: „3385 gesamt“ plus Traversal-Cap 200

- **Pfad:** `src\AiNetLinter\Mcp\Tools\SymbolGraph\CallGraphTraversal.cs`; `src\AiNetLinter\Mcp\Tools\SymbolGraph\TransitiveCallGraphFormatter.cs`
- **Symbol:** `TraversalState.CreateResult`; `TransitiveCallGraphFormatter.AppendLimitMessages`
- **Ansatz:** `TotalCallSiteCount` ist `ordered.Count` der **gesammelten** Locations der besuchten Knoten, nicht „alles in der Solution“. Footer trennen: `VisitedNodeCount` / `NodeLimit` (`MaxRecursionNodes`), `TruncatedByNodeLimit`, `TruncatedByMaxResults`, `DepthWasClamped` — Felder existieren in `TraversalCompleteness` bereits. Keine Zahl als Gesamtsolution verkaufen, wenn `TruncatedByNodeLimit`.

### degraded — Assembly-`includeReferences`: Diagnosen statt Call-Sites; Cache-Absolutpfade

- **Pfad:** `src\AiNetLinter\Mcp\Tools\SymbolGraph\AssemblyFindReferencesTool.cs`; `src\AiNetLinter\Mcp\Tools\SymbolGraph\TransitiveCallGraphFormatter.cs`; `src\AiNetLinter\Mcp\Tools\SymbolGraph\CallGraphTraversal.cs`
- **Symbol:** `AssemblyFindReferencesTool.ExecuteWithReferencesAsync`; `TransitiveCallGraphFormatter.AppendDiagnosticMetadata`; `CreateCallSiteEntry` (`Path.GetFullPath` wenn `assemblyIdentity != null`)
- **Ansatz:** Diagnosen nur in StructuredContent/`Navigation`, nicht als `[Assembly-Diagnostic]`-Zeilen vor den Call-Sites. Pfade relativ zum Dekompilat-Stamm (`GeneratedDocumentPath`), analog `PathNormalizer.ToRelative`. `includeReferences` am Projekt: `INVALID_ARGUMENT` (Registration reicht `includeReferences` nur in die Assembly-Route; Projekt ist No-Op in `AddFindReferences`).

### degraded — Assembly-IDs generationsgebunden

- **Pfad:** `src\AiNetLinter\Mcp\AnalysisSymbolIdentity.cs`; `src\AiNetLinter\Mcp\Tools\SymbolGraph\SymbolIdentifierResolver.cs`
- **Symbol:** `AnalysisSymbolIdentity.Format`; `SymbolIdentifierResolver.StaleAssemblyId`
- **Ansatz:** wie `find_symbol`: Wire ohne Generation, Rebind über SHA + DocCommentId. `AmbiguousSymbol`-Kandidaten weiter über `FindSymbolTool.FormatSymbolLocations`.

### wish — Paging / `scopeType` / Production-first Ambiguity

- **Pfad:** `src\AiNetLinter\Mcp\Tools\SymbolGraph\CallGraphTraversal.cs`; `src\AiNetLinter\Mcp\Tools\Analysis\SearchPatternScanner.Scope.cs`; `src\AiNetLinter\Core\TestDetector.cs`
- **Symbol:** `TraversalState.CreateResult`; `SearchPatternScanner.IsExcludedByScopeType`; `TestDetector.IsTestFile`
- **Ansatz:** `scopeType` an `FindReferencesRequest` durchreichen und Locations vor dem `Take(maxResults)` filtern. Continuation auf der gefilterten `ordered`-Liste. Ranking in `ResolveByNameAsync` wie oben. Alles `ISymbol`/`Location`/DocCommentId — kein LLM.
