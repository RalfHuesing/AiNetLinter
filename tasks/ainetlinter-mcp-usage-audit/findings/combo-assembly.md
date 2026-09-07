# Finding: Kombination `inspect_assembly` → `search_assembly` → `find_assembly_extensions` → `get_assembly_context`

Audit-Stand: 2026-09-07. Nur Schema-Lookup der vier Assembly-Tools plus Live-Kette. Kein Build, kein Test, keine Synthese anderer Findings.

Probe-DLL: `C:\ExternalAssemblies\Version-9\Example.External.Business.dll` (external package version 9.0, Version 9.0.0.0). Alle Calls mit `targetType=assembly` und absolutem `targetPath`.

## 1 Schema-Kurzfazit

Die vier Beschreibungen steuern **einzeln** den Einstieg (absoluter `.dll`-Pfad, `targetType` optional/`assembly`, metadata-only, nicht laden/ausführen). Als **Kette** führen sie in die Irre:

- `inspect_assembly` liefert im Agent-Text **keine** DocComment-IDs und **keine** `assembly:`-IDs, nur FQNs und Signatur-Strings. Die Folge-Tools sollen laut Schema aber `symbolIdentifier` (DocCommentId / Typname / Datei:Zeile:Spalte) bzw. Text-`pattern` nehmen.
- `search_assembly` ist **Textsuche im Dekompilat**, nicht Symbolsuche. Ein 1:1-Weiterreichen des Inspect-FQN (`Example.External.Business.Record`) mit `kind=type` + `declarationOnly` ergibt **0 Treffer**. Die Prosa verspricht „stabile IDs“ in `StructuredContent.assemblySearch` — im sichtbaren Text kommen nur relative Dateipfade mit Zeile.
- `find_assembly_extensions` akzeptiert den Inspect-FQN als `receiverType` ohne Fehler, liefert hier aber `0 von 0` — dasselbe wie ungefiltert. Schema trennt `applicable` / `not_applicable` / `not_decidable`; die Agent-Antwort zeigt diese Buckets nicht.
- `get_assembly_context` ist als „kompakter Composite-Einstieg“ beschrieben. Header `79 von 79` klingt nach **Katalogersatz** für `inspect_assembly`. Live: ohne Symbol fast leer; mit Symbol Tiefgang **eines** Typs. Den Typenkatalog ersetzt Context **nicht**.

`targetType=assembly` war in allen Calls eindeutig und wurde nicht geraten.

## 2 Calls

Reihenfolge **strikt sequentiell**. Alle Calls **schnell** (kein Timeout). Gemeinsamer Header: `origin=decompiled`, `completeness=partial`, `confidence=medium`, `bodyAvailability=available`, Cache unter `C:\Daten\Tools\AiNetLinter-win-x64\cache\asm.cursor\…`.

