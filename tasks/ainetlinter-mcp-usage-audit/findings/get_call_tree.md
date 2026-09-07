# Finding: `get_call_tree`

Datum: 2026-09-07  
Anker Projekt: `T:Sample.Project.Infrastructure.Sql.DataExecutor` / `DataExecutor.QueryAsync` (`DataExecutor.cs:20`) / `ComponentRegistrationAttribute`  
Anker Assembly: `C:\ExternalAssemblies\Version-9\Example.External.Core.dll` — `TableColumn` / `TableColumn.ToString`

## 1. Schema-Kurzfazit

Beschreibung steuert den Call **im Kern richtig**: `symbolIdentifier` (Aliase `symbol` / `identifier` / `name`), `direction` `incoming`|`outgoing`|`both`, `depth` (Default 2, hard cap 5), `topN` (Default 10, Fan-Out pro Ebene), `format` `ascii`|`mermaid`, `includeBcl`, `includeReferences` bei Assembly, Traversierung hart 250 Knoten.

Was in die Irre führt:

- JSON-`required` sind nur `targetType`/`targetPath`. Runtime verlangt trotzdem `symbolIdentifier` (oder Alias) — Schema und Fehlertext widersprechen sich.
- Es gibt **kein** `maxResults`/`cursor`. Fortsetzung heißt „`topN` erhöhen“, bis der 250-Knoten-Cap greift — dann ist der Rest unereichbar.
- Ankername `DataExecutor` bzw. `[ComponentRegistration]` ist als Kurzname **nicht** eindeutig bzw. nicht gefunden. Agent muss `T:`/`M:`/`Datei.cs:Zeile:Spalte` oder `ComponentRegistrationAttribute` kennen.
- Auf einem **Typ** ist `outgoing` faktisch leer, die Antwort behauptet aber „vollständig“. Beschreibung sagt „wen ruft das Symbol auf“ — für Klassen/Attribute ist das ein Reference-Graph, kein Call-Graph.
- `format=json` wird still auf ASCII zurückgefallen, nicht abgelehnt.
- Assembly-IDs im Format `assembly:<sha>:<generation>:<M:…>` aus `AMBIGUOUS_SYMBOL` sind **generationsgebunden** und im Folgetool schnell ungültig.

## 2. Ausgeführte Calls

Alle Calls: `CallDynamicTool` / `user-AiNetLinter` / `get_call_tree`. Wartezeit durchweg **schnell** (kein Timeout). `targetPath` Projekt = Workspace-Root, Assembly = externe Assembly oben.

