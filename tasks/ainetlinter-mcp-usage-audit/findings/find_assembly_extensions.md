# Finding: `find_assembly_extensions`

Namespace: `user-AiNetLinter`. Live-Calls 2026-09-07. Anker-Assembly: `C:\ExternalAssemblies\Version-9\Example.External.Core.dll` (Dekompilat, `origin=decompiled`, `confidence=medium`, `completeness=partial`). Kein Consumer-Projekt.

## 1. Funktion und Schema-Kurzfazit

**Soll laut Toolbeschreibung:** klassische C#-Extension-Methoden einer lokalen `.dll`/`.exe` metadata-only über Roslyn finden. `targetPath` Pflicht, `targetType` optional (Default `assembly`). Kein Consumer-Projekt in diesem Dispatch. Filter: `receiverType`, `extensionName`, `namespace`. `includeReferences` (Default false) zieht bounded Referenz-Assemblies. `maxResults` Default 100, Maximum 1000. Antwort soll `applicable` / `not_applicable` / `not_decidable` trennen; fehlende Abhängigkeiten → `completeness=partial`. Methoden zusätzlich mit strukturierten Parameterdaten. Assembly wird weder geladen noch ausgeführt.

**Steuert der Call richtig?** Für den Happy-Path-Katalog ja: nur `targetPath` reicht, `targetType` wird inferiert. Filter `receiverType` / `extensionName` / `namespace` wirken. Die Kernwarnung der Beschreibung ist **klar und live wahr**: ohne Consumer-Projekt ist die Roslyn-Anwendbarkeit `not_decidable`. Jeder Treffer trägt denselben Grund: „Kein auflösbarer Consumer-Typ angegeben.“

**Irreführung / Schema-Lücken:**

- JSON-Properties haben **keine** Feld-Beschreibungen. Agent muss die lange Tool-Prosa lesen. `cursor`, `continuationToken`, `detailLevel`, `maxResponseBytes` stehen im Schema ohne Semantik, Enum oder Beispiel.
- `receiverType` filtert den Empfänger (kurz `Record` → 19 Treffer), macht die Anwendbarkeit aber **nicht** entscheidbar. Die Formulierung „receiverType grenzt ein; ohne Consumer-Projekt … not_decidable“ ist fachlich richtig, leicht als „mit receiverType wird es decidable“ zu lesen.
- Beschreibung verspricht getrennte Buckets `applicable` / `not_applicable` / `not_decidable`. Live gibt es **keine** Bucket-Sektionen, nur den Status pro Zeile. Ohne Consumer-Projekt ist die ganze Liste `not_decidable`; `not_applicable` taucht nicht auf.
- „strukturierte Parameterdaten“ sind in der Agenten-Oberfläche **nicht** sichtbar — nur Signaturzeilen.
- `extensionName` ist undokumentierter **Substring** (`StateCalculate` trifft auch `StateCalculateValidate`).
- `maxResponseBytes`: Schema-Default `0`; live bedeutet 0 das konfigurierte Assembly-Budget **16384** (steht nur im Fehler-Hint). Werte `< 2048` → `INVALID_ARGUMENT`. `2048` und `4096` liefern sichtbar nur `.`.
- `cursor` ist live ein **ganzzahliger Offset** (String `"1"` / `"12"`), aber die gekürzte Antwort gibt **keinen** Folgewert aus. `continuationToken="next"` ist stilles No-Op.
- `targetType=project` wird still zu `assembly` umgebogen, kein Fehler.
- `detailLevel`: `full` und `minimal` dumpfen gleich viele Diagnosen; Default kürzt Diagnosen auf 1/29. Erlaubte Werte fehlen im Schema.

## 2. Ausgeführte Calls

Alle Calls außer #12: `targetPath=C:\ExternalAssemblies\Version-9\Example.External.Core.dll`. Wartezeit überall **schnell**, kein Timeout. Größen = sichtbarer Markdown-Text (StructuredContent nicht separat sichtbar).

