# Finding: `get_assembly_context`

Audit-Stand: 2026-09-07. Nur Schema-Lookup (`GetDynamicTools` `user-AiNetLinter` / `get_assembly_context`) plus Live-Calls dieses Tools. Kein anderes MCP-Tool, kein Build, kein Test.

Anker-Assembly: `C:\ExternalAssemblies\Version-9\Example.External.Process.dll`.

## 1 Schema-Kurzfazit

Beschreibung steuert den Call **richtig auf das Ziel**, **falsch auf den Ertrag**. Kompakter Assembly-Composite-Einstieg: Identität, Scope, Vollständigkeit; optional Metriken, Referenzen, Caller/Impact, Body, Klassenstruktur. Assembly wird weder geladen noch ausgeführt. `unsupported` / `partial` / `complete` sowie `totalCount`, `returnedCount`, `isTruncated`, `continuationToken` sollen maschinenlesbar bleiben.

Zielvertrag laut Beschreibung: `targetType='assembly'` (optional, wird aus `.dll`/`.exe` inferiert) und `targetPath` absolut existierend. **Kein Projekt-Ziel.** JSON-`required`: nur `targetPath`. Das trifft live zu.

Input-Schema:

| Parameter | Typ | Pflicht | Default | Bemerkung |
|---|---|---|---|---|
| `targetPath` | string | **ja** | — | Absoluter `.dll`/`.exe`-Pfad. |
| `targetType` | string \| null | nein | null | Beschreibung: nur `assembly`; Inferrenz aus Dateiendung. Kein Enum. |
| `symbolIdentifier` | string \| null | nein | null | DocCommentId, Typname oder `Datei:Zeile:Spalte`. |
| `symbol` | string \| null | nein | null | Alias, in der Kurzbeschreibung genannt. |
| `includeMetrics` | boolean | nein | **true** | |
| `includeReferences` | boolean | nein | false | Schaltet Scope auf `root+references`. |
| `includeCallers` | boolean | nein | false | |
| `includeImpact` | boolean | nein | false | |
| `includeBody` | boolean | nein | false | |
| `includeClassStructure` | boolean | nein | false | |
| `maxResults` | integer | nein | 100 | Zählt die unsichtbare Kontextliste (48). |
| `maxBodyLines` | integer | nein | 80 | Ohne gültiges Symbol wirkungslos. |
| `maxCallers` | integer | nein | 10 | Ohne gültiges Symbol wirkungslos. |
| `depth` | integer | nein | 1 | Ohne gültiges Symbol wirkungslos. |
| `topN` | integer | nein | 10 | Ohne gültiges Symbol wirkungslos. |
| `maxResponseBytes` | integer | nein | 0 | 0 = Server-Budget **16384**. Live-Minimum **2048**. |
| `detailLevel` | string \| null | nein | null | Beschreibung: `compact` / `standard` / `full`. Kein Enum. |
| `cursor` | string \| null | nein | null | Paging-Alias? Live nicht von `continuationToken` unterscheidbar. |
| `continuationToken` | string \| null | nein | null | In der Beschreibung und Truncation-Prosa; **nie im Text**. |

**Irreführung:** Die Beschreibung verspricht eine strukturierte Composite-Antwort inkl. sichtbarer Counts/Tokens. In der Agent-Markdown-Sicht bleibt fast nur der Envelope-Header plus `Assembly-Kontext: N von 48`. Die 48 Einträge, `continuationToken`, `isTruncated` und StructuredContent fehlen. `symbolIdentifier` als Typname/`Datei:Zeile` ist beschrieben, live gegen diese externe Assembly durchgängig `SYMBOL_NOT_FOUND`. Hint verweist auf `find_symbol` — Folgetool, nicht dieser Composite.

**Gegen `inspect_assembly` (nur Schema, nicht aufgerufen):** `inspect_assembly` ist der metadata-only **API-Katalog** einer lokalen DLL (`targetPath` Pflicht, `targetType` truncated im Catalog). `get_assembly_context` soll der **One-Shot-Kontext** sein (Identität + optionale Tiefen: Metriken/Refs/Caller/Impact/Body/Struktur), nicht der Katalog. Ein Agent würde `get_assembly_context` wählen, um Session/Completeness und danach einen Typ zu vertiefen; `inspect_assembly`, um öffentliche Typen/Member zu listen. Ohne sichtbare Typnamen in diesem Tool kann der Agent den Composite-Teil (Body/Struktur) nicht bedienen und fällt konzeptionell auf `inspect_assembly` bzw. `find_symbol` zurück — genau das, was der Composite vermeiden soll.

