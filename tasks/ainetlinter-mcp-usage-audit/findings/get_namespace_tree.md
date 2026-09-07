# Finding: `get_namespace_tree`

Tool: `user-AiNetLinter` / `get_namespace_tree`  
Datum: 2026-09-07  
Targets: Platform-Solution (`targetType=project`) und `Example.External.Core.dll` (`targetType=assembly`)

## 1. Schema-Kurzfazit

Beschreibung steuert den **Projekt-Happy-Path** weitgehend richtig: Solution-Übersicht ohne Parameter, `project` als Filter, `namespacePrefix` plus `depth` 1–3, `includeTypes`, `kind`, `maxResults` (Default 50, Cap 200). Pflicht `targetType`/`targetPath` steht im Text, nicht in den Property-Beschreibungen.

Irreführend oder lückenhaft:

- JSON-Schema: `targetType`/`targetPath` nur `string` ohne Enum/Beschreibung; `kind` ohne Enum (`class|interface|record|struct|enum|all`); `depth` ohne Min/Max 1–3.
- `includeTypes` Default `true` heißt laut Text „Typen ausgeben“. In der Praxis zeigt der **Baummmodus** nur Typ**zahlen**; Typnamen erscheinen erst im **Listenmodus** (Prefix trifft einen Namespace mit Typen auf genau dieser Ebene, oder `kind` gesetzt). Agent muss `includeTypes=false` oder `depth=2` kennen, sonst sieht er einen einzelnen Typ plus Hinweis statt den Baum.
- Assembly: Tipps verlangen `project="<ProjektName>"` wie bei einer Solution. Das synthetische Dekompilat heißt `Example.External.Core`. Ressourcenlimit (16 Assembly-Sessions) und `maxResponseBytes` fehlen in der Beschreibung.
- `maxResponseBytes` (Default 0 = Standardbudget) ist dokumentiert, in den Probes **wirkungslos**.

Ohne Schema-Text käme ein Agent bei `targetType` und `kind` ins Raten; mit Beschreibung reicht es für den ersten Call, nicht für den Moduswechsel Baum/Liste.

## 2. Ausgeführte Calls

Alle Calls schnell (kein Timeout), außer Call 12 (`ANALYSIS_FAILED`). Große Größen geschätzt anhand der Tool-Antworttexte.

