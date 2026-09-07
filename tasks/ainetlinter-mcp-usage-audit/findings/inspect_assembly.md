# Finding: `inspect_assembly`

Audit-Stand: 2026-09-07. Nur Schema-Lookup (`GetDynamicTools`) plus Live-Calls dieses Tools. Kein anderes MCP-Tool, kein Build, kein Test.

Probe-DLL: `C:\ExternalAssemblies\Version-9\Example.External.Business.dll` (external package version 9.0, Version 9.0.0.0).

## 1 Schema-Kurzfazit

Beschreibung steuert den **Basis-Call** richtig: `targetPath` absolut auf `.dll`/`.exe`, `targetType` optional und als `assembly` inferiert, metadata-only, Assembly wird nicht geladen/ausgeführt. Filter `namespace` / `typeName` / `memberName` / `memberNames` / `exactTypeName` / `publicOnly` / `maxResults` / `maxMembers` / `includeReferences` stehen in der Prosa und funktionieren.

Die Beschreibung **führt beim Default in die Irre**:

- `includeReferences` Default laut Prosa „ohne Typ-/Member-Filter **true**“, im JSON-Schema `false` — Live folgt dem Schema (keine Referenzliste ohne Flag).
- `detailLevel`, `cursor`, `continuationToken`, `maxResponseBytes` stehen **nur im JSON-Schema**, nicht in der Tool-Beschreibung. Ohne `detailLevel=full` bleibt eine große externe API bei **1 von 79 Typen** und **20 von 469 Membern** stehen. Ein Agent, der nur die Prosa liest, kommt nicht auf den Workaround.
- JSON ohne Enums für `targetType`/`detailLevel`; `maxResults`/`maxMembers` ohne min/max (Prosa: Default 100, Cap 1000).
- Prosa verspricht „strukturierte Parameterdaten“ für Methoden/Indexer — in der Agent-Antwort nur Signatur-Strings.

`targetType=project` an einer `.dll` wird still auf `assembly` gezogen (Pfad-Inferenz), kein Fehler.

## 2 Calls

Alle Calls **schnell** (kein Timeout). Header immer `origin=decompiled`, `completeness=partial`, `confidence=medium`, `bodyAvailability=available`, Cache unter `C:\Daten\Tools\AiNetLinter-win-x64\cache\asm.cursor\…`.

