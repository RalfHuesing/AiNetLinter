# Finding: `find_magic_values`

Audit-Stand: 2026-09-07. Nur Schema-Lookup (`GetDynamicTools`) plus Calls dieses Tools. Kein anderes MCP-Tool, kein Build, kein Test.

## 1 Schema-Kurzfazit

Beschreibung steuert den Call **größtenteils richtig**: On-Demand-Audit auf Magic Values in C#-Quellcode; Pflicht `targetType=project` + absoluter `targetPath`; Assembly **ausdrücklich unsupported**. Optionale Filter (`valueType`, `categoryFilter`, `minOccurrences`, `maxResults`, `ignoreNumbers`, `includeTests`, `includeSuppressed`, `changedOnly`, `scopeFilter` / Aliase `scope`/`path`) stehen in Beschreibung und JSON-Schema.

Abweichungen Beschreibung vs. Live:

- `valueType` (`all` / `strings` / `numbers`) ist im Schema ein freier String; ungültige Werte liefern sauberes `INVALID_ARGUMENT`. **Live filtert `numbers` nicht auf Zahlenliterale** (siehe Calls 5/19/27). `strings` hat dieselben Zähler wie `all`.
- Truncation-Footer sagt „Pattern verfeinern oder maxResults erhöhen“ — dieses Tool hat **kein** `pattern`. Copy-Paste aus `search_pattern`.
- JSON-Schema ohne Enums für `valueType`/`categoryFilter`/`targetType`; Bounds für `maxResults`/`minOccurrences` fehlen. Ungültige Enum-Strings werden aber (anders als stille Clamps bei anderen Tools) abgewiesen.
- `ignoreNumbers` ist `integer[]`; wirkt nur teilweise (HTTP-`499` ja, Splitter-`200`/`23` und Pattern-`1` nein).
- Kein `cursor`/`continuationToken`. Keine Symbol-IDs.

Antwort: Markdown-Liste `Datei:Zeile - kategorie: wert (Nx, Empfehlung: …)` plus Kopfzeile mit Treffer-/Unique-/Datei-Zählern. Relativpfade.

## 2 Calls

Projektroot: `C:\Workspace\Sample.Project`. Alle erfolgreichen Calls **schnell** (kein Timeout).

