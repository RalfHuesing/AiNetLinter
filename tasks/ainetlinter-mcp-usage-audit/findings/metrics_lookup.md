# Finding: `metrics_lookup`

Audit-Stand: 2026-09-07. Nur Schema-Lookup (`GetDynamicTools`) plus Calls dieses Tools. Kein anderes MCP-Tool, kein Build, kein Test.

Anker Projekt: `DataExecutor` / `Sample.Project/Infrastructure/Data/DataExecutor.cs`. Anker Assembly: `Example.External.Business.dll` / `T:Example.External.Business.Record`.

## 1 Schema-Kurzfazit

Beschreibung steuert den Call **weitgehend richtig**: punktgenaue Metriken für ein oder mehrere C#-Symbole, Batch in einem Turn, `targetType`+absoluter `targetPath`, Identifikatoren als DocCommentId, `Datei.cs:Zeile`, `Datei.cs:Zeile:Spalte` oder qualifizierter Name. `symbol` / `symbolIdentifier` als Alias für genau ein Symbol ist in der Prosa klar.

JSON-Schema weicht ab: nur `targetType`/`targetPath` sind `required`; `symbolIdentifiers` / `symbolIdentifier` / `symbol` gelten als optional (`default: null`). Live ist ein nichtleerer Identifikator **Pflicht** (`INVALID_ARGUMENT`). `targetType` ohne Enum. Keine Limits (`maxResults`, `cursor`, `detailLevel`) — Truncation existiert als Mechanismus nicht, große Batches laufen ungekürzt durch.

Antwort ist Markdown (Tabelle Schwellwert-Abgleich + Struktur). Beschreibung nennt `MetricsLookupBatchDto` in `structuredContent`; in der Agent-Antwort nur der Markdown-Text plus Vollständigkeits-Hinweis.

## 2 Calls

Projektroot: `C:\Workspace\Sample.Project`. Assembly: `C:\ExternalAssemblies\Version-9\Example.External.Business.dll`. Alle Calls **schnell**, kein Timeout.