| # | Parameter | Größe (grob) | Truncation / completeness | Wartezeit |
|---|-----------|--------------|---------------------------|-----------|
| 1 | nur `targetPath` | ~12 Treffer + Header + 1 Diagnose, ~6–8k | **12 von 188** (`maxResults, responseBudget`); `completeness=partial`; Referenzen 1/192 | schnell |
| 2 | `maxResults=2` | 2 Treffer, ~2k | 2 von 188 (`maxResults, responseBudget`) | schnell |
| 3 | `receiverType=Record` | 12 Treffer, ~7k | **12 von 19** (`responseBudget`); Receiver-Zeile `Record` | schnell |
| 4 | `extensionName=StateCalculate` | 3 Treffer, ~2k | **3 von 3**, vollständig für den Filter; trifft auch `StateCalculateValidate` | schnell |
| 5 | `namespace=Example.External.Core.Extensions` | wie #1, 12/188 | Filter plausibel (alle sichtbaren Treffer in diesem NS); Totale bleibt 188 | schnell |
| 6 | fehlende DLL `…\DoesNotExist.dll` | — | **nicht ausgeführt:** Cursor Auto-review blockiert abweichenden DLL-Pfad | — |
| 7 | `targetType=project` | wie #1 | stilles Coerce auf `assembly`, kein Fehler | schnell |
| 8 | `extensionName=DoesNotExistMethod` | Header + Diagnose, ~1,5k | **0 von 0**, `partial`, kein Crash | schnell |
| 9 | `receiverType=DoesNotExistType` | ~1,5k | **0 von 0**; Zeile `Receiver: DoesNotExistType` | schnell |
| 10 | `namespace=DoesNotExist.Namespace` | ~1,5k | **0 von 0** | schnell |
| 11 | `includeReferences=true`, `maxResults=5` | 5 Treffer, ~4k | 5/188; Referenzen **29/192**; Sessions **1/239**; Diagnosen 1/115; weiter `not_decidable` | schnell |
| 12 | `targetPath=""` | Fehler ~0,2k | `INVALID_ARGUMENT: Der Parameter 'targetPath' ist erforderlich` + Hint `targetType`/`targetPath` | schnell |
| 13 | `maxResponseBytes=800` | Fehler ~0,3k | `INVALID_ARGUMENT`: Minimum **2048**; Hint: weglassen → Budget **16384** | schnell |
| 14 | `detailLevel=full`, `maxResults=3` | 3 Treffer + **20/29 Diagnosen**, ~4k | 3/188 nur `maxResults` (Budget reicht); Diagnose-Flut CS0234/fehlende Refs | schnell |
| 15 | `targetType=assembly`, `receiverType=Record`, `maxResults=19` | 12 Treffer | weiter **12 von 19** (`responseBudget`) — `maxResults` allein hebt das Byte-Limit nicht | schnell |
| 16 | `extensionName=GetIsDirty` | 1 Treffer, ~1,8k | **1 von 1**, exakter Name | schnell |
| 17 | `continuationToken=next` | wie #1 | stilles No-Op, erste Seite 12/188 | schnell |
| 18 | `maxResponseBytes=2048` | **`.`** | formal akzeptiert, Agent-Text tot | schnell |
| 19 | `cursor=1`, `maxResults=5` | 5 Treffer ab `GetIsParked` | Offset **1** überspringt `GetIsDirty`; Truncation `maxResults, responseBudget` | schnell |
| 20 | `detailLevel=minimal`, `maxResults=2` | 2 Treffer + **20/29 Diagnosen** | `minimal` reduziert Diagnosen **nicht** gegenüber `full` | schnell |
| 21 | `receiverType=System.String` | ~1,5k | **0 von 0**; Receiver-Zeile gesetzt | schnell |
| 22 | `cursor=12`, `maxResults=10` | 10 Treffer (Rest-`Record` + `RecordPosition`), ~5k | nächste Seite des 188er-Katalogs; weiter `not_decidable` | schnell |
| 23 | `maxResponseBytes=4096` | **`.`** | wie #18 | schnell |

## 3. Verdict

**teilweise** — Extension-Katalog (Namen, Empfänger, Signaturen, Overloads, Generics) aus der externe Assembly ist brauchbar und filterbar. Beschreibung zu `not_decidable` ohne Consumer-Projekt trifft zu. Pagination, Byte-Budget, Schema-Doku, IDs und Anwendbarkeits-Buckets weichen ab bzw. fehlen in der sichtbaren Antwort.

## 4. Schwere

**degraded**

Nicht `broken`: mit Filtern und undokumentiertem `cursor`-Offset kommt ein Agent zum Katalog (z. B. `StateCalculate` auf `Record`, `GetRecordUuid` auf `RecordPosition`). Formal ok, aber Default-Truncation (12/188 ohne Folgetoken), totes `maxResponseBytes`, immer `not_decidable`, Diagnose-Lärm und fehlende Symbol-IDs machen Folgearbeit riskant.

