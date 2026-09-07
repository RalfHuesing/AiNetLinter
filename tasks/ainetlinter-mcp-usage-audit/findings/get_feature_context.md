# Finding: `get_feature_context`

Audit-Stand: 2026-09-07. Nur Schema-Lookup (`GetDynamicTools`) plus Calls dieses Tools. Kein anderes MCP-Tool, kein Build, kein Test.

Anker: `DataExecutor` und `[ComponentRegistration]` / `ComponentRegistrationAttribute`. Projektroot: `C:\Workspace\Sample.Project`.

## 1 Schema-Kurzfazit

Composite-One-Shot vor Edits/Refactorings: fünf Dimensionen (Deklaration, Metriken/Budget, statische Referenzen/Call-Sites, Test-Zuordnung, Linter-Violations) in einem residenten Aufruf. Beschreibung steuert den Call **weitgehend korrekt**: `symbolIdentifier` primär; Aliase `symbol`, `identifier`, `name`; `Datei.cs:Zeile` oder DocCommentId; Include-Flags Default `true`; `maxCallers` Default 10 / Cap 50; `maxTests` Dateilimit Default 10 / Cap 50. Caller-Bereich ist ausdrücklich **keine** Laufzeit-Coverage.

Zielvertrag: `targetType='project'`, `targetPath` absolut kanonisch. **Assembly-Ziele ausdrücklich unsupported.**

Input-Schema:

| Parameter | Typ | Pflicht | Default | Bemerkung |
|---|---|---|---|---|
| `targetType` | string | ja | — | Kein Enum; Beschreibung: nur `project`. |
| `targetPath` | string | ja | — | Absoluter Projektroot. |
| `symbolIdentifier` | string \| null | **nein im JSON**, **ja zur Laufzeit** | null | Primär. |
| `symbol` | string \| null | nein | null | Alias, in Fehlertexten gleichwertig genannt. |
| `identifier` | string \| null | nein | null | Alias, in der Kurzbeschreibung nicht genannt, funktioniert. |
| `name` | string \| null | nein | null | Alias, in der Kurzbeschreibung nicht genannt, funktioniert. |
| `includeCallers` | boolean | nein | true | |
| `includeTests` | boolean | nein | true | |
| `includeMetrics` | boolean | nein | true | |
| `includeViolations` | boolean | nein | true | |
| `maxCallers` | integer | nein | 10 | Cap 50 (live bestätigt). |
| `maxTests` | integer | nein | 10 | Dateilimit, Cap 50 (live: Dateien, nicht Methoden). |

**Irreführung:** JSON-`required` enthält nur `targetType`/`targetPath`, Laufzeit verlangt `symbolIdentifier` (oder Alias). Beschreibung nennt Assembly unsupported — Laufzeit prüft stattdessen einen Assembly-Dateipfad (`INVALID_ARGUMENT` „muss auf vorhandene Datei zeigen“), nicht `TOOL_TARGET_UNSUPPORTED`. Kurzname ohne Qualifikation ist in der Beschreibung erlaubt (`Namespace.Klasse.Methode`), trifft bei `DataExecutor` aber `AMBIGUOUS_SYMBOL`.

## 2 Calls

Alle Calls schnell (kein Timeout). Grobe Antwortgröße = sichtbarer Markdown-Text.

