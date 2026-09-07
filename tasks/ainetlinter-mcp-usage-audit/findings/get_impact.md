# Finding: `get_impact`

Anker: `T:Sample.Project.Infrastructure.Sql.DataExecutor` (`Sample.Project/Infrastructure/Data/DataExecutor.cs`) und `[ComponentRegistration]` als `ComponentRegistrationAttribute`. Assembly: `T:Example.External.Process.Record` in `Example.External.Process.dll`.

## 1. Schema-Kurzfazit

Die Tool-Beschreibung steuert den Call **weitgehend korrekt** und ist für Agenten besser als das nackte JSON-Schema: drei Modi (uncommittete Diffs / `gitRef` / `symbolIdentifier`, nie beides), `detailLevel` `callers` vs. `change-context` (letzteres nur Git-Diff), Assembly **nur** `symbolIdentifier`, Caps für `maxResults` (50), `depth` (1, Hard-Cap 3, 200 Knoten), `maxChangedSymbols` (20/100), `maxTestsPerSymbol` (10/50). Fehlertexte zur Laufzeit (`INVALID_ARGUMENT` bei beiden IDs, bei `change-context`+Symbol, bei Assembly ohne ID) passen zur Beschreibung.

Irreführend oder unvollständig:

- JSON-Schema markiert nur `targetType`/`targetPath` als `required`. Properties haben **keine** Enums/`description`. Gültige `detailLevel`-Werte, Exklusivität `gitRef` vs. `symbolIdentifier` und „Assembly braucht Symbol“ stehen nur im Fließtext.
- Aliase `symbol`, `identifier`, `name` stehen im Schema, in der Beschreibung nur „oder Aliase symbol, identifier, name; Format wie find_references“. Live funktionieren alle drei analog `symbolIdentifier`.
- Leeres `symbolIdentifier=""` wird **nicht** als ungültig abgelehnt, sondern wie „beide weglassen“ auf uncommittete Änderungen gemappt.
- `gitRef=HEAD` ist **nicht** „Impact des HEAD-Commits“, sondern faktisch Working-Tree vs. HEAD (uncommittete tracked Dateien). Der letzte Commit erscheint erst mit `HEAD~1`. Das steht so nicht in der Beschreibung.
- Leerer Git-Diff und fehlendes Repo teilen sich denselben Text: `Kein Git-Repository oder leerer Diff`. Hier existiert das Repo; der Diff war leer.
- Truncation-Footer kopiert `find_references`/`search_pattern`: „Pattern verfeinern oder maxResults erhöhen“. `get_impact` hat **kein** `pattern`, kein `cursor`/`continuationToken`.
- Beschreibung verspricht im change-context geänderte Symbole, Call-Sites, Tests, Violations und `dotnet test`-Filter. Das kam live an. Die `callers`-Trefferliste liefert **keine** Symbol-IDs und keine Test-Filter.
- `targetType=assembly` ohne Symbol: recoverable `INVALID_ARGUMENT` wie beschrieben. Happy Path braucht trotzdem eine ID, die dieses Tool allein nicht inventarisieren kann (Hint: `find_symbol`).

## 2. Ausgeführte Calls

Alle Calls über `CallDynamicTool` / `user-AiNetLinter` / `get_impact`. Wartezeit überall **schnell** (kein Timeout; Assembly nach Cache ebenfalls schnell). Nur dieses Tool plus Schema-Lookup.