| # | Absicht | Argumente | Größe / Completeness | Ergebnis |
|---|---|---|---|---|
| 1 | Happy Path | `project` + Root, Defaults | ~50 Listenzeilen, ~6k Zeichen; **trunkiert** | 116 Treffer / 53 Unique / 1251 Dateien. 50 gezeigt. Footer: „53 Treffer gesamt, 50 gezeigt — Pattern verfeinern oder maxResults erhöhen“. Mix `enum`/`constant`/`nameof`/`security`/`localization`. Fast nur Strings; einzige Zahl: `1`. |
| 2 | Assembly unsupported | `assembly` + `Example.External.Core.dll` | ~6 Zeilen | `[ERROR]: ASSEMBLY_TARGET_UNSUPPORTED` + Hint auf `project`. Wie beschrieben. |
| 3 | Truncation | wie 1, `maxResults=3` | ~8 Zeilen | Kopf unverändert 116/53. 3 Unique gezeigt, Footer „53 … 3 gezeigt“. |
| 4 | Leer | `minOccurrences=9999` | ~3 Zeilen | `0 Treffer … 1251 Dateien`. Text: „Keine Magic Values.“ Klar, kein `isError`. |
| 5 | `valueType=numbers` | Default `minOccurrences=2`, `maxResults=20` | ~8 Zeilen | **9/4 Unique** — identisch mit `categoryFilter=enum_candidates` (Call 20). Drei **Strings** (`"Memo"`, `"IstErledigt"`, `"Escape"`) plus Zahl `1`. |
| 6 | `valueType=strings` | `maxResults=10` | ~15 Zeilen, trunkiert | **116/53** — dieselben Zähler wie `all`. Filter schließt Zahlen nicht aus bzw. ändert die Menge nicht. |
| 7 | `categoryFilter=security_candidates` | Defaults | ~6 Zeilen, vollständig | 6/3: `"password"` (STS + ConnectionStrings) und `"autocomplete"` (ChangePasswordDialog). |
| 8 | `categoryFilter=config_candidates` | Production, Defaults | ~3 Zeilen | **Leer** (0/0 / 1251). In Tests existieren Config-Treffer (Call 25). |
| 9 | `includeTests=true` | `maxResults=15` | ~20 Zeilen, stark trunkiert | **2272/724 Unique / 2295 Dateien.** Token-Flut. Tests (Setup-Fixtures) stehen nach den ersten Prod-Hits. |
| 10 | `scopeFilter=DataExecutor` | Defaults (`minOccurrences=2`, keine Tests) | ~3 Zeilen | 0 Treffer über **2 Dateien**. Filter trifft. |
| 11 | `ignoreNumbers=[0,1,2]` + `valueType=numbers` | wie 5 | wie 5 | Zahl `1` **bleibt**. `ignoreNumbers` greift hier nicht. |
| 12 | `changedOnly=true` | Defaults | 1 Zeile | „Keine Dateien im Scope (changedOnly aktiv (kein Git-Diff oder keine geaenderten Dateien) + Test-Pfade ausgefiltert)“. Passend zum Workspace (nur untracked Finding-MDs). |
| 13 | `includeSuppressed=true` | `maxResults=10` | wie Truncation von 1 | Zähler **identisch** 116/53. Ohne bekannte `// ainetlinter-disable MagicValues` nicht beweisbar, ob der Schalter tot ist. |
| 14 | Alias `path` | `path=SchedulerSelectionMapper` | ~4 Zeilen | 2/1 über 1 Datei. Nur `enum_candidates: 1`. Alias wirkt. |
| 15 | Alias `scope` | `scope=extern100StsTokenClient` | ~4 Zeilen | 2/1: `"password"`. Alias wirkt. |
| 16 | Ungültiges `valueType` | `integers` | ~3 Zeilen | `INVALID_ARGUMENT` + gültige Werte `all, strings, numbers`. Gut. |
| 17 | Ungültiges `categoryFilter` | `not_a_category` | ~3 Zeilen | `INVALID_ARGUMENT` + volle Kategorie-Liste. Gut. |
| 18 | Fremder Projektpfad | `C:\DoesNotExist\NoProjectHere` | — | **Nicht live:** Cursor Auto-review hat den Call blockiert (anderer Root). |
| 19 | `numbers` + Tests + `minOccurrences=1` | `maxResults=20` | ~25 Zeilen, trunkiert | **209/193 / 2295 Dateien.** Gezeigte Treffer fast nur **Strings** (Tabellennamen, deutsche UI-Texte, `"/adressen"`). |
| 20 | `categoryFilter=enum_candidates` | `maxResults=8` | ~8 Zeilen | **9/4** — deckungsgleich Call 5. |
| 21 | `categoryFilter=standard_candidates` | Defaults | ~3 Zeilen | Leer bei `minOccurrences=2`. Singleton `499` erscheint erst mit `minOccurrences=1` (Call 27). |
| 22 | `localization_candidates` | `maxResults=8` | ~6 Zeilen | 4/2: DomainCalendar-Store + AiChatOrchestrator, deutsche Sätze. |
| 23 | `nameof_candidates` | `maxResults=5` | ~10 Zeilen, trunkiert | 80/36. JSON-/Metadaten-Keys (`"PackId"`, `"id"`, `"SqlConnectionId"`). |
| 24 | `constant_candidates` | `maxResults=8` | ~12 Zeilen | 17/8: Format `"yyyy-MM-dd"`, CSS-Klassen, `"refresh-data"`, Lab-IDs. |
| 25 | Scope DataExecutor + Tests + `minOccurrences=1` | `includeTests=true` | ~10 Zeilen | 7/5 über 7 Dateien: Test-ConnectionStrings (`config`/`security`), zwei deutsche TAN-Meldungen in `DataExecutor.OptimisticConcurrency.cs`. **Keine Timeout-Zahlen.** |
| 26 | `maxResults=0` | Defaults | ~6 Zeilen | Kopf 116/53, **1 gezeigt**, Footer „53 … 1 gezeigt“. 0 wird auf **1 geklemmt**, kein Fehler. |
| 27 | `valueType=numbers`, `minOccurrences=1`, Production | Defaults `maxResults` | ~25 Zeilen | 29/24. Mix: Strings **und** echte Zahlen `200`, `23`, `0.225`, `1`, `499`. |
| 28 | Vollständig | `maxResults=100` | ~55 Zeilen, **nicht** trunkiert | Alle 53 Unique. Letzte: `"end"`, `"lab-g01"`, `"yyyy-MM-dd"` in `FormValueCoercion`. |
| 29 | `ignoreNumbers=[200,23,499]` + `numbers` + `minOccurrences=1` | — | ~25 Zeilen | 28/23. **`499` weg**, `200` und `23` **bleiben**, `1` bleibt. |
| 30 | Pflichtfeld fehlt | nur `targetPath`, kein `targetType` | 1 Zeile | Unstrukturiert: `An error occurred invoking 'find_magic_values'.` Kein `INVALID_ARGUMENT`, kein Hint. |