| # | Absicht | Argumente | Größe / Completeness | Ergebnis |
|---|---|---|---|---|
| 1 | Happy Path öffentliche API | `assembly` + RecordEngine.dll, Defaults | ~2,5k Zeichen; **stark gekürzt** (`responseBudget`) | Identität 9.0.0.0, 1 öffentlicher Namespace. **1 von 79** Typen (`Record`), **20 von 469** Membern (12 Events, dann `Add*`). Referenzen 0/171, „nicht angefordert“. Diagnosen 1/22, erwähnt 5761 CS0234. |
| 2 | Fehler, fehlende Datei | `…\DoesNotExist.RecordEngine.dll` | ~4 Zeilen | `[ERROR]: INVALID_ARGUMENT` — Pfad muss existieren. Hint: absoluter `.dll`/`.exe`. |
| 3 | Truncation `maxResults=5` | wie 1 | wie Call 1 | Weiter **1 von 79** (`gekürzt: maxResults, responseBudget`). `maxResults` ändert am Default-Budget nichts Sichtbares. |
| 4 | `exactTypeName` + `maxMembers=50` | `typeName=Record` | ~2,5k; 1 von 1 Typ | Typfilter greift. Member trotzdem **20 von 469** (`maxMembers, responseBudget`) — 50 wird vom Budget überstimmt. |
| 5 | `includeReferences=true` | Defaults + Flag | ~2,2k | Referenzen **1 von 171** (nur `Microsoft.VisualBasic` aus dem AiNetLinter-Ordner). Sessions **1 von 218**. Member-Anzeige fällt auf 17. Diagnosen 1/111. |
| 6 | `memberName=Save` | `typeName=Record` (Teiltext) | ~2,8k; 23 von 79 Typen | `typeName` ohne `exactTypeName` = Substring. `Record.Save` / `SaveArchiv` / `SaveSchlussrechnung` + Event `SaveModeCommit`. Übrige `Record*`-Typen mit **0 Member**. |
| 7 | `namespace=…RecordEngine` | nur Namespace | wie Call 1 | Kein Gewinn: einziger öffentlicher Namespace. |
| 8 | `publicOnly=false` | `maxResults=3`, `maxMembers=5` | ~2,5k | Namespaces 3 (inkl. `My` / `My.Resources`). Typen **3 von 98**. Private Nested-Structs `Record.ItemUpdate`, `Record.InventoryCheck`. `Record` dann 1103 Member (vs. 469 public). |
| 9 | Kleiner Typ | `typeName=RecordManager`, `exactTypeName` | ~2,2k; 1 von 1 | **7 von 9** Member, immer noch `responseBudget`. Signaturen mit `Mandant`, `Erfassungsart` ohne Namespace. |
| 10 | Kleiner Typ vollständig | `typeName=RecordSession`, `exactTypeName` | ~2,0k | **5 Member komplett**, inkl. Konstruktor als `void RecordSession.RecordSession()`. |
| 11 | Pagination raten | `cursor=1`, `continuationToken=next` | ~2,4k; 3 von 79 | **Überspringt `Record`**, startet bei `RecordBudget`. `cursor` wirkt als Typ-Skip-Index. Antwort enthält **kein** Folgetoken. |
| 12 | `detailLevel=full` | sonst Defaults | **56,8 KB / 550 Zeilen**, Datei trunkiert (`Sag…`) | **79 von 79** Typen. `Record` 100/469, `RecordPosition` 100/309 (`maxMembers`). Letzter Typ (`DataServiceRecordPositionTransfer`) mitten in Properties abgeschnitten. |
| 13 | `maxResponseBytes=8000` | Defaults | kurz, Pfade `…` | Budget greift: Header-Pfade gekürzt. |
| 14 | `memberNames` exakt OR | `["Save","Load","Delete"]`, `typeName=Record` | ~2,6k; 24 von 79 | Nur exakte Namen: `Delete`, `Load` (2 Overloads), `Save`. Kein `SaveArchiv`. |
| 15 | `cursor=2`, ohne `targetType` | nur `targetPath` | ~2,3k; 4 von 79 | `targetType` korrekt auf `assembly` inferiert. Skip um 2 Typen (`RecordCalculateException` zuerst). |
| 16 | Falsches `targetType=project` | DLL-Pfad | wie Call 1 | **Kein Fehler.** Header `targetType=assembly`. |
| 17 | Leer, unbekannter Typ | `typeName=DoesNotExistTypeXYZ`, `exactTypeName` | ~3,5k | **0 von 0** Typen, 0 Namespaces. Dafür Diagnosen **20 von 22** (Missing-Refs, 5761 CS0234). Kein Hint auf Teiltextsuche. |
| 18 | FQN | `typeName=Example.External.Business.Record`, `exactTypeName` | wie Call 4 | Trifft denselben Typ. |
| 19 | `maxMembers=1000` ohne `detailLevel` | `typeName=Record` exact | wie Call 4 | Weiter **20 von 469** — Cap 1000 nutzlos gegen `responseBudget`. |
| 20 | Relativer Pfad | `Example.External.Business.dll` | ~3 Zeilen | `INVALID_ARGUMENT`: Pfad muss absolut sein. |
| 21 | Property-Filter | `typeName=RecordPosition`, `memberName=Handle` | ~2,6k; 1 von 11 Typen | 24 von 33 Handle-Membern. Properties **ohne Property-Typ**. |
| 22 | Ungültiges `detailLevel` | `detailLevel=bogus` | groß, wie full, Chat-Trunkierung | Verhält sich wie **full**: 79 von 79 Typen. Kein `INVALID_ARGUMENT`. |
| 23 | `maxResults=1000` ohne `detailLevel` | Defaults | wie Call 1 | Weiter **1 von 79**. Cap allein hebt `responseBudget` nicht. |
| 24 | Existierende Nicht-DLL | `…RecordEngine.xml` | ~3 Zeilen | `INVALID_ARGUMENT`: muss `.dll` oder `.exe` sein. |
| 25 | Workaround große Klasse | `detailLevel=full`, `exactTypeName`, `typeName=Record`, `maxMembers=1000` | **41,6 KB / 509 Zeilen**; 1 von 1 Typ | **469 Member vollständig** (Events, Methoden mit Signatur, Properties ohne Typ). Danach Diagnosen 20/22. |
| 26 | `cursor=0`, leeres Token | Defaults + Pagination-Felder | wie Call 1 | Wie Happy Path. Kein ausgegebenes Token. |
| 27 | `maxResults=0` | Defaults | wie Call 1 | Still 1 Typ, Footer `gekürzt: maxResults, responseBudget`. Kein Fehler. |

