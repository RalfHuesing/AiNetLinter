---
status: done (pending audit)
type: step-plan
task: magic-values-in-mcp
step: 001
corrects: null
title: "find_magic_values — Tool-Core, Basis-Klassifizierung & Doku-Sync (EPIC-1)"
epic: EPIC-1
estimated_risk: medium
step_type: single
items: []
created_by: planer
created_by_model: MiniMax-M3
created_by_model_knowledge_cutoff: 2026-01
related_to: []
---

# Step 001: find_magic_values — Tool-Core, Basis-Klassifizierung & Doku-Sync

## Bezug

- **Task:** `magic-values-in-mcp`
- **Epic:** EPIC-1 aus `roadmap.md` — Tool-Core, Basis-Klassifizierung (URLs/Pfade/Timeouts/Schwellenwerte/Format-Strings), Trivial-Filter, Trunkierung, Registrierung, Doku-Sync. Erweiterte Heuristiken (`nameof_candidates`, `enum_candidates`, `standard_candidates`, `security_candidates`, duplizierte `private const`-Felder), Suppression-Granularität via `SyntaxTrivia`, `changedOnly`, `includeTests`, `includeSuppressed` und der Suppression-Sonderfall-Hinweis bleiben **bewusst** EPIC-2 / step-002 (Roadmap-Trennung halten).
- **Konzept-Referenz:** `konzept.md` §Muss-Haven (Basis-Block: vollständige Erfassung mit `minOccurrences=1`, `maxResults=50`, `StructuredContent` als Objekt-Wrapper, Ziel-Fokus C#, Rausch-Filterung mit Trivial-Liste, Index/Loop-Ausnahme, Attribut-Isolierung, `GetHashCode()`-Sonderfall, `ignoreNumbers`-Erweiterung), §„Wo im Projekt" (Tests-Korrektur: `AiNetLinter.Tests/Mcp/Tools/` existiert nicht, siehe `roadmap.md` Tech-Stack-Notiz), §„Definition of Done" Punkte 1–7 (Tool-Registrierung, Trunkierung, Default-Argumente, Structured-Content-Shape, Doku-Update, `PatternCatalog`-Kommentar, `ROADMAP.md`-Eintrag).
- **Bestandsfund, der in EPIC-2 zu melden ist (nicht in diesem Step zu fixen):** `private const double WarnThreshold = 0.80;` in `HotspotMapBuilder.cs:23` und `GetHotspotsScanner.cs:27` (siehe `konzept.md` §„Entdeckte Mängel/Redundanzen" und `codemap.md` Eintrag zu `HotspotMapBuilder.cs`/`GetHotspotsScanner.cs`).

## Aktueller Projektzustand (JIT-Kontext)

Beim Lesen vorgefunden — die folgenden Strukturen werden in diesem Step wiederverwendet, nicht neu gebaut:

- **Registrierungs-Pattern:** `McpServerTool.Create(async (param1, param2, ..., CancellationToken ct = default) => { ... }, new McpServerToolCreateOptions { Name = ..., Description = ... })` mit `callLog.ExecuteCallAsync(...)`-Wrapper. `AnalysisToolRegistrations.cs:167-187` (`AddPatternDetect`) ist die 1:1-Vorlage.
- **Antwort-Bau:**
  - Normalfall: `McpToolResults.Text(text, new { MagicValues = list })` (`McpToolResults.cs:152-159`) — explizit dokumentiert: `payload` MUSS zu JSON-Objekt serialisieren, **kein Top-Level-Array** (war realer Bug in einem anderen Tool, siehe XML-Doc auf `McpToolResults.Text<T>`).
  - Trunkierung: `McpTruncation.TruncateLines(hitLines, totalMatches, maxResults)` mit Meta-Zeile `[N Treffer gesamt, M gezeigt — Pattern verfeinern oder maxResults erhöhen]` (`McpTruncation.cs:29-42`).
  - Fehler: `McpToolResults.InvalidArgument(...)` für unbekannte Enum-Werte (`recoverable`, `IsError=false`, `McpToolResults.cs:108-114`); `McpToolResults.SolutionNotLoaded()`, `McpToolResults.Loading()`, `McpToolResults.Error(AnalysisFailed, ...)` mit Retry-once-Hinweis für echte Malfunctions.
- **Scanner-Pattern:** `XxxTool` als dünner Dispatcher (keine eigene Logik, nur Parameter-Validierung, Loading-/Solution-Not-Loaded-Returns, Scanner-Aufruf in `Task.Run`-Wrapper), `XxxScanner` als `internal static class` mit `XxxScannerParameters`-Record und `XxxResult`-Record. `GetViolationsTool`+`GetViolationsScanner` (`Mcp/Tools/Analysis/`) und `SearchPatternTool`+`SearchPatternScanner` (`Mcp/Tools/Analysis/`) sind die Vorbilder.
- **`Task.Run`-Wrapper um den Scan:** `SearchPatternTool.cs:56-58` zeigt das Muster für CPU-/IO-bound Scans, damit das `McpCodeGraphServer`-Lock nicht unnötig gehalten wird — exakt dasselbe gilt für den `FindMagicValuesScanner`.
- **Scope-Filter-Konvention:** `scopeFilter` ist `string?` und case-insensitive `Contains` auf Projekt-Name oder solution-relativem Pfad. Übernommen aus `get_violations` / `pattern_detect` (siehe `PatternDetectTool.cs:36` und `PatternDetectScanner.cs:62` analog `ViolationScopeFilter`).
- **Test-Fixtures:**
  - `src/AiNetLinter.FastTests/Fixtures/McpInMemoryTestContext.cs` — in-memory Roslyn-Test-Solution + `CreateServer()`-Helper; `SymbolGraphMiniSolutionSpec.Create()` als Default-Content, oder `CreateScenario(params ProjectSpec[])` für eigene Test-Solutions.
  - `src/AiNetLinter.IntegrationTests/Mcp/Tools/SymbolGraphCatalogFixture.cs` — ReadOnly-`McpCodeGraphServer` auf der `SymbolGraphMini`-Fixture.
- **Bestehende Policy-Bindung:** `Mcp/IsErrorPolicy.md` ist die Single-Source-of-Truth für `IsError=true|false` (Pflicht-Argumente / unbekannte Enum-Werte → recoverable `InvalidArgument`; Server lädt noch → `Loading`; keine Solution → `SolutionNotLoaded`, `IsError=true`; echte Malfunction → `Error(AnalysisFailed, ...)`, `IsError=true`). Tabelle ist nach `18 Tools` benannt und braucht für den 19. Eintrag ein Update (siehe „Konkrete Änderungen").
- **`PatternCatalog.cs`-Kommentar:** nennt aktuell `magic-numbers` als Pattern ohne existierende Erkennung (`PatternCatalog.cs:11-13` — die Aufzählung `(deep-nesting, disposable-not-disposed, static-state, magic-numbers)`). Mit `find_magic_values` ist diese Lücke **konzeptuell gefüllt** (anderes Tool, aber dieselbe Domäne), der Hinweis ist also aus der Aufzählung zu streichen und der Kommentar entsprechend anzupassen — siehe Konzept §„Wo im Projekt" / §„DoD" Punkt 12.
- **AGENTS.md-Verweis:** die Pflicht-Test-Gates stehen in `AGENTS.md` §2 Punkt 2: `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress` UND `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress`. Konkret umfasst `Integration`/`Dogfood`/`Performance` — `Stress` ist explizit ausgeschlossen (Step-Planer erwähnt `Stress` nicht in den Test-Vorgaben, weil keine lastintensiven Tests in diesem Step entstehen).

## Intention

Nach diesem Step liefert `find_magic_values` als 19. MCP-Tool strukturierte Funde für die in EPIC-1 vorgesehenen Basis-Kategorien (`config_candidates` für URLs/Pfade/Timeouts, `constant_candidates` für Format-Strings/Schwellenwerte, `standard_candidates` für HTTP-Statuscodes) plus die Trivial-Filter (Index/Loop, Attribut, `GetHashCode()`, Trivial-Werte, `ignoreNumbers`-Erweiterung) und `minOccurrences=1` als Default. Die vier erweiterten Heuristiken (`nameof_candidates`, `enum_candidates`, `security_candidates`, duplizierte `private const`) und die Suppression-Logik sind EPIC-2 — die Tool-Schnittstelle nimmt ihre Parameter (`includeSuppressed`, `categoryFilter`-Werte) aber bereits entgegen, damit EPIC-2 ohne API-Bruch nachliefern kann. Ziel ist ein **grüner Build und grünes Test-Gate auf dem aktuellen Branch** mit einer klar abgegrenzten, kleinen Review-Oberfläche (neue Datei-Klasse + vier Test-Dateien + drei Doku-Updates).

## Konkrete Änderungen

### Datei 1: `src/AiNetLinter/Mcp/Tools/MagicValues/MagicValuesCategories.cs` (NEU)

- **Was:** `internal enum MagicValueCategory` mit allen 7 EPIC-1/EPIC-2-Werten (`ConfigCandidates`, `ConstantCandidates`, `EnumCandidates`, `NameofCandidates`, `LocalizationCandidates`, `StandardCandidates`, `SecurityCandidates`). String-Repräsentation stabil als snake_case via `ToStringValue()`-Helper (`config_candidates` / `constant_candidates` / `enum_candidates` / `nameof_candidates` / `localization_candidates` / `standard_candidates` / `security_candidates`) — diese Strings landen 1:1 im `StructuredContent` und müssen zur Konzept-Definition kompatibel bleiben.
- **Warum:** Zentrale Enum-Definition, damit `MagicValuesClassifier` (Heuristik) und `FindMagicValuesToolArgs` (Parameter-Validierung) denselben Wertevorrat teilen und keine String-Tippfehler entstehen. `McpServerTool.Create`-Delegate kann die Strings nicht direkt an ein C#-Enum binden — der String kommt aus dem JSON-RPC und wird im Tool gegen den Enum validiert (gleiches Muster wie `PatternCatalog.Patterns` für `pattern_detect`).
- **Hinweis:** In EPIC-1 sind nur `config_candidates`, `constant_candidates` und `standard_candidates` semantisch mit echten Heuristiken unterlegt. Die Enum-Werte `enum_candidates`/`nameof_candidates`/`localization_candidates`/`security_candidates` müssen im Args-Record bereits akzeptiert (sonst API-Bruch in EPIC-2) und im Classifier als „currently unclassified — domain in EPIC-2" behandelt werden. Dazu im Classifier ein expliziter `Unclassified`-Mapping-Punkt, der die Heuristik-stub markiert.

### Datei 2: `src/AiNetLinter/Mcp/Tools/MagicValues/MagicValuesClassifier.cs` (NEU)

- **Was:** `internal static class MagicValuesClassifier` mit:
  - Trivial-Wert-Tabelle: `0`, `1`, `-1`, `""`, `" "`, `"\n"`, `true`, `false`, `null` (konzepttreu — leere Strings werden NIE gemeldet).
  - Index/Loop-Ausnahme: `LiteralExpressionSyntax`, dessen `Parent` ein `ElementAccessExpressionSyntax` (Array/Tuple/Indexer) ist, oder das in `ForStatementSyntax` als Initialisierer eines Schleifenzählers auftaucht (`for (int i = 2; ...)`-Muster).
  - Attribut-Isolierung: `node.FirstAncestorOrSelf<AttributeSyntax>() != null` → skip.
  - `GetHashCode()`-Sonderfall: wenn `MethodDeclarationSyntax` mit Name `GetHashCode` im Vorfahren → skip (auch für override).
  - `ignoreNumbers`-Erweiterung: HashSet<int> der vom Aufrufer übergebenen Zahlen zusätzlich zur Trivial-Liste.
  - Basis-Heuristiken für EPIC-1:
    - `config_candidates` (Strings): URL-Pattern (`http://` / `https://` / `ftp://`), Windows-Pfad (`C:\…` / `\\server\share`), Connection-String-typische Schlüsselwörter (`Server=`, `Database=`, `Trusted_Connection=`), heuristische Timeout-Muster (`TimeSpan.FromSeconds(<zahl>)`, `Thread.Sleep(<zahl>)` via `SemanticModel.GetSymbolInfo` auf umschließenden Aufruf).
    - `config_candidates` (Zahlen): Literale in Aufruf-Argumenten, deren `SemanticModel.GetSymbolInfo(invocation).Symbol` einen Parameter mit Namen `timeout` / `millisecondsTimeout` / `delay` / `retryCount` / `maxRetries` / `port` / `bufferSize` hat.
    - `constant_candidates` (Strings): Format-Strings (`"yyyy-MM-dd"`, `"{0}"`-artige Patterns), Correlation-IDs / Header-Namen-artige Strings mit Bindestrich (`"X-Correlation-ID"`).
    - `constant_candidates` (Zahlen): Magic-Schwellenwerte wie `0.5`, `0.19`, `0.80` in `const`/`readonly`/`static`-Feld-Initialisierern oder in Zuweisungen an `static`-Variablen.
    - `standard_candidates` (Zahlen): HTTP-Statuscodes 1xx/2xx/3xx/4xx/5xx in Aufruf-Argumenten oder `const`-Feldern, mit Empfehlung `StatusCodes.StatusXXXNotFound` / `.StatusXXXOK` etc.
  - Ergebnis-Typ: `internal sealed record MagicValueClassification(bool IsMagic, MagicValueCategory Category, string Recommendation, string ContextHint)`. `Recommendation` ist der konkrete Ziel-Pfad (`"appsettings.json"`, `"Constants.cs"`, `"StatusCodes.Status404NotFound"`, `"nameof(...)"` — letzteres in EPIC-2).
- **Warum:** Reine, deterministische Funktion `MagicValueClassification Classify(LiteralExpressionSyntax literal, SemanticModel model, IReadOnlySet<int> ignoreNumbers, ISet<string> identifierNamesInScope)`. Unit-testbar ohne Roslyn-Lösung — syntaktische Heuristiken pur, plus `SemanticModel` für den Aufruf-Kontext. Hält die Datei klein genug, dass sie unter dem `MaxLineCount`-Limit der Linter-Regel bleibt.
- **Hinweis:** Heuristiken sind bewusst **konservativ** (mehr False Negatives als False Positives) — der Konzept-Abschnitt „Rausch-Filterung" priorisiert explizit Vermeidung von False Positives. Eine URL-Heuristik mit `http://`-Präfix matcht deutlich mehr Treffer als eine freie Whitelist — vertretbar, weil `ignoreNumbers` und der `categoryFilter` als zweite Stufe wirken.

### Datei 3: `src/AiNetLinter/Mcp/Tools/MagicValues/FindMagicValuesScanner.cs` (NEU)

- **Was:** `internal static class FindMagicValuesScanner` mit:
  - `internal const int DefaultMaxResults = 50;` (analog `GetViolationsScanner.DefaultMaxResults`).
  - `internal static async Task<FindMagicValuesResult> ScanAsync(FindMagicValuesScannerParameters p)` — iteriert asynchron über alle `.cs`-Documents der Solution (Filter: nicht `obj/`, `bin/`, `.ainetlinter/`), pro Document einen `CSharpSyntaxTree.GetRoot()` + `SemanticModel`, ruft den `MagicValueSyntaxWalker` auf.
  - Privater `MagicValueSyntaxWalker : CSharpSyntaxWalker`, der für jedes `LiteralExpressionSyntax` UND jedes statische Textsegment in `InterpolatedStringExpressionSyntax` (nur `contents.Where(c => c.IsKind(SyntaxKind.InterpolatedStringText))`) den `MagicValuesClassifier.Classify(...)` aufruft und ein `RawMagicValue` sammelt. Triviale/unterdrückte Treffer werden vor der Akkumulation aussortiert.
  - Suppression-Prüfung **erst in EPIC-2 aktiv** — in EPIC-1 ist der `includeSuppressed: false`-Default ein No-op (kein `SyntaxTrivia`-Walk), aber der Parameter existiert im Args-Record und im Scanner-Signatur, damit EPIC-2 rein additive Logik einhängen kann. Dokumentiere das im XML-Doc.
  - Filter-Pipeline: `valueType`-Filter (`all`/`strings`/`numbers`, Default `all`) vor `categoryFilter` (Default `all`); danach `minOccurrences`-Gruppierung (Aggregation über `(category, value, filePath)`-Tupel); dann Scope-Filter (`ViolationScopeFilter`-kompatibel, oder simpler case-insensitive `Contains` auf den relativen Pfad). Trunkierung in `FormatReport` via `McpTruncation.TruncateLines`; `StructuredContent` liefert die gekappte Liste (analog `GetViolationsScanner`).
  - Result-Records: `FindMagicValuesResult(Text, Payload, IsMalfunction, IsTruncated, Context)`; `FindMagicValuesPayload(IReadOnlyList<MagicValueEntry> MagicValues, MagicValuesSummary TotalOccurrences, int ShownOccurrences)`, `MagicValueEntry(FilePath, Line, Column, ValueType, Value, Category, Recommendation, ContextHint)`, `MagicValuesSummary(int Total, int ByCategoryConfig, int ByCategoryConstant, int ByCategoryStandard)`.
- **Warum:** Trennung Tool/Scanner ist die projektweite Konvention (siehe `codemap.md`-Eintrag zu `GetViolationsTool`+`GetViolationsScanner`). Der Scanner ist `internal static`, direkt unit-testbar, frei von `McpCodeGraphServer`-Abhängigkeit.
- **Hinweis:** `ViolationScopeFilter` ist nicht direkt wiederverwendbar (es erwartet `RuleViolation`-Listen), aber die `BuildFileToProjectMap` + `CountMatchingFiles`-Logik lässt sich 1:1 in den neuen Scanner spiegeln, oder — wenn der Scope-Filter in EPIC-1 minimal gehalten werden soll — simpler `Contains`-Substring auf den solution-relativ-Pfad reicht für `scopeFilter`. **Empfehlung: simpler Substring-Match** (gleiche Semantik wie `get_violations`-`scopeFilter`, aber ohne Projekt-Name-Auflösung) — vermeidet eine neue `XxxScopeFilter`-Klasse und passt zur Tool-Definition „Pfad/Projekt-Filter".

### Datei 4: `src/AiNetLinter/Mcp/Tools/MagicValues/FindMagicValuesTool.cs` (NEU)

- **Was:** `internal static class FindMagicValuesTool` mit `internal static async Task<CallToolResult> ExecuteAsync(McpCodeGraphServer state, FindMagicValuesToolArgs args, CancellationToken ct)`:
  1. `state.LoadState == ServerLoadState.Loading` → `McpToolResults.Loading()`.
  2. `state.GetCurrentSolution() is null` → `McpToolResults.SolutionNotLoaded()`.
  3. **Parameter-Validierung:**
     - `valueType`: unbekannter String (`"foo"`) → `McpToolResults.InvalidArgument("Unbekannter valueType '{value}'. Gueltige Werte: all, strings, numbers.", hint: "valueType korrigieren.")`.
     - `categoryFilter`: unbekannter String → analog mit `MagicValuesCategories.AllCategoryIds()` als Hint-Liste.
     - `minOccurrences < 1` → auf `1` clampen (kein Hard-Fehler).
     - `maxResults < 1` → auf `1` clampen.
  4. Defensive `try/catch` um `await FindMagicValuesScanner.ScanAsync(...)` (in `Task.Run` gewrappt) → bei Exception `McpToolResults.Error(LinterErrorCodes.AnalysisFailed, ...)` mit Retry-once-Hinweis (IsError=true, echte Malfunction, 1:1 zu `PatternDetectTool.cs:51-58`).
  5. Bei normalem Ergebnis:
     - Wenn `result.Payload` null (z. B. `scopeFilter` matched keine Datei): `McpToolResults.Text(result.Text)` ohne StructuredContent.
     - Sonst: `McpToolResults.Text(result.Text, new { MagicValues = result.Payload.MagicValues })` (Objekt-Wrapper, niemals nacktes Array; siehe `McpToolResults.Text<T>`-XML-Doc Z. 146-150).
- **Warum:** Dünner Dispatcher analog `PatternDetectTool` + `GetViolationsTool` + `SearchPatternTool`. Validierung **im Tool** (nicht im Scanner), damit der Scanner reine Daten verarbeitet und unit-testbar bleibt (Konvention siehe `SearchPatternTool.cs:17`).
- **`FindMagicValuesToolArgs`-Record:** `internal sealed record FindMagicValuesToolArgs(string? ScopeFilter, string ValueType = "all", string CategoryFilter = "all", int MinOccurrences = 1, int MaxResults = FindMagicValuesScanner.DefaultMaxResults, int[]? IgnoreNumbers = null, bool IncludeTests = false, bool IncludeSuppressed = false, bool ChangedOnly = false)` — 9 Felder, alle optional mit sinnvollem Default. `IncludeSuppressed` und `ChangedOnly` sind EPIC-2-Platzhalter mit Default `false`; der Scanner akzeptiert sie und ignoriert sie in EPIC-1 (im XML-Doc dokumentiert).

### Datei 5: `src/AiNetLinter/Mcp/AnalysisToolRegistrations.cs` (Zeile 32-52 + 167-196, Erweiterung)

- **Was:**
  - In `Register(...)` (Z. 42-52) Aufruf `AddFindMagicValues(tools, mcpState, callLog);` ergänzen — nach `AddPatternDetect`, weil `find_magic_values` semantisch am engsten verwandt ist.
  - Neue private Methode `AddFindMagicValues(...)` als 1:1-Kopie der `AddPatternDetect`-Signatur (Z. 167-187) mit dem Args-Record als einzigem `McpServerTool.Create`-Parameter (Delegate-Signatur: `async (FindMagicValuesToolArgs? args = null, CancellationToken ct = default) => { ... }`). Im Wrapper-Pattern: bei `callLog is null` direkt `FindMagicValuesTool.ExecuteAsync(...)`, sonst `callLog.ExecuteCallAsync("find_magic_values", Serialize(args), () => FindMagicValuesTool.ExecuteAsync(...))`.
  - Neues `private const string FindMagicValuesDescription = ...` mit Beschreibungstext nach Konzept §„MCP-Tool Schnittstellen-Spezifikation" (Input-Parameter-Aufzählung im Beschreibungstext, Default-Werte explizit).
  - Klassen-Doc-Kommentar (Z. 15-33) erweitern: `find_magic_values` als fünftes analyse-orientiertes Tool im selben LinterEngine-Pull-in-Block nennen.
- **Warum:** Registrierung ist der einzige Weg, dass das Tool via `tools/list` im MCP-Server auffindbar wird. Konvention 1:1 von `AddPatternDetect`/`AddGetViolations`.
- **Wichtig:** `McpServerTool.Create` Delegate-Parameter werden **positional** aus dem JSON-RPC-Payload gebunden; der Wrapper-Args-Record MUSS so heißen wie im `tools/list`-Schema deklariert. Schema-Generierung läuft über `McpServerToolCreateOptions` (Name, Description) — Schema für Input-Args wird **aus dem Delegate** abgeleitet. Empfehlung: einfache primitive Typen + nullable string-Arrays (siehe `AddMetricsTree` mit `MetricsTreeToolArgs`-Record als Positional — funktioniert dort).

### Datei 6: `src/AiNetLinter/Mcp/Tools/PatternDetect/PatternCatalog.cs` (Zeile 8-16, Kommentar-Anpassung)

- **Was:** Im Klassen-Doc-Kommentar (`PatternCatalog.cs:8-16`) die Aufzählung der „4 Patterns ohne existierende Erkennung" von `(deep-nesting, disposable-not-disposed, static-state, magic-numbers)` auf `(deep-nesting, disposable-not-disposed, static-state)` kürzen. Den Hinweis auf `magic-numbers` durch eine kurze Notiz ersetzen, dass `find_magic_values` als separates On-Demand-Audit-Tool dieselbe Domäne abdeckt (kein `pattern_detect`-Pattern, aber semantisch verwandt — Verweis auf `Mcp/Tools/MagicValues/FindMagicValuesTool.cs`).
- **Warum:** Konzept §„Wo im Projekt" / §„DoD" Punkt 12 verlangt diese Anpassung explizit; ohne den Kommentar-Update zeigt der Code eine veraltete Lücke, die nicht mehr existiert.

### Datei 7: `Docs/agent-api.md` (mehrere Stellen)

- **Was (genau):**
  - Z. 192: `18 granular abfragbare Tools` → `19 granular abfragbare Tools`.
  - Z. 215: Scope-Hinweis-Box und Tool-Liste aktualisieren — `find_magic_values` ist C#-only (`.cs`-AST-Scan). In der Aufzählung der C#-only-Tools nach `pattern_detect` ergänzen.
  - Z. 217: Section-Überschrift `### Die 18 Tools` → `### Die 19 Tools`.
  - Z. 220-238: Tool-Tabelle um eine Zeile für `find_magic_values` ergänzen (zwischen `pattern_detect` und `get_symbol_body` einsortieren — beide sind analyse-orientiert). Spalten-Inhalt strikt aus Konzept §„MCP-Tool Schnittstellen-Spezifikation" + Konzept-Defaults: `valueType` (Default `all`), `categoryFilter` (Default `all`), `minOccurrences` (Default 1), `maxResults` (Default 50), `ignoreNumbers` (optional), `includeTests` (Default false), `includeSuppressed` (Default false), `changedOnly` (Default false). Trunkierung: `ja`. C#-only: `ja`. Output: „Strukturierte Funde (URLs, Pfade, Timeouts, Format-Strings, Schwellenwerte, HTTP-Statuscodes) mit Ziel-Empfehlung (`appsettings.json`, `Constants.cs`, `StatusCodes.StatusXXX…`)" — knapp, eine Zeile.
  - Z. 240: `Structured Output`-Absatz einleitungstext („Neben dem in der Tabelle oben dokumentierten Text-Output liefern `get_violations`, `get_hotspots`, `get_server_health`, `get_index_scope`, `find_symbol`, `find_references` (nur `depth=1`), `get_impact` (Symbol- und Git-Diff-Branch, jeweils `depth=1`), `dependency_graph` (alle `depth`-Werte) und `find_duplicates`…") — `find_magic_values` in die Liste der Structured-Content-liefernden Tools aufnehmen.
  - Neuer Detail-Block nach `find_duplicates`-Block: **`find_magic_values` — Structured Output im Detail** mit JSON-Schema (`{ "MagicValues": [ { "filePath", "line", "column", "valueType", "value", "category", "recommendation", "contextHint" } ], "summary": { "total": N, "byCategory": { "config_candidates": …, "constant_candidates": …, "standard_candidates": … }, "shown": M, "truncated": true|false } }`). Suppression-Sonderfall-Hinweis in der Beschreibung erwähnen: `// ainetlinter-disable MagicValues` pro Fundstelle statt dateiweit (bewusste Ausnahme von der projektweiten Suppression-Semantik, analog Konzept §„Verworfene Alternativen").
  - Z. 375 + 377: zwei Textstellen „Neben den 18 Tools stellt der Server eine MCP-Resource bereit" / „Kurzbeschreibung aller 18 Tools" auf 19 aktualisieren.
- **Warum:** Konzept §„Wo im Projekt" + §„Definition of Done" Punkt 9 verlangen die Doku-Aktualisierung. Konsistenz mit `pattern_detect`-Block (gleicher Detail-Block-Stil, gleiche JSON-Schema-Konvention). 18 → 19 muss **jede** Stelle im Doc treffen, sonst entsteht Doku-Drift.

### Datei 8: `Docs/ROADMAP.md` (Epic-Eintrag)

- **Was:** Neuen Eintrag in einem passenden Epic anlegen. Konzept-Vorgabe ist nicht explizit, aber `pattern_detect` wurde auch in Epic 19 (AI-Developer Experience & Tooling) verortet — `find_magic_values` passt thematisch dort. **Empfehlung: Eintrag am Ende von Epic 19** mit Aufzählungspunkt „`- [ ]` **`find_magic_values` MCP-Tool (On-Demand-Magic-Value-Audit):** …". Inhalt: 1 Satz Ziel, Verweis auf `konzept.md` und `tasks/magic-values-in-mcp/step-001/`, Datum 2026-08-14, Status `pending` bis step-001 abgeschlossen.
- **Warum:** Konzept §„Wo im Projekt" nennt `Docs/ROADMAP.md` explizit. Ohne Eintrag fehlt der Verweis in der zentralen Projekt-Historie.

### Datei 9: `src/AiNetLinter/Mcp/IsErrorPolicy.md` (Zeile 26-50, Audit-Tabelle)

- **Was:** Tabellen-Überschrift „## Audit-Ergebnis pro Tool (18 Tools)" → „(19 Tools)". Eine neue Zeile am Ende der Tabelle für `find_magic_values` einfügen mit Inhalt (gemäß Konzept + diesem Step): `isError=true` für `SOLUTION_NOT_LOADED` und echte Malfunction (`ANALYSIS_FAILED` bei unerwarteter Roslyn-/Laufzeit-Exception im defensiven `try/catch`); `isError=false` (recoverable) für `INVALID_ARGUMENT` (unbekannter `valueType`/`categoryFilter`, leere Pflicht-Argumente — `minOccurrences`/`maxResults` werden geclamped, nicht abgelehnt); leere Treffermenge (0 Funde); Scope-Filter matched keine Datei.
- **Warum:** Die Policy ist Single-Source-of-Truth für `IsError`-Semantik. Nach Hinzufügen eines 19. Tools ist die Tabelle sonst veraltet (verstößt gegen `AiNetLinterRichtlinien.mdc` §1 „Dokumentations-Objektivität" — „Nur Implementiertes dokumentieren"; ein nicht aufgeführtes Tool würde bei künftigen Audits übersehen).
- **Hinweis:** Diese Datei war nicht in der expliziten Pflicht-Liste des Step-Prompts, ist aber **inhaltlich zwingend** (sonst Doku-Drift, der die Policy-Datei selbst unglaubwürdig macht). Der Coder muss sie mit-aktualisieren.

### Datei 10 (Test): `src/AiNetLinter.FastTests/Mcp/Tools/FindMagicValuesScannerTests.cs` (NEU)

- **Was:** `[Trait("Category", "Component")]`-Testklasse, die reine Scanner-Logik auf virtuellen Roslyn-Solutions testet (Pattern 1:1 von `PatternDetectScannerTests.cs`):
  - `RoslynTestSolutionFactory.CreateSolution(...)` mit minimalen Test-Sources (1-2 Dateien).
  - Helper: `RunAsync((file, source)[] files, FindMagicValuesToolArgs? argsOverride = null)`.
  - Tests siehe „Tests"-Abschnitt unten.
- **Warum:** Direkter Unit-/Component-Test des Scanners ohne MCP-Server-Lock. Pattern 1:1 von `PatternDetectScannerTests`.

### Datei 11 (Test): `src/AiNetLinter.IntegrationTests/Mcp/Tools/FindMagicValuesToolTests.cs` (NEU)

- **Was:** `[Trait("Category", "Integration")]`-Testklasse mit `SymbolGraphCatalogFixture`-Injektion (Pattern 1:1 von `SearchPatternToolTests.cs`):
  - `SOLUTION_NOT_LOADED`-Test, `Loading`-State-Übersprung, `StructuredContent`-Shape-Test, `McpCallLog`-Wrapper-Test (über `McpCallLog` aufgerufen → JSONL-Eintrag), `truncated`-Test auf der ReadOnly-Fixture (sofern `SymbolGraphMini` genug Magic Values enthält — sonst `SymbolGraphMiniFixtureWorkspace` mit zusätzlichen Literalen erweitern).
- **Warum:** Integration-Tests gegen die `SymbolGraphMini`-ReadOnly-Fixture sind die projektweite Konvention für Tool-Layer-Tests (siehe `codemap.md`-Eintrag zu `SearchPatternToolTests`).

## Tests

**FastTests / `FindMagicValuesScannerTests.cs` (Category=Component):**

- [ ] **Trivial-Filter vollständig:** Test-Source mit `0`, `1`, `-1`, `""`, `" "`, `"\n"`, `true`, `false`, `null` — keine davon taucht im `Payload.MagicValues` auf.
- [ ] **Index/Loop-Ausnahme:** Test-Source mit `args[2]` und `for (int i = 2; i < n; i++)` — beide Literale werden ignoriert.
- [ ] **Attribut-Isolierung:** `[JsonPropertyName("foo")]` und `[Route("/api/v1/users")]` und `[Obsolete("legacy")]` — keine Literale aus Attribut-Argumenten im Payload.
- [ ] **`GetHashCode()`-Sonderfall:** `override int GetHashCode() => 31 * hash + Field;` — alle Literale im Body (auch `31`, `17`, `23`) werden ignoriert.
- [ ] **`ignoreNumbers`-Erweiterung:** Default-Liste (`0`/`1`/`-1`) trifft, plus zusätzliche `ignoreNumbers: [24, 60]` — diese Zahlen werden ebenfalls ignoriert, andere Zahlen nicht.
- [ ] **URL-Heuristik `config_candidates`:** `const string ApiBaseUrl = "https://api.example.com";` — Eintrag mit `category=config_candidates`, `valueType=strings`, `recommendation` enthält `appsettings.json` o. ä.
- [ ] **Pfad-Heuristik `config_candidates`:** `const string DataDir = @"C:\Data\...";` — Eintrag mit `category=config_candidates`.
- [ ] **Timeout-Heuristik `config_candidates` (numbers):** `Thread.Sleep(5000)` — Eintrag mit `category=config_candidates`, `contextHint` enthält `millisecondsTimeout`/`timeout` (per `SemanticModel` aufgelöst).
- [ ] **Format-String `constant_candidates`:** `const string DateFormat = "yyyy-MM-dd";` — Eintrag mit `category=constant_candidates`.
- [ ] **HTTP-Statuscode `standard_candidates`:** `if (status == 404) { … }` — Eintrag mit `category=standard_candidates`, `recommendation` enthält `StatusCodes.Status404NotFound`.
- [ ] **Schwellenwert `constant_candidates` (numbers):** `private const double Tolerance = 0.19;` — Eintrag mit `category=constant_candidates`, `contextHint` enthält Hinweis auf zentrale Konstante.
- [ ] **`minOccurrences=1`-Default:** Ein Literal, das nur 1x in der Test-Solution vorkommt, wird im Payload gefunden (`Occurrences >= 1`).
- [ ] **`minOccurrences=2`-Filter:** Test-Source mit 2 identischen Literalen + `minOccurrences=2` — Eintrag ist da; 1 identisches Literal + `minOccurrences=2` — Eintrag fehlt.
- [ ] **`valueType`-Filter (`strings` vs. `numbers`):** Mit beiden Filter-Werten in getrennten Test-Cases; prüfen, dass nur der geforderte Typ gemeldet wird.
- [ ] **`categoryFilter` (z. B. `config_candidates`):** Nur Funde dieser Kategorie erscheinen im Payload (auch wenn `constant_candidates`-Funde existieren würden).
- [ ] **Default-Filter `categoryFilter=all` + `valueType=all`:** Alle Funde erscheinen (keine Filterung).
- [ ] **`scopeFilter` Substring-Match:** `scopeFilter: "Subdir"` filtert auf Dateien, deren Pfad `Subdir` enthält (case-insensitive).
- [ ] **Scope-Filter ohne Match:** `scopeFilter: "DoesNotExistAnywhere"` → `Payload` null, `Text` enthält `Keine Dateien im Scope`.
- [ ] **`maxResults`-Trunkierung:** Test-Solution mit 5 Literalen, `maxResults: 2` → `Payload.MagicValues.Count == 2`, `summary.truncated == true`, `Text` enthält `Treffer gesamt` Meta-Zeile.
- [ ] **`StructuredContent`-Shape (Objekt-Wrapper):** `result.Payload` ist ein Objekt mit `MagicValues` (Array) + `Summary`, **nicht** ein nacktes Array (siehe `McpToolResultsTests` Regressionsschutz-Muster).
- [ ] **Malfunction-Pfad:** `FaultingSolutionFixture` einsetzen; `IsMalfunction == true`, `IsError == true`, `Context` enthält Exception-Message.
- [ ] **`includeSuppressed: false`-Default ist No-op in EPIC-1:** Test-Source mit `// ainetlinter-disable MagicValues` + darunterliegendem Literal — Literal wird in EPIC-1 trotzdem gemeldet (Suppression-Logik kommt in EPIC-2). Test dokumentiert dieses **erwartete** Verhalten, damit ein künftiger EPIC-2-Refactor ihn anpasst.

**IntegrationTests / `FindMagicValuesToolTests.cs` (Category=Integration):**

- [ ] **SOLUTION_NOT_LOADED:** `new McpCodeGraphServer(...)` ohne Snapshot → `IsError == true`, Text enthält `SOLUTION_NOT_LOADED`.
- [ ] **Loading-State:** `state.LoadState = ServerLoadState.Loading` → `result.IsError == false`, Text enthält `Server laedt die Solution noch`.
- [ ] **Recovery bei unbekanntem `valueType`:** `args.ValueType = "foo"` → `IsError == false` (recoverable), Text enthält `INVALID_ARGUMENT` und `all, strings, numbers`.
- [ ] **Recovery bei unbekanntem `categoryFilter`:** `args.CategoryFilter = "foo"` → `IsError == false`, Text enthält `INVALID_ARGUMENT` und gültige Kategorien.
- [ ] **`maxResults`-Clamp:** `args.MaxResults = 0` oder `-5` → kein Crash, Tool liefert normales Ergebnis.
- [ ] **`minOccurrences`-Clamp:** `args.MinOccurrences = 0` → kein Crash, Default-Verhalten.
- [ ] **`McpCallLog`-Wrapper:** `callLog` aktiv → JSONL-Eintrag mit `tool == "find_magic_values"`, `args` enthält die relevanten Parameter.
- [ ] **`StructuredContent`-Shape Integration:** JSON-Deserialisierung auf `result.StructuredContent` zeigt `{ MagicValues: [...], Summary: { total, shown, truncated, byCategory: {...} } }` — kein Top-Level-Array.
- [ ] **Tool-Registrierung sichtbar:** Über `McpServerOptionsFactory` + `McpCodeGraphServer`-Initialisierung in einem separaten Test (`FindMagicValuesRegistrationTests`, optional in IntegrationTests-Datei) sicherstellen, dass das Tool in `tools/list` auftaucht mit `Name == "find_magic_values"` (Pattern von `SymbolGraphToolRegistrationsTests`).

## Definition of Done

- [ ] Alle „Konkrete Änderungen" (Dateien 1-11) umgesetzt.
- [ ] `dotnet build` grün — `TreatWarningsAsErrors=true` (siehe `AiNetLinterRichtlinien.mdc` §5), keine neuen Warnungen.
- [ ] `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress` grün.
- [ ] `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress` grün.
- [ ] Commit auf Branch `main` (nicht pushen — siehe Workspace-Anchor): Conventional-Commit auf Deutsch, imperativ, Subject ≤ 72 Zeichen, Suffix `[magic-values-in-mcp]`, Trailer `Refs: tasks/magic-values-in-mcp/step-001`.
- [ ] `tasks/magic-values-in-mcp/step-001/step-result.md` geschrieben (vom Coder, nicht vom Planer).
- [ ] `status` in `step-plan.md` von `open` auf `done (pending audit)` gesetzt (vom Coder nach Implementierung).
- [ ] `task-state.md`-Steps-Tabelle um Zeile `001 | EPIC-1 | done (pending audit) | <title> | null | <commit-hash> | <reviewer> | <commit-hash>` ergänzt (vom Orchestrator).

## Rules-Refs

- `.agents/rules/AiNetLinterRichtlinien.mdc#1` (Grundprinzipien) — „L wenig Magic" (trifft das Tool ironischerweise selbst zu: Heuristiken sauber in einer Helper-Klasse statt verstreut), „Immutability & Performance" (Records für Args/Result, sparsame `SyntaxTree`/`SemanticModel`-Zugriffe — `SemanticModel` nur dort, wo er für `nameof(...)`/Aufruf-Kontext wirklich nötig ist, Heuristiken rein syntaktisch wo möglich), „Dokumentation als Kontext nutzen" (vor Implementierung Konzept + Roadmap + CodeMap gelesen, `Docs/agent-api.md` wird mit-aktualisiert), „Dokumentations-Objektivität" (nur Implementiertes dokumentieren — alle 18→19-Textstellen updaten, sonst Doku-Drift).
- `.agents/rules/AiNetLinterRichtlinien.mdc#2` (Architektur-Verbote) — kein DI-Container, Tool erreicht `McpCodeGraphServer` per Delegate-Closure (genau wie `AddGetViolations`/`AddPatternDetect`), `internal static class` ohne Singleton-/Service-Lifecycle.
- `.agents/rules/AiNetLinterRichtlinien.mdc#4` (Updates & Tests) — xUnit v3, `TestCategory` korrekt setzen (Component für Scanner-Tests, Integration für Tool-Tests, **kein** Stress — kein lastintensiver Test in diesem Step), MCP-Server `ainetlinter` für Symbol-Suchen **nicht** relevant (kein Bestands-Code zu durchsuchen — neue Datei-Klasse, isoliert), Commit-Vorschlag am Antwortende mit dem reinen Commit-Text (Suffix `[magic-values-in-mcp]`, Trailer `Refs: tasks/magic-values-in-mcp/step-001`).
- `.agents/rules/AiNetLinterRichtlinien.mdc#5` (Qualitätsdrift-Prävention) — `TreatWarningsAsErrors=true`: keine ungenutzten Parameter in Records (jeder Args-Feld wird auch gelesen oder dokumentiert verworfen), `sealed` für konkrete Klassen, `internal sealed record` für Args/Result, `Result`-Pattern nur für echte Fehlerfälle (Exceptions bleiben für Roslyn-Malfunctions), Symptom-Fixing verboten (Tests dürfen nicht abgeschwächt werden, um grün zu werden — `IncludeSuppressed`-No-op-Test ist explizit als „erwartetes No-op bis EPIC-2" markiert), Sparsame Code-Kommentare (kein `step-001`-Verweis in Code-Kommentaren — Verboten laut §5; nur sachliche *Why*-Notizen, z. B. „Heuristik konservativ wegen Rausch-Filterung, siehe Konzept §Muss-Haven").
- `.agents/rules/AiNetLinter.mdc` (generierte Regeln) — `MaxMethodParameterCount: 4` → Args-Record mit 9 Feldern ist der vorgesehene Weg, Methoden-Parameter zählen Records nicht. `MaxLineCount: 700` pro Datei — alle neuen Dateien bleiben deutlich darunter (Classifier ~150-200 Zeilen, Scanner ~250-350 Zeilen, Tool ~100 Zeilen, Categories ~30 Zeilen). `EnforceSealedClasses` → alle neuen Klassen `internal sealed`. `EnforceNullableEnable` → `#nullable enable` am Dateianfang.

## Bekannte Ausnahmen

- **Test `IncludeSuppressed: false` No-op-Verhalten:** Der `IncludeSuppressed`-Test dokumentiert explizit das **Nicht-Funktionieren** der Suppression in EPIC-1 (Kommentar im Test: „Suppression-Logik kommt in EPIC-2"). Test schlägt **nicht** fehl, weil er das Default-Verhalten verifiziert (Literal wird gemeldet) — nicht weil Suppression kaputt ist. Wenn EPIC-2 Suppression implementiert, **muss dieser Test angepasst werden**, damit er dann das Stumm-Schalten verifiziert. **Flaky-Risiko: gering** — der Test ist deterministisch, kein Timing, kein Random.
- **`enum_candidates`/`nameof_candidates`/`localization_candidates`/`security_candidates` als akzeptierte Enum-Werte ohne Heuristik:** Der Args-Record akzeptiert diese Strings, der Scanner liefert für sie 0 Treffer (Classifier-Default-Zweig). In EPIC-2 werden die Heuristiken schrittweise ergänzt; bis dahin sind `categoryFilter="security_candidates"` etc. valide Aufrufe mit leerem Ergebnis. Im Agenten-Doc-Block (Datei 7) wird das transparent erwähnt: „Heuristiken für `enum_candidates`/`nameof_candidates`/`localization_candidates`/`security_candidates` sind Bestandteil von EPIC-2 und liefern in der aktuellen Version 0 Treffer."
- **`IsErrorPolicy.md`-Audit-Tabelle:** nicht in der expliziten Pflicht-Liste des Step-Prompts, aber zwingend (sonst Doku-Drift; siehe Datei 9 „Warum"). Coder MUSS diese Datei mit-aktualisieren — der Planer hat sie bewusst als Pflicht-Datei aufgenommen.

## Code-Skizze (optional)

```
// src/AiNetLinter/Mcp/Tools/MagicValues/FindMagicValuesTool.cs
internal static class FindMagicValuesTool
{
    internal static async Task<CallToolResult> ExecuteAsync(
        McpCodeGraphServer state, FindMagicValuesToolArgs args, CancellationToken ct)
    {
        if (state.LoadState == ServerLoadState.Loading) return McpToolResults.Loading();
        var solution = state.GetCurrentSolution();
        if (solution is null) return McpToolResults.SolutionNotLoaded();

        var (valueType, valueTypeError) = ResolveValueType(args.ValueType);
        if (valueTypeError is not null) return valueTypeError;
        var (category, categoryError) = ResolveCategory(args.CategoryFilter);
        if (categoryError is not null) return categoryError;

        FindMagicValuesResult result;
        try
        {
            result = await Task.Run(
                () => FindMagicValuesScanner.ScanAsync(new FindMagicValuesScannerParameters(
                    Solution: solution,
                    ScopeFilter: args.ScopeFilter,
                    ValueType: valueType,
                    Category: category,
                    MinOccurrences: Math.Max(1, args.MinOccurrences),
                    MaxResults: Math.Max(1, args.MaxResults),
                    IgnoreNumbers: args.IgnoreNumbers,
                    IncludeTests: args.IncludeTests,
                    IncludeSuppressed: args.IncludeSuppressed, // No-op bis EPIC-2
                    ChangedOnly: args.ChangedOnly,             // No-op bis EPIC-2
                    CancellationToken: ct)),
                ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return McpToolResults.Error(
                LinterErrorCodes.AnalysisFailed,
                "Unerwarteter Fehler beim Magic-Value-Scan.",
                context: ex.Message,
                hint: "Einmal erneut versuchen — bleibt der Fehler bestehen, LinterEngine-Log pruefen.");
        }

        if (result.IsMalfunction) return /* Error-Pfad analog oben */;
        if (result.Payload is null) return McpToolResults.Text(result.Text!);
        return McpToolResults.Text(result.Text!, new { MagicValues = result.Payload.MagicValues, Summary = result.Payload.Summary });
    }
    // ResolveValueType / ResolveCategory analog ResolvePatterns in PatternDetectTool.cs
}
```

## Notes

- **`McpServerTool.Create`-Delegate-Reihenfolge:** Die `McpServerTool.Create`-Delegate-Signatur MUSS die Parameter in derselben Reihenfolge und mit denselben Defaults deklarieren wie im `McpServerToolCreateOptions` dokumentierten Schema. Im Fall eines Args-Records (genau wie `MetricsTreeToolArgs`) wird der Record als **einziger Parameter** übergeben. Empfehlung: in `AddFindMagicValues` ein einziger `args: FindMagicValuesToolArgs? = null` als Delegate-Parameter + `ct = default`. Schema-Generierung leitet die Felder aus dem Record ab.
- **`McpCallLog.ExecuteCallAsync`:** `toolName = "find_magic_values"`, `args = $"scope={scope}|valueType={valueType}|category={category}|minOcc={minOccurrences}|max={maxResults}|ignoreNumbers=[{n1,n2}]|tests={includeTests}|supp={includeSuppressed}|changed={changedOnly}"` — bewusst kompakt (max 200 Zeichen + `...`, siehe `Docs/agent-api.md` §Call-Log), keine geheimen Werte loggen (in EPIC-2 mit `security_candidates` wird das nochmal geprüft).
- **`Task.Run`-Wrapper:** `SearchPatternTool.cs:56-58` zeigt das Muster. Der CPU-bound Magic-Value-Scan iteriert über alle `.cs`-Documents (kann bei großen Solutions spürbar dauern) → `Task.Run` ist Pflicht, damit `McpCodeGraphServer` während des Scans für andere Tool-Calls freigibt. CT-Weitergabe nicht vergessen.
- **`#nullable enable`:** am Dateianfang jeder neuen `.cs`-Datei (linter-required, sonst Compile-Error wegen `TreatWarningsAsErrors`).
- **`internal sealed record`:** für `FindMagicValuesToolArgs`, `FindMagicValuesResult`, `FindMagicValuesPayload`, `MagicValueEntry`, `MagicValuesSummary`, `MagicValueCategory` (Enum, kein record), `MagicValueClassification`. Records sind Records (Immutability), sealed weil kein Erweiterungs-Bedarf in EPIC-1.
- **Heuristik-Konservatismus:** Wenn unsicher, lieber **keinen** Treffer melden als einen falschen. Der `categoryFilter` und `ignoreNumbers` erlauben dem Aufrufer eine zweite Stufe; im ersten Pass Heuristiken eng. `localization_candidates` braucht z. B. Heuristik „Exception-Message-Methoden + String-Länge > 15" — in EPIC-1 NICHT implementiert, in EPIC-2.
- **Raw-String-Literal-Erkennung:** `LiteralExpressionSyntax` deckt Raw String Literals automatisch ab (sie sind `LiteralExpressionSyntax` mit `Kind == SyntaxKind.Utf8StringLiteralExpression` oder `Kind == SyntaxKind.StringLiteralExpression`, je nach Variante). Kein separater Walker nötig.
- **`InterpolatedStringExpressionSyntax`:** für EPIC-1 nur die statischen `InterpolatedStringText`-Segmente verarbeiten (siehe Konzept §„Wie" Punkt 1). Dynamische Segmente (`{x}`) werden übersprungen — sonst müssten wir die Werte zur Laufzeit auflösen, was für ein On-Demand-Audit zu teuer und semantisch fragwürdig wäre.
- **Keine `// ainetlinter-disable MagicValues`-Erkennung in EPIC-1:** Bewusst weggelassen — Suppression-Logik via `SyntaxTrivia`-Walk ist Teil von EPIC-2 (siehe `konzept.md` §„Verworfene Alternativen" für die Begründung der pro-Fundstelle-Granularität). `includeSuppressed: false` ist in EPIC-1 faktisch ein No-op.
- **Doku-Sync-Reihenfolge:** `Docs/agent-api.md` und `IsErrorPolicy.md` müssen **vor** dem Test-Gate aktualisiert sein, sonst flake-Protection-Tests auf Tool-Anzahl (z. B. `OverviewResourceRegistrationTests`) können fehlschlagen. Reihenfolge im Step: Code → Doku → Build → Tests.
- **Commit-Vorschlag (vom Coder, nicht vom Planer):** `feat: find_magic_values MCP-Tool mit Basis-Klassifizierung [magic-values-in-mcp]\n\nRefs: tasks/magic-values-in-mcp/step-001`