## 3 Verdict

**Teilweise / abweichend.** Happy Path, Kategorien, Scope-Aliase, Assembly-Absage, Leertext und gültige `INVALID_ARGUMENT`-Enums funktionieren. Der beworbene Literal-Filter `valueType` **tut nicht, was die Beschreibung sagt**. Truncation-Hinweis nennt ein nicht existentes `pattern`. `ignoreNumbers` ist kategorieabhängig. Default `minOccurrences=2` blendet die meisten konkreten Magic **Numbers** aus.

## 4 Schwere

**`degraded`**

Das Tool liefert formal Ergebnisse und ist für wiederholte String-Keys nutzbar. Für den naheliegenden Agenten-Auftrag „zeig mir Magic Numbers“ ist es **falsch** (`valueType=numbers` ≈ `enum_candidates` bei Default-`minOccurrences`, sonst String-Mix). Security-Empfehlungen („KeyVault“) auf HTML-`autocomplete` und OAuth-`grant_type=password` sind irreführend. Token-Risiko bei `includeTests` (724 Unique). Nicht `broken` im Sinne „kein Call möglich“; nicht nur `friction`, weil der Zahlenfilter inhaltlich lügt.

## 5 Nutzbarkeit

**nur mit Workaround.**

- Nicht `valueType=numbers` verwenden, bis der Filter steht.
- Für Zahlen: `minOccurrences=1` + `categoryFilter` (`constant_candidates` / `standard_candidates`) und Ergebnisse selbst nach Literalart filtern.
- `includeTests` nur mit engem `scopeFilter`/`maxResults`.
- `security_candidates` nicht als Secret-Fundstelle lesen, sondern die Zeile öffnen.
- Truncation: `maxResults` erhöhen (Default 50 verdeckt 3/53 Unique), Footer ignoriert „Pattern“.

## 6 Bugs+FP/FN

| Befund | Art | Call |
|---|---|---|
| `valueType=numbers` bei Default-`minOccurrences` = Menge `enum_candidates` (überwiegend Strings) | Bug | 5 vs. 20 |
| `valueType=strings` gleiche Zähler wie `all` (116/53) | Bug / toter Filter | 1 vs. 6 |
| `valueType=numbers` + `minOccurrences=1` listet Tabellennamen, UI-Texte, `"/adressen"` | Bug | 19, 27 |
| Truncation-Footer „Pattern verfeinern“ ohne Pattern-Parameter | Copy-Paste | 1, 3 |
| `maxResults=0` → 1 Zeile, stilles Clamp | Clamp | 26 |
| Fehlendes `targetType` → generischer Invoke-Fehler statt `INVALID_ARGUMENT` | Fehlertext | 30 |
| `ignoreNumbers` filtert `499` (`standard_candidates`), nicht `200`/`23` (`constant_candidates`) und nicht Pattern-`1` | Bug | 11, 29 |
| `"autocomplete"` → KeyVault | FP | 7, Datei |
| `"password"` in STS-Form als Secret statt OAuth-`grant_type`/`password`-Feldname | FP | 15, Datei |
| `nameof("id")` / JSON-Property-Keys | schwache Empfehlung / FP | 23 |
| Default blendet Singleton-Zahlen `200`, `23`, `0.225`, `499` aus | FN durch Default | 1 vs. 27 |
| Production `config_candidates` / `standard_candidates` leer bei `minOccurrences=2` | FN-Risiko | 8, 21 |
| DataExecutor-Scope: keine Timeout-/CommandTimeout-Zahlen, nur Texte/ConnectionStrings | möglicher FN oder bereits Konstanten | 25 |

**Stichprobe Magic Number vs. Datei:** `SchedulerSelectionMapper.cs:136` — Tool: `enum_candidates: 1 (2x, Empfehlung: enum Value { ... })`.

Ist in der Datei (`IsReadonlyProperties`):

```csharp
return value switch
{
    true => true,
    1 => true,
    "1" => true,
    _ => false,
};
```