## 3 Verdict

**Teilweise wie beschrieben.** Metadata-only, Identität, öffentliche Typen/Member, Filter, Fehler bei fehlender/falscher Datei und relativem Pfad stimmen. `completeness=partial` bei fehlenden extern-/Framework-Refs ist dokumentiert und trifft zu.

Abweichungen: Default-Antwort ist für eine große API **nicht** die „öffentliche API“, sondern ein Budget-Schnipsel; `detailLevel` (der eigentliche Schalter) fehlt in der Beschreibung; `includeReferences`-Default Prosa vs. Schema vs. Live; `cursor` paginiert Typen ohne dokumentierte Semantik und ohne Rückgabe-Token; `detailLevel=bogus` = full; `maxResults=0` still; Property-Typen fehlen; `targetType=project` an einer DLL wird verschluckt.

## 4 Schwere

**`degraded`**

Das Tool ist nicht kaputt: Fehlercodes sind klar, Signaturen einer gefilterten Klasse sind mit Workaround vollständig. Für den dokumentierten Zweck „externe API nutzen, ohne die DLL zu laden“ ist der **Default unbrauchbar** (1/79 Typen, 20/469 Member, fast nur Events). Der rettende Parameter steht nicht in der Tool-Prosa. Token-Last bei `detailLevel=full` (57 KB, trotzdem unvollständig) plus 5761-Diagnose-Rauschen machen Folgecalls riskant. Nicht `broken` (kein Absturz, kein falscher Assembly-Name in der Stichprobe). Nicht nur `friction` (Truncation verhindert das Ziel, nicht nur Schema-Härte).

## 5 Nutzbarkeit

**nur mit Workaround.**

Als Agent würde ich das Tool **wieder aufrufen**, aber nie mit Defaults auf einer externe Assembly:

1. Zuerst `detailLevel=full` + kleines `maxMembers` (oder `maxResults`) für den **Typenkatalog**.
2. Dann `exactTypeName=true` + `typeName=<Kurzname>` + `detailLevel=full` + `maxMembers=1000` für **eine** Klasse.
3. Gezielte Member: `memberName` (Teiltext) oder `memberNames` (exakt OR).

Ohne diesen Dreischritt reicht die Antwort **nicht**, um die extern-Record-API zu nutzen. Auch mit Dreischritt: Property-Typen fehlen, Fremdtypen (`Mandant`, `Message`, `Erfassungsart`, `Recordart`) oft ohne Namespace, keine XML-Docs, keine Symbol-IDs für `get_symbol_body`. Die DLL wird vom Tool nicht geladen — das Ziel „ohne Load/Execute“ ist erfüllt; „ohne die API zu kennen schon rufen können“ nicht.

## 6 Bugs+FP/FN