| # | Parameter | Antwort | Truncation / completeness | Größe (grob) |
|---|-----------|---------|---------------------------|--------------|
| A | project, `symbolIdentifier=DataExecutor`, `detailLevel=callers`, `maxResults=50` | `AMBIGUOUS_SYMBOL`, 9 Kandidaten inkl. kopierbarer `T:`/`P:`-IDs (Klasse + Properties gleichen Namens) | — | ~2 KB / ~15 Zeilen |
| B | project, Default (kein gitRef/Symbol) | `Keine betroffenen Aufrufstellen gefunden fuer 'uncommittete Aenderungen'` | — | ~0,1 KB / 1 Zeile |
| C | project, `detailLevel=change-context` (kein gitRef) | `Kein Git-Repository oder leerer Diff` + Hinweis „vollständig … kein Read/Grep“ | completeness-Hinweis trotz Leer | ~0,3 KB / 3 Zeilen |
| D | assembly BusinessOperation.dll, `symbolIdentifier=BusinessOperation` | Assembly-Header `origin=decompiled; completeness=partial`, dann `SYMBOL_NOT_FOUND` | `completeness=partial` | ~0,8 KB |
| E | project, `T:…DataExecutor`, `callers`, `maxResults=50` | 246 Treffer, 50 gezeigt, **nur** Tests.Ama/Components/Logic | ja; Hint „Pattern verfeinern“ | ~8 KB / ~51 Zeilen |
| F | project, `symbolIdentifier=ComponentRegistration` | `SYMBOL_NOT_FOUND` (Attribut-Kurzname ohne Suffix) | — | ~0,3 KB |
| G | project, `change-context`, `gitRef=HEAD` | wie C: kein Repo **oder** leerer Diff (Repo existiert; Working-Tree vs. HEAD leer) | completeness-Hinweis | ~0,3 KB |
| H | assembly, **ohne** Symbol | Header + `INVALID_ARGUMENT`: Assembly braucht `symbolIdentifier`; gitRef nicht verfügbar | `completeness=partial` | ~0,8 KB |
| I | project, `T:…DataExecutor`, `maxResults=5` | 246 gesamt, 5 gezeigt | ja | ~1 KB / 6 Zeilen |
| J | project, `T:…DataExecutor`, `depth=3`, `maxResults=20` | „3393 Treffer gesamt (depth=3, hard-cap 200), 20 gezeigt“ + Traversal-Cap 200; Labels `transitiver Aufrufer` | ja, widersprüchliche Zähler | ~4 KB |
| K | project, Alias `symbol=` gleiche T-ID | identisch zu E (246/50) | ja | ~8 KB |
| L | project, Alias `name=` gleiche T-ID | identisch zu E | ja | ~8 KB |
| M | project, `gitRef=HEAD` **und** `symbolIdentifier=T:…DataExecutor` | `INVALID_ARGUMENT`: gegenseitig exklusiv | — | ~0,3 KB |
| N | project, `detailLevel=change-context` **und** Symbol | `INVALID_ARGUMENT`: change-context nur Git-Diff; Hint `get_feature_context` | — | ~0,3 KB |
| O | assembly, `gitRef=HEAD` | wie H: `INVALID_ARGUMENT` Assembly braucht Symbol | `completeness=partial` | ~0,8 KB |
| P | project, `ComponentRegistrationAttribute` (Default callers) | 23 Treffer, vollständig; Domain-Handler + Scanner + Tests | nein; Hinweis vollständig | ~4 KB / ~25 Zeilen |
| Q | project, Alias `identifier=` T-ID, `maxResults=5` | identisch zu I | ja | ~1 KB |
| R | project, `T:…DataExecutor`, `maxResults=250` | 246 Zeilen, Produktion erst ab Rang ~208; Hinweis vollständig | nein | **41,3 KB / 248 Zeilen** |
| S | project, `change-context`, `gitRef=HEAD~1`, `maxChangedSymbols=5`, `maxTestsPerSymbol=3` | 4 Dateien, 4/4 Symbole, 0 Aufrufstellen, 4 Test-Treffer, 0 Violations; `dotnet test … --filter FullyQualifiedName~…` | unter Cap; Hinweis vollständig | ~1,5 KB / ~12 Zeilen |
| T | project, `change-context`, `gitRef=main` | `ANALYSIS_FAILED`: `fatal: bad revision 'main'` (Branch heißt nicht main) | — | ~0,4 KB |
| U | project, `DoesNotExist_NoSuchType_XYZ` | `SYMBOL_NOT_FOUND`, Hint `find_symbol` | — | ~0,3 KB |
| V | project, `symbolIdentifier=""` | wie B (uncommittete Änderungen), **kein** INVALID_ARGUMENT | — | ~0,1 KB |
| W | assembly, `Properties/AssemblyInfo.cs:1:1` | `SYMBOL_NOT_FOUND` | `completeness=partial` | ~0,8 KB |
| X | assembly, `T:Example.External.Process` | `SYMBOL_NOT_FOUND` (Namespace/Typ-Rate) | `completeness=partial` | ~0,8 KB |
| Y | project, `gitRef=HEAD~1`, `detailLevel=callers` (Default) | `Keine betroffenen Aufrufstellen gefunden fuer 'HEAD~1'` — konsistent mit S (0 Call-Sites) | — | ~0,1 KB |
| Z | project, `detailLevel=bogus` | `INVALID_ARGUMENT`: erlaubt `callers` / `change-context` | — | ~0,3 KB |
| AA | project, `targetPath=…\DoesNotExist` | `PROJECT_NOT_INITIALIZED` (sucht `ainetlinter.project.json`) | — | ~0,5 KB |
| AB | assembly, `AssemblyInfo` | `SYMBOL_NOT_FOUND` | `completeness=partial` | ~0,8 KB |
| AC | assembly, Platform-`T:…DataExecutor` | `SYMBOL_NOT_FOUND` (kein Cross-Target-Leak) | `completeness=partial` | ~0,8 KB |
| AD | project, `change-context`, `gitRef=master` | wie C: leerer Diff (aktueller Branch vs. Working-Tree) | completeness-Hinweis | ~0,3 KB |
| AE | assembly, `Example.External.Process` | `SYMBOL_NOT_FOUND` | `completeness=partial` | ~0,8 KB |
| AF | assembly, Kurzname `Record` | `AMBIGUOUS_SYMBOL`; IDs `assembly:<hash>:3:T:…Record` / `:P:…` | — | ~2 KB |
| AG | assembly, `AssemblyInfo.cs:1:10` | 92 Treffer „Aufruf von **'System'**“, 5 gezeigt — Datei:Zeile traf `using System` | ja; semantisch falsch für Impact | ~2 KB |
| AH | project, `T:…DataExecutor`, `depth=0`, `maxResults=5` | wie I; Footer `[depth auf 1 begrenzt — requestedDepth=0]` | ja | ~1 KB |
| AI | assembly, `T:Example.External.Process.Record`, `maxResults=20` | 133 Treffer, 20 gezeigt; fast nur `Record.cs` Self-Sites; Cache-Absolutpfade | ja; Header `completeness=partial` | ~8 KB |
| AJ | assembly, opaque `assembly:…:3:T:…Record`, `maxResults=10` | dieselben 133, 10 gezeigt; T-ID und assembly-präfixierte ID äquivalent | ja | ~4 KB |

