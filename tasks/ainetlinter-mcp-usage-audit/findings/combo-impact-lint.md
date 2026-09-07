# Finding: Kombination `get_impact` → `get_violations` → `metrics_lookup`

Audit-Stand: 2026-09-07. Live-Kette über `CallDynamicTool` / `user-AiNetLinter`. Nur diese drei Tools plus Schema-Lookup. Kein Build, kein Test.

Anker: `T:Sample.Project.Infrastructure.Sql.DataExecutor` (`Sample.Project/Infrastructure/Data/DataExecutor.cs`). Folge-IDs/Pfade ausschließlich aus den Antworten weitergereicht (plus der ursprüngliche Anker, weil `callers` keine Symbol-IDs liefert).

## 1. Schema-Kurzfazit

Die drei Beschreibungen suggerieren eine Agenten-Kette „Änderung prüfen → Lint der betroffenen Dateien → Metriken der betroffenen Symbole“. Das JSON-Schema unterstützt das **nicht als Vertrag**:

- `get_impact` (`callers`, Default): Trefferliste ist `Datei:Zeile - Aufruf von 'Name'`. **Keine** `T:`/`M:`-IDs, kein Testfilter. `change-context` (nur Git-Diff) nennt geänderte Symbole als Klartext plus `Datei:Start-Ende`, eingebettete Violations und `dotnet test --filter`. `detailLevel`/`gitRef` vs. `symbolIdentifier` stehen nur in der Prosa.
- `get_violations`: `scopeFilter`/`scope`/`path` = Projektname oder **Pfad-Substring**. Kein Symbol-Parameter. Extra-Felder (`gitRef`, `detailLevel`) sind im Schema nicht deklariert; live **still ignoriert**. Assembly unsupported.
- `metrics_lookup`: braucht `symbolIdentifiers` / `symbolIdentifier` / `symbol` (DocCommentId, `Datei.cs:Zeile`, `Datei.cs:Zeile:Spalte`, FQ-Name). **Kein** `Datei:Start-Ende`-Span. Dateiname ohne Zeile ist kein gültiger Identifikator.

Folge: Ein Agent, der Impact-Zeilen 1:1 in `path` oder als Span in `metrics_lookup` kopiert, trifft Schema-Lücken, die keine der drei Beschreibungen als Kombinationsformat benennt. `get_impact` Truncation-Footer („Pattern verfeinern“) gehört zu einem anderen Tool.

## 2. Ausgeführte Calls

Projekt: `targetType=project`, `targetPath=C:\Workspace\Sample.Project`. Wartezeit überall **schnell**, kein Timeout.

Kette A — Symbol-Impact (`callers`) → Lint der Trefferpfade → Metriken der weitergereichten Loci:

| # | Tool | Parameter (aus Vorantwort) | Antwort | Truncation / completeness | Größe (grob) |
|---|------|----------------------------|---------|---------------------------|--------------|
| A1 | `get_impact` | `symbolIdentifier=T:…DataExecutor`, `detailLevel=callers`, `maxResults=50` | 246 Treffer, 50 gezeigt; **nur** Tests.Ama/Components/Logic; Format `rel/Pfad.cs:Zeile` | ja; Footer „Pattern verfeinern oder maxResults erhöhen“ | ~8 KB / ~51 Zeilen |
| A2 | `get_violations` | `scopeFilter=` erster Impact-Pfad `…/AdminAiExplorationPlatformSupport.cs` (ohne Zeile) | 0 Verstöße in **1** Datei | Hinweis „vollständig“ | ~0,3 KB |
| A3 | `get_violations` | `scopeFilter=DataExecutor.cs` (Ankerdatei, in A1 **nicht** sichtbar) | 0 Verstöße in 1 Datei | vollständig | ~0,3 KB |
| A4 | `get_violations` | `scopeFilter=Sample.Project.Tests.Ama` (Projektname aus A1) | 0 Verstöße in **81** Dateien | vollständig | ~0,3 KB |
| A5 | `get_violations` | Alias `path=` naiv **inkl.** `:21` aus A1 | **Keine Dateien im Scope** | vollständig (überclaimt) | ~0,3 KB |
| A6 | `get_violations` | `scopeFilter=…/Infrastructure/Sql`, `minSeverity=warning`, `includeSnippet=true` | 0 Verstöße in 23 Dateien | vollständig | ~0,4 KB |
| A7 | `metrics_lookup` | `symbolIdentifier=T:…DataExecutor` (Anker, nicht aus A1-Liste) | NamedType LOC 479 OK; Footprint **0**; Public Member **18/15 [VIOLATION]** `MaxPublicMembersPerType`; Id `T:…DataExecutor`; Ort `DataExecutor.cs:9-9` | vollständig | ~0,9 KB |
| A8 | `metrics_lookup` | `symbolIdentifier=` Impact-Zeile `…/AdminAiExplorationPlatformSupport.cs:21` | **Parameter** `DataExecutor executor`, LOC **1**, kein Regel-Status außer OK, **keine** Id | vollständig | ~0,4 KB |
| A9 | `metrics_lookup` | Batch: `AdminAiExplorationPlatformSupport.cs:21`, `…/BuchungsserverSqlTestSupport.cs:43`, `DataExecutor.cs` | Parameter LOC 1; Methode `EnsureTanProbeAsync` mit `M:`-Id; **`DataExecutor.cs` → SYMBOL_NOT_FOUND** | vollständig | ~1,5 KB |

