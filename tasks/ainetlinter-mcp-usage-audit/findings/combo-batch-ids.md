# Finding: Kombination Batch-IDs (`namePatterns` / `symbolIdentifiers`)

Namespace: `user-AiNetLinter`. Live-Calls 2026-09-07.  
Projekt: `targetType=project`, `targetPath=C:\Workspace\Sample.Project`.  
Assembly: `targetType=assembly`, `targetPath=C:\ExternalAssemblies\Version-9\Example.External.Process.dll`.  
Anker: `DataExecutor`, `ComponentRegistrationAttribute`; Assembly `Record` / `BusinessOperationConstants`.  
Kette: `find_symbol` (Batch) → zurückgegebene IDs in `get_symbol_body` (Batch), `find_references`, `get_feature_context`.

## 1 Schema-Kurzfazit

**`find_symbol`:** `namePatterns` ist das Batch-Array (max. 10, Beschreibung und Live-Cap stimmen). Aliase `namePattern` / `pattern` / `symbol` / `query` / `name` nur für **ein** Muster. JSON-`required` listet nur `targetType`/`targetPath`; leer/`[]` → Laufzeit `INVALID_ARGUMENT` Pflicht `namePatterns`.

**`get_symbol_body`:** `symbolIdentifiers` ist das Batch-Array. Beschreibung: „eines oder mehrerer C#-Symbole (Batch-Support in 1 Turn)“. Kein Cap im Schema. Aliase `symbolIdentifier` / `symbol` / `identifier` / `name` für genau ein Symbol.

**`find_references`:** nur Singular `symbolIdentifier` (Aliase `symbol` / `identifier` / `name`). **Kein** `symbolIdentifiers[]`. Extra-Array wird still ignoriert.

**`get_feature_context`:** nur Singular `symbolIdentifier`. Assembly ausdrücklich unsupported. Extra-Array still ignoriert.

**Steuert der Call richtig?** Für den Happy Path ja, sobald der Agent die unterschiedlichen Array-Namen kennt (`namePatterns` vs. `symbolIdentifiers`) und Folgetools **nicht** batched. Irreführend:

- Workflow-Text „Batch löst N sequentielle Calls ab“ sitzt nur auf `find_symbol`. `find_references` / `get_feature_context` tun das nicht — derselbe Agent-Reflex (`symbolIdentifiers: [...]`) scheitert mit „Pflichtparameter fehlt“, nicht mit „Batch unsupported“.
- `get_symbol_body` hat live **kein** 10er-Cap (11 IDs ok), `find_symbol` schon.
- Assembly-IDs tragen `assembly:<sha256>:<generation>:<DocId>`. Schema/Beschreibung der Folgetools erwähnen Generation nicht als Verfallsmerkmal.
- Cross-Target: Assembly-ID auf `project` und falscher SHA liefern denselben Text „gehört nicht zur aktuellen Assembly-Generation“ — auch wenn das Ziel gar keine Assembly-Session ist.

## 2 Calls

Alle Wartezeiten **schnell**, kein Timeout. Größen = sichtbarer Markdown-Text.