| # | Absicht | Argumente | Größe / Completeness | Ergebnis |
|---|---|---|---|---|
| 1 | Happy, Kurzname | `project`, `symbolIdentifier=DataExecutor` | ~2k Zeichen, Ambiguity-Liste | `AMBIGUOUS_SYMBOL`. 9 Treffer, darunter Properties **und zweimal** dieselbe Klasse (`DataExecutor.cs:9` + `DataExecutor.OptimisticConcurrency.cs:11`, gleiche Id `T:…DataExecutor`). Hint: FQ-Name oder Datei:Zeile:Spalte. |
| 2 | Leer, kein Symbol | `project`, nur Target | ~3 Zeilen | `INVALID_ARGUMENT`: Pflichtparameter `symbolIdentifiers` fehlt oder ist leer. Hint zeigt nur das Array, nicht die Aliase. |
| 3 | Assembly Kurzname | `assembly` + DLL, `Record` | groß (~8k), Header `completeness=partial` | `AMBIGUOUS_SYMBOL`. Klasse `T:…Record` plus viele `P:…Record`. IDs im Format `assembly:<sha>:6:T:…`. |
| 4 | Fehler, kein Projekt | `project`, `C:\Workspace\DoesNotExist` | ~12 Zeilen | `PROJECT_NOT_INITIALIZED` + JSON-Vorlage. |
| 5 | Ungültiges `targetType` | `bogus` | ~3 Zeilen | `INVALID_ARGUMENT`: nur `project` oder `assembly`. |
| 6 | Happy Path Typ | `T:Sample.Project.Infrastructure.Sql.DataExecutor` | ~1k, Hinweis „vollständig“ | NamedType. LOC **479** / 600 OK. Footprint **0** / 5000 OK. Public Members **18** / 15 **VIOLATION**. Ort `DataExecutor.cs:9-9`. Members 32 (18 public, 29 Methoden, 0 Properties). Keine Top-Abhängigkeiten. |
| 7 | Dateipfad `:Zeile` | `…/DataExecutor.cs:9` | wie 6 | Identisch zu Call 6. |
| 8 | FQ-Name | `Sample.Project.Infrastructure.Sql.DataExecutor` | wie 6 | Identisch zu Call 6. |
| 9 | Alias `symbol` | `symbol=T:…DataExecutor` | wie 6 | Alias funktioniert. |
| 10 | Happy Path Assembly | `assembly`, `T:Example.External.Business.Record` | ~1,8k + Header `partial` | LOC **23862** / **700** VIOLATION. Footprint **53399** / 5000 VIOLATION. Public **469** / 15. Members 1098. Top-Deps: `RecordPosition` 18485, `RecordStueckliste` 2466, `DataServiceRecordTransfer` 614. Id mit Generation. Hinweis trotzdem „vollständig“. |
| 11 | Assembly-prefixed Id | `assembly:<sha>:6:T:…Record` (Generation damals 6) | wie 10 | Erfolg, solange Generation aktuell. |
| 12 | Batch gemischt | `project`, 3 Ids: DataExecutor-Typ, Property, extern-`T:Record` | ~2k | Zwei Treffer + per-item `SYMBOL_NOT_FOUND` für Assembly-Typ im Projekt-Target. Batch bricht nicht ab. |
| 13 | Methode ohne Signatur | `M:…DataExecutor.QueryAsync` | ~1,5k | `AMBIGUOUS_SYMBOL` (zwei Overloads), vollständige DocCommentIds inkl. Generics/``1. |
| 14 | Partial-Datei | `…DataExecutor.OptimisticConcurrency.cs:11` | wie 6 | Dieselbe Typ-Metrik; Ort bleibt **primäre** Datei `:9-9`, Partial-Datei verschwindet. |
| 15 | `:Zeile:Spalte` | `…DataExecutor.cs:9:1` | wie 6 | Funktioniert. |
| 16 | Unbekanntes Symbol | `NoSuchType_xyz123DoesNotExist` | ~4 Zeilen | `SYMBOL_NOT_FOUND`, Hint auf `find_symbol`. |
| 17 | Leeres Array | `symbolIdentifiers=[]` | wie 2 | Gleicher `INVALID_ARGUMENT` wie fehlender Parameter. |
| 18 | Methode per Datei:Zeile | `…DataExecutor.cs:14` | ~900 | Methode `QueryAsync<T>(string,…)`. LOC 5, CC 1, Cognitive 0, effektive Parameter **2** (CancellationToken ignoriert). `MaxMethodLineCount` **150** (Compound, CC flach) — passt zu den Platform-Regeln. |
| 19 | Volle Methoden-Id | DocCommentId aus Call 13 | wie 18 | Identisch. Folge-Call-tauglich. |
| 20 | Bare `Datei.cs:Zeile` | `DataExecutor.cs:9` | wie 6 | Relativer Dateiname ohne Ordner reicht hier. |
| 21 | Fehlende DLL | `assembly`, nicht existierende `.dll` | ~3 Zeilen | `INVALID_ARGUMENT`: Pfad muss auf vorhandene Datei zeigen. |
| 22 | Relativer `targetPath` | `Sample.Project` | ~3 Zeilen | `INVALID_ARGUMENT`: muss absolut sein. |
| 23 | `assembly` + Projektordner | Ordner statt `.dll` | ~3 Zeilen | `INVALID_ARGUMENT`: keine vorhandene Datei (nicht „kein Assembly-Target“). |
| 24 | Truncation-Stress | 34× `DataExecutor.cs:<Zeile>` | **groß** (~12k+), kein Footer, „vollständig“ | Kein Cap. Mix: Typ, Methoden, Lambdas, Parameter, Locals, `SYMBOL_NOT_FOUND`, `AMBIGUOUS_SYMBOL`, ab Zeile 300 `INVALID_ARGUMENT` „gültiger Bereich 1 bis **298**“. Zeile **280** → Local `paramKeys` an **258**. |
| 25 | Alias-Konflikt | `symbol=T:…DataExecutor` **und** `symbolIdentifier=…cs:14` | wie 18 | Nur die Methode. `symbol` still ignoriert, keine Warnung. |
| 26 | `project` + DLL-Pfad | extern-`.dll` als `targetPath` | ~12 Zeilen | `PROJECT_NOT_INITIALIZED`, sucht `ainetlinter.project.json` **unter dem DLL-Pfad als Ordner**. |
| 27 | Assembly-Batch | `Record`, `RecordPosition`, `RecordStueckliste` | ~4,5k, Header `generation=7`, `partial` | Alle drei. Footprint jeweils **53399**. `RecordPosition` nennt Dep `Record` **28423** Zeilen, Call 10/27 `Record`-LOC ist **23862**. |
| 28 | Assembly `Record.cs:36` | decompilierte Datei:Zeile | wie 10 | Löst die Klasse auf. |
| 29 | Stale Assembly-Id | `assembly:…:6:T:…Record` nach Generation 7 | ~8 Zeilen + Header | `INVALID_ARGUMENT`: Id gehört nicht zur aktuellen Generation. Hint: aktuelle `assembly:<sha>:<generation>:<symbolId>`. |
| 30 | Leerer String | `symbolIdentifier=""` | wie 2 | Wie fehlender Parameter, nicht `SYMBOL_NOT_FOUND`. |
| 31 | Beide Param-Familien | `symbolIdentifier=T:…` **und** `symbolIdentifiers=[…cs:14]` | wie 18 | Array gewinnt, Einzel-Alias still verworfen. |
| 32 | Nested Type | `T:…DataExecutor.SqlTransactionContextAdapter` | ~800 | LOC 9. Footprint **298** (entspricht Dateilänge `DataExecutor.cs`, Call 24). Public 4/15 OK. Parent (Call 6) hatte Footprint **0**. |
| 33 | `null` im Array | `[T:…DataExecutor, null, …cs:14]` | wie 12 ohne Fehlerzeile | Null-Eintrag still übersprungen, zwei Treffer. |
| 34 | Assembly-Methode geraten | `M:…Record.Speichern` | ~8 Zeilen + Header | `SYMBOL_NOT_FOUND` (Name existiert so nicht). |
| 35 | Repro Datei:Zeile 280 | `DataExecutor.cs:280` allein | ~400 | Bestätigt Call 24: Local `paramKeys`, Ort **258-258**, LOC 1, keine Regel. |
| 36 | Assembly Datei:Zeile Methode | `Record.cs:100` | ~500 + Header `generation=8` | Field `Record.StatistikUpdate.Vaterartikelgruppe`, LOC 1 — nicht die umgebende Methode/Klasse. |

