# Finding: `get_class_structure`

Datum: 2026-09-07. Nur Live-Calls dieses Tools plus Schema-Lookup (`GetDynamicTools` / `toolName=get_class_structure`). Kein Build, kein Test, kein anderes MCP-Tool.

Anker: `Sample.Project.Infrastructure.Sql.DataExecutor` (Projekt) und `[ComponentRegistration]` → `ComponentRegistrationAttribute`. Assembly: `Example.External.Process.Record` in `Example.External.Process.dll`.

---

## 1. Schema-Kurzfazit

Beschreibung steuert den Call **größtenteils richtig**: Tabellenübersicht der Member eines Typs, `symbolIdentifier` (Typname, `Datei.cs:Zeile:Spalte`, DocCommentId), `sortBy` (`lines` Default / `kind` / `name`), `kindFilter`, `nameFilter`, `maxMembers` (Default 50, Cap 200), `maxResponseBytes`, Cross-Target `project` | `assembly`.

Irreführend oder unvollständig:

- **`symbolIdentifier` ist faktisch Pflicht**, fehlt im JSON-`required` (nur `targetType`/`targetPath`). Sechs Aliase (`symbol`, `className`, `identifier`, `type`, `name`) stehen im Schema; die Beschreibung nennt nur `symbolIdentifier`. Kurzname wird als **beliebiges Symbol** aufgelöst, nicht nur als Typ — Properties gleichen Namens machen den Klassen-Anker mehrdeutig.
- **`sortBy: lines` (Default) sortiert nicht nach `LineCount`**, sondern nach Quellreihenfolge. Schema und Beschreibung lügen hier.
- **`maxResponseBytes` wird ignoriert** (400 lieferte die volle 33-Member-Tabelle).
- Beschreibung verspricht Truncation-Meta und `TotalMemberCount` vs. `ShownMemberCount` im structuredContent. Im Agenten-Text gibt es `N von M` plus eine Truncation-Zeile — aber **gleichzeitig** den Hinweis „Daten sind vollständig … kein Read/Grep nötig“, der bei Truncation falsch ist. StructuredContent war in der Agenten-Antwort nicht sichtbar.
- Cap 200 **ohne Cursor/Offset**: Typen mit >200 Membern (extern-`Record`: 316) sind in einem Call nie vollständig.

---

## 2. Ausgeführte Calls

Alle Calls schnell (kein Timeout). `targetPath` Projekt: `C:\Workspace\Sample.Project`. Assembly: `C:\ExternalAssemblies\Version-9\Example.External.Process.dll`.

### Happy Path (Projekt)

| # | Parameter | Ergebnis | Größe (grob) |
|---|-----------|----------|----------------|
| A | `symbolIdentifier=T:Sample.Project.Infrastructure.Sql.DataExecutor` | Klasse, 2 Partial-Dateien, **33 von 33**, Signaturen/Zeilen/Visibility | ~55 Zeilen, ~6–8k Zeichen |
| B | FQ-Name ohne `T:` | identisch zu A | wie A |
| C | `DataExecutor.cs:9:10` | identisch zu A | wie A |
| D | Aliase `symbol`, `identifier` mit `T:…` | identisch zu A | wie A |
| E | `name=SqlTransactionContextAdapter` | Nested Class, **4 von 4** | ~15 Zeilen |
| F | `symbolIdentifier=ComponentRegistrationAttribute` | Attribut-Klasse, **3 von 3** (Ctor + 2 Properties) | ~12 Zeilen |

### Happy Path (Assembly)