| # | Parameter | Größe (ca.) | Truncation / completeness | Wartezeit |
|---|-----------|-------------|---------------------------|-----------|
| 1 | project, `name=DataExecutor`, `outgoing`, depth 2 | Fehlertext ~2k | `AMBIGUOUS_SYMBOL` (Properties + Klasse, 2× `T:` wegen Partial) | schnell |
| 2 | project, `name=DataExecutor`, `incoming`, depth 2 | wie #1 | wie #1 | schnell |
| 3 | project, `symbolIdentifier=T:…DataExecutor`, `outgoing` | ~200 Zeichen, 2 Zeilen | Hinweis „vollständig“ — Baum ohne Kinder | schnell |
| 4 | project, `T:…DataExecutor`, `incoming`, depth 2, topN Default 10 | ~4–5k, ~40 Zeilen | „Baum trunkiert … topN erhöhen“, `… und 99 weitere` | schnell |
| 5 | project, `name=ComponentRegistration`, `outgoing` | Fehler ~200 | `SYMBOL_NOT_FOUND` (Attribut heißt `ComponentRegistrationAttribute`) | schnell |
| 6 | project, **ohne** Symbol | Fehler ~250 | `INVALID_ARGUMENT` Pflicht `symbolIdentifier` | schnell |
| 7 | project, `T:…DataExecutor`, `outgoing`, **topN=2** | ~200 | weiterhin leer + „vollständig“ (topN irrelevant) | schnell |
| 8 | project, `T:…DataExecutor`, `incoming`, depth 3, **topN=3** | ~2k | Truncation `… und 106 weitere` | schnell |
| 9 | project, `T:…DataExecutor`, `direction=both`, `format=mermaid` | ~8k | nur `[incoming]`-Knoten; outgoing fehlt; topN-Truncation | schnell |
| 10 | project, `M:…DataExecutor.QueryAsync` (ohne Signatur) | Fehler ~1.5k | `AMBIGUOUS_SYMBOL` zwei Overloads, volle `M:`-IDs inkl. Backticks | schnell |
| 11 | project, `symbolIdentifier=DataExecutor.cs:20:17`, `outgoing`, depth 1 | ~700 | „vollständig“; Callees passen zum Methodenrumpf | schnell |
| 12 | project, `name=ComponentRegistrationAttribute`, `incoming` | ~4k | topN-Truncation; Domain-Handler + Tests | schnell |
| 13 | project, `DataExecutor.cs:20:17`, `outgoing`, **includeBcl=true**, depth 2 | ~3k | topN-Truncation (`… und 1 weitere`); BCL als `[ref: System.Runtime]` | schnell |
| 14 | project, `DataExecutor.cs:20:17`, `incoming`, topN 15, depth 2 | ~6k | topN `… und 7 weitere`; Produktions-Caller sichtbar | schnell |
| 15 | assembly, `name=Core` | Header + `SYMBOL_NOT_FOUND` | Session-Header `origin=decompiled; completeness=partial` | schnell |
| 16 | project, Alias `symbol=T:…DataExecutor` (Default incoming) | wie #4 | Alias funktioniert | schnell |
| 17 | project, Alias `identifier=DataExecutor.cs:20:17`, `outgoing` | ~1.5k | „vollständig“ | schnell |
| 18 | assembly, `name=ToString`, **includeReferences=true**, `outgoing` | Fehler | `AMBIGUOUS_SYMBOL` auf **fremden** DLLs (`System.Memory`, `Microsoft.Win32.Registry` im AiNetLinter-Cache) | schnell |
| 19 | assembly, `name=ToString`, `incoming`, includeReferences false | Fehler ~1.5k | `AMBIGUOUS_SYMBOL` **in** `Example.External.Core` + copyable `assembly:…:5:M:…` und `M:…` | schnell |
| 20 | project, `DataExecutor.cs:20:17`, `outgoing`, **depth=6** | ~3k | stiller Clamp auf 5; Hinweis „vollständig“ (Clamp nicht genannt) | schnell |
| 21 | project, `name=DataExecutor.QueryAsync`, `format=json`, `incoming` | Fehler | `AMBIGUOUS_SYMBOL` (viele Test-Fakes + echte Overloads); json nicht bewertet | schnell |
| 22 | project, ungültiger `targetPath=…\does-not-exist` | ~600 | `PROJECT_NOT_INITIALIZED` + `ainetlinter.project.json`-Rezept | schnell |
| 23 | assembly, `T:…TableColumn`, `outgoing` | Header + 2 Zeilen | leer + „vollständig“ (Typ) | schnell |
| 24 | assembly, `M:…TableColumn.ToString~System.String`, `incoming` | ~4k | topN `… und 32 weitere` | schnell |
| 25 | project, `T:…DataExecutor`, `format=json`, `outgoing` | ~200 ASCII | **kein** JSON; stiller Fallback | schnell |
| 26 | project, `direction=sideways` | ~200 | `INVALID_ARGUMENT`, erlaubte Werte genannt | schnell |
| 27 | assembly, nicht existierende DLL | ~250 | `INVALID_ARGUMENT` Pfad muss existieren | schnell |
| 28 | assembly, `M:…TableColumn.ToString`, `outgoing` | Header + 2 Zeilen | leer ohne `includeBcl` (Body ist String-Concat) | schnell |
| 29 | assembly, `T:…TableColumn`, `incoming`, **includeReferences=true**, topN 5 | ~2k + Diagnosen | sinnvolle Caller; danach 100 Assembly-Diagnosen, 5 Samples | schnell |
| 30 | project, `T:…DataExecutor`, `incoming`, depth 4, **topN=40** | **94.5 KB, 475 Zeilen** | **hard-cap 250 Knoten**; eine Ebene `… und 1235 weitere` | schnell |
| 31 | assembly, ohne Symbol | Header + Fehler | `INVALID_ARGUMENT` Pflichtparameter | schnell |
| 32 | project, volle `M:…QueryAsync``1(DataExecutionScope,…)` | ~700 | trifft Overload Zeile 20; outgoing korrekt | schnell |
| 33 | assembly, `name=TableColumn`, `both`, depth 1 | ~1k | nur `[incoming]`; outgoing leer; „vollständig“ | schnell |
| 34 | assembly, `assembly:<sha>:5:M:…ToString` (ID aus Call 19) | Fehler | `INVALID_ARGUMENT`: ID gehört nicht zur **aktuellen Generation** (Session war inzwischen generation 8) | schnell |
| 35 | project, `DataExecutor.cs:20:17`, `outgoing`, **topN=0** | ~250 | nicht abgelehnt; 1 Kind + Truncation (wirkt wie Clamp auf 1) | schnell |