## 3 Verdict

**Teilweise wie beschrieben.** Mit präziser `T:`/`M:`-Id oder FQ-Name liefert das Tool schnell die versprochenen Metriken (LOC, CC/Cognitive/Parameter bei Methoden, Member-Zahlen, Schwellwert-Status). Batch, Aliase, Assembly-Target und Fehlercodes (`SYMBOL_NOT_FOUND`, `AMBIGUOUS_SYMBOL`, `PROJECT_NOT_INITIALIZED`, `INVALID_ARGUMENT`) funktionieren.

Abweichungen: Pflicht-Identifikator vs. optionales Schema; Footprint am Ankertyp **0** trotz Nested-Type und sichtbarer Contracts-Typen in Signaturen; Assembly-IDs mit Generation zerbrechen im nächsten Turn; identischer Footprint 53399 für drei Typen; `Datei.cs:Zeile` trifft Locals/Parameter/Lambdas/Fields statt der gemeinten Methode; kein Truncation-Limit; Header `completeness=partial` vs. Text „vollständig“; Quality-Regeln (MaxLineCount 700, Public Members 15) auf externes Dekompilat als VIOLATION-Lärm.

## 4 Schwere

**`degraded`**

Der Happy Path für ein bekanntes Produktions-Symbol ist brauchbar (Call 6, 18, 19). Agentenarbeit wird riskant durch falschen/fehlenden Footprint, instabile Assembly-IDs, Datei:Zeile-Falschtreffer und unkontrollierte Batch-Tokenlast. Nicht `broken` (Ziel „Metriken zu DataExecutor“ ist mit `T:` erreichbar). Nicht nur `friction` (Zahlen widersprechen sich, nicht nur Schema-Härte).

