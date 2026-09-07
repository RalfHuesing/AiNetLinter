# Finding: Kombination Typ-Navigation

Kette LIVE: `get_namespace_tree` → `get_class_structure` → `get_type_hierarchy` → `find_implementations`  
Namespace: `user-AiNetLinter`  
Datum: 2026-09-07  
Target: `targetType=project`, `targetPath=C:\Workspace\Sample.Project`  
Ankertypen (aus dem Namespace-Baum, IDs nicht geraten): `IDomainServiceModule`, `ISchedulerCommandCapabilityProvider`  
Kein Build, kein Test, keine Synthese anderer Findings.

## 1. Schema-Kurzfazit

Die vier Beschreibungen steuern **einzeln** den jeweiligen Call, nicht die Kette.

| Tool | Was die Beschreibung verspricht | Was die Kette braucht |
|---|---|---|
| `get_namespace_tree` | Progressive Disclosure, `includeTypes` Default true, `kind`, `depth` 1–3 | Typnamen **und** eine Folge-ID für `get_class_structure` |
| `get_class_structure` | `symbolIdentifier`: Typname, `Datei.cs:Zeile:Spalte` oder DocCommentId | Member-Übersicht plus stabile IDs für Hierarchie/Implementierungen |
| `get_type_hierarchy` | `T:Namespace.Klasse`, `Datei.cs:10:5`, `Datei.cs:10` oder `Klasse` | Implementierer inkl. `T:`-IDs |
| `find_implementations` | Format wie `find_references` (`M:…`, `IInterface`, `BaseClass.Method`) | Dieselben IDs, die die Vorgänger ausgeben |

Irreführend für die Kombination:

- `get_namespace_tree` `includeTypes=true` liefert Typ**namen** nur im Listenmodus (Leaf-Prefix bzw. `kind` auf genau einer Ebene). `depth=2` + `kind=interface` schaltet auf **Baum** (Typzahlen, keine Namen) und verweist auf einen weiteren Call. Ein Agent, der „Typen aus dem Baum“ erwartet, hat nach dem zweiten Call immer noch keine ID.
- Kein Tool-Text sagt, welches Identifier-Format der **Nachfolger** akzeptiert. `get_class_structure` und `get_namespace_tree` geben **keine** `T:`/`M:`/`P:`-IDs aus. `get_type_hierarchy` gibt `T:` nur für verwandte Typen aus, nicht für den abgefragten Typ. `find_implementations` gibt **keine** DocCommentIds aus, obwohl `AMBIGUOUS_SYMBOL` genau solche IDs dumppt.
- JSON-Schema: `symbolIdentifier` bei den drei Folgetools optional (`null`); Runtime macht ihn zur Pflicht (`INVALID_ARGUMENT`). Sechs bzw. vier Aliase, Beschreibung nennt nur ein bis zwei.
- `get_class_structure`-Signaturspalte sieht kopierbar aus, ist aber **kein** gültiger Identifier für die Folgetools.
- Hint bei `AMBIGUOUS_SYMBOL` verlangt `Datei:Zeile:Spalte`; `get_class_structure` liefert nur `17-17` **ohne Spalte**. `Datei:Zeile` der Property-Zeile bleibt mehrdeutig (Property vs. Getter).

Ohne Raten kommt ein Agent mit dem **Kurznamen** bzw. dem `Pfad:Zeile` aus dem Namespace-Baum durch die Typ-Kette. Sobald er Member-Namen oder Signaturen aus der Klassentabelle weiterreicht, bricht die Kette.

## 2. Ausgeführte Calls

Alle Calls schnell (kein Timeout). Größen grob nach Antworttext. Truncation: keine der Antworten war inhaltlich abgeschnitten; `AMBIGUOUS_SYMBOL` bei `HandlerType` ist faktisch ein Token-Dump ohne `maxResults`.

### Kette A — `IDomainServiceModule` (Root-Contracts, Listenmodus)