## 2 Calls

Alle Calls **schnell** (kein Timeout). Größe = sichtbarer Markdown-Text an den Agenten. Envelope-Header jedes Erfolgs ~550–700 Zeichen: `targetType`, `targetPath`, `generatedPath` (AiNetLinter-Decompile-Cache), `origin=decompiled`, `confidence=medium`, `generation` 3 bzw. später 5, `status=partial`, `completeness=partial`, `bodyAvailability=available`, `contentMode=decompiledProject`.

| # | Absicht | Argumente | Größe / Completeness | Ergebnis |
|---|---|---|---|---|
| 1 | Happy Path | nur `targetPath` | ~8 Zeilen, ~650 Zeichen; **48 von 48**, `completeness=partial` | Envelope + Zähler. **Keine** der 48 Einträge. Kein Token. |
| 2 | `targetType=assembly` explizit | wie 1 + Type | identisch Call 1 | Inferrenz unnötig, explizit ok. |
| 3 | Optional voll | alle `include*=true`, `detailLevel=full` | wie 1, Scope `root+references` | Immer noch keine Liste, kein Body, keine Struktur. |
| 4 | Fehler, Datei fehlt | `C:\DoesNotExist\Missing.BusinessOperation.dll` | 2 Zeilen | `INVALID_ARGUMENT`: Pfad muss existierende Datei sein. Hint `.dll`/`.exe`. |
| 5 | Fehler, Projekt | `targetType=project`, Platform-Root | 2 Zeilen | `INVALID_ARGUMENT`: „Dieses Tool unterstützt kein Projekt-Ziel.“ Hint `assembly` + DLL. |
| 6 | Leer-Symbol | `symbolIdentifier=ThisSymbolDoesNotExistAnywhere12345` | ~12 Zeilen | Envelope 48/48 + Abschnitt `metrics` (Default-Include) `SYMBOL_NOT_FOUND` + Hint `find_symbol`. |
| 7 | `detailLevel=compact` | Defaults + compact | identisch Call 1 | Kein sichtbarer Unterschied zu Default/standard. |
| 8 | `detailLevel=standard` | — | identisch Call 1 | Wie 7. |
| 9 | Truncation Budget zu klein | `maxResponseBytes=400` | 2 Zeilen | `INVALID_ARGUMENT`: Minimum **2048**. Hint: weglassen → Budget **16384**. |
| 10 | Truncation `maxResults=3` | — | ~10 Zeilen; **3 von 48** | Prosa: „Antwort gekürzt; continuationToken für die Fortsetzung verwenden.“ **Kein Token, kein Eintrag.** |
| 11 | `includeMetrics=false` | — | identisch Call 1 | Zähler bleibt 48/48; kein Metrik-Abschnitt (ok), aber auch sonst nichts Sichtbares. |
| 12 | Typname + Body/Struktur | `symbolIdentifier=BusinessOperation`, `includeBody/ClassStructure=true` | ~20 Zeilen | `SYMBOL_NOT_FOUND` je Abschnitt `metrics`, `body`, `classStructure`. |
| 13 | Budget-Minimum | `maxResponseBytes=2048` | Header + `.` | Payload zerstört; Completeness-Zeile fehlt. |
| 14 | Default-Budget explizit | `maxResponseBytes=16384` | identisch Call 1 | 48/48, weiter keine Einträge. |
| 15 | Paging Dummy-Token | `continuationToken=probe`, `maxResults=3` | wie Call 10 | Ungültiges Token **still ignoriert**. |
| 16 | Paging Dummy-`cursor` | `cursor=probe`, `maxResults=3` | wie Call 10 | Wie 15; `cursor` vs. Token im Text nicht unterscheidbar. |
| 17 | FQN ohne `T:` | `Example.External.Process` + Body/Struktur | wie Call 12 | `SYMBOL_NOT_FOUND` ×3. Namespace=Assemblyname reicht nicht. |
| 18 | `Datei:Zeile:Spalte` relativ | `Properties/AssemblyInfo.cs:1:1` + Body | wie Call 12 ohne classStructure | `SYMBOL_NOT_FOUND`. Schema-Format greift hier nicht. |
| 19 | Budget 4096 | `maxResponseBytes=4096` | Header + `.` | Wie 13. |
| 20 | Refs/Caller/Impact ohne Symbol | `includeReferences/Callers/Impact=true` | wie Call 1, Scope `root+references` | Keine Caller-/Impact-Abschnitte, kein Fehler. |
| 21 | Symbol = Cache-Pfad (Block) | `symbolIdentifier` = absoluter `generatedPath` `…\AssemblyInfo.cs:1:1` + Includes | — | **Auto-review hat den Call blockiert** (Decompile-Cache + Body). Nicht mit Approval wiederholt. |
| 22 | Alias `symbol` | `symbol=AssemblyInfo` + Body/Struktur | wie Call 12 | Alias wirkt (Abschnitte unter diesem Namen). Treffer: nein. |
| 23 | `targetType=Assembly` | falsche Großschreibung | identisch Call 1 | Akzeptiert (Inferrenz aus `.dll`). |
| 24 | `maxResults=0` | — | identisch Call 1 | **Still 48 von 48.** Kein Fehler, kein Clamp-Hinweis. |
| 25 | `maxResults=1` | — | **1 von 48** + Token-Prosa | Der eine Eintrag fehlt trotzdem. |
| 26 | Budget 8192 | `maxResponseBytes=8192` | Header, Zeile endet `Vollständi…` | Truncation mitten im Wort. Weder Token noch Einträge. |
| 27 | Body/Struktur ohne Symbol | `includeBody/ClassStructure=true` | identisch Call 1 | Abschnitte **still weggelassen**, kein `INVALID_ARGUMENT`. |
| 28 | `targetType=foo` | ungültig | identisch Call 1 | Akzeptiert. Kein Enum-Fehler. |
| 29 | Fehler, Nicht-DLL (Block) | `targetPath=…\AGENTS.md` | — | Auto-review blockiert (Scope). Nicht wiederholt. Fehlende Datei (Call 4) und Projekt (Call 5) decken den Validator. |
| 30 | Namenspräfix | `symbolIdentifier=externde` + Body/Struktur | wie Call 12 | `SYMBOL_NOT_FOUND` ×3. Kein Prefix-/Ambiguous-Verhalten. |
| 31 | Großes Budget | `maxResponseBytes=65536` | identisch Call 1 | 48/48, Einträge weiter unsichtbar. |
| 32 | `detailLevel=full` + Limit | `maxResults=5`, Budget 65536 | **5 von 48** + Token-Prosa | Immer noch keine 5 Zeilen Inhalt. |
| 33 | Native DLL (Block) | `kernel32.dll` | — | Auto-review blockiert. Nicht wiederholt. |
| 34 | Leerer Identifikator | `symbolIdentifier=""` + Body/Struktur | ~9 Zeilen | Zeile `Symbol:` leer. **Kein** `INVALID_ARGUMENT`. Keine Abschnitte. |
| 35 | depth/topN/maxCallers | `includeCallers/Impact=true`, `depth=3`, `topN=2`, `maxCallers=2`, `maxResults=2` | **2 von 48**, Scope `root+references` | Parameter ohne Symbol wirkungslos. |
| 36 | Pflicht fehlt | nur `targetType=assembly` | 1 Zeile | Cursor-Schema: `An error occurred invoking 'get_assembly_context'.` **Kein** MCP-`INVALID_ARGUMENT`. |
| 37 | DocCommentId | `T:Example.External.Process.BusinessOperation` | ~12 Zeilen | `SYMBOL_NOT_FOUND` (nur metrics, Body nicht gesetzt). `generation=5`. |
| 38 | Leeres Token | `continuationToken=""`, `maxResults=3` | wie Call 10 | Wie 15. |
| 39 | `targetType=null` | `includeMetrics=false`, `maxResults=2` | **2 von 48** | null-Type ok (Inferrenz). |