| # | Tool | Parameter (kurz) | Ergebnis | Größe / Truncation |
|---|------|------------------|----------|--------------------|
| 1 | `find_symbol` | project, `namePatterns=["^DataExecutor$","^ComponentRegistrationAttribute$"]`, `kind=class`, `maxResults=10` | 2 Blöcke; IDs `T:…DataExecutor` (Partial doppelt) und `T:…ComponentRegistrationAttribute` | ~0,8k; vollständig |
| 2 | `get_symbol_body` | project, `symbolIdentifiers` = beide `T:` aus #1, `maxBodyLines=8` | zwei Bodies, IDs unverändert echo | ~2k; 8/292 bzw. 8/13 |
| 3 | `find_references` | project, Singular `T:…DataExecutor`, `maxResults=5` | 246 Treffer, 5 gezeigt (Tests zuerst) | ~1k; Truncation-Hint „Pattern“ |
| 4 | `get_feature_context` | project, Singular `T:…ComponentRegistrationAttribute`, Includes aus | Deklaration + DocCommentId identisch zu #1 | ~0,5k |
| 5 | `find_symbol` | project, `namePatterns=["DataExecutor","ComponentRegistrationAttribute"]`, `kind=class`, `maxResults=8` | DataExecutor-Block: **nur Test-Fakes** (`ThrowingDataExecutor`, …), Produktionsklasse nicht in den 8; Attribute-Block korrekt | ~3k; Banner „9/8“ |
| 6 | `find_symbol` | assembly, `namePatterns=["Record","BusinessOperation"]`, `kind=class`, `maxResults=5` | Header `generation=7`; Record-Block: **Fields** `_belegHandle` … (`F:`-IDs); BusinessOperation: Exception/Konstanten-Klassen, nicht `Record` | ~4k; Cache-Absolutpfade |
| 7 | `find_symbol` | assembly, `namePatterns=["^Record$","^BusinessOperationConstants$"]`, `kind=class` | `assembly:…:7:T:…Record` und `…:7:T:…BusinessOperationConstants` | ~1,5k |
| 8 | `get_symbol_body` | assembly, `symbolIdentifiers` zwei frische `:7:T:` aus #6/#7 | beide Bodies, IDs `:7:` echo, `decompiledProject` | ~2k |
| 9 | `get_symbol_body` | assembly, Batch `:1:T:…Record` (stale) + gültige `:7:T:…Konstanten…` | **erster Eintrag** `INVALID_ARGUMENT` Generation; zweiter Body ok | ~1,5k; Batch bricht nicht ab |
| 10 | `get_symbol_body` | assembly, Projekt-`T:` DataExecutor + ComponentRegistrationAttribute | zwei `SYMBOL_NOT_FOUND` | ~1k |
| 11 | `get_symbol_body` | **project**, frische Assembly-`:7:`-ID | `INVALID_ARGUMENT` „nicht zur aktuellen Assembly-Generation“ | ~0,4k |
| 12 | `get_feature_context` | project, Fake-ID aus #5 `T:…ThrowingDataExecutor` | Erfolg — **privater Test-Fake**, nicht Produktions-`DataExecutor` | ~1,2k |
| 13 | `find_symbol` | project, `namePatterns` Methoden `ExecuteAsync` + `CreateCommandExecutor`, `kind=method`, `maxResults=3` | `M:…~ReturnType`-IDs; ExecuteAsync trifft **Interface** `IScheduledJob` / `IDataQueryExecutor`, nicht `DataExecutor` | ~2,5k |
| 14 | `find_references` | assembly, `:7:T:…Record`, `maxResults=3` | 133 Treffer, Generation bleibt **7** | ~1,5k |
| 15 | `get_feature_context` | project, Assembly-`:7:T:…Record` | `INVALID_ARGUMENT` Generation (Tool ist assembly-unsupported; Fehlertext lügt) | ~0,4k |
| 16 | `find_references` | project, `symbolIdentifiers=[T:DataExecutor, T:ComponentRegistrationAttribute]` **ohne** Singular | `INVALID_ARGUMENT` Pflicht `symbolIdentifier` — Array ignoriert | ~0,3k |
| 17 | `find_symbol` | project, 11 Patterns `A`…`K` | Cap: `Maximal 10 namePatterns … (angefordert: 11)` + Hint aufteilen | ~0,3k |
| 18 | `get_symbol_body` | project, `symbolIdentifiers` = zwei `Datei:Zeile` aus #1 | beide Treffer, kanonische `T:` echo | ~1k |
| 19 | `get_symbol_body` | project, beide `M:…~ReturnType` aus #13 | CreateCommandExecutor Body; ExecuteAsync Interface, `bodyAvailability=unavailable` | ~1,8k |
| 20 | `get_feature_context` | project, `M:…CreateCommandExecutor…~…CommandExecutor` | Erfolg, DocCommentId inkl. `~` identisch | ~0,7k |
| 21 | `find_references` | project, `M:…IDataQueryExecutor.ExecuteAsync…~Task{Int32}` | 146 Treffer, Labels `IDataQueryExecutor.ExecuteAsync` | ~1k |
| 22 | `get_feature_context` | project, `symbolIdentifiers=[…]` ohne Singular | wie #16, Pflicht `symbolIdentifier` | ~0,3k |
| 23 | `get_symbol_body` | project, Mix gültig / `T:Does.Not.Exist.AtAll` / gültig | Body + `SYMBOL_NOT_FOUND` + Body (partieller Batch) | ~1,2k |
| 24 | `get_symbol_body` | assembly, nacktes `T:…Record` (ohne `assembly:`-Prefix), Alias `name=Record` daneben | Hit; Antwort-ID wird auf `assembly:…:7:T:…Record` normalisiert; `name` verliert gegen `symbolIdentifier` | ~0,8k; 3/7145 |
| 25 | `get_symbol_body` | project, **11** `T:`/`M:`-IDs, `maxBodyLines=1` | alle 11 Sektionen, **kein Cap** | ~4k |
| 26 | `find_symbol` | project, `namePatterns=["^DataExecutor$"]` **und** `namePattern=ComponentRegistrationAttribute` | nur `^DataExecutor$`-Block (Properties ohne `kind`); Alias **still verworfen** | ~0,8k |
| 27 | `find_references` | project, `Sample.Project/Infrastructure/Data/DataExecutor.cs:9` | dieselben 246 Refs wie `T:` | ~0,8k |
| 28 | `get_symbol_body` | assembly, SHA=`DEADBEEF…`, Generation 7, sonst gültiges `T:…Record` | `INVALID_ARGUMENT` Generation (SHA-Mismatch als Generation verkauft) | ~0,6k |
| 29 | `find_symbol` | project, `namePatterns=["DataExecutor",""]` | leeres Muster **still übersprungen**; nur DataExecutor-Properties, kein Fehler | ~0,8k |
| 30 | `find_symbol` | project, `namePatterns=[]` | `INVALID_ARGUMENT` Pflicht `namePatterns` fehlt oder ist leer | ~0,3k |
| 31 | `get_feature_context` | project, `P:…ScheduleBackgroundBuildRequest.DataExecutor` aus #26 | Erfolg, Property `IDataQueryExecutor`, 1 LOC | ~0,6k |
| 32 | `get_symbol_body` | assembly, `F:…_belegHandle` aus #6 | 1-Zeilen-„Body“ `_belegHandle`, ID `:7:` echo | ~0,8k |
| 33 | `find_symbol` | assembly, `^Record$` nach Dutzend Assembly-Calls | Generation **weiter 7**, gleiche ID wie #7 | ~0,9k |