## 5 Nutzbarkeit

**nur mit Workaround**

- Kurzname `DataExecutor` / `Record` nie; immer `T:`/`M:` mit Signatur oder FQ-Name.
- Assembly: `T:Namespace.Typ` verwenden, **nicht** die zurückgegebene `assembly:<sha>:<gen>:T:…`-Id über Turn-Grenzen.
- `Datei.cs:Zeile` nur an der **Deklarationszeile** der gewünschten Methode/Klasse, sonst Locals/Parameter.
- Batches klein halten (keine Dutzend Datei:Zeile-Raster).
- Assembly-VIOLATIONs nicht als Platform-Qualität lesen (fremde DLL, andere MaxLineCount-Grenze).

Ohne Workaround (Kurzname, zurückgegebene Assembly-Id, Zeilenraster) **nein**.

## 6 Bugs+FP/FN

| Befund | Art | Call |
|---|---|---|
| `DataExecutor` Footprint **0**, Nested `SqlTransactionContextAdapter` Footprint **298** (= Dateilänge 298 aus Call 24). Parent enthält Nested Type; `QueryAsync`-Signatur zeigt `DataExecutionScope` (Contracts). | FN Footprint am Ankertyp | 6 vs. 32 vs. 13 |
| `Record` / `RecordPosition` / `RecordStueckliste` alle Footprint **53399**. Dep-LOC `Record` 28423 vs. Typ-LOC 23862. | inkonsistente / geteilte Footprint-Zahl | 10, 27 |
| `DataExecutor.cs:280` → Local `paramKeys` an Zeile **258** | FP Auflösung Datei:Zeile | 24, 35 |
| Typ-Ort `DataExecutor.cs:9-9` bei LOC 479; dieselbe Datei nur Zeilen **1–298** | Span/Partial: zweite Datei in Ambiguity, nicht im Ort | 1, 6, 14, 24 |
| `assembly:…:6:T:…` nach Generation 7/8 ungültig | instabile Folge-Id | 11 vs. 29; Header 36 schon Generation 8 |
| Header `completeness=partial` + Hinweis „Daten sind vollständig“ | widersprüchliche Completeness | 10, 27, 28 |
| Assembly `MaxLineCount` **700**, Projekt **600** | andere Regelquelle auf Dekompilat | 6 vs. 10 |
| externe Typen als massenhafte VIOLATION (469 public / 15) | irreführendes Quality-Signal, Metrik selbst plausibel groß | 10, 27 |
| Schema: Identifikator optional; Runtime Pflicht. Leerer String = „fehlt“, nicht `SYMBOL_NOT_FOUND` | Schema vs. Live | 2, 17, 30 |
| `symbolIdentifiers` schlägt `symbolIdentifier`/`symbol` ohne Warnung | stiller Alias-Vorrang | 25, 31 |
| Partial-Klasse zweimal in `AMBIGUOUS_SYMBOL` mit **gleicher** `T:`-Id | Duplikat-Treffer | 1 |
| Lambdas/Parameter/Locals als eigene Metrik-Blöcke ohne Regel | Rauschen, Agent hält sie für die Methode | 24 |
| `project`+DLL-Pfad sucht `ainetlinter.project.json` im DLL-Pfad | irreführender Fehlertext | 26 |

**Stichprobe vs. Tool-Ist (ohne Dateilesen, nur Tool-interne Konsistenz):**