| # | Tool | Identifier (Quelle) | Ergebnis | Größe | completeness |
|---|---|---|---|---|---|
| 1 | `get_namespace_tree` | Prefix `…Contracts`, `project=…Contracts`, `kind=interface`, `includeTypes=true`, `depth=1` | Listenmodus: **eine** Zeile `IDomainServiceModule (interface) — …/IDomainServiceModule.cs:16`. Kein `T:`. Hinweis auf 10 Sub-Namespaces. | ~500 Z. / 6 Z. | „vollständig“ |
| 2 | `get_class_structure` | Kurzname aus Call 1: `IDomainServiceModule` | Erfolg. Header-FQN `Sample.Project.Contracts.IDomainServiceModule`. 1 Member `ConfigureServices`, Zeilen `18-18`, Signatur ohne DocCommentId. | ~700 Z. | „vollständig“ |
| 3 | `get_type_hierarchy` | FQN aus Call-2-Header | Erfolg. 2 Implementierer **mit** `T:`-IDs (`EmployeeScheduleDomainServiceModule`, Test-Dummy). Keine Basis, keine Interfaces. | ~900 Z. | „vollständig“ |
| 4 | `find_implementations` | dieselbe FQN | Erfolg. Dieselben 2 Typen, **ohne** `T:`, dafür `Datei:Zeile:Spalte` (`…cs:17:21`, `…cs:29:26`). | ~700 Z. | „vollständig“ |

### Identifier-Varianten (weitergereicht, nicht geraten)

| # | Tool | Identifier (Quelle) | Ergebnis |
|---|---|---|---|
| 5 | `get_class_structure` | `Pfad:Zeile` aus Call 1 (`…/IDomainServiceModule.cs:16`) | Erfolg, identisch Call 2. |
| 6 | `get_type_hierarchy` | Kurzname aus Call 1 | Erfolg, identisch Call 3 (inkl. `T:` der Implementierer). |
| 7 | `find_implementations` | `T:` eines Implementierers aus Call 3 | ID **akzeptiert**. 0 Treffer (konkrete sealed-artige Klasse, keine Ableitung). |
| 8 | `find_implementations` | Member-Name `ConfigureServices` aus Call 2 | `[ERROR] AMBIGUOUS_SYMBOL` — 3 Treffer, **mit** `M:`-IDs. Hint: FQN oder Datei:Zeile:Spalte. |
| 9 | `find_implementations` | `M:`-ID aus Call-8-Fehler (Interface-Methode) | Erfolg. 2 Methoden-Overrides, ohne DocCommentId, mit `Datei:Zeile:Spalte`. |
| 10 | `get_class_structure` | `T:` des Implementierers aus Call 3 | Erfolg. Klasse, 2 Member (`.ctor`, `ConfigureServices` Zeilen `19-74`). Keine `T:`/`M:` in der Tabelle. |
| 11 | `get_type_hierarchy` | dieselbe `T:` | Erfolg. Basis `object` (extern). Interface **mit** `T:Sample.Project.Contracts.IDomainServiceModule`. |
| 13 | `find_implementations` | `T:` des Interface aus Call 11 | Erfolg. Dieselben 2 Implementierer wie Call 4, ohne `T:`. |
| 14 | `get_class_structure` | `Datei:Zeile:Spalte` aus Call 4 (`…DomainServiceModule.cs:17:21`) | Erfolg, wie Call 10. |
| 16 | `get_type_hierarchy` | `Pfad:Zeile` aus Call 1 | Erfolg, wie Call 3. |
| 17 | `find_implementations` | Kurzname aus Call 1 | Erfolg, wie Call 4. |

### Namespace-Drill (Typen nachladen)

| # | Tool | Parameter | Ergebnis |
|---|---|---|---|
| 12 | `get_namespace_tree` | Prefix Root, `depth=2`, `kind=interface`, `includeTypes=true` | **Kein Typname.** Baum mit Typ**zahlen** (Ai 1, Scheduler 2, Shared.Scheduler 3, Sql 4, …). Tipp: neuen Call mit Leaf-Prefix. |
| 15 | `get_namespace_tree` | Leaf `…Contracts.Shared.Scheduler`, `kind=interface` | 3 Interfaces: `ISchedulerCommandCapabilityProvider` (`…cs:12`), `ISchedulerContextMenuActionProvider` (`…cs:11`), `ISchedulerDragCopyCapabilityProvider` (`…cs:10`). Wieder **keine** `T:`. |

### Kette B — `ISchedulerCommandCapabilityProvider` (Kurzname aus Call 15 in alle drei Folgetools)