| # | Absicht | Argumente | Ergebnis |
|---|---|---|---|
| 1 | Happy Path Kurzname | `symbolIdentifier=DataExecutor` | `[ERROR]: AMBIGUOUS_SYMBOL`. 9 Treffer (Properties + Klasse, Partial doppelt). IDs in Backticks (`T:…`, `P:…`). Hint: FQN oder `Datei:Zeile:Spalte`. |
| 2 | Anker `[ComponentRegistration]` | `symbolIdentifier=ComponentRegistration` | `[ERROR]: SYMBOL_NOT_FOUND`. Hint: `find_symbol`. Kein Vorschlag `ComponentRegistrationAttribute`. |
| 3 | Alias `symbol` | `symbol=DataExecutor` | Identisch zu Call 1. Alias wirkt. |
| 4 | Leer/unbekannt | `symbolIdentifier=ThisSymbolDoesNotExistAnywhere12345` | `SYMBOL_NOT_FOUND` + `find_symbol`-Hint. |
| 5 | Pflicht fehlt | nur `targetType`/`targetPath` | `INVALID_ARGUMENT`: `'symbolIdentifier' (oder 'symbol') fehlt oder ist leer.` Beispiele in Hint. **`identifier`/`name` im Fehler nicht genannt.** |
| 6 | Happy Path DocCommentId | `symbolIdentifier=T:Sample.Project.Infrastructure.Sql.DataExecutor` | Erfolg. NamedType, Datei `DataExecutor.cs:9-297` (289 Zeilen), Type LOC 479, Public Members 18/15 **VIOLATION**, Footprint 0/5000. **248** Call-Sites, gezeigt 10/248 (`maxCallers`). Tests: 3 Dateien, 10 Tests (Naming Convention). Violations-Sektion: **0, Status complete**. Kein Completeness-Hinweis „kein Read/Grep“. ~70 Zeilen. |
| 7 | Datei:Zeile mit Prefix | `Sample.Project/Infrastructure/Data/DataExecutor.cs:9` | Identisch zu Call 6. |
| 8 | Anker Attribut-Typ | `symbolIdentifier=ComponentRegistrationAttribute` | Erfolg. `T:Sample.Project.Contracts.ComponentRegistrationAttribute`. 23 Call-Sites, 10/23 gezeigt. **0 Tests**. Violations 0 complete. |
| 9 | Alias `identifier` | `identifier=T:…DataExecutor` | Identisch zu Call 6. |
| 10 | Alias `name` | `name=T:…DataExecutor` | Identisch zu Call 6. |
| 11 | Includes aus | alle `include*=false` | Nur Sektion 1 (Deklaration + DocCommentId). Plus `[HINWEIS]: … vollstaendig … kein zusaetzliches Read/Grep noetig.` ~8 Zeilen. |
| 12 | Truncation hart | `maxCallers=1`, `maxTests=1` | Caller: 1 von 248. Tests: **1 von 3 Testdateien und 6 von 10 Testmethoden** — `maxTests` ist Dateilimit; Methoden der gewählten Datei bleiben vollständig. |
| 13 | Truncation Cap | `maxCallers=50`, `maxTests=50` | Caller: **50 von 248**. Alle 50 Pfade unter `*.Tests.*`. Kein Produktionspfad `Sample.Project/`. Tests unverändert 3 Dateien (unter Cap). Große Antwort (~120 Zeilen). |
| 14 | Leerer String | `symbolIdentifier=""` | Wie Call 5, `INVALID_ARGUMENT`. |
| 15 | Attribut-Syntax | `symbolIdentifier=[ComponentRegistration]` | `SYMBOL_NOT_FOUND`. Kein Mapping auf `ComponentRegistrationAttribute`. |
| 16 | Assembly, Projektroot | `targetType=assembly`, `targetPath=<Projektroot>` | `INVALID_ARGUMENT`: Assembly-Pfad muss existierende `.dll`/`.exe` sein. **Kein** „Tool unsupported“. |
| 17 | FQN ohne `T:` | `Sample.Project.Infrastructure.Sql.DataExecutor` | Identisch zu Call 6. FQN reicht. |
| 18 | Cap-Überzug | `maxCallers=100` | Wie Call 13: **50 von 248**. Cap 50 greift still (kein Warn-Text „cap applied“, nur „Begrenzung: maxCallers“). |
| 19 | Methode Kurzform | `DataExecutor.GetNextExternalSystemTransactionAsync` | `AMBIGUOUS_SYMBOL` (Fake-Test-Executor + 2 Overloads). Volle `M:…`-IDs in Backticks. |
| 20 | Property-ID aus Call 1 | `P:…FormLoadQueryRequest.DataExecutor` | Erfolg, Property. 1 Call-Site (Produktion), 2 Tests (Direct Member Match). Completeness-Hinweis. Klein. |
| 21 | Assembly, fehlende Platform-DLL | `targetType=assembly`, `targetPath=…\bin\Debug\net10.0\Sample.Project.dll` | Wie Call 16: Datei existiert nicht. Kein Tool-Unsupported. externe Assembly nicht aufgerufen (Workspace-Scope; Beschreibung: Assembly unsupported). |
| 22 | Methode DocCommentId | volle `M:…GetNextExternalSystemTransactionAsync(DataExecutionScope,…)` | Erfolg. Method, Datei `DataExecutor.OptimisticConcurrency.cs:26-72`. 22 Call-Sites, 10/22 — **Produktion zuerst** (Buchungsserver, Domain.*). Tests: 5 Dateien / 14 Tests, darunter Naming-Convention-Match auf `ExecuteMutationWithVersionCheck_*` (nicht diese Methode). Method-LOC-Limit **150** (Compound-Suppression sichtbar). |
| 23 | Attribut vollständig | `ComponentRegistrationAttribute`, `maxCallers=50` | Alle **23/23** Call-Sites, kein „Zeige N von M“. Completeness-Hinweis. Mix Contracts/Domain/Tests/Platform-Handler. Tests weiter 0. |
| 24 | Datei:Zeile:Spalte nackt | `DataExecutor.cs:9:11` | Identisch zu Call 6. Hint aus AMBIGUOUS funktioniert. |
| 25 | Nur Caller | `includeCallers=true`, übrige Includes false, `maxCallers=3` | Sektion 1 + 3 (Nummernsprung). 3/248. Kein Completeness-Hinweis. |