| # | Parameter | Ergebnis | Größe (grob) |
|---|-----------|----------|----------------|
| G | `T:Example.External.Process.Record` Defaults | Header `origin=decompiled; completeness=partial`, **50 von 316**, vor allem private Fields + Nested Types | ~60 Zeilen |
| H | Assembly-Session-ID `assembly:2AFC08…:3:T:Example.External.Process.Record`, `maxMembers=5` | **5 von 316**, gleiche Reihenfolge wie Default | ~15 Zeilen |
| I | `Record.cs:31:10` | wie G, **50 von 316** | wie G |
| J | `kindFilter=Property`, `maxMembers=5` | **5 von 95** Properties | ~15 Zeilen |
| K | `sortBy=name`, `maxMembers=5` | 2 Ctors + 3 Properties alphabetisch | ~15 Zeilen |
| L | `maxMembers=200`, `sortBy=kind` | **200 von 316**; Antwort im Transport **mitten im Wort abgeschnitten** (`privat…`) | sehr groß, >Token-Komfort |

### Optionale Parameter (Projekt)

| # | Parameter | Ergebnis |
|---|-----------|----------|
| M | `kindFilter=Method`, `nameFilter=Execute`, `sortBy=name` | **3 von 3**: zwei `ExecuteAsync` + `ExecuteMutationWithVersionCheckAsync` |
| N | `sortBy=kind` | Class → Constant → Constructor → Method, innerhalb Kind nach Name. Wirkt. |
| O | `sortBy=lines` (explizit) | **Quellreihenfolge**, nicht nach `LineCount` (Ctor 289, dann 5-Zeilen-`QueryAsync` vor 66-Zeilen-`UpdateWithRowVersionAsync`) |
| P | `kindFilter=all` | wie Default, 33/33, Quellreihenfolge |
| Q | `maxMembers=3` | **3 von 33** + Zeile „33 Member gesamt, 3 gezeigt — maxMembers erhöhen oder sortBy wechseln“ + **widersprüchlicher Vollständig-Hinweis** |
| R | `maxMembers=0` | wie 1: **1 von 33** (stillschweigend auf ≥1 gehoben) |
| S | `maxResponseBytes=400` | **ignoriert**, volle 33-Member-Tabelle |

### Fehler / Leer

| # | Parameter | Ergebnis |
|---|-----------|----------|
| T | kein `symbolIdentifier` | `INVALID_ARGUMENT`: Pflichtparameter fehlt. Hint nennt `symbol` als Alias, nicht die übrigen fünf. |
| U | `ThisTypeDoesNotExistAnywhere12345` | `SYMBOL_NOT_FOUND`, Hint auf `find_symbol`. Sauber. |
| V | Kurzname `DataExecutor` / `className=DataExecutor` | `AMBIGUOUS_SYMBOL`: 7 Properties + Klasse **zweimal** (beide Partial-Dateien). IDs `P:…` und `T:…` im Context. |
| W | `P:…FormLoadQueryRequest.DataExecutor` | **kein Fehler** — stilles Promote auf Containing Type `FormLoadQueryRequest` (record, 14 Member inkl. PrimaryCtor-Params). |
| X | `ComponentRegistration` / `MitarbeiterScheduleHandler` | `SYMBOL_NOT_FOUND` (Attribut-Kurzform bzw. geratener Handler-Name). `ComponentRegistrationAttribute` trifft. |
| Y | `kindFilter=NotAKind` | **kein Fehler**, „Keine Member gefunden“, **0 von 0** (Total verloren). |
| Z | `nameFilter=NoSuchMemberXYZ` | wie Y, 0 von 0. |
| AA | `sortBy=bogus` | **kein Fehler**, fällt auf Default-Quellreihenfolge. |
| AB | `targetType=bogus` | `INVALID_ARGUMENT`, Hint project/assembly. Klar. |
| AC | Assembly `BusinessOperation` bzw. Namespace `Example.External.Process` | `SYMBOL_NOT_FOUND` (Namespace ≠ Typ). |
| AD | Assembly Kurzname `Record` | `AMBIGUOUS_SYMBOL` (Klasse + 2 Properties) — liefert brauchbare FQ-/Assembly-IDs. |

---

## 3. Verdict