| # | Tool | Identifier | Ergebnis | Größe |
|---|---|---|---|---|
| 18 | `get_class_structure` | `ISchedulerCommandCapabilityProvider` | Erfolg. 4 Properties (`HandlerType` Zeile 17, `Undo`, `DefaultTerminDetailsFieldCommand`, `ViewportRecalcCommand`). Keine IDs. | ~1100 Z. |
| 19 | `get_type_hierarchy` | derselbe Kurzname | Erfolg. 4 Implementierer **mit** `T:` (DomainCalendar, EmployeeSchedule, 2 Test-Stubs). | ~1400 Z. |
| 20 | `find_implementations` | derselbe Kurzname | Erfolg. Dieselben 4 Typen **ohne** `T:`, mit `Datei:Zeile:Spalte`. Reihenfolge anders als Call 19. | ~1200 Z. |

### Bruchstellen der Weiterreichung

| # | Tool | Identifier (Quelle) | Ergebnis | Größe |
|---|---|---|---|---|
| 21 | `find_implementations` | Property-Name `HandlerType` aus Call 18 | `[ERROR] AMBIGUOUS_SYMBOL` — **40+** Properties/Felder solution-weit, jeweils mit `P:`/`F:`-ID. Die gesuchte Interface-Property steckt in der Liste. | ~12000+ Z. |
| 22 | `get_type_hierarchy` | Signaturstring aus Call 18 `string ISchedulerCommandCapabilityProvider.HandlerType` | `[ERROR] SYMBOL_NOT_FOUND`. Hint: `find_symbol`. | ~300 Z. |
| 23 | `get_class_structure` | Namespace-String aus Call 15 `Sample.Project.Contracts.Shared.Scheduler` | `[ERROR] SYMBOL_NOT_FOUND`. | ~300 Z. |
| 24 | `get_class_structure` | kein `symbolIdentifier` (nach Call 1, Agent vergisst den Typ) | `[ERROR] INVALID_ARGUMENT`: Pflichtparameter fehlt. Hint nennt `MyClass` / FQN / `Datei.cs:42:10`. | ~250 Z. |
| 25 | `find_implementations` | `P:`-ID der Interface-Property aus Call 21 | Erfolg. 4 Property-Implementierungen, ohne DocCommentId. | ~1100 Z. |
| 26 | `find_implementations` | `Pfad:Zeile` der Property aus Call 18 (`…ISchedulerCommandCapabilityProvider.cs:17`) | `[ERROR] AMBIGUOUS_SYMBOL`: Property `P:…HandlerType` **und** Getter `M:…get_HandlerType~System.String`. Hint verlangt Spalte, die Call 18 nicht geliefert hat. | ~800 Z. |

## 3. Verdict

**Teilweise** wie beschrieben.

Typ-Ebene: Der Kurzname und der `Pfad:Zeile`-Anhang aus `get_namespace_tree` funktionieren in `get_class_structure`, `get_type_hierarchy` und `find_implementations`. FQN aus dem Klassenstruktur-Header und `T:` aus der Hierarchie (sobald vorhanden) ebenfalls. Hierarchie und `find_implementations` stimmen in der Stichprobe überein (2 bzw. 4 Implementierer).

Abweichend von einer echten Navigationskette:

- `get_namespace_tree` mit `depth=2` entzieht die Typnamen wieder (Call 12).
- Identifier-Formate sind **nicht** durchgängig: nur die Hierarchie emittiert `T:`; Klassentabelle und Implementierungsliste nicht; DocCommentIds des abgefragten Typs fehlen überall außer in Fehlern.
- Member-Namen und Signaturen aus `get_class_structure` sind keine Folgetool-IDs (Call 8, 21, 22, 26).
- `find_implementations` auf eine `T:`-ID eines konkreten Implementierers ist formal ok, inhaltlich eine Sackgasse (0 Treffer) — die Kette gibt nicht vor, dass dieser `T:` der nächste Schritt für „Implementierungen des Interface“ ist.

## 4. Schwere

**degraded**

Die Typ-Kette ist mit Workaround (Kurzname oder `Pfad:Zeile` kopieren, Member nicht anfassen) nutzbar. Risiko: stiller Moduswechsel im Namespace-Baum, Token-Flut bei Member-Weiterreichung, fehlende Spalten, inkonsistente IDs. Nicht `broken` (Happy Path Typname funktioniert in allen vier Tools). Zusätzlich `friction` an Schema/Pflichtfeld und an der Signaturspalte, die wie ein Identifier aussieht.

## 5. Nutzbarkeit

**nur mit Workaround**

Wiederverwenden ja, mit festen Kopierregeln:

1. Aus `get_namespace_tree` nur eine **Typzeile** im Listenmodus nehmen (`Name` oder `relativerPfad:Zeile`). Nicht den Namespace-String, nicht `depth=2` als Typquelle.
2. Denselben Typ-Kurznamen bzw. die FQN in `get_class_structure`, `get_type_hierarchy` und `find_implementations` stecken — nicht die Member-Spalte `Name` oder `Signature`.
3. `T:` nur verwenden, wenn die **Hierarchie** sie ausgegeben hat (Implementierer oder, nach Umweg über den Implementierer, das Interface). Nicht aus dem Klassenkopf erfinden.
4. Member-Overrides: nicht den Kurznamen nehmen. Entweder erst `AMBIGUOUS_SYMBOL` abwarten und die `M:`/`P:`-ID kopieren, oder die Interface-`T:`/`FQN` an `find_implementations` geben (liefert Typ-Implementierer, nicht den einzelnen Member — außer nach `M:`/`P:`).
5. `Datei:Zeile:Spalte` aus `find_implementations` taugt rückwärts für `get_class_structure`. `Datei:Zeile` ohne Spalte aus der Klassentabelle taugt nicht zuverlässig für Member in `find_implementations`.

Ohne diese Regeln landet der Agent in `SYMBOL_NOT_FOUND`, `INVALID_ARGUMENT` oder einem 40-Treffer-Dump.

## 6. Bugs + FP/FN

### Bugs (reproduzierbar in dieser Kette)

- **Call 12:** `includeTypes=true` + `kind=interface` + `depth=2` liefert keine Interfaces, nur Zählwerte. Beschreibung „Typen ausgeben (Default true)“ trifft hier nicht. Folgetools haben nichts zum Übernehmen.
- **Call 18 → 21:** Die einzige kopierbare Member-Spalte (`Name`) erzeugt in `find_implementations` `AMBIGUOUS_SYMBOL` mit solution-weitem Dump. Kein Cap analog `maxResults`.
- **Call 18 → 26:** Zeilenbereich `17-17` ohne Spalte; Folgetool-Hint verlangt Spalte. Property und Getter teilen sich die Zeile.
- **Call 18 → 22:** Signaturtext der Klassentabelle ist kein Identifier (`SYMBOL_NOT_FOUND`), obwohl `get_class_structure` genau diese Spalte als „Signature“ anbietet.
- **ID-Asymmetrie:** Call 3/11/19 emittieren `T:`. Call 2/4/10/18/20 emittieren keine DocCommentIds. Dieselbe semantische Entität hat je nach Tool eine andere Kopiervorlage.
- **Call 24:** Schema markiert `symbolIdentifier` als optional; Runtime lehnt den Call nach erfolgreichem Namespace-Baum ab, wenn der Agent nur Target mitschickt.

Kein Absturz, kein Timeout, `isError` nur über `[ERROR]`-Text (Cursor-Wrapper), nicht als Tool-Crash.

### False Positives / False Negatives (Stichprobe)

- **Call 1 vs. Call 15:** `kind=interface` auf dem Contracts-Root listet nur `IDomainServiceModule`. Shared.Scheduler allein hat drei weitere Interfaces. Filter ist nicht rekursiv — für die Kette ein FN der Exploration, kein FP in der einen Root-Zeile (Dateiname stimmt).
- **Call 3/4 vs. Platform-Ist:** Zwei Implementierer von `IDomainServiceModule` (Schedule-Modul + Test-Dummy). Stichprobe plausibel; andere Domain-Projekte tauchten nicht auf. Kein Widerspruch zwischen Hierarchie und `find_implementations`.
- **Call 19 vs. 20:** Jeweils vier Implementierer von `ISchedulerCommandCapabilityProvider`, dieselben Typen, andere Reihenfolge. Kein FN zwischen den beiden Folgetools.
- **Call 7:** 0 Implementierungen zur konkreten Klasse ist fachlich korrekt, für einen Agenten nach „nimm die `T:` aus der Hierarchie“ aber ein False Drop der Kette (Werkzeug macht etwas anderes als der Kettenschritt suggeriert).

## 7. Token / IDs

