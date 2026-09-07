# Finding: `search_assembly`

Namespace: `user-AiNetLinter`. Live-Calls 2026-09-07. Anker: externe Assembly `C:\ExternalAssemblies\Version-9\Example.External.Process.dll` (Dekompilat `Example.External.Process/Record.cs`, SQL-Strings `INSERT INTO` / `ExecuteNonQuery`).

## 1. Funktion und Schema-Kurzfazit

**Soll laut Toolbeschreibung:** Read-only Text-/Mustersuche im verifizierten Source- oder Dekompilat-Root einer lokalen Assembly. Pflicht `targetPath` (absolute `.dll`/`.exe`). `targetType` optional, wird als `assembly` inferiert. `searchKind`: `text` (eigenes `pattern`), `data_access` (eingebautes Regex für DB/Datei/Transaktion), `external_calls` (HTTP/RPC/Socket/Prozess); Fachmodi ohne `pattern` mit sichtbarem Builtin-Regex. `isRegex` Tri-State (`null` = auto). `declarationOnly` schließt Kommentare, Strings, XML-Docs aus. `kind`: `method` | `type` | `property`. Limits: `maxResults` (Default 50, Cap 1000), `maxFiles`, `contextLines` (Cap 5), `fileFilter`, `maxResponseBytes`, `cursor` / `continuationToken`. StructuredContent `assemblySearch`: relative Pfade, stabile IDs, Matchbereiche, `totalCount`/`returnedCount`, `completeness`, `truncatedBy`, `continuationToken`. Ohne SourceRoot: Capability `unsupported`. Assembly wird weder geladen noch ausgeführt.

**Steuert der Call richtig?** Ja für den Happy Path: `targetPath` + `pattern` bzw. `searchKind=data_access|external_calls`. `targetType` darf fehlen; falsches `targetType=project` wird still auf `assembly` korrigiert. Fehlerpfade (`INVALID_ARGUMENT`) haben brauchbare Hints.

**Irreführung:**

- JSON-`inputSchema` listet weder Enums noch erlaubte Werte für `searchKind`/`kind`; nur die Freitext-Beschreibung. `required` = nur `targetPath` — korrekt, weil Fachmodi ohne `pattern` gehen.
- Default-Suche (`declarationOnly=false`) durchsucht den **Cache-Root inklusive `manifest.json`**. Muster wie `Record` / `BusinessOperation` treffen zuerst JSON-Metadaten (3287 bzw. 354 Treffer), nicht C#. Die Beschreibung spricht von „Source- oder dekompiliertem Root“, nicht von Cache-Manifest.
- Header `completeness=partial` / `status=partial` / `generation=N` ist **Session-Dekompilat**, nicht Suchvollständigkeit. Die Suche hat eine zweite Zeile `Vollständigkeit: complete|truncated`. Agent liest den Header als „Suche unvollständig“.
- Schema verspricht stabile IDs in StructuredContent. Im sichtbaren Markdown nur `Datei:Zeile: Text` — keine DocId, kein `assembly:…`-Prefix zum Kopieren.
- `maxResponseBytes`: Default 0 = Budget 16384 (Hint). Unter 2048 → Fehler. Genau 2048 → praktisch nur Header-Krümel (`Assembly-…`), Trefferliste tot. Envelope frisst das Minimum.
- `kind=property` trifft auch feldartige Dekompilat-Zeilen ohne `{ get;`.
- `searchKind=data_access` **mit** `pattern` schneidet das Builtin-Regex mit dem Muster (UND) — Beschreibung sagt nur „ohne pattern Builtin“.

## 2. Ausgeführte Calls

Alle Calls: `targetPath=C:\ExternalAssemblies\Version-9\Example.External.Process.dll`, außer #5 (falscher Pfad). `targetType=assembly` wo angegeben, sonst weggelassen (Inferenz). Wartezeit überall **schnell**, kein Timeout. Größen = sichtbarer Markdown-Text.