**Truncation / Token-Stress:** Default `maxCallers=10` hält die Antwort handhabbar (~70 Zeilen für `DataExecutor`). Cap 50 bläht stark auf, bleibt aber endlich. Ohne Include-Flags wäre 248 Call-Sites plus Tests eine Token-Flut; das Limit ist das eigentliche Schutzgitter. Completeness: bei vollem Scope ohne Truncation erscheint der HINWEIS „kein Read/Grep“; bei Truncation nur „Zeige N von M — Begrenzung: maxCallers/maxTests“. Kein `truncated: true` / StructuredContent sichtbar (nur Markdown).

## 3 Verdict

**Teilweise wie beschrieben — als One-Shot nützlich, sobald der Identifikator eindeutig ist; unter Default-Truncation für heiße Typen riskant.**

Stimmt: fünf Dimensionen, Aliase, Datei:Zeile(:Spalte), DocCommentId, Include-Flags, Caps, Fehlercodes `AMBIGUOUS_SYMBOL` / `SYMBOL_NOT_FOUND` / `INVALID_ARGUMENT`, statische (nicht runtime) Caller.

Weicht ab / schwächt den Agenten:

- Kurzname `DataExecutor` / `[ComponentRegistration]` sind **keine** brauchbaren Einstiege ohne Präzisierung.
- Default- und sogar Cap-50-Caller für den Typ `DataExecutor` zeigen **nur Testhosts**, obwohl Produktion existiert (bei der konkreten Methode sichtbar).
- Metriken melden Public-Members-**VIOLATION**, Violations-Sektion gleichzeitig **0 / complete**.
- Assembly-Vertrag in der Beschreibung ≠ Laufzeit.
- Schema-`required` ≠ Laufzeit-Pflicht für das Symbol.

## 4 Schwere

Gesamt: **`degraded`**.

| Befund | Schwere |
|---|---|
| Caller-Sortierung + Truncation verdeckt Produktionsnutzer von `DataExecutor` (Default 10 und Cap 50) | `degraded` |
| Metriken „Public Members VIOLATION“ vs. Violations „0, complete“ | `degraded` |
| Test-Zuordnung: FN bei `ComponentRegistrationAttribute` (0 Tests trotz vieler Test-Call-Sites); FP Naming-Match (`ExecuteMutation*` an `GetNextExternalSystemTransactionAsync`) | `degraded` |
| Kurzname / `[ComponentRegistration]` → Ambiguous/NotFound ohne Attribut-Hinweis | `friction` |
| Schema: Symbol nicht required; Aliase `identifier`/`name` in Fehlertext von Call 5 fehlend | `friction` |
| Assembly: „unsupported“ vs. DLL-Pfadvalidator | `friction` |
| Caller-Zeilen ohne DocCommentId; Display `Type()` / `Type.()` | `friction` |
| AI-Context-Footprint 0 bei `DataExecutor` (Partial, 479 LOC) vs. 14 bei kleinem Attribut | `friction` |
| Production-first Caller-Sortierung; IDs an Call-Sites; Violations an Metriken koppeln | `wish` |