**Teilweise.** Member-Tabelle, Partial-Dateien, Nested Types, Records/PrimaryCtor-Params, Aliase, FQ-Name, DocCommentId, Datei:Zeile, Assembly-Session-IDs, `kindFilter`/`nameFilter`/`sortBy=name|kind` und `maxMembers`-Truncation funktionieren. Schema und Beschreibung weichen ab bei Default-Sortierung (`lines`), `maxResponseBytes`, Pflichtfeld `symbolIdentifier`, Completeness-Text bei Truncation und stillen Fallback-Fehlern (`kindFilter`/`sortBy` ungültig).

---

## 4. Schwere

**`degraded`**

Nicht `broken`: mit FQ-Name/`T:`/`Datei:Zeile` kommt ein Agent zum Ziel. Nicht nur `friction`: Default-Sort + Default-50 auf fetten externe Typen zeigt fast nur private Fields; Cap 200 ohne Pagination lässt 116 `Record`-Member unsichtbar; Completeness-Hinweis widerspricht Truncation; Ctor-`LineCount` ist bei Primary Constructors systematisch falsch.

Einzelbefunde darunter: Schema-Pflicht/Aliase/`className` = `friction`; Ctor-Span und Completeness-Lüge = eher `degraded`; fehlende Member-IDs und Offset = `wish` (siehe §8).

---

## 5. Nutzbarkeit

**Nur mit Workaround.** Wieder aufrufen: ja, als Inventar vor Refactor — aber:

1. Nie den Kurznamen nehmen, wenn Properties so heißen (`DataExecutor`, `Record`). Immer `T:FQ` oder Datei:Zeile.
2. `sortBy=kind` oder `name` setzen; `lines` nicht glauben.
3. Große Typen: `kindFilter` + `nameFilter` + `maxMembers` (bis 200). Vollständiges Member-Inventar von `Record` (316) ist **unmöglich**.
4. Truncation-Zeile ernst nehmen, den Vollständig-Hinweis ignorieren.
5. Ungültige Filter nicht als „Typ hat keine Member“ lesen (`0 von 0`).

---

## 6. Bugs + FP/FN

Stichprobe gegen die Tool-eigene Memberliste (kein separates `rg`; User-Constraint).

**Bugs (reproduzierbar)**

- **`sortBy=lines` sortiert nicht nach Zeilen.** Call O vs. N: `UpdateWithRowVersionAsync` LineCount 66 steht nicht vorn; Default und explizites `lines` = Deklarationsreihenfolge. Beschreibung: Default `'lines'`.
- **`maxResponseBytes` tot.** Call S: 400 Bytes → volle Tabelle.
- **Ctor-Span / LineCount-FP (Primary Constructor).** `DataExecutor` `.ctor` Lines 9–297, LineCount **289**, obwohl `QueryAsync` bei Zeile 14 beginnt. `SqlTransactionContextAdapter` Ctor 177–187 umschließt die Methoden. `ComponentRegistrationAttribute` Ctor 7–13 = ganze Datei. Agent hält den Ctor für den längsten Member — genau das, wofür `sortBy=lines` gedacht wäre.
- **Completeness-Widerspruch.** Bei 3/33, 50/316, 200/316: Truncation-Zeile **und** „Daten sind vollständig … kein Read/Grep nötig“.
- **`maxMembers=0` → 1** ohne Fehler (Call R).
- **Ungültiges `kindFilter`/`sortBy`:** stilles Leeren bzw. Default statt `INVALID_ARGUMENT` (Y, AA).
- **Property-DocCommentId** wird zur Containing-Type-Struktur (W). Für ein Klassen-Tool nachvollziehbar, aber undokumentiert; Agent denkt, er bekommt die Property.
- **AMBIGUOUS listet dieselbe Klasse zweimal** (beide Partial-Dateien, gleiche `T:`-ID).
- **Transport-Truncation** bei 200 extern-Membern (Call L) zusätzlich zum `maxMembers`-Cap — letzte Zeile abgeschnitten, keine Continuation.

**FP**

