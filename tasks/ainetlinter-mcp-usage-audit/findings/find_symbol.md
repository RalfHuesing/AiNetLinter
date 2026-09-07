# Finding: `find_symbol`

Namespace: `user-AiNetLinter`. Live-Calls 2026-09-07. Anker: `DataExecutor` (`Sample.Project.Infrastructure.Sql.DataExecutor`) und `[ComponentRegistration]` / `ComponentRegistrationAttribute`.

## 1. Funktion und Schema-Kurzfazit

**Soll laut Toolbeschreibung:** C#-Symbole per Namens-Muster finden, wenn der Ort unbekannt ist. Aliase `namePatterns` / `namePattern` / `pattern` / `symbol` / `query` / `name`. Optional `kind`, `maxResults` (Default 50), `includeReferences` (Default false, nur Assembly-Referenzen). Batch max. 10. Ziel `project` oder `assembly`. Bei 0 Treffern Hinweis auf Textfunde / Fallback `search_pattern`. StructuredContent: `FindSymbolBatchDto`.

**Steuert der Call richtig?** Teilweise. Für den Happy Path reicht `targetType` + `targetPath` + `pattern`. Aliase `name`, `query`, `namePattern` funktionieren. Batch und der Cap bei 11 Patterns sind klar.

**Irreführung:**

- JSON-`required` listet nur `targetType`/`targetPath`. Ohne Muster kommt `INVALID_ARGUMENT: Pflichtparameter 'namePatterns' fehlt` — der Hint nennt nur `namePatterns`, nicht die Aliase `pattern`/`namePattern`/`name`/`query`/`symbol`.
- Beschreibung spricht von **Namens-Substring**. Glob (`DataExecutor*`) und Regex (`^DataExecutor$`) funktionieren trotzdem; das Schema verschweigt das, die Workflow-Rule erwähnt es. Agent rät oder liest die Rule.
- `kind`: Beschreibung „deutsche und englische Werte“ plus Beispiele `Class, Record, Method`. Live akzeptiert `Class` und `class`; `Klasse` ist `INVALID_ARGUMENT` mit Hint nur auf **englische Kleinschreibung** (`class, method, interface, property, record, struct, enum, delegate`).
- `maxResponseBytes` steht im Schema (Default 0), wirkt live nicht (800 Bytes → volle Liste).
- `includeReferences` ist in der Beschreibung an `targetType=assembly` gebunden. Am Projektziel stilles No-Op (kein Fehler, keine Extra-Sektion).
- 0 Treffer: kein `search_pattern`-Fallback-Hinweis, sondern Fuzzy-„Ähnliche Symbole“ (hier MaterialSymbol-*).
- Attribute-Anwendungen (`[ComponentRegistration]`) sind kein Symbolnamen-Treffer. Schema sagt das nicht; ein Agent, der Handler-Klassen sucht, bleibt bei Registry/Attribut-Typ hängen.

## 2. Ausgeführte Calls

Alle Projekt-Calls: `targetType=project`, `targetPath=C:\Workspace\Sample.Project`.  
Assembly: `targetType=assembly`, `targetPath=C:\ExternalAssemblies\Version-9\Example.External.Process.dll`.  
Wartezeit überall **schnell**, kein Timeout. Größen = sichtbarer Markdown-Text (StructuredContent nicht separat sichtbar).