| Befund | Art | Call |
|---|---|---|
| Default zeigt 1/79 Typen, 20/469 Member; `maxResults`/`maxMembers` helfen nicht gegen `responseBudget` | Truncation / Agent-Falle | 1, 3, 4, 19, 23 |
| `detailLevel` undokumentiert in der Tool-Prosa, aber nötig für den Typenkatalog | Schema vs. Beschreibung | 12, 25 |
| `detailLevel=bogus` = full, kein Fehler | Clamp / stilles Fallback | 22 |
| `includeReferences` Default: Prosa true, Schema+Live false | Spec-Lüge | 1 vs. 5 |
| `cursor` skippt Typen, Antwort liefert kein `continuationToken` | Undokumentierte Pagination | 11, 15, 26 |
| `targetType=project` an `.dll` still `assembly` | Kein INVALID_ARGUMENT | 16 |
| `maxResults=0` still 1 Typ | Clamp ohne Fehler | 27 |
| Properties ohne Datentyp (`RecordPosition.Handle`, `Record.Zwischensumme`, …) | FN der API-Nutzbarkeit | 21, 25 |
| Unbekannter Typ: 0 Treffer, dafür 20 Missing-Ref-Diagnosen statt kurzer Leermeldung | Token-FP / Rauschen | 17 |
| Collection-Typen mit 0 Member (`RecordZuschlagCollection`, …) bei `detailLevel=full` | evtl. nur geerbte Member aus anderer Assembly; kein Record ohne `rg` | 12 |
| Konstruktoren als `method: void Type.Type(…)` | Darstellung, kein FP der Existenz | 10, 12 |
| Optionaler VB-Parameter als `[bool callFromRecordCopy]` | nützlich, wie beschrieben | 11 |
| `memberName=Save` trifft Event `SaveModeCommit` (Teiltext) | erwartbar, Agent muss `memberNames` für Exakt | 6 vs. 14 |
| Referenzen 1/171 trotz Flag: Budget frisst die extern-Refs | Truncation | 5 |
| 5761 CS0234 (`Microsoft.VisualBasic.ApplicationServices`) in jeder Antwort | Diagnose-Rauschen, `completeness=partial` korrekt | alle Erfolge |

**Stichprobe vs. Ist (ohne zweites MCP-Tool):** Call 25 listet 469 Memberzeilen, Header „469 Member“. `Save(bool)`, `Load(int)`, `Delete()`, `Calculate(bool)` und Property `Handle` (über Call 21 auf `RecordPosition`) sind konsistent. Kein Namens-FP in dieser Stichprobe. Property-Typen und fehlende 369 Member im Default sind das eigentliche Loch, kein Halluzinations-FP.

## 7 Token/IDs

- Default (Call 1): klein, aber **inhaltlich leer** für API-Arbeit.
- `detailLevel=full` ohne Typfilter: ~57 KB, 79 Typen, große Klassen bei 100 Membern abgeschnitten, letzte Zeile mitten im Identifier — **Token-Flut ohne Vollständigkeit**.
- Call 25 (ein Typ, 469 Member): ~42 KB, brauchbar; Footer 20 Diagnosen sind Ballast.
- `maxResponseBytes=8000` kürzt den Header, nicht intelligent den Member-Teil.

Keine Symbol-IDs, keine Member-IDs, kein Cursor-Token in der Markdown-Antwort. Folge-Call nur über denselben `targetPath` plus `typeName`/`memberName`. `generatedPath` / `decompiledSourceRoot` zeigen auf den AiNetLinter-Cache — verleitet, Dateien dort zu lesen statt zu filtern. `bodyAvailability=available` gilt für die Session, dieses Tool liefert **keine** Bodies.

StructuredContent (Parameterobjekte) in der Agent-Sicht nicht erkennbar; nur Signaturzeilen.

## 8 Roslyn-Wünsche

- Default für große APIs: Typenkatalog **ohne** Member (Namen + Kind + Memberzahl), Member erst nach `typeName`.
- `detailLevel` in der Tool-Beschreibung mit Enum (`summary` / `types` / `full`) und `INVALID_ARGUMENT` bei unbekanntem Wert.
- Sichtbares Folgetoken (`continuationToken`) plus dokumentierte `cursor`-Semantik (Typ-Offset, Member-Offset getrennt).
- `responseBudget` darf `maxMembers=1000` auf **einem** gefilterten Typ nicht auf 20 Member stauchen; Budget zuerst Header/Diagnosen kürzen.
- Properties/Felder mit **Typ** (Roslyn `IPropertySymbol.Type`), Methodenparameter weiter als jetzt.
- Diagnosen default 0–3 Zeilen; 5761 CS0234 nicht in jeder API-Antwort.
- `includeReferences`: Schema, Prosa und Live angleichen; bei `true` eigene Pagination, nicht die Typenliste verdrängen.
- `INVALID_ARGUMENT` bei `maxResults < 1` / über Cap und bei `targetType=project` + `.dll`.
- Optionale XML-Doc-Zusammenfassung aus benachbarter `.xml` (Metadaten, kein LLM), wenn die Datei neben der DLL liegt.

## 9 Phase 3

Read-only `C:\Daten\Entwicklung\Ralf\AiNetLinter`. Je Nicht-ok-Befund: Pfad, Symbol, Roslyn-Ansatz.