## 3. Verdict

**Teilweise** wie beschrieben.

Methode + `Datei.cs:Zeile:Spalte` oder volle `M:`-ID: outgoing/incoming im **Projekt** stimmen mit dem Quelltext überein. Fehlerpfade (`SYMBOL_NOT_FOUND`, `AMBIGUOUS_SYMBOL`, ungültiges `direction`, fehlende DLL) sind klar.

Abweichend: Typ-`outgoing` leer aber „vollständig“; `format=json` still; `depth>5` still geclampt; Schema-`required` vs. Runtime; Assembly-`incoming` auf `ToString` ist ein **falscher** Call-Graph; `includeReferences=true` sucht zuerst in **fremden** Cache-Assemblies; `assembly:`-IDs veralten.

## 4. Schwere

**degraded**

Begründung: Der Agent erreicht das Ziel auf Projekt-Methoden mit Workaround. Ohne Workaround (Kurzname `DataExecutor`, Typ statt Methode, Assembly-`ToString`, `includeReferences`) ist das Ergebnis leer, testüberflutet, token-schwer oder faktisch falsch. Assembly-`ToString`-Incoming wäre isoliert `broken`; das Gesamttool bleibt nutzbar.

## 5. Nutzbarkeit

**nur mit Workaround**

- Nicht den Kurznamen `DataExecutor` / `ComponentRegistration` / `ToString` verwenden.
- Call-Graph nur auf **Methoden** (`DataExecutor.cs:20:17` oder volle `M:` aus `AMBIGUOUS_SYMBOL`).
- Typ/`Attribute` höchstens als Reference-Einstieg, nicht als „wer ruft auf“.
- Assembly: `M:` **ohne** `assembly:<sha>:<gen>:`-Präfix; `includeReferences` default false lassen.
- `topN` klein halten; bei 250-Cap nicht weiter aufblasen, sondern engeres Symbol wählen.
- `includeBcl` nur wenn Framework-Leaves wirklich gebraucht werden.

## 6. Bugs, False Positives / False Negatives

**Ist-Stichprobe (Read/`rg`, kein Vollabgleich):**