- `QueryAsync` Overloads (Call 13) vs. Metrik Call 18/19: CC 1, 5 LOC, Compound-Limit 150 — stimmig zu flacher Methode.
- Datei `DataExecutor.cs` gültiger Zeilenbereich **298**, Typ-LOC **479**: Differenz erklärt sich als Partial (`OptimisticConcurrency.cs` in Call 1). Typ-LOC wahrscheinlich Summe; Anzeige-Ort FN.
- Member-Rechnung Call 6: 29 Methoden + 0 Properties = 29, 32 gesamt → 3 weitere Member (Felder/Nested/Ctor) — intern plausibel, Public-18-VIOLATION nicht gegen Quelltext gegenzählen (Auftrag: nur dieses Tool).

Kein Timeout. Leermenge ist echter Fehlercode, keine leere 200-Tabelle.

## 7 Token/IDs

Einzel-Call Typ/Methode: kompakt, agententauglich (~0,5–1,5k Zeichen).

Token-Risiko: Ambiguity-Listen (Call 3 sehr lang), Raster-Batch ohne Cap (Call 24). Kein `maxResults`/`cursor` — Truncation-Probe ergibt **Vollausgabe**, nicht gekürzte Seite.

Stabile Folge-IDs:

- Projekt: `T:…` und volle `M:…``1(…)` aus Ambiguity/Antwort — ja (Call 19).
- Assembly: zurückgegebene `assembly:<sha>:<generation>:T:…` — **nein** über Generation-Wechsel. `T:Namespace.Typ` ohne Prefix bleibt gültig (Call 10).
- `Datei.cs:Zeile` — instabil/falsch außer an der Deklarationszeile.
- Nested/Lambda-Ids mit `.~` / doppelten Signatur-Suffixen — kopierbar, aber leicht mit der Parent-Methode zu verwechseln.

StructuredContent laut Beschreibung Batch-DTO; sichtbar war nur Markdown. `[HINWEIS] vollständig` trotz Assembly-`partial`.

## 8 Roslyn-Wünsche

- Footprint für Typen deterministisch aus dem Kompilation-Graph (eigene + projektinterne Referenztypen, Nested Types einschließen); 0 nur wenn wirklich keine eigenen Typ-Kanten. Top-Abhängigkeiten auch im Projekt-Target.
- Assembly-Symbol-Ids ohne Generation in der **zurückgegebenen** `Id`, oder akzeptiere `T:`/`M:` immer und behalte `assembly:…:gen:` nur intern. Generation-Bump darf Folge-Calls nicht brechen.
- `Datei.cs:Zeile` ohne Spalte: innerstes **benanntes** Member (Methode/Typ), nicht Local/Parameter/Lambda; bei mehreren Treffern `AMBIGUOUS_SYMBOL` statt stiller Nachbar-Local.
- Partial-Typen: ein Match pro `T:`-Id; Ort mit allen Partial-Dateien; LOC-Summe beibehalten, Zeilenbereich nicht `:9-9`.
- Schema: Identifikator-Pflicht oder dokumentierte One-of-Gruppe; Enum `targetType`; `maxResults`+Truncation-Footer für Batches.
- Alias-Konflikt: `INVALID_ARGUMENT` oder mergen, nicht still verwerfen.
- Assembly-Sessions: Quality-Schwellwerte der **Consumer-rules.json** nicht auf Dekompilat anwenden, oder Metriken ohne VIOLATION-Status (nur Rohwerte). MaxLineCount-Quelle in der Antwort nennen.
- Completeness: Header und „vollständig“-Hinweis dieselbe Wahrheit.
- `project`+Datei-`.dll`: `INVALID_ARGUMENT` (Pfad ist Assembly, nicht Projektroot), nicht `PROJECT_NOT_INITIALIZED` mit JSON unter der DLL.

## 9 Phase 3

AiNetLinter-Root: `C:\Daten\Entwicklung\Ralf\AiNetLinter`. Je Nicht-ok-Befund: Pfad + Symbol + Roslyn-Ansatz.