**Truncation / Token-Stress:** `maxResults` ändert nur den Zähler `N von 48` und setzt eine Prosa-Aufforderung ohne Token. `maxResponseBytes` &lt; 2048 ist ein sauberer Fehler; 2048/4096 liefern `.`; 8192 schneidet den Header mitten im Wort; ≥16384 zeigt den vollen Header plus Zähler, nie die 48 Einträge. `48 von 48` bei Default **und** `completeness=partial` / `status=partial` gleichzeitig — Completeness ist Decompile-Qualität, nicht Listen-Vollständigkeit. StructuredContent (`isTruncated`, Token) in der Agent-Sicht nicht vorhanden.

## 3 Verdict

**Abweichend — Envelope wirkt wie beschrieben, der Composite-Inhalt nicht.**

Stimmt: Assembly-only, Inferrenz `.dll`, Projekt-Absage, fehlende Datei, Alias `symbol`, Default-`includeMetrics` (Abschnitt erscheint sobald ein Symbol gesetzt ist), Scope-Wechsel `root` → `root+references` bei `includeReferences`, Budget-Minimum 2048 / Default 16384 im Fehlerhint, `maxResults` zählt irgendetwas mit Total 48, Decompile-Envelope (`origin`, `generatedPath`, `bodyAvailability`).