| # | Parameter | Größe (grob) | Truncation / completeness | Wartezeit |
|---|-----------|--------------|---------------------------|-----------|
| 1 | `pattern=BusinessOperation`, `declarationOnly=true`, `maxResults=20`, `contextLines=1`, `targetType=assembly` | Header + 20 Trefferzeilen (Methoden/Typen in `Record.cs` u. a.), ~4k | Suche `20 von 22`, truncated; `cursor=20`; Session `partial` / `generation=3` | schnell |
| 2 | `searchKind=data_access`, `maxResults=20`, `contextLines=1`, ohne `targetType` | SQL-Zeilen `INSERT`/`UPDATE`/`SELECT`/`ExecuteNonQuery`, ~8–12k (lange Literale) | `20 von 276`, truncated; `cursor=20` | schnell |
| 3 | `searchKind=external_calls`, `maxResults=20` | Header + `0 von 0`, ~0,7k | Suche `complete`; Session trotzdem `partial` | schnell |
| 4 | `pattern=ZzNoMatchTokenQqXxNeverExists`, `maxResults=10` | `0 von 0`, ~0,6k | `complete`, kein Fuzzy | schnell |
| 5 | `targetPath=…\DoesNotExist.BusinessOperation.dll`, `pattern=Record` | Fehler ~0,3k | `INVALID_ARGUMENT`: Pfad muss existierende Datei sein; Hint absolut `.dll`/`.exe` | schnell |
| 6 | wie #1, `cursor=20` + `continuationToken=20`, `maxResults=10` | 2 Resttreffer `MaterialCheck.cs` | `2 von 22`, Suche `complete` | schnell |
| 7 | `pattern=SaveBusinessOperation`, `kind=method`, `maxResults=15` | 8 Methodensignaturen in `Record.cs`, ~2k | `8 von 8`, `complete` | schnell |
| 8 | `pattern=class\s+Record`, `isRegex=true`, `fileFilter=*Record*.cs`, `maxResults=8` | 8 `class Record*`-Deklarationen | `8 von 20`, truncated; `cursor=8` | schnell |
| 9 | `pattern=Konstanten`, `kind=type`, `maxResults=10` | 1 Treffer `BusinessOperationConstants` | `1 von 1`, `complete` | schnell |
| 10 | `pattern=IsSpecialOperation`, `kind=property`, `maxResults=10` | 1 Zeile `public bool IsSpecialOperation` (`Record.cs:1607`) | `1 von 1`, `complete` | schnell |
| 11 | `pattern=BusinessOperation`, `maxResponseBytes=800`, `contextLines=3` | Fehler | `INVALID_ARGUMENT`: Minimum 2048 Bytes für Assembly-Envelope; Hint Budget 16384 | schnell |
| 12 | wie BusinessOperation, `maxResponseBytes=2048` | praktisch nur `Assembly-…` | Envelope frisst Limit; Trefferliste unbrauchbar | schnell |
| 13 | `pattern=ExecuteNonQuery`, `searchKind=text`, `fileFilter=*Record*.cs`, `maxFiles=2`, `maxResults=50` | 50 Zeilen `Record.cs` + `RecordPosition.cs`; Session `generation=6` | `50 von 51`, truncated by `maxResults, maxFiles`; Hint `maxFiles` erhöhen | schnell |
| 14 | `searchKind=text` ohne `pattern` | Fehler nach Header | `pattern darf bei searchKind=text nicht leer sein` | schnell |
| 15 | `kind=Klasse`, `pattern=Record` | Fehler | erlaubt: `method`, `type`, `property` | schnell |
| 16 | `searchKind=sql` | Fehler | muss `text`, `data_access` oder `external_calls` sein | schnell |
| 17 | `pattern=class Record`, `isRegex=false`, `declarationOnly=true`, `maxResults=5` | 5 Klassen (`Record`, `RecordPropertiesLegacy`, `RecordCollection`, …) | `5 von 20`, truncated; Substring `class Record*` | schnell |
| 18 | wie #17, nur `continuationToken=5` (ohne `cursor`) | nächste 5 (`RecordPosition` …) | `5 von 20`; neuer `cursor=10` | schnell |
| 19 | `pattern=INSERT INTO`, `declarationOnly=true`, `maxResults=10` | `0 von 0` | `complete` — Strings ausgeblendet | schnell |
| 20 | `pattern=INSERT INTO`, `declarationOnly=false`, `maxResults=10` | 9 SQL-String-Zeilen | `9 von 9`, `complete` | schnell |
| 21 | `pattern=BusinessOperation`, `maxResponseBytes=4096`, `maxResults=50`, **ohne** `declarationOnly` | Header + `manifest.json`-Zeilen, letzte Zeile mit `…` tot | `50 von 354`, truncated; Byte-Cut mitten in Dateiname | schnell |
| 22 | `searchKind=data_access` **und** `pattern=ExecuteNonQuery`, `maxResults=5` | 5 `ExecuteNonQuery`-Sites | `5 von 56`, truncated — Builtin ∧ pattern | schnell |
| 23 | `targetType=project` (DLL-Pfad), `pattern=Record`, `maxResults=5` | still `targetType=assembly`; erste Hits `manifest.json` (`Wawi.RecordBasic` …) | `5 von 3287`, truncated | schnell |
| 24 | `fileFilter=*NoSuchFileToken*.cs`, `pattern=Record` | `0 von 0` | `complete` | schnell |