| # | Tool | Absicht / Argumente | Größe / Completeness | Ergebnis |
|---|---|---|---|---|
| 1 | `inspect_assembly` | Defaults, nur Target | ~2,5k Zeichen; **stark gekürzt** (`responseBudget`) | Identität 9.0.0.0, 1 Namespace. **1 von 79** Typen (`Record`), **20 von 469** Membern (12 Events, dann `Add*`). FQNs: `Example.External.Business.Record`, Return-Typ `RecordPositionCollection`. **Keine** `assembly:`-ID, kein DocCommentId. Referenzen 0/171. Diagnosen 1/22 (5761 CS0234). |
| 2 | `search_assembly` | `pattern` = Inspect-FQN, `kind=type`, `declarationOnly=true`, `maxResults=50` | ~0,8k; `completeness=complete` | **0 von 0**. FQN-Handoff aus Inspect scheitert als Textmuster. |
| 3 | `search_assembly` | `pattern=Record` (Kurzname aus Inspect), sonst wie 2 | ~4,5k; `truncated` | **50 von 55**. Typenkatalog der `Record*`-Klassen inkl. `RecordPositionCollection`. Trefferform `Example.External.Business/Record.cs:36`. `cursor=50`, `continuationToken=50`. Keine `assembly:`-IDs im Text. |
| 4 | `find_assembly_extensions` | `receiverType` = Inspect-FQN `…Record` | ~1,2k; `partial` | **0 von 0**. Receiver-Zeile wiederholt den FQN. Diagnosen 1/22 (CS0234). Keine `applicable`/`not_decidable`-Trennung. |
| 5 | `find_assembly_extensions` | ungefiltert, nur Target | ~1,1k; `partial` | **0 von 0**. Diese DLL hat keine Extension-Methoden — Call 4 war kein Handoff-Fehlschlag, sondern echte Leermenge. |
| 6 | `get_assembly_context` | `symbolIdentifier` = Inspect-FQN; `includeBody=true`, `includeClassStructure=true`, `includeMetrics=true` | **groß** (~8–12k sichtbar), mehrfach intern gekürzt | Header **79 von 79**, Scope `root`. Symbol greift. Erste stabile ID: `assembly:9C087FC61A0405AF4A7FFDD0AE87945AE325C5C14E240743D94EF6E8D71E9A07:13:T:Example.External.Business.Record`. Metriken (LOC 23862, Footprint 53399, 469 public). Body **80 von 28395** Zeilen (XML-Docs, private Nested-Structs). `classStructure` **100 von 1103** Membern, bricht bei `_zkds` ab. **Keine Liste der 79 Typen.** Hinweis „vollständig für den angefragten Scope“. |
| 7 | `get_assembly_context` | ohne Symbol, `includeMetrics=true` (Default) | ~0,6k; `partial` | Header **79 von 79**, sonst **leer**. Kein Typenkatalog, keine Metriken, keine IDs. |
| 8 | `get_assembly_context` | `symbolIdentifier` = Search-Pfad `Example.External.Business/Record.cs:36`; nur Metriken | ~2,0k | Search→Context-Handoff **funktioniert**. Löst auf `Record`, dieselbe `assembly:`-ID wie Call 6. Schema nennt `Datei:Zeile:Spalte`; Zeile ohne Spalte reicht. |

Weitergereicht aus Call 1: FQN `Example.External.Business.Record`, Kurzname `Record`, Member-FQNs (`AddRecordPositions` u. a., nicht extra gesucht), Return-Typ `RecordPositionCollection` (in Call 3 sichtbar). Stabile `assembly:`-ID entsteht erst in Context, nicht in Inspect/Search/Extensions.

## 3 Verdict

**Teilweise wie beschrieben.**

Die Kette ist technisch erreichbar (gleiche DLL, `targetType=assembly`, kein Absturz). Inspect-FQN → Context und Search-`Datei:Zeile` → Context funktionieren. Search mit **Kurzname** füllt den von Inspect abgeschnittenen Typenkatalog (50/55 `Record*`). Extensions sind für diese DLL eine dokumentierbare Leermenge.

Abweichungen der **Kombination**: Inspect-FQN → Search = False Negative; Inspect liefert keine IDs, die Search/Extensions/Context laut Schema erwarten; Context-Header `79 von 79` ersetzt den Inspect-Katalog nicht; ohne Symbol ist Context unbrauchbarer als Inspect-Default; Token und Truncation stapeln sich über die vier Tools; Search-Prosa „stabile IDs“ vs. sichtbare Pfad:Zeile; Extensions-Buckets fehlen im Text.

## 4 Schwere

**`degraded`**

