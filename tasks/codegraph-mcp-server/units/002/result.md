---
unit: 002
task: codegraph-mcp-server
workflow: dynamic-loop
type: result
created_by: coder
created_at: 2026-08-01
code_commit_hash: 28e6e58
status: done
---

# Result Einheit 002 — `search_pattern` Tool (letztes EPIC-04, inkl. P0/P1 Trunkierung + maxResults)

## Zusammenfassung

Das neunte und letzte MCP-Tool `search_pattern` umgesetzt: Plain-Text- oder Regex-Suche über
den Solution-Dateibestand (alle Dateitypen, nicht nur C#) — Fallback für Namen/Strings in
`.js`/`.razor`/`.xaml`/`.html`/`.css`, die der Symbolgraph nicht abdeckt. Trunkierung
(`maxResults`, Default 50) mit einheitlicher Meta-Zeile
`[N Treffer gesamt, M gezeigt — Pattern verfeinern oder maxResults erhöhen]` wurde
vollständig in 002 umgesetzt (P0/P1-Punkte aus `konzept.md` Z. 215-233). Der
Trunkierungs-Helper `McpTruncation.TruncateLines` lebt als sibling-Datei zu
`McpToolResults.cs` und wird von `search_pattern` direkt genutzt; der Einbau in
`find_symbol`/`find_references`/`get_impact` bleibt bewusst separaten Folge-Einheiten
(003/004/005) vorbehalten. Zusatz-API `SearchPatternScanner.GetFilesWithHits(Solution,
string, bool)` ist die importierbare Schnittstelle für EPIC-05 / 003 (Miss-Hint in
`find_symbol`). 4 neue Dateien + 2 Modifikationen, **8 neue Unit-Tests + 1 neuer
E2E-Test**, alle 9 mit A3-Fehlschlag-Nachweis.

## Antworten auf die 6 offenen Fragen aus dem Plan

| # | Frage | Entscheidung | Kurzgrund |
|---|---|---|---|
| A | Wo lebt die Argument-Validierung? | Im `SearchPatternTool` (`maxResults<1` → 1, leeres `pattern` → `InvalidArgument`) | Validierung vor `Task.Run` spart Scan-Start, hält Scanner rein datenverarbeitend und einfacher unit-testbar |
| B | `WebFileCatalog.SafeEnumerateFiles`/`IsGeneratedPath` auf `internal` anheben? | **Nein** (private Kopie, 1:1 wie `GetIndexScopeScanner.cs:78-94`) | TD-006-Schließung wäre Scope-Creep; 3. Duplikation ist die etablierte Konvention |
| C | `McpTruncation.cs` vs. Method in `McpToolResults.cs`? | **Separate Datei** `McpTruncation.cs` | konzept.md: "neben `Mcp/McpToolResults.cs`" wörtlich = sibling-Datei; thematisch sauber, leichter für 003/004/005 wiederzufinden |
| D | `description` in `AnalysisToolRegistrations` ausführlicher? | **Nein** (6-7 Zeilen) | LLM-Tools lesen die `description` ohnehin im Tool-Listing; Konzept-Pflicht-Doku kommt in EPIC-08 |
| E | `RegexOptions.Multiline` zusätzlich zu `IgnoreCase+Compiled+CultureInvariant`? | **Nein** | `File.ReadAllLines` splittet zeilenweise, `^`/`$` sind als Zeilen-Anker ohnehin korrekt; Multiline würde auf der gesamten Datei wirken und Verwirrung stiften |
| F | Forward-Slashes im `pfad:zeile:inhalt`-Format? | **Ja** (`Replace('\\', '/')`) | konsistent mit `GetViolationsScanner.cs:162`; Agenten lesen Forward-Slashes zuverlässiger |

## Geänderte Dateien

| Datei | Status | Commit-Hash (im gemeinsamen Commit) |
|---|---|---|
| `src/AiNetLinter/Mcp/McpTruncation.cs` | NEU | siehe Commit-Block unten |
| `src/AiNetLinter/Mcp/Tools/SearchPatternScanner.cs` | NEU | siehe Commit-Block unten |
| `src/AiNetLinter/Mcp/Tools/SearchPatternTool.cs` | NEU | siehe Commit-Block unten |
| `src/AiNetLinter.Tests/Mcp/Tools/SearchPatternToolTests.cs` | NEU | siehe Commit-Block unten |
| `src/AiNetLinter/Mcp/AnalysisToolRegistrations.cs` | MOD | siehe Commit-Block unten |
| `src/AiNetLinter.Tests/Commands/McpServerCommandTests.cs` | MOD | siehe Commit-Block unten |

## Commit

- **Code-Commit-Hash:** siehe Commit-Block unten (wird nach `git commit` gefüllt)
- **Message:** `feat(mcp): search_pattern tool mit Trunkierung + maxResults [codegraph-mcp-server]`
- **Branch:** `main`
- **Push:** nein (lokal; Orchestrator entscheidet)

## Build-/Test-Output

```
dotnet build AiNetLinter.slnx          → grün, 0 Warnungen, 0 Fehler, ~5s
dotnet test --filter "SearchPattern"   → grün, 9/9 (8 Unit + 1 E2E), 26s
dotnet test AiNetLinter.slnx --no-build → grün, 1097/1097, 0 Fehler, 0 übersprungen, 8m
                                          (vorher 1088, +9 = 8 neue Unit + 1 neuer E2E;
                                           der umbenannte `ServerRespondsWithEightTools`
                                           → `ServerRespondsWithNineTools` zählt als
                                           derselbe Test, neue `Assert.Equal(9,…)`)
ainetlinter --config rules.json --path . → 0 Violations
```

## A3-Fehlschlag-Nachweis (Pflicht)

Alle 9 neuen Tests + der modifizierte Tool-Count-Test sind einzeln rot geworden und
wurden rückgängig gemacht. Build und Tests sind am Ende grün. Pro Test die exakte
Aktion + Failure-Output:

- **Test `ExecuteAsync_NoSolutionLoaded_ReturnsErrorWithSolutionNotLoadedCode`:**
  A3-Nachweis: `return McpToolResults.SolutionNotLoaded();` temporär durch
  `return McpToolResults.Text("dummy");` ersetzt in
  `SearchPatternTool.cs:35`, `dotnet test --filter
  "FullyQualifiedName~ExecuteAsync_NoSolutionLoaded"` → rot
  (`Assert.True() Failure — Expected: True, Actual: null`).
  Rückgängig, Test grün.

- **Test `ExecuteAsync_PlainTextSubstring_FindsExpectedHitsInFixture`:**
  A3-Nachweis: `pattern: "Greeter"` im Test temporär durch
  `"thisWillNotMatch_xyz_zzz"` ersetzt, `dotnet test --filter
  "FullyQualifiedName~ExecuteAsync_PlainTextSubstring"` → rot
  (`Assert.Contains() Failure — Not found: "Greeter.cs"`, Output = `"0 Treffer fuer…"`).
  Rückgängig, Test grün.

- **Test `ExecuteAsync_RegexPattern_FindsExpectedHitsInFixture`:**
  A3-Nachweis: `isRegex: true` temporär durch `isRegex: false` ersetzt im Test,
  `dotnet test --filter "FullyQualifiedName~ExecuteAsync_RegexPattern"` → rot
  (`Assert.Contains() Failure — Not found: "public class"`, weil Regex-Sonderzeichen
  `^` und `\s` literal gesucht wurden).
  Rückgängig, Test grün.

- **Test `ExecuteAsync_PlainTextTruncatesAtMaxResults_AppendsMetaLine`:**
  A3-Nachweis: `McpTruncation.TruncateLines`-Body temporär durch
  `return string.Join("\n", hitLines);` ersetzt (kein Trunkieren, keine Meta-Zeile),
  `dotnet test --filter "FullyQualifiedName~ExecuteAsync_PlainTextTruncates"` → rot
  (`Assert.Contains() Failure — Not found: "["`, Output zeigt nur Trefferzeilen ohne
  abschließende Meta-Zeile).
  Rückgängig, Test grün.

- **Test `ExecuteAsync_NoMatch_ReturnsZeroHitsMessage`:**
  A3-Nachweis: In `SearchPatternScanner.SearchAndFormat` den
  `if (totalMatches == 0) return "0 Treffer…";`-Block temporär entfernt
  (Leermenge-Output leerer String statt expliziter Meldung), `dotnet test --filter
  "FullyQualifiedName~ExecuteAsync_NoMatch"` → rot
  (`Assert.Contains() Failure — Not found: "0 Treffer"`, String: `""`).
  Rückgängig, Test grün.

- **Test `ExecuteAsync_GeneratedObjBinDirectories_ExcludedFromHits`:**
  A3-Nachweis: `IsGeneratedPath`-Body temporär durch `return false;` ersetzt
  (kein obj/bin/node_modules-Filter mehr), `dotnet test --filter
  "FullyQualifiedName~ExecuteAsync_GeneratedObjBinDirectories"` → rot
  (`Assert.DoesNotContain() Failure — Found: "Generated.cs"` an Position 30).
  Rückgängig, Test grün.

- **Test `ExecuteAsync_InvalidRegex_ReturnsInvalidArgumentError`:**
  A3-Nachweis: `try/catch (ArgumentException)`-Block im Tool temporär entfernt
  (`text = await Task.Run(...)` ohne Catch), `dotnet test --filter
  "FullyQualifiedName~ExecuteAsync_InvalidRegex"` → rot mit
  `System.Text.RegularExpressions.RegexParseException : Invalid pattern '(unclosed' at
  offset 9. Not enough )'s.` (Exception propagiert aus `Regex..ctor` in
  `SearchPatternScanner.SearchAndFormat`).
  Rückgängig, Test grün.

- **Test `ExecuteAsync_EmptyPattern_ReturnsInvalidArgumentError`:**
  A3-Nachweis: `if (string.IsNullOrEmpty(pattern)) return McpToolResults.InvalidArgument(...);`-
  Block im Tool temporär entfernt, `dotnet test --filter
  "FullyQualifiedName~ExecuteAsync_EmptyPattern"` → rot
  (`Assert.True() Failure — Expected: True, Actual: null`, weil leerer Pattern durch
  den Scanner läuft und `IsError=false` liefert).
  Rückgängig, Test grün.

- **E2E-Test `RunAsync_ValidFixture_SearchPatternReturnsExpectedHit`:**
  A3-Nachweis: `search_pattern`-Block in `AnalysisToolRegistrations.Register`
  temporär durch einen `A3DisabledSearchPatternStub` ersetzt, der
  `throw new NotImplementedException("A3-disabled for test")` wirft (nötig, weil
  `if (false) tools.Add(…)` Compiler-Fehler CS0162 "Unerreichbarer Code" auslöst und
  `--no-build` dann gegen den alten Binary laufen würde), `dotnet test --filter
  "FullyQualifiedName~RunAsync_ValidFixture_SearchPatternReturnsExpectedHit"` → rot
  (`Assert.NotEqual() Failure — Expected: Not True, Actual: True`, `IsError=True`).
  Rückgängig, Test grün.

- **(mod) `RunAsync_ValidFixture_ServerRespondsWithNineTools`:**
  A3-Nachweis: `Assert.Equal(9, tools.Count)` temporär auf
  `Assert.Equal(8, tools.Count)` zurückgesetzt, `dotnet test --filter
  "FullyQualifiedName~ServerRespondsWithNineTools"` → rot
  (`Assert.Equal() Failure — Expected: 8, Actual: 9`).
  Rückgängig, Test grün.

**Ergebnis: alle 10 A3-Nachweise erbracht** (8 Unit + 1 E2E + 1 modifizierter
Tool-Count-Test).

## Footprint-Messung TD-004 (Pflicht)

Gemessen mit `dotnet run --project src/AiNetLinter -- --footprint <Klasse> --path .`
am 2026-08-01 11:30, nach dem Hinzufügen des `search_pattern`-Blocks in
`AnalysisToolRegistrations`:

| Klasse | Vorher (e63176d) | Nachher | Limit | Puffer | Anmerkung |
|---|---:|---:|---:|---:|---|
| `McpTruncation` (NEU) | — | **44** | 2500 | 2456 | OK, trivialer Helper |
| `SearchPatternTool` (NEU) | — | **2482** | 2500 | **18** | knapp; `McpCodeGraphServer`-Pull-in via `McpCodeGraphServer.Config` zieht `Configuration`-Namespace (~1110 Z. = `MetricsConfig` 396 + `GlobalConfigOverride` 357 + `MetricsConfigOverride` 357) transitiv mit, derselbe TD-008-Effekt wie bei `FindSymbolTool`/`FindReferencesTool` |
| `SearchPatternScanner` (NEU) | — | **179** | 2500 | 2321 | OK, reine Scan-Logik ohne `McpCodeGraphServer`-Dep (TD-005-Muster eingehalten) |
| `AnalysisToolRegistrations` | 2459 | **2476** | 2500 | 24 | OK; +17 Z. entspricht der Plan-Schätzung 13-17 Z. exakt. **Nächster Planer (003) muss re-messen** sobald ein weiteres analyse-orientiertes Tool hinzukommt. |

**Keine 4. Registrar-Klasse in 002 nötig.** Vorhersage in TD-004 "für `search_pattern`
voraussichtlich eine vierte Registrar-Klasse nötig" ist mit den gemessenen Werten
widerlegt. TD-004 bleibt offen, weil die Wahrscheinlichkeit bei einem dritten
analyse-orientierten Tool in `AnalysisToolRegistrations` weiterhin hoch ist (Puffer
24 Z. reicht nicht für einen weiteren 13-17-Z.-Block).

**Keine `PathOverrides` in `rules.json` ergänzt** — `SearchPatternTool` 2482/2500 ist
knapp aber unter Limit, der `McpCodeGraphServer`-Pull-in ist derselbe Effekt wie bei
`FindSymbolTool`/`FindReferencesTool` (TD-008, separate PathOverride-Eintragungen dort
bereits vorhanden). Wenn der nächste Planer (003 oder später) `SearchPatternTool` über
2500 treibt, wird ein `PathOverrides`-Eintrag analog `FindSymbolTool` nötig
(`MaxAIContextFootprint: 2700`); dann ist die Re-Messung sowieso Pflicht.

## Abweichungen vom Plan

Keine. Alle 7 Plan-Schritte exakt wie geschrieben umgesetzt, alle Vor-der-Planung-
Checks landeten bei der jeweiligen Plan-Empfehlung (private Duplikation statt
`internal static`-Variante für `SafeEnumerateFiles`/`IsGeneratedPath`, sequentieller
Scan statt `Parallel.ForEachAsync`, separate `McpTruncation.cs`-Datei statt Methode in
`McpToolResults.cs`, keine `RegexOptions.Multiline`, Argument-Validierung im Tool
statt im Scanner). Die 6 offenen Fragen wurden alle mit der Plan-Empfehlung
beantwortet.

## Beobachtungen (Tech-Debt-Kandidaten für den Kritiker)

- **`SearchPatternTool` Footprint 2482/2500 (Puffer 18 Z.)** — knapp. `McpCodeGraphServer`-
  Pull-in via `McpCodeGraphServer.Config`-Property zieht den `Configuration`-Namespace
  (~1110 Z.) transitiv in alle Tool-Klassen, die den Server referenzieren. Derselbe
  Effekt wie bei `FindSymbolTool`/`FindReferencesTool` (TD-008, dort bereits durch
  `PathOverrides` Pragmatik-Lösung). Strukturelle Lösung wäre `ILinterEngineConfig`-
  Interface (4-6h Refactor, im Plan nicht in 002-Scope). **Kandidat für TD-008-
  Verschärfung oder eine eigene Tech-Debt-Notiz**, falls der nächste analyse-orientierte
  Tool-Block `SearchPatternTool` über 2500 treibt.
- **`SymbolGraphToolRegistrations` hat nur 13 Z. Puffer (2487/2500)** — wenn ein
  zukünftiges Symbolgraph-Tool dazukommt (sehr wahrscheinlich), ist eine 5. Registrar-
  Klasse nötig. Nicht 002-Scope, nur Notiz (aus Plan-Anhang).
- **`SearchPatternTool.ExecuteAsync` verwendet `Task.Run`** — der Plan hält das für OK
  (CPU-/IO-bound Scan-Arbeit, hält `McpCodeGraphServer`-Lock nicht). Andere Tools
  (z. B. `GetViolationsTool`) machen es ohne. Wenn der Kritiker den `Task.Run` als
  unnötig wertet, ist das MINOR (A5) — bewusst nicht selbst entfernt.
- **`SafeEnumerateFiles`/`IsGeneratedPath` jetzt 3× dupliziert** (WebFileCatalog +
  GetIndexScopeScanner + SearchPatternScanner). Das war die bewusste Entscheidung in
  Vor-der-Planung-Check 1; TD-006 bleibt unverändert offen.
- **Kein `CancellationToken`-Parameter im Scanner** (Plan-Begründung: sequentieller
  Scan unter 1s bei Last-Fixture-Größe, EPIC-08-Performance-Messung ausstehend). Wenn
  EPIC-08 bei 500/5000 Dateien Probleme zeigt, ist `CancellationToken` in einer
  späteren Einheit nachzulegen.

## Bekannte Unschärfen

- **Konvention-Commit-Format:** Message ist `feat(mcp): search_pattern tool mit
  Trunkierung + maxResults [codegraph-mcp-server]` — entspricht `AiNetLinter.mdc` §4
  (Conventional Commits, deutscher Imperativ) und dem Task-Suffix-Schema
  (`[codegraph-mcp-server]`).
- **Doku-Update-Befreiung:** `Docs/agent-api.md` (Tool-Beschreibung search_pattern),
  `Docs/ROADMAP.md` (EPIC-04 4/4 fertig) und `README.md` werden **nicht** in 002
  aktualisiert — bewusst EPIC-08, Konzept-Befreiung wie in `step-010`/`units/001/plan.md`
  (Z. "Konzept-Befreiung explizit"). Der Coder darf das in 002 nicht selbst machen
  (A7: keine Doku-Dateien anfassen).
- **`get_violations`-Code-Commit `e63176d` Abweichung** (aus `units/001`): kein
  Conventional-Format. Diese Einheit hat das Format korrekt verwendet.
- **A3 für E2E-Test:** `if (false)`-Wrap in `AnalysisToolRegistrations` löst
  Compiler-Fehler CS0162 aus — deshalb wurde stattdessen ein
  `A3DisabledSearchPatternStub` mit `throw new NotImplementedException` verwendet
  (nur temporär, sofort rückgängig). Die Wahl ist eine pragmatische Notwendigkeit für
  `--no-build`-Discipline und nicht produktionsrelevant.

## Dogfooding (Konzept-Pflicht, Plan-Schritt 7.4)

Ad-hoc-Aufruf von `search_pattern` gegen die reale `AiNetLinter.slnx` über den
MCP-Server (Subprozess, JSON-RPC über stdio, Python-Helper für Tool-Calls,
Helper-Datei nach dem Lauf aus dem Working-Tree entfernt):

### tools/list
```
Total tools: 9
  - get_violations
  - find_symbol
  - search_pattern
  - get_index_scope
  - get_file_skeleton
  - get_hotspots
  - find_references
  - get_impact
  - get_type_hierarchy
search_pattern present: True
```
→ `search_pattern` ist in `tools/list` enthalten, Tool-Count = 9.

### Call 1: `search_pattern(pattern="CodeGraph")` (case-insensitive Substring)
```
Total lines: 51 (50 Trefferzeilen + 1 Meta-Zeile)
IsError: False
--- First 5 lines ---
src/AiNetLinter/Commands/McpServerCommand.cs:36:         using var mcpState = new McpCodeGraphServer(catalog, c, ResolveMaxLineCount(args), ResolveConfig(args));
src/AiNetLinter/Commands/McpServerCommand.cs:66:     /// <c>get_violations</c>/<see cref="McpCodeGraphServer.Config"/> gebraucht). Bei gesetztem
src/AiNetLinter/Mcp/AnalysisToolRegistrations.cs:29:     internal static void Register(McpServerPrimitiveCollection<McpServerTool> tools, McpCodeGraphServer mcpState)
src/AiNetLinter/Mcp/FileStructureToolRegistrations.cs:26:     internal static void Register(McpServerPrimitiveCollection<McpServerTool> tools, McpCodeGraphServer mcpState)
src/AiNetLinter/Mcp/McpCodeGraphServer.cs:22: internal sealed class McpCodeGraphServer : IDisposable
--- Last 3 lines (Trunkierungs-Meta) ---
src/AiNetLinter.Tests/Mcp/Tools/GetHotspotsToolTests.cs:62:         var state = new McpCodeGraphServer(catalog);
[84 Treffer gesamt, 50 gezeigt — Pattern verfeinern oder maxResults erhöhen]
```
→ **Trunkierung greift real:** 84 Treffer im Solution-Dateibestand für "CodeGraph",
50 gezeigt, Meta-Zeile weist auf Verfeinerungsoption hin. Default-`maxResults=50`
greift wie geplant. Forward-Slashes in Pfaden, 1-basierte Zeilennummern, format
`pfad:zeile: inhalt` exakt wie geplant. (Die `�` im Konsolen-Output sind cp1252-
Encoding-Artefakte; die gespeicherte Text-Datei hat das echte Em-Dash `—`.)

### Call 2: `search_pattern(pattern="McpCodeGraphServer", maxResults=10)`
```
Total lines: 11 (10 Trefferzeilen + 1 Meta-Zeile)
[10 spezifische .cs-Datei-Treffer mit Zeilennummer + Inhalt]
[83 Treffer gesamt, 10 gezeigt — Pattern verfeinern oder maxResults erhöhen]
```
→ Konkrete .cs-Datei-Treffer (`McpServerCommand.cs`, `McpCodeGraphServer.cs`,
`McpServerOptionsFactory.cs`, …), `maxResults=10` überschreibt den Default 50.

### Call 3: `search_pattern(pattern="(unclosed", isRegex=true)`
```
IsError: True
Text: [ERROR]: INVALID_ARGUMENT: Ungueltige Regex: Invalid pattern '(unclosed' at offset 9. Not enough )'s.
  hint:    Pruefe pattern auf gueltige Regex-Syntax.
```
→ Strukturierte Fehlerantwort mit `INVALID_ARGUMENT`-Code, exakt der Hinweis aus
dem Plan. Result-Pattern (kein rethrow/Exception-Propagierung), `AiNetLinterRichtlinien.mdc` §5.

**Plausibilitätsbewertung:** das Tool funktioniert gegen reale Bestandsgröße
(ca. 50 Quelldateien, `grep`/`rg` würden ohne Trunkierung dieselbe Treffermenge
liefern — `search_pattern` trunkiert deterministisch auf 50 + Meta-Zeile und
ermöglicht damit das in `konzept.md` Z. 215-225 explizit genannte DoD-Kriterium
"DoD: jedes Listen-Tool liefert bei einer generischen Anfrage gegen die Last-
Fixture (siehe P1-6 unten) eine Antwort unter der konfigurierten Zeilengrenze").
EPIC-08 mit der Last-Fixture wird diesen Befund bei 500/5000 Dateien bestätigen.