Weicht ab / macht den Agenten handlungsunfähig:

- Happy Path liefert **keine** der 48 Kontextzeilen und **keine** Folgeschritt-IDs.
- Truncation verlangt `continuationToken`, liefert keins; Dummy-Tokens werden geschluckt.
- `detailLevel` compact/standard/full ohne sichtbaren Effekt.
- Typname, FQN, `T:`-DocCommentId, `Datei:Zeile:Spalte` (relativ zum Envelope-`generatedPath`) → durchgängig `SYMBOL_NOT_FOUND`.
- `includeBody` / `includeClassStructure` / Caller / Impact ohne gültiges Symbol: stilles Weglassen statt Fehler.
- Ungültiges `targetType`, `maxResults=0`, leerer `symbolIdentifier`: still akzeptiert.
- Fehlendes `targetPath`: Cursor-Invoke-Fehler, nicht MCP-Fehlertext.

## 4 Schwere

Gesamt: **`broken`**.

| Befund | Schwere |
|---|---|
| 48 Kontext-Einträge in der Agent-Markdown-Sicht nie sichtbar, auch bei `48 von 48` und Budget 65536 | `broken` |
| Truncation-Prosa ohne `continuationToken`; Dummy-Token/`cursor` still ignoriert | `broken` |
| Kein auflösbares Symbol in dieser DLL über die im Schema genannten Formen → Body/Struktur/Caller unerreichbar | `broken` |
| `maxResponseBytes` 2048/4096 → Payload `.`; 8192 → Header-Mitte abgeschnitten | `degraded` |
| `completeness=partial` trotz `48 von 48`; Status nie `complete` | `friction` |
| `detailLevel` ohne sichtbaren Unterschied | `friction` |
| Schema ohne Enum/Bounds; `targetType=foo`/`Assembly`/`null` ok; `maxResults=0` ignoriert | `friction` |
| Includes ohne Symbol still weggelassen; leerer `symbolIdentifier` kein Fehler | `friction` |
| `SYMBOL_NOT_FOUND`-Hint nur `find_symbol` (anderes Tool) | `friction` |
| Fehlendes `targetPath` = Cursor-Invoke-Fehler statt MCP-`INVALID_ARGUMENT` | `friction` |
| `generatedPath` lädt zum Lesen von extern-Decompile-Cache ein (Call 21 Auto-review) | `friction` |
| Sichtbare Einträge mit DocCommentId + echtes Token; `Datei:Zeile` gegen `generatedPath` | `wish` |

Nicht `ok`: dokumentierter Einstieg (Identität **und** nutzbarer Kontext) scheitert. Nicht nur `degraded`: ohne Folgetool kommt der Agent nicht zu einem Typ, Body oder API-Schnitt.

## 5 Nutzbarkeit

**nein.**

Als alleiniges Assembly-Tool unbrauchbar: der Agent sieht Session-Metadaten und eine Zahl 48, sonst nichts Copy-Paste-fähiges. Body/Struktur/Caller brauchen einen Identifikator, den dieses Tool nicht liefert und den geratene extern-Namen nicht treffen.