| # | Parameter | Größe (grob) | Truncation / completeness | Wartezeit |
|---|-----------|--------------|---------------------------|-----------|
| 1 | `pattern=DataExecutor`, `maxResults=50` | ~32 Trefferzeilen, ~9–11k Zeichen | Keine Kürzungszeile; Liste vollständig für **Symbolnamen** | schnell |
| 2 | `pattern=ComponentRegistration`, `kind=Class`, `maxResults=20` | 16 Treffer, ~4k | Keine Kürzung | schnell |
| 3 | Assembly `pattern=BusinessOperation`, `maxResults=50` | Header + ~11 Treffer, letzte Signatur mit `externde.Of…` abgeschnitten, ~8k+ | Header: `completeness=partial`, `status=partial`, `origin=decompiled`; Textzeile mittendrin tot | schnell |
| 4 | `pattern=ThisSymbolDoesNotExistAnywhereXyz987`, `maxResults=20` | ~5 Zeilen, ~0,5k | Leer + Ähnlichkeitsvorschläge, kein Timeout | schnell |
| 5 | `namePattern=DataExecutor`, `kind=Class`, `maxResults=10` | 10 von „11“, ~3k | Banner „11 Treffer gesamt, 10 gezeigt“ (hier plausibel) | schnell |
| 6 | `namePatterns=["DataExecutor","ComponentRegistrationAttribute","IDataQueryExecutor"]`, `maxResults=5` | 3 Blöcke, ~2,5k | Erster Block „6 gesamt, 5 gezeigt“ (Zahl falsch, s. Bugs) | schnell |
| 7 | Assembly `namePattern=Record`, `kind=Class`, `includeReferences=true`, `maxResults=15` | 1 sichtbarer Treffer, dann hart abgeschnitten (`externde.Office…`) | `completeness=partial`; Antwort für den Agenten praktisch unbrauchbar | schnell |
| 8 | `pattern=DataExecutor`, `maxResults=3` | 3 Zeilen + Banner, ~1,2k | „4 Treffer gesamt, 3 gezeigt“ — **falsche Gesamtsumme** | schnell |
| 9 | `pattern=DataExecutor`, `kind=Method`, `maxResults=10` | 10 Methoden, ~3k | „11 gesamt, 10 gezeigt“ | schnell |
| 10 | `pattern=^DataExecutor$`, `kind=Klasse`, `maxResults=8` | Fehler ~0,3k | `INVALID_ARGUMENT` unbekannter kind | schnell |
| 11 | Assembly `pattern=IsSpecialOperation`, `includeReferences=true`, `maxResults=8` | 1 Property + Referenzblock 15/15 + 5 Diagnosen, ~2,5k Nutz + Lärm | `includeReferences=true; Assemblies: 15 von 15; Vollständigkeit: partial; Ergebnisse gekürzt: False` + Decompiler-CS-Diagnosen | schnell |
| 12 | `name=ComponentRegistrationAttribute` | 1 Treffer, ~0,4k | vollständig | schnell |
| 13 | `targetPath=…\does-not-exist` | Fehler ~0,6k | `PROJECT_NOT_INITIALIZED` + Bootstrap-JSON (Irreführung: Agent soll `ainetlinter.project.json` anlegen) | schnell |
| 14 | `pattern=^DataExecutor$`, `kind=class`, `maxResults=10` | 2 Treffer (beide Partials derselben Klasse), ~0,6k | vollständig, exakt der Produktionstyp | schnell |
| 15 | `pattern=DataExecutor`, `maxResponseBytes=800`, `maxResults=50` | gleiche Vollliste wie #1 (~10k) | **Limit ignoriert** | schnell |
| 16 | `query=DataExecutor`, `maxResults=5` | wie #8-Familie, 5+Banner | Alias ok; Totale „6/5“ wieder `maxResults+1` | schnell |
| 17 | `pattern=ComponentRegistration`, `kind=record`, `maxResults=10` | 4 Treffer inkl. **Field** | keine Kürzung | schnell |
| 18 | nur `targetType`+`targetPath` | Fehler | Pflicht `namePatterns` | schnell |
| 19 | `pattern=DataExecutor*`, `kind=class`, `maxResults=8` | 6 Klassen (Prefix-Glob) | vollständig für Glob | schnell |
| 20 | Projekt `includeReferences=true`, `pattern=DataExecutor`, `maxResults=5` | wie nackte Suche mit Cap 5 | No-Op für Referenzen | schnell |
| 21 | `namePatterns` 11 Einträge `A`…`K`, `maxResults=2` | Fehler | Cap 10, Hint „auf mehrere Calls aufteilen“ | schnell |
| 22 | Assembly `pattern=^Record$`, `kind=class`, `includeReferences=false`, `maxResults=5` | Header + 1 Klasse, ~0,9k | `completeness=partial` (Session), Trefferliste vollständig | schnell |

Nicht ausgeführt: absichtlich falsche Assembly-Datei — Cursor Auto-review hat den Call blockiert (Pfad weicht vom Pflicht-Target ab). Fehlerpfad dafür über #13 (Projekt) und #10/#18/#21 (INVALID_ARGUMENT) abgedeckt.

## 3. Verdict