Nicht ausgeführt: Call mit Generation-`:3:` (Cursor Auto-review hat den einzelnen Assembly-Body-Call blockiert). Stale-Generation ist über Call #9 (`:1:` vs. live `:7:`) abgedeckt.

## 3 Verdict

**teilweise** — Innerhalb **desselben Targets** und **derselben Assembly-Generation** sind kopierte IDs (`T:` / `M:…~Return` / `P:` / `F:` / `Datei:Zeile`) in allen drei Folgetools auflösbar. Batch-Handoff `find_symbol.namePatterns` → `get_symbol_body.symbolIdentifiers` funktioniert.

Abweichend: Folgetools `find_references` und `get_feature_context` sind nicht batched; Extra-Arrays verschwinden still. Assembly-IDs sind generation-gebunden und nach Session-/Agent-Wechsel tot. Substring-Batch liefert oft **gültige Fremd-IDs** (Test-Fake, Property, Field), die Folgetools brav auflösen — der Agent arbeitet am falschen Symbol.

## 4 Schwere

**degraded** (Projekt-Handoff mit Workaround brauchbar).  
**broken** für den Teilpfad „Assembly-ID aus einem früheren Turn/Agent in ein Folgetool stecken“: Generation wechselt (Welle-1-IDs `:1:`/`:2:`/`:3:` vs. dieser Combo-Lauf `:7:`); Call #9 ist reproduzierbar tot. Falscher SHA und Cross-Target-project nutzen denselben Fehlertext.

Nicht `ok`: Schema-Asymmetrie Batch vs. Singular und Ranking-IDs machen den Default-Pfad riskant.

## 5 Nutzbarkeit

**nur mit Workaround**

- `find_symbol`: Regex `^Name$` + `kind=class` (englisch), sonst kopiert der Agent `ThrowingDataExecutor` / `P:….DataExecutor` / `_belegHandle`.
- Folgetool-Batch nur bei `get_symbol_body` (`symbolIdentifiers`). Für Referenzen/Kontext: **ein Call pro ID**.
- Assembly: ID **sofort** im selben Target verbrauchen; nacktes `T:Namespace.Typ` am Assembly-Ziel ist robuster als `assembly:sha:gen:…` über Turns. `get_feature_context` für externe Assemblies nicht verwenden.
- `namePatterns` und `namePattern` nicht mischen — Array gewinnt, String stirbt still.