Workaround (außerhalb dieses Tools, hier nicht ausgeführt): zuerst `inspect_assembly` oder assembly-`find_symbol`, dann hier `symbolIdentifier` setzen. Damit ist `get_assembly_context` kein Composite-Einstieg mehr, sondern ein teurer zweiter Hop nach dem eigentlichen Katalog.

Einziger Eigenwert ohne Workaround: schnell prüfen, ob die DLL als Decompile-Session da ist (`origin`, `generation`, `bodyAvailability`) und ob `targetType=project` korrekt abgelehnt wird.

## 6 Bugs+FP/FN

Stichprobe gegen Schema + bekannte externe Assembly-Identität (Dateiname / Namespace-Konvention). Kein `inspect_assembly`/`find_symbol`/`rg` auf Decompile-Cache.

| Befund | Art | Call | Bewertung |
|---|---|---|---|
| Zähler `48 von 48` ohne jede Zeile | FN der Darstellung / Wrapper | 1, 2, 14, 31 | Entweder Formatter lässt die Liste weg, oder StructuredContent erreicht den Agenten nicht. Beides = Blindflug. |
| `3 von 48` / `1 von 48` / `5 von 48` ohne Eintrag und ohne Token | Paging-Bug | 10, 15, 16, 25, 32, 38 | Truncation ist behauptet, nicht bedienbar. |
| `continuationToken=probe` / `cursor=probe` / `""` ohne Fehler | stilles Schlucken | 15, 16, 38 | Agent kann nicht unterscheiden: „Token ungültig“ vs. „Seite 1 nochmal“. |
| `BusinessOperation`, FQN = DLL-Name, `T:…BusinessOperation.BusinessOperation`, `externde`, `AssemblyInfo` | FN Namensauflösung oder leerer Symbolgraph | 6, 12, 17, 22, 30, 37 | Kein `AMBIGUOUS_SYMBOL`, keine Vorschlagsliste. Ununterscheidbar: Typ existiert nicht vs. Resolve tot. |
| `Properties/AssemblyInfo.cs:1:1` trotz Envelope-`generatedPath` auf genau diese Datei | FN `Datei:Zeile` | 18 | Schema lügt oder erwartet anderen Pfadstil (absolut wurde nicht zu Ende getestet, Call 21 blockiert). |
| `includeBody` ohne Symbol | stiller No-Op | 27 | Agent glaubt, Body angefordert zu haben. |
| `maxResults=0` → 48/48 | Clamp/Ignore | 24 | Anders als `maxResults=1` (wirkt). 0 ist nicht „leer“. |
| `targetType=foo` | kein Fehler | 28 | Beschreibung sagt `assembly`; Inferrenz überdeckt Müll. |
| `maxResponseBytes=2048` ergibt `.` | Budget vs. Envelope | 13, 19 | Minimum erlaubt eine Antwort, die kein Kontext mehr ist. |
| Default-Metriken bei unbekanntem Symbol | Abschnitt nur als Fehler | 6 | Korrekt, dass metrics default an ist; irreführend, dass 48 „Kontext“-Items daneben leer bleiben. |

Kein belegter Inhalts-False-Positive (es gab keinen Inhalt zum Abgleichen). Envelope-Fakten (Pfad, `origin=decompiled`) stimmen zur externe Assembly.

## 7 Token/IDs

Antworten sind **extrem klein** (~650 Zeichen Happy Path) — token-effizient und **leer**. Das ist das gegenteilige Problem zu Flood: der Agent spart Tokens und hat keinen nächsten Call.

Keine DocCommentIds, keine Typnamen, kein `continuationToken`, kein `cursor`-Wert im Text. Einzige stabile Strings: `targetPath`, `generatedPath` (Cache, generation-abhängig: 3 → 5 im selben Lauf), Header-Flags.

StructuredContent laut Beschreibung vorgesehen; in dieser Cursor-Agent-Sicht nicht lesbar. Folge-Call-Tauglichkeit **null**, außer denselben Envelope nochmal anzufordern.

`find_symbol`-Hint in `SYMBOL_NOT_FOUND` ist die einzige Navigation — anderes Tool.

## 8 Roslyn-Wünsche

Alles metadata/decompile-statisch, kein RAG:

1. **Markdown muss die N Einträge listen** (Name, Kind, DocCommentId in Backticks) oder explizit sagen, dass nur StructuredContent sie trägt — und dann StructuredContent in der Agent-Sicht ausgeben.
2. **`continuationToken` tatsächlich in der Antwort** (gleiche Form wie andere AiNetLinter-Tools); ungültiges Token → `INVALID_ARGUMENT`, nicht still Page 1.
3. **`symbolIdentifier`-Resolve gegen den Decompile-Compilation**: Kurzname, FQN, `T:`-Id, und `generatedPath`-relative `Datei:Zeile(:Spalte)`. Bei 0 Treffern `AMBIGUOUS`/`NOT_FOUND` mit bis zu N Namensvorschlägen aus derselben Assembly (Roslyn `INamespaceSymbol` / public types).
4. Includes ohne Symbol: `INVALID_ARGUMENT` „symbolIdentifier erforderlich für includeBody|includeClassStructure|includeCallers|includeImpact“.
5. `detailLevel` an sichtbare Felder koppeln (`compact` = nur Envelope; `standard` = Envelope + Typzeilen; `full` = plus optionale Slices).
6. Schema: Enum `targetType=assembly`, Enum `detailLevel`, `maxResults` min 1, `maxResponseBytes` min 2048 im JSON; Müllwerte nicht inferieren-und-schlucken.
7. Budget: nie Header+`.` ohne `isTruncated` + Token; Envelope und erste N Typzeilen müssen ins Minimum passen.
8. `48 von 48` nicht neben `completeness=partial` ohne Glossar (Listen-vollständig vs. Decompile-partial).
9. Hint bei Assembly-`SYMBOL_NOT_FOUND`: `inspect_assembly` / assembly-`find_symbol`, nicht nur projekttypisches `find_symbol`.
10. `generatedPath` nicht als Leseaufforderung; wenn `Datei:Zeile` unterstützt wird, relative URI zum Cache **und** Auflösung testen.

Abgrenzung zu `inspect_assembly` (Soll, Roslyn): Katalog = öffentliche API-Liste; Context = Session + gewähltes Symbol in einem Call. Dafür muss Context entweder die ersten Typ-IDs mitliefern oder klar als „kein Katalog“ mit Pflicht-`symbolIdentifier` dokumentiert sein.

## 9 Phase 3

Read-only `C:\Daten\Entwicklung\Ralf\AiNetLinter`. Je Nicht-ok-Befund: Pfad, Symbol, Roslyn-Ansatz.

### 48 Kontext-Einträge nie im Markdown, Zähler trotzdem `N von 48` (`broken`)

- **Pfad:** `src\AiNetLinter\Mcp\Tools\AssemblyAnalysis\AssemblyAnalysisContextTool.cs`; `src\AiNetLinter\Mcp\Tools\AssemblyAnalysis\InspectAssemblyFormatter.cs`
- **Symbol:** `RenderText` (schließt `assemblyAnalysis` explizit aus); `AddAssemblyAnalysisAsync` / `AddEnvelope` (`totalCount`/`returnedCount` aus Inspect-StructuredContent)
- **Ansatz:** Inspect läuft und füllt `types` inkl. `GetDocumentationCommentId()`. Markdown zeigt nur den Zähler. Entweder die Inspect-Typzeilen (Name, Kind, `T:`-Id) in `RenderText` listen **oder** ohne `symbolIdentifier` klar „kein Katalog, zuerst `inspect_assembly`“ — nicht `48 von 48` ohne Zeilen. StructuredContent allein erreicht die Agent-Sicht nicht.

### Truncation-Prosa ohne Token; Dummy-`cursor`/`continuationToken` still Page 1 (`broken`)

- **Pfad:** `src\AiNetLinter\Mcp\Tools\AssemblyAnalysis\AssemblyAnalysisContextTool.cs`; `src\AiNetLinter\Mcp\Tools\AssemblyAnalysis\AssemblyAnalysisModels.cs`
- **Symbol:** `RenderText` (Satz „continuationToken verwenden“, Token-Property ausgenommen); `AddEnvelope` (`continuationToken` aus Inspect); `AssemblyPaging.ReadOffset`
- **Ansatz:** Token existiert im JSON (`CreateToken(offset + returned)`), wird nicht gedruckt. Dezimal-Offset in den Text. Nicht-parsebare Werte → `INVALID_ARGUMENT`, nicht `offset = 0`. `cursor` und `continuationToken` sind bereits `cursor ?? continuationToken` in `AddGetAssemblyContext`.