Kein `broken`: mit DocCommentId/FQN kommt der Agent zum Ziel. Nicht `ok`, weil der dokumentierte Hauptzweck (Kontext vor Edit) unter Default-Limits für Kerninfrastruktur-Typen irreführend ist.

## 5 Nutzbarkeit

**Nur mit Workaround: ja.**

Wieder aufrufen als MCP-first-Einstieg, aber:

1. Identifikator **nie** als Kurzname bei häufigen Membernamen (`DataExecutor`); sofort `T:`/`M:`/`P:` oder `Datei.cs:Zeile` aus einem vorherigen Ambiguous-Block.
2. `[ComponentRegistration]` → `ComponentRegistrationAttribute` raten oder (Folgetool, hier nicht aufgerufen) `find_symbol`.
3. `maxCallers` auf Cap 50 setzen **und** Truncation-Zeile ernst nehmen: 50/248 nur Tests heißt nicht „keine Produktion“.
4. Für Token: `include*=false` und gezielt eine Dimension einschalten.
5. Violations-Sektion nicht als alleinige Lint-Wahrheit nehmen, wenn Metriken `VIOLATION` zeigen.
6. Test-Sektion ist Naming/Invocation-Heuristik, keine Coverage.

Ohne diese Workarounds würde ein Agent `DataExecutor` als „vor allem Testhost-Abhängigkeit, 0 Lints“ missverstehen.

## 6 Bugs+FP/FN

Stichprobe gegen bekannte Platform-Fakten (kein `rg`/Folgetool; Evidenz aus denselben Calls).

| Befund | Art | Call | Bewertung |
|---|---|---|---|
| Kurzname `DataExecutor` trifft Properties vor/neben der Klasse; Partial-Klasse doppelt (`DataExecutor.cs` und `OptimisticConcurrency.cs`) in der Ambiguous-Liste | erwartet / Reibung | 1, 3 | IDs sind korrekt und copy-paste-fähig. Agent ohne Folgeschritt bleibt stehen. |
| `ComponentRegistration` und `[ComponentRegistration]` finden den Attributtyp nicht | FN der Namensauflösung | 2, 15 vs. 8 | C#-Konvention `Foo` ↔ `FooAttribute` wird nicht angewendet. Hint zeigt nur `find_symbol`. |
| Typ-Caller `DataExecutor`: 10/248 und 50/248 ausschließlich `*.Tests.*` | FN durch Sortierung+Truncation | 6, 13, 18, 25 | Methode `GetNextExternalSystemTransactionAsync` (Call 22) zeigt Produktion unter `Buchungsserver`/`Domain.*`. Pfad-Sortierung: `Sample.Project.` (Tests) vor `Sample.Project/` (Produktion). |
| Public Members 18/15 VIOLATION in Metriken, Violations-Sektion 0 complete auf derselben Datei | Widerspruch / FP der Completeness | 6 | Agent hält Lint für sauber. Status `complete` verstärkt das. Violations offenbar datei- nicht typbezogen; Partial-Zweitdatei in Call 22 ebenfalls 0. |
| `ComponentRegistrationAttribute`: 0 Tests, aber Call-Sites in `*Tests.cs` (Call 8/23) | FN Test-Dimension | 8, 23 | Caller-Dimension sieht Tests; Test-Zuordnung nicht. Naming-Convention matcht den Attributtyp nicht. |
| `GetNextExternalSystemTransactionAsync`: 6 `ExecuteMutationWithVersionCheck_*` per „Naming Convention Match“ | FP Test-Zuordnung | 22 | Dateiname `DataExecutorOptimisticConcurrencyIntegrationTests` matcht den Container, nicht die Methode. |
| Display `AdminAiExplorationPlatformSupport()` / `FirmenkalenderPersistenceStore.()` / `AiWorkspaceBunitSupport.()` | FP der Call-Site-Beschriftung | 6, 13, 22 | Wirkt wie Konstruktoraufruf; oft DI-Property/Parameter. Extra-Punkt `Type.()`. |
| AI-Context-Footprint 0 für `DataExecutor` | verdächtig / möglicher FN der Metrik | 6 | Kleines Attribut hat 14. Ohne Folgetool nicht gegen rules.json verifiziert; für Agenten wirkt der Typ „entkoppelt“. |
| `targetType=assembly` nicht als Tool-Unsupported | Vertrag | 16, 21 | Wie bei anderen Tools: DLL-Validator. Existierende externe Assembly hier nicht geprüft. |
| Schema required ohne Symbol | Vertrag | 5, 14 | Laufzeit fängt es sauber als `INVALID_ARGUMENT` — besser als generischer Invoke-Fehler. |
| Include-Flags lassen Sektionsnummern springen (1, dann 3) | Kosmetik | 11, 25 | Kein Funktionsbug. |