Das Integer-`1` existiert. Die `2x` zählen vermutlich Literal `1` und String `"1"` zusammen. **Empfehlung `enum Value` ist ein False Positive:** Truthy-Coercion für JSON/JS-`readonly` (bool / 1 / `"1"`), kein Domänen-Enum. `"true"` (Call 27, Zeile 135) ist ebenfalls kein Enum-Kandidat.

**Offensichtlich fehlend im Happy Path:** Singleton-Schwellen `200` (Splitter-Min, zwei Dateien je 1×), `23` (`DataTable.razor.cs:81`), `0.225` (Sidebar), HTTP-`499` (`PlatformApplicationExtensions.cs:110`, Empfehlung `StatusCodes.Status499` — fachlich plausibel). Default `minOccurrences=2` hält sie zurück. Timeouts in `DataExecutor` tauchten in der Stichprobe nicht auf.

`ChangePasswordDialog.razor.cs:21`: `["autocomplete"] = "current-password"` — HTML-Attribut, kein Secret. `extern100StsTokenClient.cs:75`: `["grant_type"] = "password"` plus Form-Key `["password"]` — OAuth-Password-Grant, kein KeyVault-Kandidat für das Literal `"password"`.

## 7 Token/IDs

Default (50 Unique-Cap): mittel, für einen Audit-Scan ok, aber 3 Unique still versteckt. `includeTests` ohne Scope: **unbrauchbar groß** (724 Unique, 2272 Treffer).

Keine Symbol-IDs, kein StructuredContent mit Folgetool-Hints. Nur `Pfad:Zeile`. Folge-Call muss `view_file`/`Read` sein, nicht `get_symbol_body`. Kein Continuation-Token; Pagination nur über `maxResults` (dann wieder von vorn, nicht cursor-basiert).

Zähler im Kopf („116 Treffer in 53 eindeutigen Einträgen“) bleiben bei Truncation stabil — das ist gut. Der Footer-Hinweis auf `pattern` ist schlecht.

## 8 Roslyn-konforme Wünsche

- `valueType=numbers` strikt auf numerische Literale (`LiteralExpressionSyntax` Numeric, inkl. `0.225`); Strings nie in dieser Menge.
- `valueType=strings` die Zahl `1` und andere Numeric-Literale ausschließen.
- `ignoreNumbers` auf den Literalwert **aller** Kategorien anwenden (nicht nur `standard_candidates`).
- Footer-Text ohne `pattern`; optional `nextHint: maxResults erhöhen` / `minOccurrences=1 für Singletons`.
- Security-Heuristik: HTML-Attribute (`autocomplete`) und OAuth-Grant-Typen nicht als KeyVault-Secrets klassifizieren (Roslyn: Dictionary-Indexer-Key vs. Literal-Wert, bekannte Key-Namen).
- Pattern-Match `1` / `"1"` / `true` nicht als `enum_candidates` vorschlagen (Switch-Arm auf `object`/JSON).
- `maxResults<=0` → `INVALID_ARGUMENT` statt Clamp auf 1.
- Fehlendes `targetType` → dasselbe `INVALID_ARGUMENT` wie bei Enum-Fehlern.
- Option: `minOccurrences` Default 2 in der Beschreibung als „versteckt Singletons“ kenntlich machen, oder `includeSingletons` für Zahlen.
- Symbol-ID oder `(Datei, Start, End)` für Folge-`get_symbol_body`.

## 9 Phase 3

AiNetLinter-Root: `C:\Daten\Entwicklung\Ralf\AiNetLinter`. Je Nicht-ok-Befund: Pfad + Symbol + Roslyn-Ansatz.

### `valueType=numbers` ≈ `enum_candidates` / Strings in der Zahlenmenge (Call 5, 19, 20, 27)

- **Pfad:** `src\AiNetLinter\Mcp\Tools\MagicValues\FindMagicValuesScannerWalker.cs`
- **Symbol:** `ClassifyEnumCandidates` (schreibt direkt in `Sink`, ohne `IsInScope` / `ValueTypeFilter`), `ProcessLiteral.IsInScope` (filtert `NumericLiteralExpression` vs. String nur auf dem normalen Literal-Pfad)
- **Ansatz:** Enum-Pfad durch dieselbe Scope-Prüfung: nur `SyntaxKind.NumericLiteralExpression` bei `valueType=numbers`, nur String-Literale bei `strings`. `DuplicateConstScanner.DetectDuplicateConstFieldsAsync` ebenfalls `ValueTypeFilter` anwenden (umgeht den Walker). `LiteralExpressionSyntax.Kind()` / `Token.Value is int/double`.