### Footprint 0 am Ankertyp, Nested 298 (Call 6 vs. 32)

- **Pfad:** `src\AiNetLinter\Metrics\AIContextFootprintCalculator.cs`
- **Symbol:** `AIContextFootprintCalculator.CalculateDetailed`, `QueueMemberSymbols`, `SumLinesForSymbol`
- **Ansatz:** `QueueMemberSymbols` läuft nur über Field/Property/Method, nicht über nested `INamedTypeSymbol`. `SumLinesForSymbol` zählt ganze `SyntaxTree`-Dateien und teilt `visitedTrees` — die Zieldatei kann 0 werden, sobald ein anderer besuchter Typ denselben Tree schon gelegt hat; der Nested-Typ als eigenes Target zählt dieselbe Datei voll (298). Target-eigene Deklarationszeilen immer zählen (nicht über `visitedTrees` wegwerfen); nested Types aus `INamedTypeSymbol.GetMembers()` mit `QueueNamedSymbol` einreihen; Signaturtypen mit `DeclaringSyntaxReferences` (Solution-Projekte, z. B. Contracts) nicht still verwerfen.

### Geteilter Assembly-Footprint 53399 / Dep-LOC ≠ Typ-LOC (Call 10, 27)

- **Pfad:** `src\AiNetLinter\Metrics\AIContextFootprintCalculator.cs`
- **Symbol:** `CalculateDetailed` (deps-Schleife ohne `visitedTrees` vs. `totalLines` mit `visitedTrees`)
- **Ansatz:** Drei stark zyklisch verbundene Dekompilat-Typen laufen denselben Tree-Set ab → gleiche Summe. Dep-Zeilen nutzen `DeclaringSyntaxReferences.Select(r => r.SyntaxTree).Distinct().Sum(t => t.GetText().Lines.Count)` ohne Sharing, deshalb `Record` als Dep 28423 vs. als Target 23862. Eine Zählregel: entweder Span der Typdeklaration (`FileLinePositionSpan` über alle `DeclaringSyntaxReferences`) oder dieselbe `visitedTrees`-Semantik für Top-Deps; nicht Datei-LOC als Typ-Footprint.

### `Datei.cs:Zeile` trifft Local/Parameter/Lambda/Field (Call 24, 35, 36)

- **Pfad:** `src\AiNetLinter\Mcp\Tools\SymbolGraph\FindReferencesTool.cs`, `src\AiNetLinter\Mcp\Tools\SymbolGraph\SymbolIdentifierResolver.cs`
- **Symbol:** `FindReferencesTool.ResolveByLineAsync`, `SymbolIdentifierResolver.ResolveSymbolsOnLine`, `TryFindEnclosingMember` (derzeit tot: `symbols.Count == 0` kehrt vorher mit `SYMBOL_NOT_FOUND` zurück)
- **Ansatz:** Nach dem Zeilen-Scan zuerst Member-Deklarationen (`IMethodSymbol`/`INamedTypeSymbol`/`IPropertySymbol`). Sonst `TryFindEnclosingMember`: `AncestorsAndSelf` auf `MethodDeclarationSyntax` / `BaseTypeDeclarationSyntax`, `SemanticModel.GetDeclaredSymbol`. Locals/Parameter/Lambdas nicht als einziger Treffer zurückgeben; bei mehreren benannten Membern `AMBIGUOUS_SYMBOL`. `MetricsLookupScanner.ScanFallback` dann nur noch für echte Member.

### Typ-Ort `:9-9`, Partial-Datei verschwindet (Call 1, 6, 14)

- **Pfad:** `src\AiNetLinter\Mcp\Tools\MetricsLookup\MetricsLookupScanner.cs`
- **Symbol:** `ExtractLocation` (`symbol.Locations.FirstOrDefault` + Identifier-`GetLineSpan`)
- **Ansatz:** Alle `DeclaringSyntaxReferences` zu Relativpfad+Span sammeln. `StartLine`/`EndLine` = Min/Max über Partials (nicht nur das Namens-Token). Formatter: Ort-Liste statt einer Datei. LOC-Summe über `DeclaringSyntaxReferences` bleibt.