Kein beobachteter Absturz, keine leere Erfolgsantwort bei gültiger ID.

## 7 Token/IDs

- **Tokens:** Default-One-Shot für `DataExecutor` mittel (~70 Zeilen). Call 11 (nur Deklaration) sehr sparsam. Call 13/18 (50 Caller) unnötig testlastig. Caps verhindern unbegrenzte Flut; 248 ungekürzt wäre schlecht.
- **IDs am Subjekt:** DocCommentId stets in Backticks — direkt weiterreichbar an `get_symbol_body` / `find_references` / `get_call_tree` / `get_impact` (nicht aufgerufen).
- **IDs an Call-Sites:** **fehlen.** Nur `pfad:zeile` plus Displayname. Folgetool muss Datei:Zeile oder Displaynamen neu auflösen.
- **IDs in Fehlern:** Ambiguous-Liste ist das beste Handoff-Format der Session (`id: \`T:…\`` / `M:…`). `SYMBOL_NOT_FOUND` nennt explizit `find_symbol`. Success-Pfad nennt **kein** Folgetool.
- **Tests:** Methodennamen ohne DocCommentId; Dateipfad vorhanden. `get_test_context` wäre rätbar, nicht verdrahtet.
- **StructuredContent:** in der Agent-Sicht nicht erkennbar; nur Markdown. Truncation nur als Prosa „Zeige N von M“.
- **Stabilität:** Call 6, 7, 9, 10, 17, 24 textgleich für denselben Typ.

## 8 Roslyn-Wünsche

Alles statisch/Roslyn, kein RAG:

1. **Caller-Ranking:** Produktion vor Tests (Projektordner / Compilation), oder `scopeType`-analog (`production`/`tests`/`all`). Sonst bleibt Truncation ein Produktions-Blindflug.
2. **DocCommentId je Call-Site und je Testmethode** in Backticks — Folgetool ohne Re-Resolve.
3. **Attribut-Alias:** `ComponentRegistration` / `[ComponentRegistration]` → `ComponentRegistrationAttribute` (Roslyn `INamedTypeSymbol` + `Attribute`-Suffix).
4. **Ambiguous:** Klasse `T:` vor Properties `P:` priorisieren oder gruppieren, Partial nicht doppelt listen.
5. **Violations-Sektion** an Typ-Metriken koppeln (Partial-Dateien union) oder Completeness nicht `complete` setzen, wenn Sektion 2 `VIOLATION` zeigt.
6. **Test-Zuordnung:** Invocation/Direct Member strikt vor Datei-Naming; Naming nicht von der Containertyp-Datei auf unbeteiligte Methoden erben.
7. **Assembly:** `TOOL_TARGET_UNSUPPORTED` statt DLL-Existenzprüfung, Beschreibung = Verhalten.
8. **Schema:** `symbolIdentifier` required (oder anyOf mit Aliasen); Fehlertext alle Aliase; Cap-Überschreitung (`maxCallers=100`) explizit „capped to 50“.
9. **Caller-Display:** Member/Kind (`ctor`/`property`/`attribute`) statt immer `Type()`.
10. Optional: `continuationToken` / Offset für die restlichen 198/248 statt Cap-50-Wall.

## 9 Phase 3 (AiNetLinter-Quellzeiger)

Nur Nicht-`ok`-Befunde. Kein Patch in diesem Task.