| # | Target | Parameter | Ergebnis | Größe | Truncation / completeness | Wartezeit |
|---|--------|-----------|----------|-------|---------------------------|-----------|
| 1 | project | `namespacePrefix=Sample.Project.Contracts`, `depth=2` | Baum der Child-Namespaces inkl. `Shared.Kalender`/`Scheduler`, nur Typzahlen. Projekt aus eindeutigem Prefix inferiert. | ~900 Z. / 16 Z. | Hinweis „vollständig“ | schnell |
| 2 | assembly | `namespacePrefix=Sample.ExternalSuite`, `depth=2` | Antwortinhalt nur `.` | 1 Z. | keine Metadaten | schnell |
| 3 | project | `namespacePrefix=ZzzNoSuchNamespace.DefinitelyMissing`, `depth=1` | `[ERROR] INVALID_ARGUMENT`, Hint listet **Projekte**, nicht Namespaces | ~700 Z. | — | schnell |
| 4 | project | Prefix `Sample.Project`, `maxResults=5`, `includeTypes=true`, `kind=class` (ohne `project`) | `[ERROR] AMBIGUOUS_SYMBOL` + 15 csproj-Pfade | ~2500 Z. | — | schnell |
| 5 | project | `project=…Contracts`, Prefix `…Contracts.Sql`, `includeTypes=false` | „Keine Namespaces mit Typen gefunden“ (Leaf ohne Kinder) | ~400 Z. | „vollständig“ | schnell |
| 6 | project | `project=…Contracts`, Prefix `…Contracts`, `kind=interface` | Listenmodus: nur `IDomainServiceModule`; Hinweis auf 10 Sub-Namespaces | ~500 Z. | „vollständig“ | schnell |
| 7 | assembly | nur Target, kein Prefix | Overview: 40 Namespaces, 199 Typen; **kein** Namespace-Baum. Tipp `project=` | ~900 Z. Header+Body | Header `completeness=partial` | schnell |
| 8 | project | `project=Sample.Project`, Prefix Host-NS, `depth=2`, `maxResults=5` | 3 Top-Level + Auth-Kinder; Text `[30 Namespaces gesamt, 5 gezeigt]` | ~450 Z. | Truncation-Hinweis, kein `completeness`-Feld | schnell |
| 9 | project | `project=…Contracts`, Prefix `…Sql`, `includeTypes=true` | 20 Typen mit Relativpfad:Zeile, **keine Symbol-IDs** | ~1800 Z. / 22 Z. | „vollständig“ | schnell |
| 10 | assembly | Prefix `Example.External.Core` | Listenmodus: nur `TraceLog`; Hinweis 30 Sub-Namespaces. Header `status=partial; completeness=partial` vs. Body „vollständig“ | ~1100 Z. | widersprüchlich | schnell |
| 11 | assembly | Prefix `ZzzNoSuch…` | `INVALID_ARGUMENT`; Hint: `Verfuegbare Projekte: Example.External.Core` | ~700 Z. | Header `partial` | schnell |
| 12 | assembly | Prefix `Example.External.Core`, `includeTypes=false` | `[ERROR] ANALYSIS_FAILED`: „Das externe Ressourcenlimit ist ausgeschöpft (16 Einträge).“ | ~150 Z. | — | schnell |
| 13 | project | Host-NS, `project=Host`, `depth=2`, `maxResponseBytes=400` | voller Baum (~11 Top-Level inkl. Nested); Limit **ignoriert** | ~2200 Z. | „vollständig“ | schnell |
| 14 | project | keine Optionalen | Solution Overview, 15 Projekte, Namespace-/Typzahlen | ~1100 Z. / 20 Z. | kein Truncation-Hinweis | schnell |
| 15 | project | Prefix `…Sql`, `kind=enum` | nur `SqlOptimisticMutationResult` | ~350 Z. | „vollständig“ | schnell |
| 16 | assembly | Prefix `Example.External.Core`, `includeTypes=false` (Retry) | 30 direkte Kinder mit Typzahlen | ~1800 Z. | Header `partial`, Body „vollständig“ | schnell |
| 17 | project | nur `project=…Contracts` | eine Zeile Root `(35 Typen)`; Tipp `depth=2` | ~450 Z. | „vollständig“ | schnell |
| 18 | project | `project=…Contracts`, Prefix Contracts, `depth=3` | wie Call 1 (Baum endet bei Shared.*) | ~900 Z. | „vollständig“ | schnell |
| 19 | project | Prefix `…Sql`, `kind=notAKind` | **nicht am Server**: Cursor-Auto-Review hat den Call blockiert | — | — | — |
| 20 | project | `project=…Contracts`, `depth=2`, kein Prefix | Root + direkte Kinder; **ohne** `Shared.Kalender`/`Scheduler` | ~800 Z. | „vollständig“ | schnell |
| 21 | assembly | Prefix `Example.External.Core`, `maxResults=3` | weiter Listenmodus (`TraceLog`); `maxResults` greift nicht in den Baum | ~1100 Z. | Header `partial` | schnell |
| 22 | assembly | Prefix `Sample.ExternalSuite` (Retry von 2) | „Keine Typen gefunden“ + Hinweis 1 Sub-NS `Example.External.Core` | ~800 Z. | Header `partial` | schnell |
| 23 | project | Prefix `…Sql`, `kind=record`, `maxResults=10` | 4 Records inkl. `record struct` | ~550 Z. | „vollständig“ | schnell |
| 24 | assembly | Prefix `Example.External.Core`, `depth=2` | Baum mit Enkeln (z. B. `DataService.*`, `AdContainerExchange.*`) | ~2500 Z. | Header `partial`, Body „vollständig“ | schnell |
| 25 | project | Host-NS, `includeTypes=false`, `maxResults=3` | Api/Auth/Components; `[11 Namespaces gesamt, 3 gezeigt]` | ~350 Z. | Truncation-Hinweis | schnell |
| 26 | project | `project=…Buchungsserver`, `maxResponseBytes=80` | eine Zeile Root `(8 Typen)`; Limit **ignoriert** | ~450 Z. | „vollständig“ | schnell |