- Typ-Kette (Calls 1–4, 15, 18–20) ist kompakt (je ~500–1400 Zeichen) und folgt dem Progressive-Disclosure-Gedanken.
- Call 21 sprengt das: ein Property-Name aus der Klassentabelle → fünfstellige Zeichenzahl, 40+ Symbole, darunter viele irrelevante `HandlerType`-Felder in Tests/Handlern. Für die Kette der teuerste Fehlgriff.
- Call 8 ist klein und nützlich: drei `M:`-IDs, direkt in Call 9 verwendbar. `AMBIGUOUS_SYMBOL` ist hier ein verstecktes ID-Tool — ungleich Call 21.
- Stabile IDs in der Kette:

  | Ausgabe | `get_namespace_tree` | `get_class_structure` | `get_type_hierarchy` | `find_implementations` |
  |---|---|---|---|---|
  | Kurzname | ja | ja (Member) | nein (FQN/`T:` der Verwandten) | FQN |
  | `Pfad:Zeile` | ja | Zeilenbereich ohne Spalte | `Pfad:Zeile` der Verwandten | `Pfad:Zeile:Spalte` |
  | `T:`/`M:`/`P:` | nein | nein | `T:` der Verwandten | nein (nur im Fehler) |

- Kein `cursor`/`continuationToken`. StructuredContent in der Agenten-Antwort nicht sichtbar; Weiterreichung nur über den Markdown-Text.
- Folge-Call-Tauglichkeit: **Typ-Kurzname und Root-`Pfad:Zeile` ja**; Member-Name/Signatur **nein**; `T:` **ja, sobald die Hierarchie sie geliefert hat**.

## 8. Roslyn-konforme Wünsche

Alles aus `INamespaceSymbol` / `INamedTypeSymbol` / `ISymbol.GetDocumentationCommentId()` und Source-Locations, kein LLM:

1. In **allen vier** Antworten dieselbe Symbol-ID emittieren (`T:` für Typen, `M:`/`P:` für Member), copy-paste-fähig als `symbolIdentifier`.
2. `get_namespace_tree`: Listenmodus-Zeile `Name (kind) — pfad:zeile id: T:…`. Baummodus nicht als „Typen ausgeben“ verkaufen; `kind`+`depth>1` entweder rekursiv Typen listen oder den Modus im Text benennen.
3. `get_class_structure`: Spalte oder Trailing `id: M:…`/`P:…`; Start/Ende **mit Spalte**, damit `Datei.cs:17:19` den Getter vom Property trennt.
4. Signaturspalte nicht als Identifier andeuten — oder die Signatur zusätzlich als gültiges `symbolIdentifier`-Format akzeptieren (Roslyn `ToDisplayString` rundtrip).
5. `find_implementations`: Trefferzeilen mit derselben `T:`/`M:`/`P:`-ID wie die Hierarchie, nicht nur FQN + Location.
6. `AMBIGUOUS_SYMBOL` deckeln (`maxResults`) und die zum Vorgänger-Typ passenden Treffer zuerst (ContainingType-Match), statt 40 solution-weite `HandlerType`.
7. Schema: `symbolIdentifier` in `required`; `targetType` als Enum; in den Beschreibungen ein Satz „Identifier aus `get_namespace_tree` / `get_class_structure` / `get_type_hierarchy` übernehmen“.
8. Hint nach Namespace-Baum: nicht nur `depth=2`, sondern explizit Leaf-Prefix **oder** den gefundenen Typnamen an `get_class_structure(symbolIdentifier=…)`.

## 9. Phase 3

AiNetLinter read-only `C:\Daten\Entwicklung\Ralf\AiNetLinter`. Je Nicht-ok-Kettenbefund: Pfad + Symbol + Roslyn-Ansatz.

### Call 12 — `includeTypes=true` + `depth=2` ohne Typnamen

- **Pfad:** `src\AiNetLinter\Mcp\Tools\FileStructure\GetNamespaceTreeScanner.cs`
- **Symbol:** `GetNamespaceTreeScanner.ScanProjectNamespacesAsync` (Zweig `IncludeTypes && Depth <= 1` → `RenderNamespaceTypes`, sonst `RenderNamespaceTree`); `RenderNamespaceTree`; `CollectNamespaceTreeNodes`
- **Ansatz:** Modus nicht an `depth` koppeln. Bei `includeTypes=true` die in `CollectNamespaceTreeNodes` bereits gebauten `TypeNodeEntry`s im Markdown ausgeben (`Name (kind) — pfad:zeile id: T:…`), nicht nur `(n Typen)`. Alternativ `kind`+`depth>1` rekursiv `INamespaceSymbol.GetTypeMembers()` plus `GetNamespaceMembers()` listen. Beschreibung in `FileStructureToolRegistrations.GetNamespaceTreeDescription` an den tatsächlichen Modus anpassen (Baum = Zählwerte vs. Liste = Namen).