### Default 1/79 Typen, 20/469 Member; `maxResults`/`maxMembers` verlieren gegen `responseBudget` (`degraded`)

- **Pfad:** `src\AiNetLinter\Mcp\Tools\AssemblyAnalysis\Responses\InspectAssemblyResponseBuilder.cs`; `src\AiNetLinter\Mcp\Tools\AssemblyAnalysis\AssemblyAnalysisResponseLimits.Budget.cs`; `src\AiNetLinter\Configuration\AssemblyAnalysisConfiguration.cs`
- **Symbol:** `InspectAssemblyResponseBuilder.ApplyResponseBudget`; `AssemblyAnalysisResponseLimits.ProjectResponseBudget` / `TryRemoveLastMember` / `TryRemoveLastType`; `AssemblyAnalysisConfigurationOptions.DefaultResponseBudgetBytes` (16 KiB)
- **Ansatz:** `FitsResponseBudget` zählt Markdown **plus** JSON-Payload gegen dasselbe 16-KiB-Lease-Budget. Zuerst Referenzen/Diagnosen, dann Member (Minimum 3 bzw. 1), dann Typen. Default-Katalog: `Inspect` ohne Member (`INamedTypeSymbol.GetMembers` erst nach `typeName`). Bei gesetztem `typeName` Member-Schnitt vor Header/Diagnosen schützen: `maxMembers` nicht unter das angeforderte Limit stauchen, solange ein Typ gefiltert ist. `detailLevel=full` setzt in `ResolveResponseBudget` bereits 64 KiB — das innere Skip in `ApplyResponseBudget` (sobald `DetailLevel` nicht null) ist der Grund, warum erst `full` den Katalog zeigt.

### `detailLevel` fehlt in der Tool-Prosa (`friction`)

- **Pfad:** `src\AiNetLinter\Mcp\Registration\AssemblyAnalysisToolRegistrations.cs`
- **Symbol:** `InspectAssemblyDescription`; Parameter `detailLevel` in `AddInspectAssembly`
- **Ansatz:** Prosa um Enum `compact` / `standard` / `full` ergänzen (wie `get_assembly_context`). Semantik an `AssemblyAnalysisResponseLimits.ResolveResponseBudget` koppeln: `compact` = Namespaces + Typnamen ohne Member; `standard` = Default-Budget; `full` = `MaxResponseBytes`. JSON-Schema-Enum nachziehen (MCP-SDK-Parameter ist `string?`).

### `detailLevel=bogus` verhält sich wie voller Katalog (`friction`)

- **Pfad:** `src\AiNetLinter\Mcp\Tools\AssemblyAnalysis\Responses\InspectAssemblyResponseBuilder.cs`; `src\AiNetLinter\Mcp\Tools\AssemblyAnalysis\AssemblyAnalysisResponseLimits.cs`
- **Symbol:** `InspectAssemblyResponseBuilder.ApplyResponseBudget` (Skip bei jedem nicht-null `DetailLevel`); `ResolveResponseBudget` (`_ => standard`)
- **Ansatz:** Unbekannten Wert → `McpToolResults.InvalidArgument` analog `GetImpactTool.ResolveDetailLevel`. Skip der Projektion nur bei gültigem `full`, nicht bei jedem String.

### `includeReferences`: Prosa true, Schema+Live false (`friction`)

- **Pfad:** `src\AiNetLinter\Mcp\Registration\AssemblyAnalysisToolRegistrations.cs`; `src\AiNetLinter\Mcp\Tools\AssemblyAnalysis\AssemblyAnalysisModels.cs`
- **Symbol:** `AddInspectAssembly` (`bool includeReferences = false`); `InspectAssemblyArguments.IncludeReferenceDetails`
- **Ansatz:** `IncludeReferenceDetails` wertet `IncludeReferences ?? (kein Typ-/Member-Filter)` aus, aber die Registration reicht immer ein konkretes `bool` durch — `??` feuert nie. Parameter auf `bool? includeReferences = null` stellen **oder** Prosa/Default auf `false` angleichen. Liste weiter über `AssemblyAnalysisResponseLimits.ProjectReferences` (Cap 32), Pagination getrennt von der Typenliste.

### `cursor` skippt Typen, Markdown ohne Folgetoken (`degraded`)