`maxChangedSymbols=1` / `maxTestsPerSymbol=1` auf `HEAD~1`: zweiter Truncation-Call wurde von der Umgebung blockiert (Approval-Kontext). Cap-Verhalten nur indirekt: Call S mit 5/3 zeigte 4/4 Symbole (unter Cap).

## 3. Verdict

**Teilweise wie beschrieben.** Symbol-Impact, Ambiguity mit kopierbaren IDs, Exklusivität gitRef/Symbol, Assembly-only-`symbolIdentifier`, `change-context` inkl. Testfilter und `dotnet test --filter`, `maxResults`/`depth`-Caps und klare `INVALID_ARGUMENT`/`SYMBOL_NOT_FOUND` funktionieren. Dagegen: Default-50 blendet Produktion aus, Truncation-Hints lügen, `gitRef=HEAD` vs. Commit-Impact ist unklar, Leermeldung vermischt „kein Git“ und „leerer Diff“, `callers`-Zeilen haben keine IDs, Assembly-Happy-Path ist ohne vorheriges `find_symbol` Glückssache, Datei:Zeile kann auf `System` auflösen.

## 4. Schwere

`degraded`

Begründung: Mit voller `T:`-ID und `maxResults` ≥ Gesamttreffer ist echte Arbeit möglich; `change-context` auf einem echten Commit-Ref (`HEAD~1`) ist der wertvollste Modus (Symbole + Testfilter). Mit Schema-Default (50, Kurzname `DataExecutor`, `gitRef=HEAD` als „letzter Commit“) sieht der Agent Testhits, Ambiguity oder eine irreführende Leermenge. Nicht dauerhaft kaputt, aber für den dokumentierten Agenten-Workflow (Impact vor Tests) riskant.

## 5. Nutzbarkeit

**nur mit Workaround**