Kette B — Git-`change-context` → Lint der Commit-Dateien → Metriken der Commit-Symbole:

| # | Tool | Parameter | Antwort | Truncation / completeness | Größe (grob) |
|---|------|-----------|---------|---------------------------|--------------|
| B1 | `get_impact` | `detailLevel=change-context` (kein `gitRef`) | `Kein Git-Repository oder leerer Diff` | completeness-Hinweis | ~0,3 KB |
| B2 | `get_impact` | `change-context`, `gitRef=HEAD~1`, `maxChangedSymbols=8`, `maxTestsPerSymbol=3` | 4 Dateien, 4/4 Symbole, 0 Aufrufstellen, 4 Tests, **0 Violations**; Klartext-Methodennamen; Spans `…cs:9-13` bzw. `:90-102`; `dotnet test … --filter FullyQualifiedName~…` | unter Cap; vollständig | ~1,5 KB / ~12 Zeilen |
| B3 | `get_violations` | `scopeFilter=` B2-Pfad `…/ReleaseHttpStatusTests.cs` | 0 Verstöße in 1 Datei | vollständig | ~0,3 KB |
| B4 | `metrics_lookup` | Batch aus B2: Klartext-Methodenname; `…cs:9`; `…cs:9-13` | Name → Methode + `M:`-Id, LOC 5 OK; **`:9` SYMBOL_NOT_FOUND**; **`:9-13` SYMBOL_NOT_FOUND** | vollständig | ~1,2 KB |
| B5 | `metrics_lookup` | `…/ReleaseHttpStatusTests.cs:10` (Zeile der Auflösung in B4, nicht der Span-Start) | dieselbe Methode wie B4-Name | vollständig | ~0,7 KB |

Nach A7 (Metrik-VIOLATION) Gegenprobe Lint-Regel und umgekehrte ID-Gabe Violation → Metrik:

| # | Tool | Parameter | Antwort | Größe |
|---|------|-----------|---------|-------|
| C1 | `get_violations` | `scopeFilter=DataExecutor.cs`, `minSeverity=info` | 0 | ~0,3 KB |
| C2 | `get_violations` | `scopeFilter=DataExecutor.cs`, `ruleId=MaxPublicMembersPerType` | 0 | ~0,3 KB |
| C3 | `get_violations` | **kein** Scope (Absicht: Impact-Parameter `gitRef`/`detailLevel` versehentlich mitgegeben) | Extra-Felder ignoriert; **1** Warning Solution-weit: `SetupText.cs:8` `MaxPublicMembersPerType` 19/15; Kopf „1 Verstoesse in 2287 Dateien“ | ~0,8 KB |
| C4 | `get_violations` | `ruleId=MaxPublicMembersPerType` unscoped, `maxResults=5` | nur `SetupText` (DataExecutor **fehlt**) | ~0,7 KB |
| C5 | `get_violations` | `scopeFilter=SetupText.cs`, `includeSnippet=true` | 1 Warning, Snippet Zeile 8 `public static class SetupText` | ~0,9 KB |
| C6 | `metrics_lookup` | `Sample.Project.Setup/Setup/SetupText.cs:8` (aus C3/C5) | NamedType, Public **19/15 [VIOLATION]**, Id `T:…SetupText`, Footprint 235 | ~1,0 KB |