### Call 1 vs. 15 — `kind=interface` nicht rekursiv

- **Pfad:** `src\AiNetLinter\Mcp\Tools\FileStructure\GetNamespaceTreeScanner.cs`
- **Symbol:** `GetNamespaceTreeScanner.CollectSourceTypes` (`ns.GetTypeMembers()` nur der aktuellen Ebene); `RenderNamespaceTypes`
- **Ansatz:** Optional `recursive=true` oder bei `kind`-Filter transitiv `GetNamespaceMembers` mitlaufen lassen (`INamespaceSymbol`-Walk). Sonst im Listenmodus explizit „nur direkte Typen, Sub-Namespaces per Leaf-Prefix“ — der Hint `AppendSubNamespaceHint` darf den gefundenen Typnamen an `get_class_structure(symbolIdentifier=…)` nennen, nicht nur `depth=2`.

### ID-Asymmetrie — nur Hierarchie emittiert `T:`

- **Pfad:** `src\AiNetLinter\Mcp\Tools\FileStructure\GetNamespaceTreeModels.cs` (`TypeNodeEntry` ohne Id); `GetNamespaceTreeScanner.ToTypeEntry`; `src\AiNetLinter\Mcp\Tools\FileStructure\GetClassStructureModels.cs` (`ClassStructureMemberEntry` ohne Id); `GetClassStructureTool.CreateMemberEntry` / `AppendMemberRows`; `src\AiNetLinter\Mcp\Tools\SymbolGraph\GetTypeHierarchyFormatter.cs` (`FormatHierarchyTypeReference` → `FindSymbolTool.FormatSymbolLocations`); `src\AiNetLinter\Mcp\Tools\TypeHierarchy\FindImplementationsModels.cs` (`ImplementationItemDto` ohne Id); `FindImplementationsTool.MapToDto` / `FormatResultText`
- **Symbol:** `DocumentationCommentId.CreateDeclarationId` (bereits in `FindSymbolTool.FormatSymbolLocationEntries`); `ISymbol.GetDocumentationCommentId()`
- **Ansatz:** Dieselbe Formatter-Zeile wie `FindSymbolTool.FormatEntry` (`id: \`T:…\`` / `M:` / `P:`) in Namespace-Typzeile, Klassen-Memberzeile, Hierarchie (inkl. **abgefragtem** Typ, nicht nur Verwandten) und Implementierungsliste. `TypeNodeEntry` / `ClassStructureMemberEntry` / `ImplementationItemDto` um `Id` + Spalte erweitern. `GetTypeHierarchyFormatter.BuildHierarchyAsync` speichert `type.ToDisplayString()` ohne DocId — Header analog `FormatSymbolLocations` für den Query-Typ.

### Call 18→21 — Member-Name → ungedeckelter `AMBIGUOUS_SYMBOL`

- **Pfad:** `src\AiNetLinter\Mcp\Tools\SymbolGraph\FindReferencesTool.cs`; `src\AiNetLinter\Mcp\McpToolResults.cs`; `src\AiNetLinter\Mcp\Tools\TypeHierarchy\FindImplementationsTool.cs`
- **Symbol:** `FindReferencesTool.ResolveByNameAsync` (`SymbolFinder.FindSourceDeclarationsAsync` auf `lastSegment`, dann alle Kandidaten); `McpToolResults.AmbiguousSymbol` (kein Cap)
- **Ansatz:** Cap analog `maxResults` (z. B. 10) in `AmbiguousSymbol`. Ranking: `ContainingType` des zuletzt gesehenen Typs zuerst (`ISymbol.ContainingType` / `INamedTypeSymbol`). Rest hinter Truncation-Zeile. Roslyn bleibt `FindSourceDeclarationsAsync`; nur Ausgabe und Sortierung ändern.

### Call 18→26 — `Datei:Zeile` ohne Spalte, Property vs. Getter