Workaround: Kurzname nicht verwenden; aus `AMBIGUOUS_SYMBOL` die `T:`-ID kopieren; `maxResults` auf die gemeldete Gesamttrefferzahl setzen; `depth` auf 1 lassen; für Diffs `detailLevel=change-context` und `gitRef=HEAD~1` (nicht `HEAD`), wenn der letzte Commit gemeint ist; Assembly erst nach `find_symbol` (Kurzname `Record` → Ambiguity → `T:…Record`). `[ComponentRegistration]` als `ComponentRegistrationAttribute`, nicht `ComponentRegistration`. Ohne das: Default-Callers sind für Produktions-Impact unbrauchbar.

## 6. Bugs, False Positives / False Negatives vs. Ist

Stichprobe gegen Platform-Quelltext / Dekompilat-Cache (kein Vollabgleich):

**Stimmt (TP):**

- `AdminAiExplorationPlatformSupport.cs:21` — Parameter/Nutzung `DataExecutor` (Ist).
- `AdminComponentListHandler.cs:17` und `PlatformServiceExtensions.cs:264–268` stehen in der **vollen** 246er-Liste (Rang 221 bzw. 212–215).
- `ComponentRegistrationAttribute` trifft `CompanyCalendarHandler.cs:12` (`[ComponentRegistration(…)]`) und `ComponentRegistrationAssemblyScanner.cs:16`.
- Unbekanntes Symbol → `SYMBOL_NOT_FOUND`, nicht leere Trefferliste.
- Assembly-`T:…DataExecutor` in der externe Assembly → `SYMBOL_NOT_FOUND` (kein Leak).
- `HEAD~1` change-context: 4 Testmethoden in `Tests.Logic/Scripts/`; callers-Modus dazu 0 Call-Sites — intern konsistent (Facts ohne externe Aufrufer).
- `gitRef=main` → echte Git-Fehlermeldung (`bad revision`), Branch in diesem Repo ist nicht `main`.

**Problematisch:**

- **Default-50 = False Negative für Produktion.** Volle Liste: Produktion `Sample.Project/` beginnt erst ~Rang 208/246 (`AiChatSessionManager.cs:19`). Default und `maxResults=50` zeigen ausschließlich Testhits (Pfad-Sortierung `Tests.Ama` zuerst). Agent „was bricht, wenn ich DataExecutor ändere?“ sieht die falsche Welt.
- **`gitRef=HEAD` vs. Beschreibung „Commit-Ref“.** `HEAD` und Default und `master` (aktueller Branch) liefern Leer; `HEAD~1` liefert den letzten Commit. Agent, der `HEAD` als „dieser Commit“ liest, hält Impact für leer.
- **Leer-Text lügt:** `Kein Git-Repository oder leerer Diff` obwohl `.git` am `targetPath` existiert. Completeness-Hinweis „vollständig, kein Read/Grep“ auf dieser Leermenge ist falsch (Untracked-Dateien im Workspace wurden nicht als Diff gezählt — Ist laut Start-Status nur `??`).
- **Leeres `symbolIdentifier=""`** fällt still auf uncommittete Änderungen statt Validierungsfehler.
- **`[ComponentRegistration]`:** Kurzname `ComponentRegistration` → NOT_FOUND; nur `ComponentRegistrationAttribute`. Attribute-Suffix ist Roslyn-üblich, in der Probe-Anker-Formulierung aber nicht offensichtlich.
- **Datei:Zeile Assembly `AssemblyInfo.cs:1:10`:** 92× „Aufruf von `System`“ — das ist `using System;`, kein Impact-Ziel. Schwerer FP, wenn Agent dem Hint „Datei.cs:42:10“ folgt.
- **Assembly-Self-Hits:** `T:…Record` listet überwiegend Zeilen in `Record.cs` selbst (Felder, `this`, Konstruktor). Duplikat gleicher Zeile `Record` + `Record..ctor` (z. B. 2078). Für „wer nutzt Record von außen?“ Rauschen; 20/133 reichen nicht für Fremdtypen.
- **depth=3-Zähler:** 3393 gesamt **und** „Traversal auf 200 Knoten begrenzt — weitere Treffer nicht enthalten“. Unklar, ob 3393 Schätzung, Overflow oder Widerspruch.
- **Truncation-Copy-Paste:** „Pattern verfeinern“ ist für dieses Tool falsch.
- **`ComponentRegistration` vs. Produktion:** 23 Treffer wirken vollständig; das sind Attribut-Anwendungen/ctor, nicht „Handler-Impact“. Fachlich ok für das Attribut, leicht verwechselbar mit dem Probe-Anker `[ComponentRegistration]`.