## 3. Verdict

**Abweichend / teilweise.** Die Kette ist als Arbeitsablauf denkbar, aber die drei Tools sprechen **keine gemeinsame ID-Sprache** und **dieselbe Regel nicht mit derselben Wahrheit**.

- `callers` → Violations: Pfad **ohne** Zeile als `scopeFilter` trifft Dateien; **mit** `:Zeile` (1:1-Kopie) trifft **nichts**.
- `callers` → Metriken: `Datei:Zeile` löst oft das **kleinste** Symbol (Parameter, LOC 1), nicht den Impact-Anker und nicht die aufrufende Methode. Dateiname ohne Zeile (`DataExecutor.cs`) ist `SYMBOL_NOT_FOUND`.
- `change-context` → Violations: relativer Pfad ohne Span funktioniert; eingebettete „0 Violations“ stimmte für HEAD~1 mit `get_violations` auf derselben Datei überein.
- `change-context` → Metriken: Klartext-Methodenname funktioniert und **erzeugt erst dann** eine `M:`-Id. Der gelieferte Span `Datei:9-13` und die Startzeile `:9` sind **keine** gültigen `metrics_lookup`-Identifikatoren; die Methode sitzt auf Zeile 10.
- **Widerspruch Anker:** `metrics_lookup` markiert `DataExecutor` als `MaxPublicMembersPerType`-**VIOLATION** (18 > 15). `get_violations` auf genau dieser Datei, mit `minSeverity=info` und mit `ruleId=MaxPublicMembersPerType` Solution-weit, listet **nur** `SetupText` (19 > 15), nicht `DataExecutor`. Dieselbe Regel, zwei Tools, zwei Welten. Bei `SetupText` stimmen Lint und Metrik überein.

## 4. Schwere

`degraded`

Begründung: Kein Crash, keine dauerhaft leere Session. Mit Workarounds (T-Id behalten, Zeile vom Span abstreifen, Klartextname statt Span, `maxResults` hoch) kommt ein Agent zu Teilergebnissen. Für den dokumentierten Ablauf „Impact vor Tests / nach Edit linten / Metriken prüfen“ ist das riskant: Default-`callers` blendet Produktion aus, IDs fehlen, eine Metrik-VIOLATION ohne Lint-Treffer kann Edit oder Falsch-Entwarnung auslösen. Nicht `broken`, weil `change-context` plus Pfad-Substring plus `SetupText` als Gegenprobe konsistent war.

## 5. Nutzbarkeit

**nur mit Workaround**

Workaround:

1. `get_impact` mit voller `T:`-Id und `maxResults` ≥ Gesamttreffer, sonst nur Testhits.
2. Für `get_violations` aus Impact-Zeilen **nur den Pfad vor dem ersten `:Ziffer`** als `scopeFilter` übernehmen — nie `Datei.cs:21`.
3. `metrics_lookup` nicht mit Impact-`callers`-Zeilen füttern, wenn der Typ gemeint ist (sonst Parameter-LOC-1). Anker-`T:`-Id wiederverwenden. Aus `change-context` den **Methodennamen** nehmen, nicht `Datei:Start-Ende`.
4. `[VIOLATION]` in `metrics_lookup` nicht mit „steht in `get_violations`“ gleichsetzen; Gegenprobe per `ruleId` + Dateiscope bleibt Pflicht.
5. Leeren `change-context` nicht als „kein Git“ lesen, wenn das Repo existiert (Working-Tree vs. HEAD). Extra-Parameter an `get_violations` nicht als Moduswechsel erwarten.

Ohne das: Kette A endet bei 0 Lint auf Testsupport und einer Parameter-Metrik; die DataExecutor-Typverletzung sieht nur, wer den Anker separat nachschlägt — und selbst dann lügt die Lint-Liste.

## 6. Bugs

Reproduzierbar, mit Call:

1. **Regel-Widerspruch `MaxPublicMembersPerType` / `DataExecutor`.** A7: Metrik 18 public, Status `[VIOLATION]`, Limit 15. C1/C2/C4: Lint 0 auf `DataExecutor.cs` und Solution-weit nur `SetupText`. Ground Truth der Zählung hier nicht per Dateilesen verifiziert; der **Tool-Widerspruch** ist unabhängig davon ein Agenten-Bug (welche Antwort ist die Lint-Wahrheit?).
2. **Impact-`callers` ohne Folge-IDs.** A1 liefert 50 Zeilen ohne `T:`/`M:`. A8 zeigt, dass die naheliegende Weitergabe `Datei:Zeile` das **falsche Symbol** trifft (Parameter statt Typ/Methode).
3. **`path`/`scopeFilter` + `:Zeile`.** A5: 0 indexierte Dateien. A2 ohne Suffix: 1 Datei. Dieselbe Impact-Zelle, zwei Semantiken.
4. **`change-context`-Span ist kein `metrics_lookup`-Key.** B2 `…cs:9-13` / B4 `:9` → `SYMBOL_NOT_FOUND`. B5 `:10` und der Klartextname treffen. Beschreibung von `metrics_lookup` erwähnt `Datei.cs:Zeile`, nicht Spans; `get_impact` dokumentiert die Übergabe nicht.
5. **`DataExecutor.cs` ohne Zeile.** A9: `SYMBOL_NOT_FOUND` in `metrics_lookup`, während derselbe String in `get_violations` eine Datei scoped (A3).
6. **Truncation-Hint von `get_impact`.** A1: „Pattern verfeinern“ — `get_impact` hat kein `pattern`. Agent kann den nächsten Call falsch wählen statt `maxResults` zu erhöhen.
7. **Default-50 blendet den Anker aus.** A1 zeigt keine Produktionsdatei `Infrastructure/Data/DataExecutor.cs`. Agent, der nur die Liste an B2/A2 weiterreicht, lintet nie die geänderte Typdatei.
8. **Stille Extra-Parameter.** C3: `gitRef`/`detailLevel` an `get_violations` werden geschluckt; Antwort ist Solution-Lint, nicht Impact. Kein `INVALID_ARGUMENT`.
9. **Leer-Diff-Text.** B1: „Kein Git-Repository oder leerer Diff“ trotz vorhandenem Repo (uncommittete tracked Änderungen leer; Untracked aus git-status zählen offenbar nicht).

False Positive / False Negative vs. Ist (Stichprobe, kein Vollabgleich):

| Probe | MCP | Ist / anderes Tool | Urteil |
|-------|-----|--------------------|--------|
| A2 Lint auf erster Caller-Datei | 0 | Testdatei, relaxierte Testregeln plausibel | kein Widerspruch zu Metrik-Parameter LOC 1 |
| B2 vs B3 HEAD~1 | Impact 0 Violations; Lint 0 | gleiche Datei | **einig** |
| C5 vs C6 `SetupText` | Lint 19/15; Metrik 19/15 `[VIOLATION]` | gleiche Regel/Datei/Zeile 8 | **einig** |
| A7 vs C4 `DataExecutor` | Metrik VIOLATION; Lint fehlt in 2287 Dateien | gleiche Regel, anderer Typ | **Widerspruch** |
| A8 Caller-Zeile 21 | Parameter | Impact-Text „Aufruf von DataExecutor“ | semantisch FP für „Impact des Typs“ |

Kein Crash. `isError` nur als per-item `SYMBOL_NOT_FOUND` in Batch A9/B4, Rest Markdown-Erfolg.

## 7. Token / IDs

- A1 allein ~8 KB bei 50/246; Rest der Kette ist klein, **solange** der Agent nicht 50 Dateien einzeln an `get_violations` und 50 Zeilen an `metrics_lookup` schickt. Ungedrosselt: 50× Lint-Kopf + 50× Metrik-Tabellen, ohne dass Produktion vorkommt.
- Completeness-Hinweise fast immer „vollständig“, auch bei A5 (0 Dateien) und A1 (explizit unvollständig). Agent kann die Kette für geschlossen halten.
- Stabile IDs: `get_impact` `callers` **keine**. `change-context` **keine** DocCommentIds, nur Klartext + Span. `get_violations` **keine** Symbol-IDs, Folgekey = Relativpfad + Zeile + Regelname. `metrics_lookup` **gibt** `T:`/`M:` zurück — aber nur, wenn die Eingabe schon auflösbar war. Die `M:`-Id aus B4 ist der erste wirklich kettenfähige Schlüssel, und sie entsteht erst **nach** einem Rate-Call.
- StructuredContent (`MetricsLookupBatchDto`) in dieser Oberfläche nicht sichtbar; nur Markdown.
- Doppelte Token-Last: dieselbe Datei kann in Impact (50 Zeilen), Lint-Kopf und Metrik-Tabelle dreimal erzählt werden, ohne gemeinsame Dedup-Id.