Kein Absturz, keine falsche Assembly-Identität, Context versteht den Inspect-FQN. Für den dokumentierten Agenten-Zweck „externe API in einer DLL erkunden“ ist die Kette **riskant**: Default-Inspect zeigt 1/79 Typen; der naheliegende Folgecall (FQN in Search) ist leer; Context ohne Symbol ist leer trotz `79 von 79`; die erste wirklich stabile ID kommt erst im letzten Tool. Ein Agent, der den Composite als Katalogersatz nimmt, bleibt ohne Typenliste. Nicht `broken` (Workaround existiert: Kurzname in Search, FQN in Context). Nicht nur `friction` (Truncation + ID-Bruch verhindern das Ziel, nicht nur Schema-Härte).

## 5 Nutzbarkeit

**nur mit Workaround.**

Als Agent würde ich die vier Tools **nicht** in der beschriebenen Reihenfolge als Katalog→Suche→Extensions→Composite fahren. Brauchbares Muster für diese externe Assembly:

1. `inspect_assembly` nur für **Identität** (Name, Version) — nicht für den Typenkatalog.
2. `search_assembly` mit **Kurzname** (`Record`, `kind=type`, `declarationOnly`) für den Typenkatalog; FQN nicht als `pattern` verwenden. Pagination über `cursor=50`.
3. `find_assembly_extensions` einmal ungefiltert; bei `0 von 0` abbrechen — kein Receiver aus Inspect nachschieben.
4. `get_assembly_context` mit Inspect-FQN **oder** Search-`Datei:Zeile` für Metriken/Body/Struktur **eines** Typs. Die dort ausgegebene `assembly:`-ID merken. `includeBody`/`includeClassStructure` nur gezielt; Default ohne Symbol weglassen.

Context **ersetzt den Katalog nicht.** Inspect-Default **ersetzt ihn auch nicht** (1/79). Der Katalog in dieser Kette kommt aus **Search mit Kurzname**.

## 6 Bugs

Reproduzierbar an dieser DLL und dieser Call-Reihenfolge. Stichprobe gegen Inspect-Ausgabe und Dekompilat-Header (`public class Record`), kein Vollabgleich.

| Befund | Art | Call |
|---|---|---|
| Inspect-FQN als Search-`pattern` (`kind=type`, `declarationOnly`) → 0 Treffer, obwohl Inspect denselben Typ zeigt und Call 3 denselben Typ in `Record.cs:36` findet | False Negative / ID-Handoff | 2 vs. 1+3 |
| `inspect_assembly` gibt keine `assembly:`- oder DocComment-IDs aus; Context erzeugt sie erst nachträglich | ID-Bruch zwischen Katalog und Composite | 1 vs. 6 |
| Search-Prosa: stabile IDs in StructuredContent; Agent-Text nur `Pfad:Zeile` | Beschreibung vs. Folge-Tool | 3 |
| `get_assembly_context` ohne Symbol: Header `79 von 79`, Rumpf leer — wirkt wie vollständiger Katalog, ist keiner | False Completeness / Katalogersatz | 7 |
| Context mit Symbol: Header `79 von 79`, Inhalt nur `Record`; Hinweis „vollständig“ trotz Body 80/28395 und Structure 100/1103 | Irreführende Completeness | 6 |
| Inspect-Default 1/79 Typen, 20/469 Member — Search-Kurzname listet 50/55 verwandte Typen, Inspect nicht | Truncation macht den Einstieg unbrauchbar | 1 vs. 3 |
| Extensions-Antwort zeigt weder `applicable`/`not_applicable`/`not_decidable` noch explizit „keine Extensions in dieser Assembly“; `0 von 0` + CS0234-Diagnose wirkt wie Fehler | Leermenge vs. Fehler | 4, 5 |
| Schema Context: `Datei:Zeile:Spalte`; Search liefert `Datei:Zeile` ohne Spalte — zufällig akzeptiert | Schema-Lücke, hier kein Bruch | 8 |

Keine False Positives in der Stichprobe (Call 3 listet reale `Record*`-Typen; Call 6/8 lösen denselben Typ).

## 7 Token/IDs