**teilweise** — Kernjob (unbekannten C#-Namen lokalisieren, Doc-ID liefern) klappt auf Projekt und externe Assembly. Beschreibung, `kind`, Totale, Ranking, Assembly-Truncation und Attribut-Suche weichen ab.

## 4. Schwere

**degraded**

Nicht `broken`: mit `kind` + `^Name$` kommt man zum Anker. Ohne Workaround (nacktes Substring, kleines `maxResults`, `kind=Class` bei `ComponentRegistration`) ist das Ergebnis formal da, aber für die nächste Agenten-Aktion falsch oder zu groß/zu früh abgeschnitten.

## 5. Nutzbarkeit

**nur mit Workaround**

Braucht man den Produktionstyp `DataExecutor` oder die externe Klasse `Record`: `pattern="^DataExecutor$"` / `"^Record$"` plus `kind=class` (englisch, Klein- oder PascalCase). Nacktes `DataExecutor` + Default/`maxResults=3` liefert Properties/Testfabriken zuerst und **nie** die Klasse. `[ComponentRegistration]`-Handler nicht über dieses Tool suchen (Namen-Substring ≠ Attributanwendung).

## 6. Bugs (reproduzierbar) und FP/FN

### Bugs

1. **Gefälschte Gesamttrefferzahl bei Truncation.** Call #8: `maxResults=3` → „4 Treffer gesamt, 3 gezeigt“. Call #1 ohne Cap: 32 Symbolnamen. Die Totale ist `maxResults+1`, nicht die Ist-Zahl. Agent glaubt, einmal `maxResults` leicht erhöhen reiche.
2. **`kind` undicht.** Call #2 `kind=Class` und Call #17 `kind=record` enthalten `SetupShellViewModel._componentHandlers` (Field, Datei Zeile 26). Records erscheinen unter `kind=Class`. Filter ist Namens-Substring plus laxer Kind, nicht Roslyn-`INamedTypeSymbol`/`TypeKind`.
3. **`kind`-Schema lügt.** `Klasse` → Fehler; Beschreibung verspricht Deutsch. Hint-Liste ≠ Schema-Beispiele (`Class` vs. `class`).
4. **`maxResponseBytes` No-Op.** Call #15 mit 800 Bytes = volle ~10k-Zeichen-Liste.
5. **JSON-Schema `required` unvollständig.** Call #18.
6. **Assembly-Texttruncation ohne Folgeschritt.** Call #3/#7: Signaturen mit `…` tot, kein Cursor, keine „nächste Seite“. Call #7 trotz `maxResults=15` nach einem Treffer weg (lange Cache-Pfade + `assembly:`-IDs).
7. **Assembly-Symbol-IDs instabil.** Dieselbe Property `IsSpecialOperation`: Generation `:1:` (Call #3), `:2:` (Call #11), Klasse `Record` `:3:` (Call #22). Prefixe `assembly:{sha}:{generation}:{docId}` — Generation steigt in der Session. Folge-Calls mit kopierter ID riskieren Mismatch.
8. **Falscher Projekt-Fehlerpfad.** Call #13 auf nicht existierendem Root → `PROJECT_NOT_INITIALIZED` plus Anleitung, dort ein `ainetlinter.project.json` anzulegen (Agent-Falle, kein „Pfad existiert nicht“).

### FP / FN vs. Ist (Stichprobe)

**`DataExecutor` (Projekt)**

- Datei: `Sample.Project/Infrastructure/Data/DataExecutor.cs` Zeile 9 `public sealed partial class DataExecutor` — MCP Call #1/#5/#14: gleicher Pfad, Zeile 9, ID `T:Sample.Project.Infrastructure.Sql.DataExecutor`. **TP.**
- Partial `DataExecutor.OptimisticConcurrency.cs:11` — MCP listet denselben Typ ein zweites Mal (gleiche ID). Kein FP, aber Doppelzeile.
- `rg` `class DataExecutor`: Produktions-Partials + Testklassen `DataExecutorOptimisticConcurrency*` / `DataExecutorExternalSystemTransaction*`. MCP `kind=class` trifft dieselben plus `ThrowingDataExecutor`, `FakeTerminDetailsDataExecutor`, `TestDataExecutorFactory` (Substring im Namen). **FP relativ zu „die Klasse DataExecutor“**, aber konform zu undokumentiertem Substring. Workaround Regex #14: nur die zwei Produktionsdateien. **kein FN** des Typnamens.
- `rg` trifft `DataExecutor` in 100+ Dateien (Verwendungen). MCP sucht **Deklarationsnamen**, nicht Referenzen. Kein FN des Tools, aber leicht mit `find_references` verwechselt. Schema sagt „Fundstelle(n) von C#-Symbolen“, nicht „alle Vorkommen im Text“.

**`[ComponentRegistration]` / `ComponentRegistrationAttribute`**

- Datei `Contracts/ComponentRegistrationAttribute.cs:8` — MCP Call #12: Zeile 8, ID `T:Sample.Project.Contracts.ComponentRegistrationAttribute`. **TP.**
- `rg` `[ComponentRegistration`: u. a. `FormComponentHandler`, `GenericSchedulerHandler`, Domain-Handler, Test-Mocks. **Kein einziger dieser Typen** in Call #2/#12. **FN**, wenn die Agentenfrage „welche Handler haben `[ComponentRegistration]`?“ ist. TP nur für Typen, deren **Name** `ComponentRegistration` enthält (`ComponentHandlerRegistry`, `ComponentRegistrationAttribute`, …).
- Call #17: `ComponentRegistrationContext` Record Zeile 13 — Datei `public sealed record ComponentRegistrationContext(` Zeile 13. **TP.** Field `_componentHandlers` unter `kind=record`: **FP.**

**Assembly `Record`**

- Dekompilat `…\Example.External.Process\Record.cs` Zeile 31 `public class Record` — MCP Call #22: Zeile 31, ID `assembly:…:3:T:Example.External.Process.Record`. **TP** gegen Cache-Ist. Header `confidence=medium`, `completeness=partial` ist ehrlich für Decompiler-Session, nicht für die eine Klasse.

**Leer**

- Unbekanntes Symbol: 0 Treffer, kein Crash. Fuzzy-Nachbarn sind keine FPs der Suche, können aber als Treffer gelesen werden.

## 7. Token-Effizienz und Folge-Call-Tauglichkeit

**Projekt, nacktes Substring:** hohe Last. Default 50 listet Properties, Testmethoden `CreateDataExecutor*`, Nested Test-Fakes — der gesuchte Typ steht **hinten** (Pfad-Sortierung: `Domain.*` / `Tests.*` vor `Sample.Project/Infrastructure`). Kleines `maxResults` spart Tokens, **verliert den Anker** und lügt bei der Totale.

**Regex+kind:** tokenarm, IDs sofort nutzbar. Project-IDs sind stabile DocIds (`T:…`, `P:…`, `M:…`, `F:…`).

**Batch `namePatterns`:** eine Antwort, getrennte Überschriften, gut für Folge-`get_symbol_body`. `maxResults` gilt **pro** Pattern (DataExecutor-Block wurde gekürzt, Attribute nicht).

**Assembly:** jeder Treffer trägt den vollen Cache-Pfad (`C:\Daten\Tools\AiNetLinter-win-x64\cache\asm.cursor\…\generation-…\…cs`) plus `assembly:{64 hex}:{gen}:{docId}`. `includeReferences=true` hängt 15 Assemblies, `partial`, und Decompiler-Diagnosen (`CS0535` Collection/IList, `CS0227` unsafe, `CS1525` ref) — für Symbolsuche Lärm. `Ergebnisse gekürzt: False` trotz Call-#7-Texttod.

**StructuredContent:** Beschreibung verspricht `FindSymbolBatchDto`. In der Agenten-Oberfläche nur Markdown; IDs stehen in Backticks — für Copy-Paste reicht das auf Projekt. Assembly-IDs wegen Generation **nicht** sessionstabil.

**Hints:** Truncation-Banner „Pattern verfeinern oder maxResults erhöhen“ ist nützlich, aber mit falscher Totale gefährlich. Fehler-Hints zu `kind` und Batch-Cap sind brauchbar. 0-Treffer-Hint auf `search_pattern` fehlt.

## 8. Roslyn-konforme Wünsche

- `kind` als `SymbolKind`/`TypeKind` hart filtern; Fields nicht unter `class`/`record`. Deutsche Aliase weglassen oder wirklich mappen; Schema = Hint-Liste.
- Gesamttreffer = echte `IEnumerable`-Count (oder `truncated=true` ohne Fake-`max+1`). Continuation/`offset` statt nur „maxResults erhöhen“.
- Default-Ranking: exakter Identifier-Match und `INamedTypeSymbol` vor Membern/Tests; oder `nameEquals` neben Substring.
- Schema: Regex/Glob dokumentieren (`^…$`, `*`); `required` um ein Musterfeld; `maxResponseBytes` implementieren oder entfernen.
- Assembly-IDs ohne Session-`generation` (SHA + DocId reichen); Trefferzeilen relative Dekompilat-Pfade, nicht 200-Zeichen-Cache-Roots.
- `includeReferences`: Diagnosen nicht in die Symboliste mischen; Compact-Flag oder nur `completeness`.
- Optional Roslyn: Attribut-Anwendungen von `ComponentRegistrationAttribute` als eigener `kind`/Schalter (`GetAttributes()`), damit `[ComponentRegistration]` Handler findet — das ist statisch, kein LLM.
- 0 Treffer: nicht MaterialSymbol-Fuzzy als Ersatz für `search_pattern`-Hinweis.
- `PROJECT_NOT_INITIALIZED` nur bei fehlender Integration im **kanonischen** Root; unbekannter Pfad → `Pfad nicht gefunden`.

## 9. Phase 3

AiNetLinter-Repo `C:\Daten\Entwicklung\Ralf\AiNetLinter`, read-only. Je Nicht-ok-Befund: Pfad, Symbol, Roslyn-Ansatz.

### degraded — Gefälschte Gesamttrefferzahl (`maxResults+1`)

- **Pfad:** `src\AiNetLinter\Mcp\Tools\SymbolGraph\FindSymbolScanner.cs`
- **Symbol:** `FindSymbolScanner.FindMatchesWithEntriesAsync`
- **Ansatz:** `hasMore ? request.MaxResults + 1 : collectedEntries.Count` durch echte Zählung ersetzen: nach dem `maxResults`-Schnitt die restlichen `ISymbol.Locations` (nur `IsInSource`) weiterzählen, oder vor dem Schnitt `filtered.SelectMany(s => s.Locations.Where(l => l.IsInSource)).Count()` als `totalMatches` an `McpTruncation.TruncateLines` geben. `truncated=true` nur wenn `total > shown`. Kein Sentinel `max+1`.

### degraded — `kind` undicht (Field unter `class`/`record`; Record unter `Class`)

- **Pfad:** `src\AiNetLinter\Mcp\Tools\FileStructure\SymbolKindClassifier.cs`
- **Symbol:** `SymbolKindClassifier.MatchesSymbolKind` / `MatchesTypeKind`
- **Ansatz:** Fallback `return true` für Nicht-Typ/Nicht-Method/Nicht-Property (u. a. `IFieldSymbol`) entfernen → `false`. `kind=class`: `type.TypeKind == TypeKind.Class && !type.IsRecord`. `kind=record` nur `type.IsRecord`. Optional `kind=field` → `symbol.Kind == SymbolKind.Field`. Filter weiter über `FindSymbolScanner.FilterByKind`.

### friction — `kind`-Schema vs. Runtime (Deutsch, Hint-Liste)

- **Pfad:** `src\AiNetLinter\Mcp\Registration\SymbolGraphToolRegistrations.cs` und `src\AiNetLinter\Mcp\Tools\SymbolGraph\FindSymbolTool.cs`
- **Symbol:** `SymbolGraphToolRegistrations.FindSymbolDescription`; `FindSymbolTool.ValidateKind` / `ValidKinds`
- **Ansatz:** Beschreibung und Hint auf dieselbe englische Menge wie `ValidKinds` ziehen (`class`, `method`, `interface`, `property`, `record`, `struct`, `enum`, `delegate`). Deutsche Aliase entweder hart in `ValidateKind` mappen (`Klasse`→`class`) oder den Satz „deutsche und englische Werte“ streichen. Schema-Beispiele = Hint.

### friction — JSON-`required` ohne Muster; Alias-Hint nur `namePatterns`

- **Pfad:** `src\AiNetLinter\Mcp\Registration\SymbolGraphToolRegistrations.cs`; `src\AiNetLinter\Mcp\Tools\SymbolGraph\FindSymbolTool.cs`
- **Symbol:** `SymbolGraphToolRegistrations.AddFindSymbol`; `FindSymbolTool.ValidateNamePatterns`
- **Ansatz:** MCP-SDK leitet `required` aus der Lambda ab (`targetType`/`targetPath` Pflicht, Muster optional). Fehlertext und `McpToolResults.NamePatternsBatchHint` um Aliase `pattern` / `namePattern` / `name` / `query` / `symbol` ergänzen. Optional Schema-Overlay `anyOf` mit mindestens einem Musterfeld — keine zweite Parser-Logik neben `NormalizeNamePatterns`.

### friction — Glob/Regex in der Beschreibung verschwiegen

- **Pfad:** `src\AiNetLinter\Mcp\Tools\SymbolGraph\SymbolNameMatcher.cs`; `src\AiNetLinter\Mcp\Registration\SymbolGraphToolRegistrations.cs`
- **Symbol:** `SymbolNameMatcher.CreatePredicateForSimplePattern`; `FindSymbolDescription`
- **Ansatz:** Ist bereits Roslyn-tauglich (`*`/`?` über `RegexAutoDetector.ConvertWildcardToRegex`, `^…$` über `IsLikelyRegex`). Nur Beschreibung anpassen: Substring **oder** Glob **oder** Regex. Kein neuer Matcher.

### friction — `includeReferences` am Projektziel stilles No-Op

- **Pfad:** `src\AiNetLinter\Mcp\Registration\SymbolGraphToolRegistrations.cs`
- **Symbol:** `SymbolGraphToolRegistrations.AddFindSymbol`
- **Ansatz:** `ExpandAssemblyReferences` gilt nur in `AssemblyAnalysisDispatcher`. Bei `targetType=project` und `includeReferences=true`: `INVALID_ARGUMENT` mit Hint „nur assembly“, analog Assembly-Pfadprüfung in `AnalysisTargetResolver.Resolve`.

### degraded — `maxResponseBytes` No-Op (Projekt)

- **Pfad:** `src\AiNetLinter\Mcp\AnalysisToolCall.cs`; `src\AiNetLinter\Mcp\Registration\SymbolGraphToolRegistrations.cs`
- **Symbol:** `ProjectAnalysisDispatcher.ExecuteAsync`; `AnalysisToolDispatch.MaxResponseBytes`
- **Ansatz:** Parameter wird nur in `AssemblyAnalysisDispatcher` → `AssemblyAnalysisResponse.Enrich` verbraucht. Entweder Budget auch auf der Projektroute anwenden (nach `FindSymbolTool.ExecuteAsync`, Zeilengrenzen wie `McpTruncation`) oder das Feld aus der `find_symbol`-Lambda entfernen. Kein stilles Ignorieren.

### degraded — Assembly-Texttod ohne Folgeschritt; „Ergebnisse gekürzt: False“

- **Pfad:** `src\AiNetLinter\Mcp\Assemblies\Analysis\AssemblyAnalysisResponse.cs`; `src\AiNetLinter\Mcp\Tools\SymbolGraph\AssemblyFindSymbolTool.cs`
- **Symbol:** `AssemblyAnalysisResponse.ApplyWireBudget` / `TrimUtf8`; `AssemblyFindSymbolTool.BuildResponseAsync`
- **Ansatz:** Wire-Cut nicht mitten in einer Signatur (`TrimUtf8` + `…`). Vor dem Byte-Schnitt an letzter vollständiger Trefferzeile kappen, `ResultsTruncated=true` setzen, `continuationToken` über bestehendes `AssemblyPaging` ausgeben. Footer nicht `Ergebnisse gekürzt: False`, wenn der Envelope danach gekürzt wurde. Relative Dekompilat-Pfade in `FindSymbolTool.FormatSymbolLocationEntries` (`absolutePaths: false` auch für Assembly, Stamm = `origin.GeneratedDocumentPath`).

### degraded — Assembly-Symbol-IDs mit Session-`generation`

- **Pfad:** `src\AiNetLinter\Mcp\AnalysisSymbolIdentity.cs`; `src\AiNetLinter\Mcp\Assemblies\Analysis\AssemblyAnalysisRegistry.cs`; `src\AiNetLinter\Mcp\Tools\SymbolGraph\SymbolIdentifierResolver.cs`
- **Symbol:** `AnalysisSymbolIdentity.Format`; `AssemblyAnalysisRegistry` (`nextGenerations`); `SymbolIdentifierResolver.TryNormalizeAssemblyId` / `StaleAssemblyId`
- **Ansatz:** Wire-ID = `assembly:{ContentHash}:{DocCommentId}` ohne Generation. Generation intern am Cache/Lease halten. Lookup: SHA matcht → aktuelle Session; SHA mismatch → `SYMBOL_NOT_FOUND`. `StaleAssemblyId` nicht mehr an Generation koppeln. `DocumentationCommentId.CreateDeclarationId` bleibt die stabile Nutzlast (`FindSymbolTool.FormatSymbolLocationEntries`).

### degraded — `includeReferences`: Decompiler-Diagnosen in der Symboliste

- **Pfad:** `src\AiNetLinter\Mcp\Tools\SymbolGraph\AssemblyFindSymbolTool.cs`
- **Symbol:** `AssemblyFindSymbolTool.BuildResponseAsync`
- **Ansatz:** `summary.Diagnostics` nicht in denselben Markdown-Block wie Trefferzeilen schreiben. Completeness-Zeile (`Assemblies: n von m`) behalten; Diagnosen nur in `FindSymbolBatchDto.Navigation` / StructuredContent, Default-Text aus. Treffer weiter über `AssemblySymbolSearch.FindMatchesAsync` + `SymbolFinder.FindSourceDeclarationsAsync`.

### friction — 0 Treffer: Fuzzy statt `search_pattern`-Hinweis

- **Pfad:** `src\AiNetLinter\Mcp\Tools\SymbolGraph\FindSymbolScanner.cs`
- **Symbol:** `FindSymbolScanner.AppendMissHintAsync` / `FormatMissMessageAsync`
- **Ansatz:** `search_pattern`-Hinweis immer anhängen (auch wenn `missScan.Files.Count == 0`). `SymbolNameMatcher.FindSimilarSymbolNamesAsync` optional hinter den Hint, klar als „ähnliche C#-Namen“, nicht als Trefferliste. Vorhandener Pfad `SearchPatternLegacyFileHitScanner.Scan` bleibt.

### friction — `PROJECT_NOT_INITIALIZED` für nicht existierenden Root

- **Pfad:** `src\AiNetLinter\Mcp\Projects\ProjectDefinitionLoader.cs`; `src\AiNetLinter\Mcp\AnalysisTargetResolver.cs`
- **Symbol:** `ProjectDefinitionLoader.Load`; `AnalysisTargetResolver.Resolve`
- **Ansatz:** Vor `File.Exists(ainetlinter.project.json)`: `Directory.Exists(projectRoot)` — fehlt das Verzeichnis → `PATH_NOT_FOUND` / `INVALID_ARGUMENT` ohne Bootstrap-JSON (Assembly-Zweig prüft Dateiexistenz bereits). `PROJECT_NOT_INITIALIZED` + `NotInitializedTemplate` nur wenn der Root existiert, die Definitionsdatei aber fehlt.

### degraded — Ranking: Substring listet Member/Tests vor dem Anker-Typ

- **Pfad:** `src\AiNetLinter\Mcp\Tools\SymbolGraph\FindSymbolScanner.cs`
- **Symbol:** `FindSymbolScanner.FindMatchesWithEntriesAsync`
- **Ansatz:** Nach `FilterByKind` sortieren, bevor `maxResults` schneidet: (1) `string.Equals(symbol.Name, cleanPattern, OrdinalIgnoreCase)`, (2) `INamedTypeSymbol` vor Membern, (3) `!TestDetector.IsTestFile(location.SourceTree.FilePath)` (`AiNetLinter.Core.TestDetector`, analog `SearchPatternScanner.IsExcludedByScopeType`). Dann Dateipfad. Optional `scopeType=production|tests|all` wie `search_pattern`.

### wish — `[ComponentRegistration]`-Anwendungen über `GetAttributes()`

- **Pfad:** `src\AiNetLinter\Mcp\Tools\SymbolGraph\FindSymbolScanner.cs` (neuer Filterzweig); Vorbild `src\AiNetLinter\Mcp\Tools\FileStructure\GetNamespaceTreeScanner.cs`
- **Symbol:** `FindSymbolScanner.FindMatchesWithEntriesAsync`; `GetNamespaceTreeScanner.IsCompilerGenerated` (nutzt bereits `INamedTypeSymbol.GetAttributes()`)
- **Ansatz:** Optionaler `kind=attribute` oder Schalter `attributeUsages=true`: Attributtyp per bestehendem Matcher auflösen, dann `compilation.GlobalNamespace` / Quelltypen mit `type.GetAttributes()`, `AttributeClass` per `SymbolEqualityComparer` gegen den Attributtyp (inkl. `Name + "Attribute"`). Ergebnis: deklarierende Typen mit DocCommentId. Rein Roslyn, kein LLM.