### Partial zweimal in `AMBIGUOUS_SYMBOL` mit gleicher `T:`-Id (Call 1)

- **Pfad:** `src\AiNetLinter\Mcp\Tools\SymbolGraph\FindSymbolTool.cs`, `FindReferencesTool.ResolveByNameAsync`
- **Symbol:** `FindSymbolTool.FormatSymbolLocationEntries` (eine Zeile je `Location.IsInSource`)
- **Ansatz:** Ambiguitätsliste nach `DocumentationCommentId.CreateDeclarationId` deduplizieren; Partial-Dateien als Attribute einer Id, nicht als zwei Kandidaten. `SymbolFinder.FindSourceDeclarationsAsync` liefert ein `ISymbol` mit mehreren Locations — nicht als zwei Typen behandeln.

### Instabile `assembly:<sha>:<generation>:T:…`-Id (Call 11 vs. 29)

- **Pfad:** `src\AiNetLinter\Mcp\AnalysisSymbolIdentity.cs`, `src\AiNetLinter\Mcp\Assemblies\Analysis\AssemblyAnalysisSession.Generation.cs`, `src\AiNetLinter\Mcp\Tools\SymbolGraph\SymbolIdentifierResolver.cs`
- **Symbol:** `AnalysisSymbolIdentity.Format`, `AssemblyAnalysisSession.CreateAndInstallGenerationAsync` (`Interlocked.Increment(ref nextGeneration)`), `TryNormalizeAssemblyId` / `StaleAssemblyId`
- **Ansatz:** In der Agent-Antwort nackte DocCommentId (`T:`/`M:`) ausgeben; `assembly:`+Generation nur intern. Lookup: SHA+DocId, Generation nicht in der kopierbaren Id. Stale → `STALE_ASSEMBLY_SNAPSHOT` plus aktueller nackter Id, nicht `INVALID_ARGUMENT` „gehört nicht zur Generation“.

### Header `completeness=partial` vs. Text „vollständig“ (Call 10, 27, 28)

- **Pfad:** `src\AiNetLinter\Mcp\Assemblies\Analysis\AssemblyAnalysisResponse.cs`, `src\AiNetLinter\Mcp\McpSufficiencyHints.cs`, `src\AiNetLinter\Mcp\Tools\MetricsLookup\MetricsLookupTool.cs`
- **Symbol:** `AssemblyAnalysisResponse.CreateEnriched` (`ToCompletenessLabel`), `McpSufficiencyHints.Append`, `RenderMetricsLookupsAsync`
- **Ansatz:** `Append` nicht nach Assembly-`Enrich` aufrufen, wenn `effectiveStatus` nicht `Complete` ist. Oder Sufficiency-Hinweis an `lease.Context.Status` koppeln: nur bei `complete` „kein Read/Grep“.

### Assembly-Schwellwerte Default 700 / Public 15 auf Dekompilat (Call 6 vs. 10, 27)

- **Pfad:** `src\AiNetLinter\Mcp\Assemblies\Analysis\Factories\AssemblyAnalysisEntryFactory.cs`, `src\AiNetLinter\Configuration\MetricsConfig.cs`, `src\AiNetLinter\Mcp\Tools\MetricsLookup\MetricsLookupScanner.cs`
- **Symbol:** `CreateReadOnlyStateProvider` (`new Config { Metrics = new MetricsConfig() }`), `MetricsConfig.MaxLineCount` Default 700, `MaxPublicMembersPerType` Default 15, `ScanType` / `CheckThreshold`
- **Ansatz:** Assembly-Session: Metriken ohne VIOLATION-Status (Rohwerte), oder eigene Assembly-Limits (`MaxLineCount=0` = kein Check). Consumer-`rules.json` nicht auf Fremd-Dekompilat anwenden. In der Tabelle Limit-Quelle nennen (`UsedDefaultConfig` aus `GetConfigSnapshot`).

