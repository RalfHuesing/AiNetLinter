# Finding: `search_pattern`

Audit-Stand: 2026-09-07. Nur Schema-Lookup (`GetDynamicTools` `user-AiNetLinter` / `search_pattern`) plus Live-Calls dieses Tools. Kein anderes MCP-Tool, kein `rg`, kein Build, kein Test.

Ziel: `targetType=project`, `targetPath=C:\Workspace\Sample.Project`.  
Assembly-Call laut Schema unsupported; ein Probe-Call auf eine externe Assembly wurde von Cursor Auto-Review **vor** dem MCP-Server blockiert.

## 1 Schema-Kurzfazit

Beschreibung steuert den **Kernvertrag richtig**: Fallback für Text außerhalb des C#-Symbolgraphs (JS, Razor, Config), `pattern`/`query`/`searchPattern`-Aliase, `isRegex` Tri-State (`null` = auto, Plain-First, Promotion bei 0 Treffern), `scopeType` `all`/`production`/`tests`, `enrichCSharp` opt-in, `targetType=project` + absoluter `targetPath`, Assembly ausdrücklich unsupported.

Was ein Agent aus Schema/Beschreibung **nicht** zuverlässig ableitet:

| Lücke | Wirkung |
|---|---|
| Kein Enum im JSON-Schema für `scopeType`/`targetType`/`isRegex` | Ungültiges `scopeType` wird still als Default geschluckt. |
| Default-Scan ist **Workspace-Text**, nicht „Code“ | `logs/**` und `Release/build/**` zählen voll mit. |
| `fileFilter`/`includePattern(s)` Glob-Semantik | `wwwroot/js` ohne `**/` → 0 Treffer, `[vollstaendig]`-artig. |
| `enrichCSharp` / `contextLines` nur als Felder genannt | In der Agent-Textantwort **kein** `semantic`-Block, keine Extra-Kontextzeilen. |
| Kein Hinweis auf case-insensitive Substring | `@page` trifft `@PageTitleText`. |
| Kein Flood-Hinweis Default `maxResults=50` | 92 128 Treffer, erste Seite = Docs/Rules, nicht die Klasse. |
| Kein Continuation-Token | Footer sagt nur „maxResults erhöhen oder Pattern verfeinern“. |

`pattern` ist im JSON-Schema **nicht required** (Default `null`); der Server antwortet trotzdem `INVALID_ARGUMENT: pattern darf nicht leer sein` plus Hint. Beschreibung und Runtime passen, das JSON-Schema nicht.

## 2 Calls

Alle Calls schnell (kein Timeout). Größen grob nach sichtbarer Textantwort. Footer-Form: `[N Treffer gesamt, M gezeigt]` bzw. Datei-Variante bei `maxFiles`.