### `valueType=strings` gleiche Zähler wie `all` (Call 1 vs. 6)

- **Pfad:** dieselbe Walker-Datei plus `src\AiNetLinter\Mcp\Tools\MagicValues\MagicValuesClassifier.cs`
- **Symbol:** `IsTrivialLiteral` (int `−1…1` werden im Classifier verworfen; die sichtbare Zahl `1` kommt vom Enum-Pfad), `IsInScope`
- **Ansatz:** Nach dem Enum-Fix fallen Zahlen aus `strings` raus. `all` darf Mix. Zähler dann divergieren.

### Truncation-Footer „Pattern verfeinern“ (Call 1, 3)

- **Pfad:** `src\AiNetLinter\Mcp\McpTruncation.cs`, `src\AiNetLinter\Mcp\Tools\MagicValues\FindMagicValuesScanner.cs`
- **Symbol:** `McpTruncation.TruncateLines` (fester Meta-Text), `FormatReport`
- **Ansatz:** Tool-spezifische Meta-Zeile (`maxResults erhöhen`, `minOccurrences=1` für Singletons) oder optionaler `hint`-Parameter an `TruncateLines`. Nicht das Wort `pattern` bei Tools ohne Pattern-Parameter.

### `maxResults=0` → Clamp auf 1 (Call 26)

- **Pfad:** `src\AiNetLinter\Mcp\Tools\MagicValues\FindMagicValuesTool.cs`
- **Symbol:** `ExecuteAsync` (`Math.Max(1, args.MaxResults)`, Kommentar „Clamp statt reject“)
- **Ansatz:** `maxResults < 1` → `INVALID_ARGUMENT`. Gleiches für `minOccurrences < 1` (heute ebenfalls `Math.Max(1, …)`).

### Fehlendes `targetType`: Invoke-Fehler (Call 30)

- **Pfad:** `src\AiNetLinter\Mcp\Registration\AnalysisToolRegistrations.cs`, `src\AiNetLinter\Mcp\AnalysisTargetResolver.cs`
- **Symbol:** `AddFindMagicValues` (`string targetType` Pflicht), `AnalysisTargetResolver.Resolve` (leeres `targetType` → `INVALID_ARGUMENT`, Server nicht erreicht)
- **Ansatz:** wie `pattern_detect`: Server-Validierung existiert; Cursor-SDK bricht vorher ab. Kein Roslyn-Fix. Schema `required` beibehalten.

### `ignoreNumbers` nur bei `int` im Classifier, nicht Enum/Dup-Const (Call 11, 29)

- **Pfad:** `src\AiNetLinter\Mcp\Tools\MagicValues\MagicValuesClassifier.cs`, `FindMagicValuesScannerWalker.cs`, `DuplicateConstScanner.cs`
- **Symbol:** `IsTrivialLiteral` (`Token.Value is int` → `ignoreNumbers.Contains`; `double`/`long` nie), `ClassifyEnumCandidates`, `EmitDuplicateConstGroups`
- **Ansatz:** Ignore-Set auf den Literalwert aller Pfade: Enum-Sink, Duplicate-Const, Numeric unabhängig vom CLR-Typ (`Convert.ToInt32` wo ganzzahlig; Doubles nicht über `int[]` matchen oder `ignoreNumbers` um Doubles erweitern). HTTP-`499` verschwindet heute, weil er den Classifier-Pfad mit `int` nimmt.

### `"autocomplete"` / `"password"` → KeyVault (Call 7, 15)

- **Pfad:** `src\AiNetLinter\Mcp\Tools\MagicValues\MagicValuesStringHeuristics.cs`
- **Symbol:** `ClassifySecurityCandidate` (Heuristik 2: `ResolveSurroundingName` + `SecurityNameKeywords.Contains`/Substring; Heuristik 3: Literal exakt `password`), `ResolveSurroundingName` (kein Indexer-Key vs. Wert)
- **Ansatz:** `ElementAccessExpressionSyntax` / Collection-Initializer: Key-Literale (`autocomplete`, `grant_type`) nicht als Secret. Wert `"password"` neben Key `grant_type` = OAuth-Grant, nicht CWE-798. HTML-Attribut-Keys (`autocomplete`) allowlisten. Surrounding-Name nicht über Klassen-/Methodennamen mit `password` im Identifier auf jedes Literal im Body ziehen.