### Schema: Identifikator optional, Runtime Pflicht; leerer String = fehlt (Call 2, 17, 30)

- **Pfad:** `src\AiNetLinter\Mcp\Registration\AnalysisToolRegistrations.cs`, `src\AiNetLinter\Mcp\Tools\MetricsLookup\MetricsLookupTool.cs`, `src\AiNetLinter\Mcp\McpToolResults.cs`
- **Symbol:** `AddMetricsLookup` (`string[]? symbolIdentifiers = null`), `ExecuteAsync` + `McpBatchArguments.Normalize`, `SymbolIdentifiersBatchHint`
- **Ansatz:** JSON-Schema One-of (`symbolIdentifiers` | `symbolIdentifier` | `symbol`) als required-Gruppe, soweit das MCP-SDK das hergibt; sonst Beschreibung „Pflicht: mindestens ein nichtleerer Identifikator“. Hint um Aliase erweitern. Leerer String weiter `INVALID_ARGUMENT`, nicht `SYMBOL_NOT_FOUND`.

### Stiller Alias-Vorrang Array > `symbolIdentifier` > `symbol` (Call 25, 31)

- **Pfad:** `src\AiNetLinter\Mcp\Registration\AnalysisToolRegistrations.cs`
- **Symbol:** `ResolveMetricsLookupIdentifiers`
- **Ansatz:** Wenn mehr als eine Familie gesetzt ist: `INVALID_ARGUMENT` oder mergen (Union, Dedup). Nicht still verwerfen.

### `null` im Array still übersprungen (Call 33)

- **Pfad:** `src\AiNetLinter\Mcp\McpBatchArguments.cs`
- **Symbol:** `McpBatchArguments.Normalize` (`IsNullOrWhiteSpace` → `continue`)
- **Ansatz:** `null`/leer im Batch als per-item `INVALID_ARGUMENT` in der Markdown-Liste (wie `SYMBOL_NOT_FOUND` bei Mixed-Batch), nicht droppen.

### Kein Batch-Cap / Truncation (Call 24)

- **Pfad:** `src\AiNetLinter\Mcp\Tools\MetricsLookup\MetricsLookupTool.cs`
- **Symbol:** `RenderMetricsLookupsAsync` (ungekürzte `for`-Schleife)
- **Ansatz:** `maxResults` (Default z. B. 20) + `McpTruncation.TruncateLines` bzw. nach N Identifiern abbrechen mit Footer. Schema-Bound.

### `project` + DLL-Pfad → `PROJECT_NOT_INITIALIZED` (Call 26)

- **Pfad:** `src\AiNetLinter\Mcp\AnalysisTargetResolver.cs`, `src\AiNetLinter\Mcp\Projects\ProjectDefinitionLoader.cs`
- **Symbol:** `AnalysisTargetResolver.Resolve` (prüft Datei-Existenz nur bei `assembly`), `ProjectDefinitionLoader.Load` (`Path.Combine(projectRoot, "ainetlinter.project.json")`)
- **Ansatz:** Bei `targetType=project` und existierender Datei (`.dll`/`.exe`): `INVALID_ARGUMENT` „Pfad ist Assembly, nicht Projektroot“. Nicht `Lease` auf den DLL-Pfad als Ordner.

### StructuredContent `MetricsLookupBatchDto` unsichtbar

- **Pfad:** `src\AiNetLinter\Mcp\Tools\MetricsLookup\MetricsLookupTool.cs`
- **Symbol:** `McpToolResults.Text(final, new MetricsLookupBatchDto(…))`
- **Ansatz:** Agent-Text bleibt Quelle; IDs stehen schon im Markdown. Kein LLM. Optional Completeness/Truncation nur im Text konsistent halten (siehe Header-Befund).