## 8. Roslyn-konforme Wünsche

Statisch/deterministisch, ohne LLM:

1. Gemeinsames Folge-ID-Format: jede Impact-Zeile mit kopierbarer `T:`/`M:`-Id **und** Relativpfad ohne Span; `get_violations` akzeptiert dieselben Pfade; `metrics_lookup` akzeptiert dieselben Ids.
2. `get_impact` `callers`: Truncation-Footer an `maxResults` koppeln, nicht an `pattern`. Optional Produktions- vs. Test-Split, damit Default-50 den Anker nicht versteckt.
3. `change-context`-Spans nicht als `Datei:9-13` ausgeben, wenn Folgetools nur `Datei:Zeile` (deklarationsgenaue Zeile) verstehen — oder `metrics_lookup` Spans auf das innerste/deklarierende Symbol mappen.
4. `get_violations`: `Datei.cs:21` als Scope entweder auf Datei+Zeile einschränken oder als `INVALID_ARGUMENT` mit Hint „Pfad ohne Zeile“. Extra-Properties ablehnen.
5. **Eine** Schwellwert-Wahrheit: `metrics_lookup`-`[VIOLATION]` nur, wenn dieselbe Regel in `get_violations` für dasselbe Symbol erscheinen würde (Suppressions, Test-Overrides, Compound-Limits identisch anwenden) — oder Status klar als „Roh-Schwellwert, kein Lint-Inventar“ kennzeichnen.
6. Leerer Git-Diff vs. fehlendes Repo getrennte Texte; Untracked in change-context erwähnen oder bewusst als Nicht-Diff dokumentieren.

## 9. Phase 3

Welle 3, nur Nicht-ok-Kettenbrüche. AiNetLinter read-only; Ansatz = statisch/Roslyn.