## 3. Verdict

**Teilweise** wie beschrieben.

Projekt-Drilldown mit eindeutigem Prefix bzw. gesetztem `project` plus `depth` liefert einen brauchbaren Namespace-Baum. Solution-Overview, `kind`-Filter auf **genau einer** Namespace-Ebene und `maxResults`-Truncation (Projekt, Baummodus) stimmen mit der Beschreibung überein.

Abweichungen: Dual-Modus Baum vs. Typ-Liste; Leertreffer als `INVALID_ARGUMENT` statt leerer Menge; Assembly-Erstcall `.`, Session-Limit 16, Overview ohne Baum, widersprüchliches `completeness`; `maxResponseBytes` ohne Wirkung; Depth zählt mit/ohne Prefix unterschiedlich; Tipps für Assembly nutzen `project=`.

## 4. Schwere

**degraded**

Begründung: Ziel (Namespaces/Typen finden) ist im Projekt mit Workaround erreichbar. Assembly, Truncation ohne Pagination, ignoriertes Byte-Budget, fehlende IDs und der stillschweigende Moduswechsel machen Folgecalls riskant. Nicht `broken` (Happy Path Projekt funktioniert). Zusätzlich `friction` am Schema (`kind`/`targetType` ohne Enum, irreführendes `includeTypes`).

## 5. Nutzbarkeit

**nur mit Workaround**

Wiederverwenden ja, aber:

1. Bei Solution-weitem Prefix immer `project=` (sonst `AMBIGUOUS_SYMBOL`).
2. Für den Baum: `depth=2` oder `includeTypes=false`; nicht auf Default-`includeTypes=true` verlassen.
3. Assembly erst nach Warmup; bei `ANALYSIS_FAILED` (Limit 16) warten/neu versuchen; Prefix exakt `Example.External.Core`, nicht das Elternsegment `Sample.ExternalSuite`.
4. Typen für Folgetools über Namen+Pfad, nicht über IDs.

Ohne diese Regeln landet der Agent in Fehlern, Leermeldungen oder einer einzeiligen Typ-Liste.

## 6. Bugs + FP/FN

### Bugs (reproduzierbar)

- **Call 2:** Assembly + Prefix `Sample.ExternalSuite` + `depth=2` → nur `.`. Call 22 mit denselben Kernparametern (ohne `depth`) später: leere Typ-Liste plus Hinweis auf `Example.External.Core`. Erstcall nach Session-Aufbau unbrauchbar.
- **Call 12:** Wiederholte Assembly-Calls → `ANALYSIS_FAILED` Ressourcenlimit **16 Einträge**. Parallel-Agenten teilen die Session.
- **Call 13, 26:** `maxResponseBytes=400` bzw. `80` ändert die Antwort nicht; voller Text plus „vollständig“.
- **Call 3, 11:** Unbekannter Prefix → `INVALID_ARGUMENT`, kein leeres Resultat. Hint nennt Projekte, nicht ähnliche Namespaces.
- **Call 10 vs. Hinweis:** Header `completeness=partial` und Body „Diese Daten sind vollstaendig fuer den angefragten Scope“.
- **Call 7:** Assembly ohne Prefix listet die 40 Namespaces nicht, nur Aggregatzahlen. Beschreibung „Ohne Parameter: Projekt-Uebersicht“ trifft die Solution, nicht die DLL.
- **Call 5:** `includeTypes=false` auf einem Leaf (`…Sql`, 20 Typen laut Call 1/9) → „Keine Namespaces mit Typen gefunden“ statt „keine Kind-Namespaces, Typen mit includeTypes=true“.
- **Call 20 vs. 1/18:** `depth=2` ohne Prefix unterschlägt `Shared.Kalender`/`Shared.Scheduler`; mit `namespacePrefix` auf dem Root erscheinen sie. Depth zählt den Root mit, sobald kein Prefix gesetzt ist.