- **Pfad:** `src\AiNetLinter\Mcp\Tools\AssemblyAnalysis\AssemblyAnalysisModels.cs`; `src\AiNetLinter\Mcp\Tools\AssemblyAnalysis\InspectAssemblyFormatter.cs`; `src\AiNetLinter\Mcp\Tools\AssemblyAnalysis\AssemblyAnalysisService.cs`
- **Symbol:** `AssemblyPaging.ReadOffset` / `CreateToken`; `InspectAssemblyFormatter.FormatText`; `AssemblyAnalysisService.Inspect` (`types.Skip(offset).Take(maxResults)`)
- **Ansatz:** Offset ist 0-basierter Typ-Skip (`INamedTypeSymbol`-Liste nach Namespace/Name). Token liegt schon im Payload (`ContinuationToken`), der Formatter schreibt ihn nicht. In `AppendTypes` die nächste Offset-Zahl ausgeben (`cursor`/`continuationToken` dieselbe Dezimalform). `ReadOffset`: nicht-numerische Tokens (`next`) → `INVALID_ARGUMENT`, nicht still 0. Member-Offset getrennt, falls später nötig.

### `targetType=project` an `.dll` still `assembly` (`friction`)

- **Pfad:** `src\AiNetLinter\Mcp\Registration\AssemblyAnalysisToolRegistrations.cs`; `src\AiNetLinter\Mcp\AnalysisTargetResolver.cs`
- **Symbol:** `AssemblyAnalysisToolRegistrations.ResolveTargetType`; `AnalysisTargetResolver.ResolveTargetType`
- **Ansatz:** Pfad-Inferenz nur wenn `targetType` null/leer. Explizites `project` nicht umbiegen — `AssemblyAnalysisDispatcher.ExecuteAsync` liefert dann bereits `UnsupportedProjectTarget`. Müllwerte (`foo`) durch den Resolver (`null` → `INVALID_ARGUMENT`: nur `project`|`assembly`).

### `maxResults=0` still 1 Typ (`friction`)

- **Pfad:** `src\AiNetLinter\Mcp\Tools\AssemblyAnalysis\AssemblyAnalysisService.cs`
- **Symbol:** `AssemblyAnalysisService.NormalizeLimit` (`requested <= 0 ? defaultValue`)
- **Ansatz:** `requested < 1` oder über `MaxResults` (1000) → `INVALID_ARGUMENT`, kein stilles Clamp auf 100. Dasselbe für `maxMembers`.

### Properties ohne Datentyp (`degraded`)

- **Pfad:** `src\AiNetLinter\Mcp\Tools\AssemblyAnalysis\AssemblyAnalysisService.cs`; `src\AiNetLinter\Mcp\Tools\AssemblyAnalysis\InspectAssemblyFormatter.cs`
- **Symbol:** `AssemblyAnalysisService.ToMemberDto`; `InspectAssemblyFormatter.AppendType`
- **Ansatz:** Für `IPropertySymbol` Signatur analog `MethodSignature`: `property.Type.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat)` plus Name; Felder über `IFieldSymbol.Type`. Parameter-DTOs existieren schon (`Parameters`) — im Markdown mit ausgeben (Name, Typ, `RefKind`), nicht nur `Signature`.

### Unbekannter Typ: 0 Treffer, 20 Missing-Ref-Diagnosen (`degraded`)

- **Pfad:** `src\AiNetLinter\Mcp\Tools\AssemblyAnalysis\InspectAssemblyFormatter.cs`; `src\AiNetLinter\Mcp\Tools\AssemblyAnalysis\AssemblyAnalysisResponseLimits.cs`
- **Symbol:** `InspectAssemblyFormatter.FormatText` → `AppendDiagnostics`; `ProjectDiagnostics` (Default-Sample 20)
- **Ansatz:** Bei `ShownCount == 0` Diagnosen auf 0–1 Zeile kappen oder hinter `detailLevel=full`. CS0234 aus der Decompile-`Compilation.GetDiagnostics()` sind `completeness=partial` — nicht die API-Antwort füllen. Leermenge: ein Satz plus Hint auf Teiltext (`exactTypeName=false`).

### Collection-Typen mit 0 Member (`wish`)