### Caller-Sortierung + Truncation verdeckt Produktion (`degraded`)

- **Pfad:** `src\AiNetLinter\Mcp\Tools\FeatureContext\FeatureContextScanner.cs`; Sortierschlüssel aus `src\AiNetLinter\Core\DiffImpactAnalyzer.cs`.
- **Symbol:** `FeatureContextScanner.CollectCallersAsync`; `DiffImpactAnalyzer.FindCallSiteEntriesAsync`.
- **Ansatz:** `SymbolFinder.FindReferencesAsync` belassen. Vor `Take(maxCallers)` nicht nach `FilePath` lexikographisch sortieren (`Platform.Tests.*` vor `Platform/` wegen `.` < `/`). Stattdessen `TestDetector.IsTestFile`/`IsTestProject` als Primärschlüssel (Produktion zuerst), dann Pfad. Truncation-DTO um `productionRemaining` ergänzen; Formatter schreibt das in die „Zeige N von M“-Zeile. Gegenprobe: `T:…DataExecutor` Default `maxCallers=10` zeigt mindestens eine Produktions-Call-Site **oder** weist Restproduktion aus.

### Metriken-VIOLATION vs. Violations „0 complete“ (`degraded`)

- **Pfad:** `FeatureContextScanner.cs` (`CollectViolationsAsync`/`FilterViolationsForFile`); `FeatureContextFormatter.AppendViolationsSection`; Zählung `src\AiNetLinter\Mcp\Tools\MetricsLookup\MetricsLookupScanner.cs` vs. `src\AiNetLinter\Core\Checkers\PublicMembersChecker.cs`.
- **Symbol:** `MetricsLookupScanner.ScanType` (`INamedTypeSymbol.GetMembers`); `PublicMembersChecker.CountPublicMembers` (`TypeDeclarationSyntax.Members` einer Partial-Datei); `ViolationsReportDto.Status`.
- **Ansatz:** Violations-Filter auf **alle** `symbol.DeclaringSyntaxReferences`-Pfade unionen, nicht nur `ExtractLocation`-Erstdatei. Metrik zählt den Typ über Partials; der Checker zählt pro Syntaxknoten — dieselbe Einheit wählen oder Completeness nicht `complete` setzen, wenn `ThresholdCheckDto.Status` bereits `VIOLATION` ist. `FeatureContextFormatter.FormatReport` darf `McpSufficiencyHints.Append` dann nicht anhängen.

### Test-Zuordnung FN Attribut / FP Naming-Erbe (`degraded`)

- **Pfad:** `src\AiNetLinter\Core\TestCoverageScanner.cs`, `TestCoverageBatchScan.cs`; Aufruf `FeatureContextScanner.CollectTestsAsync`.
- **Symbol:** `TestCoverageScanner.SelectMatchingMethodsAndReason`; `FindTestMethods`/`CallsOrUsesTargetType`; `TestCoverageBatchScan.NormalizeTargets`.
- **Ansatz:** Bei `targetMemberName != null` Naming-Convention der Testdatei **nicht** auf alle Methoden erben (`classNameMatches` → `allNames`). Attributtypen (`INamedTypeSymbol` mit Basistyp `System.Attribute`): zusätzlich `AttributeSyntax` per `SemanticModel.GetSymbolInfo` matchen, Kurzname ohne `Attribute`-Suffix. Gegenprobe: `ComponentRegistrationAttribute` sieht Test-Call-Sites; `M:…GetNextExternalSystemTransactionAsync` ohne `ExecuteMutation*`.

### Kurzname / `[ComponentRegistration]` ohne Attribut-Hinweis (`friction`)

- **Pfad:** `src\AiNetLinter\Mcp\Tools\SymbolGraph\FindReferencesTool.cs`; `SymbolIdentifierResolver.cs`; Format `FindSymbolTool.FormatSymbolLocations`.
- **Symbol:** `FindReferencesTool.ResolveByNameAsync`; `SymbolFinder.FindSourceDeclarationsAsync` (exakter `lastSegment`).
- **Ansatz:** Identifier `[ComponentRegistration]` klammern strippen. Bei 0 Treffern und `INamedTypeSymbol`-Suche: C#-Konvention `Name` → `NameAttribute` nachziehen (zweiter `FindSourceDeclarationsAsync` oder Kandidat `lastSegment+"Attribute"`). Ambiguous: nach `DocumentationCommentId.CreateDeclarationId` deduplizieren (Partial nicht zweimal); `NamedType` vor `Property`/`Field`. Gegenprobe: `ComponentRegistration` → Attributtyp oder Ambiguous inkl. `ComponentRegistrationAttribute`.