### False Positives / False Negatives (Stichprobe, nur dieses Tool)

- **FN `kind`:** Call 6 (`kind=interface` auf `…Contracts`) liefert nur `IDomainServiceModule`. Call 9 zeigt allein unter `…Sql` vier Interfaces (`IExternalDataQueryExecutor`, `IDataQueryExecutor`, …). Filter ist **nicht rekursiv**; Agent erwartet das oft anders. Kein FP in der einen Trefferzeile (Dateiname passt zum bekannten Contracts-Typ).
- **FN Baum bei Default:** Call 10/21 listen `TraceLog` und verstecken 30 Kinder hinter einem Hinweis. Ohne `depth=2`/`includeTypes=false` wirkt die DLL fast leer (FN der Exploration).
- **Zählung:** Overview (Call 14) Host = **50** Namespaces / **983** Typen. Truncation Call 8 = **30** Namespaces unter dem Host-Prefix bei `depth=2`. Call 25 (`includeTypes=false`, depth default) = **11** direkte Kinder — das passt zu den 11 Top-Level-Namen aus Call 13. 50 vs. 30 bleibt ungeprüft (vermutlich alle Nested vs. Truncation-Scope).
- **Contracts.Sql:** Call 1 „20 Typen“, Call 9 listet 20 Zeilen (class/interface/record/enum/record struct). Call 15 enum = 1, Call 23 record = 4 inkl. `SqlOptimisticUpdateRowVersionResult` (record struct). Intern konsistent, kein FN in dieser Stichprobe.
- **Buchungsserver:** Overview 1 Namespace / 8 Typen = Call 26. OK.

## 7. Token / IDs

- Solution-Overview (Call 14) ist kompakt und der richtige Einstieg.
- `AMBIGUOUS_SYMBOL` (Call 4) dumppt alle csproj-Vollpfade — unnötig tokenlastig; Projektnamen würden reichen.
- Assembly-Header wiederholt bei jedem Call denselben `generatedPath` (Cache-Pfad, Generation, `contentMode=decompiledProject`). Für den Agenten selten nützlich.
- Typ-Zeilen: `Name (kind) — relativePath:line`. **Keine** `symbolIdentifier`, kein `cursor`/`continuationToken`. Nächster Schritt ist raten (`find_symbol` / `get_class_structure` / `get_symbol_body` per Name).
- Truncation (Call 8, 25): Text „depth reduzieren oder maxResults erhoehen“. Kein Offset, kein Continuation. Cap 200 im Schema; Host hat 50 NS / 983 Typen — für Typ-Listen knapp, für den Namespace-Baum meist genug.
- Follow-up-Tipps sind konkret (nächster Prefix), aber Assembly-Tipps mit `project=` sind falsch gemünzt und kosten einen Fehlversuch.

## 8. Roslyn-konforme Wünsche

Alles statisch aus Compilation/INamespaceSymbol bzw. Metadata-Namespace der Dekompilat-Compilation, kein LLM:

1. Ein Modus-Flag oder deterministische Regel: Baum vs. Typ-Liste in der Beschreibung und im Schema (`responseMode=tree|types`). Default Baum, Typen nur bei `includeTypes=true` **und** Leaf bzw. explizitem Modus.
2. `maxResponseBytes` tatsächlich anwenden oder aus dem Schema nehmen.
3. Unbekannter Prefix: leere Treffermenge + `completeness=complete` + optionale Namespace-Vorschläge (Präfix-Match über `INamespaceSymbol`), nicht `INVALID_ARGUMENT` mit Projektliste.
4. Stabile Typ-IDs in der Typ-Liste (gleiche ID wie `get_class_structure` / `get_symbol_body`).
5. Assembly-Overview: erste Ebene der Metadaten-Namespaces ausgeben, nicht nur „40 Namespaces“. Session-Reuse/Limit dokumentieren oder hartes Limit vermeiden (eine Session pro `targetPath`).
6. `kind` und `targetType` als JSON-Enum; `depth` min/max 1–3 im Schema. `kind` entweder rekursiv über Kind-Namespaces oder explizit „nur dieser Namespace“.
7. Einheitliche Depth-Semantik (Prefix-Wurzel zählt nicht doppelt). Pagination statt nur `maxResults` erhöhen.
8. Assembly-Tipps: `namespacePrefix` + `includeTypes=false`/`depth=2`, nicht `project=`.

## 9. Phase 3

Welle 3, read-only `C:\Daten\Entwicklung\Ralf\AiNetLinter`. Je Nicht-ok-Befund: Pfad + Symbol + Ansatz. Kein Patch. Alles über `INamespaceSymbol` / `Compilation.GlobalNamespace` / Dekompilat-Compilation, kein LLM.

### Dual-Modus Baum vs. Typ-Liste / irreführendes `includeTypes` (`degraded` / `friction`)

- **Pfad:** `src\AiNetLinter\Mcp\Tools\FileStructure\GetNamespaceTreeScanner.cs`; `src\AiNetLinter\Mcp\Registration\FileStructureToolRegistrations.cs`
- **Symbol:** `GetNamespaceTreeScanner.ScanProjectNamespacesAsync` (Zweig `NamespacePrefix && IncludeTypes && Depth <= 1` → `RenderNamespaceTypes`, sonst `RenderNamespaceTree`); `GetNamespaceTreeDescription`
- **Ansatz:** Explizites `responseMode=tree|types` oder: Default immer Baum; Typnamen nur im Listenmodus. `includeTypes=true` im Baum hängt Typen nur ins StructuredContent (`NamespaceTreeNode.Types`), der Text zeigt weiter nur Zählungen — Description an den Text anpassen oder Typnamen im Markdown ausgeben. `INamespaceSymbol.GetTypeMembers()` bleibt die Typquelle.

### Unbekannter Prefix → `INVALID_ARGUMENT` mit Projektliste (`degraded`)

- **Pfad:** `src\AiNetLinter\Mcp\Tools\FileStructure\GetNamespaceTreeTool.cs`
- **Symbol:** `GetNamespaceTreeTool.ExecuteAutoProjectDrilldownAsync` (`matchingProjects.Count == 0` → `LinterErrorCodes.InvalidArgument`, Hint `Verfuegbare Projekte`)
- **Ansatz:** Leere Treffermenge + `completeness=complete` (wie `ScanProjectNamespacesAsync` schon bei gesetztem `project` den Text „Namespace … nicht gefunden“ ohne Error-Code liefert). Optionale Vorschläge: Präfix-Match über `compilation.GlobalNamespace.GetNamespaceMembers()` rekursiv (`INamespaceSymbol.ToDisplayString()` starts-with). Hint = Namespace-Namen, nicht csproj-Pfade.

### `AMBIGUOUS_SYMBOL` dumppt csproj-Vollpfade (`degraded`)

- **Pfad:** `src\AiNetLinter\Mcp\Tools\FileStructure\GetNamespaceTreeTool.cs`
- **Symbol:** `GetNamespaceTreeTool.ExecuteAutoProjectDrilldownAsync` (`matchingProjects.Count > 1`)
- **Ansatz:** Context nur `p.Name`, nicht `p.FilePath`. Weiter `project=` als Pflicht bei Mehrdeutigkeit.

### `maxResponseBytes` wirkungslos (`degraded`)