Nicht ausgeführt: fehlendes `targetPath` (Schema-`required`, Call würde clientseitig scheitern). Ein erster `maxFiles`-Call wurde einmal von Cursor Auto-review blockiert, nach Approval erfolgreich (#13).

## 3. Verdict

**teilweise** — Kernjob (Text in externes Dekompilat, Fachmodus SQL, Continuation) klappt. Default-Root mischt Cache-JSON ein, Byte-Limit kämpft mit dem Envelope, sichtbare Antwort hat keine stabilen Symbol-IDs, Session-Header signalisiert dauernd `partial`.

## 4. Schwere

**degraded**

Nicht `broken`: mit `declarationOnly=true` oder `searchKind=data_access` kommt ein Agent zu echten C#-/SQL-Treffern und kann per `continuationToken` blättern. Ohne Workaround ist die Default-Textsuche formal voll, aber die ersten Dutzend Hits sind `manifest.json` (hundert- bis tausendfach), und `maxResponseBytes` nahe dem Minimum liefert keinen nutzbaren Body.

## 5. Nutzbarkeit

**nur mit Workaround**

Für Identifier/API: `declarationOnly=true` (und ggf. `kind=method|type`). Für Persistenz: `searchKind=data_access` ohne oder mit zusätzlichem `pattern`. Continuation: denselben Call plus `continuationToken` aus dem Banner. `external_calls` hier leer — als Negativbefund brauchbar. Nie nacktes Substring ohne `declarationOnly` auf dieser DLL.

## 6. Bugs (reproduzierbar) und FP/FN

### Bugs

1. **Cache-`manifest.json` im Suchscope.** Call #21/#23: `BusinessOperation` → 354 Treffer, `Record` → 3287, erste Hits JSON. Call #1 mit `declarationOnly=true`: 22 Identifier. Default durchsucht den Assembly-Cache, nicht nur `.cs`.
2. **`maxResponseBytes=2048` unbrauchbar.** Call #11: Floor 2048 erzwungen. Call #12: sichtbarer Text `Assembly-…`. Das Minimum reicht nur für den Envelope (lange `generatedPath`-Zeile), nicht für Treffer.
3. **Byte-Truncation ohne Continuation-Hint.** Call #21: letzte Zeile mit `…` tot; Banner nennt `maxResults`, nicht Bytes. Agent weiß nicht, ob er `cursor` oder Bytes erhöhen soll.
4. **Session-`completeness=partial` bei kompletter Suche.** Call #3/#4/#9/#10/#19: Suche `complete`, Header trotzdem `partial` / `confidence=medium`. Generation sprang in derselben Session von `3` (#1) auf `6` (#13 ff.) — Dekompilat neu erzeugt.
5. **Keine sichtbaren stabilen IDs.** Beschreibung/StructuredContent versprechen IDs; Markdown nur Pfad+Zeile. Folge-`get_symbol_body` muss den Namen raten oder ein anderes Tool bemühen.
6. **`targetType=project` stillschweigend korrigiert.** Call #23 kein Fehler, Header `assembly`. Gut für Robustheit, schlecht wenn der Agent glaubt, er suche im Platform-Projekt.
7. **`maxFiles`-Banner auch bei `fileFilter`.** Call #13: Filter `*Record*.cs` + `maxFiles=2` → Hint „maxFiles erhöhen, um weitere Dateien in den Scope aufzunehmen“, parallel `50 von 51` (maxResults). Unklar, ob weitere `*Record*`-Dateien ungesucht blieben.

### FP / FN vs. Ist (Stichprobe)

**`declarationOnly` + `BusinessOperation` (Call #1/#6)**

- Dekompilat `Record.cs`: `IsSpecialOperation`, `SaveBusinessOperationKopf`, `DeleteSpecialOperation`, Klasse `BusinessOperationConstants`. MCP listet dieselben Signaturen. **TP** gegen Cache-Ist.
- Continuation #6: `MaterialCheck.CheckSpecialOperationMaterial` / `DeleteSpecialOperation`. **TP**, Restmenge 2 = 22−20.

**`INSERT INTO` (Call #19 vs #20)**

- Ohne Flag: 9 String-Literale (`ArbeitsgangPosition.cs:598`, `Record.cs:5795` …). Mit `declarationOnly=true`: 0. **TP** für „Strings ausblenden“. Kein FN auf Identifier (SQL steht nur in Strings).

**`data_access` (Call #2/#22)**

- Call #2: echte `INSERT`/`UPDATE`/`SELECT`/`ExecuteNonQuery` auf `BusinessRecordTable*` / `GenericConnection`. **TP**.
- Call #22: nur `ExecuteNonQuery`, 56 statt 276 — Filter ∧ Builtin, kein Widerspruch. **kein FN** relativ zum kombinierten Muster.

**`external_calls` (Call #3)**

- 0 Treffer. Für diese PPS-Fach-DLL plausibel (kein HttpClient/Socket im Dekompilat-Stichprobe der SQL-lastigen Dateien). **kein nachgewiesenes FN**; Builtin-Regex nicht im Text sichtbar (Beschreibung verspricht „sichtbares Regex“ — im Call-Output nicht abgedruckt).

**`kind`**

- `method` + `SaveBusinessOperation`: 8 private Save-Methoden, keine Klassen. **TP**.
- `type` + `Konstanten`: `public sealed class BusinessOperationConstants`. **TP**.
- `property` + `IsSpecialOperation`: Zeile `public bool IsSpecialOperation` ohne Accessor. Im Dekompilat eher Feld. **möglicher FP** von `kind=property`.

**Substring-FP**

- `class Record` / `class\s+Record`: trifft `RecordCollection`, `RecordPosition`, Nested `RecordPropertiesLegacy`. Konform zu Plain/Regex, aber nicht „die Klasse Record“. Workaround wäre `^public class Record$` (nicht extra live gegen Endanker geprüft; #8 mit `class\s+Record` ist bewusst breit).

**Leer / Filter**

- Unbekanntes Token (#4) und toter `fileFilter` (#24): echte 0, kein Crash, kein Fuzzy. **kein FP**.

## 7. Token-Effizienz und Folge-Call-Tauglichkeit

**Mit `declarationOnly=true`:** kompakt. 20 Identifier-Zeilen plus Continuation für den Rest. Relative Pfade `Example.External.Process/Record.cs:5788` sind zum Lesen gut, zum Folge-Call nur als Datei+Zeile, nicht als Symbol-ID.

**`data_access` Default 50/276:** tokenlastig — einzelne INSERT-Zeilen >500 Zeichen. `maxResults=20` plus `continuationToken` ist der praktikable Weg. `contextLines=1` half wenig (Trefferzeile schon lang).

**Ohne `declarationOnly`:** unbrauchbar tokenineffizient (`manifest.json` zuerst, 3287× `Record`). `maxResults` klein spart Tokens und **verliert alle C#-Treffer**.

**Continuation:** `cursor` und `continuationToken` sind numerische Offsets (gleicher Wert in #1/#6). Allein `continuationToken` (#18) funktioniert. Banner sagt klar „dieselbe Suchanfrage“. Kein Symbol-Cursor.

**StructuredContent:** Agent sieht es nicht. Hints bei `INVALID_ARGUMENT` (kind, searchKind, pattern, Pfad, maxResponseBytes) sind gut. 0-Treffer ohne Hinweis auf `declarationOnly`/`fileFilter=*.cs`.

**Header-Lärm:** jede Antwort wiederholt den vollen Cache-`generatedPath` (`…\cache\asm.cursor\{sha}\{hash}\generation-{guid}\Properties\AssemblyInfo.cs`). Das dominiert kleine Budgets.

## 8. Roslyn-konforme Wünsche

- Suchroot auf Dekompilat-`.cs` beschränken (oder `manifest.json` / Cache-Metadaten fest excluden). `declarationOnly` nicht als einziger Schutz gegen JSON.
- Builtin-Regex von `data_access` / `external_calls` in der Antwort (eine Zeile) zeigen, wie die Beschreibung verspricht.
- `kind` an Dekompilat-`ISymbol.Kind` binden (`IPropertySymbol` vs. `IFieldSymbol`); Schema-Enum = Hint (`method|type|property`).
- Sichtbare, sessionstabile Treffer-IDs (DocId ohne `generation`) oder explizit „nur Datei:Zeile, kein Symbolgraph“.
- `maxResponseBytes`: Floor so setzen, dass nach Envelope noch Treffer passen; Truncation-Banner `truncatedBy=maxResponseBytes` plus `continuationToken`.
- Session-Header `completeness` von Such-`completeness` trennen (zwei Felder, oder Header nur bei Capability-Lücken).
- `targetType=project` + `.dll` → Fehler oder deutliche Korrekturzeile, kein stilles Rewrite.
- Optional: `fileFilter` Default `*.cs` für Assembly-Textsuche.

## 9. Phase 3

AiNetLinter-Root: `C:\Daten\Entwicklung\Ralf\AiNetLinter`. Nur Nicht-ok-Befunde.

**Cache-`manifest.json` im Suchscope** (`degraded`)
- Pfad: `src\AiNetLinter\Mcp\Tools\AssemblyAnalysis\AssemblySearchTool.cs`; `src\AiNetLinter\Mcp\Tools\FileStructure\AssemblyGetFileTreeTool.cs`; `src\AiNetLinter\Baseline\FileSystemExclusionHelpers.cs`
- Symbol: `AssemblyGetFileTreeTool.ResolveRoot` (`DecompiledSourceRoot`); `ScanFile`/`ShouldSkip` (nur `IsSearchExcludedRelativePath`, keine JSON-Exclusion); `declarationOnly` filtert Nicht-`.cs` erst danach
- Ansatz: Default `fileFilter=*.cs` oder `manifest.json`/Cache-Metadaten in `ShouldSkip`. `declarationOnly` nicht als einziger Schutz. Enumeration bleibt Datei-Walk, kein Assembly-Load.

**`maxResponseBytes=2048` unbrauchbar / Envelope frisst Budget** (`degraded`)
- Pfad: `src\AiNetLinter\Mcp\Tools\AssemblyAnalysis\AssemblyAnalysisResponseLimits.cs`; `src\AiNetLinter\Mcp\Assemblies\Analysis\AssemblyAnalysisResponse.cs`
- Symbol: `MinimumResponseBytes = 2 * 1024`; `IsBelowMinimumResponseBudget`; `FormatHeader` (lange `generatedPath`-Zeile); `ApplyWireBudget`
- Ansatz: Floor = Header + mindestens eine Trefferzeile, oder Header nicht auf das Tool-Budget anrechnen. `generatedPath` kürzen (relativer Dekompilat-Root).

**Byte-Truncation ohne `truncatedBy=maxResponseBytes`** (`degraded`)
- Pfad: `AssemblyAnalysisResponse.cs`; `src\AiNetLinter\Mcp\Assemblies\Analysis\Factories\AssemblyAnalysisSearchEnvelope.cs`; `AssemblySearchTool.cs`
- Symbol: `RenderText` setzt `truncatedBy` nur aus `maxResults`/`maxFiles`; `Enrich`/`ApplyWireBudget` kürzt danach den Text; `AssemblyAnalysisSearchEnvelope.Update` schreibt `responseBudget` ins StructuredContent, nicht ins Markdown
- Ansatz: Nach Budget-Kappung Banner `truncatedBy=maxResponseBytes` plus `continuationToken` (Envelope `UpdateKnownContinuation`).

**Session-`completeness=partial` bei kompletter Suche** (`friction`)
- Pfad: `AssemblyAnalysisResponse.cs`; `src\AiNetLinter\Mcp\Assemblies\Analysis\AssemblySessionStatusExtensions.cs`; `AssemblySearchTool.cs`
- Symbol: `FormatHeader` (`metadata.Completeness` = `AssemblySessionStatus.ToCompletenessLabel()`); `RenderText` zweite Zeile `Vollständigkeit: {payload.Completeness}`
- Ansatz: Header-Feld umbenennen (`sessionCompleteness`) oder nur bei Capability-Lücken zeigen; Such-`completeness` allein in der Suchzeile.

**Keine sichtbaren stabilen IDs** (`degraded`)
- Pfad: `AssemblySearchTool.cs`; `src\AiNetLinter\Mcp\Tools\AssemblyAnalysis\AssemblySearchModels.cs`
- Symbol: `CreateMatch` (`asm-search:` + SHA16 über Pfad/Zeile/Pattern); `RenderText` druckt nur `FilePath:Line: LineText`; `AssemblySearchMatch.Id` liegt im StructuredContent
- Ansatz: `Id` in die Markdown-Zeile. Optional DocCommentId über SemanticModel der Dekompilat-Compilation (`CSharpSyntaxTree` existiert schon in `AssemblySearchDeclarationFilter.InitSyntaxTree` — Bindung nur wenn Lease eine Compilation hat, sonst Datei:Zeile belassen und im Text sagen).

**`targetType=project` still auf `assembly`** (`friction`)
- Pfad: `src\AiNetLinter\Mcp\Registration\AssemblyAnalysisToolRegistrations.cs`
- Symbol: `ResolveTargetType` (`IsSupportedAssemblyPath` → immer `"assembly"`)
- Ansatz: `targetType=project` + `.dll` → `INVALID_ARGUMENT` oder eine Korrekturzeile im Header, kein stilles Rewrite.

**`maxFiles`-Hint trotz `fileFilter`** (`friction`)
- Pfad: `AssemblySearchTool.cs`
- Symbol: `SelectMatches` (`MaxFilesTruncated`); `BuildHint` („maxFiles erhöhen, um weitere Dateien in den Scope aufzunehmen“)
- Ansatz: Hint unterscheiden: weitere `fileFilter`-Dateien vs. ungefilterter Root. `maxFiles` bleibt Scope-Limit ohne Continuation (`HasMoreVisibleMatches`).

**`kind=property` trifft feldartige Dekompilat-Zeilen** (`degraded`)
- Pfad: `src\AiNetLinter\Mcp\Tools\AssemblyAnalysis\AssemblySearchDeclarationFilter.cs`
- Symbol: `ResolveMemberHeader` (`PropertyDeclarationSyntax` → `"property"`, `FieldDeclarationSyntax` → `"field"`); Filter ist syntax-only, kein `ISymbol.Kind`
- Ansatz: Wenn Dekompilat-Compilation da: `SemanticModel.GetDeclaredSymbol` → `IPropertySymbol` vs. `IFieldSymbol`. Sonst `kind=property` nur bei `AccessorList`/`ExpressionBody`. Schema-Enum `method|type|property` an `AddSearchAssembly`.

**Builtin-Regex nicht in der Antwort** (`friction`)
- Pfad: `AssemblySearchTool.cs`
- Symbol: `BuiltInPatterns`; `ResolvePattern` (ohne `pattern` Builtin, **mit** `pattern` Ersatz — kein UND); `RenderText` zeigt `payload.Query`, bei Fachmodus ohne Pattern nicht als „Builtin“ gekennzeichnet
- Ansatz: Eine Zeile `regex=` mit dem tatsächlich verwendeten Muster. Beschreibung: mit `pattern` ersetzt das Muster das Builtin (Live-Call #22 ist Ersatz, nicht Schnittmenge).

**Schema ohne Enums für `searchKind`/`kind`** (`friction`)
- Pfad: `src\AiNetLinter\Mcp\Registration\AssemblyAnalysisToolRegistrations.cs`
- Symbol: `AddSearchAssembly` (`string? searchKind`, `string? kind`); Validierung in `AssemblySearchTool.ValidateKind` / `ValidateSymbolKind`
- Ansatz: Enums oder JSON-Schema-`enum`. Laufzeitfehlertexte sind bereits brauchbar.