### `nameof("id")` / JSON-Keys als `nameof_candidates` (Call 23)

- **Pfad:** `src\AiNetLinter\Mcp\Tools\MagicValues\MagicValuesStringHeuristics.cs`
- **Symbol:** `ClassifyNameofCandidate` / `HasMatchingSymbolName` (exakter Identifier-Match im Scope)
- **Ansatz:** Dictionary-Indexer-Keys und JSON-Property-Literale (`["id"]`, `"PackId"`) nicht als `nameof` vorschlagen, wenn Parent `ElementAccess`/`ImplicitElementAccess`/`AttributeArgument` ist. `nameof` nur bei Vergleich/Argument, das einem lokalen/Parameter-Symbol entspricht (`ISymbol.Kind` über `SemanticModel`).

### Switch `1` / `"1"` / `true` → `enum Value` (Call 14, Stichprobe `SchedulerSelectionMapper`)

- **Pfad:** `src\AiNetLinter\Mcp\Tools\MagicValues\FindMagicValuesScannerWalker.cs`
- **Symbol:** `DetectEnumCandidatesInSwitchExpression` (`literals.Count >= 3`, jedes Literal inkl. Zahl und String), `ClassifyEnumCandidates`
- **Ansatz:** Schwelle nur auf Literale **desselben** `SyntaxKind` (nicht int+string+bool mischen). Governing Expression vom Typ `object`/`JsonElement`/ohne Identifier (`ExtractIdentifierName` null bei MemberAccess) nicht als Enum verkaufen. Truthy-Coercion (`true`/`1`/`"1"`) ausschließen.

### Default `minOccurrences=2` blendet Singleton-Zahlen aus (Call 1 vs. 27; 8, 21)

- **Pfad:** `src\AiNetLinter\Mcp\Registration\AnalysisToolRegistrations.cs`, `src\AiNetLinter\Mcp\Tools\MagicValues\FindMagicValuesScanner.cs`
- **Symbol:** `AddFindMagicValues` Default `minOccurrences = 2`, `AggregateAndFilter` (`Occurrences >= minOccurrences`), Gruppe `(Category, Value, FilePath)`
- **Ansatz:** Beschreibung: Default versteckt Einmal-Literale. Optional `includeSingletons` oder Default 1 nur für `valueType=numbers`. Keine neue Heuristik nötig — Aggregation ist schon Roslyn-Literal-basiert.

### DataExecutor: keine Timeout-Zahlen (Call 25)

- **Pfad:** `src\AiNetLinter\Mcp\Tools\MagicValues\MagicValuesNumberClassifier.cs`
- **Symbol:** `ClassifyNumber` (`IsInMethodCallArgument` + `TimeoutParameterNames` inkl. `timeout`; Property-Assignment nicht)
- **Ansatz:** Zusätzlich `AssignmentExpressionSyntax` / Property `CommandTimeout`/`Timeout` (`IdentifierName` enthält Timeout-Token). `SemanticModel.GetSymbolInfo` auf `IPropertySymbol`. Const-Felder, die Timeouts schon halten, bleiben FN — das ist dann korrekt kein Magic Literal.

### `maxResults`/`valueType` ohne Schema-Enum/Bounds (§1)

- **Pfad:** `src\AiNetLinter\Mcp\Registration\AnalysisToolRegistrations.cs`, `src\AiNetLinter\Mcp\Tools\MagicValues\FindMagicValuesTool.cs`
- **Symbol:** `AddFindMagicValues` (`string? valueType = "all"`), `ResolveValueType` / `ResolveCategory` (Live-Enum)
- **Ansatz:** Live-`INVALID_ARGUMENT` ist gut. SDK-`enum` für `valueType`/`categoryFilter`/`targetType` analog anderer Tools; Bounds an `maxResults`/`minOccurrences`.

### Keine Symbol-IDs, nur `Pfad:Zeile` (§7, Wunsch)

- **Pfad:** `src\AiNetLinter\Mcp\Tools\MagicValues\FindMagicValuesScanner.cs`, `FindMagicValuesScannerWalker.cs`
- **Symbol:** `RawMagicValue` / `GroupedMagicValue` / `FormatReport`
- **Ansatz:** Am Literal `SemanticModel.GetEnclosingSymbol(span)` oder `TryFindEnclosingMember`; DocCommentId in die Zeile. Folge-Call `get_symbol_body` ohne Raten.