Keine stillen Leermengen bei gültiger, eindeutiger `T:`-ID im callers-Modus beobachtet.

## 7. Token / IDs / Hints

- **Token:** Default-Antwort für den Kern-SQL-Typ ist eine Testhit-Liste. Vollabruf 41,3 KB / 248 Zeilen ohne Aggregation (kein `scopeType`, keine Gruppierung Produktion/Tests). `depth>1` explodiert (3393). Assembly-Header wiederholt sich bei jedem Fehler (`generatedPath`, generation, completeness=partial).
- **IDs:** Nur **Ambiguity/Not-Found** liefert folgetaugliche IDs (`T:…`, `P:…`, `assembly:<hash>:<gen>:T:…`). Die callers-Trefferliste hat **keine** Symbol-IDs, nur `Datei:Zeile`. `change-context` nennt Methodennamen + Datei:Span, aber ebenfalls keine `M:`-IDs; dafür einen direkt nutzbaren `dotnet test --filter`.
- **Hints:** `SYMBOL_NOT_FOUND` → `find_symbol` gut. Ambiguity → FQ-Name oder Datei:Zeile:Spalte gut (Datei:Zeile ohne Spalte auf Assembly war gefährlich). change-context+Symbol → `get_feature_context` gut. Truncation-Hint falsch, kein Paging.
- **StructuredContent:** Auf der Agenten-Textfläche nicht sichtbar. Footer muss geparst werden (`[246 Treffer gesamt, 50 gezeigt]`). Assembly-Metadaten nur als Headerzeile.
- **Aliase:** `symbol` / `name` / `identifier` sind runtime-äquivalent zu `symbolIdentifier` (an T-ID geprüft).

## 8. Roslyn-konforme Wünsche

Alles statisch/Roslyn/Git machbar, kein LLM:

1. **Paging statt „Pattern verfeinern“:** `continuationToken` auf der bereits berechneten Impact-Liste. Footer: „weitere N, Token=…“.
2. **Sortierung/Filter:** Produktion vor Tests, oder `scopeType` (`production`/`tests`/`all`). Default darf nicht 50 Testhits auf einem Infrastrukturtyp sein.
3. **Treffer mit Symbol-ID:** jede Call-Site als `file:line` **und** `id` (`T:`/`M:`/containing method), damit `get_symbol_body` folgt.
4. **`gitRef`-Semantik dokumentieren und trennen:** `HEAD` = Working-Tree vs. HEAD; Commit-Impact = `git show`/`git diff parent..ref`. Leertexte splitten: `NO_GIT_REPO` vs. `EMPTY_DIFF`. Completeness-Hinweis nicht auf Leermengen.
5. **Leeres `symbolIdentifier`:** `INVALID_ARGUMENT`, nicht stiller Fallback.
6. **Schema = Runtime:** `detailLevel` Enum; Hinweis dass genau eines von gitRef/symbolIdentifier/omit; Assembly: Symbol Pflicht im Schema, nicht nur im Text.
7. **depth-Completeness ehrlich:** besuchte Knoten, gekappte Knoten, getrennte Gründe (`maxResults` vs. traversal-cap vs. depth).
8. **Assembly:** Call-Sites relativ zum Dekompilat-Stamm oder als `assembly:…:T:`; Datei:Zeile nicht auf `using`-Tokens mappen; Self-Hits/ctor-Duplikate zusammenziehen. Ohne `find_symbol` zumindest einen Typ-Katalog-Hint (öffentliche Typen der Session) statt nur NOT_FOUND.
9. **change-context:** `M:`-IDs an geänderten Symbolen; `maxChangedSymbols`-Truncation explizit (`4/4` war klar, Cap-Hit ungetestet).

## 9. Phase 3 (AiNetLinter-Quellzeiger)

AiNetLinter-Root: `C:\Daten\Entwicklung\Ralf\AiNetLinter`. Nur Nicht-`ok`.