| # | Absicht | Parameter | Größe / completeness | Wartezeit |
|---|---|---|---|---|
| 1 | C#-Literal auto, `scopeType=all` | `pattern=DataExecutor`, `isRegex=null` | **92 128** gesamt, 50 gezeigt; erste Hits `.cursor/rules` + `Docs/` | schnell |
| 2 | JS `wwwroot/js` | `pattern=invokeMethodAsync`, `fileFilter=**/wwwroot/js/**` | 78 gesamt, 50 gezeigt; **zuerst `Release/build/wwwroot/js/…`**, danach Quelle | schnell |
| 3 | Razor | `pattern=MudButton`, `fileFilter=*.razor` | 109 gesamt, 50 gezeigt; echte Markup-Hits (`AiChatComposer.razor` u. a.) | schnell |
| 4 | `scopeType=production`, plain | `DataExecutor`, `isRegex=false` | **91 732** gesamt, 50 gezeigt; **enthält `Tests.Ama`** | schnell |
| 5 | `scopeType=tests`, plain | wie 4, `scopeType=tests` | 396 gesamt, 50 gezeigt; nur Testpfade | schnell |
| 6 | Leer | `zzzz-kein-treffer-xyz-pattern-9f3k2` | `0 Treffer`; kein `isError` | schnell |
| 7 | Truncation | `public class`, `isRegex=false`, `maxResults=5` | 5 Zeilen, kein Gesamtwert im Footer | schnell |
| 8 | `enrichCSharp` | `DataExecutor`, `enrichCSharp=true`, `maxResults=20` | 92 128 / 20; **kein `semantic`-Feld** in der Textantwort | schnell |
| 9 | Klassendeklaration | `class DataExecutor`, `fileFilter=*.cs` | 6 Hits: 4 Testdateien + `DataExecutor.cs` / `.OptimisticConcurrency.cs` | schnell |
| 10 | Regex explizit `DataExecutor\?` | `isRegex=true`, `scopeType=production` | 3 Hits inkl. **`Tests.Ama`**, `Tests.Support`, Finding-Markdown | schnell |
| 11 | Auto `DataExecutor?` (C#-Nullable) | `isRegex=null` | identisch Call 10 (Plain-First, kein Quantifier) | schnell |
| 12 | Plain `DataExecutor?` | `isRegex=false` | identisch Call 10/11 | schnell |
| 13 | Auto C#-Aufruf mit `(` | `DataExecutor.ExecuteAsync(` | 16 Hits `dataExecutor.ExecuteAsync(` (case-insensitive, als Plain) | schnell |
| 14 | JS ohne Release | `invokeMethodAsync`, `fileFilter=**/wwwroot/js/**`, `excludePatterns=["Release/**"]`, `scopeType=production` | Quelle `Sample.Project/wwwroot/js/…` (Liste bis `schedulerResourceChromeResize.js`) | schnell |
| 15 | Razor `@inject` plain | `fileFilter=*.razor` | **0 Treffer** | schnell |
| 16 | `enrichCSharp` + `sealed class DataExecutor` | `fileFilter=*.cs` | 2 Testhits (`DataExecutorExternalSystemTransaction…`); **nicht** `partial class DataExecutor`; kein Semantic | schnell |
| 17 | Razor `inject` ohne `@` | `fileFilter=*.razor` | 3 Hits `InjectViewSettingsMenu` / `@if (ShouldInject…)` — nicht DI | schnell |
| 18 | Regex `@inject` | `isRegex=true`, `*.razor` | 0 Treffer | schnell |
| 19 | Zähler nur `.cs` | `DataExecutor`, `fileFilter=*.cs`, `maxResults=5` | **686** gesamt / 5 | schnell |
| 20 | Exclude Release/bin/obj, kein fileFilter | `DataExecutor` | **92 165** / 5 (Flut bleibt) | schnell |
| 21 | Ungültige Regex | `isRegex=true`, `pattern=(unclosed` | `INVALID_ARGUMENT` + Offset + Hint | schnell |
| 22 | Pattern fehlt | nur Target | `INVALID_ARGUMENT: pattern darf nicht leer sein` + Hint | schnell |
| 23 | Assembly (Schema: unsupported) | `targetType=assembly`, externe Assembly | **nicht beim MCP**: Cursor Auto-Review blockt den Call | — |
| 24 | `contextLines=2` + `scope` JS | `scope=Sample.Project/wwwroot/js`, `maxResults=3` | **39** gesamt / 3; **keine** Extra-Kontextzeilen im Text | schnell |
| 25 | Razor `@page` | `fileFilter=*.razor` | 33 / 10; echte `@page "…"` **und** FP `@PageTitleText` | schnell |
| 26 | Razor `@code` | `fileFilter=*.razor` | 2 Hits, beide `Tests.Support` | schnell |
| 27 | Zähler `*.md` | `DataExecutor` | 182 / 3 | schnell |
| 28 | Zähler `*.log` | `DataExecutor` | **91 294** / 3 — `Sample.Project/logs/…` | schnell |
| 29 | Glob ohne `**/` | `fileFilter=wwwroot/js` | **0 Treffer** | schnell |
| 30 | Auto `.Where(` | `scopeType=production` | 91 / 10; echte LINQ-Aufrufe, **inkl. `Tests.Ama`** | schnell |
| 31 | Alias `query` | `query=DataExecutor` | 92 165 / 5, wie Default-Scan | schnell |
| 32 | `enrichCSharp` im Typ-Ordner | `scope=…/Infrastructure/Sql`, `*.cs` | 10 / 5; Klasse sichtbar; **kein Semantic** | schnell |
| 33 | Auto-Promotion `SqlExecut.*or` | `isRegex=null` | Banner `[Auto-Detect: … als Regex]`; **92 191** / 10; FP über `.*` + Wort `oder` | schnell |
| 34 | Plain `SqlExecut.*or` | `isRegex=false` | 0 Treffer + Hinweis auf `isRegex: true` | schnell |
| 35 | Auto `string[]` | `scopeType=production` | 76 / 10; echte `string[]` (Plain-First, `[]` nicht Character-Class) | schnell |
| 36 | Regex `.Where(` | `isRegex=true` | `INVALID_ARGUMENT` unmatched `(` | schnell |
| 37 | `maxFiles=2` + production `.cs` | `DataExecutor` | `81 Dateien … 2 gezeigt` (Datei-Truncation) | schnell |
| 38 | `includePatterns=["*.razor"]` | `MudButton` | 109 / 8, analog `fileFilter` | schnell |
| 39 | Token-Stress | `IDataQueryExecutor`, `*.cs`, `maxResults=2000` | lange Liste ohne Footer „N gezeigt“; wirkt vollständig für `.cs` | schnell |
| 40 | `[Inject]` Code-Behind | `fileFilter=*.razor.cs` | 169 / 10 — erklärt Call 15 | schnell |
| 41 | production + Deklaration | `class DataExecutor`, `*.cs`, `scopeType=production` | **Tests.Logic zuerst**, dann echte `DataExecutor` | schnell |
| 42 | Ungültiges `scopeType` | `scopeType=not-a-scope`, `MudButton` | **kein Fehler**; 119 / 5, Default-Scan | schnell |
| 43 | `maxResponseBytes=500` | `DataExecutor` im Sql-Ordner | **leer** + `[Antwort wegen maxResponseBytes begrenzt …]` | schnell |
| 44 | Aliase `searchPattern` + `includePattern` | `invokeMethodAsync`, `includePattern=*.js` | 245 / 5; zuerst `ClientTests/tests/…` | schnell |
| 45 | `.cs` ohne logs/Release | `excludePatterns=["**/logs/**","Release/**"]` | 686 / 5 — gleich Call 19 | schnell |

Call mit `fileFilter=DataExecutor.cs` + `contextLines` + `enrichCSharp` wurde ebenfalls von Auto-Review blockiert; Befund zu beiden Parametern stützt sich auf Call 8/16/24/32.

## 3 Verdict

**Teilweise wie beschrieben.** Textsuche in `.cs` / `.js` / `.razor` funktioniert, Aliase greifen, Leermenge und kaputte Regex sind klar, Plain-First rettet typische C#-Syntax (`string[]`, `.Where(`, `Foo.Bar(`).

Abweichend vom Vertrag:

- `scopeType=production` **filtert Tests nicht**.
- Ungültiges `scopeType` ist stiller Default, kein `INVALID_ARGUMENT`.
- Default-Korpus enthält **Laufzeitlogs** und **Release-Duplikate**.
- `enrichCSharp` und `contextLines` sind in der Agent-Textantwort nicht nutzbar.
- `fileFilter=wwwroot/js` ohne `**/` ist False Negative mit leerer, „vollständiger“ Antwort.

## 4 Schwere

**`degraded`**

Das Tool ist als JS/Razor/Config-Fallback brauchbar, aber Default-Aufruf und `scopeType=production` sind für einen Agenten **faktisch falsch kalibriert**: 91 k Log-Hits auf einen Typnamen, Production-Treffer aus `Tests.*`, erste 50 Zeilen ohne die definierende Klasse. Dazu `friction` (Glob, Schema-Enums, unsichtbares `enrichCSharp`). Nicht `broken`: gezieltes `fileFilter`/`scope`/`excludePatterns` liefert korrekte Stichproben.

## 5 Nutzbarkeit

**Nur mit Workaround.** Wiederverwenden so:

1. Immer `fileFilter` oder `scope` setzen (`*.cs`, `*.razor`, `**/wwwroot/js/**`).
2. `excludePatterns` für `**/logs/**` und `Release/**`.
3. C#-Syntax-Muster mit `isRegex=false` (sonst `.Where(` explizit = Fehler).
4. `isRegex=null` nur wenn man das Auto-Banner liest; `.*` nach Promotion ist gefährlich.
5. `maxResults` bewusst erhöhen; kein Cursor.

Nicht wiederverwenden: nacktes `pattern=DataExecutor` ohne Filter; `scopeType=production` als Wahrheitsfilter; `fileFilter=wwwroot/js`.

## 6 Bugs, FP/FN vs. Ist

Kein `rg` (Auftrag). Ground Truth: Folge-Calls desselben Tools + bekannter Ankertyp `DataExecutor` unter `Sample.Project/Infrastructure/Data/DataExecutor.cs`.

### Bug A — `scopeType=production` schließt Tests nicht aus (Call 4, 10, 30, 41)

Call 41 (`class DataExecutor`, `*.cs`, production) zeigt zuerst  
`Sample.Project.Tests.Logic/…/DataExecutorOptimisticConcurrencyIntegrationTests`.  
Call 4 listet `Tests.Ama`-Dateien in der Default-Seite. `scopeType=tests` (Call 5) wirkt dagegen eng (396). Der Production-Zweig ist der defekte.

### Bug B — Default-Korpus = Logs (Call 1 vs. 19/27/28)

| Filter | Treffer `DataExecutor` |
|---|---|
| ungefiltert | 92 128–92 165 |
| `*.log` | **91 294** |
| `*.cs` | 686 |
| `*.md` | 182 |

686 + 182 + 91 294 ≈ ungefilterte Summe. Die Zahl ist **kein Zähler-Bug**, sondern Indexierung von `Sample.Project/logs/`. Agent „wo liegt DataExecutor?“ sieht Docs, dann Truncation, nie die Klasse.

### Bug C — Glob ohne `**/` (Call 29 vs. 2/14/24)

`fileFilter=wwwroot/js` → 0.  
`fileFilter=**/wwwroot/js/**` → 78 (inkl. Release).  
`scope=Sample.Project/wwwroot/js` → 39 (Quelle).  
Gleicher Fußangel wie bei `get_file_tree`, in der `search_pattern`-Beschreibung nicht gewarnt.

### Bug D — `enrichCSharp` / `contextLines` unsichtbar (Call 8, 16, 24, 32)

Beschreibung verspricht `semantic`-Feld (`resolved` / `not_applicable` / …) und Kontextzeilen. CallDynamicTool-Text bleibt `pfad:zeile: zeile`. Auch am definierenden Ordner (Call 32: `public sealed partial class DataExecutor(`) keine Anreicherung.

### Bug E — ungültiges `scopeType` ohne Fehler (Call 42)

`not-a-scope` → 119 MudButton-Treffer wie ein All-Scan. Agent glaubt gefiltert zu haben.

### Regex-Autodetect / C#-Syntax (Agentensicht)

| Muster | `isRegex=null` | `false` | `true` |
|---|---|---|---|
| `DataExecutor?` | Plain-First, 3 Literal-Hits `DataExecutor?` | gleich | gleich (explizites `\?` in Call 10) |
| `DataExecutor.ExecuteAsync(` | Plain, 16 Hits, case-insensitive | — | — |
| `.Where(` | Plain, 91 echte LINQ-Hits | — | **INVALID_ARGUMENT** unmatched `(` |
| `string[]` | Plain, 76 echte Arrays (`[]` nicht Character-Class) | — | — |
| `SqlExecut.*or` | **Promotion**, Banner, 92 191; FP `SqlExecution…` + `oder` | 0 + Wildcard-Hinweis | — |

Plain-First erfüllt die Beschreibung und verhindert die schlimmsten C#-False-Positives. Nach Promotion ist `.*` zeilenweit (deutsch „oder“) — das muss ein Agent wissen. Explizites `isRegex=true` an copy-pastetem C# mit `(` ist eine Falle.

### Weitere FP/FN

- **FN durch Truncation + Sortierung:** Default-50 zu `DataExecutor` zeigt Rules/Docs, nicht `Infrastructure/Data/DataExecutor.cs`. Erst `scope`/`class DataExecutor` + `*.cs` findet die Definition (Call 9/32).
- **FP Substring + Case:** `DataExecutor` trifft `IDataQueryExecutor`, `TestDataExecutorFactory`, `dataExecutor`. `@page` trifft `@PageTitleText` / `@PageScopeSelectValue` (Call 25). `sealed class DataExecutor` verfehlt `sealed partial class DataExecutor` und trifft Testhits `DataExecutorExternalSystemTransaction…` (Call 16).
- **FP Duplikat:** JS-Hits doppelt Quelle + `Release/build/wwwroot/js` (Call 2).
- **Kein FN `@inject`:** 0 Hits (Call 15/18) bei 169× `[Inject]` in `*.razor.cs` (Call 40). Plattform-Konvention, nicht Scanner-Blindheit. `@page` / `MudButton` / `@code` in `.razor` funktionieren.
- **Leermenge ehrlich:** Call 6, kein `isError`.

## 7 Token/IDs

Default ohne Filter: Footer behauptet fünfstellige Treffer, zeigt 50 Docs-Zeilen — **hohe Token-Last, niedriger Nutzwert**.

`maxResults=2000` auf `IDataQueryExecutor` + `*.cs` (Call 39): lange, brauchbare Liste; Footer „N gezeigt“ fehlte in der Agent-Antwort (Transport- oder Formatter-Lücke).

`maxResponseBytes=500` (Call 43): harte Kappung, ehrlicher Satz, **null sichtbare Hits**, kein Continuation-Token.

`maxFiles` truncatiert nach Dateien, anderer Footer — das ist verständlich.

**Keine Symbol-IDs**, kein `cursor`. Ausgabe ist `relativer/pfad:zeile: text`. Folge-Call zu `get_symbol_body` / `find_symbol` nur über den Pfad+Namen, den der Agent selbst extrahiert. `enrichCSharp` sollte genau diese Brücke sein und tut es in der sichtbaren Antwort nicht.

StructuredContent laut Beschreibung vorhanden; über `CallDynamicTool` nur Text.

## 8 Roslyn-Wünsche

Statisch/indexerisch, ohne LLM:

- Default-`exclude` oder dokumentierter Default-Ignore für `**/logs/**`, `**/bin/**`, `**/obj/**`, `Release/build/**` (wie ein Code-Suchindex, nicht wie `type` auf der Platte).
- `scopeType=production` über denselben Test-/Production-Split wie der Rest des Servers (Solution-Folder, `*Tests*`, Testdokumente) — und `INVALID_ARGUMENT` für unbekannte Werte.
- Glob: `wwwroot/js` entweder als Prefix-Match oder Warnung „0 Treffer, Glob braucht `**/`“.
- Textformatter: bei `enrichCSharp=true` pro C#-Hit eine Zeile `symbolId` + `resolution` (Roslyn `SemanticModel` am Span); bei `contextLines>0` `±n` Zeilen im Text, nicht nur StructuredContent.
- Truncation: Continuation (`offset` / `cursor`) statt nur „maxResults erhöhen“.
- Schema: `pattern` required; Enums für `scopeType`/`isRegex`; Hinweis case-insensitive.
- Optional: `caseSensitive` (Default derzeit offenbar ignore-case — FP `@page`).

## 9 Phase 3

Quelle: `C:\Daten\Entwicklung\Ralf\AiNetLinter`. Kein Patch in diesem Task.

### Bug A — `scopeType=production` lässt Tests durch (Calls 4, 10, 30, 41) — `degraded`

- **Pfad:** `src\AiNetLinter\Mcp\Tools\Analysis\SearchPatternScanner.Scope.cs`
- **Symbol:** `SearchPatternScanner.IsExcludedByScopeType`
- **Ansatz:** Nur `TestDetector.IsTestFile(filePath)`. `GetHotspotsScanner.MatchesScopeType` nutzt zusätzlich `TestDetector.IsTestProject`. `IsTestFile` matcht `.Tests/` und Suffix `Tests.cs`, nicht Ordner `.Tests.Ama/`. Datei über `GetProjectName` dem Roslyn-`Project` zuordnen, dann `IsTestProject || IsTestFile` (xUnit-Refs / Namenssuffix `Tests`). Unbekannten `scopeType` nicht als `all` schlucken (siehe Bug E).

### Bug B — Default-Korpus Logs/Release (Calls 1, 2, 28) — `degraded`

- **Pfad:** `src\AiNetLinter\Baseline\FileSystemExclusionHelpers.cs` und `SearchPatternScanner.ScanFiles`
- **Symbol:** `FileSystemExclusionHelpers.SearchExcludedDirectories` / `IsSearchExcludedRelativePath`
- **Ansatz:** Walk ist `Directory.EnumerateFiles` über den Solution-Root, nicht `Solution.Documents`. Liste um `logs`, `Release` (ggf. `build`) erweitern — gleiche Segment-Logik wie `obj`/`bin`. Kein Roslyn; Dateisystem-Index analog Code-Suche.

### Bug C — Glob ohne `**/` → 0 Treffer (Call 29) — `friction`

- **Pfad:** `src\AiNetLinter\Mcp\Tools\Analysis\SearchPatternScanner.Scope.cs`
- **Symbol:** `SearchPatternScanner.MatchesPattern` → `FileFilterEvaluator.MatchesGlobForWeb` / `PathGlobMatcher.Matches` (`^…$`)
- **Ansatz:** Muster ohne `*`/`?` wie `MatchesScope` behandeln: exakt oder `relativePath.StartsWith(pattern + "/")`. Alternativ 0-Treffer-Hinweis „Glob braucht `**/`“. `scope=` bleibt Prefix; `fileFilter=` angleichen.

### Bug D — `enrichCSharp` / `contextLines` unsichtbar (Calls 8, 16, 24, 32) — `degraded`

- **Pfad:** `src\AiNetLinter\Mcp\Tools\Analysis\SearchPatternLegacyFormatter.cs` (Text); Anreicherung schon in `SearchPatternRoslynEnricher.cs` / `SearchPatternScanner.CreateMatch`
- **Symbol:** `SearchPatternLegacyFormatter.Format` (nur `FilePath:Line: LineText`); `SearchPatternRoslynEnricher.EnrichAsync` schreibt `SearchPatternMatch.Semantic`; `CreateMatch` füllt `ContextBefore`/`ContextAfter`
- **Ansatz:** Im Textformatter bei `Semantic != null` eine Zeile `symbolId` + `resolution` (`ISymbol.TryGetDocCommentId` in `Resolved`). Bei `contextLines > 0` `±n` aus den schon gebauten Kontextarrays. StructuredContent existiert; Agent sieht nur den Text.

### Bug E — ungültiges `scopeType` ohne Fehler (Call 42) — `friction`

- **Pfad:** `src\AiNetLinter\Mcp\Tools\Analysis\SearchPatternScanner.Scope.cs` und `SearchPatternTool.ValidateArguments`
- **Symbol:** `IsExcludedByScopeType` (`return false` für unbekannte Werte); Vergleich: `GetHotspotsTool` weist mit `INVALID_ARGUMENT` ab
- **Ansatz:** Wie Hotspots: nur `all`/`production`/`tests`, sonst `INVALID_ARGUMENT`. Kein Roslyn.

### Truncation ohne Continuation (Calls 1, 7, 39, 43) — `wish`

- **Pfad:** `src\AiNetLinter\Mcp\Tools\Analysis\SearchPatternScanner.cs` / `SearchPatternLegacyFormatter.cs` / `src\AiNetLinter\Mcp\McpTruncation.cs`
- **Symbol:** `SelectVisibleMatches` (nur `Take(maxResults)`); `AssemblySearchTool` hat bereits `cursor`/`continuationToken`
- **Ansatz:** Offset-Cursor analog Assembly-Suche (`AssemblyPaging.ReadOffset`), nicht LLM. Footer immer `N gesamt / M gezeigt`, auch wenn nur `maxResults` greift (`FormatHitLines` vs. `AppendHints` splitten die Meta-Zeile).

### Schema: `pattern` optional, keine Enums — `friction`

- **Pfad:** `src\AiNetLinter\Mcp\Registration\AnalysisToolRegistrations.cs`
- **Symbol:** `AddSearchPattern` (`string? pattern = null`, `string? scopeType = null`)
- **Ansatz:** Runtime prüft leer bereits in `SearchPatternTool.ValidateArguments`. JSON-Schema: `pattern` required; `scopeType`/`isRegex` als Enum bzw. Tri-State. Hinweis case-insensitive (`FindPlainRanges` = `OrdinalIgnoreCase`, Regex = `CompiledIgnoreCase`). Kein Roslyn.

### FP `@page` / Substring ignore-case (Call 25) — `wish`

- **Pfad:** `src\AiNetLinter\Mcp\Tools\Analysis\SearchPatternScanner.cs`
- **Symbol:** `FindPlainRanges` (`StringComparison.OrdinalIgnoreCase`)
- **Ansatz:** Optionaler `caseSensitive` (Default false belassen). Razor-Token über Roslyn nicht nötig; reiner Textvergleich.