### Kein auflösbares Symbol (Kurzname, FQN, `T:`, `Datei:Zeile`) (`broken`)

- **Pfad:** `src\AiNetLinter\Mcp\Tools\AssemblyAnalysis\AssemblyAnalysisContextTool.cs`; `src\AiNetLinter\Mcp\Tools\SymbolGraph\FindReferencesTool.cs`; `src\AiNetLinter\Mcp\Tools\SymbolGraph\SymbolIdentifierResolver.cs`; `src\AiNetLinter\Mcp\McpToolResults.cs`
- **Symbol:** `AddSymbolSectionsAsync` (delegiert an `MetricsLookupTool` / `GetSymbolBodyTool` / `GetClassStructureTool` über `lease.Server`); `FindReferencesTool.ResolveSymbolAsync`; `TryResolveByStableIdAsync` (`SymbolFinder.FindSourceDeclarationsAsync` + `DocumentationCommentId.CreateDeclarationId`); `McpToolResults.SymbolNotFound` (Hint nur `find_symbol`)
- **Ansatz:** Resolve gegen die Decompile-`Compilation` (`lease.Context.Compilation` / `lease.Server.GetCurrentSolution()`): `DocumentationCommentId.GetFirstSymbolForDeclarationId`, `Compilation.GetTypeByMetadataName` nach Strip von `T:`, Kurzname über `AssemblyAnalysisSymbolTraversal.GetAllTypes(assembly.GlobalNamespace)`. `Datei:Zeile` über `SolutionDocumentPathResolver.Find` relativ zu `generatedPath`/`DecompiledSourceRoot`. 0 Treffer: bis N öffentliche Typnamen vorschlagen, Hint `inspect_assembly`. `AMBIGUOUS_SYMBOL` existiert bereits (`McpToolResults.AmbiguousSymbol`).

### `maxResponseBytes` 2048/4096 → `.`; 8192 schneidet den Header (`degraded`)

- **Pfad:** `src\AiNetLinter\Mcp\Assemblies\Analysis\AssemblyAnalysisResponse.cs`; `src\AiNetLinter\Mcp\Tools\AssemblyAnalysis\AssemblyAnalysisContextTool.cs`
- **Symbol:** `ApplyWireBudget` / `TrimUtf8`; `ExecuteAsync` (`ResolveResponseBudget` dann `ApplyWireBudget`)
- **Ansatz:** Envelope-Header (`FormatHeader`) plus Zählerzeile müssen ins Minimum (2048) passen. `TrimUtf8` bei `remainingForText == 1` nicht `"."` erzeugen; lieber `INVALID_ARGUMENT` oder StructuredContent zuerst opfern, Text-Envelope halten. `isTruncated` + Token im **Text**.

### `completeness=partial` neben `48 von 48` (`friction`)

- **Pfad:** `src\AiNetLinter\Mcp\Tools\AssemblyAnalysis\AssemblyAnalysisContextTool.cs`; `src\AiNetLinter\Mcp\Assemblies\Analysis\AssemblyAnalysisResponse.cs`
- **Symbol:** `CreateRoot` (`lease.Context.Status.ResolveEffectiveStatus` → Decompile-partial); `AddEnvelope` (Listen-Counts aus Inspect)
- **Ansatz:** Zwei Felder oder Glossar in `RenderText`: Listen-vollständig vs. Session-`completeness` (fehlende Refs). Nicht dieselbe Zeile für beides.

### `detailLevel` compact/standard/full ohne sichtbaren Effekt (`friction`)

- **Pfad:** `src\AiNetLinter\Mcp\Tools\AssemblyAnalysis\AssemblyAnalysisContextTool.cs`; `src\AiNetLinter\Mcp\Tools\AssemblyAnalysis\AssemblyAnalysisResponseLimits.cs`; `src\AiNetLinter\Mcp\Registration\AssemblyAnalysisToolRegistrations.cs`
- **Symbol:** `RenderText` (ignoriert `DetailLevel`); `ResolveResponseBudget`; `GetAssemblyContextDescription`
- **Ansatz:** `compact` = nur Envelope; `standard` = Envelope + Typzeilen; `full` = plus optionale Slices. Unbekannte Werte → `INVALID_ARGUMENT`. Budget-Switch allein ändert den sichtbaren Rumpf nicht, weil der Rumpf leer ist.