**Token-Last der Kette:** Call 1+2+4+5+7 sind klein, aber **wenig Informationsgewinn** (Inspect-Schnipsel, zwei Leersuchen, leerer Context). Die nutzbare Last sitzt in Call 3 (Katalog) und Call 6 (Tiefgang, größte Antwort, intern mehrfach gekürzt). Extensions und Context-ohne-Symbol verdoppeln Header-/Diagnose-Rauschen (CS0234) ohne Folgeschritt.

**Truncation stapelt sich:** Inspect `1/79` Typen + `20/469` Member → Search `50/55` → Context Body `80/28395` + Structure `100/1103`. Kein Tool gibt die vom nächsten Tool benötigte ID ungekürzt weiter.

**Folge-Call-Tauglichkeit:**

| Quelle | Was kommt an | Taugt für |
|---|---|---|
| Inspect-FQN | `Example.External.Business.Record` | Context `symbolIdentifier` **ja**; Search `pattern` **nein**; Extensions `receiverType` formal ja, hier leer |
| Inspect-Kurzname | `Record` | Search **ja** (breit) |
| Inspect-Member-FQN | z. B. `Record.AddRecordPositions` | in dieser Kette nicht weitergereicht; kein DocCommentId |
| Search-Text | `…/Record.cs:36` | Context **ja** (ohne Spalte) |
| Context | `assembly:9C08…:13:T:…Record` | entsteht zu spät für Search/Extensions; Rückgabe in Inspect fehlt |
| StructuredContent-IDs | in der Agent-Antwort **nicht sichtbar** | Folgecall muss Raten oder Text parsen |

## 8 Roslyn-Wünsche

Alles statisch/metadata-only, kein RAG:

- Dieselbe **stabile Symbol-ID** (`assembly:…` oder DocCommentId) in Inspect-, Search- und Context-Text ausgeben, nicht nur intern im StructuredContent.
- `search_assembly` zusätzlich **symbolisch** (MetadataName / FQN / DocCommentId), nicht nur Substring im Dekompilat — sonst bleibt Inspect→Search ein Medienbruch.
- `get_assembly_context` ohne Symbol: entweder den **Typenkatalog** (Namen + IDs, budgetiert) liefern oder Header `79 von 79` nicht als Vollständigkeit der Liste zeigen.
- Context-Hinweis „kein Read/Grep nötig“ unterdrücken, wenn Body/Structure gekürzt sind.
- `find_assembly_extensions`: bei 0 Treffern maschinenlesbar `none_in_assembly` vs. `receiver_unresolved` vs. `not_decidable`; Diagnose-CS0234 nicht als Ersatz für die Leermenge.
- Inspect-Default: ersten öffentlichen Typ **oder** eine ID-Liste der 79 Typen, nicht 20 Event-Zeilen von `Record`.

## 9 Phase 3

AiNetLinter read-only `C:\Daten\Entwicklung\Ralf\AiNetLinter`. Je Nicht-ok-Kettenbefund: Pfad + Symbol + Roslyn-Ansatz.

### Inspect-FQN als Search-`pattern` → 0 Treffer (Call 2 vs. 1+3)

- **Pfad:** `src\AiNetLinter\Mcp\Tools\AssemblyAnalysis\AssemblySearchTool.cs`; `src\AiNetLinter\Mcp\Tools\AssemblyAnalysis\AssemblySearchDeclarationFilter.cs`; `src\AiNetLinter\Mcp\Registration\AssemblyAnalysisToolRegistrations.cs`
- **Symbol:** `AssemblySearchTool.Scan` / `ScanLines` (Regex/Substring auf Dekompilat-Zeilen); `AssemblySearchDeclarationFilter.ResolveTypeHeader` (`BaseTypeDeclarationSyntax.Identifier` = Kurzname `Record`, nicht FQN)
- **Ansatz:** Zweiter Dispatch neben Textsuche: wenn `kind=type` und das Pattern ein FQN/`T:`/`DocumentationCommentId` ist, `INamedTypeSymbol` über die Session-Compilation auflösen (`compilation.GetTypeByMetadataName` / `DocumentationCommentId.GetFirstSymbolForDeclarationId`) und Deklarationslocation ausgeben. Textpfad unverändert für Kurzname `Record`. Kein RAG.