- `DataExecutor.cs:20` outgoing listet `MergeParametersForSql`, `OpenConnectionAsync`, `RunWithLoggingAsync`, `ToDynamicParameters`, Dapper `QueryAsync` — entspricht dem Rumpf. **TP.**
- `UserConfigService.cs:46` und `UserConfigPageScopeCatalog.cs:30/58` rufen `_dataExecutor.QueryAsync` auf. Incoming auf `QueryAsync` (topN 15) zeigte Domain-Stores und `AiSessionStore`, endete mit `… und 7 weitere` — **UserConfig ist agentensichtbar ein FN durch topN**, nicht durch Roslyn-Miss.
- Typ-Incoming `DataExecutor` beginnt mit `Tests.Ama`-Hosts und Compiler-Generates (`<Scenario>k__BackingField`). Produktions-Handler erscheinen erst hinter `… und 99 weitere` bzw. nach 250-Cap gar nicht. **Ranking/Testflut, kein reiner FP.**
- `ComponentRegistration` → `SYMBOL_NOT_FOUND`; `ComponentRegistrationAttribute` incoming enthält `CompanyCalendarHandler.cs:12` — Attribut sitzt dort. **TP als Reference, nicht als Call.**
- Assembly `TableColumn` incoming → `TabellenSchemaService.SpaltenToSqlParameters` (`List<TableColumn>`). Dekompilat bestätigt. **TP.**
- Assembly `TableColumn.ToString` incoming nennt u. a. `AccessDatabase.GetColumns:230` und `BlobStorageProvider.PutFile:135`. Ist: `row["COLUMN_NAME"].ToString()` bzw. `Guid.NewGuid().ToString()` — **nicht** `TableColumn.ToString`. **Schwerer FP** (Override vs. beliebige `ToString`-Calls im Dekompilat).
- `includeReferences=true` + `name=ToString` auf der externe Assembly: Ambiguity-Treffer in `System.Memory.dll` / `Microsoft.Win32.Registry.dll` unter `C:\Daten\Tools\AiNetLinter-win-x64\`. **Falsches Target / Session-Leak.**
- Partial-Klasse `DataExecutor` erscheint in Ambiguity **zweimal** als `T:` (`.cs:9` und `OptimisticConcurrency.cs:11`). Kosmetischer FP der Auflösung.
- `both` auf Typen: nur incoming; outgoing-Seite fehlt ohne Warnung.

## 7. Token / IDs

- Erfolgsbaum: `Name — relativerPfad:Zeile` **ohne** `T:`/`M:` in den Knoten. Folgetool geht über `Datei.cs:Zeile:Spalte` (hat für QueryAsync funktioniert). Kein `cursor`.
- `AMBIGUOUS_SYMBOL` liefert die brauchbarsten IDs (`T:…`, volle `M:…` mit ````1``). Das ist der eigentliche ID-Kanal.
- Assembly-IDs `assembly:<sha256>:<generation>:<symbolId>`: Generation stieg in dieser Session von 2 auf 8. ID aus generation 5 war bei generation 8 **ungültig**. Agent darf diese IDs nicht cachen.
- `includeReferences=true`: Diagnoseflut (100 Diagnosen, 5 Samples) plus doppelte `[assembly=… origin=decompiled]`-Suffixe — Token ohne Nutzen für den Call-Tree.
- 250-Cap-Antwort: 94.5 KB, fast nur Tests, Rest `… und 1235 weitere` — für einen Agenten-Turn zu groß und unvollständig zugleich.
- Completeness-Sprache widersprüchlich: leerer Typ-outgoing = „vollständig, kein Read/Grep nötig“; das ist für den Anker `DataExecutor` **falsch**.

## 8. Roslyn-konforme Wünsche

- Call-Tree default auf **Methoden**; bei Typ/Attribut explizit „reference tree“ labeln oder auf `find_references` verweisen.
- `outgoing` auf Typen: entweder Member-Fan-out (welche Methoden rufen wen auf) oder Fehler „kein ausführbarer Body“.
- Incoming: Option `scopeType` analog `search_pattern` (`production`/`tests`) oder Tests nachrangig ranken.
- `ToString`/`Equals`/`GetHashCode`: nur Call-Sites mit passendem Receiver-Typ, nicht jedes `*.ToString()` im Compilation.
- Stabile IDs in **jedem** Knoten (`T:`/`M:`); Assembly-IDs ohne Generation oder mit Auto-Rebind. `assembly:`-Präfix nicht als Copy-Paste für den nächsten Call verkaufen, wenn er verfällt.
- Schema: `symbolIdentifier` required; `format` Enum hart (`ascii`|`mermaid`); `depth`/`topN` Min/Max und Clamp im Text; kein stilles `json`.
- Truncation: `continuationToken` oder „nächste Geschwister ab Offset“, statt nur „topN erhöhen“ bis 250 tot sind.
- `includeReferences` default false belassen; bei true **zuerst** die Ziel-DLL, nicht AiNetLinter-Nebenassemblies.

## 9. Phase 3

AiNetLinter-Repo `C:\Daten\Entwicklung\Ralf\AiNetLinter`, read-only. Je Nicht-ok-Befund: Pfad, Symbol, Roslyn-Ansatz.

### friction — JSON-`required` vs. Runtime-`symbolIdentifier`

- **Pfad:** `src\AiNetLinter\Mcp\Registration\SymbolGraphToolRegistrations.cs`; `src\AiNetLinter\Mcp\Tools\CallTree\GetCallTreeTool.cs`
- **Symbol:** `SymbolGraphToolRegistrations.AddGetCallTree`; `GetCallTreeTool.ExecuteAsync`
- **Ansatz:** Wie `find_references`: Pflicht ist `GetCallTreeInput.EffectiveSymbolIdentifier`. Schema-Overlay oder Beschreibung: eines von `symbolIdentifier`/`symbol`/`identifier`/`name` ist Pflicht. `targetType`-Enum im Fließtext ist bereits in `GetCallTreeDescription` + `ReadOnlyTargetContract`.

### degraded — Typ-`outgoing` leer, Antwort „vollständig“

- **Pfad:** `src\AiNetLinter\Mcp\Tools\SymbolGraph\OutgoingCallScanner.cs`; `src\AiNetLinter\Core\DuplicateDetection\MethodBodyLocator.cs`; `src\AiNetLinter\Mcp\Tools\CallTree\GetCallTreeTool.cs`; `src\AiNetLinter\Mcp\McpSufficiencyHints.cs`
- **Symbol:** `OutgoingCallScanner.ScanAsync`; `MethodBodyLocator.GetBody`; `GetCallTreeTool.ExecuteAsync`; `McpSufficiencyHints.Append`
- **Ansatz:** `GetBody` liefert `null` für `TypeDeclarationSyntax` → keine Invocations → leerer Baum → kein `HasTreeOverflow` → Sufficiency-Hint. Bei Seed `INamedTypeSymbol`: entweder Member-Fan-out (`type.GetMembers().OfType<IMethodSymbol>()`, Bodies per `ScanAsync`) **oder** `INVALID_ARGUMENT` / explizites Label „kein ausführbarer Body — find_references für Typnutzung“. `both` ohne Outgoing-Kinder nicht als vollständigen Call-Graph deklarieren.

### broken — Assembly-`ToString`-Incoming ist Object.ToString-Cascade

- **Pfad:** `src\AiNetLinter\Mcp\Tools\SymbolGraph\CallGraphTreeBuilder.cs`; `src\AiNetLinter\Mcp\Tools\SymbolGraph\CallGraphTraversal.cs`
- **Symbol:** `CallGraphTreeBuilder.BuildGroupsAsync` (`SymbolFinder.FindReferencesAsync`); `CallGraphTraversal.AppendReferenceLocations`
- **Ansatz:** `FindReferencesAsync` ohne Options cascaded Overrides auf `object.ToString`. Filter: `ReferencedSymbol.Definition` per `SymbolEqualityComparer` gleich Seed; bei virtuellen `SpecialType`/`WellKnownMember` (`ToString`/`Equals`/`GetHashCode`) zusätzlich Receiver prüfen — `SemanticModel.GetTypeInfo` auf der `InvocationExpressionSyntax` bzw. `MemberAccessExpressionSyntax`, `ConvertedType`/`Type` muss `seed.ContainingType` (oder abgeleitet) sein. Dasselbe Filter in der flachen Traversierung, damit `find_references` nicht denselben FP erbt.

### degraded — `includeReferences=true` löst Kurzname in Fremd-DLLs (Session-Leak)

- **Pfad:** `src\AiNetLinter\Mcp\Tools\SymbolGraph\AssemblySymbolResolver.cs`; `src\AiNetLinter\Mcp\Tools\SymbolGraph\AssemblyNavigationLeaseAccess.cs`; `src\AiNetLinter\Mcp\Tools\CallTree\AssemblyGetCallTreeTool.cs`
- **Symbol:** `AssemblySymbolResolver.ResolveCandidatesAsync`; `AssemblyNavigationLeaseAccess.GetLeases`; `AssemblyGetCallTreeTool.BuildResponseAsync`
- **Ansatz:** Zuerst nur Root-Lease (`leases[0]` / `root`) auflösen. Referenz-Leases (`ReferenceLeasesSnapshot`, inkl. `System.Memory` / `Microsoft.Win32.Registry` im AiNetLinter-Cache) erst bei 0 Root-Treffern oder bei vollqualifizierter `T:`/`M:`-ID. Kurzname + `includeReferences` darf nicht `AMBIGUOUS_SYMBOL` über Nebenassemblies liefern.

### degraded — Assembly-IDs `assembly:sha:generation:` veralten

- **Pfad:** `src\AiNetLinter\Mcp\AnalysisSymbolIdentity.cs`; `src\AiNetLinter\Mcp\Tools\SymbolGraph\SymbolIdentifierResolver.cs`; `src\AiNetLinter\Mcp\Assemblies\Analysis\AssemblyAnalysisRegistry.cs`
- **Symbol:** `AnalysisSymbolIdentity.Format`; `SymbolIdentifierResolver.TryNormalizeAssemblyId` / `StaleAssemblyId`; `AssemblyAnalysisRegistry` (`nextGenerations`)
- **Ansatz:** Wie die anderen Symbolgraph-Tools: Wire-ID ohne Generation, Rebind SHA+DocCommentId. `GetCallTreeTool` hängt `state.AssemblySymbolIdentity` an `CallGraphTraversal.GetStableSymbolId` — dort `Format` umstellen. Ambiguity-Copy-Paste dann sessionstabil.

### degraded — Incoming-Ranking / 250-Cap: Tests zuerst, Produktion hinter `… und N weitere`

- **Pfad:** `src\AiNetLinter\Mcp\Tools\SymbolGraph\CallGraphTreeBuilder.cs`; `src\AiNetLinter\Mcp\Tools\MetricsTree\MetricsTreeRenderer.cs`; `src\AiNetLinter\Core\TestDetector.cs`
- **Symbol:** `CallGraphTreeBuilder.SortGroups`; `MetricsTreeRenderer.RenderChildren`; `TestDetector.IsTestFile`
- **Ansatz:** `SortGroups` (`OrderBy FirstLocationPath`) durch Production-first ersetzen (`!IsTestFile`). `topN` greift dann auf die sortierte Liste (`ExpandNodeAsync` `recursed >= TopN`). Optional `scopeType`. Hard-cap `MaxCallTreeNodes` (250) bleibt; Truncation-Hint statt „topN erhöhen“, sobald Cap greift — Rest ist mit größerem `topN` unerreichbar.

### degraded — Knoten ohne `T:`/`M:`; kein Continuation-Token

- **Pfad:** `src\AiNetLinter\Mcp\Tools\SymbolGraph\CallGraphTreeBuilder.cs`; `src\AiNetLinter\Mcp\Tools\MetricsTree\MetricsTreeRenderer.cs`; `src\AiNetLinter\Mcp\Tools\CallTree\GetCallTreeModels.cs`
- **Symbol:** `CallGraphTreeBuilder.ToMetricsTreeNode` / `AddChild`; `MetricsTreeNode`; `CallTreePayload`
- **Ansatz:** `MetricsTreeNode` um optionale `Id` aus `CallGraphTraversal.GetStableSymbolId(group.CallerSymbol)` erweitern; Renderer gibt `` id: `M:…` `` neben `Pfad:Zeile` aus. Truncation: Offset der Geschwisterliste (nicht nur „topN erhöhen“) in `CallTreePayload`, analog Assembly-Paging.

### friction — `format=json` stilles ASCII; `depth>5` stiller Clamp; `topN=0` → 1

- **Pfad:** `src\AiNetLinter\Mcp\Tools\CallTree\GetCallTreeTool.cs`; `src\AiNetLinter\Mcp\Tools\SymbolGraph\CallGraphTreeBuilder.cs`; `src\AiNetLinter\Mcp\Registration\SymbolGraphToolRegistrations.cs`
- **Symbol:** `GetCallTreeTool.RenderTree` / `ExecuteAsync`; `CallGraphTreeBuilder.BuildTreeAsync` (`Math.Clamp`); `GetCallTreeDescription`
- **Ansatz:** Unbekanntes `format` → `INVALID_ARGUMENT` (erlaubt `ascii`|`mermaid`), kein Fall-through auf `MetricsTreeRenderer`. `requestedDepth != clamped` analog `TraversalCompleteness.DepthWasClamped` in den Footer. `topN < 1` ablehnen statt `input.TopN < 1 ? 1`. Clamp in der Beschreibung nennen.

### friction — Kurzname / Partial-Duplikat in `AMBIGUOUS_SYMBOL`

- **Pfad:** `src\AiNetLinter\Mcp\Tools\SymbolGraph\FindReferencesTool.cs`; `src\AiNetLinter\Mcp\Tools\SymbolGraph\FindSymbolTool.cs`
- **Symbol:** `FindReferencesTool.ResolveByNameAsync`; `FindSymbolTool.FormatSymbolLocationEntries`
- **Ansatz:** Gemeinsame Auflösung mit `get_call_tree`. Ambiguity-Zeilen per `DocumentationCommentId.CreateDeclarationId` deduplizieren (Partial `DataExecutor` = eine `T:`-ID, zwei Dateizeilen in StructuredContent ok, nicht zwei Kandidaten). Ranking: NamedType vor Property, Produktion vor Tests (`TestDetector`).

### friction — `PROJECT_NOT_INITIALIZED` für fehlenden Projektpfad

- **Pfad:** `src\AiNetLinter\Mcp\Projects\ProjectDefinitionLoader.cs`
- **Symbol:** `ProjectDefinitionLoader.Load`
- **Ansatz:** Wie `find_symbol`: fehlendes Verzeichnis ≠ fehlende Integration. `Directory.Exists` vor `NotInitializedTemplate`. Assembly-Nicht-Existenz ist in `AnalysisTargetResolver.Resolve` bereits korrekt `INVALID_ARGUMENT`.

### degraded — `includeReferences`: Diagnoseflut im Call-Tree-Markdown

- **Pfad:** `src\AiNetLinter\Mcp\Tools\SymbolGraph\TransitiveCallGraphFormatter.cs`; `src\AiNetLinter\Mcp\Tools\CallTree\AssemblyGetCallTreeTool.cs`
- **Symbol:** `TransitiveCallGraphFormatter.FormatAssemblyCallTreeResponse` / `AppendDiagnosticMetadata`; `AssemblyGetCallTreeTool.BuildResponseAsync`
- **Ansatz:** Tree-Body unvermischt lassen; Diagnosen Default aus, nur StructuredContent/`Navigation.Diagnostics` (Cap `MaxDiagnosticSamples` bleibt). Completeness `partial` bei Diagnosen ist ehrlich — nicht 100 CS-Zeilen in denselben Turn wie die Caller.

### wish — Call-Tree default Methoden; `scopeType`; stabile IDs in jedem Knoten

- **Pfad:** `src\AiNetLinter\Mcp\Tools\CallTree\GetCallTreeTool.cs`; `src\AiNetLinter\Mcp\Tools\Analysis\SearchPatternScanner.Scope.cs`
- **Symbol:** `GetCallTreeTool.ExecuteAsync`; `SearchPatternScanner.IsExcludedByScopeType`
- **Ansatz:** Nach `ResolveSymbolAsync`: wenn Seed kein `IMethodSymbol`/`IPropertySymbol`/`IEventSymbol`, Hinweis auf `find_references` oder expliziter Reference-Tree-Modus. `scopeType` in `CallTreeBuildRequest` und in `SortGroups` anwenden. IDs siehe `ToMetricsTreeNode` oben. Rein Roslyn.