- **Pfad:** `src\AiNetLinter\Mcp\Tools\FileStructure\GetClassStructureTool.cs`; `src\AiNetLinter\Mcp\Tools\SymbolGraph\FindReferencesTool.cs`; `src\AiNetLinter\Mcp\Tools\SymbolGraph\SymbolIdentifierResolver.cs`
- **Symbol:** `GetClassStructureTool.CreateMemberEntry` (`FileLinePosition.Line` ohne `Character`); `FindReferencesTool.ResolveByLineAsync` (`ResolveSymbolsOnLine` → Property `IPropertySymbol` + Getter `IMethodSymbol` auf derselben Zeile)
- **Ansatz:** `StartColumn` aus `GetLineSpan().StartLinePosition.Character + 1` in die Memberzeile (`Datei.cs:17:19`). Property-Zeile als `P:`; Getter nicht parallel als `M:get_…` in derselben Tabellenzeile. Fallback ohne Spalte: in `ResolveByLineAsync` `IPropertySymbol` vor Accessor-`IMethodSymbol` bevorzugen, wenn beide auf der Zeile sitzen.

### Call 18→22 — Signaturspalte ist kein Identifier

- **Pfad:** `src\AiNetLinter\Mcp\Tools\FileStructure\GetClassStructureTool.cs`; `src\AiNetLinter\Mcp\Registration\FileStructureToolRegistrations.cs`
- **Symbol:** `GetClassStructureTool.CreateMemberEntry` (`ToDisplayString(MinimallyQualifiedFormat)`); `FindReferencesTool.ResolveByNameAsync` (`IsSymbolMatch` auf `EndsWith`)
- **Ansatz:** Signatur nicht als kopierbare ID andeuten (Spaltenname / Beschreibung). Identifier-Spalte = `GetDocumentationCommentId()`. Optional Roundtrip: `MinimallyQualifiedFormat` in `ResolveByNameAsync` zusätzlich gegen `symbol.ToDisplayString(MinimallyQualifiedFormat)` prüfen — nur wenn eindeutig, sonst `AMBIGUOUS_SYMBOL` mit `M:`/`P:`.

### Call 24 — Schema optional, Runtime Pflicht

- **Pfad:** `src\AiNetLinter\Mcp\Registration\FileStructureToolRegistrations.cs`; `src\AiNetLinter\Mcp\Registration\SymbolGraphToolRegistrations.cs`; `src\AiNetLinter\Mcp\Tools\McpToolRegistrationOptions.cs`; `GetClassStructureTool.ExecuteAsync`; `GetTypeHierarchyTool.ExecuteAsync`; `FindImplementationsTool.ExecuteAsync`
- **Symbol:** C#-Parameter `string? symbolIdentifier = null` (SDK → JSON nicht in `required`); Runtime `LinterErrorCodes.InvalidArgument`
- **Ansatz:** Entweder Parameter nicht-nullable ohne Default (Schema = Runtime) oder SDK-`required` setzen. Beschreibung nennt Pflicht bereits (`GetClassStructureDescription`); Schema muss folgen. Alias-Liste in der Description auf die tatsächlich gebundenen Namen kürzen (sechs vs. ein bis zwei).

### Call 7 — `T:` des konkreten Implementierers → 0 Treffer

- **Pfad:** `src\AiNetLinter\Mcp\Tools\TypeHierarchy\FindImplementationsTool.cs`
- **Symbol:** `FindImplementationsTool.FindTypeImplementationsAsync` (`SymbolFinder.FindDerivedClassesAsync` / `FindImplementationsAsync`)
- **Ansatz:** Verhalten Roslyn-korrekt (sealed/konkret → keine Derived). Formatter bei 0 Treffern auf `TypeKind.Class` + nicht-abstrakt: eine Zeile „keine Ableitungen; für Interface-Implementierer die Interface-`T:` verwenden“ plus Echo der Query-`T:`. Kein Fake-Treffer.

### Sufficiency-Hinweis trotz Member-Truncation (Kette B, `maxMembers`)

- **Pfad:** `src\AiNetLinter\Mcp\Tools\FileStructure\GetClassStructureTool.cs`; `src\AiNetLinter\Mcp\McpSufficiencyHints.cs`
- **Symbol:** `GetClassStructureTool.ExecuteAsync` ruft `McpSufficiencyHints.Append` **immer**, auch bei `payload.Truncated`
- **Ansatz:** Wie `GetNamespaceTreeTool.ExecuteProjectDrilldownInternalAsync`: Hint nur wenn `!Truncated`. Bei Truncation nur die bestehende `maxMembers`-Zeile.