- **Pfad:** `src\AiNetLinter\Mcp\Tools\AssemblyAnalysis\AssemblyAnalysisService.cs`
- **Symbol:** `ToTypeDto` (`type.GetMembers()` ohne Basistypen)
- **Ansatz:** `INamedTypeSymbol.GetMembers()` ist nur declared. Optional `BaseType`-Kette in derselben oder referenzierten Assembly laufen (`OriginalDefinition`), Member mit `ContainingType` kennzeichnen — oder explizit „nur declared, keine geerbten“ in der Typzeile. Kein RAG.

### Referenzen 1/171 trotz Flag (`degraded`)

- **Pfad:** `src\AiNetLinter\Mcp\Tools\AssemblyAnalysis\AssemblyAnalysisResponseLimits.Budget.cs`; `src\AiNetLinter\Mcp\Tools\AssemblyAnalysis\InspectAssemblyFormatter.cs`
- **Symbol:** `TryRemoveLastReference` / `TryTrimInspectOptional` (Referenzen vor Membern/Typen); `AppendReferences`
- **Ansatz:** Bei `includeReferences=true` Referenzliste nicht zugunsten der ersten `Record`-Member opfern; eigene Pagination (`cursor` auf `References`-Index) oder Diagnosen/Header zuerst kürzen. Cap bleibt `ProjectReferences` (32).

### 5761 CS0234 in jeder erfolgreichen Antwort (`friction`)

- **Pfad:** `src\AiNetLinter\Mcp\Tools\AssemblyAnalysis\AssemblyAnalysisContextFactory.cs`; `src\AiNetLinter\Mcp\Tools\AssemblyAnalysis\AssemblyAnalysisResponseLimits.cs`
- **Symbol:** `FromGeneration` (`generation.Diagnostics`); `AppendDiagnostics` / `ProjectDiagnostics`
- **Ansatz:** Decompile-Diagnostics als Session-Status (`completeness=partial`) zählen, Samples default 1; `detailLevel=full` darf bis `DefaultMaxDiagnostics` (20). Nicht jedes `INamedTypeSymbol`-Listing mit Framework-Missing-Refs füllen.

### Default soll Typenkatalog ohne Member sein (`wish`)

- **Pfad:** `src\AiNetLinter\Mcp\Tools\AssemblyAnalysis\AssemblyAnalysisService.cs`; `src\AiNetLinter\Mcp\Tools\AssemblyAnalysis\InspectAssemblyFormatter.cs`
- **Symbol:** `Inspect` / `ToTypeDto`; `AppendType`
- **Ansatz:** Ohne `typeName`: nur `INamedTypeSymbol` (Name, `TypeKind`, `GetDocumentationCommentId()`, `GetMembers().Length` als Zahl). Member erst mit Filter. Spart das Budget, das heute Events von `Record` frisst.

### Keine Symbol-IDs im Markdown (`degraded`)

- **Pfad:** `src\AiNetLinter\Mcp\Tools\AssemblyAnalysis\InspectAssemblyFormatter.cs`; `src\AiNetLinter\Mcp\Tools\AssemblyAnalysis\AssemblyAnalysisService.cs`
- **Symbol:** `InspectAssemblyFormatter.AppendType`; `AssemblyAnalysisService.StableId`
- **Ansatz:** `ToTypeDto`/`ToMemberDto` setzen bereits `ISymbol.GetDocumentationCommentId()`. Formatter ignoriert `AssemblyTypeDto.Id` / `AssemblyMemberDto.Id`. In die Typ-/Memberzeile in Backticks schreiben, damit `get_symbol_body` / `get_assembly_context` denselben `T:`/`M:`-String übernehmen.

### XML-Docs neben der DLL (`wish`)

- **Pfad:** `src\AiNetLinter\Mcp\Assemblies\Analysis\AssemblyRoslynWorkspaceFactory.cs`; `src\AiNetLinter\Mcp\Assemblies\Analysis\AssemblyReferenceResolver.cs`
- **Symbol:** `CreateAsync` / `MetadataReference.CreateFromFile`
- **Ansatz:** Liegt `Path.ChangeExtension(dll, ".xml")` vor, `XmlDocumentationProvider.CreateFromFile` an die Metadata-Reference hängen; Zusammenfassung über `ISymbol.GetDocumentationCommentXml()` (erste `<summary>`). Rein metadata, kein LLM.