### Inspect/Search ohne `assembly:`- bzw. DocComment-IDs im Agent-Text (Call 1, 3 vs. 6)

- **Pfad:** `src\AiNetLinter\Mcp\Tools\AssemblyAnalysis\InspectAssemblyFormatter.cs` (`AppendType` nur `` `Namespace.Name` ``); `src\AiNetLinter\Mcp\Tools\AssemblyAnalysis\AssemblyAnalysisService.cs` (`ToTypeDto` / `StableId` = `ISymbol.GetDocumentationCommentId()`, Feld `AssemblyTypeDto.Id` existiert, Formatter ignoriert es); `AssemblySearchTool.CreateMatch` (SHA256 über `pfad\\nzeile\\npattern`, 16 Hex — **keine** Symbol-ID); `AssemblySearchTool.RenderText` (`FilePath:Line: LineText`); `AssemblyAnalysisToolRegistrations.SearchAssemblyDescription` („stabile IDs“ in StructuredContent)
- **Symbol:** `AssemblyAnalysisService.StableId`; `AnalysisSymbolIdentity.Format`; `FindSymbolTool.FormatEntry`
- **Ansatz:** Im Inspect- und Search-Markdown dieselbe `assembly:{sha}:{gen}:{T:…}`-Zeile wie `GetSymbolBodyTool.RenderResolvedSymbol`. Search-`Id` von Content-Hash auf DocCommentId umstellen, sobald `declarationOnly`/`kind=type` ein `ISymbol` hat; sonst `Datei:Zeile` plus nacktes `T:` aus dem Deklarationsheader (`INamedTypeSymbol` am Token). Description nicht „stabile IDs“ versprechen, solange nur StructuredContent sie trägt.

### Inspect-Default 1/79 Typen, Events zuerst (Call 1 vs. 3)

- **Pfad:** `src\AiNetLinter\Mcp\Tools\AssemblyAnalysis\Responses\InspectAssemblyResponseBuilder.cs`; `src\AiNetLinter\Mcp\Tools\AssemblyAnalysis\AssemblyAnalysisResponseLimits.Budget.cs`; `src\AiNetLinter\Mcp\Tools\AssemblyAnalysis\AssemblyAnalysisService.cs`
- **Symbol:** `InspectAssemblyResponseBuilder.ApplyResponseBudget` → `ProjectResponseBudget` / `TryTrimInspect` (`TryRemoveLastMember`, `TryRemoveLastType`); `ToTypeDto` sortiert Member `Kind` dann `Signature` (Events vor Methoden); `DefaultMaxResults`/`DefaultMaxMembers` = 100, Wire-Budget schneidet härter
- **Ansatz:** Budget zuerst Member-Listen kürzen, Typ**namen**+`Id` der 79 Typen behalten (Katalogzeile ohne Member). Member-Sort: öffentliche Methoden/Typen vor Events. ContinuationToken für Typ-Offset belassen.

### Context ohne Symbol: Header `79 von 79`, Rumpf leer (Call 7)

- **Pfad:** `src\AiNetLinter\Mcp\Tools\AssemblyAnalysis\AssemblyAnalysisContextTool.cs`
- **Symbol:** `AssemblyAnalysisContextTool.AddSymbolSectionsAsync` (Early-Return ohne `symbolIdentifier`); `AddEnvelope` kopiert `totalCount`/`returnedCount` aus `assemblyAnalysis`; `RenderText` **schließt** `assemblyAnalysis` aus der sichtbaren Property-Schleife aus
- **Ansatz:** Ohne Symbol entweder `InspectAssemblyFormatter`-Typkatalog (budgetiert, mit IDs) in den Text rendern oder Header nicht als `79 von 79` der **Antwort** ausgeben (`totalCount=0`, Satz „kein Symbol, kein Katalog — inspect_assembly / search_assembly“). Composite-Beschreibung in `GetAssemblyContextDescription` („Katalogersatz“) anpassen.