- **Pfad:** `src\AiNetLinter\Mcp\Registration\FileStructureToolRegistrations.cs`; `src\AiNetLinter\Mcp\AnalysisToolCall.cs`; `src\AiNetLinter\Mcp\Assemblies\Analysis\AssemblyAnalysisResponse.cs`; `src\AiNetLinter\Mcp\Tools\AssemblyAnalysis\AssemblyAnalysisResponseLimits.cs`
- **Symbol:** `AddGetNamespaceTree` (übergibt `MaxResponseBytes` nur an `AnalysisToolDispatch`); `ProjectAnalysisDispatcher.ExecuteAsync` (Parameter wird **nicht** gelesen); `AssemblyAnalysisResponseLimits.IsBelowMinimumResponseBudget` (Minimum 2048; 400/80 wären auf Assembly `INVALID_ARGUMENT`, auf Projekt tot)
- **Ansatz:** Projekt-Route: Budget anwenden oder Parameter aus Schema/Description nehmen. Assembly: Description an Minimum 2 KB / Default 32 KB anpassen, nicht „0 = Standardbudget“ ohne Untergrenze.

### Assembly-Erstcall `.` (`degraded`)

- **Pfad:** `src\AiNetLinter\Mcp\Assemblies\Analysis\AssemblyAnalysisResponse.cs`
- **Symbol:** `AssemblyAnalysisResponse.TrimUtf8` (Fallback `"."` wenn das Restbudget kleiner als die Ellipsis-Bytes ist); ausgelöst über `Enrich` / `ApplyWireBudget`
- **Ansatz:** Nicht mit einem Zeichen abschneiden; bei Unterbudget `INVALID_ARGUMENT` oder Header kürzen, Body behalten. `ScanProjectNamespacesAsync` auf leerer Warmup-Compilation liefert sonst den Leertext, nicht `.`.

### Assembly-Overview ohne Namespace-Baum (`degraded`)

- **Pfad:** `src\AiNetLinter\Mcp\Tools\FileStructure\GetNamespaceTreeScanner.cs`; `GetNamespaceTreeTool.cs`
- **Symbol:** `GetNamespaceTreeScanner.ScanSolutionProjectsAsync` (nur Projekt-Aggregate); `GetNamespaceTreeTool.AddAssemblyOverviewHeader` (Überschrift umbenennen, Inhalt unverändert)
- **Ansatz:** Bei `state.AssemblySymbolIdentity != null` erste Ebene via `FlattenToTopLevelMeaningfulNamespaces(compilation.GlobalNamespace, projectTrees)` ausgeben, nicht nur „40 Namespaces / 199 Typen“.

### Session-Limit 16 / `ANALYSIS_FAILED` (`degraded`)

- **Pfad:** `src\AiNetLinter\Mcp\Assemblies\Analysis\AssemblyAnalysisResourceBudget.cs`; `AssemblyAnalysisRegistryDisposal.cs`
- **Symbol:** `ExternalResourceRegistryDefaults.MaxResidentResources` (`16`); `AssemblyAnalysisRegistryDisposal` (Meldung „Das externe Ressourcenlimit ist ausgeschöpft ({MaxResidentResources} Einträge).“)
- **Ansatz:** Eine residente Session pro kanonischem `targetPath` wiederverwenden; temporäre Referenz-Sessions weiter über `EvictTemporaryReferenceSessionsAsync` rauswerfen, bevor Capacity failt. Limit in `GetNamespaceTreeDescription` nennen.

### Header `completeness=partial` vs. Body „vollständig“ (`degraded`)

- **Pfad:** `src\AiNetLinter\Mcp\Assemblies\Analysis\AssemblyAnalysisResponse.cs`; `AssemblySessionStatusExtensions.cs`; `src\AiNetLinter\Mcp\McpSufficiencyHints.cs`; `GetNamespaceTreeTool.cs`
- **Symbol:** `AssemblyAnalysisResponse.FormatHeader` (`completeness={metadata.Completeness}` aus `ToCompletenessLabel` = Sessionstatus); `GetNamespaceTreeTool.ExecuteProjectDrilldownInternalAsync` (`McpSufficiencyHints.Append` wenn `!treePayload.Truncated`)
- **Ansatz:** Sufficiency-Satz unterdrücken, sobald Assembly-Header `partial`/`degraded` ist, oder Completeness im Body an den Sessionstatus koppeln. Zwei verschiedene Completeness-Begriffe nicht mischen.