| Befund | Pfad + Symbol | Roslyn-Ansatz |
|---|---|---|
| Schema ohne Enums/`required`; Aliase nur im Fließtext | `src\AiNetLinter\Mcp\Registration\SymbolGraphToolRegistrations.cs` — `AddGetImpact`; `src\AiNetLinter\Mcp\Tools\McpToolRegistrationOptions.cs` — `TargetedReadOnlyTool`. JSON kommt aus der C#-Lambda (`string? gitRef = null`, `string? detailLevel = null`). | Parameter nicht-nullable bzw. `[Required]`; `detailLevel` als Enum/`JsonStringEnumConverter`. Assembly-Zweig: `symbolIdentifier` im Schema Pflicht. Keine zweite Wahrheitsquelle neben der Lambda. |
| Leeres `symbolIdentifier=""` → uncommittete Änderungen | `src\AiNetLinter\Mcp\Tools\SymbolGraph\GetImpactTool.cs` — `GetImpactInput.EffectiveSymbolIdentifier`; `ValidateTargetArguments`. Registration: `effectiveIdentifier = symbolIdentifier ?? symbol ?? identifier ?? name` (leerer String bleibt). Danach `IsNullOrWhiteSpace` → `hasSymbolIdentifier=false`. | Nach Alias-Merge: `string.IsNullOrWhiteSpace` → `INVALID_ARGUMENT`, analog `GetTypeHierarchyTool.ExecuteAsync`. |
| `gitRef=HEAD` = Working-Tree vs. HEAD, nicht Commit-Impact | `src\AiNetLinter\Core\DiffImpactAnalyzer.cs` — `RunGitDiff`. Explizites Ref: `git diff -U0 {gitSinceRef} -- *.cs`. `HEAD` ist also Index/WT gegen HEAD. | Beschreibung in `GetImpactDescription` an `git diff <ref>` koppeln. Commit-Impact: `git diff -U0 {ref}^ {ref} -- *.cs` (eigenes Flag oder dokumentiertes `HEAD~1`). Untracked: vorhandenes `RunGitUntrackedFiles` in denselben Diff-Pfad ziehen oder explizit ausschließen. |
| Leertext vermischt „kein Git“ und „leerer Diff“; Completeness auf Leermenge | `DiffImpactAnalyzer.RunAnalysisAsync` (beide Fälle `null`); `GetImpactTool.ExecuteChangeContextBranchAsync` (ein Satz + `McpSufficiencyHints.Append`). `GitRepositoryLocator.FindRoot` vs. leeres `diffOutput` sind intern schon getrennt. | Zwei Recoverable-Codes: `NO_GIT_REPO` / `EMPTY_DIFF`. Sufficiency-Hint nur bei nicht-leerem Payload (`ChangeContextResponseMapper.BuildEmptyPayload` ohne `Append`). |
| Default-50 = nur Testhits (Pfad-Sortierung) | `src\AiNetLinter\Mcp\Tools\SymbolGraph\CallGraphTraversal.cs` — `TraversalState.CreateResult`: `OrderBy FilePath` ordinal/ignore-case. `Tests.Ama` vor `Sample.Project`. | Vor `Take(maxResults)`: Source-Dokumente mit Testhilfsprojekten nachrangig (`Compilation.AssemblyName` / Pfadsegment `Tests.`), oder `scopeType` analog `search_pattern` (`production`/`tests`/`all`). Ranking über `ISymbol.ContainingAssembly`, nicht LLM. |
| callers-Zeilen ohne Symbol-ID | `src\AiNetLinter\Mcp\Tools\SymbolGraph\TransitiveCallGraphFormatter.cs` — `FormatEntry`; `TransitiveCallSiteEntry.ReachedFromSymbolId` ist schon gesetzt (`CallGraphTraversal.GetStableSymbolId` / `DocumentationCommentId.CreateDeclarationId`). | ID in die Textzeile (`id: T:`/`M:`) und StructuredContent durchreichen. Containing-Member der Call-Site: `semanticModel.GetEnclosingSymbol` (bereits `ResolveEnclosingMemberAsync`). |
| Truncation „Pattern verfeinern“ | `TransitiveCallGraphFormatter.CreateMaxResultsMessage` (Depth=1); Git-callers: `src\AiNetLinter\Mcp\McpTruncation.cs` — `TruncateLines`. `get_impact` hat kein `pattern`. | Tool-eigene Meta-Zeile (ohne Pattern). Optional Offset/`continuationToken` auf der bereits sortierten `ordered`-Liste in `CreateResult` — reine Listenpagination, kein RAG. |
| depth=3: 3393 Treffer **und** Cap 200 | `CallGraphTraversal.MaxRecursionNodes`; `TraversalCompleteness.VisitedNodeCount` vs. `TotalCallSiteCount`. 200 besuchte Symbole × viele Locations. Footer mischt beides. | Footer splitten: `visitedNodes`, `cappedNodes`, `totalCallSites`, `shown`. `TruncatedByNodeLimit` und `TruncatedByMaxResults` getrennt lassen (Felder existieren). |
| Datei:Zeile:Spalte → `using System` | `src\AiNetLinter\Mcp\Tools\SymbolGraph\FindReferencesTool.cs` — `ResolveByPositionAsync` → `SymbolIdentifierResolver.ResolveSymbolAtToken` (`GetDeclaredSymbol` / `GetSymbolInfo`). Spalte 10 auf `using System;` trifft `INamespaceSymbol`. Danach `SymbolFinder.FindReferencesAsync` auf den Namespace. | Für Impact: Token-Kind filtern (`INamedTypeSymbol`/`IMethodSymbol`/`IPropertySymbol`); Namespaces/`using`-Direktiven ablehnen (`INVALID_ARGUMENT` + Hint Spalte auf Typnamen). Zeilen-Fallback filtert Metadata bereits (`ResolveSymbolsOnLine`). |
| Assembly-Self-Hits / ctor-Duplikate | `CallGraphTraversal.AppendReferenceLocations` — jede `ReferenceLocation` inkl. Deklarationsdatei. `CreateCallSiteEntry` nutzt `reference.Definition` (Typ **und** `.ctor` können dieselbe Zeile sein). | Locations in der deklarierenden Syntax des Seeds überspringen (`symbol.DeclaringSyntaxReferences`). Duplikate: `Distinct` auf `(FilePath, Line, ReachedFromSymbolId)`. Pfade relativ zum Dekompilat-Stamm (`PathNormalizer`) statt Cache-Absolutpfad. |
| Leeres `maxResults`/`depth=0` clamped still | `GetImpactTool.ExecuteSymbolBranchAsync`: `MaxResults < 1 ? 1`; `CallGraphTraversal.ExpandAsync`: `Math.Clamp(requestedDepth, 1, 3)`. | `maxResults < 1` → `INVALID_ARGUMENT`. Depth-0-Footer existiert schon (`DepthWasClamped`); Schema-Minimum 1. |
| change-context: keine `M:` in der Textzeile | `GetImpactTool.FormatSymbolLine` druckt `DisplayName`+Datei:Span. `ChangedSymbolPayload.DocumentationCommentId` ist gemappt (`ChangeContextResponseMapper.MapChangedSymbol`). | DocCommentId in die Symbolzeile; Truncation-Footer schon in `BuildTruncationMeta`. |
| Assembly ohne inventarisierte ID | `FindReferencesTool.ResolveByNameAsync` (`SymbolFinder.FindSourceDeclarationsAsync`, `SymbolFilter.TypeAndMember`); Metadata-Typen nur über `TryResolveMetadataTypeAsync` (`Compilation.GetTypeByMetadataName`). Kurzname `BusinessOperation` trifft oft nichts. | Bei `SYMBOL_NOT_FOUND` vorhandenes `SymbolNameMatcher.FindSimilarSymbolNamesAsync` (filter `SymbolFilter.Type`) anhängen — kein Katalog-LLM. |
| `[ComponentRegistration]` ohne Suffix | Dieselbe `ResolveByNameAsync`: `name == lastSegment`, kein Compiler-Attribut-Alias. | Wenn 0 Treffer und Identifier nicht auf `Attribute` endet: zweiten Lookup `lastSegment + "Attribute"` (`INamedTypeSymbol`). Roslyn-Konvention, analog `GetTypeByMetadataName`. |