### Context mit Symbol: Header `79 von 79` + „vollständig“ trotz Body/Structure-Truncation (Call 6)

- **Pfad:** `src\AiNetLinter\Mcp\Tools\AssemblyAnalysis\AssemblyAnalysisContextTool.cs`; `src\AiNetLinter\Mcp\Assemblies\Analysis\AssemblySessionStatusExtensions.cs`; `src\AiNetLinter\Mcp\McpSufficiencyHints.cs`; `src\AiNetLinter\Mcp\Tools\FileStructure\GetClassStructureTool.cs`
- **Symbol:** `CreateRoot` setzt `completeness` aus `lease.Context.Status.ResolveEffectiveStatus` (Session, nicht Treffer); `RenderText` nutzt Inspect-`totalTypes` als Kontext-Zähler; eingebettetes `GetClassStructureTool` hängt `McpSufficiencyHints.Append` auch bei Truncation an
- **Ansatz:** Session-`completeness` und Scope-Completeness trennen (`hitsComplete` für Body/Structure). Header `returnedCount` = sichtbare Abschnitte des **angefragten** Typs, nicht 79 API-Typen. Sufficiency-Satz unterdrücken, sobald Body `80/n` oder Structure `100/n`.

### Extensions `0 von 0` ohne Buckets, CS0234 wirkt wie Fehler (Call 4, 5)

- **Pfad:** `src\AiNetLinter\Mcp\Tools\AssemblyAnalysis\Responses\FindAssemblyExtensionsResponseBuilder.cs`; `src\AiNetLinter\Mcp\Tools\AssemblyAnalysis\AssemblyAnalysisService.cs`; `src\AiNetLinter\Mcp\Registration\AssemblyAnalysisToolRegistrations.cs`
- **Symbol:** `AssemblyAnalysisService.FindExtensions` (`IMethodSymbol.IsExtensionMethod`); `ToExtensionDto` setzt `applicable`/`not_applicable`/`not_decidable` **pro Treffer**; `FormatText.AppendExtensions` bei leerer Liste keine Bucket-Zeile; Diagnosen via `AssemblyAnalysisResponseLimits.AppendDiagnostics`
- **Ansatz:** Bei `TotalExtensions==0` maschinenlesbar `none_in_assembly` (Compilation hat keine Extension-Methoden) vs. `receiver_unresolved` vs. `not_decidable`. `IMethodSymbol.ReduceExtensionMethod` nur wenn Treffer existieren. Leermenge nicht hinter CS0234-Samples verstecken; Diagnosen-Block nach dem Status.

### Search-`Datei:Zeile` ohne Spalte vs. Schema `Datei:Zeile:Spalte` (Call 8)

- **Pfad:** `src\AiNetLinter\Mcp\Tools\AssemblyAnalysis\AssemblySearchTool.cs` (`RenderText`); `src\AiNetLinter\Mcp\Registration\AssemblyAnalysisToolRegistrations.cs` (`GetAssemblyContextDescription`); `src\AiNetLinter\Mcp\Tools\SymbolGraph\FindReferencesTool.cs` (`ResolveByLineAsync`)
- **Symbol:** `AssemblySearchMatch` hat `MatchRanges.Column`, Renderer druckt sie nicht
- **Ansatz:** Text `rel/Pfad.cs:36:1` aus Range-Spalte; Schema und Search-Ausgabe angleichen. Resolve ohne Spalte darf weiter funktionieren (`ResolveByLineAsync`).