### Leaf + `includeTypes=false` → „Keine Namespaces mit Typen gefunden“ (`degraded`)

- **Pfad:** `src\AiNetLinter\Mcp\Tools\FileStructure\GetNamespaceTreeScanner.cs`
- **Symbol:** `GetNamespaceTreeScanner.RenderNamespaceTree` (`shownList.Count == 0`); `CollectNamespaceTreeNodes` (nur `GetNamespaceMembers()`, nicht die Typen des Start-NS)
- **Ansatz:** Wenn `startNs` Typen hat und keine Kinder: eine Zeile für den Prefix-Namespace plus Hinweis `includeTypes=true` für die Typ-Liste. `CollectSourceTypes(startNs, projectTrees)` ist schon da.

### Depth mit/ohne Prefix zählt anders (`degraded`)

- **Pfad:** `src\AiNetLinter\Mcp\Tools\FileStructure\GetNamespaceTreeScanner.cs`
- **Symbol:** `CollectNamespaceTreeNodes` (`currentDepth: 1`); `FlattenToTopLevelMeaningfulNamespaces` (ohne Prefix wird die Wurzel als Tiefe 1 gezählt und Ketten kollabiert)
- **Ansatz:** Prefix-Wurzel nicht mitzählen (Kinder starten bei Depth 1) **oder** ohne Prefix denselben Zähler: Flatten-Wurzel = Depth 0, `depth=2` erreicht `Shared.Kalender`. Eine Semantik, in der Description festschreiben.

### `kind` nicht rekursiv (`friction` / FN)

- **Pfad:** `src\AiNetLinter\Mcp\Tools\FileStructure\GetNamespaceTreeScanner.cs`; `SymbolKindClassifier.cs`
- **Symbol:** `RenderNamespaceTypes` → `CollectSourceTypes` = nur `INamespaceSymbol.GetTypeMembers()` der **einen** Ebene; `SymbolKindClassifier.MatchesTypeKind`
- **Ansatz:** Entweder Kind-Namespaces mit `GetNamespaceMembers()` mittraversieren oder Description/Schema: „nur dieser Namespace“. `IsValidKind` existiert bereits (`GetNamespaceTreeTool.ExecuteAsync`).

### Keine Typ-IDs / keine Pagination (`wish`)

- **Pfad:** `src\AiNetLinter\Mcp\Tools\FileStructure\GetNamespaceTreeModels.cs`; `GetNamespaceTreeScanner.cs`
- **Symbol:** `TypeNodeEntry` (kein ID-Feld); `ToTypeEntry`; `RenderNamespaceTree` / `RenderNamespaceTypes` (`Take(MaxResults)`, kein Offset)
- **Ansatz:** `ISymbol.TryGetDocCommentId()` (`src\AiNetLinter\Core\RoslynSymbolExtensions.cs`) in die Typzeile (`T:…` / Assembly-SHA+DocId ohne Generation). Continuation analog `maxResults`-Skip, nicht nur „maxResults erhöhen“.

### Schema ohne Enums; Assembly-Tipps mit `project=` (`friction`)

- **Pfad:** `src\AiNetLinter\Mcp\Registration\FileStructureToolRegistrations.cs`; `GetNamespaceTreeScanner.cs`
- **Symbol:** `AddGetNamespaceTree` (`string targetType`, `string? kind = "all"`, `int depth = 1`); `AppendNamespaceTreeSummary` (Tipp immer `project="{parameters.Project.Name}"`)
- **Ansatz:** `kind`/`targetType` als Enum-Parameter der MCP-Lambda (SDK-Schema); `depth` 1–3 bleibt `Math.Clamp` in `GetNamespaceTreeTool.ExecuteAsync`. Assembly-Tipps: `namespacePrefix` + `includeTypes=false`/`depth=2`, `project=` nur bei Solution-Ziel.