## 5. Nutzbarkeit

**nur mit Workaround**

Wieder aufrufen als **Inventar**: ja, wenn der Agent `maxResults` klein hält **und** selbst `cursor` als Offset hochzählt (`"0"`, `"12"`, `"22"`, …), plus `receiverType`/`extensionName` für Präzision. Anwendbarkeit (`applicable`) ohne Consumer-Projekt **nicht** erwarten — die Beschreibung sagt das, die Antwort bleibt trotzdem eine flache `not_decidable`-Liste. Für Folge-Tools (`get_symbol_body`, `find_references`) fehlen kopierbare Symbol-IDs.

## 6. Bugs (reproduzierbar) und FP/FN

### Bugs

1. **Default-Antwort kürzt den Katalog ohne Folgeschritt.** Call #1: 12 von 188, Gründe `maxResults, responseBudget`, obwohl Schema-Default `maxResults=100`. Call #15: `maxResults=19` bleibt bei 12 wegen `responseBudget`. Kein `cursor`/`continuationToken` in der Antwort. Agent muss Offset **raten**.
2. **`cursor` wirkt, ist aber undokumentiert.** Call #19 `"1"` überspringt den ersten Treffer; Call #22 `"12"` liefert die nächste inhaltlich konsistente Seite (`PositionOrderBy`, `Save`, dann `RecordPosition`-Extensions). Schema/Beschreibung erwähnen das nicht; gekürzte Antworten offerieren den nächsten Offset nicht.
3. **`continuationToken` No-Op.** Call #17 `"next"` = erste Seite wie #1, kein Fehler.
4. **`maxResponseBytes` unter Default zerstört die Antwort.** Call #13 `<2048` → klarer Fehler. Call #18/`#23` (`2048`, `4096`) → sichtbarer Text nur `.` — kein Envelope, kein Hinweis. Schema-Default `0` ≠ dokumentiertes Live-Budget 16384.
5. **`targetType=project` stilles Coerce.** Call #7. Schema-Prosa verlangt `assembly`; JSON erlaubt beliebigen String.
6. **`detailLevel` ohne Vertrag.** `full` und `minimal` (Call #14/#20) dumpfen 20/29 Decompiler-Diagnosen; Default 1/29. `minimal` ist nicht minimal.
7. **`includeReferences` ändert Anwendbarkeit nicht.** Call #11: Sessions 1/239, Referenzen 29/192, weiter `not_decidable` / „Kein auflösbarer Consumer-Typ“. Mehr Token, kein `applicable`.
8. **Promised StructuredContent unsichtbar.** Keine Parameter-Objekte, keine Bucket-Listen, keine IDs — nur Markdown-Signaturen.
9. **Datei-nicht-gefunden** nicht autonom prüfbar (Auto-review, Call #6). Leerpfad Call #12 ist ein sauberer `INVALID_ARGUMENT`.

### FP / FN vs. Ist (Stichprobe)

Ground Truth = dieselbe MCP-Session (Dekompilat-Katalog), keine zweite Metadatenquelle in diesem Auftrag.

- Call #16 `GetIsDirty` → eine Methode `ExternalRecordExtension.GetIsDirty(Record)` — **TP**, konsistent mit #1 Zeile 1.
- Call #4 `StateCalculate` → zwei `StateCalculate`-Overloads **plus** `StateCalculateValidate`. **FP**, wenn der Agent exakten Namen erwartet; **TP** für undokumentierten Substring.
- Call #3 `receiverType=Record` → Totale 19 (nicht 188). Call #22 ab Offset 12 zeigt weitere `Record`-Methoden und dann `RecordPosition` — Empfänger-Filter in #3 hat `RecordPosition` korrekt **nicht** in den 19. **kein FN** für den Kurznamen `Record`.
- Call #8/#9/#10/#21 unbekannter Name/Typ/NS/`System.String` → 0/0. **kein FP**; Leer ist unterscheidbar von Fehler. Unbekannter Receiver wird nicht als „Typ fehlt in der Assembly“ erklärt (könnte FN-Missverständnis sein: 0 Extensions vs. unauflösbarer Typ).
- Alle 188 sichtbaren Seiten: Status `not_decidable`. Laut Beschreibung **kein FP** (kein Consumer-Projekt). Bucket `not_applicable` nie beobachtet — entweder keine unpassenden Kandidaten nach Filter oder Bucket wird nicht gerendert (**mögliche FN-Darstellung**).
- Diagnosen CS0234 `Example.External.Engine` u. a.: ehrlich für `completeness=partial`, aber kein Extension-Befund.

## 7. Token-Effizienz und Folge-Call-Tauglichkeit

**Default (#1):** ~12 volle Signaturen + langer Cache-`generatedPath` + eine abgeschnittene CS0234-Diagnose. 176 Extensions unsichtbar. Token-Last mittel, Informationsverlust hoch.

**Filter:** `extensionName` exakt (Call #16) ist tokenarm und vollständig. `receiverType=Record` spart Suche, bleibt aber bei 12/19 am Byte-Budget hängen. `includeReferences=true` und `detailLevel=full|minimal` sind teuer (fehlende extern-/BCL-Refs, Versionsmismatches Polly/SimpleInjector/MSTest) und helfen dem Katalog nicht.

**IDs:** keine `assembly:…`-DocIds, keine Methodensymbole. Sichtbar sind Anzeigenamen `Example.External.Core.Extensions.GetIsDirty` und Signaturen mit **kurzen** Empfängertypen (`Record`, nicht FQN). Für Folge-Calls muss der Agent raten (`find_symbol`/`get_symbol_body` auf dem Dekompilat) — dieser Tool-Output ist kein ID-Lieferant.

**Pagination-Hint:** Truncation nennt `maxResults, responseBudget`, nicht den nächsten `cursor`. Fehler-Hints zu `targetPath` und `maxResponseBytes≥2048` sind brauchbar; letzterer widerspricht dem Schema-Default 0.

**`not_decidable` als Agenten-Signal:** klar genug, um Anwendbarkeit **nicht** zu behaupten. Ohne Consumer-Projekt ist das Tool ein Extension-Verzeichnis, kein Roslyn-`ApplicableToType`-Orakel. Das steht in der Beschreibung; die Antwort verstärkt es in jeder Zeile (redundant, aber ehrlich).

## 8. Roslyn-konforme Wünsche

- Gekürzte Listen: nächsten Offset/`cursor` (oder Continuation) **in den Text** schreiben; `cursor` im Schema als ganzzahligen 0-basierten Offset dokumentieren. `continuationToken` implementieren oder entfernen.
- Default-Budget so setzen, dass `maxResults=100` oder klar `N von Total` plus Folgeschritt erreichbar ist. `maxResponseBytes`: Default 0 im Schema durch „0 = Server-Budget“ ersetzen; Werte ≥2048 dürfen nie nur `.` liefern.
- `extensionName`: Exact vs. Substring dokumentieren oder `nameEquals`/Regex analog anderer Tools.
- `receiverType`: kurze Namen und FQN; in der Antwort FQN des `this`-Parameters. Anwendbarkeit weiter `not_decidable` ohne Compilation des Consumers — aber Bucket-Sektionen nur füllen, was wirklich berechnet wurde; leere `applicable`/`not_applicable` explizit machen.
- Pro Treffer eine stabile metadata-DocId (`M:Example.External.Core.Extensions.ExternalRecordExtension.GetIsDirty(Record)` bzw. Assembly-SHA+DocId ohne Session-Generation), damit Welle-2-Combos zu `get_symbol_body`/`search_assembly` durchreichen.
- `detailLevel`: Enum `minimal|standard|full`; `minimal` = Katalog ohne Diagnose-Samples; Decompiler-CS* nur unter `full` oder `completeness`.
- `includeReferences`: Anwendbarkeit bleibt `not_decidable` ohne Consumer — im Text nicht suggerieren, Sessions würden `applicable` machen. Optional: Receiver-Typ in Referenz-Sessions auflösen, sobald die Assembly den Typ exportiert (weiter metadata-only).
- `targetType` ungleich `assembly` → `INVALID_ARGUMENT`, kein stilles Coerce.
- Strukturierte Parameter (Name, Typ, `this`) auch im Markdown, nicht nur im unsichtbaren DTO.

## 9. Phase 3

Read-only `C:\Daten\Entwicklung\Ralf\AiNetLinter`. Je Nicht-ok-Befund: Pfad, Symbol, Roslyn-Ansatz.

### Default 12/188 ohne Folgetoken; `maxResults` verliert gegen `responseBudget` (`degraded`)

- **Pfad:** `src\AiNetLinter\Mcp\Tools\AssemblyAnalysis\Responses\FindAssemblyExtensionsResponseBuilder.cs`; `src\AiNetLinter\Mcp\Tools\AssemblyAnalysis\AssemblyAnalysisResponseLimits.Budget.cs`
- **Symbol:** `FindAssemblyExtensionsResponseBuilder.ApplyResponseBudget` / `FormatText`; `AssemblyAnalysisResponseLimits.ProjectResponseBudget` / `TryRemoveLastExtension`; `AssemblyPaging.CreateToken`
- **Ansatz:** Wie Inspect: Markdown+JSON gegen 16-KiB-Lease (`AssemblyAnalysisConfigurationOptions.DefaultResponseBudgetBytes`). `FindExtensions` nimmt `IMethodSymbol.IsExtensionMethod` + `Skip(offset).Take(maxResults)`, Token liegt im Payload, `FormatText` schreibt ihn nicht. Nächsten Offset in den Header (`Assembly-Extensions: N von Total; cursor=…`). `detailLevel` nicht-null überspringt die Projektion komplett — Default muss `maxResults=100` oder klaren Folgeschritt liefern.

### `cursor` wirkt undokumentiert; `continuationToken=next` No-Op (`degraded`)

- **Pfad:** `src\AiNetLinter\Mcp\Tools\AssemblyAnalysis\AssemblyAnalysisModels.cs`; `src\AiNetLinter\Mcp\Registration\AssemblyAnalysisToolRegistrations.cs`
- **Symbol:** `AssemblyPaging.ReadOffset` (`int.TryParse` und `offset > 0`, sonst 0); `AddFindAssemblyExtensions` (`effectiveCursor = cursor ?? continuationToken`); `FindAssemblyExtensionsDescription`
- **Ansatz:** Schema/Prosa: 0-basierter Treffer-Offset. `CreateToken` ist schon die Dezimalzahl. Nicht-numerische Tokens → `INVALID_ARGUMENT`. Alias dokumentieren oder `continuationToken` entfernen.

### `maxResponseBytes` 2048/4096 → sichtbarer Text `.` (`degraded`)

- **Pfad:** `src\AiNetLinter\Mcp\Assemblies\Analysis\AssemblyAnalysisResponse.cs`; `src\AiNetLinter\Configuration\AssemblyAnalysisConfiguration.cs`
- **Symbol:** `AssemblyAnalysisResponse.ApplyWireBudget` / `TrimUtf8`; `Enrich` (`IsBelowMinimumResponseBudget`)
- **Ansatz:** `Measure` = Textbytes + StructuredContent. Bei knappem Budget bleibt `remainingForText` oft 1 → `TrimUtf8` liefert `"."` (`targetBytes < 0` und `maxBytes >= 1`). Floor so setzen, dass Envelope + Header + erste Extension-Zeile passen, sonst `INVALID_ARGUMENT`. Schema-Default 0 in der Prosa als „konfiguriertes Budget 16384“ benennen.

### `targetType=project` stilles Coerce (`friction`)

- **Pfad:** `src\AiNetLinter\Mcp\Registration\AssemblyAnalysisToolRegistrations.cs`; `src\AiNetLinter\Mcp\AnalysisTargetResolver.cs`
- **Symbol:** `ResolveTargetType` (DLL-Pfad erzwingt `"assembly"`); `AnalysisTargetResolver.ResolveTargetType`
- **Ansatz:** Nur inferieren wenn `targetType` leer. Sonst `UnsupportedProjectTarget` bzw. Resolver-`INVALID_ARGUMENT` für Werte ≠ `assembly`.

### `detailLevel` ohne Vertrag; `minimal` nicht minimal (`friction`)

- **Pfad:** `src\AiNetLinter\Mcp\Tools\AssemblyAnalysis\Responses\FindAssemblyExtensionsResponseBuilder.cs`; `src\AiNetLinter\Mcp\Tools\AssemblyAnalysis\AssemblyAnalysisResponseLimits.cs`
- **Symbol:** `ApplyResponseBudget` (Skip bei jedem `DetailLevel`); `ResolveResponseBudget` (nur `compact`/`full`); `AppendDiagnostics`
- **Ansatz:** Enum `minimal|standard|full`. `minimal` = Katalog ohne Diagnose-Samples (`WithoutSamples`). Unbekannte Werte → `INVALID_ARGUMENT`. Skip der Budget-Projektion nicht an beliebige Strings koppeln.

### `includeReferences` ändert Anwendbarkeit nicht (`friction`)

- **Pfad:** `src\AiNetLinter\Mcp\Tools\AssemblyAnalysis\AssemblyAnalysisService.cs`; `src\AiNetLinter\Mcp\Tools\AssemblyAnalysis\AssemblyAnalysisContextFactory.cs`; `src\AiNetLinter\Mcp\Tools\AssemblyAnalysis\FindAssemblyExtensionsTool.cs`
- **Symbol:** `ToExtensionDto` (`context.Receiver is null` → immer `not_decidable`); `FindConsumerReceiverAsync` (sucht nur in **Consumer-Solution**); MCP-Pfad `ExecuteLeaseAsync` setzt `Receiver` nie
- **Ansatz:** Ohne Consumer-Projekt bleibt `ReduceExtensionMethod` unentscheidbar — das ist korrekt. `receiverType` gegen `context.Compilation` (und bei `includeReferences` gegen Referenz-Compilations) per `Compilation.GetTypeByMetadataName` / `AssemblyAnalysisSymbolTraversal.GetAllTypes` auflösen, dann `IMethodSymbol.ReduceExtensionMethod(ITypeSymbol)`. Treffer dann `applicable`/`not_applicable`. Sessions allein dürfen `applicable` nicht suggerieren.

### Promised StructuredContent / Buckets unsichtbar (`degraded`)

- **Pfad:** `src\AiNetLinter\Mcp\Tools\AssemblyAnalysis\Responses\FindAssemblyExtensionsResponseBuilder.cs`; `src\AiNetLinter\Mcp\Tools\AssemblyAnalysis\AssemblyAnalysisModels.cs`
- **Symbol:** `FormatText` / `AppendExtensions`; `AssemblyExtensionDto` (`Parameters`, `Applicability`, kein `Id`)
- **Ansatz:** DTO hat `Parameters` und `Applicability`, Markdown ist eine flache Liste. Gruppieren nach `applicable` / `not_applicable` / `not_decidable` (leere Buckets explizit). Parameter (`this` = `Parameters[0]`, Name, Typ) in die Signaturzeile. `GetDocumentationCommentId()` als `Id` analog `StableId` bei Inspect.

### `extensionName` ist undokumentierter Substring (`friction`)

- **Pfad:** `src\AiNetLinter\Mcp\Tools\AssemblyAnalysis\AssemblyAnalysisService.cs`; `src\AiNetLinter\Mcp\Registration\AssemblyAnalysisToolRegistrations.cs`
- **Symbol:** `Matches` (`string.Contains`, OrdinalIgnoreCase); `FindExtensions`; `FindAssemblyExtensionsDescription`
- **Ansatz:** Prosa „Substring“ oder Exact-Flag. Filter bleibt `IMethodSymbol.Name` — kein Regex nötig.

### `receiverType` macht Anwendbarkeit nicht decidable; Ausgabe ohne FQN (`wish`)

- **Pfad:** `src\AiNetLinter\Mcp\Tools\AssemblyAnalysis\AssemblyAnalysisService.cs`
- **Symbol:** `MatchesReceiverType` (Kurzname = `ITypeSymbol.Name`, FQN = `CSharpErrorMessageFormat`); `ToExtensionDto` (`Parameters[0].Type.ToDisplayString`)
- **Ansatz:** Kurzname-Gleichheit beibehalten. In der Zeile `FullyQualifiedFormat` für den `this`-Parameter. Anwendbarkeit siehe `ReduceExtensionMethod` oben — `receiverType` allein bleibt Filter, nicht Consumer.

### Diagnose-Lärm CS0234 (`friction`)

- **Pfad:** `src\AiNetLinter\Mcp\Tools\AssemblyAnalysis\Responses\FindAssemblyExtensionsResponseBuilder.cs`; `src\AiNetLinter\Mcp\Tools\AssemblyAnalysis\AssemblyAnalysisResponseLimits.cs`
- **Symbol:** `CreatePayload` → `ProjectDiagnostics`; `AppendDiagnostics`
- **Ansatz:** Default 1 Sample; `detailLevel=full` bis 20. Fehlende extern-/BCL-Refs sind Session-`completeness=partial`, keine Extension-Treffer.