| Kettenbefund | Bruch | Pfad + Symbol | Roslyn-Ansatz |
|---|---|---|---|
| `callers` ohne Folge-`T:`/`M:`; `Datei:Zeile` trifft Parameter (A8) | ID-Handover | `src\AiNetLinter\Core\DiffImpactAnalyzer.cs` `FormatCallSite` / `FindCallSiteEntriesAsync`; `CallSiteEntry` ohne Caller-Id; `src\AiNetLinter\Mcp\Tools\SymbolGraph\FindReferencesTool.cs` `ResolveByLineAsync` → `SymbolIdentifierResolver.ResolveSymbolsOnLine` (jedes Token der Zeile, inkl. `IParameterSymbol`) | Impact-Zeile: Relativpfad **und** `` `M:` ``/`T:` des Enclosing-Members (`GetEnclosingSymbol` + `CreateDeclarationId`). `ResolveByLineAsync`: Member-Deklaration vor Parameter; oder Impact gibt die Id, nicht die Nutzungszeile, als Folgekey aus. |
| `path`/`scopeFilter` inkl. `:21` → 0 Dateien (A5); ohne Suffix 1 Datei (A2) | ID-Handover | `src\AiNetLinter\Mcp\Tools\Analysis\ViolationScopeFilter.cs` `MatchesScope`; `src\AiNetLinter\Output\PathNormalizer.cs` `MatchesScope` (`Contains` auf Pfad, `:Zeile` nie im Dateipfad); `GetViolationsScanner.FormatReport` | Vor dem Match `:digits` am Ende als Zeile abstreifen **oder** `INVALID_ARGUMENT` „Pfad ohne Zeile“. Optional danach auf `RuleViolation.LineNumber` filtern. |
| `change-context`-Span `Datei:9-13` / Start `:9` → `SYMBOL_NOT_FOUND` (B4); Name und `:10` treffen | ID-Handover | `GetImpactTool.FormatSymbolLine` (`FilePath:StartLine-EndLine`); `SymbolIdentifierResolver.TryParsePosition` / `TryParseLineOnlyPosition` (letztes `:`-Segment muss `int` sein, `9-13` scheitert); `FindReferencesTool.ResolveSymbolCoreAsync` fällt auf `ResolveByNameAsync` | Entweder Span nicht emittieren (deklarationsgenaue Zeile + `` `M:` `` aus `ChangedSymbolPayload`) **oder** Resolver `Datei:Start-Ende` auf innerstes deklarierendes Symbol im Span mappen (`FindNode` + `GetDeclaredSymbol`). |
| `DataExecutor.cs` ohne Zeile → `SYMBOL_NOT_FOUND` in `metrics_lookup` (A9), in `get_violations` Dateiscope | ID-Handover | `FindReferencesTool.ResolveSymbolCoreAsync` (kein Datei-ohne-Zeile-Zweig); `MetricsLookupTool.RenderSingleLookupAsync`; `ViolationScopeFilter.MatchesScope` (Substring-Dateiname) | Gleiches Pfadkontrakt: Dateiname allein = Dateiscope für Lint, für Metriken `INVALID_ARGUMENT` mit Hint `T:` oder `Datei.cs:Zeile` — nicht still `SYMBOL_NOT_FOUND` als fehlendes Symbol. |
| `MaxPublicMembersPerType`: Metrik VIOLATION 18/15 (`DataExecutor`), Lint nur `SetupText` | Formatter | `MetricsLookupScanner.ScanType` (`INamedTypeSymbol.GetMembers()` = Partial-Union); `src\AiNetLinter\Core\Checkers\PublicMembersChecker.CountPublicMembers` (nur `TypeDeclarationSyntax.Members` **einer** Partial-Datei); `GetViolationsScanner.FilterAndSortViolations`; `FeatureContext` dieselbe Schere | Eine Zählung: Union über `GetMembers()` im Checker **oder** Metrik per Syntax-Datei wie der Lint. `CheckThreshold` ohne Compound-Suppressions angleichen an `CompoundSuppressionEvaluator`. Status `[VIOLATION]` nur, wenn dieselbe Regel in `get_violations` erscheinen würde. |
| A1-Footer „Pattern verfeinern“ — `get_impact` hat kein `pattern` | Truncation | `src\AiNetLinter\Mcp\McpTruncation.cs` `TruncateLines`; `TransitiveCallGraphFormatter.CreateMaxResultsMessage`; `GetImpactTool.ExecuteSymbolBranchAsync` | Tool-spezifische Meta-Zeile: `maxResults erhöhen` (kein `pattern`). `McpTruncation` parametrisieren oder Impact eigenen Footer. |
| Default-50 blendet Produktionsanker / Testhits zuerst | Truncation | `CallGraphTraversal.AppendReferenceLocations` (ungeordnetes `SymbolFinder`-Präfix); `McpTruncation.TruncateLines` Take-first; `GetImpactTool` `effectiveMax` | Produktion vor Tests sortieren (`TestDetector.IsTestFile`); Ankerdatei nicht hinter Cap verstecken. Offset statt Präfix wiederholen. |
| Extra `gitRef`/`detailLevel` an `get_violations` still ignoriert → Solution-Lint (C3) | Formatter | `src\AiNetLinter\Mcp\Registration\AnalysisToolRegistrations.cs` `AddGetViolations` (Lambda ohne diese Parameter; MCP-JSON droppt Unmapped) | Unbekannte Keys `INVALID_ARGUMENT` oder in der Beschreibung „keine Impact-Aliase“. Nicht als Moduswechsel lesen. |
| B1 „Kein Git-Repository oder leerer Diff“ trotz Repo | Formatter | `GetImpactTool.ExecuteChangeContextBranchAsync` (ein Text für `analysis is null`); `DiffImpactAnalyzer.AnalyzeDiffAsync` / `RunAnalysisAsync` (kein Repo **und** leerer Diff → `null`) | Getrennte ReasonCodes: `NO_GIT_REPOSITORY` vs. `EMPTY_DIFF`. Untracked erwähnen oder bewusst als Nicht-Diff dokumentieren. |
| Completeness „vollständig“ bei A5 (0 Dateien) | Formatter | `GetViolationsScanner.FormatReport` Early-Return ohne Truncation-Flag; `GetViolationsTool.ExecuteAsync` hängt `McpSufficiencyHints.Append` an nicht-trunkierte Texte | 0 Dateien im Scope = nicht `complete` für die Kette; Hint „`:Zeile` entfernen“ statt Sufficiency. |

Gegenprobe: dieselbe `T:`-Id an Impact → Violations (Pfad ohne Span) → `metrics_lookup` (`T:` oder `M:` aus Impact); `DataExecutor` Metrik und Lint dieselbe Wahrheit; Span aus `change-context` entweder auflösbar oder nicht mehr als Identifier ausgegeben.