Ohne Workaround (nacktes Substring-Batch + erste ID in `get_feature_context`): ja, der Call „klappt“, aber am Test-Fake.

## 6 Bugs

Reproduzierbar, mit Call-Nummer.

1. **Generation-wechselnde Assembly-IDs.** Format `assembly:<64 hex>:<int>:<DocId>`. In diesem Combo-Lauf durchgängig `generation=7` (Call #6–#8, #14, #32–#33). Call #9 mit `:1:` (ältere Session) → `INVALID_ARGUMENT` „gehört nicht zur aktuellen Assembly-Generation“. Folgetool-IDs sind **nicht** sessionübergreifend stabil. Innerhalb eines Laufs blieb 7 konstant — der Sprung passiert zwischen Agenten/Turns, nicht bei jedem Call.
2. **Cross-Target und SHA als „Generation“.** Call #11 (Assembly-ID auf `project`) und Call #28 (falscher SHA, richtige Generation) denselben Generation-Fehler. Call #15: `get_feature_context` (assembly unsupported) ebenfalls Generation statt `TOOL_TARGET_UNSUPPORTED`. Call #10 (Projekt-`T:` auf assembly) ist ehrlicher: `SYMBOL_NOT_FOUND`.
3. **Batch-Array an Singular-Tools = stilles Drop.** Call #16/#22: `symbolIdentifiers` ohne `symbolIdentifier` → Pflichtfeld fehlt. Kein Hinweis „dieses Tool batched nicht; nimm `symbolIdentifier`“.
4. **Cap-Asymmetrie.** Call #17: `namePatterns` hart 10. Call #25: `symbolIdentifiers` 11 ohne Fehler. Beschreibung von `get_symbol_body` erwähnt kein Limit.
5. **`namePattern` vs. `namePatterns`.** Call #26: Array gewinnt, String-Alias ohne Warnung verworfen.
6. **Leeres Array-Element.** Call #29: `""` in `namePatterns` still übersprungen; `[]` (Call #30) Fehler. Kein Hinweis auf das ignorierte Muster.
7. **Gültige ID, falsches Symbol (Ranking).** Call #5 → Call #12: erste DataExecutor-`kind=class`-ID ist `ThrowingDataExecutor`; `get_feature_context` bestätigt den Fake. Call #6 `kind=class` + `Record` → Fields (`F:`), Call #32 löst sie. Call #13 `ExecuteAsync` → Interface-`M:`, Call #19/#21 folgen dem Interface, nicht `DataExecutor.ExecuteAsync`.
8. **Gefälschte Totale** (wie Einzeltool): Call #5 „9 gesamt, 8 gezeigt“; Regex+#kind in Call #1 hatte 2 Dateien derselben Klasse, keine 9. Agent erhöht `maxResults` unter falscher Annahme.
9. **`kind=class` undicht auf Assembly.** Call #6 listet Fields trotz `kind=class`.
10. **`find_references`-Truncation-Hint** nach ID-Handoff (Call #3/#14/#27) spricht von „Pattern verfeinern“ — das Folgetool hat kein `pattern`.

### FP / FN vs. Ist (Stichprobe)

- `T:Sample.Project.Infrastructure.Sql.DataExecutor` aus Call #1 = Klasse in `Infrastructure/Data/DataExecutor.cs:9`. Folgetools #2/#3/#18/#27 treffen denselben Typ. **TP.**
- `T:Sample.Project.Contracts.ComponentRegistrationAttribute` aus Call #1 = `Contracts/ComponentRegistrationAttribute.cs:8`. Call #4 DocCommentId identisch. **TP.**
- Call #5 listet **nicht** die Produktionsklasse unter den 8 DataExecutor-`kind=class`-Treffern. **FN** für den Anker, **TP** für Substring-Fakes. Folge-Call #12 ist dann TP *auf den Fake*.
- Call #6 trifft nicht `class Record` in `Record.cs:31` (Call #7 mit Regex schon). **FN** des Substring-Batch; die `F:`-IDs sind echte Fields. **kein ID-Bruch**, sondern Suchranking.
- Stale `:1:T:…Record` (Call #9): Klasse existiert im Dekompilat (Call #7/24/33). **Tool-FN durch ID-Generation**, nicht durch fehlendes Symbol.

## 7 Token/IDs

**Batch `find_symbol`:** eine Antwort, getrennte Überschriften, IDs in Backticks — Copy-Paste nach `get_symbol_body.symbolIdentifiers` ist der einzige günstige 1-Turn-Handoff. `maxResults` gilt **pro** Pattern.

**Batch `get_symbol_body`:** N Bodies in einem Turn, gemischte Fehler bleiben lokal (Call #9/#23). `maxBodyLines` begrenzt Token; 11×`maxBodyLines=1` bleibt klein, Default 80×N würde explodieren (Record 7145 Zeilen).

**`find_references` / `get_feature_context`:** kein Batch → N Folge-Turns. Default-Refs für `DataExecutor` sind Testhits (246 gesamt). Folgeliste enthält **keine** Symbol-IDs, nur `Datei:Zeile` — die sind wieder als Identifikator nutzbar (Call #27), aber nicht als DocCommentId ausgewiesen.

**ID-Stabilität Projekt:** `T:` / `P:` / `M:…(~ReturnType)` / `Datei:Zeile` roundtrip-stabil. `get_symbol_body` und `get_feature_context` echoen dieselbe DocCommentId. `M:`-IDs aus `find_symbol` **mit** `~ReturnType` funktionieren in allen drei Folgetools (Call #19–#21) — nicht abschneiden.

**ID-Stabilität Assembly:** Prefixe `assembly:{SHA}:{generation}:{T|M|P|F}:…`. SHA war in diesem Lauf `2AFC08F1…AAC35`, Generation **7**. Nacktes `T:externde…Record` am Assembly-Ziel wird akzeptiert und zur aktuellen Generation normalisiert (Call #24) — das ist der haltbare Identifikator, nicht der `assembly:`-String aus der Trefferliste.

**StructuredContent:** Beschreibung `FindSymbolBatchDto` / `callSites` — in der Agentenfläche nur Markdown. IDs stehen in Backticks; für Handoff reicht das auf Projekt. Assembly-Zeilen sind durch Cache-Pfade token-teuer.

**Hints:** Cap-10-Hint (Call #17) gut. Generation-Hint „aktuelle assembly:sha:gen:id aus dem Assembly-Ziel“ gut, aber bei project-Target und SHA-Fehler falsch. Missing-Singular nach Array-Call nennt nicht den tatsächlich gesendeten Parameternamen `symbolIdentifiers`.

## 8 Roslyn-Wünsche

- Eine kanonische ID-Form über Targets: DocCommentId (`T:`/`M:`/`P:`/`F:`) plus optional SHA; **Generation aus der ID entfernen**. Session/Cache intern versionieren, Lookup über SHA+DocId. Stale-IDs → `SYMBOL_NOT_FOUND` oder `STALE_ASSEMBLY_SNAPSHOT` mit der **aktuellen** ID in der Antwort, nicht Generation-Text auf `project`.
- Batch-Vertrag angleichen: entweder `symbolIdentifiers` auch an `find_references` / `get_feature_context` (sequentiell intern, max. 10) **oder** Extra-Keys explizit `INVALID_ARGUMENT: unbekanntes Feld / Batch unsupported`.
- `get_symbol_body`: Cap dokumentieren oder wie `namePatterns` auf 10 kappen; gemischte Fehler beibehalten.
- `namePatterns` + Alias gleichzeitig: Fehler oder Merge, kein stilles Verwerfen. Leere Array-Einträge Fehler, nicht Drop.
- Ranking für Batch-Suche: exakter Identifier / `INamedTypeSymbol` vor Membern, Tests und Fields; `kind=class` = `TypeKind` hart (keine `IFieldSymbol`).
- Cross-Target: `TARGET_MISMATCH` wenn `assembly:`-ID auf `project` oder umgekehrt; SHA-Mismatch nicht als Generation verkaufen.
- `get_feature_context`: Assembly-ID → `TOOL_TARGET_UNSUPPORTED`, nicht Generation.
- Truncation-Hint in `find_references` ohne das Wort `pattern`.
- Optional: in jeder Trefferzeile neben der langen `assembly:`-ID die nackte `T:`/`M:` zum Copy-Paste ausgeben.

## 9 Phase 3 (AiNetLinter-Quellzeiger)

Nur Nicht-ok-Kettenbefunde. Pfade unter `C:\Daten\Entwicklung\Ralf\AiNetLinter\src\AiNetLinter\`.

1. **Generation-wechselnde Assembly-IDs (`assembly:sha:gen:DocId`).**  
   Pfad: `Mcp\AnalysisSymbolIdentity.cs`; `Mcp\Assemblies\Analysis\AssemblyAnalysisRegistry.cs`; `Mcp\Assemblies\Analysis\AssemblyAnalysisSession.Generation.cs`; `Mcp\Tools\SymbolGraph\FindSymbolTool.cs`.  
   Symbol: `AnalysisSymbolIdentity.Format` / `Matches`; `AssemblyAnalysisRegistry.CreateEntry` (`nextGenerations + 1`); `AssemblyAnalysisSession.CreateAndInstallGenerationAsync` (`Interlocked.Increment(ref nextGeneration)`); `FindSymbolTool.FormatSymbolLocationEntries` (`assemblyIdentity?.Format(symbolId)`).  
   Ansatz: öffentliche ID = SHA + DocCommentId (`T:`/`M:`/`P:`/`F:`). Generation nur intern am Session-Cache. Lookup über Hash+DocId; stale → `STALE_ASSEMBLY_SNAPSHOT` plus **aktuelle** ID in der Antwort. `GetDocumentationCommentId()` bleibt der Roslyn-Anker.

2. **Cross-Target und SHA-Mismatch als „Generation“.**  
   Pfad: `Mcp\Tools\SymbolGraph\SymbolIdentifierResolver.cs`; `Mcp\AnalysisSymbolIdentity.cs`; `Mcp\AnalysisToolCall.cs`.  
   Symbol: `SymbolIdentifierResolver.TryNormalizeAssemblyId`; `StaleAssemblyId`; `AnalysisSymbolIdentity.Matches` (Hash **und** Generation); `ProjectAnalysisDispatcher.UnsupportedAssemblyTarget`.  
   Ansatz: vor `Matches` verzweigen: `assembly:`-Prefix am `targetType=project` → `TARGET_MISMATCH`; Hash ≠ Session-SHA → eigener Code; Generation ≠ Session → `STALE_ASSEMBLY_SNAPSHOT`. `get_feature_context` ist projektgebunden (`AddGetFeatureContext` → `ProjectAnalysisDispatcher`): Assembly-ID dort `TOOL_TARGET_UNSUPPORTED` (`LinterErrorCodes.AssemblyTargetUnsupported`), nicht Generation. Nacktes `T:` am Assembly-Ziel (bereits `expectedIdentity is not null && HasKnownDocumentationCommentIdPrefix`) dokumentieren und belassen.

3. **`symbolIdentifiers[]` an Singular-Tools = stilles Drop.**  
   Pfad: `Mcp\Registration\SymbolGraphToolRegistrations.cs`; `Mcp\Registration\AnalysisToolRegistrations.cs`; `Mcp\Tools\SymbolGraph\FindReferencesTool.cs`; `Mcp\Tools\FeatureContext\GetFeatureContextTool.cs`.  
   Symbol: `SymbolGraphToolRegistrations.AddFindReferences` (Lambda nur `symbolIdentifier`); `AnalysisToolRegistrations.AddGetFeatureContext` (ebenso); `FindReferencesTool.ExecuteAsync` / `GetFeatureContextTool.ExecuteAsync` (`EffectiveSymbolIdentifier` leer → Pflichtfeld).  
   Ansatz: Extra-Key ist kein eigener Deserializer — das MCP-SDK bindet nur deklarierte Lambda-Parameter. Entweder `string[]? symbolIdentifiers` deklarieren und bei gesetztem Array ohne Singular `INVALID_ARGUMENT: Batch unsupported` (gesendeten Namen nennen) **oder** intern sequentiell wie `GetSymbolBodyTool.RenderSymbolBodiesAsync` (max. 10, Roslyn `SymbolFinder` je ID).

4. **Cap-Asymmetrie `namePatterns` 10 vs. `symbolIdentifiers` unbegrenzt.**  
   Pfad: `Mcp\Tools\SymbolGraph\FindSymbolTool.cs`; `Mcp\Tools\GetSymbolBodyTool.cs`; `Mcp\McpToolResults.cs`.  
   Symbol: `FindSymbolTool.MaxPatternsPerCall` / `ValidateNamePatterns`; `GetSymbolBodyTool.NormalizeIdentifiers` (kein Cap).  
   Ansatz: dieselbe Konstante (10) in `NormalizeIdentifiers`; Schema/Beschreibung von `SymbolBodyToolRegistrations` anpassen. Gemischte Fehler je Eintrag beibehalten (`RenderSingleSymbolAsync`).

5. **`namePattern` vs. `namePatterns`: Array gewinnt, Alias stirbt still.**  
   Pfad: `Mcp\Tools\SymbolGraph\FindSymbolTool.cs`; `Mcp\Registration\SymbolGraphToolRegistrations.cs`.  
   Symbol: `FindSymbolTool.NormalizeNamePatterns` (`if (patterns.Count > 0) return patterns` — Skalare werden nicht gemerged).  
   Ansatz: wenn Array **und** Scalar gesetzt: `INVALID_ARGUMENT` oder Merge (Scalar anhängen, dann Cap). Nicht verwerfen ohne Hinweis.

6. **Leeres `namePatterns`-Element still übersprungen.**  
   Pfad: `Mcp\McpBatchArguments.cs`; `Mcp\Tools\SymbolGraph\FindSymbolTool.cs`.  
   Symbol: `McpBatchArguments.Normalize` (`IsNullOrWhiteSpace` → `continue`); `FindSymbolTool.ValidateNamePatterns` (nur leere Gesamtliste).  
   Ansatz: Whitespace-Eintrag als `INVALID_ARGUMENT` („leeres namePatterns-Element“), analog zu `[]`.

7. **Gültige ID, falsches Symbol (Ranking / Substring).**  
   Pfad: `Mcp\Tools\SymbolGraph\FindSymbolScanner.cs`; `Mcp\Tools\SymbolGraph\SymbolNameMatcher.cs`.  
   Symbol: `FindSymbolScanner.FindMatchesWithEntriesAsync` (`SymbolFinder.FindSourceDeclarationsAsync` + `FilterByKind`, keine Sortierung); `SymbolNameMatcher.MatchesSymbol` (Substring/`MatchesSimplePattern`).  
   Ansatz: nach dem Filter ranken: exakter `ISymbol.Name` / Regex-Volltreffer vor Substring; `INamedTypeSymbol` vor Membern; Produktions-`Project` vor Test-Projekten (Pfad/`IsTest`-Konvention). `maxResults` erst nach dem Ranking anwenden, damit `DataExecutor`+`kind=class` nicht in `ThrowingDataExecutor` endet.

8. **Gefälschte Totale („9 gesamt, 8 gezeigt“).**  
   Pfad: `Mcp\Tools\SymbolGraph\FindSymbolScanner.cs`; `Mcp\McpTruncation.cs`.  
   Symbol: `FindSymbolScanner.FindMatchesWithEntriesAsync` (`totalMatches = hasMore ? request.MaxResults + 1 : collectedEntries.Count`); `McpTruncation.TruncateLines`.  
   Ansatz: echte Restmenge zählen (alle `FilterByKind`-Symbole bzw. Location-Einträge), nicht `maxResults+1` als Gesamtzahl. Banner dann ehrlich oder „mindestens N+1“.

9. **`kind=class` lässt Assembly-Fields durch.**  
   Pfad: `Mcp\Tools\FileStructure\SymbolKindClassifier.cs`.  
   Symbol: `SymbolKindClassifier.MatchesSymbolKind` — `IMethodSymbol`/`IPropertySymbol`/`INamedTypeSymbol` behandelt, **sonst `return true`** (`IFieldSymbol` passiert `kind=class`).  
   Ansatz: Default `false`; `IFieldSymbol` nur bei `kind=field`. `kind=class` = `INamedTypeSymbol` + `MatchesTypeKind` (`TypeKind.Class`). Assembly-Pfad ist derselbe Filter (`FindSymbolTool.ExecuteAsync` via `AssemblyFindSymbolTool`).

10. **`find_references`-Truncation-Hint „Pattern verfeinern“ nach ID-Handoff.**  
    Pfad: `Mcp\Tools\SymbolGraph\TransitiveCallGraphFormatter.cs`; `Mcp\McpTruncation.cs`.  
    Symbol: `TransitiveCallGraphFormatter.CreateMaxResultsMessage` (depth=1: Literal „Pattern verfeinern“); `McpTruncation.TruncateLines` (dieselbe Formel).  
    Ansatz: ID-basierte Tools: „maxResults erhöhen“ / Offset, ohne das Wort `pattern`. `McpTruncation` parametrisieren oder eigenen Hint in `FindReferencesTool`.