### Schema-Required vs. Alias-Fehlertext (`friction`)

- **Pfad:** `src\AiNetLinter\Mcp\Registration\AnalysisToolRegistrations.cs`; `src\AiNetLinter\Mcp\Tools\FeatureContext\GetFeatureContextTool.cs`.
- **Symbol:** `AnalysisToolRegistrations.AddGetFeatureContext`; `GetFeatureContextTool.ExecuteAsync`.
- **Ansatz:** JSON-`required` folgt dem SDK aus nicht-nullbaren Parametern — `symbolIdentifier` als Pflicht (oder `anyOf` der Aliase) im Create-Delegate. Fehlertext alle vier Aliase nennen (`symbolIdentifier`/`symbol`/`identifier`/`name`), analog zur Bindung `symbolIdentifier ?? symbol ?? identifier ?? name`. Cap-Überschreitung (`maxCallers=100`) in `CollectCallersAsync` nach `Math.Clamp` explizit als `capped to 50` ausweisen.

### Assembly-Beschreibung vs. DLL-Pfadvalidator (`friction`)

- **Pfad:** `src\AiNetLinter\Mcp\AnalysisTargetResolver.cs`; `src\AiNetLinter\Mcp\AnalysisToolCall.cs`; Envelope `src\AiNetLinter\Mcp\Assemblies\Analysis\AssemblyAnalysisResponse.cs`.
- **Symbol:** `AnalysisTargetResolver.Resolve` (Existenz/`.dll`/`.exe` zuerst); `ProjectAnalysisDispatcher.ExecuteProjectAsync` → `UnsupportedAssemblyTarget`.
- **Ansatz:** Bei project-only-Tools (`McpToolRegistrationOptions.ReadOnlyTool`) Capability-Gate **vor** Dateiexistenz: `targetType=assembly` → `ASSEMBLY_TARGET_UNSUPPORTED` / `AssemblyAnalysisResponse.Unsupported`, ohne „Pfad muss auf vorhandene Datei zeigen“. Beschreibung und Fehler deckungsgleich.

### Call-Site ohne DocCommentId / Display `Type()` (`friction` + `wish`)

- **Pfad:** `src\AiNetLinter\Core\DiffImpactAnalyzer.cs`; DTO `src\AiNetLinter\Core\DiffImpactAnalysisModels.cs`; Ausgabe `FeatureContextFormatter.AppendCallersSection`.
- **Symbol:** `DiffImpactAnalyzer.ResolveCallerMemberNameAsync`; `CallSiteEntry`; `FeatureContextFormatter`.
- **Ansatz:** Enclosing-`ISymbol` aus `SemanticModel.GetEnclosingSymbol` behalten; `TryGetDocCommentId()` (bereits `RoslynSymbolExtensions`) in `CallSiteEntry` legen und in Backticks drucken. Display nach `MethodKind` (`Constructor` → `.ctor`, Property ohne `()`), nicht pauschal `` `{Name}()` ``.

### AI-Context-Footprint 0 (`friction`)

- **Pfad:** `src\AiNetLinter\Metrics\AIContextFootprintCalculator.cs`; Anbindung `MetricsLookupScanner.ScanType`.
- **Symbol:** `AIContextFootprintCalculator.QueueNamedSymbol` / `IsIgnoredSymbol`; `CalculateDetailed`.
- **Ansatz:** Zieltyp immer in `visited` aufnehmen, auch wenn `FootprintIgnoreNamespacePrefixes` greift (Ignore nur für **transitive** Abhängigkeiten). `DeclaringSyntaxReferences.Length == 0` nicht als 0-Footprint eines Source-Typs verkaufen — `n/a` statt `0`. Gegenprobe: `DataExecutor` Partial mit 479 LOC ≠ 0.