- Kurzname trifft Properties (V, AD) — für ein *Class*-Structure-Tool False Positives in der Kandidatenmenge.
- Nested Compiler-Closure `_Closure_0024__496_002D0` in `Record` als Class-Member (decompiled VB/C#). Faktisch korrekt aus Roslyn, für Agenten-Inventar Rauschen.

**FN**

- `0 von 0` bei leerem Filter versteckt, dass der Typ 33 Member hat (Y, Z).
- Member-Tabelle ohne DocCommentIds: Overloads (`QueryAsync` ×2, `Record.Create` ×3) sind für `get_symbol_body` nicht eindeutig weiterreichbar.
- 116 `Record`-Member hinter Cap 200 ohne Offset: systematisches FN für Vollständigkeit.

Fakten, die stimmig wirkten: Partial `DataExecutor` (zwei Dateien, OptimisticConcurrency-Konstanten), Nested Adapter 4 Member, `ComponentRegistrationAttribute` 2 Properties, `Execute*`-Filter 3 Treffer, Assembly-Header `decompiled`/`partial`.

---

## 7. Token / IDs

- Kleine Plattform-Klassen (33 Member): Tabelle angemessen, Hinweis „kein Read/Grep“ dort fair.
- extern-`Record` Default 50: Token okay, **Inhalt schlecht** (Fields). 200 Member: Token-Stress plus Cutoff.
- Stabile Typ-IDs: `T:…` und Assembly-IDs `assembly:<hash>:<gen>:T:…` aus AMBIGUOUS-Context **funktionieren** als Folge-`symbolIdentifier`.
- Member-Zeilen haben **keine** `M:`/`P:`/`F:`-IDs, nur Name + Signatur + Zeile. Overloads brauchen Datei:Zeile oder selbst gebaute DocCommentIds — fehleranfällig.
- `className` ist kein Typ-Filter, nur Alias von `symbolIdentifier`.
- Hint bei Leer/AMBIGUOUS zeigt auf `find_symbol` — Folge-Call-tauglich, aber in diesem Einzeltool-Lauf nicht nutzbar. Datei:Zeile aus AMBIGUOUS reicht als Workaround.

---

## 8. Roslyn-Wünsche

Alles statisch/Roslyn, kein LLM:

1. **Typ-first-Auflösung** für dieses Tool: bei Mehrdeutigkeit nur `INamedTypeSymbol` (Klasse/Record/Struct/Interface); Properties gleichen Namens nicht in die Ambiguity-Liste. `className` sollte das erzwingen.
2. **`sortBy=lines` wirklich nach `LineCount` descending**; Ctor-Span über `IMethodSymbol.Locations` / Parameterliste, **nicht** `DeclaringSyntaxReference` der Type-Declaration (Primary Constructor).
3. **`maxResponseBytes` durchsetzen** oder aus dem Schema nehmen.
4. **`symbolIdentifier` in `required`**; ungültiges `kindFilter`/`sortBy`/`maxMembers=0` als `INVALID_ARGUMENT` mit erlaubten Werten.
5. Truncation: Completeness `partial` + `shown/total`; den Satz „vollständig, kein Read/Grep“ **unterdrücken**, sobald `Shown < Total` oder Cap greift.
6. **Member-DocCommentIds** (und Assembly-IDs) in der Tabelle für direkten `get_symbol_body`-Folgecall; Overloads unterscheidbar.
7. **Offset/`continuationToken` oder `startIndex`**, weil Cap 200 < reale Memberzahlen (316). Alternativ Cap anheben, Pagination bleibt trotzdem nötig.
8. Optional: `kindFilter` erlaubte Werte dokumentieren (`Method|Property|Field|Constructor|Constant|Event|Class|all`); Nested Compiler-generated Types ausblendbar (`excludeCompilerGenerated`).

---

## 9. Phase 3

Welle 3, read-only `C:\Daten\Entwicklung\Ralf\AiNetLinter`. Je Nicht-ok-Befund: Pfad + Symbol + Ansatz. Kein Patch.

### `sortBy=lines` sortiert nach Quellreihenfolge (`degraded`)

- **Pfad:** `src\AiNetLinter\Mcp\Tools\FileStructure\GetClassStructureTool.cs`; `src\AiNetLinter\Mcp\Registration\FileStructureToolRegistrations.cs`
- **Symbol:** `GetClassStructureTool.SortMembers` (`_ =>` = `FilePath`, dann `StartLine` — kein Zweig `"lines"`); Default-Parameter `sortBy = "lines"` in `AddGetClassStructure` / `GetClassStructureArgs`
- **Ansatz:** Zweig `"lines" => OrderByDescending(m => m.LineCount)`. Ungültiges `sortBy` → `INVALID_ARGUMENT` (wie `GetFileTreeInputValidator.IsValidSort`), nicht still Default.

### Ctor-Span / Primary Constructor `LineCount` (`degraded`)

- **Pfad:** `src\AiNetLinter\Mcp\Tools\FileStructure\GetClassStructureTool.cs`
- **Symbol:** `GetClassStructureTool.CreateMemberEntry` (`DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax()` → Location der **TypeDeclaration**); `IsExcludedMember` lässt `MethodKind.Constructor` trotz `IsImplicitlyDeclared` durch
- **Ansatz:** Für `IMethodSymbol.MethodKind is Constructor`: wenn Syntax `TypeDeclarationSyntax` ist, Span der `ParameterList` (Primary Ctor), nicht `typeDecl.GetLocation()`. Sonst `ConstructorDeclarationSyntax`. Roslyn: `IMethodSymbol.Locations` / Parameterliste, nicht die umschließende Typdeklaration.

### Completeness-Widerspruch bei Truncation (`degraded`)

- **Pfad:** `src\AiNetLinter\Mcp\Tools\FileStructure\GetClassStructureTool.cs`; `src\AiNetLinter\Mcp\McpSufficiencyHints.cs`
- **Symbol:** `GetClassStructureTool.ExecuteAsync` (`McpSufficiencyHints.Append(markdown)` **immer**, auch bei `truncated`); `RenderMarkdown` schreibt parallel die Truncation-Zeile
- **Ansatz:** Wie `GetNamespaceTreeTool.ExecuteProjectDrilldownInternalAsync`: Append nur wenn `!truncated`. `McpSufficiencyHints` ist dafür gebaut (Doc: nie zusammen mit Truncation).

### `maxResponseBytes` tot (`degraded`)

- **Pfad:** `src\AiNetLinter\Mcp\Registration\FileStructureToolRegistrations.cs`; `src\AiNetLinter\Mcp\AnalysisToolCall.cs`; `src\AiNetLinter\Mcp\Assemblies\Analysis\AssemblyAnalysisResponse.cs`
- **Symbol:** `AddGetClassStructure` (`MaxResponseBytes` nur im Dispatch); `ProjectAnalysisDispatcher` ignoriert den Wert; Assembly: `ApplyWireBudget` / `TrimUtf8` (Call L, Cutoff mitten im Wort bei Default 32 KB vs. Kommentar „immer unter ~50 KB“ an `MaxMembersCap`)
- **Ansatz:** Projekt: Budget durchsetzen oder aus Schema nehmen. Assembly: Member-Liste am Byte-Budget kürzen **oder** Cap/Default an `AssemblyAnalysisResponseLimits.DefaultResponseBytes` (32 KB) anpassen; Truncation nicht mitten im Token (`TrimUtf8` an Zeilengrenze). Continuation statt stummem Cut.

### `symbolIdentifier` nicht in JSON-`required` / Aliase (`friction`)

- **Pfad:** `src\AiNetLinter\Mcp\Registration\FileStructureToolRegistrations.cs`; `GetClassStructureTool.cs`
- **Symbol:** `AddGetClassStructure` (`string? symbolIdentifier = null` plus fünf weitere optionale Strings); Runtime-Pflicht in `GetClassStructureTool.ExecuteAsync` (`EffectiveSymbolIdentifier` leer → `INVALID_ARGUMENT`, Hint nennt nur `symbol`)
- **Ansatz:** SDK-Schema folgt der Lambda: ohne Default wird das Feld required — dann aber Aliase brechen. Besser: Description/Hint alle Aliase; kanonisch `symbolIdentifier`. `className` nicht als Typ-Filter verkaufen (ist nur Alias).

### Kurzname trifft Properties / stilles Promote `P:` → Containing Type (`degraded` / `friction`)

- **Pfad:** `src\AiNetLinter\Mcp\Tools\SymbolGraph\FindReferencesTool.cs`; `GetClassStructureTool.cs`
- **Symbol:** `FindReferencesTool.ResolveByNameAsync` (`SymbolFinder.FindSourceDeclarationsAsync` + `SymbolFilter.TypeAndMember`, Last-Segment); `GetClassStructureTool.TryResolveNamedType` (`symbol as INamedTypeSymbol ?? symbol.ContainingType`)
- **Ansatz:** In diesem Tool nur `INamedTypeSymbol` (TypeKind Class/Record/Struct/Interface/Enum). Ambiguity-Liste auf Typen beschränken; `P:`/`M:` entweder `INVALID_ARGUMENT` („kein Typ“) oder dokumentiertes Promote mit Typnamen in der ersten Zeile. `className`-Alias → `SymbolFilter.Type`.

### `AMBIGUOUS` listet dieselbe Klasse zweimal (`friction`)

- **Pfad:** `src\AiNetLinter\Mcp\Tools\SymbolGraph\FindSymbolTool.cs`
- **Symbol:** `FindSymbolTool.FormatSymbolLocationEntries` (eine Zeile je `symbol.Locations`, gleiche `T:`-ID bei `partial`)
- **Ansatz:** Für Ambiguity nach `DocumentationCommentId` deduplizieren, Partial-Dateien in einer Zeile sammeln. `GetClassStructureTool` zeigt Partials danach korrekt über `DeclaringSyntaxReferences`.

### `maxMembers=0` → 1; ungültiges `kindFilter` → `0 von 0` (`friction`)

- **Pfad:** `src\AiNetLinter\Mcp\Tools\FileStructure\GetClassStructureTool.cs`
- **Symbol:** `ExecuteAsync` (`Math.Clamp(args.MaxMembers, 1, MaxMembersCap)`); `FilterMembers` / `MatchesKind` (unbekannter Filter → keine Treffer; `TotalMemberCount` = gefilterte Menge)
- **Ansatz:** `maxMembers < 1` und unbekannter `kindFilter`/`sortBy` → `INVALID_ARGUMENT` mit erlaubten Werten (`Method|Property|Field|Constructor|Constant|Event|Class|all`). `TotalMemberCount` vor dem Filter behalten, wenn die Filtermenge leer ist.

### Transport-Truncation Cap 200 ohne Offset (`degraded` / `wish`)

- **Pfad:** `src\AiNetLinter\Mcp\Tools\FileStructure\GetClassStructureTool.cs`; `GetClassStructureModels.cs`; `AssemblyAnalysisResponse.cs`
- **Symbol:** `MaxMembersCap = 200`; `shownMembers = Take(clampedMaxMembers)`; `ClassStructureMemberEntry` ohne ID/Cursor
- **Ansatz:** `startIndex`/`continuationToken` (Skip/Take auf der sortierten Liste). Member-IDs: `ISymbol.TryGetDocCommentId()` in `CreateMemberEntry` / `ExtractRecordPrimaryCtorParams` (`M:`/`P:`/`F:` plus Assembly-SHA ohne Generation). Nested Compiler-Types (`_Closure_*`): `IsExcludedMember` um `CompilerGeneratedAttribute` / Namensmuster erweitern (`GetNamespaceTreeScanner.IsCompilerGenerated` als Vorlage, inkl. VB-Closure).