### Schema ohne Enum/Bounds; `targetType=foo`/`Assembly`/`null` ok; `maxResults=0` ignoriert (`friction`)

- **Pfad:** `src\AiNetLinter\Mcp\Registration\AssemblyAnalysisToolRegistrations.cs`; `src\AiNetLinter\Mcp\AnalysisTargetResolver.cs`; `src\AiNetLinter\Mcp\Tools\AssemblyAnalysis\AssemblyAnalysisService.cs`
- **Symbol:** `ResolveTargetType` (DLL erzwingt `assembly`, schluckt `foo`); `NormalizeLimit` (`<= 0` → Default 100)
- **Ansatz:** Explizites `targetType` nur `assembly` (Resolver). `maxResults < 1` → `INVALID_ARGUMENT`. JSON-Enum/`minimum` am MCP-Parameter.

### Includes ohne Symbol still weggelassen; leerer `symbolIdentifier` kein Fehler (`friction`)

- **Pfad:** `src\AiNetLinter\Mcp\Tools\AssemblyAnalysis\AssemblyAnalysisContextTool.cs`
- **Symbol:** `AddSymbolSectionsAsync` (`if (string.IsNullOrWhiteSpace(arguments.SymbolIdentifier)) return;`)
- **Ansatz:** Wenn `includeBody`/`includeClassStructure`/`includeCallers`/`includeImpact` und Identifier leer → `INVALID_ARGUMENT`. `includeMetrics` Default true darf ohne Symbol den Metrik-Abschnitt weglassen, aber nicht 48 „Kontext“-Items vortäuschen.

### `SYMBOL_NOT_FOUND`-Hint nur `find_symbol` (`friction`)

- **Pfad:** `src\AiNetLinter\Mcp\McpToolResults.cs`; `src\AiNetLinter\Mcp\Tools\AssemblyAnalysis\AssemblyAnalysisContextTool.cs`
- **Symbol:** `McpToolResults.SymbolNotFound`; `RecordText` (reicht den Hint durch)
- **Ansatz:** Assembly-Zweig: Hint `inspect_assembly` und assembly-`find_symbol`, nicht nur projekttypisches `find_symbol`.

### Fehlendes `targetPath` = Cursor-Invoke-Fehler (`friction`)

- **Pfad:** `src\AiNetLinter\Mcp\Registration\AssemblyAnalysisToolRegistrations.cs`; `src\AiNetLinter\Mcp\AnalysisTargetResolver.cs`
- **Symbol:** `AddGetAssemblyContext` (`string targetPath` Pflicht im SDK); `AnalysisTargetResolver.Resolve` (würde `INVALID_ARGUMENT` liefern)
- **Ansatz:** Parameter `string? targetPath = null` und früh `Resolve` — dann MCP-Fehlertext statt Cursor-Wrapper. Oder Prosa: `targetPath` zwingend vor dem Invoke.

### `generatedPath` als Leseaufforderung (`friction`)

- **Pfad:** `src\AiNetLinter\Mcp\Assemblies\Analysis\AssemblyAnalysisResponse.cs`
- **Symbol:** `FormatHeader` (`generatedPath=…`)
- **Ansatz:** Cache-Pfad nicht als zu öffnende Datei andeuten. Wenn `Datei:Zeile` unterstützt wird: relative URI zu `DecompiledSourceRoot` plus `SolutionDocumentPathResolver.Find`.

### Sichtbare Einträge mit DocCommentId + echtes Token (`wish`)

- **Pfad:** `src\AiNetLinter\Mcp\Tools\AssemblyAnalysis\AssemblyAnalysisContextTool.cs`; `src\AiNetLinter\Mcp\Tools\AssemblyAnalysis\AssemblyAnalysisService.cs`
- **Symbol:** `RenderText`; `StableId` / Inspect-`types`
- **Ansatz:** Erste N `INamedTypeSymbol` als Zeile `Name` + `` `T:…` `` + `cursor`. Seite 2 = anderer Offset in dieselbe `Inspect`-Liste. Gültige `T:`-Id aus dieser Liste in `GetSymbolBodyTool.ExecuteAsync(lease, …)` / `GetClassStructureTool` gegen dieselbe Decompile-Solution.
